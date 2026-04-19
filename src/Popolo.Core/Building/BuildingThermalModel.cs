/* BuildingThermalModel.cs
 * 
 * Copyright (C) 2016 E.Togashi
 * 
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 2 of the License, or (at
 * your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful, but
 * WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 675 Mass Ave, Cambridge, MA 02139, USA.
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Popolo.Core.Building.Envelope;
using Popolo.Core.Climate;
using Popolo.Core.Exceptions;

namespace Popolo.Core.Building
{

  /// <inheritdoc cref="IReadOnlyBuildingThermalModel"/>
  /// <remarks>
  /// <para>
  /// This is the mutable implementation of <see cref="IReadOnlyBuildingThermalModel"/>.
  /// Construct the building by passing an array of <see cref="MultiRoom"/>
  /// instances to the constructor (or build them up incrementally), then drive
  /// the simulation by repeatedly setting outdoor conditions and advancing the
  /// solver. For read-only access (e.g., when handing the model to reporting
  /// components), use the <see cref="IReadOnlyBuildingThermalModel"/>
  /// interface.
  /// </para>
  /// <para>
  /// When <see cref="EnableParallelComputing"/> is true (the default), the
  /// independent <see cref="MultiRoom"/> instances are solved on parallel
  /// tasks using a configurable <see cref="MaxDegreeOfParallelism"/>. The
  /// parallel safety of this scheme depends on the one-time-step lag in
  /// inter-block boundary exchange described in
  /// <see cref="IReadOnlyBuildingThermalModel"/>.
  /// </para>
  /// </remarks>
  public class BuildingThermalModel : IReadOnlyBuildingThermalModel
  {

    #region プロパティ

    /// <summary>Gets or sets a value indicating whether parallel computing is enabled.</summary>
    public static bool EnableParallelComputing { get; set; } = true;

    /// <summary>Gets or sets the maximum degree of parallelism for parallel computation.</summary>
    public static int MaxDegreeOfParallelism { get; set; } = 4;

    /// <summary>Gets or sets a value indicating whether tilted-surface solar irradiance is provided directly.</summary>
    public bool IsSolarIrradianceGiven
    {
      get
      {
        if (mRooms.Length == 0) return false;
        else return mRooms[0].IsSolarIrradianceGiven;
      }
      set
      {
        foreach (MultiRoom ml in mRooms) 
          ml.IsSolarIrradianceGiven = value;
      }
    }

    #endregion

    #region インスタンス変数

    /// <summary>True on the first forecast call after FixState; reset after each FixState.</summary>
    private bool isFirstForecast = true;

    /// <summary>Tracks whether sensible heat boundary conditions have changed for each MultiRooms.</summary>
    private Dictionary<MultiRoom, bool> hasHTChgd = new Dictionary<MultiRoom, bool>();

    /// <summary>Tracks whether moisture boundary conditions have changed for each MultiRooms.</summary>
    private Dictionary<MultiRoom, bool> hasWTChgd = new Dictionary<MultiRoom, bool>();

    /// <summary>All wall assemblies across all MultiRooms instances.</summary>
    internal List<Wall> walls;

    /// <summary>Array of multi-room systems.</summary>
    private MultiRoom[] mRooms;

    /// <summary>Calculation time step [s].</summary>
    private double timeStep = 3600;

    /// <summary>Inter-zone air flow collections, indexed by [MultiRooms index][zone index].</summary>
    private InterZoneAirFlowCollection[][] zoneVent;

    #endregion

    #region プロパティ

    /// <summary>Gets the array of multi-room systems.</summary>
    public IReadOnlyMultiRoom[] MultiRoom { get { return mRooms; } }

    /// <summary>Gets the current simulation date and time.</summary>
    public DateTime CurrentDateTime { get; private set; }

    /// <summary>Gets or sets the calculation time step [s].</summary>
    public double TimeStep
    {
      get { return timeStep; }
      set
      {
        for (int i = 0; i < mRooms.Length; i++) mRooms[i].TimeStep = value;
        timeStep = value;
      }
    }

    /// <summary>Gets the solar state.</summary>
    public IReadOnlySun Sun { get; private set; } = null!;

    /// <summary>Gets the outdoor dry-bulb temperature [°C].</summary>
    public double OutdoorTemperature { get; private set; }

    /// <summary>Gets the outdoor humidity ratio [kg/kg].</summary>
    public double OutdoorHumidityRatio { get; private set; }

    /// <summary>Gets the nocturnal (long-wave) radiation [W/m²].</summary>
    public double NocturnalRadiation { get; private set; }

    #endregion

    #region コンストラクタ

    /// <summary>Initializes a new instance.</summary>
    /// <param name="mRooms">Array of multi-room systems.</param>
    public BuildingThermalModel(MultiRoom[] mRooms)
    {
      this.mRooms = mRooms;
      zoneVent = new InterZoneAirFlowCollection[mRooms.Length][];
      walls = new List<Wall>();
      List<IReadOnlyZone> zns = new List<IReadOnlyZone>();
      List<IReadOnlyWindow> wins = new List<IReadOnlyWindow>();
      for (int i = 0; i < mRooms.Length; i++)
      {
        zoneVent[i] = new InterZoneAirFlowCollection[mRooms[i].ZoneCount];
        for (int j = 0; j < zoneVent[i].Length; j++) zoneVent[i][j] = new InterZoneAirFlowCollection();
        hasHTChgd.Add(mRooms[i], true);
        hasWTChgd.Add(mRooms[i], true);

        foreach (Wall wl in mRooms[i].Walls)
          if (!walls.Contains(wl)) walls.Add(wl);

        //ゾーンの重複確認
        foreach (IReadOnlyZone zn in mRooms[i].Zones)
          if (zns.Contains(zn)) throw new PopoloArgumentException("A zone belongs to more than one MultiRooms instance.", "mRooms");
        zns.AddRange(mRooms[i].Zones);

        //窓の重複確認
        foreach (IReadOnlyWindow win in mRooms[i].Windows)
          if (wins.Contains(win)) throw new PopoloArgumentException("A window belongs to more than one MultiRooms instance.", "mRooms");
        wins.AddRange(mRooms[i].Windows);
      }
    }

    #endregion

    #region 熱平衡更新処理

    /// <summary>Forecasts the future sensible heat balance state.</summary>
    /// <remarks>
    /// May be called multiple times iteratively.
    /// Call <see cref="FixState"/> afterwards to commit the result.
    /// </remarks>
    public void ForecastHeatTransfer()
    {
      //初回計算時に他のゾーン温湿度を境界条件に設定
      if (isFirstForecast)
      {
        SetInterZoneAirFlow();
        isFirstForecast = false;
      }

      if (EnableParallelComputing)
      {
        ParallelOptions options = new ParallelOptions();
        options.MaxDegreeOfParallelism = MaxDegreeOfParallelism;
        Parallel.ForEach(mRooms, options, mr =>
        {
          if (hasHTChgd[mr])
          {
            mr.ForecastHeatTransfer();
            hasHTChgd[mr] = false;
          }
        });
      }
      else
      {
        foreach (MultiRoom mr in mRooms)
        {
          if (hasHTChgd[mr])
          {
            mr.ForecastHeatTransfer();
            hasHTChgd[mr] = false;
          }
        }
      }
    }

    /// <summary>Forecasts the future moisture balance state.</summary>
    /// <remarks>
    /// May be called multiple times iteratively.
    /// Call <see cref="FixState"/> afterwards to commit the result.
    /// </remarks>
    public void ForecastWaterTransfer()
    {
      //初回計算時に他のゾーン温湿度を境界条件に設定
      if (isFirstForecast)
      {
        SetInterZoneAirFlow();
        isFirstForecast = false;
      }

      foreach (MultiRoom mr in mRooms)
      {
        if (!mr.SolveMoistureTransferSimultaneously)
        {
          if (hasWTChgd[mr])
          {
            mr.ForecastMoistureTransfer();
            hasWTChgd[mr] = false;
          }
        }
      }
    }

    /// <summary>Commits the forecasted state and advances the wall heat transfer by one time step.</summary>
    public void FixState()
    {
      foreach (MultiRoom mr in mRooms)
      {
        mr.FixHeatTransfer();
        mr.FixMoistureTransfer();
        hasHTChgd[mr] = hasWTChgd[mr] = true;
      }
      //壁の熱流を更新
      foreach (Wall wl in walls)
      {
        wl.invMatrixUpdated = false;
        wl.Update();
      }
      isFirstForecast = true;
    }

    /// <summary>Reverts zone temperatures and humidity ratios from forecast values to current values.</summary>
    public void ResetAirState()
    {
      foreach (MultiRoom mr in mRooms)
      {
        mr.ResetAirState();
        hasHTChgd[mr] = hasWTChgd[mr] = true;
      }
    }

    /// <summary>Sets inter-zone air flow conditions from the zoneVent collection.</summary>
    private void SetInterZoneAirFlow()
    {
      for (int i = 0; i < zoneVent.Length; i++)
      {
        for (int j = 0; j < zoneVent[i].Length; j++)
        {
          double afsum, tsa, wsa;
          afsum = tsa = wsa = 0;
          foreach (InterZoneAirFlow zaf in zoneVent[i][j])
          {
            afsum += zaf.aFlow;
            IReadOnlyZone zn = mRooms[zaf.rmIndex].Zones[zaf.znIndex];
            tsa += zn.Temperature * zaf.aFlow;
            wsa += zn.HumidityRatio * zaf.aFlow;
          }
          if (afsum != 0)
          {
            tsa /= afsum;
            wsa /= afsum;
          }
          mRooms[i].SetSupplyAir2(j, tsa, wsa, afsum);
        }
      }
    }

    /// <summary>Updates the heat and moisture balance while respecting HVAC capacity limits.</summary>
    public void UpdateHeatTransferWithinCapacityLimit()
    {
      List<int> mrIndex = new List<int>();
      List<int> znIndex = new List<int>();
      List<bool> isDBTSP = new List<bool>();
      List<double> sPoint = new List<double>();

      while (true)
      {
        ForecastHeatTransfer();
        ForecastWaterTransfer();

        //過負荷系統の制御を解除する
        bool hasOverLoad = false;
        for (int i = 0; i < MultiRoom.Length; i++)
        {
          for (int j = 0; j < MultiRoom[i].ZoneCount; j++)
          {
            IReadOnlyZone zn = MultiRoom[i].Zones[j];
            if (zn.TemperatureControlled && //温度制御をしている場合で
              (zn.HeatingCapacity < zn.HeatSupply || zn.HeatSupply < -zn.CoolingCapacity)) //加熱・冷却能力を超えてしまった場合
            {
              mrIndex.Add(i);
              znIndex.Add(j);
              isDBTSP.Add(true);
              sPoint.Add(zn.Temperature);
              hasOverLoad = true;
              //加熱・冷却能力以内におさめる
              ControlHeatSupply(i, j, 0 < zn.HeatSupply ? zn.HeatingCapacity : -zn.CoolingCapacity);
            }
            if (zn.HumidityControlled && //湿度制御をしている場合で
              (zn.HumidifyingCapacity < zn.MoistureSupply || zn.MoistureSupply < -zn.DehumidifyingCapacity)) //加湿・除湿能力を超えてしまった場合
            {
              mrIndex.Add(i);
              znIndex.Add(j);
              isDBTSP.Add(false);
              sPoint.Add(zn.HumidityRatio);
              hasOverLoad = true;
              //加湿・除湿能力以内におさめる
              ControlMoistureSupply(i, j, 0 < zn.MoistureSupply ? zn.HumidifyingCapacity : -zn.DehumidifyingCapacity);
            }
          }
        }
        if (!hasOverLoad) break;
      }
      //状態を確定
      FixState();

      //制御解除した系統をもとに戻す
      for (int i = 0; i < mrIndex.Count; i++)
      {
        if (isDBTSP[i])
          ControlDryBulbTemperature(mrIndex[i], znIndex[i], sPoint[i]);
        else
          ControlHumidityRatio(mrIndex[i], znIndex[i], sPoint[i]);
      }
    }

    /// <summary>Gets the breakdown of sensible heat flows into the zone (positive = inflow).</summary>
    /// <param name="mRoomIndex">MultiRooms index.</param>
    /// <param name="zoneIndex">Zone index.</param>
    /// <param name="wallSurfaces">Heat flow from wall and window surfaces [W].</param>
    /// <param name="zoneAirChange">Inter-zone ventilation heat flow [W].</param>
    /// <param name="outdoorAir">Outdoor air heat flow [W].</param>
    /// <param name="supplyAir">Supply air heat flow [W].</param>
    /// <param name="heatGains">Internal heat gains [W].</param>
    /// <param name="heatSupply">HVAC heat supply [W].</param>
    public void GetBreakdownOfSensibleHeatFlow(
      int mRoomIndex, int zoneIndex,
      out double wallSurfaces, out double zoneAirChange,
      out double outdoorAir, out double supplyAir,
      out double heatGains, out double heatSupply)
    {
      mRooms[mRoomIndex].GetBreakdownOfSensibleHeatFlow(
        zoneIndex,
        out wallSurfaces, out zoneAirChange, out outdoorAir, out supplyAir, out heatGains, out heatSupply);
    }

    /// <summary>Gets the convective heat flow from a wall surface [W].</summary>
    /// <param name="mRoomIndex">MultiRooms index.</param>
    /// <param name="wallIndex">Wall index.</param>
    /// <param name="isSideF">True for the F-side surface; false for the B side.</param>
    /// <returns>Convective heat flow [W].</returns>
    public double GetWallConvectiveHeatFlow(int mRoomIndex, int wallIndex, bool isSideF)
    {
      return mRooms[mRoomIndex].GetWallConvectiveHeatFlow(wallIndex, isSideF);
    }

    /// <summary>Gets the breakdown of moisture flows into the zone (positive = inflow).</summary>
    /// <param name="mRoomIndex">MultiRooms index.</param>
    /// <param name="zoneIndex">Zone index.</param>
    /// <param name="zoneAirChange">Inter-zone ventilation moisture flow [kg/s].</param>
    /// <param name="outdoorAir">Outdoor air moisture flow [kg/s].</param>
    /// <param name="supplyAir">Supply air moisture flow [kg/s].</param>
    /// <param name="moistureGains">Internal moisture gains [kg/s].</param>
    /// <param name="moistureSupply">HVAC moisture supply/removal [kg/s].</param>
    public void GetBreakdownOfLatentHeatFlow(
      int mRoomIndex, int zoneIndex,
      out double zoneAirChange,
      out double outdoorAir, out double supplyAir,
      out double moistureGains, out double moistureSupply)
    {
      mRooms[mRoomIndex].GetBreakdownOfLatentHeatFlow(
        zoneIndex, 
        out zoneAirChange, out outdoorAir, out supplyAir, out moistureGains, out moistureSupply);
    }

    #endregion

    #region 境界条件設定処理

    /// <summary>Initializes wall and zone temperatures and humidity ratios.</summary>
    /// <param name="temperature">Dry-bulb temperature [°C].</param>
    /// <param name="humidityRatio">Humidity ratio [kg/kg].</param>
    public void InitializeAirState(double temperature, double humidityRatio)
    { foreach (MultiRoom ml in mRooms) ml.InitializeAirState(temperature, humidityRatio); }

    /// <summary>Updates outdoor conditions for all multi-room systems.</summary>
    /// <param name="dTime">Current date and time.</param>
    /// <param name="sun">Solar state.</param>
    /// <param name="temperature">Outdoor dry-bulb temperature [°C].</param>
    /// <param name="humidityRatio">Outdoor humidity ratio [kg/kg].</param>
    /// <param name="nocRadiation">Nocturnal radiation [W/m²].</param>
    public void UpdateOutdoorCondition
      (DateTime dTime, IReadOnlySun sun, double temperature, double humidityRatio, double nocRadiation)
    {
      CurrentDateTime = dTime;
      this.Sun = sun;
      OutdoorTemperature = temperature;
      OutdoorHumidityRatio = humidityRatio;
      NocturnalRadiation = nocRadiation;
      foreach (MultiRoom mr in mRooms)
      {
        mr.UpdateOutdoorCondition(dTime, sun, temperature, humidityRatio, nocRadiation);
        hasHTChgd[mr] = hasWTChgd[mr] = true;
      }
    }

    /// <summary>Sets the ground temperature [°C].</summary>
    /// <param name="mRoomIndex">MultiRooms index.</param>
    /// <param name="wallIndex">Wall index.</param>
    /// <param name="isSideF">True for the F side; false for the B side.</param>
    /// <param name="groundTemperature">Ground temperature [°C].</param>
    public void SetGroundTemperature(int mRoomIndex, int wallIndex, bool isSideF, double groundTemperature)
    {
      mRooms[mRoomIndex].SetGroundTemperature(wallIndex, isSideF, groundTemperature);
      hasHTChgd[mRooms[mRoomIndex]] = hasWTChgd[mRooms[mRoomIndex]] = true;
    }

    /// <summary>Sets the ground temperature [°C].</summary>
    /// <param name="groundTemperature">Ground temperature [°C].</param>
    public void SetGroundTemperature(double groundTemperature)
    {
      for (int i = 0; i < mRooms.Length; i++)
        mRooms[i].SetGroundTemperature(groundTemperature);
    }

    /// <summary>Sets the inter-zone air flow rate [kg/s].</summary>
    /// <param name="rmIndex">MultiRooms index.</param>
    /// <param name="znIndex1">Source zone index.</param>
    /// <param name="znIndex2">Destination zone index.</param>
    /// <param name="airFlowRate">Inter-zone air flow rate [kg/s].</param>
    public void SetCrossVentilation(int rmIndex, int znIndex1, int znIndex2, double airFlowRate)
    {
      mRooms[rmIndex].SetCrossVentilation(znIndex1, znIndex2, airFlowRate);
      hasHTChgd[mRooms[rmIndex]] = hasWTChgd[mRooms[rmIndex]] = true;
    }

    /// <summary>Sets the inter-zone air flow rate [kg/s].</summary>
    /// <param name="rmIndex1">Source MultiRooms index.</param>
    /// <param name="znIndex1">Source zone index.</param>
    /// <param name="rmIndex2">Destination MultiRooms index.</param>
    /// <param name="znIndex2">Destination zone index.</param>
    /// <param name="airFlowRate">Inter-zone air flow rate [kg/s].</param>
    public void SetCrossVentilation(int rmIndex1, int znIndex1, int rmIndex2, int znIndex2, double airFlowRate)
    {
      //同一の多数室の場合には連成計算
      if (rmIndex1 == rmIndex2) SetCrossVentilation(rmIndex1, znIndex1, znIndex2, airFlowRate);
      //他の多数室の場合には境界条件として計算
      else
      {
        zoneVent[rmIndex2][znIndex2].AddAirFlow(rmIndex1, znIndex1, airFlowRate);
        zoneVent[rmIndex1][znIndex1].AddAirFlow(rmIndex2, znIndex2, airFlowRate);
      }
    }

    /// <summary>Sets the air flow rate from zone 1 to zone 2 [kg/s].</summary>
    /// <param name="rmIndex1">Source MultiRooms index.</param>
    /// <param name="znIndex1">Source zone index.</param>
    /// <param name="rmIndex2">Destination MultiRooms index.</param>
    /// <param name="znIndex2">Destination zone index.</param>
    /// <param name="airFlowRate">Air flow rate [kg/s].</param>
    public void SetAirFlow(int rmIndex1, int znIndex1, int rmIndex2, int znIndex2, double airFlowRate)
    {
      //同一の多数室の場合には連成計算
      if (rmIndex1 == rmIndex2) SetAirFlow(rmIndex1, znIndex1, znIndex2, airFlowRate);
      //他の多数室の場合には境界条件として計算
      else zoneVent[rmIndex2][znIndex2].AddAirFlow(rmIndex1, znIndex1, airFlowRate);
    }

    /// <summary>Sets the air flow rate from zone 1 to zone 2 [kg/s].</summary>
    /// <param name="mRoomIndex">MultiRooms index.</param>
    /// <param name="zoneIndex1">Source zone index.</param>
    /// <param name="zoneIndex2">Destination zone index.</param>
    /// <param name="airFlowRate">Air flow rate [kg/s].</param>
    /// <remarks>The overall air flow balance across all zones must be maintained by the caller.</remarks>
    public void SetAirFlow(int mRoomIndex, int zoneIndex1, int zoneIndex2, double airFlowRate)
    {
      mRooms[mRoomIndex].SetAirFlow(zoneIndex1, zoneIndex2, airFlowRate);
      hasHTChgd[mRooms[mRoomIndex]] = hasWTChgd[mRooms[mRoomIndex]] = true;
    }

    /// <summary>Sets the water supply conditions for the buried pipe at the specified node.</summary>
    /// <param name="mRoomIndex">MultiRooms index.</param>
    /// <param name="wallIndex">Wall (floor) index.</param>
    /// <param name="mIndex">Node index.</param>
    /// <param name="flowRate">Water mass flow rate [kg/s].</param>
    /// <param name="temperature">Inlet water temperature [°C].</param>
    public void SetBuriedPipeWaterState
      (int mRoomIndex, int wallIndex, int mIndex, double flowRate, double temperature)
    {
      mRooms[mRoomIndex].SetBuriedPipeWaterState(wallIndex, mIndex, flowRate, temperature);
      hasHTChgd[mRooms[mRoomIndex]] = true;
    }

    /// <summary>Sets the supply air conditions for the specified zone.</summary>
    /// <param name="mRoomIndex">MultiRooms index.</param>
    /// <param name="zoneIndex">Zone index.</param>
    /// <param name="saTemperature">Supply air temperature [°C].</param>
    /// <param name="saHumidityRatio">Supply air humidity ratio [kg/kg].</param>
    /// <param name="saFlowRate">Supply air flow rate [kg/s].</param>
    public void SetSupplyAir
      (int mRoomIndex, int zoneIndex, double saTemperature, double saHumidityRatio, double saFlowRate)
    {
      mRooms[mRoomIndex].SetSupplyAir(zoneIndex, saTemperature, saHumidityRatio, saFlowRate);
      hasHTChgd[mRooms[mRoomIndex]] = hasWTChgd[mRooms[mRoomIndex]] = true;
    }

    /// <summary>Sets the ventilation air flow rate for the specified zone [kg/s].</summary>
    /// <param name="mRoomIndex">MultiRooms index.</param>
    /// <param name="zoneIndex">Zone index.</param>
    /// <param name="ventilationRate">Ventilation air flow rate [kg/s].</param>
    public void SetVentilationRate(int mRoomIndex, int zoneIndex, double ventilationRate)
    {
      mRooms[mRoomIndex].SetVentilationRate(zoneIndex, ventilationRate);
      hasHTChgd[mRooms[mRoomIndex]] = hasWTChgd[mRooms[mRoomIndex]] = true;
    }

    /// <summary>Sets the base heat gain values for the specified zone.</summary>
    /// <param name="mRoomIndex">MultiRooms index.</param>
    /// <param name="zoneIndex">Zone index.</param>
    /// <param name="convectiveHeatGain">Convective sensible heat gain [W].</param>
    /// <param name="radiativeHeatGain">Radiative sensible heat gain [W].</param>
    /// <param name="moistureGain">Moisture generation rate [kg/s].</param>
    public void SetBaseHeatGain(int mRoomIndex, int zoneIndex,
      double convectiveHeatGain, double radiativeHeatGain, double moistureGain)
    { mRooms[mRoomIndex].SetBaseHeatGain(zoneIndex, convectiveHeatGain, radiativeHeatGain, moistureGain); }

    /// <summary>Adds a heat gain element to the specified zone.</summary>
    /// <param name="mRoomIndex">MultiRooms index.</param>
    /// <param name="zoneIndex">Zone index.</param>
    /// <param name="heatGain">Heat gain element.</param>
    public void AddHeatGain(int mRoomIndex, int zoneIndex, IHeatGain heatGain)
    { mRooms[mRoomIndex].AddHeatGain(zoneIndex, heatGain); }

    /// <summary>Removes a heat gain element from the specified zone.</summary>
    /// <param name="mRoomIndex">MultiRooms index.</param>
    /// <param name="zoneIndex">Zone index.</param>
    /// <param name="heatGain">Heat gain element.</param>
    public void RemoveHeatGain(int mRoomIndex, int zoneIndex, IHeatGain heatGain)
    { mRooms[mRoomIndex].RemoveHeatGain(zoneIndex, heatGain); }

    /// <summary>Sets the outdoor convective heat transfer coefficient [W/(m²·K)].</summary>
    /// <param name="mRoomIndex">MultiRooms index.</param>
    /// <param name="convectiveCoefficient">Outdoor convective heat transfer coefficient [W/(m²·K)].</param>
    public void SetOutsideConvectiveCoefficient(int mRoomIndex, double convectiveCoefficient)
    { mRooms[mRoomIndex].SetOutsideConvectiveCoefficient(convectiveCoefficient); }

    /// <summary>Sets the indoor convective heat transfer coefficient [W/(m²·K)].</summary>
    /// <param name="mRoomIndex">MultiRooms index.</param>
    /// <param name="convectiveCoefficient">Outdoor convective heat transfer coefficient [W/(m²·K)].</param>
    public void SetInsideConvectiveCoefficient(int mRoomIndex, double convectiveCoefficient)
    { mRooms[mRoomIndex].SetInsideConvectiveCoefficient(convectiveCoefficient); }

    /// <summary>Sets the convective heat transfer coefficient for the specified wall surface [W/(m²·K)].</summary>
    /// <param name="mRoomIndex">MultiRooms index.</param>
    /// <param name="wallIndex">Wall index.</param>
    /// <param name="isSideF">True for the F side; false for the B side.</param>
    /// <param name="convectiveCoefficient">Convective heat transfer coefficient [W/(m²·K)].</param>
    public void SetConvectiveCoefficient(int mRoomIndex, int wallIndex, bool isSideF, double convectiveCoefficient)
    { mRooms[mRoomIndex].SetConvectiveCoefficient(wallIndex, isSideF, convectiveCoefficient); }

    /// <summary>Sets the solar irradiance on the specified wall surface.</summary>
    /// <param name="mRoomIndex">MultiRooms index.</param>
    /// <param name="wallIndex">Wall index.</param>
    /// <param name="directIrradiance">Direct solar irradiance [W/m²].</param>
    /// <param name="diffuseIrradiance">Diffuse solar irradiance [W/m²].</param>
    public void SetWallIrradiance(int mRoomIndex, int wallIndex, double directIrradiance, double diffuseIrradiance)
    { mRooms[mRoomIndex].SetWallIrradiance(wallIndex, directIrradiance, diffuseIrradiance); }

    /// <summary>Sets the solar irradiance on the specified window surface.</summary>
    /// <param name="mRoomIndex">MultiRooms index.</param>
    /// <param name="windowIndex">Window index.</param>
    /// <param name="directIrradiance">Direct solar irradiance [W/m²].</param>
    /// <param name="diffuseIrradiance">Diffuse solar irradiance [W/m²].</param>
    public void SetWindowIrradiance(int mRoomIndex, int windowIndex, double directIrradiance, double diffuseIrradiance)
    { mRooms[mRoomIndex].SetWindowIrradiance(windowIndex, directIrradiance, diffuseIrradiance); }

    #endregion

    #region 制御関連の処理

    /// <summary>Enables dry-bulb temperature control for the specified zone.</summary>
    /// <param name="mRoomIndex">MultiRooms index.</param>
    /// <param name="zoneIndex">Zone index.</param>
    /// <param name="setpoint">Temperature setpoint [°C].</param>
    public void ControlDryBulbTemperature(int mRoomIndex, int zoneIndex, double setpoint)
    {
      mRooms[mRoomIndex].ControlDryBulbTemperature(zoneIndex, setpoint);
      hasHTChgd[mRooms[mRoomIndex]] = true;
      if (mRooms[mRoomIndex].SolveMoistureTransferSimultaneously) hasWTChgd[mRooms[mRoomIndex]] = true;
    }

    /// <summary>Disables temperature control and sets a fixed sensible heat supply for the specified zone.</summary>
    /// <param name="mRoomIndex">MultiRooms index.</param>
    /// <param name="zoneIndex">Zone index.</param>
    /// <param name="heatSupply">Sensible heat supply [W].</param>
    public void ControlHeatSupply(int mRoomIndex, int zoneIndex, double heatSupply)
    {
      mRooms[mRoomIndex].ControlHeatSupply(zoneIndex, heatSupply);
      hasHTChgd[mRooms[mRoomIndex]] = true;
      if (mRooms[mRoomIndex].SolveMoistureTransferSimultaneously) hasWTChgd[mRooms[mRoomIndex]] = true;
    }

    /// <summary>Enables humidity ratio control for the specified zone.</summary>
    /// <param name="mRoomIndex">MultiRooms index.</param>
    /// <param name="zoneIndex">Zone index.</param>
    /// <param name="setpoint">Humidity ratio setpoint [kg/kg].</param>
    public void ControlHumidityRatio(int mRoomIndex, int zoneIndex, double setpoint)
    {
      mRooms[mRoomIndex].ControlHumidityRatio(zoneIndex, setpoint);
      hasWTChgd[mRooms[mRoomIndex]] = true;
      if (mRooms[mRoomIndex].SolveMoistureTransferSimultaneously) hasHTChgd[mRooms[mRoomIndex]] = true;
    }

    /// <summary>Disables humidity control and sets a fixed moisture supply for the specified zone.</summary>
    /// <param name="mRoomIndex">MultiRooms index.</param>
    /// <param name="zoneIndex">Zone index.</param>
    /// <param name="moistureSupply">Moisture supply [kg/s].</param>
    public void ControlMoistureSupply(int mRoomIndex, int zoneIndex, double moistureSupply)
    {
      mRooms[mRoomIndex].ControlMoistureSupply(zoneIndex, moistureSupply);
      hasWTChgd[mRooms[mRoomIndex]] = true;
      if (mRooms[mRoomIndex].SolveMoistureTransferSimultaneously) hasHTChgd[mRooms[mRoomIndex]] = true;
    }

    #endregion

    #region 空調能力設定関連の処理

    /// <summary>Sets the maximum heating capacity for the specified zone [W].</summary>
    /// <param name="mRoomIndex">MultiRooms index.</param>
    /// <param name="zoneIndex">Zone index.</param>
    /// <param name="heatingCapacity">Maximum heating capacity [W].</param>
    public void SetHeatingCapacity(int mRoomIndex, int zoneIndex, double heatingCapacity)
    { mRooms[mRoomIndex].SetHeatingCapacity(zoneIndex, heatingCapacity); }

    /// <summary>Sets the maximum cooling capacity for the specified zone [W].</summary>
    /// <param name="mRoomIndex">MultiRooms index.</param>
    /// <param name="zoneIndex">Zone index.</param>
    /// <param name="coolingCapacity">Maximum cooling capacity [W].</param>
    public void SetCoolingCapacity(int mRoomIndex, int zoneIndex, double coolingCapacity)
    { mRooms[mRoomIndex].SetCoolingCapacity(zoneIndex, coolingCapacity); }

    /// <summary>Sets the maximum humidifying capacity for the specified zone [kg/s].</summary>
    /// <param name="mRoomIndex">MultiRooms index.</param>
    /// <param name="zoneIndex">Zone index.</param>
    /// <param name="humidifyingCapacity">Maximum humidifying capacity [kg/s].</param>
    public void SetHumidifyingCapacity(int mRoomIndex, int zoneIndex, double humidifyingCapacity)
    { mRooms[mRoomIndex].SetHumidifyingCapacity(zoneIndex, humidifyingCapacity); }

    /// <summary>Sets the maximum dehumidifying capacity for the specified zone [kg/s].</summary>
    /// <param name="mRoomIndex">MultiRooms index.</param>
    /// <param name="zoneIndex">Zone index.</param>
    /// <param name="dehumidifyingCapacity">Maximum dehumidifying capacity [kg/s].</param>
    public void SetDehumidifyingCapacity(int mRoomIndex, int zoneIndex, double dehumidifyingCapacity)
    { mRooms[mRoomIndex].SetDehumidifyingCapacity(zoneIndex, dehumidifyingCapacity); }

    #endregion

    #region モデル検証

    /// <summary>Checks whether any wall surfaces are not registered in any MultiRooms.</summary>
    /// <param name="wallInfo">Information about unconnected wall surfaces.</param>
    /// <returns>True if connection errors exist; otherwise false.</returns>
    public bool HasConnectionError(out WallInfo[] wallInfo)
    {
      List<WallInfo> wInfo = new List<WallInfo>();
      for(int i=0;i<walls.Count;i++)
      {
        //F側のチェック
        bool has = false;
        for (int j = 0; j < mRooms.Length; j++)
        {
          if (mRooms[j].HasSurface(walls[i].SurfaceF))
          {
            has = true;
            break;
          }
        }
        if (!has) wInfo.Add(new WallInfo(i, true));

        //B側のチェック
        has = false;
        for (int j = 0; j < mRooms.Length; j++)
        {
          if (mRooms[j].HasSurface(walls[i].SurfaceB))
          {
            has = true;
            break;
          }
        }
        if (!has) wInfo.Add(new WallInfo(i, false));
      }

      wallInfo = wInfo.ToArray();
      return wallInfo.Length != 0;
    }

    /// <summary>Holds information about a wall surface not registered in any MultiRooms.</summary>
    public struct WallInfo
    {

      /// <summary>Gets the wall index.</summary>
      public int Index { get; private set; }

      /// <summary>Gets a value indicating whether the unregistered surface is the F side.</summary>
      public bool IsSideF { get; private set; }

      /// <summary>Initializes a new instance.</summary>
      /// <param name="index">Wall index.</param>
      /// <param name="isSideF">True for the F side; false for the B side.</param>
      public WallInfo(int index , bool isSideF) 
      {
        Index = index;
        IsSideF = isSideF;
      }
    }

    #endregion

    #region その他の処理

    /// <summary>Gets all wall assemblies in the model across all MultiRooms.</summary>
    /// <returns>Array of read-only wall assemblies.</returns>
    public IReadOnlyWall[] GetWalls()
    {
      List<IReadOnlyWall> walls = new List<IReadOnlyWall>();
      foreach (MultiRoom mrm in mRooms) walls.AddRange(mrm.Walls);
      return walls.ToArray();
    }

    /// <summary>Gets all zones in the model across all MultiRooms.</summary>
    /// <returns>Array of read-only zones.</returns>
    public IReadOnlyZone[] GetZones()
    {
      List<IReadOnlyZone> zones = new List<IReadOnlyZone>();
      foreach (MultiRoom mrm in mRooms) zones.AddRange(mrm.Zones);
      return zones.ToArray();
    }

    #endregion

    #region internalメソッド

    /// <summary>Assigns sequential IDs to all walls in the model.</summary>
    internal void SetWallID()
    {
      int id = 0;
      for (int i = 0; i < walls.Count; i++)
      {
        walls[i].ID = id;
        id++;
      }
    }

    #endregion

    #region インナークラス定義

    /// <summary>Collects inter-zone air flow entries for a single destination zone.</summary>
      private class InterZoneAirFlowCollection : IEnumerable<InterZoneAirFlow>
    {

      /// <summary>Air flow rates [kg/s] keyed by [source MultiRooms index][source zone index].</summary>
      private Dictionary<int, Dictionary<int, double>> aFlows 
        = new Dictionary<int, Dictionary<int, double>>();

      /// <summary>Adds or updates an air flow entry from the specified source zone.</summary>
      /// <param name="rmIndex">MultiRooms index.</param>
      /// <param name="znIndex">Zone index.</param>
      /// <param name="aFlow">Air flow rate [kg/s].</param>
      public void AddAirFlow(int rmIndex, int znIndex, double aFlow)
      {
        if (!aFlows.ContainsKey(rmIndex))
          aFlows.Add(rmIndex, new Dictionary<int, double>());
        aFlows[rmIndex][znIndex] = aFlow;
      }

      /// <summary>Removes an air flow entry from the specified source zone.</summary>
      /// <param name="rmIndex">MultiRooms index.</param>
      /// <param name="znIndex">Zone index.</param>
      public void RemoveAirFlow(int rmIndex, int znIndex)
      {
        if (aFlows.ContainsKey(rmIndex))
        {
          if (aFlows[rmIndex].ContainsKey(znIndex)) aFlows[rmIndex].Remove(znIndex);
          if (aFlows[rmIndex].Count == 0) aFlows.Remove(rmIndex);
        }
      }

      /// <summary>Enumerates all air flow entries.</summary>
      /// <returns>Sequence of inter-zone air flow entries.</returns>
      public IEnumerator<InterZoneAirFlow> GetEnumerator()
      {
        foreach (int key1 in aFlows.Keys)
          foreach (int key2 in aFlows[key1].Keys)
            yield return new InterZoneAirFlow(key1, key2, aFlows[key1][key2]);
      }

      IEnumerator IEnumerable.GetEnumerator()
      {
        throw new PopoloNotImplementedException(
          "IEnumerable.GetEnumerator (non-generic). Use the generic GetEnumerator instead.");
      }
    }

    /// <summary>Holds a single inter-zone air flow entry.</summary>
    private class InterZoneAirFlow
    {
      /// <summary>Gets or sets the source MultiRooms index.</summary>
      public int rmIndex { get; set; }

      /// <summary>Gets or sets the source zone index.</summary>
      public int znIndex { get; set; }

      /// <summary>Gets or sets the air flow rate [kg/s].</summary>
      public double aFlow { get; set; }

      /// <summary>Initializes a new instance.</summary>
      /// <param name="rmIndex">Source MultiRooms index.</param>
      /// <param name="znIndex">Zone index.</param>
      /// <param name="aFlow">Air flow rate [kg/s].</param>
      public InterZoneAirFlow(int rmIndex, int znIndex, double aFlow)
      {
        this.rmIndex = rmIndex;
        this.znIndex = znIndex;
        this.aFlow = aFlow;
      }
    }

    #endregion

  }

}
