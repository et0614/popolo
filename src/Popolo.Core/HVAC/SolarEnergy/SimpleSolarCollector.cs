/* SimpleSolarCollector.cs
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

using System;

using Popolo.Core.Physics;
using Popolo.Core.Climate;

namespace Popolo.Core.HVAC.SolarEnergy
{

  /// <summary>Simplified solar collector model based on characteristic equations.</summary>
  public class SimpleSolarCollector
  {

    #region 列挙型定義

    /// <summary>Solar collector receiver type.</summary>
    public enum HeatReceiver
    {
      /// <summary>Evacuated tube collector.</summary>
      VacuumTube,
      /// <summary>Flat-plate collector.</summary>
      FlatPlate
    }

    #endregion

    #region インスタンス変数・プロパティ

    /// <summary>Inclined surface data.</summary>
    private Incline incline = null!;

    /// <summary>Gets the inclined surface.</summary>
    public IReadOnlyIncline Incline { get { return incline; } }

    /// <summary>Gets the characteristic coefficient A [W/K].</summary>
    public double CoefficientA { get; private set; }

    /// <summary>Gets the characteristic coefficient B [-].</summary>
    public double CoefficientB { get; private set; }

    /// <summary>Gets the collection efficiency [-].</summary>
    public double Efficiency { get; private set; }

    /// <summary>Gets the panel surface area [m²].</summary>
    public double SurfaceArea { get; private set; }

    /// <summary>Gets the inlet water temperature [°C].</summary>
    public double InletWaterTemperature { get; private set; }

    /// <summary>Gets the outlet water temperature [°C].</summary>
    public double OutletWaterTemperature { get; private set; }

    /// <summary>Gets the water flow rate [kg/s].</summary>
    public double WaterFlowRate { get; private set; }

    /// <summary>Gets the collected heat [kW].</summary>
    public double HeatCollection
    { get { return (OutletWaterTemperature - InletWaterTemperature) * WaterFlowRate * 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat; } }

    #endregion

    #region コンストラクタ

    /// <summary>Initializes a new instance.</summary>
    /// <param name="incline">Inclined surface.</param>
    /// <param name="surfaceArea">Collector surface area [m²].</param>
    /// <param name="coefficientA">Characteristic coefficient A.</param>
    /// <param name="coefficientB">Characteristic coefficient B.</param>
    public SimpleSolarCollector
      (IReadOnlyIncline incline, double surfaceArea, double coefficientA, double coefficientB)
    {
      Initialize(incline, surfaceArea, coefficientA, coefficientB);
    }

    /// <summary>Initializes a new instance.</summary>
    /// <param name="incline">Inclined surface.</param>
    /// <param name="surfaceArea">Collector surface area [m²].</param>
    /// <param name="heatReceiver">Collector receiver type.</param>
    public SimpleSolarCollector(IReadOnlyIncline incline, double surfaceArea, HeatReceiver heatReceiver)
    {
      switch (heatReceiver)
      {
        case HeatReceiver.FlatPlate:
          Initialize(incline, surfaceArea, -4.14, 0.8);
          return;
        default:
          Initialize(incline, surfaceArea, -1.75, 0.6);
          return;
      }
    }

    /// <summary>Initializes internal parameters.</summary>
    /// <param name="incline">Inclined surface.</param>
    /// <param name="surfaceArea">Collector surface area [m²].</param>
    /// <param name="coefficientA">Characteristic coefficient A.</param>
    /// <param name="coefficientB">Characteristic coefficient B.</param>
    private void Initialize
      (IReadOnlyIncline incline, double surfaceArea, double coefficientA, double coefficientB)
    {
      this.incline = new Incline(incline);
      this.CoefficientA = coefficientA;
      this.CoefficientB = coefficientB;
      this.SurfaceArea = surfaceArea;
    }

    #endregion

    #region publicメソッド

    /// <summary>Computes the required water flow rate [kg/s].</summary>
    /// <param name="totalIrradiance">Total irradiance on the inclined surface [W/m²].</param>
    /// <param name="waterTemperature">Inlet water temperature [°C].</param>
    /// <param name="temperatureDifferrence">Inlet-to-outlet temperature difference [K].</param>
    /// <param name="ambientTemperature">Ambient temperature [°C].</param>
    /// <returns>Water flow rate [kg/s].</returns>
    public double GetWaterFlowRate
      (double totalIrradiance, double waterTemperature, double temperatureDifferrence, double ambientTemperature)
    {
      //入口水温保存
      InletWaterTemperature = waterTemperature;

      //温度差が負または日射0の場合には停止
      if (temperatureDifferrence <= 0 || totalIrradiance <= 0)
      {
        OutletWaterTemperature = InletWaterTemperature;
        Efficiency = 0;
        WaterFlowRate = 0;
        return 0;
      }

      //出口水温[C]     
      OutletWaterTemperature = waterTemperature + temperatureDifferrence;

      //平均集熱温度[C]
      double tM = 0.5 * (InletWaterTemperature + OutletWaterTemperature);

      //外気温度差[K]
      double dT = tM - ambientTemperature;

      //集熱効率[-]
      Efficiency = Math.Max(0, CoefficientA * (dT / totalIrradiance) + CoefficientB);

      //水量[kg/s]
      WaterFlowRate = 0.001 * totalIrradiance * SurfaceArea
        * Efficiency / (temperatureDifferrence * 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat);
      return WaterFlowRate;
    }

    /// <summary>Computes the required water flow rate [kg/s].</summary>
    /// <param name="sun">Sun model providing radiation and position data.</param>
    /// <param name="waterTemperature">Inlet water temperature [°C].</param>
    /// <param name="temperatureDifferrence">Inlet-to-outlet temperature difference [K].</param>
    /// <param name="ambientTemperature">Ambient temperature [°C].</param>
    /// <returns>Water flow rate [kg/s].</returns>
    public double GetWaterFlowRate
      (IReadOnlySun sun, double waterTemperature, double temperatureDifferrence, double ambientTemperature)
    {
      double totalIrradiance = Incline.GetDirectSolarRadiationRate(sun) * sun.DirectNormalRadiation 
        + Incline.ConfigurationFactorToSky * sun.DiffuseHorizontalRadiation;
      return GetWaterFlowRate(totalIrradiance, waterTemperature, temperatureDifferrence, ambientTemperature);
    }

    /// <summary>Computes the outlet water temperature [°C].</summary>
    /// <param name="totalIrradiance">Total irradiance on the inclined surface [W/m²].</param>
    /// <param name="waterTemperature">Inlet water temperature [°C].</param>
    /// <param name="waterFlowRate">Water flow rate [kg/s].</param>
    /// <param name="ambientTemperature">Ambient temperature [°C].</param>
    /// <returns>Outlet water temperature [°C].</returns>
    public double GetOutletTemperature
      (double totalIrradiance, double waterTemperature, double waterFlowRate, double ambientTemperature)
    {
      //流量が0の場合には停止
      if (waterFlowRate <= 0)
      {
        OutletWaterTemperature = InletWaterTemperature;
        Efficiency = 0;
        WaterFlowRate = 0;
        return 0;
      }

      //出入口温度差の計算
      double dT = 0.001 * SurfaceArea * 
        (CoefficientA * (waterTemperature - ambientTemperature) + CoefficientB * totalIrradiance)
        / (0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat * waterFlowRate - 0.0005 * SurfaceArea * CoefficientA);

      GetWaterFlowRate(totalIrradiance, waterTemperature, dT, ambientTemperature);
      return OutletWaterTemperature;
    }

    /// <summary>Computes the outlet water temperature [°C].</summary>
    /// <param name="sun">Sun model providing radiation and position data.</param>
    /// <param name="waterTemperature">Inlet water temperature [°C].</param>
    /// <param name="waterFlowRate">Water flow rate [kg/s].</param>
    /// <param name="ambientTemperature">Ambient temperature [°C].</param>
    /// <returns>Outlet water temperature [°C].</returns>
    public double GetOutletTemperature
      (IReadOnlySun sun, double waterTemperature, double waterFlowRate, double ambientTemperature)
    {
      double totalIrradiance = Incline.GetDirectSolarRadiationRate(sun) * sun.DirectNormalRadiation
        + Incline.ConfigurationFactorToSky * sun.DiffuseHorizontalRadiation;
      return GetOutletTemperature(totalIrradiance, waterTemperature, waterFlowRate, ambientTemperature);
    }

    #endregion

  }
}
