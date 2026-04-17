/* GroundHeatSourceHeatPumpSystem.cs
 * 
 * Copyright (C) 2018 E.Togashi
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

using Popolo.Core.HVAC.Storage;
using Popolo.Core.HVAC.HeatSource;
using Popolo.Core.HVAC.FluidCircuit;
using Popolo.Core.Physics;
using Popolo.Core.HVAC.HeatExchanger;
using Popolo.Core.Numerics;

namespace Popolo.Core.HVAC.SystemModel
{
  /// <summary>Ground-source heat pump system.</summary>
  public class GroundHeatSourceHeatPumpSystem : IHeatSourceSubSystem
  {

    #region 定数宣言

    /// <summary>Minimum heat-source water temperature in heating mode [°C].</summary>
    private const double HEAT_MIN_TEMP = 0;

    /// <summary>Maximum heat-source water temperature in cooling mode [°C].</summary>
    private const double COOL_MAX_TEMP = 40;


    #endregion

    #region IHeatSourceSubSystem実装（プロパティ）

    /// <summary>Gets the selectable operating modes.</summary>
    public HeatSourceSystemModel.OperatingMode SelectableMode
    { get { return HeatSourceSystemModel.OperatingMode.Cooling | HeatSourceSystemModel.OperatingMode.Heating; } }

    /// <summary>Gets or sets the current operating mode.</summary>
    public HeatSourceSystemModel.OperatingMode Mode { get; set; }

    /// <summary>Gets or sets the outdoor air conditions.</summary>
    public IReadOnlyMoistAir OutdoorAir { get; set; } = new MoistAir(35, 0.0185);

    /// <summary>Gets or sets the current date and time.</summary>
    public DateTime CurrentDateTime { get; set; }

    /// <summary>Gets or sets the simulation time step [s].</summary>
    public double TimeStep
    {
      get { return gHex.TimeStep; }
      set { gHex.TimeStep = value; }
    }

    /// <summary>Gets a value indicating whether the chilled water supply is overloaded.</summary>
    public bool IsOverLoad_C { get; private set; }

    /// <summary>Gets a value indicating whether the hot water supply is overloaded.</summary>
    public bool IsOverLoad_H { get; private set; }

    /// <summary>Gets or sets the hot water return temperature [°C].</summary>
    public double HotWaterReturnTemperature { get; set; }

    /// <summary>Gets or sets the hot water supply temperature setpoint [°C].</summary>
    public double HotWaterSupplyTemperatureSetpoint
    {
      get { return whp.HotWaterSetPoint; }
      set { whp.HotWaterSetPoint = value; }
    }

    /// <summary>Gets the hot water supply temperature [°C].</summary>
    public double HotWaterSupplyTemperature { get; private set; }

    /// <summary>Gets the hot water flow rate [kg/s].</summary>
    public double HotWaterFlowRate { get; private set; }

    /// <summary>Gets the maximum hot water flow rate [kg/s].</summary>
    public double MaxHotWaterFlowRate { get { return whp.NominalHotWaterFlowRate; } }

    /// <summary>Gets the minimum hot water flow rate ratio [-].</summary>
    public double MinHotWaterFlowRatio { get { return 0.5; } }

    /// <summary>Gets or sets the chilled water return temperature [°C].</summary>
    public double ChilledWaterReturnTemperature { get; set; }

    /// <summary>Gets or sets the chilled water supply temperature setpoint [°C].</summary>
    public double ChilledWaterSupplyTemperatureSetpoint
    {
      get { return whp.ChilledWaterSetPoint; }
      set { whp.ChilledWaterSetPoint = value; }
    }

    /// <summary>Gets the chilled water supply temperature [°C].</summary>
    public double ChilledWaterSupplyTemperature { get; private set; }

    /// <summary>Gets the chilled water flow rate [kg/s].</summary>
    public double ChilledWaterFlowRate { get; private set; }

    /// <summary>Gets the maximum chilled water flow rate [kg/s].</summary>
    public double MaxChilledWaterFlowRate { get { return whp.NominalChilledWaterFlowRate; } }

    /// <summary>Gets the minimum chilled water flow rate ratio [-].</summary>
    public double MinChilledWaterFlowRatio { get { return 0.5; } }

    #endregion

    #region インスタンス変数・プロパティ

    /// <summary>Gets a value indicating whether the system is overloaded.</summary>
    public bool IsGroundHEX_OverLoad { get; private set; }

    /// <summary>Water-source heat pump.</summary>
    private WaterHeatPump whp;

    /// <summary>Ground heat exchanger.</summary>
    private SimpleGroundHeatExchanger gHex;

    /// <summary>Water pump.</summary>
    private CentrifugalPump groundWaterPump, supplyPump;

    /// <summary>Gets the water-source heat pump.</summary>
    public IReadOnlyWaterHeatPump WaterHeatPump { get { return whp; } }

    /// <summary>Gets the ground heat exchanger.</summary>
    public IReadOnlySimpleGroundHeatExchanger GroundHeatExchanger { get { return gHex; } }

    /// <summary>Gets the ground (heat-source) water pump.</summary>
    public IReadOnlyCentrifugalPump GroundWaterPump { get { return groundWaterPump; } }

    /// <summary>Gets the chilled/hot water pump.</summary>
    public IReadOnlyCentrifugalPump SupplyPump { get { return supplyPump; } }

    /// <summary>Gets the heat-source water flow rate [kg/s].</summary>
    public double GroundWaterFlowRate { get; private set; }

    /// <summary>Gets or sets the heat-source water flow rate setpoint [kg/s].</summary>
    public double GroundWaterFlowRateSetpoint
    {
      get { return grndFlowSP; }
      set { if (0 < value) grndFlowSP = value; }
    }

    /// <summary>Heat-source water flow rate setpoint [kg/s].</summary>
    private double grndFlowSP;

    #endregion

    #region IHeatSourceSubSystem実装（メソッド）

    /// <summary>Forecasts the supply water temperatures for the given flow rates.</summary>
    /// <param name="chilledWaterFlowRate">Chilled water flow rate [kg/s].</param>
    /// <param name="hotWaterFlowRate">Hot water flow rate [kg/s].</param>
    public void ForecastSupplyWaterTemperature(double chilledWaterFlowRate, double hotWaterFlowRate)
    {
      ChilledWaterFlowRate = chilledWaterFlowRate;
      HotWaterFlowRate = hotWaterFlowRate;

      if (Mode == HeatSourceSystemModel.OperatingMode.Cooling)
        ForecastCooling_Internal(chilledWaterFlowRate);
      else if (Mode == HeatSourceSystemModel.OperatingMode.Heating)
        ForecastHeating_Internal(hotWaterFlowRate);
      else
      {
        gHex.Update(0, 0);  //土壌熱流のみを計算
        ShutOff();
      }
    }

    /// <summary>Forecasts the cooling operation state.</summary>
    /// <param name="chilledWaterFlowRate">Chilled water flow rate [kg/s].</param>
    private void ForecastCooling_Internal(double chilledWaterFlowRate)
    {
      if (chilledWaterFlowRate <= 0 || ChilledWaterReturnTemperature < ChilledWaterSupplyTemperatureSetpoint)
      {
        gHex.Update(0, 0);  //土壌熱流のみを計算
        ShutOff();
        return;
      }

      //冷温水ポンプの計算
      supplyPump.UpdateState(0.001 * chilledWaterFlowRate);
      double deltaTW = supplyPump.GetElectricConsumption() / (0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat * chilledWaterFlowRate);

      //熱源水ポンプの計算
      GroundWaterFlowRate = GroundWaterFlowRateSetpoint;
      groundWaterPump.UpdateState(0.001 * GroundWaterFlowRate);  //ポンプ状態を更新:比重量はブラインでも同じで良いのか？？？
      double deltaTG = groundWaterPump.GetElectricConsumption() / (gHex.FluidSpecificHeat * GroundWaterFlowRate);

      //熱源水上限温度で計算
      whp.CoolWater(chilledWaterFlowRate, GroundWaterFlowRate, ChilledWaterReturnTemperature + deltaTW, COOL_MAX_TEMP);
      gHex.ForecastState(whp.CoolingWaterOutletTemperature + deltaTG, GroundWaterFlowRate);

      //過負荷の場合は下限温度で計算を打ち切り
      if (COOL_MAX_TEMP < gHex.FluidOutletTemperature) IsGroundHEX_OverLoad = true;
      //熱源水温度を収束計算
      else
      {
        //黄金分割法
        Roots.ErrorFunction eFnc = delegate (double x)
        {
          whp.CoolWater(chilledWaterFlowRate, GroundWaterFlowRate, ChilledWaterReturnTemperature + deltaTW, x);
          gHex.ForecastState(whp.CoolingWaterOutletTemperature + deltaTG, GroundWaterFlowRate);
          return gHex.FluidOutletTemperature - x;
        };
        Roots.Brent(gHex.NearGroundTemperature, COOL_MAX_TEMP, 0.01, eFnc);

        IsGroundHEX_OverLoad = false;
      }

      HotWaterSupplyTemperature = HotWaterReturnTemperature;
      IsOverLoad_H = false;

      IsOverLoad_C = whp.IsOverLoad;
      if (IsOverLoad_C) ChilledWaterSupplyTemperature = whp.ChilledWaterOutletTemperature;
      else ChilledWaterSupplyTemperature = ChilledWaterSupplyTemperatureSetpoint;
    }

    /// <summary>Forecasts the heating operation state.</summary>
    /// <param name="hotWaterFlowRate">Hot water flow rate [kg/s].</param>
    private void ForecastHeating_Internal(double hotWaterFlowRate)
    {
      if (hotWaterFlowRate <= 0 || HotWaterSupplyTemperatureSetpoint < HotWaterReturnTemperature)
      {
        gHex.Update(0, 0);  //土壌熱流のみを計算
        ShutOff();
        return;
      }

      //冷温水ポンプの計算
      supplyPump.UpdateState(0.001 * hotWaterFlowRate);
      double deltaTW = supplyPump.GetElectricConsumption() / (0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat * hotWaterFlowRate);

      //熱源水ポンプの計算
      GroundWaterFlowRate = GroundWaterFlowRateSetpoint;
      groundWaterPump.UpdateState(0.001 * GroundWaterFlowRate);  //ポンプ状態を更新:比重量はブラインでも同じで良いのか？？？
      double deltaTG = groundWaterPump.GetElectricConsumption() / (gHex.FluidSpecificHeat * GroundWaterFlowRate);

      //熱源水下限温度で計算
      whp.HeatWater(hotWaterFlowRate, GroundWaterFlowRate, HotWaterReturnTemperature + deltaTW, HEAT_MIN_TEMP);
      gHex.ForecastState(whp.HeatSourceWaterOutletTemperature + deltaTG, GroundWaterFlowRate);

      //過負荷の場合は下限温度で計算を打ち切り
      if (gHex.FluidOutletTemperature < HEAT_MIN_TEMP) IsGroundHEX_OverLoad = true;
      //熱源水温度を収束計算
      else
      {
        //黄金分割法
        Roots.ErrorFunction eFnc = delegate (double x) 
        {
          whp.HeatWater(hotWaterFlowRate, GroundWaterFlowRate, HotWaterReturnTemperature + deltaTW, x);
          gHex.ForecastState(whp.HeatSourceWaterOutletTemperature + deltaTG, GroundWaterFlowRate);
          return gHex.FluidOutletTemperature - x;
        };
        Roots.Brent(HEAT_MIN_TEMP, gHex.NearGroundTemperature, 0.01, eFnc);

        IsGroundHEX_OverLoad = false;
      }

      ChilledWaterSupplyTemperature = ChilledWaterReturnTemperature;
      IsOverLoad_C = false;

      IsOverLoad_H = whp.IsOverLoad;
      if (IsOverLoad_H) HotWaterSupplyTemperature = whp.HotWaterOutletTemperature;
      else HotWaterSupplyTemperature = HotWaterSupplyTemperatureSetpoint;
    }

    /// <summary>Fixes (commits) the forecast state as the current state.</summary>
    public void FixState()
    { gHex.FixState(); }

    /// <summary>Shuts off this heat source sub-system.</summary>
    public void ShutOff()
    {
      IsOverLoad_C = IsOverLoad_H = false;
      GroundWaterFlowRate = 0;
      groundWaterPump.ShutOff();
      supplyPump.ShutOff();
      whp.ShutOff();
    }

    #endregion

    #region コンストラクタ

    /// <summary></summary>
    /// <param name="whp"></param>
    /// <param name="gHex"></param>
    /// <param name="groundWaterPump"></param>
    /// <param name="supplyPump"></param>
    public GroundHeatSourceHeatPumpSystem
      (WaterHeatPump whp, SimpleGroundHeatExchanger gHex, CentrifugalPump groundWaterPump, CentrifugalPump supplyPump)
    {
      this.whp = whp;
      this.gHex = gHex;
      this.groundWaterPump = groundWaterPump;
      this.supplyPump = supplyPump;

      GroundWaterFlowRateSetpoint = 1000 * groundWaterPump.DesignFlowRate;
    }

    #endregion

  }
}
