/* CrossFinCondensor.cs
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

namespace Popolo.Core.HVAC.HeatExchanger
{
  /// <summary>Cross-fin (plate-fin-and-tube) air-cooled condenser.</summary>
  public class CrossFinCondensor : IReadOnlyCrossFinCondensor
  {

    #region 定数宣言


    /// <summary>Heat transfer coefficient calculation factor for the coil [kW/(m²·K)].</summary>
    private const double CF_A = 0.0688;
    private const double CF_B = 0.3187;

    #endregion

    #region プロパティ

    /// <summary>Temperature reduction effectiveness of water spray [-].</summary>
    /// <remarks>Typical value is 0.4–0.5. Set to 0 to disable water spray.</remarks>
    private double sprayEffectiveness = 0.0;

    /// <summary>Gets the heat transfer surface area [m²].</summary>
    public double SurfaceArea { get; private set; }

    /// <summary>Gets the nominal air flow rate [kg/s].</summary>
    public double NominalAirFlowRate { get; private set; }

    /// <summary>Gets the current air flow rate [kg/s].</summary>
    public double AirFlowRate { get; private set; }

    /// <summary>Gets the condensing temperature [°C].</summary>
    public double CondensingTemperature { get; private set; }

    /// <summary>Gets or sets the inlet air dry-bulb temperature [°C].</summary>
    public double InletAirTemperature { get; set; }

    /// <summary>Gets or sets the inlet air humidity ratio [kg/kg].</summary>
    public double InletAirHumidityRatio { get; set; }

    /// <summary>Gets the outlet air dry-bulb temperature [°C].</summary>
    public double OutletAirTemperature { get; set; }

    /// <summary>Gets the outlet air humidity ratio [kg/kg].</summary>
    public double OutletAirHumidityRatio { get; set; }

    /// <summary>Gets the heat transfer rate [kW].</summary>
    public double HeatTransfer { get; private set; }

    /// <summary>Gets or sets the water spray temperature reduction effectiveness [-].</summary>
    public double SprayEffectiveness
    {
      get { return sprayEffectiveness; }
      set { sprayEffectiveness = Math.Max(0, Math.Min(value, 1.0)); }
    }

    /// <summary>Gets the water consumption rate due to spray [kg/s].</summary>
    public double WaterSupply { get; private set; }

    /// <summary>Gets a value indicating whether the condenser is shut off.</summary>
    public bool IsShutOff { get; private set; }

    /// <summary>Gets a value indicating whether water spray is active.</summary>
    public bool UseWaterSpray { get; set; }

    #endregion

    #region コンストラクタ

    /// <summary>Initializes a new instance from rated operating conditions.</summary>
    /// <param name="cndTemperature">Condensing temperature [°C].</param>
    /// <param name="heatTransfer">Rated heat transfer capacity [kW].</param>
    /// <param name="airFlowRate">Air mass flow rate [kg/s].</param>
    /// <param name="inletAirTemperature">Inlet air dry-bulb temperature [°C].</param>
    /// <param name="inletAirHumidityRatio">Inlet air humidity ratio [kg/kg].</param>
    public CrossFinCondensor
      (double cndTemperature, double heatTransfer, double airFlowRate,
      double inletAirTemperature, double inletAirHumidityRatio)
    {
      //プロパティ初期化
      NominalAirFlowRate = airFlowRate;
      AirFlowRate = airFlowRate;
      InletAirTemperature = inletAirTemperature;
      InletAirHumidityRatio = inletAirHumidityRatio;
      ShutOff();

      //伝熱面積を初期化する
      SurfaceArea = GetSurfaceArea
        (cndTemperature, heatTransfer, airFlowRate, airFlowRate, inletAirTemperature, inletAirHumidityRatio);
    }

    /// <summary>Computes the heat transfer surface area [m²] from rated conditions.</summary>
    /// <param name="cndTemperature">Condensing temperature [°C].</param>
    /// <param name="heatTransfer">Rated heat transfer capacity [kW].</param>
    /// <param name="airFlowRate">Air mass flow rate [kg/s].</param>
    /// <param name="nominalAirFlowRate">Nominal air mass flow rate [kg/s].</param>
    /// <param name="inletAirTemperature">Inlet air dry-bulb temperature [°C].</param>
    /// <param name="inletAirHumidityRatio">Inlet air humidity ratio [kg/kg].</param>
    /// <returns>Heat transfer surface area [m²].</returns>
    public static double GetSurfaceArea
      (double cndTemperature, double heatTransfer, double airFlowRate,
      double nominalAirFlowRate, double inletAirTemperature, double inletAirHumidityRatio)
    {
      double cpma = MoistAir.GetSpecificHeat(inletAirHumidityRatio);
      double mca = cpma * airFlowRate;
      double epsilon = heatTransfer / (mca * (cndTemperature - inletAirTemperature));
      double kCnd = CF_A * Math.Pow(airFlowRate / nominalAirFlowRate, CF_B);
      return -Math.Log(1 - epsilon) * mca / kCnd;
    }

    /// <summary>Shuts off the condenser.</summary>
    private void ShutOff()
    {
      AirFlowRate = 0;
      OutletAirTemperature = InletAirTemperature;
      OutletAirHumidityRatio = InletAirHumidityRatio;
      CondensingTemperature = InletAirTemperature;
      WaterSupply = 0;
      IsShutOff = true;
    }

    /// <summary>Computes the water consumption rate due to spray [kg/s].</summary>
    /// <param name="inletAirTemperature">Inlet air dry-bulb temperature [°C].</param>
    /// <param name="inletAirHumidityRatio">Inlet air humidity ratio [kg/kg].</param>
    /// <param name="sprayEffectiveness">Water spray temperature reduction effectiveness [-].</param>
    /// <param name="airFlowRate">Air mass flow rate [kg/s].</param>
    private static double GetWaterSupply
      (ref double inletAirTemperature, ref double inletAirHumidityRatio,
      double sprayEffectiveness, double airFlowRate)
    {
      double twb = MoistAir.GetWetBulbTemperatureFromDryBulbTemperatureAndHumidityRatio
          (inletAirTemperature, inletAirHumidityRatio, PhysicsConstants.StandardAtmosphericPressure);
      double ts = MoistAir.GetDryBulbTemperatureFromWetBulbTemperatureAndRelativeHumidity
        (twb, 100, PhysicsConstants.StandardAtmosphericPressure);
      double ws = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndRelativeHumidity
        (ts, 100, PhysicsConstants.StandardAtmosphericPressure);
      inletAirTemperature -= sprayEffectiveness * (inletAirTemperature - ts);
      inletAirHumidityRatio += sprayEffectiveness * (ws - inletAirHumidityRatio);
      return airFlowRate * sprayEffectiveness * (ws - inletAirHumidityRatio);
    }

    #endregion

    #region 交換熱量計算処理

    /// <summary>Computes the heat transfer rate [kW] (positive = heating, negative = cooling).</summary>
    /// <param name="cndTemperature">Condensing temperature [°C].</param>
    /// <param name="airFlowRate">Air mass flow rate [kg/s].</param>
    /// <param name="inletAirTemperature">Inlet air dry-bulb temperature [°C].</param>
    /// <param name="inletAirHumidityRatio">Inlet air humidity ratio [kg/kg].</param>
    /// <returns>Heat transfer rate [kW].</returns>
    public double GetHeatTransfer
      (double cndTemperature, double airFlowRate, double inletAirTemperature, double inletAirHumidityRatio)
    {
      //プロパティ設定
      CondensingTemperature = cndTemperature;
      AirFlowRate = airFlowRate;
      InletAirTemperature = inletAirTemperature;
      InletAirHumidityRatio = inletAirHumidityRatio;

      //運転判定
      if (airFlowRate <= 0 || cndTemperature <= inletAirTemperature)
      {
        ShutOff();
        return 0;
      }

      double ht, to, wo, ws, sp;
      if (UseWaterSpray) sp = sprayEffectiveness;
      else sp = 0;
      GetHeatTransfer(cndTemperature, airFlowRate, NominalAirFlowRate, SurfaceArea, 
        inletAirTemperature, inletAirHumidityRatio, sp, out ht, out to, out wo, out ws);
      OutletAirTemperature = to;
      OutletAirHumidityRatio = wo;
      HeatTransfer = ht;
      WaterSupply = ws;
      return ht;
    }

    /// <summary>Computes the heat transfer rate [kW] (positive = heating, negative = cooling).</summary>
    /// <param name="cndTemperature">Condensing temperature [°C].</param>
    /// <param name="airFlowRate">Air mass flow rate [kg/s].</param>
    /// <param name="nominalAirFlowRate">Nominal air mass flow rate [kg/s].</param>
    /// <param name="surfaceArea">Heat transfer surface area [m²].</param>
    /// <param name="inletAirTemperature">Inlet air dry-bulb temperature [°C].</param>
    /// <param name="inletAirHumidityRatio">Inlet air humidity ratio [kg/kg].</param>
    /// <param name="sprayEffectiveness">Water spray temperature reduction effectiveness [-].</param>
    /// <param name="heatTransfer">Output: heat transfer rate [kW].</param>
    /// <param name="outletAirTemperature">Output: outlet air dry-bulb temperature [°C].</param>
    /// <param name="outletAirHumidityRatio">Output: outlet air humidity ratio [kg/kg].</param>
    /// <param name="waterSupply">Output: water consumption rate [kg/s].</param>
    public static void GetHeatTransfer
      (double cndTemperature, double airFlowRate, double nominalAirFlowRate, double surfaceArea,
      double inletAirTemperature, double inletAirHumidityRatio, double sprayEffectiveness,
      out double heatTransfer, out double outletAirTemperature,
      out double outletAirHumidityRatio, out double waterSupply)
    {
      //水噴霧がある場合
      if (0 < sprayEffectiveness)
        waterSupply = GetWaterSupply
         (ref inletAirTemperature, ref inletAirHumidityRatio, sprayEffectiveness, airFlowRate);
      //水噴霧がない場合
      else waterSupply = 0;

      //熱通過率[kW/m2K]
      double kCnd = CF_A * Math.Pow(airFlowRate / nominalAirFlowRate, CF_B);
      //湿り空気比熱[kJ/kgK]
      double cpma = MoistAir.GetSpecificHeat(inletAirHumidityRatio);
      double mca = cpma * airFlowRate;

      double epsilon = 1 - Math.Exp(-kCnd * surfaceArea / mca);
      double q = epsilon * mca * (cndTemperature - inletAirTemperature);
      outletAirTemperature = inletAirTemperature + q / mca;
      outletAirHumidityRatio = inletAirHumidityRatio;
      heatTransfer = q;
    }

    #endregion

    #region 凝縮温度計算処理

    /// <summary>Computes the condensing temperature [°C] from the given air and heat conditions.</summary>
    /// <param name="heatTransfer">Heat transfer rate [kW].</param>
    /// <param name="airFlowRate">Air mass flow rate [kg/s].</param>
    /// <param name="inletAirTemperature">Inlet air dry-bulb temperature [°C].</param>
    /// <param name="inletAirHumidityRatio">Inlet air humidity ratio [kg/kg].</param>
    /// <returns>Condensing temperature [°C].</returns>
    public double GetCondensingTemperature
      (double heatTransfer, double airFlowRate, double inletAirTemperature, double inletAirHumidityRatio)
    {
      //プロパティ設定
      HeatTransfer = heatTransfer;
      AirFlowRate = airFlowRate;
      InletAirTemperature = inletAirTemperature;
      InletAirHumidityRatio = inletAirHumidityRatio;

      //運転判定
      if (airFlowRate <= 0 || heatTransfer <= 0)
      {
        ShutOff();
        return 0;
      }

      double tc, to, wo, ws;
      GetCondensingTemperature(heatTransfer, airFlowRate, NominalAirFlowRate, SurfaceArea,
        inletAirTemperature, inletAirHumidityRatio, sprayEffectiveness, out tc, out to, out wo, out ws);
      OutletAirTemperature = to;
      OutletAirHumidityRatio = wo;
      WaterSupply = ws;
      return tc;
    }

    /// <summary>Computes the condensing temperature [°C] from the given air and heat conditions.</summary>
    /// <param name="heatTransfer">Heat transfer rate [kW].</param>
    /// <param name="airFlowRate">Air mass flow rate [kg/s].</param>
    /// <param name="nominalAirFlowRate">Nominal air mass flow rate [kg/s].</param>
    /// <param name="surfaceArea">Heat transfer surface area [m²].</param>
    /// <param name="inletAirTemperature">Inlet air dry-bulb temperature [°C].</param>
    /// <param name="inletAirHumidityRatio">Inlet air humidity ratio [kg/kg].</param>
    /// <param name="sprayEffectiveness">Water spray temperature reduction effectiveness [-].</param>
    /// <param name="condensingTemperature">Output: condensing temperature [°C].</param>
    /// <param name="outletAirTemperature">Output: outlet air dry-bulb temperature [°C].</param>
    /// <param name="outletAirHumidityRatio">Output: outlet air humidity ratio [kg/kg].</param>
    /// <param name="waterSupply">Output: water consumption rate [kg/s].</param>
    public static void GetCondensingTemperature
      (double heatTransfer, double airFlowRate, double nominalAirFlowRate,
      double surfaceArea, double inletAirTemperature, double inletAirHumidityRatio,
      double sprayEffectiveness, out double condensingTemperature,
      out double outletAirTemperature, out double outletAirHumidityRatio,
      out double waterSupply)
    {
      //水噴霧がある場合
      if (0 < sprayEffectiveness)
        waterSupply = GetWaterSupply
         (ref inletAirTemperature, ref inletAirHumidityRatio, sprayEffectiveness, airFlowRate);
      //水噴霧がない場合
      else waterSupply = 0;

      //熱通過率[kW/m2K]
      double kCnd = CF_A * Math.Pow(airFlowRate / nominalAirFlowRate, CF_B);
      //湿り空気比熱[kJ/kgK]
      double cpma = MoistAir.GetSpecificHeat(inletAirHumidityRatio);
      double mca = cpma * airFlowRate;

      outletAirTemperature = inletAirTemperature + heatTransfer / mca;
      outletAirHumidityRatio = inletAirHumidityRatio;
      double epsilon = 1 - Math.Exp(-kCnd * surfaceArea / mca);
      condensingTemperature = inletAirTemperature + heatTransfer / (epsilon * mca);
    }

    #endregion

  }
}
