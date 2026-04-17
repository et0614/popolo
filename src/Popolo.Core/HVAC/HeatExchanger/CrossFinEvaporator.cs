/* CrossFinEvaporator.cs
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

using Popolo.Core.Exceptions;
using Popolo.Core.Physics;
using Popolo.Core.Numerics;

namespace Popolo.Core.HVAC.HeatExchanger
{
  /// <summary>Cross-fin (plate-fin-and-tube) evaporator with dehumidification and frosting capability.</summary>
  /// <remarks>Deprecated: use Popolo.Core.HVAC.VRF.VRFUnit instead.</remarks>
  public class CrossFinEvaporator : IReadOnlyCrossFinEvaporator
  {

    #region 定数宣言


    /// <summary>Latent heat of sublimation of ice [kJ/kg].</summary>
    private const double SUBLIMINATION_LATENT_HEAT = 2837;

    /// <summary>Latent heat of vaporisation of water at 0°C [kJ/kg].</summary>
    private const double VAPORIZATION_LATENT_HEAT = 2501;

    /// <summary>Specific heat of dry air at constant pressure [kJ/(kg·K)].</summary>
    private const double DRYAIR_ISOBARIC_SPECIFIC_HEAT = 1.005;

    /// <summary>Specific heat of water vapour at constant pressure [kJ/(kg·K)].</summary>
    private const double VAPOR_ISOBARIC_SPECIFIC_HEAT = 1.805;

    /// <summary>Specific heat of ice at constant pressure [kJ/(kg·K)].</summary>
    private const double ICE_ISOBARIC_SPECIFIC_HEAT = 2.090;

    /// <summary>Heat transfer coefficient calculation factor for the dry coil [kW/(m²·K)].</summary>
    /// <remarks>Based on plate-fin at 2.0 m/s face velocity (Air Conditioning Handbook).</remarks>
    private const double CF_A = 0.0236;
    private const double CF_B = 0.5479;

    /// <summary>Heat transfer efficiency reduction factor due to frosting.</summary>
    private const double F_PENALTY = 0.6;

    #endregion

    #region プロパティ
    
    /// <summary>Relative humidity threshold at the dry/wet boundary [%].</summary>
    private double borderRelativeHumidity;

    /// <summary>Gets the total heat transfer surface area [m²].</summary>
    public double SurfaceArea { get; private set; }

    /// <summary>Gets the dry surface area [m²].</summary>
    public double DrySurfaceArea { get; private set; }

    /// <summary>Gets the wet surface area [m²].</summary>
    public double WetSurfaceArea { get; private set; }

    /// <summary>Gets the frosted surface area [m²].</summary>
    public double FrostSurfaceArea
    {
      get { return SurfaceArea - (DrySurfaceArea + WetSurfaceArea); }
    }

    /// <summary>Gets the nominal air flow rate [kg/s].</summary>
    public double NominalAirFlowRate { get; private set; }

    /// <summary>Gets the current air flow rate [kg/s].</summary>
    public double AirFlowRate { get; private set; }

    /// <summary>Gets the evaporating temperature [°C].</summary>
    public double EvaporatingTemperature { get; private set; }

    /// <summary>Gets or sets the dry/wet boundary relative humidity [%].</summary>
    public double BorderRelativeHumidity
    {
      get { return borderRelativeHumidity; }
      set { borderRelativeHumidity = Math.Max(50, value); }
    }

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

    /// <summary>Gets the defrost load [kW].</summary>
    public double DefrostLoad { get; private set; }

    /// <summary>Gets a value indicating whether the evaporator is shut off.</summary>
    public bool IsShutOff { get; private set; }

    #endregion

    #region コンストラクタ

    /// <summary>Initializes a new instance from rated operating conditions.</summary>
    /// <param name="evpTemperature">Evaporating temperature [°C].</param>
    /// <param name="heatTransfer">Rated heat transfer capacity [kW].</param>
    /// <param name="airFlowRate">Air mass flow rate [kg/s].</param>
    /// <param name="inletAirTemperature">Inlet air dry-bulb temperature [°C].</param>
    /// <param name="inletAirHumidityRatio">Inlet air humidity ratio [kg/kg].</param>
    /// <param name="borderRelativeHumidity">Dry/wet boundary relative humidity [%].</param>
    public CrossFinEvaporator(double evpTemperature, double heatTransfer, double airFlowRate,
      double inletAirTemperature, double inletAirHumidityRatio, double borderRelativeHumidity)
    {
      //プロパティ初期化
      NominalAirFlowRate = airFlowRate;
      AirFlowRate = airFlowRate;
      BorderRelativeHumidity = borderRelativeHumidity;
      InletAirTemperature = inletAirTemperature;
      InletAirHumidityRatio = inletAirHumidityRatio;
      ShutOff();

      //伝熱面積を初期化する
      SurfaceArea = GetSurfaceArea(evpTemperature, heatTransfer, airFlowRate, airFlowRate,
        inletAirTemperature, inletAirHumidityRatio, borderRelativeHumidity);
    }

    /// <summary>Computes the heat transfer surface area [m²] from rated conditions.</summary>
    /// <param name="evpTemperature">Evaporating temperature [°C].</param>
    /// <param name="heatTransfer">Rated heat transfer capacity [kW].</param>
    /// <param name="airFlowRate">Air mass flow rate [kg/s].</param>
    /// <param name="nominalAirFlowRate">Nominal air mass flow rate [kg/s].</param>
    /// <param name="inletAirTemperature">Inlet air dry-bulb temperature [°C].</param>
    /// <param name="inletAirHumidityRatio">Inlet air humidity ratio [kg/kg].</param>
    /// <param name="borderRelativeHumidity">Dry/wet boundary relative humidity [%].</param>
    /// <returns>Heat transfer surface area [m²].</returns>
    public static double GetSurfaceArea
      (double evpTemperature, double heatTransfer, double airFlowRate, double nominalAirFlowRate, 
      double inletAirTemperature, double inletAirHumidityRatio, double borderRelativeHumidity)
    {
      double epsilon;
      double kD = CF_A * Math.Pow(airFlowRate / nominalAirFlowRate, CF_B);

      //乾湿境界判定
      double rh = MoistAir.GetRelativeHumidityFromDryBulbTemperatureAndHumidityRatio
        (inletAirTemperature, inletAirHumidityRatio, PhysicsConstants.StandardAtmosphericPressure);
      borderRelativeHumidity = Math.Max(rh, borderRelativeHumidity);

      //湿り空気比熱の計算
      double cpmaWB = MoistAir.GetSpecificHeat(inletAirHumidityRatio);

      //乾きコイル面積の計算
      double mca = cpmaWB * airFlowRate;
      double tWB = MoistAir.GetDryBulbTemperatureFromHumidityRatioAndRelativeHumidity
        (inletAirHumidityRatio, borderRelativeHumidity, PhysicsConstants.StandardAtmosphericPressure);
      double qD = (inletAirTemperature - tWB) * mca;

      //乾きコイルで伝熱が終了する場合
      if (heatTransfer < qD)
      {
        epsilon = heatTransfer / (mca * (inletAirTemperature - evpTemperature));
        return -Math.Log(1 - epsilon) * mca / kD;
      }
      //湿りコイルまで到達する場合
      epsilon = qD / (mca * (inletAirTemperature - evpTemperature));
      double sD = -Math.Log(1 - epsilon) * mca / kD;

      double qW, sW, xFB, tFB, cpmaFB;
      //湿りコイルがある場合
      if (0 < tWB)
      {
        tFB = 0;
        //湿りコイル面積の計算
        xFB = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndRelativeHumidity
          (0, borderRelativeHumidity, PhysicsConstants.StandardAtmosphericPressure);
        cpmaFB = MoistAir.GetSpecificHeat(xFB);
        double hWB = MoistAir.GetEnthalpyFromDryBulbTemperatureAndHumidityRatio
          (tWB, inletAirHumidityRatio);
        double hEvp = MoistAir.GetEnthalpyFromDryBulbTemperatureAndRelativeHumidity
          (evpTemperature, 100, PhysicsConstants.StandardAtmosphericPressure);
        double hFB = MoistAir.GetEnthalpyFromDryBulbTemperatureAndRelativeHumidity
          (0, borderRelativeHumidity, PhysicsConstants.StandardAtmosphericPressure);
        qW = (hWB - hFB) * airFlowRate;
        double kW = kD / (0.5 * (cpmaWB + cpmaFB));

        //湿りコイルで伝熱が終了する場合
        if (heatTransfer - qD < qW)
        {
          epsilon = (heatTransfer - qD) / (airFlowRate * (hWB - hEvp));
          return -Math.Log(1 - epsilon) * airFlowRate / kW + sD;
        }
        //着霜コイルまで到達する場合
        epsilon = qW / (airFlowRate * (hWB - hEvp));
        sW = -Math.Log(1 - epsilon) * airFlowRate / kW;
      }
      else
      {
        tFB = tWB;
        xFB = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndRelativeHumidity
          (tWB, borderRelativeHumidity, PhysicsConstants.StandardAtmosphericPressure);
        cpmaFB = MoistAir.GetSpecificHeat(xFB);
        qW = 0;
        sW = 0;
      }

      //着霜コイル面積の計算
      double kF = kD / cpmaFB * F_PENALTY;
      double hdF = GetHD(tFB, borderRelativeHumidity);
      double hdEvp = GetHD(evpTemperature, 100);
      epsilon = (heatTransfer - qD - qW) / (airFlowRate * (hdF - hdEvp));
      if (1 <= epsilon) throw new PopoloNumericalException(
        "CrossFinEvaporator.GetSurfaceArea",
        $"NTU epsilon reached {epsilon} (>= 1); heat transfer requirement exceeds physical limit.");
      double sF = -Math.Log(1 - epsilon) * airFlowRate / kF;

      return sF + sD + sW;
    }

    /// <summary>Computes the solid-reference specific enthalpy [kJ/kg] from dry-bulb temperature.</summary>
    /// <param name="temperature">Dry-bulb temperature [°C].</param>
    /// <param name="relativeHumidity">Relative humidity [%].</param>
    /// <returns>Solid-reference specific enthalpy [kJ/kg].</returns>
    private static double GetHD(double temperature, double relativeHumidity)
    {
      double x = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndRelativeHumidity
        (temperature, relativeHumidity, PhysicsConstants.StandardAtmosphericPressure);
      return DRYAIR_ISOBARIC_SPECIFIC_HEAT * temperature 
        + (ICE_ISOBARIC_SPECIFIC_HEAT * temperature + SUBLIMINATION_LATENT_HEAT) * x;
    }

    /// <summary>Shuts off the evaporator.</summary>
    private void ShutOff()
    {
      AirFlowRate = 0;
      OutletAirTemperature = InletAirTemperature;
      OutletAirHumidityRatio = InletAirHumidityRatio;
      DrySurfaceArea = SurfaceArea;
      WetSurfaceArea = 0;
      HeatTransfer = 0;
      EvaporatingTemperature = 0;
      DefrostLoad = 0;
      IsShutOff = true;
    }

    #endregion

    #region 交換熱量計算処理

    /// <summary>Computes the heat transfer rate [kW] (positive = cooling, negative = heating).</summary>
    /// <param name="evpTemperature">Evaporating temperature [°C].</param>
    /// <param name="airFlowRate">Air mass flow rate [kg/s].</param>
    /// <param name="inletAirTemperature">Inlet air dry-bulb temperature [°C].</param>
    /// <param name="inletAirHumidityRatio">Inlet air humidity ratio [kg/kg].</param>
    /// <returns>Heat transfer rate [kW].</returns>
    public double GetHeatTransfer
      (double evpTemperature, double airFlowRate, double inletAirTemperature, double inletAirHumidityRatio)
    {
      //プロパティ設定
      EvaporatingTemperature = evpTemperature;
      AirFlowRate = airFlowRate;
      InletAirTemperature = inletAirTemperature;
      InletAirHumidityRatio = inletAirHumidityRatio;

      //運転判定
      if (airFlowRate <= 0 || inletAirTemperature <= evpTemperature)
      {
        ShutOff();
        return 0;
      }

      double ht, to, wo, sd, sw, dfl;
      GetHeatTransfer(evpTemperature, airFlowRate, NominalAirFlowRate, SurfaceArea, inletAirTemperature, 
        inletAirHumidityRatio, borderRelativeHumidity, out ht, out to, out wo, out sd, out sw, out dfl);
      OutletAirTemperature = to;
      OutletAirHumidityRatio = wo;
      DrySurfaceArea = sd;
      WetSurfaceArea = sw;
      HeatTransfer = ht;
      DefrostLoad = dfl;
      return ht;
    }

    /// <summary>Computes the heat transfer rate [kW] (positive = cooling, negative = heating).</summary>
    /// <param name="evpTemperature">Evaporating temperature [°C].</param>
    /// <param name="airFlowRate">Air mass flow rate [kg/s].</param>
    /// <param name="nominalAirFlowRate">Nominal air mass flow rate [kg/s].</param>
    /// <param name="surfaceArea">Heat transfer surface area [m²].</param>
    /// <param name="inletAirTemperature">Inlet air dry-bulb temperature [°C].</param>
    /// <param name="inletAirHumidityRatio">Inlet air humidity ratio [kg/kg].</param>
    /// <param name="borderRelativeHumidity">Dry/wet boundary relative humidity [%].</param>
    /// <param name="heatTransfer">Output: heat transfer rate [kW].</param>
    /// <param name="outletAirTemperature">Output: outlet air dry-bulb temperature [°C].</param>
    /// <param name="outletAirHumidityRatio">Output: outlet air humidity ratio [kg/kg].</param>
    /// <param name="sD">Output: dry coil surface area [m²].</param>
    /// <param name="sW">Output: wet coil surface area [m²].</param>
    /// <param name="defrostLoad">Output: defrost load [kW].</param>
    public static void GetHeatTransfer
      (double evpTemperature, double airFlowRate, double nominalAirFlowRate, double surfaceArea,
      double inletAirTemperature, double inletAirHumidityRatio, double borderRelativeHumidity, 
      out double heatTransfer, out double outletAirTemperature, out double outletAirHumidityRatio,
      out double sD, out double sW, out double defrostLoad)
    {
      //乾きコイルの熱通過率[kW/m2K]
      double kD = CF_A * Math.Pow(airFlowRate / nominalAirFlowRate, CF_B);

      //乾湿境界判定
      double rh = MoistAir.GetRelativeHumidityFromDryBulbTemperatureAndHumidityRatio
        (inletAirTemperature, inletAirHumidityRatio, PhysicsConstants.StandardAtmosphericPressure);
      borderRelativeHumidity = Math.Max(rh, borderRelativeHumidity);
      double tWB = MoistAir.GetDryBulbTemperatureFromHumidityRatioAndRelativeHumidity
        (inletAirHumidityRatio, borderRelativeHumidity, PhysicsConstants.StandardAtmosphericPressure);

      //湿り空気比熱[kJ/kgK]の計算
      double cpmaWB = MoistAir.GetSpecificHeat(inletAirHumidityRatio);
      double mca = cpmaWB * airFlowRate;

      //乾きコイルの計算
      //露点まで冷却するために必要な面積を計算
      double qD = mca * (inletAirTemperature - tWB);
      double epsilonD = qD / (mca * (inletAirTemperature - evpTemperature));
      if (epsilonD < 1) sD = -Math.Log(1 - epsilonD) * mca / kD;
      else sD = surfaceArea;

      double hWB = MoistAir.GetEnthalpyFromDryBulbTemperatureAndHumidityRatio(tWB, inletAirHumidityRatio);
      double hEvp = 
        MoistAir.GetEnthalpyFromDryBulbTemperatureAndRelativeHumidity(evpTemperature, 100, PhysicsConstants.StandardAtmosphericPressure);

      //乾きコイルのみで伝熱が終了する場合
      if (surfaceArea <= sD || 1 <= epsilonD || hWB < hEvp)
      {
        sD = surfaceArea;
        sW = 0;
        defrostLoad = 0;
        outletAirHumidityRatio = inletAirHumidityRatio;

        epsilonD = 1 - Math.Exp(-kD * sD / mca);
        qD = epsilonD * mca * (inletAirTemperature - evpTemperature);
        outletAirTemperature = inletAirTemperature - qD / mca;
        heatTransfer = qD;
        return;
      }

      //湿りコイルがある場合
      double tFB, qW, xFB, cpmaFB;
      if (0 < tWB)
      {
        tFB = 0;
        xFB = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndRelativeHumidity
          (0, borderRelativeHumidity, PhysicsConstants.StandardAtmosphericPressure);
        cpmaFB = MoistAir.GetSpecificHeat(xFB);

        //凝固点（0C）まで冷却するために必要な面積を計算
        double hFB = MoistAir.GetEnthalpyFromDryBulbTemperatureAndRelativeHumidity
          (0, borderRelativeHumidity, PhysicsConstants.StandardAtmosphericPressure);

        qW = (hWB - hFB) * airFlowRate;
        double kW = kD / (0.5 * (cpmaWB + cpmaFB));
        double epsilonW = qW / (airFlowRate * (hWB - hEvp));
        if (epsilonW < 1) sW = -Math.Log(1 - epsilonW) * airFlowRate / kW;
        else sW = surfaceArea - sD;

        //湿りコイルで伝熱が終了する場合
        if (surfaceArea <= sW + sD || 1 <= epsilonW)
        {
          sW = surfaceArea - sD;
          defrostLoad = 0;

          epsilonW = 1 - Math.Exp(-kW * sW / airFlowRate);
          qW = epsilonW * airFlowRate * (hWB - hEvp);
          double ho = hWB - qW / airFlowRate;
          outletAirHumidityRatio = MoistAir.GetHumidityRatioFromEnthalpyAndRelativeHumidity
            (ho, borderRelativeHumidity, PhysicsConstants.StandardAtmosphericPressure);
          outletAirTemperature = MoistAir.GetDryBulbTemperatureFromHumidityRatioAndEnthalpy
            (outletAirHumidityRatio, ho);
          heatTransfer = qD + qW;
          return;
        }
      }
      else
      {
        qW = 0;
        sW = 0;
        tFB = tWB;
        xFB = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndRelativeHumidity
          (tWB, borderRelativeHumidity, PhysicsConstants.StandardAtmosphericPressure);
        cpmaFB = MoistAir.GetSpecificHeat(xFB);
      }

      //着霜コイルの計算
      double kF = kD / cpmaFB * F_PENALTY;
      double hdFB = GetHD(tFB, borderRelativeHumidity);
      double hdEvp = GetHD(evpTemperature, 100);
      double sF = surfaceArea - sD - sW;
      double epsilonF = 1 - Math.Exp(-kF * sF / airFlowRate);
      double qF = epsilonF * airFlowRate * (hdFB - hdEvp);
      double hdo = hdFB - qF / airFlowRate;

      //出口空気温度を収束計算
      double to = tFB;
      double err1 = Math.Abs(GetHD(to, borderRelativeHumidity) - hdo);
      const double DELTA = 0.001;
      while (0.01 < err1)
      {
        double err2 = Math.Abs(GetHD(to + DELTA, borderRelativeHumidity) - hdo);
        to -= DELTA * err1 / (err2 - err1);
        err1 = Math.Abs(GetHD(to, borderRelativeHumidity) - hdo);
      }
      outletAirTemperature = to;
      outletAirHumidityRatio = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndRelativeHumidity
        (outletAirTemperature, borderRelativeHumidity, PhysicsConstants.StandardAtmosphericPressure);

      //除霜負荷を計算
      defrostLoad = airFlowRate * (xFB - outletAirHumidityRatio) 
        * (SUBLIMINATION_LATENT_HEAT - ICE_ISOBARIC_SPECIFIC_HEAT * outletAirTemperature);

      //交換熱量[kW]を集計
      heatTransfer = qD + qW + qF;
    }

    #endregion

    #region 蒸発温度計算処理

    /// <summary>Computes the evaporating temperature [°C] from the given air and heat conditions.</summary>
    /// <param name="heatTransfer">Heat transfer rate [kW].</param>
    /// <param name="airFlowRate">Air mass flow rate [kg/s].</param>
    /// <param name="inletAirTemperature">Inlet air dry-bulb temperature [°C].</param>
    /// <param name="inletAirHumidityRatio">Inlet air humidity ratio [kg/kg].</param>
    /// <param name="deductDefrostLoad">True to subtract the defrost load from the heat transfer.</param>
    /// <returns>Evaporating temperature [°C].</returns>
    public double GetEvaporatingTemperature(double heatTransfer, double airFlowRate, 
      double inletAirTemperature, double inletAirHumidityRatio, bool deductDefrostLoad)
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

      double te, to, wo, sd, sw, dfl;
      GetEvaporatingTemperature(heatTransfer, airFlowRate, NominalAirFlowRate, SurfaceArea, 
        inletAirTemperature, inletAirHumidityRatio, borderRelativeHumidity, deductDefrostLoad, 
        out te, out to, out wo, out sd, out sw, out dfl);
      OutletAirTemperature = to;
      OutletAirHumidityRatio = wo;
      DrySurfaceArea = sd;
      WetSurfaceArea = sw;
      EvaporatingTemperature = te;
      DefrostLoad = dfl;
      return te;
    }

    /// <summary>Computes the evaporating temperature [°C] from the given air and heat conditions.</summary>
    /// <param name="heatTransfer">Heat transfer rate [kW].</param>
    /// <param name="airFlowRate">Air mass flow rate [kg/s].</param>
    /// <param name="nominalAirFlowRate">Nominal air mass flow rate [kg/s].</param>
    /// <param name="surfaceArea">Heat transfer surface area [m²].</param>
    /// <param name="inletAirTemperature">Inlet air dry-bulb temperature [°C].</param>
    /// <param name="inletAirHumidityRatio">Inlet air humidity ratio [kg/kg].</param>
    /// <param name="borderRelativeHumidity">Dry/wet boundary relative humidity [%].</param>
    /// <param name="deductDefrostLoad">True to subtract the defrost load from the heat transfer.</param>
    /// <param name="evaporatingTemperature">Output: evaporating temperature [°C].</param>
    /// <param name="outletAirTemperature">Output: outlet air dry-bulb temperature [°C].</param>
    /// <param name="outletAirHumidityRatio">Output: outlet air humidity ratio [kg/kg].</param>
    /// <param name="sD">Output: dry coil surface area [m²].</param>
    /// <param name="sW">Output: wet coil surface area [m²].</param>
    /// <param name="defrostLoad">Output: defrost load [kW].</param>
    public static void GetEvaporatingTemperature
      (double heatTransfer, double airFlowRate, double nominalAirFlowRate,
      double surfaceArea, double inletAirTemperature, double inletAirHumidityRatio, double borderRelativeHumidity,
      bool deductDefrostLoad, out double evaporatingTemperature, out double outletAirTemperature, 
      out double outletAirHumidityRatio, out double sD, out double sW, out double defrostLoad)
    {
      //蒸発温度を仮定
      double cpma = MoistAir.GetSpecificHeat(inletAirHumidityRatio);
      evaporatingTemperature = inletAirTemperature - heatTransfer / (airFlowRate * cpma);

      Roots.ErrorFunction eFnc = delegate (double eTemp)
      {
        double ht, ot, oa, sd, sw, dl;
        GetHeatTransfer(eTemp, airFlowRate, nominalAirFlowRate, surfaceArea, inletAirTemperature,
          inletAirHumidityRatio, borderRelativeHumidity, out ht, out ot, out oa, out sd, out sw, out dl);
        if (deductDefrostLoad) return ht - heatTransfer - dl;
        else return ht - heatTransfer;
      };
      evaporatingTemperature = Roots.Brent(evaporatingTemperature - 10, evaporatingTemperature + 10, 0.00001, eFnc);
      double hTransfer;
      GetHeatTransfer(evaporatingTemperature, airFlowRate, nominalAirFlowRate, surfaceArea,
        inletAirTemperature, inletAirHumidityRatio, borderRelativeHumidity, 
        out hTransfer, out outletAirTemperature, out outletAirHumidityRatio, out sD, out sW, out defrostLoad);
    }

    #endregion

  }
}
