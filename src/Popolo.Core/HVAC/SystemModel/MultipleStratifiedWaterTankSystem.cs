/* MultipleStratifiedWaterTankSystem.cs
 * 
 * Copyright (C) 2016 E.Togashi
 * 
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 3 of the License, or (at
 * your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful, but
 * WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301, USA.
 */

using System;

using Popolo.Core.HVAC.HeatExchanger;
using Popolo.Core.HVAC.HeatSource;
using Popolo.Core.HVAC.FluidCircuit;
using Popolo.Core.Physics;
using Popolo.Core.Numerics;
using Popolo.Core.HVAC.Storage;

namespace Popolo.Core.HVAC.SystemModel
{
  /// <summary>Heat source sub-system using stratified thermal storage tanks.</summary>
  public class MultipleStratifiedWaterTankSystem : IHeatSourceSubSystem
  {

    #region 定数宣言


    #endregion

    #region インスタンス変数・プロパティ

    /// <summary>Gets a value indicating whether a forecast calculation is in progress.</summary>
    private bool isForecasting = false;

    /// <summary>Temperature distribution inside the tank.</summary>
    private double[] oldTemps;

    /// <summary>Temperature rise due to chiller pump work [K].</summary>
    private double dtChillingPump, dtCoolingPump;

    /// <summary>Centrifugal chiller.</summary>
    private ICentrifugalChiller chiller;

    /// <summary>Water pump.</summary>
    private CentrifugalPump cdwPump, chwPump, chgPump, disPump;

    /// <summary>Cooling tower.</summary>
    private CoolingTower cTower;

    /// <summary>Plate heat exchanger.</summary>
    private PlateHeatExchanger pHex;

    /// <summary>Stratified thermal storage tank.</summary>
    private MultipleStratifiedWaterTank wTank;

    /// <summary>Gets the centrifugal chiller.</summary>
    public IReadOnlyCentrifugalChiller Chiller { get { return chiller; } }

    /// <summary>Gets the chilled water pump.</summary>
    public IReadOnlyCentrifugalPump ChilledWaterPump { get { return chwPump; } }

    /// <summary>Gets the cooling water pump.</summary>
    public IReadOnlyCentrifugalPump CoolingWaterPump { get { return cdwPump; } }

    /// <summary>Gets the thermal storage charge pump.</summary>
    public IReadOnlyCentrifugalPump ChargePump { get { return chgPump; } }

    /// <summary>Gets the thermal storage discharge pump.</summary>
    public IReadOnlyCentrifugalPump DischargePump { get { return disPump; } }

    /// <summary>Gets the cooling tower.</summary>
    public IReadOnlyCoolingTower CoolingTower { get { return cTower; } }

    /// <summary>Gets the plate heat exchanger.</summary>
    public IReadOnlyPlateHeatExchanger PlateHeatExchanger { get { return pHex; } }

    /// <summary>Stratified thermal storage tank.</summary>
    public IReadOnlyMultipleStratifiedWaterTank WaterTank { get { return wTank; } }

    /// <summary>Gets the total number of chiller units.</summary>
    public int ChillerNumber { get; private set; }

    /// <summary>Gets the number of cooling tower cells per chiller unit.</summary>
    public int CoolingTowerNumber { get; private set; }

    /// <summary>Gets or sets a value indicating whether to control cooling water temperature.</summary>
    public bool ControlCoolingWaterTemperature { get; set; }

    /// <summary>Gets or sets the cooling water temperature setpoint [°C].</summary>
    public double CoolingWaterTemperatureSetpoint { get; set; } = 32;

    /// <summary>Gets or sets a value indicating whether the chiller is operating.</summary>
    public bool OperateChiler
    {
      get { return chiller.IsOperating; }
      set { chiller.IsOperating = value; }
    }

    /// <summary>Gets or sets the state-update time step for the thermal storage tank [s].</summary>
    public double TankUpdateTimeStep { get; set; } = 600;

    /// <summary>Gets or sets the thermal storage temperature [°C].</summary>
    public double StorageTemperature
    {
      get { return chiller.ChilledWaterOutletSetPointTemperature; }
      set { chiller.ChilledWaterOutletSetPointTemperature = value; }
    }

