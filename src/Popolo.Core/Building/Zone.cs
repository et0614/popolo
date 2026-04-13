/* Zone.cs
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

using Popolo.Core.Building.Envelope;
using Popolo.Core.Numerics;
using Popolo.Core.Physics;
using System;
using System.Collections.Generic;

namespace Popolo.Core.Building
{
  /// <summary>Represents a single-node thermal zone with air temperature and humidity ratio.</summary>
  public class Zone : IReadOnlyZone
  {

    #region インスタンス変数・プロパティ

    /// <summary>List of heat gain elements assigned to this zone.</summary>
    private List<IHeatGain> heatGains = new List<IHeatGain>();

    /// <summary>Gets the name of the zone.</summary>
    public string Name { get; private set; }

    /// <summary>Gets or sets the ventilation air flow rate [kg/s].</summary>
    public double VentilationRate { get; set; }

    /// <summary>Gets or sets the supply air flow rate [kg/s].</summary>
    public double SupplyAirFlowRate { get; set; }

    /// <summary>Gets the air mass in the zone [kg].</summary>
    public double AirMass { get; private set; }

    /// <summary>Gets the floor area of the zone [m²].</summary>
    public double FloorArea { get; private set; }

    /// <summary>Gets the room index to which this zone belongs (-1 if unassigned).</summary>
    public int RoomIndex { get; internal set; } = -1;

    /// <summary>Gets the zone index within its MultiRooms.</summary>
    public int Index { get; internal set; }

    /// <summary>Gets the multi-room system to which this zone belongs.</summary>
    public IReadOnlyMultiRooms MultiRoom { get; internal set; } = null!;

    /// <summary>Gets or sets the sensible heat capacity of objects other than air [J/K].</summary>
    /// <remarks>
    /// A typical value for office buildings is approximately 12,000 J/(m³·K).
    /// For residential buildings, roughly half that value is appropriate.
    /// (Reference: Kimura, K. 1961, Heat Capacity of Office Furniture.)
    /// </remarks>
    public double HeatCapacity { get; set; }

    /// <summary>Gets or sets the moisture capacity of objects other than air [kg].</summary>
    public double MoistureCapacity { get; set; }

    /// <summary>Gets the dry-bulb temperature of the zone air [°C].</summary>
    public double Temperature { get; internal set; } = 24;

    /// <summary>Gets the dry-bulb temperature setpoint [°C].</summary>
    public double TemperatureSetpoint { get; private set; } = 24;

    /// <summary>Gets the sensible heat supply to the zone [W].</summary>
    public double HeatSupply { get; internal set; }

    /// <summary>Gets a value indicating whether the zone dry-bulb temperature is being controlled.</summary>
    public bool TemperatureControlled { get; internal set; }

    /// <summary>Gets or sets the supply air temperature [°C].</summary>
    public double SupplyAirTemperature { get; set; }

    /// <summary>Gets the humidity ratio of the zone air [kg/kg].</summary>
    public double HumidityRatio { get; internal set; } = 0.0095;

    /// <summary>Gets the humidity ratio setpoint [kg/kg].</summary>
    public double HumidityRatioSetpoint { get; private set; } = 0.0095;

    /// <summary>Gets the moisture supply to the zone [kg/s].</summary>
    public double MoistureSupply { get; internal set; }

    /// <summary>Gets a value indicating whether the zone humidity ratio is being controlled.</summary>
    public bool HumidityControlled { get; internal set; }

    /// <summary>Gets or sets the supply air humidity ratio [kg/kg].</summary>
    public double SupplyAirHumidityRatio { get; set; }

    /// <summary>Gets or sets the maximum heating capacity [W].</summary>
    public double HeatingCapacity { get; set; } = double.PositiveInfinity;

    /// <summary>Gets or sets the maximum cooling capacity [W].</summary>
    public double CoolingCapacity { get; set; } = double.PositiveInfinity;

    /// <summary>Gets or sets the maximum humidifying capacity [kg/s].</summary>
    public double HumidifyingCapacity { get; set; } = double.PositiveInfinity;

    /// <summary>Gets or sets the maximum dehumidifying capacity [kg/s].</summary>
    public double DehumidifyingCapacity { get; set; } = double.PositiveInfinity;

    /// <summary>Gets the base heat gain element (always present).</summary>
    public IHeatGain BaseHeatGain { get; private set; } = new SimpleHeatGain(0, 0, 0);

    #endregion

    #region internalプロパティ

    /// <summary>List of boundary surface elements facing this zone.</summary>
    internal List<BoundarySurface> Surfaces { get; set; }

    /// <summary>Supply air flow rate from another zone [kg/s].</summary>
    internal double _supplyAirFlowRate2 { get; set; }

    /// <summary>Supply air temperature from another zone [°C].</summary>
    internal double _supplyAirTemperature2 { get; set; }

    /// <summary>Supply air humidity ratio from another zone [kg/kg].</summary>
    internal double _supplyAirHumidityRatio2 { get; set; }

    #endregion

    #region コンストラクタ

    /// <summary>Initializes a new zone with a specified air mass.</summary>
    /// <param name="name">Zone name.</param>
    /// <param name="airMass">Air mass [kg].</param>
    public Zone(string name, double airMass)
    {
      Name = name;
      AirMass = airMass;
      Surfaces = new List<BoundarySurface>();
    }

    /// <summary>Initializes a new zone with a specified air mass and floor area.</summary>
    /// <param name="name">Zone name.</param>
    /// <param name="airMass">Air mass [kg].</param>
    /// <param name="floorArea">Floor area [m²].</param>
    public Zone(string name, double airMass, double floorArea)
    {
      Name = name;
      AirMass = airMass;
      FloorArea = floorArea;
      Surfaces = new List<BoundarySurface>();
    }

    #endregion

    #region 温湿度設定・制御関連の処理

    /// <summary>Initializes the zone air temperature and humidity ratio.</summary>
    /// <param name="temperature">Dry-bulb temperature [°C].</param>
    /// <param name="humidityRatio">Humidity ratio [kg/kg].</param>
    public void InitializeAirState(double temperature, double humidityRatio)
    {
      Temperature = temperature;
      HumidityRatio = humidityRatio;
    }

    /// <summary>Enables dry-bulb temperature control with the specified setpoint.</summary>
    /// <param name="setpoint">Temperature setpoint [°C].</param>
    public void ControlDrybulbTemperature(double setpoint)
    {
      TemperatureControlled = true;
      TemperatureSetpoint = setpoint;
    }

    /// <summary>Disables temperature control and sets a fixed sensible heat supply.</summary>
    /// <param name="heatSupply">Sensible heat supply [W].</param>
    public void ControlHeatSupply(double heatSupply)
    {
      TemperatureControlled = false;
      HeatSupply = heatSupply;
    }

    /// <summary>Enables humidity ratio control with the specified setpoint.</summary>
    /// <param name="setpoint">Humidity ratio setpoint [kg/kg].</param>
    public void ControlHumidityRatio(double setpoint)
    {
      HumidityControlled = true;
      HumidityRatioSetpoint = setpoint;
    }

    /// <summary>Disables humidity control and sets a fixed moisture supply.</summary>
    /// <param name="moistureSupply">Moisture supply [kg/s].</param>
    public void ControlMoistureSupply(double moistureSupply)
    {
      HumidityControlled = false;
      MoistureSupply = moistureSupply;
    }

    /// <summary>Computes the mean surface temperature weighted by emissivity and area.</summary>
    /// <returns>Emissivity–area weighted mean surface temperature [°C].</returns>
    public double GetMeanSurfaceTemperature()
    {
      double tSum = 0;
      double sSum = 0;
      foreach (BoundarySurface ws in Surfaces)
      {
        double buff = ws.Area * ws.LongWaveEmissivity;
        tSum += ws.SurfaceTemperature * buff;
        sSum += buff;
      }
      if (sSum == 0) return this.Temperature;
      else return tSum / sSum;
    }

    #endregion

    #region 発熱関連の処理

    /// <summary>Sets the base heat gain values for this zone.</summary>
    /// <param name="convectiveHeatGain">Convective sensible heat gain [W].</param>
    /// <param name="radiativeHeatGain">Radiative sensible heat gain [W].</param>
    /// <param name="moistureGain">Moisture generation rate [kg/s].</param>
    public void SetBaseHeatGain
      (double convectiveHeatGain, double radiativeHeatGain, double moistureGain)
    {
      SimpleHeatGain sGain = (SimpleHeatGain)BaseHeatGain;
      sGain.ConvectiveHeatGain = convectiveHeatGain;
      sGain.RadiativeHeatGain = radiativeHeatGain;
      sGain.MoistureGain = moistureGain;
    }

    /// <summary>Adds a heat gain element to this zone.</summary>
    /// <param name="heatGain">Heat gain element to add.</param>
    public void AddHeatGain(IHeatGain heatGain) { heatGains.Add(heatGain); }

    /// <summary>Removes a heat gain element from this zone.</summary>
    /// <param name="heatGain">Heat gain element to remove.</param>
    public void RemoveHeatGain(IHeatGain heatGain) { heatGains.Remove(heatGain); }

    /// <summary>Gets all heat gain elements assigned to this zone.</summary>
    /// <returns>Array of heat gain elements.</returns>
    public IHeatGain[] GetHeatGains() { return heatGains.ToArray(); }

    /// <summary>Integrates the convective sensible heat gains from all elements [W].</summary>
    /// <returns>Total convective sensible heat gain [W].</returns>
    public double IntegrateConvectiveHeatgains()
    {
      double sum = BaseHeatGain.GetConvectiveHeatGain(this);
      foreach (IHeatGain hg in heatGains) sum += hg.GetConvectiveHeatGain(this);
      return sum;
    }

    /// <summary>Integrates the radiative sensible heat gains from all elements [W].</summary>
    /// <returns>Total radiative sensible heat gain [W].</returns>
    public double IntegrateRadiativeHeatGains()
    {
      double sum = BaseHeatGain.GetRadiativeHeatGain(this);
      foreach (IHeatGain hg in heatGains) sum += hg.GetRadiativeHeatGain(this);
      return sum;
    }

    /// <summary>Integrates the moisture gains from all elements [kg/s].</summary>
    /// <returns>Total moisture generation rate [kg/s].</returns>
    public double IntegrateMoistureGains()
    {
      double sum = BaseHeatGain.GetMoistureGain(this);
      foreach (IHeatGain hg in heatGains) sum += hg.GetMoistureGain(this);
      return sum;
    }

    #endregion

    #region その他処理

    /// <summary>Gets the total window area facing this zone [m²].</summary>
    /// <returns>Total window area [m²].</returns>
    public double GetTotalWindowArea()
    {
      double sum = 0;
      foreach (BoundarySurface wsf in Surfaces)
        if (!wsf.IsWall) sum += wsf.Area;
      return sum;
    }

    /// <summary>Gets all windows facing this zone.</summary>
    /// <returns>Array of read-only window instances.</returns>
    public IReadOnlyWindow[] GetWindows()
    {
      List<IReadOnlyWindow> wins = new List<IReadOnlyWindow>();
      foreach (BoundarySurface wsf in Surfaces)
        if (!wsf.IsWall) wins.Add(wsf.Window);
      return wins.ToArray();
    }

    /// <summary>Gets the sensible heat input from supply air [W].</summary>
    /// <returns>Supply air sensible heat [W].</returns>
    public double GetSupplyAirHeat()
    { return PhysicsConstants.NominalMoistAirIsobaricSpecificHeat * SupplyAirFlowRate * (SupplyAirTemperature - Temperature); }

    /// <summary>Gets the moisture input from supply air [kg/s].</summary>
    /// <returns>Supply air moisture [kg/s].</returns>
    public double GetSupplyAirMoisture()
    { return SupplyAirFlowRate * (SupplyAirHumidityRatio - HumidityRatio); }

    #endregion

  }

}
