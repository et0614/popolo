/* DirectFiredDoubleEffectAbsorptionChiller.cs
 * 
 * Copyright (C) 2015 E.Togashi
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

using Popolo.Core.Numerics;
using Popolo.Core.Physics;

namespace Popolo.Core.HVAC.HeatSource
{
  /// <summary>Direct-fired double-effect LiBr absorption chiller/heater.</summary>
  public class DirectFiredAbsorptionChiller: IReadOnlyDirectFiredAbsorptionChiller
  {

    #region 定数宣言


    /// <summary>Minimum variable flow ratio for chilled/hot water [-].</summary>
    private const double MIN_WATERFLOW_RATIO = 0.5;

    /// <summary>Minimum variable flow ratio for solution [-].</summary>
    private const double MIN_SOLUTIONFLOW_RATIO = 0.5;

    /// <summary>Minimum partial load ratio [-].</summary>
    private const double MIN_PARTIALLOAD = 0.2;

    /// <summary>Ambient temperature and combustion air temperature [°C].</summary>
    private const double AMB_TEMP = 25;

    /// <summary>Excess air ratio for combustion [-].</summary>
    private const double AIR_RATIO = 1.15;

    /// <summary>Flue gas temperature [°C].</summary>
    private const double SMK_TEMP = 100;

    #endregion

    #region インスタンス変数

    /// <summary>Evaporator overall heat transfer conductance [kW/K].</summary>
    private double evaporatorKA;

    /// <summary>Condenser overall heat transfer conductance [kW/K].</summary>
    private double condenserKA;

    /// <summary>Low-temperature desorber overall heat transfer conductance [kW/K].</summary>
    private double lowDesorborKA;

    /// <summary>Low-temperature solution heat exchanger conductance [kW/K].</summary>
    private double thinSolutionHexKA;

    /// <summary>Nominal solution circulation flow rate [kg/s].</summary>
    private double solutionFlowRate;

    /// <summary>Nominal high-temperature desorber heat input [kW].</summary>
    private double desorbHeat;

    /// <summary>Shell heat loss coefficient [kW/K].</summary>
    private double heatLossCoefficient;

    /// <summary>Hot-water boiler component (for heating mode).</summary>
    private HotWaterBoiler hBoiler;

    #endregion

    #region プロパティ・インスタンス変数

    /// <summary>Gets or sets a value indicating whether the unit is in cooling mode.</summary>
    public bool IsCoolingMode { get; set; }

    /// <summary>Gets the fuel type.</summary>
    public Boiler.Fuel Fuel { get; private set; }

    /// <summary>Gets the outlet water temperature [°C].</summary>
    public double OutletWaterTemperature { get; private set; }

    /// <summary>Gets or sets the outlet water temperature setpoint [°C].</summary>
    public double OutletWaterSetpointTemperature { get; set; }

    /// <summary>Gets the inlet water temperature [°C].</summary>
    public double InletWaterTemperature { get; private set; }

    /// <summary>Gets the cooling water outlet temperature [°C].</summary>
    public double CoolingWaterOutletTemperature { get; private set; }

    /// <summary>Gets the cooling water inlet temperature [°C].</summary>
    public double CoolingWaterInletTemperature { get; private set; }

    /// <summary>Gets the chilled/hot water flow rate [kg/s].</summary>
    public double WaterFlowRate { get; private set; }

    /// <summary>Gets the cooling water flow rate [kg/s].</summary>
    public double CoolingWaterFlowRate { get; private set; }

    /// <summary>Gets the maximum chilled water flow rate [kg/s].</summary>
    public double MaxChilledWaterFlowRate { get; private set; }

    /// <summary>Gets the maximum hot water flow rate [kg/s].</summary>
    public double MaxHotWaterFlowRate { get; private set; }

    /// <summary>Gets the minimum chilled water flow rate [kg/s].</summary>
    public double MinChilledWaterFlowRate { get; private set; }

    /// <summary>Gets the minimum hot water flow rate [kg/s].</summary>
    public double MinHotWaterFlowRate { get; private set; }

    /// <summary>Gets the maximum cooling water flow rate [kg/s].</summary>
    public double MaxCoolingWaterFlowRate { get; private set; }

    /// <summary>Gets the minimum cooling water flow rate [kg/s].</summary>
    public double MinCoolingWaterFlowRate { get; private set; }

    /// <summary>Gets the nominal cooling capacity [kW].</summary>
    public double NominalCoolingCapacity { get; private set; }

    /// <summary>Gets the nominal heating capacity [kW].</summary>
    public double NominalHeatingCapacity { get; private set; }

    /// <summary>Gets the nominal cooling fuel consumption rate [Nm³/s or kg/s].</summary>
    public double NominalCoolingFuelConsumption { get; private set; }

    /// <summary>Gets the nominal heating fuel consumption rate [Nm³/s or kg/s].</summary>
    public double NominalHeatingFuelConsumption { get; private set; }

    /// <summary>Gets the minimum partial load ratio for capacity control [-].</summary>
    public double MinimumPartialLoadRatio { get; private set; }

    /// <summary>Gets the nominal electric power consumption [kW].</summary>
    public double NominalElectricConsumption { get; private set; }

    /// <summary>Gets the electric power consumption [kW].</summary>
    public double ElectricConsumption { get; private set; }

    /// <summary>Gets the fuel consumption rate [Nm³/s or kg/s].</summary>
    public double FuelConsumption { get; private set; }

    /// <summary>Gets the heating load [kW].</summary>
    public double HeatingLoad { get; private set; }

    /// <summary>Gets the cooling load [kW].</summary>
    public double CoolingLoad { get; private set; }

    /// <summary>Gets the coefficient of performance [-].</summary>
    public double COP
    {
      get
      {
        if (FuelConsumption == 0) return 0;
        else if(IsCoolingMode)
          return 0.001 * CoolingLoad / (FuelConsumption * Boiler.GetHeatingValue(Fuel, true));
        else return 0.001 * HeatingLoad / (FuelConsumption * Boiler.GetHeatingValue(Fuel, true));
      }
    }

    /// <summary>Gets or sets a value indicating whether the solution pump has inverter control.</summary>
    public bool HasSolutionInverterPump { get; set; }

    /// <summary>Gets the concentrated (thick) solution mass fraction [-].</summary>
    public double ThickSolutionMassFraction { get; private set; }

    /// <summary>Gets the dilute (thin) solution mass fraction [-].</summary>
    public double ThinSolutionMassFraction { get; private set; }

    /// <summary>Gets the evaporating temperature [°C].</summary>
    public double EvaporatingTemperature { get; private set; }

    /// <summary>Gets the condensing temperature [°C].</summary>
    public double CondensingTemperature { get; private set; }

    /// <summary>Gets the desorption temperature [°C].</summary>
    public double DesorbTemperature { get; private set; }

    /// <summary>Gets a value indicating whether the unit is overloaded.</summary>
    public bool IsOverLoad { get; private set; }

    #endregion

    #region コンストラクタ

    /// <summary>Initializes a new instance from rated conditions.</summary>
    /// <param name="nominalCoolingFuelConsumption">Nominal cooling fuel consumption rate [Nm³/s or kg/s].</param>
    /// <param name="nominalHetingFuelConsumption">Nominal heating fuel consumption rate [Nm³/s or kg/s].</param>
    /// <param name="chilledWaterInletTemperature">Chilled water inlet temperature [°C].</param>
    /// <param name="chilledWaterOutletTemperature">Chilled water outlet temperature [°C].</param>
    /// <param name="coolingWaterInletTemperature">Cooling water inlet temperature [°C].</param>
    /// <param name="coolingWaterOutletTemperature">Cooling water outlet temperature [°C].</param>
    /// <param name="hotWaterInletTemperature">Hot water inlet temperature [°C].</param>
    /// <param name="hotWaterOutletTemperature">Hot water outlet temperature [°C].</param>
    /// <param name="chilledWaterFlowRate">Chilled water mass flow rate [kg/s].</param>
    /// <param name="coolingWaterFlowRate">Cooling water mass flow rate [kg/s].</param>
    /// <param name="hotWaterFlowRate">Hot water mass flow rate [kg/s].</param>
    /// <param name="electricConsumption">Electric power consumption [kW].</param>
    /// <param name="fuel">Fuel type.</param>
    public DirectFiredAbsorptionChiller
      (double nominalCoolingFuelConsumption, double nominalHetingFuelConsumption,
      double chilledWaterInletTemperature, double chilledWaterOutletTemperature,
      double coolingWaterInletTemperature, double coolingWaterOutletTemperature,
      double hotWaterInletTemperature, double hotWaterOutletTemperature, double chilledWaterFlowRate,
      double coolingWaterFlowRate, double hotWaterFlowRate, double electricConsumption, Boiler.Fuel fuel)
    {
      this.IsCoolingMode = true;
      this.OutletWaterSetpointTemperature = chilledWaterOutletTemperature;
      this.CoolingWaterInletTemperature = coolingWaterInletTemperature;
      this.InletWaterTemperature = chilledWaterInletTemperature;
      this.CoolingWaterOutletTemperature = coolingWaterOutletTemperature;
      this.CoolingWaterFlowRate = coolingWaterFlowRate;
      this.WaterFlowRate = chilledWaterFlowRate;
      this.MinCoolingWaterFlowRate = coolingWaterFlowRate * MIN_WATERFLOW_RATIO;
      this.MaxCoolingWaterFlowRate = coolingWaterFlowRate;
      this.MaxChilledWaterFlowRate = chilledWaterFlowRate;
      this.MaxHotWaterFlowRate = hotWaterFlowRate;
      this.MinChilledWaterFlowRate = chilledWaterFlowRate * MIN_WATERFLOW_RATIO;
      this.MinHotWaterFlowRate = hotWaterFlowRate * MIN_WATERFLOW_RATIO;
      this.NominalCoolingCapacity = chilledWaterFlowRate * 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat
        * (chilledWaterInletTemperature - chilledWaterOutletTemperature);
      this.NominalHeatingCapacity = hotWaterFlowRate * 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat
        * (hotWaterOutletTemperature - hotWaterInletTemperature);
      this.NominalCoolingFuelConsumption = nominalCoolingFuelConsumption;
      this.NominalHeatingFuelConsumption = nominalHetingFuelConsumption;
      this.Fuel = fuel;
      this.NominalElectricConsumption = electricConsumption;

      //吸収冷凍サイクルの各種性能を計算
      AbsorptionRefrigerationCycle.GetHeatTransferCoefficients(chilledWaterInletTemperature,
        chilledWaterOutletTemperature, chilledWaterFlowRate, coolingWaterInletTemperature,
        coolingWaterOutletTemperature, coolingWaterFlowRate, out evaporatorKA, out condenserKA,
        out lowDesorborKA, out thinSolutionHexKA, out solutionFlowRate, out desorbHeat);
      desorbHeat *= 1.001;  //定格性能を担保するための処理

      //缶体熱損失係数の計算
      heatLossCoefficient = Boiler.GetHeatLossCoefficient
        (desorbHeat, AbsorptionRefrigerationCycle.NOM_DSB_LIQ_TEMP, AMB_TEMP, Fuel,
        SMK_TEMP, AIR_RATIO, NominalCoolingFuelConsumption, AMB_TEMP);

      //温水ボイラ初期化
      hBoiler = new HotWaterBoiler(hotWaterInletTemperature, hotWaterOutletTemperature, hotWaterFlowRate,
        NominalHeatingFuelConsumption, 0, AMB_TEMP, AIR_RATIO, Fuel, SMK_TEMP);

      //運転停止
      ShutOff();
    }

    #endregion

    #region publicメソッド

    /// <summary>Updates the unit state for the given inlet conditions.</summary>
    /// <param name="coolingWaterInletTemperature">Cooling water inlet temperature [°C].</param>
    /// <param name="inletWaterTemperature">Chilled/hot water inlet temperature [°C].</param>
    /// <param name="coolingWaterFlowRate">Cooling water mass flow rate [kg/s].</param>
    /// <param name="waterFlowRate">Chilled/hot water mass flow rate [kg/s].</param>
    public void Update(double coolingWaterInletTemperature, double inletWaterTemperature,
      double coolingWaterFlowRate, double waterFlowRate)
    {
      //状態値を保存
      this.InletWaterTemperature = inletWaterTemperature;
      this.CoolingWaterInletTemperature = coolingWaterInletTemperature;
      this.CoolingWaterFlowRate = Math.Max(coolingWaterFlowRate, MinCoolingWaterFlowRate);

      //冷却運転
      if (IsCoolingMode && (OutletWaterSetpointTemperature < InletWaterTemperature))
      {
        //極少負荷対応のための入口水温・水量補正
        this.WaterFlowRate = Math.Max(waterFlowRate, MinChilledWaterFlowRate);
        double pl = (InletWaterTemperature - OutletWaterSetpointTemperature)
          * 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat * WaterFlowRate / NominalCoolingCapacity;
        double ti = (InletWaterTemperature - OutletWaterSetpointTemperature)
          / Math.Min(1, pl / MIN_PARTIALLOAD) + OutletWaterSetpointTemperature;

        double dsbH = 0;
        double tcdo, tdsb, tevp, tcnd, wtn, wtk;
        //定格の高温再生器投入熱量で出口状態を計算
        double cht = AbsorptionRefrigerationCycle.GetChilledWaterOutletTemperature
          (ti, WaterFlowRate, CoolingWaterInletTemperature, CoolingWaterFlowRate, evaporatorKA,
          condenserKA, lowDesorborKA, thinSolutionHexKA, solutionFlowRate, desorbHeat,
          out tcdo, out tdsb, out tevp, out tcnd, out wtn, out wtk);

        IsOverLoad = OutletWaterSetpointTemperature <= cht;
        //過負荷の場合
        if (IsOverLoad)
        {
          OutletWaterTemperature = cht;
          dsbH = desorbHeat;
        }
        //処理可能の場合
        else
        {
          if (HasSolutionInverterPump)
          {
            Minimization.MinimizeFunction mFnc = delegate (double sFlow)
            {
              dsbH = AbsorptionRefrigerationCycle.GetDesorbHeat
              (ti, WaterFlowRate, CoolingWaterInletTemperature, CoolingWaterFlowRate, evaporatorKA, condenserKA,
              lowDesorborKA, thinSolutionHexKA, sFlow, OutletWaterSetpointTemperature,
              out tcdo, out tdsb, out tevp, out tcnd, out wtn, out wtk);
              return dsbH;
            };
            double sf = solutionFlowRate * MIN_SOLUTIONFLOW_RATIO;
            Minimization.GoldenSection(ref sf, solutionFlowRate, mFnc);
          }
          else
          {
            dsbH = AbsorptionRefrigerationCycle.GetDesorbHeat
              (ti, WaterFlowRate, CoolingWaterInletTemperature, CoolingWaterFlowRate, evaporatorKA, condenserKA,
              lowDesorborKA, thinSolutionHexKA, solutionFlowRate, OutletWaterSetpointTemperature,
              out tcdo, out tdsb, out tevp, out tcnd, out wtn, out wtk);
          }
          OutletWaterTemperature = OutletWaterSetpointTemperature;
        }
        CoolingWaterOutletTemperature = tcdo;
        CoolingLoad = (InletWaterTemperature - OutletWaterTemperature) * 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat * waterFlowRate;
        HeatingLoad = 0;
        FuelConsumption = Boiler.GetFuelConsumption
          (dsbH, tdsb, AMB_TEMP, Fuel, SMK_TEMP, AIR_RATIO, heatLossCoefficient,
          AMB_TEMP, AbsorptionRefrigerationCycle.NOM_DSB_LIQ_TEMP);
        ElectricConsumption = CoolingLoad / NominalCoolingCapacity * NominalElectricConsumption;
        DesorbTemperature = tdsb;
        EvaporatingTemperature = tevp;
        CondensingTemperature = tcnd;
        ThickSolutionMassFraction = wtk;
        ThinSolutionMassFraction = wtn;
      }
      //加熱運転
      else if (!IsCoolingMode && (InletWaterTemperature < OutletWaterSetpointTemperature))
      {
        //水量設定
        CoolingWaterFlowRate = 0;
        this.WaterFlowRate = Math.Max(waterFlowRate, MinHotWaterFlowRate);

        //温水ボイラを更新
        hBoiler.OutletWaterSetpointTemperature = this.OutletWaterSetpointTemperature;
        hBoiler.Update(InletWaterTemperature, WaterFlowRate);
        IsOverLoad = hBoiler.IsOverLoad;
        HeatingLoad = hBoiler.HeatLoad;
        CoolingLoad = 0;
        OutletWaterTemperature = hBoiler.OutletWaterTemperature;
        FuelConsumption = hBoiler.FuelConsumption;
        ElectricConsumption = HeatingLoad / NominalHeatingCapacity * NominalElectricConsumption;
      }
      //運転停止
      else ShutOff();
    }

    /// <summary>Shuts off the unit.</summary>
    public void ShutOff()
    {
      OutletWaterTemperature = InletWaterTemperature;
      CoolingWaterOutletTemperature = CoolingWaterInletTemperature;
      FuelConsumption = CoolingLoad = HeatingLoad = CoolingWaterFlowRate = WaterFlowRate = 0;
      ElectricConsumption = 0;
    }

    #endregion

  }
}