    /// <summary>Gets a value indicating whether the system is in thermal storage charge mode.</summary>
    public bool Charging
    {
      get { return chiller.IsOperating; }
      set { chiller.IsOperating = value; }
    }

    /// <summary>Gets or sets the cooling water flow rate setpoint [kg/s].</summary>
    public double CoolingWaterFlowSetpoint { get; set; }

    #endregion

    #region IHeatSourceSubSystem実装

    /// <summary>Gets a value indicating whether the chilled water supply is overloaded.</summary>
    public bool IsOverLoad_C { get; private set; }

    /// <summary>Gets a value indicating whether the hot water supply is overloaded.</summary>
    public bool IsOverLoad_H { get; private set; }

    /// <summary>Gets the selectable operating modes.</summary>
    public HeatSourceSystemModel.OperatingMode SelectableMode
    { get { return HeatSourceSystemModel.OperatingMode.Cooling; } }

    /// <summary>Gets or sets the current operating mode.</summary>
    public HeatSourceSystemModel.OperatingMode Mode { get; set; }

    /// <summary>Gets or sets the current date and time.</summary>
    public DateTime CurrentDateTime { get; set; }

    /// <summary>Gets or sets the simulation time step [s].</summary>
    public double TimeStep { get; set; }

    /// <summary>Gets or sets the hot water return temperature [°C].</summary>
    public double HotWaterReturnTemperature { get; set; } = 40;

    /// <summary>Gets or sets the hot water supply temperature setpoint [°C].</summary>
    public double HotWaterSupplyTemperatureSetpoint { get; set; } = 45;

    /// <summary>Gets the hot water supply temperature [°C].</summary>
    public double HotWaterSupplyTemperature { get; private set; } = 45;

    /// <summary>Gets the hot water flow rate [kg/s].</summary>
    public double HotWaterFlowRate { get; private set; }

    /// <summary>Gets the maximum hot water flow rate [kg/s].</summary>
    public double MaxHotWaterFlowRate { get { return 0; } }

    /// <summary>Gets the minimum hot water flow rate ratio [-].</summary>
    public double MinHotWaterFlowRatio { get { return 0; } }

    /// <summary>Gets or sets the chilled water return temperature [°C].</summary>
    public double ChilledWaterReturnTemperature { get; set; } = 12;

    /// <summary>Gets or sets the chilled water supply temperature setpoint [°C].</summary>
    public double ChilledWaterSupplyTemperatureSetpoint
    {
      get { return pHex.SupplyTemperatureSetpoint; }
      set { pHex.SupplyTemperatureSetpoint = value; }
    }

    /// <summary>Gets the chilled water supply temperature [°C].</summary>
    public double ChilledWaterSupplyTemperature { get; private set; } = 7;

    /// <summary>Gets the chilled water flow rate [kg/s].</summary>
    public double ChilledWaterFlowRate { get; private set; }

    /// <summary>Gets the maximum chilled water flow rate [kg/s].</summary>
    public double MaxChilledWaterFlowRate { get { return pHex.MaxSupplyFlowRate; } }

    /// <summary>Gets the minimum chilled water flow rate ratio [-].</summary>
    public double MinChilledWaterFlowRatio { get { return 0; } }

    /// <summary>Gets or sets the outdoor air conditions.</summary>
    public IReadOnlyMoistAir OutdoorAir { get; set; } = new MoistAir(35, 0.0185);

    /// <summary>Shuts off this heat source sub-system.</summary>
    public void ShutOff()
    {
      IsOverLoad_C = IsOverLoad_H = false;
      chiller.ShutOff();
      chwPump.ShutOff();
      cdwPump.ShutOff();
      chgPump.ShutOff();
      disPump.ShutOff();
      cTower.ShutOff();
    }

