/* AirHeatSourceModularChillersSystem.cs
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

using Popolo.Core.HVAC.HeatSource;
using Popolo.Core.Physics;
using Popolo.Core.HVAC.FluidCircuit;

namespace Popolo.Core.HVAC.SystemModel
{
  /// <summary>Heat source sub-system using air-heat-source modular chillers/heat pumps.</summary>
  public class AirHeatSourceModularChillersSystem : IHeatSourceSubSystem
  {

    #region 定数宣言


    #endregion

    #region インスタンス変数・プロパティ

    /// <summary>Air-heat-source heat pump (modular chiller).</summary>
    private AirHeatSourceModularChillers mChiller;

    /// <summary>Chilled and hot water pumps.</summary>
    private CentrifugalPump chwPump, hwPump;

    /// <summary>Gets the air-heat-source modular chiller.</summary>
    public IReadOnlyAirHeatSourceModularChillers AirHeatSourceModularChillers { get { return mChiller; } }

    /// <summary>Gets the chilled water pump.</summary>
    public IReadOnlyCentrifugalPump ChilledWaterPump { get { return chwPump; } }

    /// <summary>Gets the hot water pump.</summary>
    public IReadOnlyCentrifugalPump HotWaterPump { get { return hwPump; } }

    /// <summary>Gets the total number of modular chiller sets.</summary>
    public int ChillerCount { get; private set; }

    /// <summary>Gets the number of operating units.</summary>
    public int ActiveChillerCount { get; private set; }

    #endregion

    #region IHeatSourceSubSystem実装

    /// <summary>Gets a value indicating whether the chilled water supply is overloaded.</summary>
    public bool IsOverLoad_C { get; private set; }

    /// <summary>Gets a value indicating whether the hot water supply is overloaded.</summary>
    public bool IsOverLoad_H { get; private set; }

    /// <summary>Gets the selectable operating modes.</summary>
    public HeatSourceSystemModel.OperatingMode SelectableMode { get; }

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
    public double MaxHotWaterFlowRate
    { get { return mChiller.MaxHotWaterFlowRate * ChillerCount; } }

    /// <summary>Gets the minimum hot water flow rate ratio [-].</summary>
    public double MinHotWaterFlowRatio
    { get { return mChiller.MinHotWaterFlowRate / MaxHotWaterFlowRate; } }

    /// <summary>Gets or sets the chilled water return temperature [°C].</summary>
    public double ChilledWaterReturnTemperature { get; set; } = 12;

    /// <summary>Gets or sets the chilled water supply temperature setpoint [°C].</summary>
    public double ChilledWaterSupplyTemperatureSetpoint { get; set; } = 7;

    /// <summary>Gets the chilled water supply temperature [°C].</summary>
    public double ChilledWaterSupplyTemperature { get; private set; } = 7;

    /// <summary>Gets the chilled water flow rate [kg/s].</summary>
    public double ChilledWaterFlowRate { get; private set; }

    /// <summary>Gets the maximum chilled water flow rate [kg/s].</summary>
    public double MaxChilledWaterFlowRate
    { get { return mChiller.MaxChilledWaterFlowRate * ChillerCount; } }

    /// <summary>Gets the minimum chilled water flow rate ratio [-].</summary>
    public double MinChilledWaterFlowRatio
    { get { return mChiller.MinChilledWaterFlowRate / MaxChilledWaterFlowRate; } }

    /// <summary>Gets or sets the outdoor air conditions.</summary>
    public IReadOnlyMoistAir OutdoorAir { get; set; } = new MoistAir(35, 0.0185);

    /// <summary>Shuts off this heat source sub-system.</summary>
    public void ShutOff()
    {
      IsOverLoad_C = IsOverLoad_H = false;
      ActiveChillerCount = 0;
      mChiller.ShutOff();
      chwPump.ShutOff();
      hwPump.ShutOff();
    }

    /// <summary>Forecasts the supply water temperatures for the given flow rates.</summary>
    /// <param name="chilledWaterFlowRate">Chilled water flow rate [kg/s].</param>
    /// <param name="hotWaterFlowRate">Hot water flow rate [kg/s].</param>
    public void ForecastSupplyWaterTemperature(double chilledWaterFlowRate, double hotWaterFlowRate)
    {
      ChilledWaterFlowRate = chilledWaterFlowRate;
      HotWaterFlowRate = hotWaterFlowRate;

      if (Mode == HeatSourceSystemModel.OperatingMode.Cooling) ForecastCooling_Internal(chilledWaterFlowRate);
      else if (Mode == HeatSourceSystemModel.OperatingMode.Heating) ForecastHeating_Internal(hotWaterFlowRate);
      else ShutOff();
    }

    /// <summary>Forecasts the heating operation state.</summary>
    /// <param name="hotWaterFlowRate">Hot water flow rate [kg/s].</param>
    private void ForecastHeating_Internal(double hotWaterFlowRate)
    {
      if (hotWaterFlowRate == 0)
      {
        ShutOff();
        return;
      }
      mChiller.Mode = HeatSource.AirHeatSourceModularChillers.OperatingMode.Heating;
      ChilledWaterSupplyTemperature = ChilledWaterReturnTemperature;

      ActiveChillerCount = (int)Math.Ceiling(hotWaterFlowRate / mChiller.MaxHotWaterFlowRate);
      ActiveChillerCount = Math.Min(ChillerCount, ActiveChillerCount);
      mChiller.WaterOutletSetpointTemperature = HotWaterSupplyTemperatureSetpoint;
      while (true)
      {
        //ポンプによる昇温を評価
        double hwFlow = hotWaterFlowRate / ActiveChillerCount;
        hwPump.UpdateState(0.001 * hwFlow);
        double twi = HotWaterReturnTemperature + hwPump.GetElectricConsumption() / (0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat * hwFlow);

        mChiller.Update(twi, hwFlow, OutdoorAir.DryBulbTemperature);
        IsOverLoad_H = mChiller.IsOverLoad;
        if (ActiveChillerCount == ChillerCount || !IsOverLoad_H) break;
        else ActiveChillerCount++;
      }

      if (mChiller.IsOverLoad) HotWaterSupplyTemperature = mChiller.WaterOutletTemperature;
      else HotWaterSupplyTemperature = HotWaterSupplyTemperatureSetpoint;
    }

    /// <summary>Forecasts the cooling operation state.</summary>
    /// <param name="chilledWaterFlowRate">Chilled water flow rate [kg/s].</param>
    private void ForecastCooling_Internal(double chilledWaterFlowRate)
    {
      if (chilledWaterFlowRate == 0)
      {
        ShutOff();
        return;
      }
      mChiller.Mode = HeatSource.AirHeatSourceModularChillers.OperatingMode.Cooling;
      HotWaterSupplyTemperature = HotWaterReturnTemperature;

      ActiveChillerCount = (int)Math.Ceiling(chilledWaterFlowRate / mChiller.MaxChilledWaterFlowRate);
      ActiveChillerCount = Math.Min(ChillerCount, ActiveChillerCount);
      mChiller.WaterOutletSetpointTemperature = ChilledWaterSupplyTemperatureSetpoint;
      while (true)
      {
        //ポンプによる昇温を評価
        double chwFlow = chilledWaterFlowRate / ActiveChillerCount;
        chwPump.UpdateState(0.001 * chwFlow);
        double twi = ChilledWaterReturnTemperature + chwPump.GetElectricConsumption() / (0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat * chwFlow);

        mChiller.Update(twi, chwFlow, OutdoorAir.DryBulbTemperature);
        IsOverLoad_C = mChiller.IsOverLoad;
        if (ActiveChillerCount == ChillerCount || !IsOverLoad_C) break;
        else ActiveChillerCount++;
      }

      if (mChiller.IsOverLoad) ChilledWaterSupplyTemperature = mChiller.WaterOutletTemperature;
      else ChilledWaterSupplyTemperature = ChilledWaterSupplyTemperatureSetpoint;
    }

    /// <summary>Fixes (commits) the forecast state as the current state.</summary>
    public void FixState() { }

    #endregion

    #region コンストラクタ

    /// <summary>Initializes a new instance.</summary>
    /// <param name="mChiller">Modular chiller/heat pump unit.</param>
    /// <param name="chwPump">Chilled water pump.</param>
    /// <param name="hwPump">Hot water pump.</param>
    /// <param name="count">Total number of modular chiller units.</param>
    public AirHeatSourceModularChillersSystem
      (AirHeatSourceModularChillers mChiller, CentrifugalPump chwPump, CentrifugalPump hwPump, int count)
    {
      this.mChiller = mChiller;
      this.ChillerCount = count;
      this.chwPump = chwPump;
      this.hwPump = hwPump;

      //加熱運転・冷却運転対応
      SelectableMode = HeatSourceSystemModel.OperatingMode.Cooling | HeatSourceSystemModel.OperatingMode.Heating;
      Mode = HeatSourceSystemModel.OperatingMode.ShutOff;
    }

    #endregion

  }
}
