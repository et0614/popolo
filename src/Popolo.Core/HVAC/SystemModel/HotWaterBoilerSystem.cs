/* HotWaterBoilerSystem.cs
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
using Popolo.Core.HVAC.FluidCircuit;
using Popolo.Core.Physics;

namespace Popolo.Core.HVAC.SystemModel
{
  /// <summary>Heat source sub-system using hot-water boilers.</summary>
  public class HotWaterBoilerSystem : IHeatSourceSubSystem
  {

    #region 定数宣言


    #endregion

    #region インスタンス変数・プロパティ

    /// <summary>Hot-water boiler.</summary>
    private HotWaterBoiler boiler;

    /// <summary>Hot water pump.</summary>
    private CentrifugalPump hwPump;

    /// <summary>Gets the hot-water boiler.</summary>
    public IReadOnlyHotWaterBoiler Boiler { get { return boiler; } }

    /// <summary>Gets the hot water pump.</summary>
    public IReadOnlyCentrifugalPump HotWaterPump { get { return hwPump; } }

    /// <summary>Gets the total number of boiler units.</summary>
    public int BoilerCount { get; private set; }

    /// <summary>Gets the number of operating units.</summary>
    public int ActiveUnitCount { get; private set; }

    #endregion

    #region IHeatSourceSubSystem実装

    /// <summary>Gets a value indicating whether the chilled water supply is overloaded.</summary>
    public bool IsOverLoad_C { get { return false; } }

    /// <summary>Gets a value indicating whether the hot water supply is overloaded.</summary>
    public bool IsOverLoad_H { get; private set; }

    /// <summary>Gets the selectable operating modes.</summary>
    public HeatSourceSystemModel.OperatingMode SelectableMode
    { get { return HeatSourceSystemModel.OperatingMode.Heating; } }

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

    /// <summary>Gets or sets the hot water flow rate [kg/s].</summary>
    public double HotWaterFlowRate { get; set; }

    /// <summary>Gets the maximum hot water flow rate [kg/s].</summary>
    public double MaxHotWaterFlowRate
    { get { return boiler.MaxWaterFlowRate * BoilerCount; } }

    /// <summary>Gets the minimum hot water flow rate ratio [-].</summary>
    public double MinHotWaterFlowRatio
    { get { return boiler.MinWaterFlowRatio / BoilerCount; } }

    /// <summary>Gets or sets the chilled water return temperature [°C].</summary>
    public double ChilledWaterReturnTemperature { get; set; }

    /// <summary>Gets or sets the chilled water supply temperature setpoint [°C].</summary>
    public double ChilledWaterSupplyTemperatureSetpoint { get; set; }

    /// <summary>Gets the chilled water supply temperature [°C].</summary>
    public double ChilledWaterSupplyTemperature { get; private set; }

    /// <summary>Gets the chilled water flow rate [kg/s].</summary>
    public double ChilledWaterFlowRate { get; private set; }

    /// <summary>Gets the maximum chilled water flow rate [kg/s].</summary>
    public double MaxChilledWaterFlowRate { get { return 0; } }

    /// <summary>Gets the minimum chilled water flow rate ratio [-].</summary>
    public double MinChilledWaterFlowRatio { get { return 0; } }

    /// <summary>Gets or sets the outdoor air conditions.</summary>
    public IReadOnlyMoistAir OutdoorAir { get; set; } = new MoistAir(35, 0.0185);

    /// <summary>Shuts off this heat source sub-system.</summary>
    public void ShutOff()
    {
      IsOverLoad_H = false;
      BoilerCount = 0;
      boiler.ShutOff();
      hwPump.ShutOff();
    }

    /// <summary>Forecasts the supply water temperatures for the given flow rates.</summary>
    /// <param name="chilledWaterFlowRate">Chilled water flow rate [kg/s].</param>
    /// <param name="hotWaterFlowRate">Hot water flow rate [kg/s].</param>
    public void ForecastSupplyWaterTemperature(double chilledWaterFlowRate, double hotWaterFlowRate)
    {
      ChilledWaterFlowRate = chilledWaterFlowRate;

      if (Mode != HeatSourceSystemModel.OperatingMode.Heating || hotWaterFlowRate == 0)
      {
        ShutOff();
        return;
      }

      ActiveUnitCount = (int)Math.Ceiling(hotWaterFlowRate / boiler.MaxWaterFlowRate);
      ActiveUnitCount = Math.Min(BoilerCount, ActiveUnitCount);
      boiler.AmbientTemperature = OutdoorAir.DryBulbTemperature;
      boiler.OutletWaterSetpointTemperature = HotWaterSupplyTemperatureSetpoint;

      while (true)
      {
        //ポンプによる昇温を評価
        double hwFlow = hotWaterFlowRate / ActiveUnitCount;
        hwPump.UpdateState(0.001 * hwFlow);
        double twi = HotWaterReturnTemperature + hwPump.GetElectricConsumption() / (0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat * hwFlow);
        boiler.Update(twi, hwFlow);
        if (ActiveUnitCount == BoilerCount || !boiler.IsOverLoad) break;
        else ActiveUnitCount++;
      }

      IsOverLoad_H = boiler.IsOverLoad;
      if (IsOverLoad_H) HotWaterSupplyTemperature = boiler.OutletWaterTemperature;
      else HotWaterSupplyTemperature = HotWaterSupplyTemperatureSetpoint;
    }

    /// <summary>Fixes (commits) the forecast state as the current state.</summary>
    public void FixState() { }

    #endregion

    #region コンストラクタ

    /// <summary>Initializes a new instance.</summary>
    /// <param name="boiler">Hot-water boiler.</param>
    /// <param name="hwPump">Hot water pump.</param>
    /// <param name="unitCount">Total number of boiler units.</param>
    public HotWaterBoilerSystem(HotWaterBoiler boiler, CentrifugalPump hwPump, int unitCount)
    {
      this.boiler = boiler;
      this.hwPump = hwPump;
      BoilerCount = unitCount;
      Mode = HeatSourceSystemModel.OperatingMode.ShutOff;
    }

    #endregion

  }
}