    /// <summary>Forecasts the supply water temperatures for the given flow rates.</summary>
    /// <param name="chilledWaterFlowRate">Chilled water flow rate [kg/s].</param>
    /// <param name="hotWaterFlowRate">Hot water flow rate [kg/s].</param>
    public void ForecastSupplyWaterTemperature(double chilledWaterFlowRate, double hotWaterFlowRate)
    {
      ChilledWaterFlowRate = chilledWaterFlowRate;
      HotWaterFlowRate = hotWaterFlowRate;
      cTower.SetOutdoorAirState(OutdoorAir.WetbulbTemperature, OutdoorAir.HumidityRatio);

      //水槽内温度分布の一時保存と復元
      if (isForecasting) wTank.InitializeTemperature(oldTemps);
      else
      {
        isForecasting = true;
        wTank.GetTemperatures(ref oldTemps);
      }

      //タイムステップが経過するまで水槽温度更新を続ける
      double remTime = TimeStep;
      double aveTemp = 0;
      IsOverLoad_C = false;
      while (true)
      {
        bool isLastCalc = remTime <= TankUpdateTimeStep;
        wTank.TimeStep = TankUpdateTimeStep;
        if (isLastCalc) wTank.TimeStep = remTime;
        remTime -= wTank.TimeStep;

        //冷凍機稼働かつ二次側負荷有りの場合はHEX所要流量を収束計算（追掛運転）
        if (Charging && 0 < ChilledWaterFlowRate)
        {
          //冷水ポンプの昇温幅を計算
          chgPump.UpdateState(chgPump.DesignFlowRate);
          chwPump.UpdateState(0.001 * ChilledWaterFlowRate);
          double dtCHP = chwPump.GetElectricConsumption() / (0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat * ChilledWaterFlowRate);

          Roots.ErrorFunction eFnc = delegate (double hexFlow)
          {
            CalcHexHeatTransfer(hexFlow, ChilledWaterReturnTemperature + dtCHP);
            return pHex.SupplyTemperature - ChilledWaterSupplyTemperatureSetpoint;
          };

          //最大流量で処理可能か
          if (eFnc(pHex.MaxHeatSourceFlowRate) < 0)
          {
            IsOverLoad_C = false;
            //最小流量で制御可能か
            if (0 < eFnc(pHex.MaxHeatSourceFlowRate * 0.001))
              Roots.Bisection(eFnc, pHex.MaxHeatSourceFlowRate * 0.001,
                pHex.MaxHeatSourceFlowRate, 0.01, pHex.MaxHeatSourceFlowRate * 0.01, 20);
          }
          else IsOverLoad_C = true;

          //水槽内温度更新処理
          double tTNKin;
          bool isDownFlow = chiller.ChilledWaterFlowRate < pHex.HeatSourceFlowRate;
          if (isDownFlow) tTNKin = pHex.HeatSourceOutletTemperature 
              + disPump.GetElectricConsumption() / (0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat * pHex.HeatSourceFlowRate);
          else tTNKin = chiller.ChilledWaterOutletTemperature;
          wTank.ForecastState
            (tTNKin, 0.001 * Math.Abs(chiller.ChilledWaterFlowRate - pHex.HeatSourceFlowRate), isDownFlow);
        }
        //冷凍機非稼働+二次側負荷有り（放熱運転）
        else if (0 < ChilledWaterFlowRate)
        {
          chiller.ShutOff();
          chgPump.ShutOff();
          cdwPump.ShutOff();
          cTower.ShutOff();

          //冷水ポンプの昇温幅を計算
          chwPump.UpdateState(0.001 * ChilledWaterFlowRate);
          double dtCHP = chwPump.GetElectricConsumption() / (0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat * ChilledWaterFlowRate);

          //放熱用プレート熱交換器の必要通水量を計算
          pHex.ControlSupplyTemperature
            (WaterTank.LowerOutletTemperarture, ChilledWaterReturnTemperature + dtCHP, ChilledWaterFlowRate);

          //水槽内温度を更新
          double tankInletTemp = 0;
          if (0 < pHex.HeatSourceFlowRate)
          {
            disPump.UpdateState(0.001 * pHex.HeatSourceFlowRate);
            tankInletTemp = pHex.HeatSourceOutletTemperature
              + disPump.GetElectricConsumption() / (0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat * pHex.HeatSourceFlowRate);
          }
          wTank.ForecastState(tankInletTemp, 0.001 * pHex.HeatSourceFlowRate, true);

          IsOverLoad_C = IsOverLoad_C || pHex.IsOverLoad;
        }
        //冷凍機稼働+二次側負荷無し（蓄熱運転）
        else if (Charging)
        {
          pHex.ShutOff();
          disPump.ShutOff();
          chwPump.ShutOff();
          chgPump.UpdateState(chgPump.DesignFlowRate);
          CalcChillerAndCoolingTower(wTank.UpperOutletTemperarture + dtChillingPump);
          wTank.ForecastState
            (chiller.ChilledWaterOutletTemperature, 0.001 * chiller.ChilledWaterFlowRate, false);
          IsOverLoad_C = false;
        }
        //冷凍機非稼働+二次側負荷無し（蓄熱槽熱損失のみ）
        else
        {
          ShutOff();
          wTank.ForecastState(0, 0, true);
          IsOverLoad_C = false;
        }
        aveTemp += pHex.SupplyTemperature * wTank.TimeStep;

        if (isLastCalc) break;
      }

      //過負荷の場合には平均的な供給水温を出力
      if (IsOverLoad_C) ChilledWaterSupplyTemperature = aveTemp / TimeStep;
      else ChilledWaterSupplyTemperature = ChilledWaterSupplyTemperatureSetpoint;
    }

