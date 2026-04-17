/* DirectFiredAbsorptionChillerSystem.cs
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
using Popolo.Core.HVAC.HeatExchanger;
using Popolo.Core.HVAC.FluidCircuit;
using Popolo.Core.Physics;
using Popolo.Core.Numerics;

namespace Popolo.Core.HVAC.SystemModel
{
  /// <summary>Heat source sub-system using direct-fired absorption chiller/heaters.</summary>
  public class DirectFiredAbsorptionChillerSystem: IHeatSourceSubSystem
  {

    #region 定数宣言
    

    #endregion

    #region インスタンス変数・プロパティ

    /// <summary>Direct-fired absorption chiller/heater.</summary>
    private DirectFiredAbsorptionChiller chiller;

    /// <summary>Water pump.</summary>
    private CentrifugalPump cdwPump, chwPump, hwPump;

    /// <summary>Cooling tower.</summary>
    private CoolingTower cTower;

    /// <summary>Gets the direct-fired absorption chiller/heater.</summary>
    public IReadOnlyDirectFiredAbsorptionChiller DirectFiredAbsorptionChiller { get { return chiller; } }

    /// <summary>Gets the chilled water pump.</summary>
    public IReadOnlyCentrifugalPump ChilledWaterPump { get { return chwPump; } }

    /// <summary>Gets the hot water pump.</summary>
    public IReadOnlyCentrifugalPump HotWaterPump { get { return hwPump; } }

    /// <summary>Gets the cooling water pump.</summary>
    public IReadOnlyCentrifugalPump CoolingWaterPump { get { return cdwPump; } }

    /// <summary>Gets the cooling tower.</summary>
    public IReadOnlyCoolingTower CoolingTower { get { return cTower; } }

    /// <summary>Gets the number of chiller cells.</summary>
    public int ChillerNumber { get; private set; }

    /// <summary>Gets the number of cooling tower units per chiller unit.</summary>
    public int CoolingTowerNumber { get; private set; }

    /// <summary>Gets the number of operating units.</summary>
    public int OperatingChillerNumber { get; private set; }

    /// <summary>Gets or sets a value indicating whether to operate cooling towers one-to-one with chillers.</summary>
    public bool OperateCoolingTowerOneOnOne { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether to control cooling water temperature.</summary>
    public bool ControlCoolingWaterTemperature { get; set; }

    /// <summary>Gets or sets a value indicating whether to modulate cooling water flow in proportion to load.</summary>
    public bool ControlCoolingWaterFlowRate { get; set; }

    /// <summary>Gets or sets the minimum cooling water flow rate ratio [-].</summary>
    public double MinimumCoolingWaterFlowRatio { get; set; } = 0.5;

    /// <summary>Gets or sets the cooling water temperature setpoint [°C].</summary>
    public double CoolingWaterTemperatureSetpoint { get; set; } = 32;

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
    { get { return chiller.MaxHotWaterFlowRate * ChillerNumber; } }

    /// <summary>Gets the minimum hot water flow rate ratio [-].</summary>
    public double MinHotWaterFlowRatio
    { get { return chiller.MinHotWaterFlowRate / MaxHotWaterFlowRate; } }

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
    { get { return chiller.MaxChilledWaterFlowRate * ChillerNumber; } }

    /// <summary>Gets the minimum chilled water flow rate ratio [-].</summary>
    public double MinChilledWaterFlowRatio
    { get { return chiller.MinChilledWaterFlowRate / MaxChilledWaterFlowRate; } }

    /// <summary>Gets or sets the outdoor air conditions.</summary>
    public IReadOnlyMoistAir OutdoorAir { get; set; } = new MoistAir(35, 0.0185);

    /// <summary>Shuts off this heat source sub-system.</summary>
    public void ShutOff()
    {
      IsOverLoad_C = IsOverLoad_H = false;
      OperatingChillerNumber = 0;
      chiller.ShutOff();
      chwPump.ShutOff();
      hwPump.ShutOff();
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
      chiller.IsCoolingMode = false;

      ChilledWaterSupplyTemperature = ChilledWaterReturnTemperature;
      cTower.ShutOff();
      cdwPump.ShutOff();
      chwPump.ShutOff();

      OperatingChillerNumber = (int)Math.Ceiling(hotWaterFlowRate / chiller.MaxHotWaterFlowRate);
      OperatingChillerNumber = Math.Min(ChillerNumber, OperatingChillerNumber);
      chiller.OutletWaterSetPointTemperature = HotWaterSupplyTemperatureSetpoint;
      while (true)
      {
        //ポンプによる昇温を評価
        double hwFlow = hotWaterFlowRate / OperatingChillerNumber;
        hwPump.UpdateState(0.001 * hwFlow);
        double twi = HotWaterReturnTemperature + hwPump.GetElectricConsumption() / (0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat * hwFlow);

        chiller.Update(32, twi, 0, hwFlow);
        if (OperatingChillerNumber == ChillerNumber || !chiller.IsOverLoad) break;
        else OperatingChillerNumber++;
      }

      IsOverLoad_H = chiller.IsOverLoad;
      if (IsOverLoad_H) HotWaterSupplyTemperature = chiller.OutletWaterTemperature;
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
      chiller.IsCoolingMode = true;

      HotWaterSupplyTemperature = HotWaterReturnTemperature;
      cTower.SetOutdoorAirState(OutdoorAir.WetbulbTemperature, OutdoorAir.HumidityRatio);
      hwPump.ShutOff();

      OperatingChillerNumber = (int)Math.Ceiling(chilledWaterFlowRate / chiller.MaxChilledWaterFlowRate);
      OperatingChillerNumber = Math.Min(ChillerNumber, OperatingChillerNumber);
      chiller.OutletWaterSetPointTemperature = ChilledWaterSupplyTemperatureSetpoint;
      while (true)
      {
        //冷水・冷却水流量を計算
        double chwFlow = chilledWaterFlowRate / OperatingChillerNumber;
        //double pLoad = chwFlow / chiller.MaxChilledWaterFlowRate; //2019.06.29 負荷流量ではなく負荷そのもので負荷率を計算
        double pLoad = 4.186 * chwFlow * (ChilledWaterReturnTemperature - ChilledWaterSupplyTemperatureSetpoint) / chiller.NominalCoolingCapacity;
        double cdwFlow;
        if (ControlCoolingWaterFlowRate) cdwFlow = cTower.WaterFlowRate 
            = cTower.MaxWaterFlowRate * Math.Max(pLoad, MinimumCoolingWaterFlowRatio);  //2017.12.15 BugFix
        else cdwFlow = cTower.WaterFlowRate = cTower.MaxWaterFlowRate; //2017.12.15 BugFix
        if (!OperateCoolingTowerOneOnOne) cdwFlow /= ChillerNumber;

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
          cTower.OutletWaterSetPointTemperature = CoolingWaterTemperatureSetpoint;
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

        if (OperatingChillerNumber == ChillerNumber || !chiller.IsOverLoad) break;
        else OperatingChillerNumber++;
      }

      IsOverLoad_C = chiller.IsOverLoad;
      if (IsOverLoad_C) ChilledWaterSupplyTemperature = chiller.OutletWaterTemperature;
      else ChilledWaterSupplyTemperature = ChilledWaterSupplyTemperatureSetpoint;
    }

    /// <summary>Fixes (commits) the forecast state as the current state.</summary>
    public void FixState() { }

    #endregion

    #region コンストラクタ

    /// <summary>Initializes a new instance.</summary>
    /// <param name="chiller">Direct-fired absorption chiller/heater.</param>
    /// <param name="chwPump">Chilled water pump.</param>
    /// <param name="hwPump">Hot water pump.</param>
    /// <param name="cdwPump">Cooling water pump.</param>
    /// <param name="cTower">Cooling tower.</param>
    /// <param name="chillerNumber">Number of chiller units.</param>
    /// <param name="coolingTowerNumber">Number of cooling tower units per chiller.</param>
    public DirectFiredAbsorptionChillerSystem
      (DirectFiredAbsorptionChiller chiller, CentrifugalPump chwPump, CentrifugalPump hwPump, 
      CentrifugalPump cdwPump, CoolingTower cTower, int chillerNumber, int coolingTowerNumber)
    {
      this.chiller = chiller;
      this.chwPump = chwPump;
      this.hwPump = hwPump;
      this.cdwPump = cdwPump;
      this.cTower = cTower;
      this.ChillerNumber = chillerNumber;
      this.CoolingTowerNumber = coolingTowerNumber;

      //加熱運転・冷却運転対応
      SelectableMode = HeatSourceSystemModel.OperatingMode.Cooling | HeatSourceSystemModel.OperatingMode.Heating;
      Mode = HeatSourceSystemModel.OperatingMode.ShutOff;      
    }

    #endregion

  }
}
