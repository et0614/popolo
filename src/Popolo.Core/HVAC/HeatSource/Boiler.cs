/* Boiler.cs
 * 
 * Copyright (C) 2014 E.Togashi
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

using Popolo.Core.Physics;


namespace Popolo.Core.HVAC.HeatSource
{
  /// <summary>Provides static methods for hot-water and steam boiler calculations.</summary>
  public static class Boiler
  {

    #region 定数宣言


    /// <summary>Specific heat of air at constant pressure [kJ/(kg·K)].</summary>
    private const double AIR_SPECIFIC_HEAT = 1.006;


    /// <summary>Specific heat of flue gas [kJ/(kg·K)].</summary>
    private const double SMOKE_SPECIFIC_HEAT = 1.38;

    /// <summary>Flue gas temperature approximation coefficient A.</summary>
    private const double A_ST = 1.17;

    /// <summary>Flue gas temperature approximation coefficient B.</summary>
    private const double B_ST = -0.17;

    #endregion

    #region 列挙型定義

    /// <summary>Fuel type.</summary>
    public enum Fuel
    {
      /// <summary>City gas 13A.</summary>
      Gas13A,
      /// <summary>LNG</summary>
      LNG,
      /// <summary>LPG</summary>
      LPG,
      /// <summary>Coal.</summary>
      Coal
    }

    #endregion

    #region 成り行き状態の計算

    /// <summary>Computes the boiler outlet water temperature and heat output.</summary>
    /// <param name="inletWaterTemperature">Inlet water temperature [°C].</param>
    /// <param name="waterFlowRate">Water flow rate [kg/s].</param>
    /// <param name="fuelConsumption">Fuel consumption rate [Nm³/s or kg/s].</param>
    /// <param name="inletAirTemperature">Inlet air temperature [°C].</param>
    /// <param name="fuel">Fuel type.</param>
    /// <param name="nominalSmokeTemperature">Nominal flue gas temperature [°C].</param>
    /// <param name="airRatio">Excess air ratio [-].</param>
    /// <param name="heatLossCoefficient">Heat loss coefficient [kW/K].</param>
    /// <param name="ambientTemperature">Ambient temperature [°C].</param>
    /// <param name="nominalOutletWaterTemperature">Nominal outlet water temperature [°C].</param>
    /// <param name="outletWaterTemperature">Outlet water temperature [°C].</param>
    /// <param name="heatLoad">Heat output [kW].</param>
    public static void GetOutletWaterTemperature
      (double inletWaterTemperature, double waterFlowRate, double fuelConsumption, double inletAirTemperature, Fuel fuel,
      double nominalSmokeTemperature, double airRatio, double heatLossCoefficient, double ambientTemperature, 
      double nominalOutletWaterTemperature, out double outletWaterTemperature, out double heatLoad)
    {
      double rf = GetSmokeCoefficient(airRatio, fuel);
      double bf1 = PhysicsConstants.NominalMoistAirDensity * GetTheoreticalAir(fuel) * airRatio * AIR_SPECIFIC_HEAT * inletAirTemperature;
      bf1 += 1000d * GetHeatingValue(fuel, true);
      bf1 -= SMOKE_SPECIFIC_HEAT * nominalSmokeTemperature * rf * B_ST;
      bf1 *= fuelConsumption;
      bf1 += heatLossCoefficient * ambientTemperature + waterFlowRate * 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat * inletWaterTemperature;
      double bf2 = (waterFlowRate * 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat + heatLossCoefficient);
      bf2 += A_ST * SMOKE_SPECIFIC_HEAT * rf * fuelConsumption * nominalSmokeTemperature / nominalOutletWaterTemperature;
      outletWaterTemperature = bf1 / bf2;
      heatLoad = 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat * waterFlowRate * (outletWaterTemperature - inletWaterTemperature);
    }

    /// <summary>Computes the steam flow rate [kg/s] and heat output from fuel consumption.</summary>
    /// <param name="inletWaterTemperature">Inlet water temperature [°C].</param>
    /// <param name="steamPressure">Steam pressure [kPa].</param>
    /// <param name="fuelConsumption">Fuel consumption rate [Nm³/s or kg/s].</param>
    /// <param name="inletAirTemperature">Inlet air temperature [°C].</param>
    /// <param name="fuel">Fuel type.</param>
    /// <param name="nominalSmokeTemperature">Nominal flue gas temperature [°C].</param>
    /// <param name="airRatio">Excess air ratio [-].</param>
    /// <param name="heatLossCoefficient">Heat loss coefficient [kW/K].</param>
    /// <param name="ambientTemperature">Ambient temperature [°C].</param>
    /// <param name="nominalSteamTemperature">Nominal steam temperature [°C].</param>
    /// <param name="steamFlowRate">Output: steam flow rate [kg/s].</param>
    /// <param name="heatLoad">Output: heat output [kW].</param>
    public static void GetSteamFlowRate
      (double inletWaterTemperature, double steamPressure, double fuelConsumption, double inletAirTemperature, 
      Fuel fuel, double nominalSmokeTemperature, double airRatio, double heatLossCoefficient, 
      double ambientTemperature, double nominalSteamTemperature, out double steamFlowRate, out double heatLoad)
    {
      double rf = GetSmokeCoefficient(airRatio, fuel);
      double twsv = Water.GetSaturationTemperature(steamPressure);
      double hwsv = Water.GetSaturatedVaporEnthalpy(twsv);
      double hwi = inletWaterTemperature * 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat;
      double qa = PhysicsConstants.NominalMoistAirDensity * GetTheoreticalAir(fuel) * airRatio * AIR_SPECIFIC_HEAT * inletAirTemperature;
      double qf = 1000d * GetHeatingValue(fuel, true);
      double qs = SMOKE_SPECIFIC_HEAT * rf * nominalSmokeTemperature * (A_ST * twsv / nominalSteamTemperature + B_ST);
      double ql = heatLossCoefficient * (twsv - ambientTemperature);

      heatLoad = (qa + qf - qs) * fuelConsumption - ql;
      steamFlowRate = heatLoad / (hwsv - hwi);
    }

    #endregion

    #region 燃料消費量の計算

    /// <summary>Computes the fuel consumption rate [kg/s or Nm³/s] required for the given heat load.</summary>
    /// <param name="heatLoad">Heat load [kW].</param>
    /// <param name="outletWaterTemperature">Outlet water or steam temperature [°C].</param>
    /// <param name="inletAirTemperature">Inlet air temperature [°C].</param>
    /// <param name="fuel">Fuel type.</param>
    /// <param name="nominalSmokeTemperature">Nominal flue gas temperature [°C].</param>
    /// <param name="airRatio">Excess air ratio [-].</param>
    /// <param name="heatLossCoefficient">Heat loss coefficient [kW/K].</param>
    /// <param name="ambientTemperature">Ambient temperature [°C].</param>
    /// <param name="nominalOutletWaterTemperature">Nominal outlet water temperature [°C].</param>
    /// <returns>Fuel consumption rate [kg/s or Nm³/s].</returns>
    public static double GetFuelConsumption
      (double heatLoad, double outletWaterTemperature, double inletAirTemperature, Fuel fuel,
      double nominalSmokeTemperature, double airRatio, double heatLossCoefficient, 
      double ambientTemperature, double nominalOutletWaterTemperature)
    {
      double smokeTemp = nominalSmokeTemperature 
        * (A_ST * outletWaterTemperature / nominalOutletWaterTemperature + B_ST);
      double m1 = heatLoad + heatLossCoefficient * (outletWaterTemperature - ambientTemperature);
      double m2 = PhysicsConstants.NominalMoistAirDensity * GetTheoreticalAir(fuel) * airRatio * AIR_SPECIFIC_HEAT * inletAirTemperature 
        + 1000d * GetHeatingValue(fuel, true);
      m2 -= SMOKE_SPECIFIC_HEAT * smokeTemp * GetSmokeCoefficient(airRatio, fuel);

      return m1 / m2;
    }
    
    #endregion

    #region 熱損失係数の計算

    /// <summary>Computes the heat loss coefficient [kW/K] from rated operating conditions.</summary>
    /// <param name="heatLoad">Heat output [kW].</param>
    /// <param name="outletWaterTemperature">Outlet water or steam temperature [°C].</param>
    /// <param name="inletAirTemperature">Inlet air temperature [°C].</param>
    /// <param name="fuel">Fuel type.</param>
    /// <param name="smokeTemperature">Flue gas temperature [°C].</param>
    /// <param name="airRatio">Excess air ratio [-].</param>
    /// <param name="fuelConsumption">Fuel consumption rate [kg/s or Nm³/s].</param>
    /// <param name="ambientTemperature">Ambient temperature [°C].</param>
    /// <returns>Heat loss coefficient [kW/K].</returns>
    public static double GetHeatLossCoefficient
      (double heatLoad, double outletWaterTemperature, double inletAirTemperature, Fuel fuel,
      double smokeTemperature, double airRatio, double fuelConsumption, double ambientTemperature)
    {
      double airHeat = PhysicsConstants.NominalMoistAirDensity * fuelConsumption * GetTheoreticalAir(fuel) * airRatio
        * AIR_SPECIFIC_HEAT * inletAirTemperature;
      double fuelHeat = fuelConsumption * GetHeatingValue(fuel, true) * 1000;
      double smokeHeat = SMOKE_SPECIFIC_HEAT * fuelConsumption
        * GetSmokeCoefficient(airRatio, fuel) * smokeTemperature;
      double heatLoss = airHeat + fuelHeat - heatLoad - smokeHeat;

      return heatLoss / (outletWaterTemperature - ambientTemperature);
    }

    #endregion

    #region その他の計算

    /// <summary>Computes the flue gas mass flow coefficient used in enthalpy calculations.</summary>
    /// <param name="airRatio">Excess air ratio [-].</param>
    /// <param name="fuel">Fuel type.</param>
    /// <returns>Flue gas mass flow coefficient.</returns>
    private static double GetSmokeCoefficient(double airRatio, Fuel fuel)
    {
      double am1 = airRatio - 1d;
      switch (fuel)
      {
        case Fuel.Gas13A: return (0.293 + 0.268 * am1) * 41d;
        case Fuel.LNG: return (0.376 + 0.296 * am1) * 49.1d - 3.91 - 1.36 * am1;
        case Fuel.LPG: return (0.376 + 0.296 * am1) * 45.7d - 3.91 - 1.36 * am1;
        default: return (0.0216 + 0.0241 * am1) * 27.6d + 1.67 + 0.56 * am1;  //石炭
      }
    }

    /// <summary>Returns the heating value [MJ/Nm³ or MJ/kg] for the specified fuel.</summary>
    /// <param name="fuel">Fuel type.</param>
    /// <param name="isHighValue">True for the higher (gross) heating value; false for the lower (net) value.</param>
    /// <returns>Heating value [MJ/Nm³ or MJ/kg].</returns>
    public static double GetHeatingValue(Fuel fuel, bool isHighValue)
    {
      switch (fuel)
      {
        case Fuel.Gas13A:
          if (isHighValue) return 45.6;
          else return 41.0;
        case Fuel.LNG:
          if (isHighValue) return 54.6;
          else return 49.1;
        case Fuel.LPG:
          if (isHighValue) return 50.8;
          else return 45.7;
        default:  //石炭
          if (isHighValue) return 29.0;
          else return 27.6;
      }
    }

    /// <summary>Returns the theoretical air requirement [Nm³/Nm³ or Nm³/kg] for the specified fuel.</summary>
    /// <param name="fuel">Fuel type.</param>
    /// <returns>Theoretical air requirement [Nm³/Nm³ or Nm³/kg].</returns>
    public static double GetTheoreticalAir(Fuel fuel)
    {
      switch (fuel)
      {
        case Fuel.Gas13A: return 10.949;
        case Fuel.LNG: return 13.093;
        case Fuel.LPG: return 12.045;
        default: return 7.8;  //石炭
      }
    }

    #endregion

  }
}