    /// <summary>Updates the heat exchanger heat transfer.</summary>
    /// <param name="hexFlow">Heat exchanger flow rate [kg/s].</param>
    /// <param name="rtnTmp">Chilled water return temperature including pump heat gain [°C].</param>
    private void CalcHexHeatTransfer(double hexFlow, double rtnTmp)
    {
      //水槽内の流れ方向を確定
      double ttlChilFlow = chiller.MaxChilledWaterFlowRate * ChillerNumber;
      bool isDownFlow = ttlChilFlow < hexFlow;

      //ターボ冷凍機供給温度を仮定
      double chilOut = StorageTemperature;
      double chilIn = dtChillingPump;
      if (isDownFlow)
      {
        double tHexIn = (WaterTank.LowerOutletTemperarture * (hexFlow - ttlChilFlow)
          + chilOut * ttlChilFlow) / hexFlow;
        pHex.Update(tHexIn, rtnTmp, hexFlow, ChilledWaterFlowRate);
        disPump.UpdateState(pHex.HeatSourceFlowRate);
        double dtDisP = disPump.GetElectricConsumption() / (0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat * pHex.HeatSourceFlowRate);
        chilIn += pHex.HeatSourceOutletTemperature + dtDisP;
      }
      else
      {
        pHex.Update(chilOut, rtnTmp, hexFlow, ChilledWaterFlowRate);
        disPump.UpdateState(pHex.HeatSourceFlowRate);
        double dtDisP = disPump.GetElectricConsumption() / (0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat * pHex.HeatSourceFlowRate);
        chilIn += ((pHex.HeatSourceOutletTemperature + dtDisP) * hexFlow 
          + wTank.UpperOutletTemperarture * (ttlChilFlow - hexFlow)) / ttlChilFlow;
      }

      //冷却塔と冷凍機の連成計算
      CalcChillerAndCoolingTower(chilIn);

      //ターボ冷凍機過負荷（冷水出口温度が上昇）の場合には1回だけHEX交換熱量を補正
      if (chiller.IsOverLoad)
      {
        if (isDownFlow)
        {
          double tHexIn = (WaterTank.LowerOutletTemperarture * (hexFlow - ttlChilFlow)
            + chiller.ChilledWaterOutletTemperature * ttlChilFlow) / hexFlow;
          pHex.Update(tHexIn, rtnTmp, hexFlow, ChilledWaterFlowRate);
        }
        else
          pHex.Update(chiller.ChilledWaterOutletTemperature, rtnTmp, hexFlow, ChilledWaterFlowRate);
      }
    }

