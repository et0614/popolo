/* HotWaterBoiler.cs
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

using Popolo.Core.Physics;

namespace Popolo.Core.HVAC.HeatSource
{
  /// <summary>Hot-water boiler.</summary>
  public class HotWaterBoiler: IReadOnlyHotWaterBoiler
  {

    #region プロパティ・インスタンス変数

    /// <summary>Excess air ratio [-].</summary>
    private double airRatio = 1.1;

    /// <summary>Heat loss coefficient [kW/K].</summary>
    private double heatLossCoefficient = 0.0;

    /// <summary>Nominal outlet water temperature [°C].</summary>
    private double nomOutletWaterTemperature;

    /// <summary>Nominal flue gas temperature [°C].</summary>
    private double nominalSmokeTemperature;

    /// <summary>Gets or sets the primary energy conversion factor for electricity [MJ/kWh].</summary>
    public double PrimaryEnergyFactor { get; set; } = 9.76;

    /// <summary>Gets the fuel type.</summary>
    public Boiler.Fuel Fuel { get; private set;}

    /// <summary>Gets the outlet water temperature [°C].</summary>
    public double OutletWaterTemperature { get; private set; }

    /// <summary>Gets or sets the outlet water temperature setpoint [°C].</summary>
    public double OutletWaterSetPointTemperature { get; set; }

    /// <summary>Gets the inlet water temperature [°C].</summary>
    public double InletWaterTemperature { get; private set; }

    /// <summary>Gets the current water flow rate [kg/s].</summary>
    public double WaterFlowRate { get; private set; }

    /// <summary>Gets the maximum allowable water flow rate [kg/s].</summary>
    public double MaxWaterFlowRate { get; private set; }

    /// <summary>Gets or sets the minimum water flow rate ratio [-].</summary>
    public double MinWaterFlowRatio { get; set; } = 0.2;

    /// <summary>Gets the nominal heating capacity [kW].</summary>
    public double NominalCapacity { get; private set; }

    /// <summary>Gets the nominal fuel consumption rate [kg/s or Nm³/s].</summary>
    public double NominalFuelConsumption { get; private set; }
    
    /// <summary>Gets the electric power consumption [kW].</summary>
    public double ElectricConsumption { get; private set; }

    /// <summary>Gets the current fuel consumption rate [kg/s or Nm³/s].</summary>
    public double FuelConsumption { get; private set; }

    /// <summary>Gets the current heat output [kW].</summary>
    public double HeatLoad { get; private set; }
    
    /// <summary>Gets or sets the ambient temperature [°C].</summary>
    public double AmbientTemperature { get; set; }

    /// <summary>Gets or sets the excess air ratio [-] (clamped to 1.0–1.5).</summary>
    public double AirRatio
    {
      get { return airRatio; }
      set { airRatio = Math.Max(1.0, Math.Min(1.5, value)); }
    }

    /// <summary>Gets the coefficient of performance (primary energy basis) [-].</summary>
    public double COP
    {
      get
      {
        if (HeatLoad == 0) return 0;
        else
          return 0.001 * HeatLoad / (ElectricConsumption * PrimaryEnergyFactor 
            + FuelConsumption * Boiler.GetHeatingValue(Fuel, true));
      }
    }

    /// <summary>Gets a value indicating whether the boiler is overloaded.</summary>
    public bool IsOverLoad { get; private set; }

    #endregion

    #region コンストラクタ

    /// <summary>Initializes a new instance from rated operating conditions.</summary>
    /// <param name="inletWaterTemperature">Inlet water temperature [°C].</param>
    /// <param name="outletWaterTemperature">Outlet water temperature [°C].</param>
    /// <param name="waterFlowRate">Water flow rate [kg/s].</param>
    /// <param name="fuelConsumption">Fuel consumption rate [kg/s or Nm³/s].</param>
    /// <param name="electricConsumption">Electric power consumption [kW].</param>
    /// <param name="ambientTemperature">Ambient temperature [°C].</param>
    /// <param name="airRatio">Excess air ratio [-].</param>
    /// <param name="fuel">Fuel type.</param>
    /// <param name="smokeTemperature">Flue gas temperature [°C].</param>
    public HotWaterBoiler
      (double inletWaterTemperature, double outletWaterTemperature, double waterFlowRate,
      double fuelConsumption, double electricConsumption, double ambientTemperature,
      double airRatio, Boiler.Fuel fuel, double smokeTemperature)
    {
      //プロパティ保存
      Fuel = fuel;
      AirRatio = airRatio;
      nominalSmokeTemperature = smokeTemperature;
      NominalFuelConsumption = fuelConsumption;
      ElectricConsumption = electricConsumption;
      AmbientTemperature = ambientTemperature;
      OutletWaterSetPointTemperature = nomOutletWaterTemperature = outletWaterTemperature;
      MaxWaterFlowRate  = WaterFlowRate = waterFlowRate;
      NominalCapacity = (outletWaterTemperature - inletWaterTemperature) * WaterFlowRate * 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat;

      //熱損失係数の計算
      heatLossCoefficient = Boiler.GetHeatLossCoefficient
        (NominalCapacity, outletWaterTemperature, AmbientTemperature, fuel,
        nominalSmokeTemperature, AirRatio, NominalFuelConsumption, AmbientTemperature);

      //停止させる
      ShutOff();
    }

    #endregion

    #region publicメソッド

    /// <summary>Updates the boiler state for the given inlet conditions and flow rate.</summary>
    /// <param name="inletWaterTemperature">Inlet water temperature [°C].</param>
    /// <param name="waterFlowRate">Water flow rate [kg/s].</param>
    public void Update(double inletWaterTemperature, double waterFlowRate)
    {
      //入力条件保存
      InletWaterTemperature = inletWaterTemperature;
      WaterFlowRate = waterFlowRate;

      //流量0、設定温度<入口温度で停止
      if (WaterFlowRate <= 0 || OutletWaterSetPointTemperature < InletWaterTemperature)
      {
        ShutOff();
        return;
      }

      //燃料消費量を計算
      double hl = 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat * waterFlowRate * (OutletWaterSetPointTemperature - inletWaterTemperature);
      FuelConsumption = Boiler.GetFuelConsumption(hl, OutletWaterSetPointTemperature, AmbientTemperature, Fuel,
        nominalSmokeTemperature, airRatio, heatLossCoefficient, AmbientTemperature, nomOutletWaterTemperature);

      //過負荷の場合には成り行き計算
      IsOverLoad = NominalFuelConsumption < FuelConsumption;
      if (IsOverLoad)
      {
        FuelConsumption = NominalFuelConsumption;
        double to;
        Boiler.GetOutletWaterTemperature(InletWaterTemperature, WaterFlowRate, 
          NominalFuelConsumption, AmbientTemperature, Fuel, nominalSmokeTemperature, 
          AirRatio, heatLossCoefficient, AmbientTemperature, nomOutletWaterTemperature, out to, out hl);
        OutletWaterTemperature = to;
      }
      else OutletWaterTemperature = OutletWaterSetPointTemperature;
      HeatLoad = hl;
    }

    /// <summary>Shuts off the boiler.</summary>
    public void ShutOff()
    {
      OutletWaterTemperature = InletWaterTemperature;
      ElectricConsumption = WaterFlowRate = HeatLoad = FuelConsumption = 0;     
    }

    #endregion

  }
}
