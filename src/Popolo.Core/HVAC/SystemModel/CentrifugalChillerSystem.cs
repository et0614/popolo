/* CentrifugalChillerSystem.cs
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
using Popolo.Core.HVAC.HeatExchanger;
using Popolo.Core.Physics;
using Popolo.Core.Numerics;

namespace Popolo.Core.HVAC.SystemModel
{
  /// <summary>Heat source sub-system using centrifugal chillers.</summary>
  public class CentrifugalChillerSystem : IHeatSourceSubSystem
  {

    #region 定数宣言


    #endregion

    #region インスタンス変数・プロパティ

    /// <summary>Centrifugal chiller.</summary>
    private ICentrifugalChiller chiller;

    /// <summary>Water pump.</summary>
    private CentrifugalPump cdwPump, chwPump;

    /// <summary>Cooling tower.</summary>
    private CoolingTower cTower;

    /// <summary>Gets the centrifugal chiller.</summary>
    public IReadOnlyCentrifugalChiller Chiller { get { return chiller; } }

    /// <summary>Gets the chilled water pump.</summary>
    public IReadOnlyCentrifugalPump ChilledWaterPump { get { return chwPump; } }

    /// <summary>Gets the cooling water pump.</summary>
    public IReadOnlyCentrifugalPump CoolingWaterPump { get { return cdwPump; } }

    /// <summary>Gets the cooling tower.</summary>
    public IReadOnlyCoolingTower CoolingTower { get { return cTower; } }

    /// <summary>Gets the total number of chiller units.</summary>
    public int ChillerCount { get; private set; }

    /// <summary>Gets the number of cooling tower cells per chiller unit.</summary>
    public int CoolingTowerCount { get; private set; }

    /// <summary>Gets the number of operating units.</summary>
    public int ActiveChillerCount { get; private set; }

    /// <summary>Gets or sets a value indicating whether to operate cooling towers one-to-one with chillers.</summary>
    public bool OperateCoolingTowerOneOnOne { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether to control cooling water temperature.</summary>
    public bool ControlCoolingWaterTemperature { get; set; }

    /// <summary>Gets or sets a value indicating whether to control cooling water flow rate in proportion to load.</summary>
    public bool AutoControlCoolingWaterFlowRate { get; set; }

    /// <summary>Gets or sets the minimum cooling water flow rate ratio [-].</summary>
    public double MinCoolingWaterFlowRatio { get; set; } = 0.5;

    /// <summary>Gets or sets the cooling water temperature setpoint [°C].</summary>
    public double CoolingWaterTemperatureSetpoint { get; set; } = 32;

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
    public double MaxHotWaterFlowRate
    { get { return 0; } }

    /// <summary>Gets the minimum hot water flow rate ratio [-].</summary>
    public double MinHotWaterFlowRatio
    { get { return 0; } }

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
    { get { return chiller.MaxChilledWaterFlowRate * ChillerCount; } }

    /// <summary>Gets the minimum chilled water flow rate ratio [-].</summary>
    public double MinChilledWaterFlowRatio
    { get { return chiller.MinChilledWaterFlowRatio / ChillerCount; } }

    /// <summary>Gets or sets the outdoor air conditions.</summary>
    public IReadOnlyMoistAir OutdoorAir { get; set; } = new MoistAir(35, 0.0185);

    /// <summary>Shuts off this heat source sub-system.</summary>
    public void ShutOff()
    {
      IsOverLoad_C = IsOverLoad_H = false;
      ActiveChillerCount = 0;
      chiller.ShutOff();
      chwPump.ShutOff();
      cdwPump.ShutOff();
      cTower.ShutOff();
    }

    /// <summary>Forecasts the supply water temperatures for the given flow rates.</summary>
    /// <param name="chilledWaterFlowRate">Chilled water flow rate [kg/s].</param>
    /// <param name="hotWaterFlowRate">Hot water flow rate [kg/s].</param>
    public void ForecastSupplyWaterTemperature(double chilledWaterFlowRate, double hotWaterFlowRate)
    {
      ChilledWaterFlowRate = chilledWaterFlowRate;
      HotWaterFlowRate = hotWaterFlowRate;

      if (Mode != HeatSourceSystemModel.OperatingMode.Cooling || chilledWaterFlowRate == 0)
      {
        ShutOff();
        return;
      }

      chiller.IsOperating = true;
      cTower.SetOutdoorAirState(OutdoorAir.WetBulbTemperature, OutdoorAir.HumidityRatio);

      ActiveChillerCount = (int)Math.Ceiling(chilledWaterFlowRate / chiller.MaxChilledWaterFlowRate);
      ActiveChillerCount = Math.Min(ChillerCount, ActiveChillerCount);
      chiller.ChilledWaterOutletSetpointTemperature = ChilledWaterSupplyTemperatureSetpoint;
      while (true)
      {
        //冷水・冷却水流量を計算
        double chwFlow = chilledWaterFlowRate / ActiveChillerCount;
        double pLoad = chwFlow / chiller.MaxChilledWaterFlowRate;
        double cdwFlow;
        //2026.04.17 Bugfix
        if (AutoControlCoolingWaterFlowRate || CoolingWaterFlowSetpoint == 0) 
          cdwFlow = cTower.MaxWaterFlowRate * CoolingTowerCount * Math.Max(pLoad, MinCoolingWaterFlowRatio);
        else cdwFlow = CoolingWaterFlowSetpoint;
        if (!OperateCoolingTowerOneOnOne) cdwFlow /= ChillerCount;
        cTower.WaterFlowRate = cdwFlow / CoolingTowerCount;

        //ポンプによる昇温を評価
        cdwPump.UpdateState(0.001 * cdwFlow);
        double dCDT = cdwPump.GetElectricConsumption() / (0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat * cdwFlow);
        chwPump.UpdateState(0.001 * chwFlow);
        double twi = ChilledWaterReturnTemperature
          + chwPump.GetElectricConsumption() / (0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat * chwFlow);

        //冷却塔と冷凍機の連成計算（冷却水温度の計算）
        bool needIteration = !ControlCoolingWaterTemperature;
        if (ControlCoolingWaterTemperature)
        {
          chiller.Update(CoolingWaterTemperatureSetpoint + dCDT, twi, cdwFlow, chwFlow);
          cTower.OutletWaterSetpointTemperature = CoolingWaterTemperatureSetpoint;
          cTower.Update(chiller.CoolingWaterOutletTemperature, true);
          if (cTower.IsOverLoad) needIteration = true;
        }
        //過負荷または冷却水温度成行の場合には収束計算
        if (needIteration)
        {
          Roots.ErrorFunction eFnc = delegate (double cdt)
          {
            chiller.Update(cdt + dCDT, twi, cdwFlow, chwFlow);
            cTower.Update(chiller.CoolingWaterOutletTemperature, cTower.MaxAirFlowRate);
            return cTower.OutletWaterTemperature - cdt;
          };
          if (eFnc(10) < 0) break;
          else if (0 < eFnc(32)) break;
          else Roots.Bisection(eFnc, 10, 32, 0.01, 0.001, 10);
        }

        if (ActiveChillerCount == ChillerCount || !chiller.IsOverLoad) break;
        else ActiveChillerCount++;
      }

      IsOverLoad_C = chiller.IsOverLoad;
      if (IsOverLoad_C) ChilledWaterSupplyTemperature = chiller.ChilledWaterOutletTemperature;
      else ChilledWaterSupplyTemperature = ChilledWaterSupplyTemperatureSetpoint;
    }

    /// <summary>Fixes (commits) the forecast state as the current state.</summary>
    public void FixState() { }

    #endregion

    #region コンストラクタ

    /// <summary>Initializes a new instance.</summary>
    /// <param name="chiller">Centrifugal chiller.</param>
    /// <param name="chwPump">Chilled water pump.</param>
    /// <param name="cdwPump">Cooling water pump.</param>
    /// <param name="cTower">Cooling tower.</param>
    /// <param name="chillerCount">Total number of chiller units.</param>
    /// <param name="coolingTowerCount">Total number of cooling tower units per chiller.</param>
    public CentrifugalChillerSystem
      (ICentrifugalChiller chiller, CentrifugalPump chwPump,
      CentrifugalPump cdwPump, CoolingTower cTower, int chillerCount, int coolingTowerCount)
    {
      this.chiller = chiller;
      this.chwPump = chwPump;
      this.cdwPump = cdwPump;
      this.cTower = cTower;
      this.ChillerCount = chillerCount;
      this.CoolingTowerCount = coolingTowerCount;
      
      Mode = HeatSourceSystemModel.OperatingMode.ShutOff;
    }

    #endregion

  }
}