    /// <summary>Performs coupled chiller and cooling tower calculation.</summary>
    /// <param name="inletChilledWaterTemp">Chilled water return temperature [°C].</param>
    private void CalcChillerAndCoolingTower(double inletChilledWaterTemp)
    {
      //冷却塔と冷凍機の連成計算（冷却水温度の計算）
      double mch = 1000 * chgPump.DesignFlowRate;
      // 2026.01.09 修正: 冷却水流量は設定値に従う
      double vcd = Math.Min(cdwPump.DesignFlowRate, 0.001 * CoolingWaterFlowSetpoint);
      double mcd = 1000 * vcd;
      cdwPump.UpdateState(vcd);
      //double mcd = 1000 * cdwPump.DesignFlowRate;
      //cdwPump.UpdateState(cdwPump.DesignFlowRate);

      bool needIteration = !ControlCoolingWaterTemperature;
      if (ControlCoolingWaterTemperature)
      {
        chiller.Update(CoolingWaterTemperatureSetpoint + dtCoolingPump, inletChilledWaterTemp, mcd, mch);
        cTower.OutletWaterSetPointTemperature = CoolingWaterTemperatureSetpoint;
        cTower.Update(chiller.CoolingWaterOutletTemperature, true);
        if (cTower.IsOverLoad) needIteration = true;
      }
      //過負荷または冷却水温度成行の場合には収束計算
      if (needIteration)
      {
        Roots.ErrorFunction eFnc = delegate (double cdt)
        {
          chiller.Update(cdt + dtCoolingPump, inletChilledWaterTemp, mcd, mch);
          cTower.Update(chiller.CoolingWaterOutletTemperature, cTower.MaxAirFlowRate);
          return cTower.OutletWaterTemperature - cdt;
        };
        double fmax = eFnc(OutdoorAir.WetbulbTemperature);
        double fmin = eFnc(37);
        if (0 <= fmin && fmax <= 0)
          Roots.Bisection(eFnc, 37, OutdoorAir.WetbulbTemperature, fmin, fmax, 0.01, 0.01, 10);
      }
    }

    /// <summary>Fixes (commits) the forecast state as the current state.</summary>
    public void FixState() { isForecasting = false; }

    #endregion

    #region コンストラクタ

    /// <summary>Initializes a new instance.</summary>
    /// <param name="waterTank">Stratified thermal storage tank.</param>
    /// <param name="plateHex">Plate heat exchanger.</param>
    /// <param name="chiller">Centrifugal chiller.</param>
    /// <param name="chwPump">Chilled water pump.</param>
    /// <param name="cdwPump">Cooling water pump.</param>
    /// <param name="chargePump">Thermal storage charge pump.</param>
    /// <param name="dischargePump">Thermal storage discharge pump.</param>
    /// <param name="cTower">Cooling tower.</param>
    /// <param name="chillerNumber">Number of chiller units.</param>
    /// <param name="coolingTowerNumber">Number of cooling tower units per chiller.</param>
    public MultipleStratifiedWaterTankSystem
      (MultipleStratifiedWaterTank waterTank, PlateHeatExchanger plateHex, ICentrifugalChiller chiller,
      CentrifugalPump chwPump, CentrifugalPump cdwPump, CentrifugalPump chargePump,
      CentrifugalPump dischargePump, CoolingTower cTower, int chillerNumber, int coolingTowerNumber)
    {
      this.chiller = chiller;
      this.chwPump = chwPump;
      this.cdwPump = cdwPump;
      this.chgPump = chargePump;
      this.disPump = dischargePump;
      this.cTower = cTower;
      this.pHex = plateHex;
      this.wTank = waterTank;
      this.ChillerNumber = chillerNumber;
      this.CoolingTowerNumber = coolingTowerNumber;
      oldTemps = new double[WaterTank.LayerNumber];

      //冷凍機ポンプの昇温幅を計算・保存
      chgPump.UpdateState(chgPump.DesignFlowRate);
      dtChillingPump = chgPump.GetElectricConsumption() / (0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat * 1000 * chgPump.DesignFlowRate);
      cdwPump.UpdateState(cdwPump.DesignFlowRate);
      dtCoolingPump = cdwPump.GetElectricConsumption() / (0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat * 1000 * cdwPump.DesignFlowRate);

      Mode = HeatSourceSystemModel.OperatingMode.ShutOff;
    }

    #endregion

  }
}
