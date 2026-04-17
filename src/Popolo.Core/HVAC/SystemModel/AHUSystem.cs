/* AHUSystem.cs
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
using System.Collections.Generic;

using Popolo.Core.Numerics;
using Popolo.Core.Numerics.LinearAlgebra;
using Popolo.Core.Building;
using Popolo.Core.HVAC.AirSide;
using Popolo.Core.Physics;

namespace Popolo.Core.HVAC.SystemModel
{

  /// <summary>Secondary air-conditioning system using air handling units.</summary>
  public class AHUSystem : IAirConditioningSystemModel
  {

    #region 列挙型定義

    /// <summary>AHU operating mode.</summary>
    public enum OperatingMode
    {
      /// <summary>Shut-off.</summary>
      ShutOff,
      /// <summary>Ventilation only (no temperature control).</summary>
      Ventilation,
      /// <summary>Cooling only.</summary>
      Cooling,
      /// <summary>Heating only.</summary>
      Heating
    }

    #endregion

    #region インスタンス変数・プロパティ

    /// <summary>Outdoor air conditions.</summary>
    private IReadOnlyMoistAir outdoorAir = new MoistAir(35, 0.0185);

    /// <summary>Building thermal load calculation model.</summary>
    private BuildingThermalModel bModel;

    /// <summary>List of air handling units.</summary>
    private AirHandlingUnit[] ahu;

    /// <summary>Per-AHU zone control list.</summary>
    private VolumeController[][] vlmCtrl;

    /// <summary>True if the AHU control setpoint has been determined.</summary>
    private bool[] ctrlFixed;

    /// <summary>Gets the list of air handling units.</summary>
    public IReadOnlyAirHandlingUnit[] AHUs { get { return ahu; } }

    /// <summary>Gets the list of AHU controllers.</summary>
    public AHUController[] Controllers { get; private set; }

    /// <summary>True on the first iteration of the convergence loop.</summary>
    private bool isFirstCall = true;

    /// <summary>List of AHU inlet air humidity ratios [kg/kg].</summary>
    private double[] hrAHUs;

    #endregion

    #region コンストラクタ

    /// <summary>Initializes a new instance.</summary>
    /// <param name="bModel">Building thermal load calculation model.</param>
    /// <param name="ahu">List of air handling units.</param>
    public AHUSystem(BuildingThermalModel bModel, AirHandlingUnit[] ahu)
    {
      this.bModel = bModel;
      this.ahu = ahu;

      hrAHUs = new double[ahu.Length];//DEBUG
      ctrlFixed = new bool[ahu.Length];
      Controllers = new AHUController[ahu.Length];
      vlmCtrl = new VolumeController[ahu.Length][];
      for (int i = 0; i < ahu.Length; i++)
      {
        Controllers[i] = new AHUController();
        vlmCtrl[i] = new VolumeController[0];
      }
    }

    #endregion

    #region インスタンスメソッド（モデル構築関連）

    /// <summary>Sets the outdoor air flow rate for the AHU.</summary>
    /// <param name="ahuIndex">AHU index.</param>
    /// <param name="minOA">Minimum outdoor air flow rate [kg/s].</param>
    /// <param name="maxOA">Maximum outdoor air flow rate [kg/s].</param>
    public void SetOutdoorAirFlow(int ahuIndex, double minOA, double maxOA)
    { ahu[ahuIndex].SetOutdoorAirFlowRange(minOA, maxOA); }

    /// <summary>Registers a VAV controller with the AHU.</summary>
    /// <param name="ahuIndex">AHU index.</param>
    /// <param name="vavs">List of VAV controllers.</param>
    public void SetVAV(int ahuIndex, VolumeController[] vavs)
    {
      vlmCtrl[ahuIndex] = vavs;
      Controllers[ahuIndex].IsCAVControl = false;
    }

    /// <summary>Registers a CAV controller with the AHU.</summary>
    /// <param name="ahuIndex">AHU index.</param>
    /// <param name="cavs">List of CAV controllers.</param>
    public void SetCAV(int ahuIndex, VolumeController[] cavs)
    {
      vlmCtrl[ahuIndex] = cavs;
      Controllers[ahuIndex].IsCAVControl = true;
    }

    /// <summary>Controls the zone temperature.</summary>
    /// <param name="ahuIndex">AHU index.</param>
    /// <param name="controlZoneIndex">Control zone index.</param>
    /// <param name="setpointTemperature">Temperature setpoint [°C].</param>
    public void ControlZoneTemperature
      (int ahuIndex, int controlZoneIndex, double setpointTemperature)
    {
      if (Controllers[ahuIndex].IsCAVControl)
      {
        Controllers[ahuIndex].SetpointTemperature = setpointTemperature;
        Controllers[ahuIndex].IsRATemperatureControl = false;
      }
      else
      {
        VolumeController vc = vlmCtrl[ahuIndex][controlZoneIndex];
        vc.IsShutOff = false;
        vc.SetpointTemperature = setpointTemperature;
      }
    }

    /// <summary>Shuts off air conditioning for the zone.</summary>
    /// <param name="ahuIndex">AHU index.</param>
    /// <param name="controlZoneIndex">Control zone index.</param>
    public void ShutOff(int ahuIndex, int controlZoneIndex)
    { vlmCtrl[ahuIndex][controlZoneIndex].IsShutOff = true; }

    /// <summary>Gets the volume controller (VAV/CAV) at the specified index.</summary>
    /// <param name="ahuIndex">AHU index.</param>
    /// <param name="vavIndex">VAV index.</param>
    /// <returns>Volume controller (VAV/CAV).</returns>
    public VolumeController GetVolumeController(int ahuIndex, int vavIndex)
    { return vlmCtrl[ahuIndex][vavIndex]; }

    /// <summary>Gets the total number of volume controllers (VAV/CAV).</summary>
    /// <param name="ahuIndex">AHU index.</param>
    /// <returns>Total number of volume controllers.</returns>
    public int GetVolumeControllerCount(int ahuIndex)
    { return vlmCtrl[ahuIndex].Length; }

    #endregion

    #region IAirConditioningSystemModel実装

    /// <summary>Gets or sets the current date and time.</summary>
    public DateTime CurrentDateTime { get; set; }

    /// <summary>Gets or sets the simulation time step [s].</summary>
    public double TimeStep { get; set; }

    /// <summary>Gets or sets the outdoor air conditions.</summary>
    public IReadOnlyMoistAir OutdoorAir
    {
      get { return outdoorAir; }
      set
      {
        outdoorAir = value;
        for (int i = 0; i < ahu.Length; i++)
        {
          ahu[i].OATemperature = value.DryBulbTemperature;
          ahu[i].OAHumidityRatio = value.HumidityRatio;
        }
      }
    }

    /// <summary>Gets the building thermal model associated with this air-conditioning system.</summary>
    public IReadOnlyBuildingThermalModel BuildingThermalModel
    { get { return bModel; } }

    /// <summary>Gets the chilled water supply temperature [°C].</summary>
    public double ChilledWaterSupplyTemperature { get; private set; }

    /// <summary>Gets the chilled water return temperature [°C].</summary>
    public double ChilledWaterReturnTemperature { get; private set; }

    /// <summary>Gets the chilled water flow rate [kg/s].</summary>
    public double ChilledWaterFlowRate { get; private set; }

    /// <summary>Gets the hot water supply temperature [°C].</summary>
    public double HotWaterSupplyTemperature { get; private set; }

    /// <summary>Gets the hot water return temperature [°C].</summary>
    public double HotWaterReturnTemperature { get; private set; }

    /// <summary>Gets the hot water flow rate [kg/s].</summary>
    public double HotWaterFlowRate { get; private set; }

    /// <summary>Forecasts the return water temperatures for the given supply temperatures.</summary>
    /// <param name="chilledWaterSupplyTemperature">Chilled water supply temperature [°C].</param>
    /// <param name="hotWaterSupplyTemperature">Hot water supply temperature [°C].</param>
    public void ForecastReturnWaterTemperature
      (double chilledWaterSupplyTemperature, double hotWaterSupplyTemperature)
    {
      ChilledWaterSupplyTemperature = chilledWaterSupplyTemperature;
      HotWaterSupplyTemperature = hotWaterSupplyTemperature;

      if (isFirstCall)
      {
        isFirstCall = false;
        for (int i = 0; i < ahu.Length; i++)
        {
          hrAHUs[i] = 0;
          double mRA = 0;
          foreach (VolumeController vc in vlmCtrl[i])
          {
            IReadOnlyZone zn = bModel.MultiRoom[vc.RoomIndex].Zones[vc.ZoneIndex];
            hrAHUs[i] += zn.HumidityRatio * vc.MaxRAFlow;
            mRA += vc.MaxRAFlow;
          }
          hrAHUs[i] /= mRA;
        }
      }
      for (int i = 0; i < ahu.Length; i++)
        if (Controllers[i].Mode != OperatingMode.ShutOff) ahu[i].RAHumidityRatio = hrAHUs[i];

      //AHUの冷温水と風量初期化
      bool isAllAHUShutoff = true;
      for (int i = 0; i < ahu.Length; i++)
      {
        //冷温水コイルに通水する
        ahu[i].ChilledWaterInletTemperature = chilledWaterSupplyTemperature;
        ahu[i].HotWaterInletTemperature = hotWaterSupplyTemperature;

        //給気風量を初期化
        double maxSA = 0;
        double maxRA = 0;
        if (Controllers[i].Mode != OperatingMode.ShutOff)
        {
          bool allClosed = true;
          foreach (VolumeController ctrl in vlmCtrl[i])
          {
            if (!ctrl.IsShutOff) allClosed = false;
            ctrl.SAFlow = ctrl.MaxSAFlow;
            maxSA += ctrl.MaxSAFlow;
            maxRA += ctrl.MaxRAFlow;
          }
          //全VAV,CAVが停止の場合にはAHUも停止させる
          if (allClosed) Controllers[i].Mode = OperatingMode.ShutOff;
          else ahu[i].SetAirFlowRate(maxRA, maxSA);
        }

        if (Controllers[i].Mode == OperatingMode.ShutOff)
        {
          ahu[i].ShutOff();
          ctrlFixed[i] = true;
          for (int j = 0; j < vlmCtrl[i].Length; j++) vlmCtrl[i][j].SAFlow = 0; //2017.12.02追加
        }
        else
        {
          isAllAHUShutoff = false;
          ctrlFixed[i] = false;
        }
      }

      //すべてのAHUが非稼働の場合には給気量0で成行計算を1回実行して終了
      if (isAllAHUShutoff)
      {
        for (int i = 0; i < ahu.Length; i++)
        {
          foreach (VolumeController vc in vlmCtrl[i])
          {
            bModel.SetSupplyAir(vc.RoomIndex, vc.ZoneIndex, 0, 0, 0);
            bModel.ControlHeatSupply(vc.RoomIndex, vc.ZoneIndex, 0);
            bModel.ControlMoistureSupply(vc.RoomIndex, vc.ZoneIndex, 0);
          }
        }
        bModel.ForecastHeatTransfer();
        bModel.ForecastWaterTransfer();
        UpdateReturnWaterState();
        return;
      }

      //顕熱平衡の収束計算
      SolveHeatTransfer();

      //水分平衡の計算
      for (int i = 0; i < ahu.Length; i++)
      {
        foreach (VolumeController vc in vlmCtrl[i])
        {
          if (Controllers[i].Mode == OperatingMode.ShutOff)
            bModel.SetSupplyAir(vc.RoomIndex, vc.ZoneIndex, 0, 0, 0);
          else bModel.SetSupplyAir
              (vc.RoomIndex, vc.ZoneIndex, ahu[i].SATemperature, ahu[i].SAHumidityRatio, vc.SAFlow);
          bModel.ControlHeatSupply(vc.RoomIndex, vc.ZoneIndex, 0);
          bModel.ControlMoistureSupply(vc.RoomIndex, vc.ZoneIndex, 0);
        }
      }
      bModel.ForecastWaterTransfer();

      //冷温水量を更新
      UpdateReturnWaterState();
    }

    /// <summary>Fixes (commits) the forecast state as the current state.</summary>
    public void FixState() { isFirstCall = true; }

    #endregion

    #region インスタンスメソッド（平衡状態計算関連）

    /// <summary>Solves the sensible heat balance for all zones.</summary>
    private void SolveHeatTransfer()
    {
      //完全に制御した場合の負荷にもとづいて収束計算初期値を作成
      double[] initVec = new double[ahu.Length];
      for (int i = 0; i < ahu.Length; i++)
      {
        if (Controllers[i].Mode == OperatingMode.Cooling 
          || Controllers[i].Mode == OperatingMode.Heating)
        {
          foreach (VolumeController vc in vlmCtrl[i])
          {
            double sp;
            if (Controllers[i].IsCAVControl) sp = Controllers[i].SetpointTemperature;
            else sp = vc.SetpointTemperature;
            bModel.ControlDryBulbTemperature(vc.RoomIndex, vc.ZoneIndex, sp);
            bModel.SetSupplyAir(vc.RoomIndex, vc.ZoneIndex, 0, 0, 0); //DEBUG

            //加湿系統は潜熱負荷を計算する
            if (Controllers[i].Mode == OperatingMode.Heating 
              && ahu[i].Humidifier != AirHandlingUnit.HumidifierType.None)
              bModel.ControlHumidityRatio(vc.RoomIndex, vc.ZoneIndex, Controllers[i].MinimumHumidity);
          }
        }
      }
      bModel.ForecastHeatTransfer();
      bModel.ForecastWaterTransfer();

      for (int i = 0; i < ahu.Length; i++)
      {
        initVec[i] = 0;
        Controllers[i].splyHumidSP = 0;
        double sHL = 0;
        double mRA = 0;
        double mSA = 0;
        foreach (VolumeController ctrl in vlmCtrl[i])
        {
          IReadOnlyZone zn = bModel.MultiRoom[ctrl.RoomIndex].Zones[ctrl.ZoneIndex];
          initVec[i] += zn.Temperature * ctrl.RAFlow;
          sHL += zn.HeatSupply;
          mRA += ctrl.MaxRAFlow;
          mSA += ctrl.MaxSAFlow;

          //加湿系統のAHU出口湿度を計算する
          if (Controllers[i].Mode == OperatingMode.Heating
            && ahu[i].Humidifier != AirHandlingUnit.HumidifierType.None)
          {
            double wAHUo = zn.MoistureSupply / ctrl.MaxSAFlow + zn.HumidityRatio;
            Controllers[i].splyHumidSP = Math.Max(Controllers[i].splyHumidSP, wAHUo);
          }
        }
        initVec[i] = initVec[i] / mRA + sHL / (mSA * PhysicsConstants.NominalMoistAirIsobaricSpecificHeat);
      }

      //過負荷に変化するAHUがなくなるまで繰り返し計算
      bool lastCalc = false;
      while (true)
      {
        //収束計算対象の給気温度を初期化
        List<double> vars = new List<double>();
        for (int i = 0; i < ahu.Length; i++)
        {
          AHUController ctr = Controllers[i];
          if (ctr.Mode != OperatingMode.ShutOff 
            && (ctrlFixed[i] || ctr.Mode == OperatingMode.Ventilation || ctr.IsCAVControl))
            vars.Add(initVec[i]);
        }

        //収束計算実行
        IVector tandh = new Vector(vars.Count);
        IVector fx = new Vector(vars.Count);
        if (vars.Count != 0)
        {
          int iter;
          for (int i = 0; i < tandh.Length; i++) tandh[i] = vars[i];
          //絶対誤差0.01度未満まで収束計算
          double err;
          MultiRoots.Newton(ErrorFnc, ref tandh, 1e-3, 1e-3, 50, out iter, out err);
        }
        else ErrorFnc(tandh, ref fx);

        //過負荷に変化する系統がなくなればVAV風量を最小化して最終の計算処理
        if (!OverLoadAHUChanged(tandh))
        {
          if (lastCalc) return;
          else if (MinimizeVAV(initVec)) lastCalc = true;
          else return;
        }
      }
    }

    /// <summary>Error evaluation function for convergence iteration.</summary>
    /// <param name="vecX">AHU supply air temperature/humidity vector.</param>
    /// <param name="vecF">Error vector.</param>
    private void ErrorFnc(IVector vecX, ref IVector vecF)
    {
      //制御点確定済・換気・CAV制御の場合には給気温度が収束計算対象
      int indx = 0;
      for (int i = 0; i < ahu.Length; i++)
      {
        AHUController ctr = Controllers[i];
        if (ctr.Mode != OperatingMode.ShutOff &&
          (ctrlFixed[i] || ctr.Mode == OperatingMode.Ventilation || ctr.IsCAVControl))
        {
          foreach (VolumeController ctrl in vlmCtrl[i])
          {
            bModel.SetSupplyAir(ctrl.RoomIndex, ctrl.ZoneIndex, vecX[indx], 0, ctrl.SAFlow);
            bModel.ControlHeatSupply(ctrl.RoomIndex, ctrl.ZoneIndex, 0);
          }
          indx++;
        }
      }
      //顕熱平衡を更新
      bModel.ForecastHeatTransfer();

      //誤差集計
      indx = 0;
      for (int i = 0; i < ahu.Length; i++)
      {
        AHUController ctr = Controllers[i];
        if (ctr.Mode != OperatingMode.ShutOff)
        {
          //還気温度を計算
          double tra = 0;
          double mra = 0;
          foreach (VolumeController ctrl in vlmCtrl[i])
          {
            IReadOnlyZone zn = bModel.MultiRoom[ctrl.RoomIndex].Zones[ctrl.ZoneIndex];
            tra += zn.Temperature * ctrl.RAFlow;
            mra += ctrl.RAFlow;
          }
          tra /= mra;
          ahu[i].RATemperature = tra;

          //制御点確定済または換気のみのAHUの給気温度の誤差を計算
          if (ctrlFixed[i] || ctr.Mode == OperatingMode.Ventilation)
          {
            //AHU成行計算処理
            if (ctr.Mode == OperatingMode.Cooling) ahu[i].CoolAir();
            else if (ctr.Mode == OperatingMode.Heating) ahu[i].HeatAir();
            else ahu[i].Ventilate();
            //誤差評価
            vecF[indx] = Math.Abs(ahu[i].SATemperature - vecX[indx]);
            indx++;
          }
          //軽負荷CAV系統の制御誤差評価
          else if (ctr.IsCAVControl)
          {
            double sp = ctr.SetpointTemperature;
            if (ctr.IsRATemperatureControl) vecF[indx] = Math.Abs(tra - sp);
            else
            {
              IReadOnlyZone zn = bModel.MultiRoom[ctr.TargetRoomIndex].Zones[ctr.TargetZoneIndex];
              vecF[indx] = Math.Abs(zn.Temperature - sp);
            }
            indx++;
          }
        }
      }
    }

    /// <summary>Checks whether the number of overloaded AHUs has changed.</summary>
    /// <param name="vec">State variable vector.</param>
    /// <returns>True if a change was detected.</returns>
    private bool OverLoadAHUChanged(IVector vec)
    {
      bool incrsd = false;
      int indx = 0;
      for (int i = 0; i < ahu.Length; i++)
      {
        AHUController ctr = Controllers[i];
        if (ctr.Mode != OperatingMode.ShutOff && !ctrlFixed[i]
          && (ctr.Mode == OperatingMode.Cooling || ctr.Mode == OperatingMode.Heating))
        {
          //還気温度を計算
          ahu[i].RATemperature = 0;
          double mra = 0;
          foreach (VolumeController ctrl in vlmCtrl[i])
          {
            IReadOnlyZone zn = bModel.MultiRoom[ctrl.RoomIndex].Zones[ctrl.ZoneIndex];
            ahu[i].RATemperature += zn.Temperature * ctrl.RAFlow;
            mra += ctrl.RAFlow;
          }
          ahu[i].RATemperature /= mra;

          //過負荷判定
          double tSP;
          //CAV制御の場合には収束計算対象の状態変数が出口温度設定値
          if (ctr.IsCAVControl) tSP = vec[indx];
          //VAVの場合には顕熱負荷にもとづき出口温度設定値を計算
          else
          {
            if (ctr.Mode == OperatingMode.Cooling) tSP = 60;
            else tSP = -10;
            foreach (VolumeController ctrl in vlmCtrl[i])
            {
              IReadOnlyZone zn = bModel.MultiRoom[ctrl.RoomIndex].Zones[ctrl.ZoneIndex];
              double bf = zn.Temperature + zn.HeatSupply / (PhysicsConstants.NominalMoistAirIsobaricSpecificHeat * ctrl.SAFlow);
              if (ctr.Mode == OperatingMode.Cooling) tSP = Math.Min(tSP, bf);
              else tSP = Math.Max(tSP, bf);
            }
          }
          //出口温度実現可能か
          if (ctr.Mode == OperatingMode.Cooling) ctrlFixed[i] = !ahu[i].CoolAir(tSP, 0);
          else ctrlFixed[i] = !ahu[i].HeatAir(tSP, ctr.splyHumidSP);
          if (ctrlFixed[i]) incrsd = true;
        }

        if (ctr.Mode != OperatingMode.ShutOff &&
          (ctrlFixed[i] || ctr.Mode == OperatingMode.Ventilation || ctr.IsCAVControl))
          indx++;
      }
      return incrsd;
    }

    /// <summary>Minimises the VAV airflow rates.</summary>
    /// <remarks>Returns false if no VAV is present.</remarks>
    private bool MinimizeVAV(double[] initVec)
    {
      bool hasVAV = false;
      for (int i = 0; i < ahu.Length; i++)
      {
        //制御点未確定でVAV制御のAHUについて計算
        if (Controllers[i].Mode != OperatingMode.ShutOff && !Controllers[i].IsCAVControl && !ctrlFixed[i])
        {
          bool isCooling = (Controllers[i].Mode == OperatingMode.Cooling);
          int vcNum = vlmCtrl[i].Length;
          bool[] vavSt = new bool[vcNum];
          double[] znT = new double[vcNum];
          double[] znW = new double[vcNum];
          double[] znHL = new double[vcNum];
          double[] minSA = new double[vcNum];
          double[] maxSA = new double[vcNum];
          double[] maxRA = new double[vcNum];
          for (int j = 0; j < vlmCtrl[i].Length; j++)
          {
            VolumeController vc = vlmCtrl[i][j];
            IReadOnlyZone zn = bModel.MultiRoom[vc.RoomIndex].Zones[vc.ZoneIndex];
            znT[j] = zn.Temperature;
            znW[j] = zn.HumidityRatio;
            znHL[j] = zn.HeatSupply;
            vavSt[j] = vc.IsShutOff;
            minSA[j] = vc.MinSAFlow;
            maxSA[j] = vc.MaxSAFlow;
            maxRA[j] = vc.MaxRAFlow;
          }
          bool sc;
          double[] aFlow = ahu[i].OptimizeVAV
            (isCooling, Controllers[i].splyHumidSP, vavSt, znT, znW, znHL, minSA, maxSA, maxRA, out sc);
          for (int j = 0; j < vlmCtrl[i].Length; j++) vlmCtrl[i][j].SAFlow = aFlow[j];          

          //過剰処理系統があれば制御点を確定して収束計算実施
          if (!sc)
          {
            hasVAV = true;
            ctrlFixed[i] = true;
            initVec[i] = ahu[i].SATemperature; //収束計算用給気温度仮定値を更新
          }
        }
      }
      return hasVAV;
    }

    /// <summary>Updates the chilled/hot water return state.</summary>
    private void UpdateReturnWaterState()
    {
      double tcw, thw;
      tcw = thw = ChilledWaterFlowRate = HotWaterFlowRate = 0;
      foreach (AirHandlingUnit ah in ahu)
      {
        tcw += ah.CoolingCoil.OutletWaterTemperature * ah.CoolingCoil.WaterFlowRate;
        thw += ah.HeatingCoil.OutletWaterTemperature * ah.HeatingCoil.WaterFlowRate;
        ChilledWaterFlowRate += ah.CoolingCoil.WaterFlowRate;
        HotWaterFlowRate += ah.HeatingCoil.WaterFlowRate;
      }
      if (ChilledWaterFlowRate == 0) ChilledWaterReturnTemperature = ChilledWaterSupplyTemperature;
      else ChilledWaterReturnTemperature = tcw / ChilledWaterFlowRate;
      if (HotWaterFlowRate == 0) HotWaterReturnTemperature = HotWaterSupplyTemperature;
      else HotWaterReturnTemperature = thw / HotWaterFlowRate;
    }

    #endregion

    #region インナークラスの定義

    /// <summary>VAV or CAV volume controller.</summary>
      public class VolumeController
    {
      /// <summary>Gets the supply air flow rate [kg/s].</summary>
      public double SAFlow { get; internal set; }

      /// <summary>Gets the return air flow rate [kg/s].</summary>
      public double RAFlow { get { return SAFlow - (MaxSAFlow - MaxRAFlow); } }

      /// <summary>Gets the maximum supply air flow rate [kg/s].</summary>
      public double MaxSAFlow { get; internal set; }

      /// <summary>Gets the maximum return air flow rate [kg/s].</summary>
      public double MaxRAFlow { get; internal set; }

      /// <summary>Gets the minimum supply air flow rate [kg/s].</summary>
      public double MinSAFlow { get; internal set; }

      /// <summary>Gets the room indices served by this AHU.</summary>
      public int RoomIndex { get; internal set; }

      /// <summary>Gets the zone indices served by this AHU.</summary>
      public int ZoneIndex { get; internal set; }

      /// <summary>Gets the temperature setpoint [°C].</summary>
      public double SetpointTemperature { get; internal set; }

      /// <summary>Gets a value indicating whether the system is shut off.</summary>
      public bool IsShutOff { get; internal set; }

      /// <summary>Initializes a new instance.</summary>
      /// <param name="rmIndex">Room index.</param>
      /// <param name="znIndex">Zone index.</param>
      /// <param name="maxSAFlow">Maximum supply air flow rate [kg/s].</param>
      /// <param name="maxRAFlow">Maximum return air flow rate [kg/s].</param>
      /// <param name="minSAFlow">Minimum supply air flow rate [kg/s].</param>
      public VolumeController(int rmIndex, int znIndex, double maxSAFlow, double maxRAFlow, double minSAFlow)
      {
        RoomIndex = rmIndex;
        ZoneIndex = znIndex;
        MaxSAFlow = maxSAFlow;
        MaxRAFlow = maxRAFlow;
        MinSAFlow = minSAFlow;
      }
    }

    /// <summary>AHU control class.</summary>
      public class AHUController
    {
      /// <summary>Initializes a new instance.</summary>
      internal AHUController() { }

      /// <summary>Gets or sets the current operating mode.</summary>
      public OperatingMode Mode { get; set; } = OperatingMode.ShutOff;

      /// <summary>Gets a value indicating whether CAV control is active.</summary>
      public bool IsCAVControl { get; internal set; } = true;

      /// <summary>Gets or sets a value indicating whether return air temperature control is active.</summary>
      public bool IsRATemperatureControl { get; set; } = true;

      /// <summary>Gets or sets the controlled room index.</summary>
      public int TargetRoomIndex { get; set; }

      /// <summary>Gets or sets the controlled zone index.</summary>
      public int TargetZoneIndex { get; set; }

      /// <summary>Gets or sets the CAV supply air temperature setpoint [°C].</summary>
      public double SetpointTemperature { get; set; } = 24.0;

      /// <summary>Gets or sets the minimum supply air humidity ratio [kg/kg].</summary>
      public double MinimumHumidity { get; set; }

      /// <summary>Gets or sets the supply air humidity ratio setpoint [kg/kg].</summary>
      internal double splyHumidSP { get; set; }
    }

    #endregion

  }
}
