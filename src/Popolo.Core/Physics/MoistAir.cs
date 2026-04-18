/* MoistAir.cs
 *
 * Copyright (C) 2007 E.Togashi
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 3 of the License, or (at
 * your option) any later version.
 *
 * This program is distributed in the hope that it will be useful, but
 * WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
 * General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301, USA.
 */

using System;
using Popolo.Core.Exceptions;
using Popolo.Core.Numerics;

namespace Popolo.Core.Physics
{
  /// <summary>
  /// Provides thermodynamic state and psychrometric calculations for moist air.
  /// </summary>
  /// <remarks>
  /// References:
  /// - HVACSIM+(J)
  /// - Udagawa, M., "Air Conditioning Calculations with Personal Computers" (in Japanese)
  /// - ASHRAE Fundamentals Handbook 1997, Psychrometrics
  /// </remarks>
  public class MoistAir : IReadOnlyMoistAir
  {

    #region 定数

    /// <summary>Isobaric specific heat of dry air [kJ/(kg·K)].</summary>
    public const double DryAirIsobaricSpecificHeat = 1.006;

    /// <summary>Isobaric specific heat of water vapor [kJ/(kg·K)].</summary>
    public const double VaporIsobaricSpecificHeat = 1.805;

    /// <summary>Isobaric specific heat of water at 0 °C [kJ/(kg·K)].</summary>
    public const double WaterIsobaricSpecificHeat = 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat;

    /// <summary>Latent heat of vaporization of water at 0 °C [kJ/kg].</summary>
    public const double VaporizationLatentHeat = 2501.0;

    /// <summary>Gas constant of dry air [kJ/(kg·K)].</summary>
    public const double DryAirGasConstant = 0.287055;

    #endregion

    #region プロパティ

    /// <summary>
    /// Gets the isobaric specific heat of dry air [kJ/(kg·K)].
    /// </summary>
    public static double IsobaricSpecificHeatOfDryAir => DryAirIsobaricSpecificHeat;

    /// <summary>
    /// Gets the isobaric specific heat of water vapor [kJ/(kg·K)].
    /// </summary>
    public static double IsobaricSpecificHeatOfVapor => VaporIsobaricSpecificHeat;

    /// <summary>
    /// Gets the latent heat of vaporization at 0 °C [kJ/kg].
    /// </summary>
    public static double LatentHeatOfVaporization => VaporizationLatentHeat;

    /// <summary>Gets or sets the dry-bulb temperature [°C].</summary>
    public double DryBulbTemperature { get; set; }

    /// <summary>Gets or sets the wet-bulb temperature [°C].</summary>
    public double WetBulbTemperature { get; set; }

    /// <summary>Gets or sets the humidity ratio [kg/kg(DA)].</summary>
    public double HumidityRatio { get; set; }

    /// <summary>Gets or sets the relative humidity [%].</summary>
    public double RelativeHumidity { get; set; }

    /// <summary>Gets or sets the specific enthalpy [kJ/kg].</summary>
    public double Enthalpy { get; set; }

    /// <summary>Gets or sets the specific volume [m³/kg].</summary>
    public double SpecificVolume { get; set; }

    /// <summary>Gets or sets the atmospheric pressure [kPa].</summary>
    public double AtmosphericPressure { get; set; }

    #endregion

    #region コンストラクタ

    /// <summary>
    /// Initializes a new instance with default conditions (24 °C, humidity ratio 0.0093 kg/kg).
    /// </summary>
    public MoistAir() : this(24, 0.0093) { }

    /// <summary>
    /// Initializes a new instance from dry-bulb temperature and humidity ratio
    /// at standard atmospheric pressure.
    /// </summary>
    /// <param name="dryBulbTemperature">Dry-bulb temperature [°C]</param>
    /// <param name="humidityRatio">Humidity ratio [kg/kg(DA)]</param>
    public MoistAir(double dryBulbTemperature, double humidityRatio)
        : this(dryBulbTemperature, humidityRatio, PhysicsConstants.StandardAtmosphericPressure)
    { }

    /// <summary>
    /// Initializes a new instance from dry-bulb temperature, humidity ratio,
    /// and atmospheric pressure.
    /// </summary>
    /// <param name="dryBulbTemperature">Dry-bulb temperature [°C]</param>
    /// <param name="humidityRatio">Humidity ratio [kg/kg(DA)]</param>
    /// <param name="atmosphericPressure">Atmospheric pressure [kPa]</param>
    public MoistAir(double dryBulbTemperature, double humidityRatio, double atmosphericPressure)
    {
      ValidateTemperature(dryBulbTemperature, nameof(dryBulbTemperature));
      ValidateHumidityRatio(humidityRatio, nameof(humidityRatio));
      ValidateAtmosphericPressure(atmosphericPressure, nameof(atmosphericPressure));
      AtmosphericPressure = atmosphericPressure;
      DryBulbTemperature = dryBulbTemperature;
      HumidityRatio = humidityRatio;
      RelativeHumidity = GetRelativeHumidityFromDryBulbTemperatureAndHumidityRatio(
          dryBulbTemperature, humidityRatio, atmosphericPressure);
      Enthalpy = GetEnthalpyFromDryBulbTemperatureAndHumidityRatio(
          dryBulbTemperature, humidityRatio);
      WetBulbTemperature = GetWetBulbTemperatureFromDryBulbTemperatureAndHumidityRatio(
          dryBulbTemperature, humidityRatio, atmosphericPressure);
      SpecificVolume = GetSpecificVolumeFromDryBulbTemperatureAndHumidityRatio(
          dryBulbTemperature, humidityRatio, atmosphericPressure);
    }

    /// <summary>
    /// Initializes a new instance by copying from an <see cref="IReadOnlyMoistAir"/> instance.
    /// </summary>
    /// <param name="moistAir">The source moist air state to copy.</param>
    public MoistAir(IReadOnlyMoistAir moistAir)
    {
      AtmosphericPressure = moistAir.AtmosphericPressure;
      DryBulbTemperature = moistAir.DryBulbTemperature;
      HumidityRatio = moistAir.HumidityRatio;
      RelativeHumidity = moistAir.RelativeHumidity;
      Enthalpy = moistAir.Enthalpy;
      WetBulbTemperature = moistAir.WetBulbTemperature;
      SpecificVolume = moistAir.SpecificVolume;
    }

    #endregion

    #region 入力検証

    /// <summary>絶対零度 [°C]（温度の物理的下限）</summary>
    private const double AbsoluteZero = PhysicsConstants.CelsiusToKelvinOffset * -1.0;

    private static void ValidateTemperature(double temperature, string paramName)
    {
      if (temperature < AbsoluteZero)
        throw new PopoloOutOfRangeException(paramName, temperature, AbsoluteZero, null);
    }

    private static void ValidateHumidityRatio(double humidityRatio, string paramName)
    {
      if (humidityRatio < 0)
        throw new PopoloOutOfRangeException(paramName, humidityRatio, 0.0, null);
    }

    private static void ValidateRelativeHumidity(double relativeHumidity, string paramName)
    {
      if (relativeHumidity < 0 || relativeHumidity > 100)
        throw new PopoloOutOfRangeException(paramName, relativeHumidity, 0.0, 100.0);
    }

    private static void ValidateAtmosphericPressure(double atmosphericPressure, string paramName)
    {
      if (atmosphericPressure <= 0)
        throw new PopoloOutOfRangeException(paramName, atmosphericPressure, 0.0, null,
            "Atmospheric pressure must be positive.");
    }

    #endregion

    #region 水蒸気分圧・絶対湿度の変換

    /// <summary>
    /// Gets the water vapor partial pressure [kPa]
    /// from the humidity ratio [kg/kg(DA)] and atmospheric pressure [kPa].
    /// </summary>
    /// <param name="humidityRatio">Humidity ratio [kg/kg(DA)]</param>
    /// <param name="atmosphericPressure">Atmospheric pressure [kPa]</param>
    /// <returns>Water vapor partial pressure [kPa]</returns>
    public static double GetWaterVaporPartialPressureFromHumidityRatio(
        double humidityRatio, double atmosphericPressure)
    {
      ValidateHumidityRatio(humidityRatio, nameof(humidityRatio));
      ValidateAtmosphericPressure(atmosphericPressure, nameof(atmosphericPressure));
      return atmosphericPressure * humidityRatio / (0.62198 + humidityRatio);
    }

    /// <summary>
    /// Gets the humidity ratio [kg/kg(DA)]
    /// from the water vapor partial pressure [kPa] and atmospheric pressure [kPa].
    /// </summary>
    /// <param name="waterVaporPartialPressure">Water vapor partial pressure [kPa]</param>
    /// <param name="atmosphericPressure">Atmospheric pressure [kPa]</param>
    /// <returns>Humidity ratio [kg/kg(DA)]</returns>
    public static double GetHumidityRatioFromWaterVaporPartialPressure(
        double waterVaporPartialPressure, double atmosphericPressure)
    {
      ValidateAtmosphericPressure(atmosphericPressure, nameof(atmosphericPressure));
      return 0.62198 * waterVaporPartialPressure
          / (atmosphericPressure - waterVaporPartialPressure);
    }

    #endregion

    #region エンタルピーの計算

    /// <summary>
    /// Gets the specific enthalpy [kJ/kg]
    /// from the dry-bulb temperature [°C] and humidity ratio [kg/kg(DA)].
    /// </summary>
    /// <param name="dryBulbTemperature">Dry-bulb temperature [°C]</param>
    /// <param name="humidityRatio">Humidity ratio [kg/kg(DA)]</param>
    /// <returns>Specific enthalpy [kJ/kg]</returns>
    public static double GetEnthalpyFromDryBulbTemperatureAndHumidityRatio(
        double dryBulbTemperature, double humidityRatio)
    {
      ValidateTemperature(dryBulbTemperature, nameof(dryBulbTemperature));
      ValidateHumidityRatio(humidityRatio, nameof(humidityRatio));
      return DryAirIsobaricSpecificHeat * dryBulbTemperature
          + humidityRatio * (VaporIsobaricSpecificHeat * dryBulbTemperature
          + VaporizationLatentHeat);
    }

    /// <summary>
    /// Gets the humidity ratio [kg/kg(DA)]
    /// from the dry-bulb temperature [°C] and specific enthalpy [kJ/kg].
    /// </summary>
    /// <param name="dryBulbTemperature">Dry-bulb temperature [°C]</param>
    /// <param name="enthalpy">Specific enthalpy [kJ/kg]</param>
    /// <returns>Humidity ratio [kg/kg(DA)]</returns>
    public static double GetHumidityRatioFromDryBulbTemperatureAndEnthalpy(
        double dryBulbTemperature, double enthalpy)
    {
      return (enthalpy - DryAirIsobaricSpecificHeat * dryBulbTemperature)
          / (VaporIsobaricSpecificHeat * dryBulbTemperature + VaporizationLatentHeat);
    }

    /// <summary>
    /// Gets the dry-bulb temperature [°C]
    /// from the humidity ratio [kg/kg(DA)] and specific enthalpy [kJ/kg].
    /// </summary>
    /// <param name="humidityRatio">Humidity ratio [kg/kg(DA)]</param>
    /// <param name="enthalpy">Specific enthalpy [kJ/kg]</param>
    /// <returns>Dry-bulb temperature [°C]</returns>
    public static double GetDryBulbTemperatureFromHumidityRatioAndEnthalpy(
        double humidityRatio, double enthalpy)
    {
      return (enthalpy - VaporizationLatentHeat * humidityRatio)
          / (DryAirIsobaricSpecificHeat + VaporIsobaricSpecificHeat * humidityRatio);
    }

    #endregion

    #region 湿球温度の計算

    /// <summary>
    /// Gets the humidity ratio [kg/kg(DA)]
    /// from the dry-bulb temperature [°C], wet-bulb temperature [°C],
    /// and atmospheric pressure [kPa].
    /// </summary>
    /// <param name="dryBulbTemperature">Dry-bulb temperature [°C]</param>
    /// <param name="wetBulbTemperature">Wet-bulb temperature [°C]</param>
    /// <param name="atmosphericPressure">Atmospheric pressure [kPa]</param>
    /// <returns>Humidity ratio [kg/kg(DA)]</returns>
    public static double GetHumidityRatioFromDryBulbTemperatureAndWetBulbTemperature(
        double dryBulbTemperature, double wetBulbTemperature, double atmosphericPressure)
    {
      double ps = Water.GetSaturationPressure(wetBulbTemperature);
      double ws = GetHumidityRatioFromWaterVaporPartialPressure(ps, atmosphericPressure);
      double a = ws * (VaporizationLatentHeat
          + (VaporIsobaricSpecificHeat - WaterIsobaricSpecificHeat) * wetBulbTemperature)
          + DryAirIsobaricSpecificHeat * (wetBulbTemperature - dryBulbTemperature);
      double b = VaporizationLatentHeat + VaporIsobaricSpecificHeat * dryBulbTemperature
          - WaterIsobaricSpecificHeat * wetBulbTemperature;
      return a / b;
    }

    /// <summary>
    /// Gets the dry-bulb temperature [°C]
    /// from the humidity ratio [kg/kg(DA)], wet-bulb temperature [°C],
    /// and atmospheric pressure [kPa].
    /// </summary>
    /// <param name="humidityRatio">Humidity ratio [kg/kg(DA)]</param>
    /// <param name="wetBulbTemperature">Wet-bulb temperature [°C]</param>
    /// <param name="atmosphericPressure">Atmospheric pressure [kPa]</param>
    /// <returns>Dry-bulb temperature [°C]</returns>
    public static double GetDryBulbTemperatureFromHumidityRatioAndWetBulbTemperature(
        double humidityRatio, double wetBulbTemperature, double atmosphericPressure)
    {
      double ps = Water.GetSaturationPressure(wetBulbTemperature);
      double ws = GetHumidityRatioFromWaterVaporPartialPressure(ps, atmosphericPressure);
      double a = ws * (VaporizationLatentHeat
          + (VaporIsobaricSpecificHeat - WaterIsobaricSpecificHeat) * wetBulbTemperature);
      a += DryAirIsobaricSpecificHeat * wetBulbTemperature;
      a += (WaterIsobaricSpecificHeat * wetBulbTemperature - VaporizationLatentHeat) * humidityRatio;
      double b = VaporIsobaricSpecificHeat * humidityRatio + DryAirIsobaricSpecificHeat;
      return a / b;
    }

    /// <summary>
    /// Gets the wet-bulb temperature [°C]
    /// from the dry-bulb temperature [°C], humidity ratio [kg/kg(DA)],
    /// and atmospheric pressure [kPa].
    /// </summary>
    /// <param name="dryBulbTemperature">Dry-bulb temperature [°C]</param>
    /// <param name="humidityRatio">Humidity ratio [kg/kg(DA)]</param>
    /// <param name="atmosphericPressure">Atmospheric pressure [kPa]</param>
    /// <returns>Wet-bulb temperature [°C]</returns>
    public static double GetWetBulbTemperatureFromDryBulbTemperatureAndHumidityRatio(
        double dryBulbTemperature, double humidityRatio, double atmosphericPressure)
    {
      Roots.ErrorFunction eFnc = wbt =>
          humidityRatio - GetHumidityRatioFromDryBulbTemperatureAndWetBulbTemperature(
              dryBulbTemperature, wbt, atmosphericPressure);
      return Roots.Newton(eFnc, dryBulbTemperature, 1e-5, 1e-7, 1e-4, 20);
    }

    #endregion

    #region エンタルピーと湿球温度の変換

    /// <summary>
    /// Gets the dry-bulb temperature [°C]
    /// from the wet-bulb temperature [°C], specific enthalpy [kJ/kg],
    /// and atmospheric pressure [kPa].
    /// </summary>
    /// <param name="wetBulbTemperature">Wet-bulb temperature [°C]</param>
    /// <param name="enthalpy">Specific enthalpy [kJ/kg]</param>
    /// <param name="atmosphericPressure">Atmospheric pressure [kPa]</param>
    /// <returns>Dry-bulb temperature [°C]</returns>
    public static double GetDryBulbTemperatureFromWetBulbTemperatureAndEnthalpy(
        double wetBulbTemperature, double enthalpy, double atmosphericPressure)
    {
      Roots.ErrorFunction eFnc = dbt =>
          GetHumidityRatioFromDryBulbTemperatureAndEnthalpy(dbt, enthalpy)
          - GetHumidityRatioFromDryBulbTemperatureAndWetBulbTemperature(
              dbt, wetBulbTemperature, atmosphericPressure);
      return Roots.Newton(eFnc, wetBulbTemperature, 1e-5, 1e-7, 1e-4, 20);
    }

    /// <summary>
    /// Gets the humidity ratio [kg/kg(DA)]
    /// from the wet-bulb temperature [°C], specific enthalpy [kJ/kg],
    /// and atmospheric pressure [kPa].
    /// </summary>
    /// <param name="wetBulbTemperature">Wet-bulb temperature [°C]</param>
    /// <param name="enthalpy">Specific enthalpy [kJ/kg]</param>
    /// <param name="atmosphericPressure">Atmospheric pressure [kPa]</param>
    /// <returns>Humidity ratio [kg/kg(DA)]</returns>
    public static double GetHumidityRatioFromWetBulbTemperatureAndEnthalpy(
        double wetBulbTemperature, double enthalpy, double atmosphericPressure)
    {
      double dbt = GetDryBulbTemperatureFromWetBulbTemperatureAndEnthalpy(
          wetBulbTemperature, enthalpy, atmosphericPressure);
      return GetHumidityRatioFromDryBulbTemperatureAndEnthalpy(dbt, enthalpy);
    }

    /// <summary>
    /// Gets the wet-bulb temperature [°C]
    /// from the dry-bulb temperature [°C], specific enthalpy [kJ/kg],
    /// and atmospheric pressure [kPa].
    /// </summary>
    /// <param name="dryBulbTemperature">Dry-bulb temperature [°C]</param>
    /// <param name="enthalpy">Specific enthalpy [kJ/kg]</param>
    /// <param name="atmosphericPressure">Atmospheric pressure [kPa]</param>
    /// <returns>Wet-bulb temperature [°C]</returns>
    public static double GetWetBulbTemperatureFromDryBulbTemperatureAndEnthalpy(
        double dryBulbTemperature, double enthalpy, double atmosphericPressure)
    {
      double hrt = GetHumidityRatioFromDryBulbTemperatureAndEnthalpy(dryBulbTemperature, enthalpy);
      return GetWetBulbTemperatureFromDryBulbTemperatureAndHumidityRatio(
          dryBulbTemperature, hrt, atmosphericPressure);
    }

    /// <summary>
    /// Gets the wet-bulb temperature [°C]
    /// from the humidity ratio [kg/kg(DA)], specific enthalpy [kJ/kg],
    /// and atmospheric pressure [kPa].
    /// </summary>
    /// <param name="humidityRatio">Humidity ratio [kg/kg(DA)]</param>
    /// <param name="enthalpy">Specific enthalpy [kJ/kg]</param>
    /// <param name="atmosphericPressure">Atmospheric pressure [kPa]</param>
    /// <returns>Wet-bulb temperature [°C]</returns>
    public static double GetWetBulbTemperatureFromHumidityRatioAndEnthalpy(
        double humidityRatio, double enthalpy, double atmosphericPressure)
    {
      double dbt = GetDryBulbTemperatureFromHumidityRatioAndEnthalpy(humidityRatio, enthalpy);
      return GetWetBulbTemperatureFromDryBulbTemperatureAndHumidityRatio(
          dbt, humidityRatio, atmosphericPressure);
    }

    /// <summary>
    /// Gets the specific enthalpy [kJ/kg]
    /// from the dry-bulb temperature [°C], wet-bulb temperature [°C],
    /// and atmospheric pressure [kPa].
    /// </summary>
    /// <param name="dryBulbTemperature">Dry-bulb temperature [°C]</param>
    /// <param name="wetBulbTemperature">Wet-bulb temperature [°C]</param>
    /// <param name="atmosphericPressure">Atmospheric pressure [kPa]</param>
    /// <returns>Specific enthalpy [kJ/kg]</returns>
    public static double GetEnthalpyFromDryBulbTemperatureAndWetBulbTemperature(
        double dryBulbTemperature, double wetBulbTemperature, double atmosphericPressure)
    {
      double hrt = GetHumidityRatioFromDryBulbTemperatureAndWetBulbTemperature(
          dryBulbTemperature, wetBulbTemperature, atmosphericPressure);
      return GetEnthalpyFromDryBulbTemperatureAndHumidityRatio(dryBulbTemperature, hrt);
    }

    /// <summary>
    /// Gets the specific enthalpy [kJ/kg]
    /// from the humidity ratio [kg/kg(DA)], wet-bulb temperature [°C],
    /// and atmospheric pressure [kPa].
    /// </summary>
    /// <param name="humidityRatio">Humidity ratio [kg/kg(DA)]</param>
    /// <param name="wetBulbTemperature">Wet-bulb temperature [°C]</param>
    /// <param name="atmosphericPressure">Atmospheric pressure [kPa]</param>
    /// <returns>Specific enthalpy [kJ/kg]</returns>
    public static double GetEnthalpyFromHumidityRatioAndWetBulbTemperature(
        double humidityRatio, double wetBulbTemperature, double atmosphericPressure)
    {
      double dbt = GetDryBulbTemperatureFromHumidityRatioAndWetBulbTemperature(
          humidityRatio, wetBulbTemperature, atmosphericPressure);
      return GetEnthalpyFromDryBulbTemperatureAndHumidityRatio(dbt, humidityRatio);
    }

    #endregion

    #region 相対湿度の計算

    /// <summary>
    /// Gets the humidity ratio [kg/kg(DA)]
    /// from the dry-bulb temperature [°C], relative humidity [%],
    /// and atmospheric pressure [kPa].
    /// </summary>
    /// <param name="dryBulbTemperature">Dry-bulb temperature [°C]</param>
    /// <param name="relativeHumidity">Relative humidity [%]</param>
    /// <param name="atmosphericPressure">Atmospheric pressure [kPa]</param>
    /// <returns>Humidity ratio [kg/kg(DA)]</returns>
    public static double GetHumidityRatioFromDryBulbTemperatureAndRelativeHumidity(
        double dryBulbTemperature, double relativeHumidity, double atmosphericPressure)
    {
      ValidateTemperature(dryBulbTemperature, nameof(dryBulbTemperature));
      ValidateRelativeHumidity(relativeHumidity, nameof(relativeHumidity));
      ValidateAtmosphericPressure(atmosphericPressure, nameof(atmosphericPressure));
      //飽和水蒸気分圧を計算し、相対湿度から実際の水蒸気分圧を求める
      double ps = Water.GetSaturationPressure(dryBulbTemperature);
      double pw = 0.01 * relativeHumidity * ps;
      return GetHumidityRatioFromWaterVaporPartialPressure(pw, atmosphericPressure);
    }

    /// <summary>
    /// Gets the relative humidity [%]
    /// from the dry-bulb temperature [°C], humidity ratio [kg/kg(DA)],
    /// and atmospheric pressure [kPa].
    /// </summary>
    /// <param name="dryBulbTemperature">Dry-bulb temperature [°C]</param>
    /// <param name="humidityRatio">Humidity ratio [kg/kg(DA)]</param>
    /// <param name="atmosphericPressure">Atmospheric pressure [kPa]</param>
    /// <returns>Relative humidity [%]</returns>
    public static double GetRelativeHumidityFromDryBulbTemperatureAndHumidityRatio(
        double dryBulbTemperature, double humidityRatio, double atmosphericPressure)
    {
      ValidateTemperature(dryBulbTemperature, nameof(dryBulbTemperature));
      ValidateHumidityRatio(humidityRatio, nameof(humidityRatio));
      ValidateAtmosphericPressure(atmosphericPressure, nameof(atmosphericPressure));
      double pw = GetWaterVaporPartialPressureFromHumidityRatio(humidityRatio, atmosphericPressure);
      double ps = Water.GetSaturationPressure(dryBulbTemperature);
      if (ps <= 0.0) return 0.0;
      return 100.0 * pw / ps;
    }

    /// <summary>
    /// Gets the dry-bulb temperature [°C]
    /// from the humidity ratio [kg/kg(DA)], relative humidity [%],
    /// and atmospheric pressure [kPa].
    /// </summary>
    /// <param name="humidityRatio">Humidity ratio [kg/kg(DA)]</param>
    /// <param name="relativeHumidity">Relative humidity [%]</param>
    /// <param name="atmosphericPressure">Atmospheric pressure [kPa]</param>
    /// <returns>Dry-bulb temperature [°C]</returns>
    public static double GetDryBulbTemperatureFromHumidityRatioAndRelativeHumidity(
        double humidityRatio, double relativeHumidity, double atmosphericPressure)
    {
      double ps = GetWaterVaporPartialPressureFromHumidityRatio(humidityRatio, atmosphericPressure);
      return Water.GetSaturationTemperature(ps / relativeHumidity * 100);
    }

    /// <summary>
    /// Gets the relative humidity [%]
    /// from the dry-bulb temperature [°C], wet-bulb temperature [°C],
    /// and atmospheric pressure [kPa].
    /// </summary>
    /// <param name="dryBulbTemperature">Dry-bulb temperature [°C]</param>
    /// <param name="wetBulbTemperature">Wet-bulb temperature [°C]</param>
    /// <param name="atmosphericPressure">Atmospheric pressure [kPa]</param>
    /// <returns>Relative humidity [%]</returns>
    public static double GetRelativeHumidityFromDryBulbTemperatureAndWetBulbTemperature(
        double dryBulbTemperature, double wetBulbTemperature, double atmosphericPressure)
    {
      double hrt = GetHumidityRatioFromDryBulbTemperatureAndWetBulbTemperature(
          dryBulbTemperature, wetBulbTemperature, atmosphericPressure);
      return GetRelativeHumidityFromDryBulbTemperatureAndHumidityRatio(
          dryBulbTemperature, hrt, atmosphericPressure);
    }

    /// <summary>
    /// Gets the relative humidity [%]
    /// from the humidity ratio [kg/kg(DA)], wet-bulb temperature [°C],
    /// and atmospheric pressure [kPa].
    /// </summary>
    /// <param name="humidityRatio">Humidity ratio [kg/kg(DA)]</param>
    /// <param name="wetBulbTemperature">Wet-bulb temperature [°C]</param>
    /// <param name="atmosphericPressure">Atmospheric pressure [kPa]</param>
    /// <returns>Relative humidity [%]</returns>
    public static double GetRelativeHumidityFromWetBulbTemperatureAndHumidityRatio(
        double humidityRatio, double wetBulbTemperature, double atmosphericPressure)
    {
      double dbt = GetDryBulbTemperatureFromHumidityRatioAndWetBulbTemperature(
          humidityRatio, wetBulbTemperature, atmosphericPressure);
      return GetRelativeHumidityFromDryBulbTemperatureAndWetBulbTemperature(
          dbt, wetBulbTemperature, atmosphericPressure);
    }

    /// <summary>
    /// Gets the relative humidity [%]
    /// from the dry-bulb temperature [°C], specific enthalpy [kJ/kg],
    /// and atmospheric pressure [kPa].
    /// </summary>
    /// <param name="dryBulbTemperature">Dry-bulb temperature [°C]</param>
    /// <param name="enthalpy">Specific enthalpy [kJ/kg]</param>
    /// <param name="atmosphericPressure">Atmospheric pressure [kPa]</param>
    /// <returns>Relative humidity [%]</returns>
    public static double GetRelativeHumidityFromDryBulbTemperatureAndEnthalpy(
        double dryBulbTemperature, double enthalpy, double atmosphericPressure)
    {
      double hrt = GetHumidityRatioFromDryBulbTemperatureAndEnthalpy(dryBulbTemperature, enthalpy);
      return GetRelativeHumidityFromDryBulbTemperatureAndHumidityRatio(
          dryBulbTemperature, hrt, atmosphericPressure);
    }

    /// <summary>
    /// Gets the relative humidity [%]
    /// from the humidity ratio [kg/kg(DA)], specific enthalpy [kJ/kg],
    /// and atmospheric pressure [kPa].
    /// </summary>
    /// <param name="humidityRatio">Humidity ratio [kg/kg(DA)]</param>
    /// <param name="enthalpy">Specific enthalpy [kJ/kg]</param>
    /// <param name="atmosphericPressure">Atmospheric pressure [kPa]</param>
    /// <returns>Relative humidity [%]</returns>
    public static double GetRelativeHumidityFromHumidityRatioAndEnthalpy(
        double humidityRatio, double enthalpy, double atmosphericPressure)
    {
      double dbt = GetDryBulbTemperatureFromHumidityRatioAndEnthalpy(humidityRatio, enthalpy);
      return GetRelativeHumidityFromDryBulbTemperatureAndHumidityRatio(
          dbt, humidityRatio, atmosphericPressure);
    }

    /// <summary>
    /// Gets the wet-bulb temperature [°C]
    /// from the dry-bulb temperature [°C], relative humidity [%],
    /// and atmospheric pressure [kPa].
    /// </summary>
    /// <param name="dryBulbTemperature">Dry-bulb temperature [°C]</param>
    /// <param name="relativeHumidity">Relative humidity [%]</param>
    /// <param name="atmosphericPressure">Atmospheric pressure [kPa]</param>
    /// <returns>Wet-bulb temperature [°C]</returns>
    public static double GetWetBulbTemperatureFromDryBulbTemperatureAndRelativeHumidity(
        double dryBulbTemperature, double relativeHumidity, double atmosphericPressure)
    {
      double hrt = GetHumidityRatioFromDryBulbTemperatureAndRelativeHumidity(
          dryBulbTemperature, relativeHumidity, atmosphericPressure);
      return GetWetBulbTemperatureFromDryBulbTemperatureAndHumidityRatio(
          dryBulbTemperature, hrt, atmosphericPressure);
    }

    /// <summary>
    /// Gets the wet-bulb temperature [°C]
    /// from the humidity ratio [kg/kg(DA)], relative humidity [%],
    /// and atmospheric pressure [kPa].
    /// </summary>
    /// <param name="humidityRatio">Humidity ratio [kg/kg(DA)]</param>
    /// <param name="relativeHumidity">Relative humidity [%]</param>
    /// <param name="atmosphericPressure">Atmospheric pressure [kPa]</param>
    /// <returns>Wet-bulb temperature [°C]</returns>
    public static double GetWetBulbTemperatureFromHumidityRatioAndRelativeHumidity(
        double humidityRatio, double relativeHumidity, double atmosphericPressure)
    {
      double dbt = GetDryBulbTemperatureFromHumidityRatioAndRelativeHumidity(
          humidityRatio, relativeHumidity, atmosphericPressure);
      return GetWetBulbTemperatureFromDryBulbTemperatureAndHumidityRatio(
          dbt, humidityRatio, atmosphericPressure);
    }

    /// <summary>
    /// Gets the specific enthalpy [kJ/kg]
    /// from the dry-bulb temperature [°C], relative humidity [%],
    /// and atmospheric pressure [kPa].
    /// </summary>
    /// <param name="dryBulbTemperature">Dry-bulb temperature [°C]</param>
    /// <param name="relativeHumidity">Relative humidity [%]</param>
    /// <param name="atmosphericPressure">Atmospheric pressure [kPa]</param>
    /// <returns>Specific enthalpy [kJ/kg]</returns>
    public static double GetEnthalpyFromDryBulbTemperatureAndRelativeHumidity(
        double dryBulbTemperature, double relativeHumidity, double atmosphericPressure)
    {
      double hrt = GetHumidityRatioFromDryBulbTemperatureAndRelativeHumidity(
          dryBulbTemperature, relativeHumidity, atmosphericPressure);
      return GetEnthalpyFromDryBulbTemperatureAndHumidityRatio(dryBulbTemperature, hrt);
    }

    /// <summary>
    /// Gets the specific enthalpy [kJ/kg]
    /// from the humidity ratio [kg/kg(DA)], relative humidity [%],
    /// and atmospheric pressure [kPa].
    /// </summary>
    /// <param name="humidityRatio">Humidity ratio [kg/kg(DA)]</param>
    /// <param name="relativeHumidity">Relative humidity [%]</param>
    /// <param name="atmosphericPressure">Atmospheric pressure [kPa]</param>
    /// <returns>Specific enthalpy [kJ/kg]</returns>
    public static double GetEnthalpyFromHumidityRatioAndRelativeHumidity(
        double humidityRatio, double relativeHumidity, double atmosphericPressure)
    {
      double dbt = GetDryBulbTemperatureFromHumidityRatioAndRelativeHumidity(
          humidityRatio, relativeHumidity, atmosphericPressure);
      return GetEnthalpyFromDryBulbTemperatureAndHumidityRatio(dbt, humidityRatio);
    }

    /// <summary>
    /// Gets the dry-bulb temperature [°C]
    /// from the wet-bulb temperature [°C], relative humidity [%],
    /// and atmospheric pressure [kPa].
    /// </summary>
    /// <param name="wetBulbTemperature">Wet-bulb temperature [°C]</param>
    /// <param name="relativeHumidity">Relative humidity [%]</param>
    /// <param name="atmosphericPressure">Atmospheric pressure [kPa]</param>
    /// <returns>Dry-bulb temperature [°C]</returns>
    public static double GetDryBulbTemperatureFromWetBulbTemperatureAndRelativeHumidity(
        double wetBulbTemperature, double relativeHumidity, double atmosphericPressure)
    {
      Roots.ErrorFunction eFnc = dbt =>
      {
        double hrt = GetHumidityRatioFromDryBulbTemperatureAndRelativeHumidity(
            dbt, relativeHumidity, atmosphericPressure);
        return dbt - GetDryBulbTemperatureFromHumidityRatioAndWetBulbTemperature(
            hrt, wetBulbTemperature, atmosphericPressure);
      };
      return Roots.Newton(eFnc, wetBulbTemperature, 1e-5, 1e-4, 1e-4, 20);
    }

    /// <summary>
    /// Gets the humidity ratio [kg/kg(DA)]
    /// from the wet-bulb temperature [°C], relative humidity [%],
    /// and atmospheric pressure [kPa].
    /// </summary>
    /// <param name="wetBulbTemperature">Wet-bulb temperature [°C]</param>
    /// <param name="relativeHumidity">Relative humidity [%]</param>
    /// <param name="atmosphericPressure">Atmospheric pressure [kPa]</param>
    /// <returns>Humidity ratio [kg/kg(DA)]</returns>
    public static double GetHumidityRatioFromWetBulbTemperatureAndRelativeHumidity(
        double wetBulbTemperature, double relativeHumidity, double atmosphericPressure)
    {
      double dbt = GetDryBulbTemperatureFromWetBulbTemperatureAndRelativeHumidity(
          wetBulbTemperature, relativeHumidity, atmosphericPressure);
      return GetHumidityRatioFromDryBulbTemperatureAndRelativeHumidity(
          dbt, relativeHumidity, atmosphericPressure);
    }

    /// <summary>
    /// Gets the specific enthalpy [kJ/kg]
    /// from the wet-bulb temperature [°C], relative humidity [%],
    /// and atmospheric pressure [kPa].
    /// </summary>
    /// <param name="wetBulbTemperature">Wet-bulb temperature [°C]</param>
    /// <param name="relativeHumidity">Relative humidity [%]</param>
    /// <param name="atmosphericPressure">Atmospheric pressure [kPa]</param>
    /// <returns>Specific enthalpy [kJ/kg]</returns>
    public static double GetEnthalpyFromWetBulbTemperatureAndRelativeHumidity(
        double wetBulbTemperature, double relativeHumidity, double atmosphericPressure)
    {
      double dbt = GetDryBulbTemperatureFromWetBulbTemperatureAndRelativeHumidity(
          wetBulbTemperature, relativeHumidity, atmosphericPressure);
      return GetEnthalpyFromDryBulbTemperatureAndRelativeHumidity(
          dbt, relativeHumidity, atmosphericPressure);
    }

    /// <summary>
    /// Gets the dry-bulb temperature [°C]
    /// from the specific enthalpy [kJ/kg], relative humidity [%],
    /// and atmospheric pressure [kPa].
    /// </summary>
    /// <param name="enthalpy">Specific enthalpy [kJ/kg]</param>
    /// <param name="relativeHumidity">Relative humidity [%]</param>
    /// <param name="atmosphericPressure">Atmospheric pressure [kPa]</param>
    /// <returns>Dry-bulb temperature [°C]</returns>
    public static double GetDryBulbTemperatureFromEnthalpyAndRelativeHumidity(
        double enthalpy, double relativeHumidity, double atmosphericPressure)
    {
      Roots.ErrorFunction eFnc = dbt =>
          enthalpy - GetEnthalpyFromDryBulbTemperatureAndRelativeHumidity(
              dbt, relativeHumidity, atmosphericPressure);
      return Roots.Newton(eFnc, 25, 1e-5, 1e-4, 1e-4, 20);
    }

    /// <summary>
    /// Gets the humidity ratio [kg/kg(DA)]
    /// from the specific enthalpy [kJ/kg], relative humidity [%],
    /// and atmospheric pressure [kPa].
    /// </summary>
    /// <param name="enthalpy">Specific enthalpy [kJ/kg]</param>
    /// <param name="relativeHumidity">Relative humidity [%]</param>
    /// <param name="atmosphericPressure">Atmospheric pressure [kPa]</param>
    /// <returns>Humidity ratio [kg/kg(DA)]</returns>
    public static double GetHumidityRatioFromEnthalpyAndRelativeHumidity(
        double enthalpy, double relativeHumidity, double atmosphericPressure)
    {
      double dbt = GetDryBulbTemperatureFromEnthalpyAndRelativeHumidity(
          enthalpy, relativeHumidity, atmosphericPressure);
      return GetHumidityRatioFromDryBulbTemperatureAndRelativeHumidity(
          dbt, relativeHumidity, atmosphericPressure);
    }

    /// <summary>
    /// Gets the wet-bulb temperature [°C]
    /// from the specific enthalpy [kJ/kg], relative humidity [%],
    /// and atmospheric pressure [kPa].
    /// </summary>
    /// <param name="enthalpy">Specific enthalpy [kJ/kg]</param>
    /// <param name="relativeHumidity">Relative humidity [%]</param>
    /// <param name="atmosphericPressure">Atmospheric pressure [kPa]</param>
    /// <returns>Wet-bulb temperature [°C]</returns>
    public static double GetWetBulbTemperatureFromEnthalpyAndRelativeHumidity(
        double enthalpy, double relativeHumidity, double atmosphericPressure)
    {
      double dbt = GetDryBulbTemperatureFromEnthalpyAndRelativeHumidity(
          enthalpy, relativeHumidity, atmosphericPressure);
      return GetWetBulbTemperatureFromDryBulbTemperatureAndRelativeHumidity(
          dbt, relativeHumidity, atmosphericPressure);
    }

    #endregion

    #region 比体積の計算

    /// <summary>
    /// Gets the dry-bulb temperature [°C]
    /// from the relative humidity [%], specific volume [m³/kg],
    /// and atmospheric pressure [kPa].
    /// </summary>
    /// <param name="relativeHumidity">Relative humidity [%]</param>
    /// <param name="specificVolume">Specific volume [m³/kg]</param>
    /// <param name="atmosphericPressure">Atmospheric pressure [kPa]</param>
    /// <returns>Dry-bulb temperature [°C]</returns>
    /// <exception cref="PopoloNumericalException">
    /// Thrown when the iterative calculation fails to converge.
    /// </exception>
    public static double GetDryBulbTemperatureFromRelativeHumidityAndSpecificVolume(
        double relativeHumidity, double specificVolume, double atmosphericPressure)
    {
      const double DELTA = 1.0e-10;
      const double TOL = 1.0e-9;
      int iterNum = 0;
      double dbt = 25;
      while (true)
      {
        double err1 = GetHumidityRatioFromDryBulbTemperatureAndRelativeHumidity(
            dbt, relativeHumidity, atmosphericPressure)
            - GetHumidityRatioFromDryBulbTemperatureAndSpecificVolume(
            dbt, specificVolume, atmosphericPressure);
        if (Math.Abs(err1) < TOL) break;
        double err2 = GetHumidityRatioFromDryBulbTemperatureAndRelativeHumidity(
            dbt + DELTA, relativeHumidity, atmosphericPressure)
            - GetHumidityRatioFromDryBulbTemperatureAndSpecificVolume(
            dbt + DELTA, specificVolume, atmosphericPressure);
        dbt -= err1 / ((err2 - err1) / DELTA);
        if (20 < iterNum++)
          throw new PopoloNumericalException(
              "GetDryBulbTemperatureFromRelativeHumidityAndSpecificVolume",
              $"Convergence failed after {iterNum} iterations. Last dbt={dbt} °C.");
      }
      return dbt;
    }

    /// <summary>
    /// Gets the dry-bulb temperature [°C]
    /// from the wet-bulb temperature [°C], specific volume [m³/kg],
    /// and atmospheric pressure [kPa].
    /// </summary>
    /// <param name="wetBulbTemperature">Wet-bulb temperature [°C]</param>
    /// <param name="specificVolume">Specific volume [m³/kg]</param>
    /// <param name="atmosphericPressure">Atmospheric pressure [kPa]</param>
    /// <returns>Dry-bulb temperature [°C]</returns>
    /// <exception cref="PopoloNumericalException">
    /// Thrown when the iterative calculation fails to converge.
    /// </exception>
    public static double GetDryBulbTemperatureFromWetBulbTemperatureAndSpecificVolume(
        double wetBulbTemperature, double specificVolume, double atmosphericPressure)
    {
      const double DELTA = 1.0e-10;
      const double TOL = 1.0e-9;
      int iterNum = 0;
      double dbt = 25;
      while (true)
      {
        double err1 = GetHumidityRatioFromDryBulbTemperatureAndWetBulbTemperature(
            dbt, wetBulbTemperature, atmosphericPressure)
            - GetHumidityRatioFromDryBulbTemperatureAndSpecificVolume(
            dbt, specificVolume, atmosphericPressure);
        if (Math.Abs(err1) < TOL) break;
        double err2 = GetHumidityRatioFromDryBulbTemperatureAndWetBulbTemperature(
            dbt + DELTA, wetBulbTemperature, atmosphericPressure)
            - GetHumidityRatioFromDryBulbTemperatureAndSpecificVolume(
            dbt + DELTA, specificVolume, atmosphericPressure);
        dbt -= err1 / ((err2 - err1) / DELTA);
        if (20 < iterNum++)
          throw new PopoloNumericalException(
              "GetDryBulbTemperatureFromWetBulbTemperatureAndSpecificVolume",
              $"Convergence failed after {iterNum} iterations. Last dbt={dbt} °C.");
      }
      return dbt;
    }

    /// <summary>
    /// Gets the specific volume [m³/kg]
    /// from the dry-bulb temperature [°C], humidity ratio [kg/kg(DA)],
    /// and atmospheric pressure [kPa].
    /// </summary>
    /// <param name="dryBulbTemperature">Dry-bulb temperature [°C]</param>
    /// <param name="humidityRatio">Humidity ratio [kg/kg(DA)]</param>
    /// <param name="atmosphericPressure">Atmospheric pressure [kPa]</param>
    /// <returns>Specific volume [m³/kg]</returns>
    public static double GetSpecificVolumeFromDryBulbTemperatureAndHumidityRatio(
        double dryBulbTemperature, double humidityRatio, double atmosphericPressure)
    {
      ValidateTemperature(dryBulbTemperature, nameof(dryBulbTemperature));
      ValidateHumidityRatio(humidityRatio, nameof(humidityRatio));
      ValidateAtmosphericPressure(atmosphericPressure, nameof(atmosphericPressure));
      return PhysicsConstants.ToKelvin(dryBulbTemperature)
          * DryAirGasConstant / atmosphericPressure * (1.0 + 1.6078 * humidityRatio);
    }

    /// <summary>
    /// Gets the dry-bulb temperature [°C]
    /// from the specific volume [m³/kg], humidity ratio [kg/kg(DA)],
    /// and atmospheric pressure [kPa].
    /// </summary>
    /// <param name="specificVolume">Specific volume [m³/kg]</param>
    /// <param name="humidityRatio">Humidity ratio [kg/kg(DA)]</param>
    /// <param name="atmosphericPressure">Atmospheric pressure [kPa]</param>
    /// <returns>Dry-bulb temperature [°C]</returns>
    public static double GetDryBulbTemperatureFromSpecificVolumeAndHumidityRatio(
        double specificVolume, double humidityRatio, double atmosphericPressure)
    {
      return PhysicsConstants.ToCelsius(
          specificVolume / (1.0 + 1.6078 * humidityRatio)
          * atmosphericPressure / DryAirGasConstant);
    }

    /// <summary>
    /// Gets the humidity ratio [kg/kg(DA)]
    /// from the dry-bulb temperature [°C], specific volume [m³/kg],
    /// and atmospheric pressure [kPa].
    /// </summary>
    /// <param name="dryBulbTemperature">Dry-bulb temperature [°C]</param>
    /// <param name="specificVolume">Specific volume [m³/kg]</param>
    /// <param name="atmosphericPressure">Atmospheric pressure [kPa]</param>
    /// <returns>Humidity ratio [kg/kg(DA)]</returns>
    public static double GetHumidityRatioFromDryBulbTemperatureAndSpecificVolume(
        double dryBulbTemperature, double specificVolume, double atmosphericPressure)
    {
      return (specificVolume / PhysicsConstants.ToKelvin(dryBulbTemperature)
          / DryAirGasConstant * atmosphericPressure - 1.0) / 1.6078;
    }

    #endregion

    #region 飽和状態の計算

    /// <summary>
    /// Gets the dew point temperature [°C] (saturation dry-bulb temperature)
    /// from the humidity ratio [kg/kg(DA)] and atmospheric pressure [kPa].
    /// </summary>
    /// <param name="humidityRatio">Humidity ratio [kg/kg(DA)]</param>
    /// <param name="atmosphericPressure">Atmospheric pressure [kPa]</param>
    /// <returns>Dew point temperature [°C]</returns>
    public static double GetSaturationDryBulbTemperatureFromHumidityRatio(
        double humidityRatio, double atmosphericPressure)
    {
      double ps = GetWaterVaporPartialPressureFromHumidityRatio(humidityRatio, atmosphericPressure);
      return Water.GetSaturationTemperature(ps);
    }

    /// <summary>
    /// Gets the dew point temperature [°C] (saturation dry-bulb temperature)
    /// from the specific enthalpy [kJ/kg] and atmospheric pressure [kPa].
    /// </summary>
    /// <param name="enthalpy">Specific enthalpy [kJ/kg]</param>
    /// <param name="atmosphericPressure">Atmospheric pressure [kPa]</param>
    /// <returns>Dew point temperature [°C]</returns>
    public static double GetSaturationDryBulbTemperatureFromEnthalpy(
        double enthalpy, double atmosphericPressure)
    {
      return GetDryBulbTemperatureFromEnthalpyAndRelativeHumidity(
          enthalpy, 100, atmosphericPressure);
    }

    /// <summary>
    /// Gets the saturation humidity ratio [kg/kg(DA)]
    /// from the dry-bulb temperature [°C] and atmospheric pressure [kPa].
    /// </summary>
    /// <param name="dryBulbTemperature">Dry-bulb temperature [°C]</param>
    /// <param name="atmosphericPressure">Atmospheric pressure [kPa]</param>
    /// <returns>Saturation humidity ratio [kg/kg(DA)]</returns>
    public static double GetSaturationHumidityRatioFromDryBulbTemperature(
        double dryBulbTemperature, double atmosphericPressure)
    {
      double ps = Water.GetSaturationPressure(dryBulbTemperature);
      return GetHumidityRatioFromWaterVaporPartialPressure(ps, atmosphericPressure);
    }

    /// <summary>
    /// Gets the saturation humidity ratio [kg/kg(DA)]
    /// from the specific enthalpy [kJ/kg] and atmospheric pressure [kPa].
    /// </summary>
    /// <param name="enthalpy">Specific enthalpy [kJ/kg]</param>
    /// <param name="atmosphericPressure">Atmospheric pressure [kPa]</param>
    /// <returns>Saturation humidity ratio [kg/kg(DA)]</returns>
    public static double GetSaturationHumidityRatioFromEnthalpy(
        double enthalpy, double atmosphericPressure)
    {
      return GetHumidityRatioFromEnthalpyAndRelativeHumidity(enthalpy, 100, atmosphericPressure);
    }

    /// <summary>
    /// Gets the saturation specific enthalpy [kJ/kg]
    /// from the dry-bulb temperature [°C] and atmospheric pressure [kPa].
    /// </summary>
    /// <param name="dryBulbTemperature">Dry-bulb temperature [°C]</param>
    /// <param name="atmosphericPressure">Atmospheric pressure [kPa]</param>
    /// <returns>Saturation specific enthalpy [kJ/kg]</returns>
    public static double GetSaturationEnthalpyFromDryBulbTemperature(
        double dryBulbTemperature, double atmosphericPressure)
    {
      double ps = Water.GetSaturationPressure(dryBulbTemperature);
      double ws = GetHumidityRatioFromWaterVaporPartialPressure(ps, atmosphericPressure);
      return GetEnthalpyFromDryBulbTemperatureAndHumidityRatio(dryBulbTemperature, ws);
    }

    /// <summary>
    /// Gets the saturation specific enthalpy [kJ/kg]
    /// from the humidity ratio [kg/kg(DA)] and atmospheric pressure [kPa].
    /// </summary>
    /// <param name="humidityRatio">Humidity ratio [kg/kg(DA)]</param>
    /// <param name="atmosphericPressure">Atmospheric pressure [kPa]</param>
    /// <returns>Saturation specific enthalpy [kJ/kg]</returns>
    public static double GetSaturationEnthalpyFromHumidityRatio(
        double humidityRatio, double atmosphericPressure)
    {
      double ts = GetSaturationDryBulbTemperatureFromHumidityRatio(
          humidityRatio, atmosphericPressure);
      return GetEnthalpyFromDryBulbTemperatureAndHumidityRatio(ts, humidityRatio);
    }

    #endregion

    #region その他の物性値

    /// <summary>
    /// Gets the atmospheric pressure [kPa] at the given elevation [m].
    /// </summary>
    /// <param name="elevation">Elevation above sea level [m]</param>
    /// <returns>Atmospheric pressure [kPa]</returns>
    public static double GetAtmosphericPressure(double elevation)
    {
      return PhysicsConstants.StandardAtmosphericPressure
          * Math.Pow(1.0 - 2.25577e-5 * elevation, 5.2559);
    }

    /// <summary>
    /// Gets the isobaric specific heat of moist air [kJ/(kg·K)]
    /// from the humidity ratio [kg/kg(DA)].
    /// </summary>
    /// <param name="humidityRatio">Humidity ratio [kg/kg(DA)]</param>
    /// <returns>Isobaric specific heat of moist air [kJ/(kg·K)]</returns>
    public static double GetSpecificHeat(double humidityRatio)
    {
      return DryAirIsobaricSpecificHeat + VaporIsobaricSpecificHeat * humidityRatio;
    }

    /// <summary>
    /// Gets the dynamic viscosity of moist air [Pa·s]
    /// from the dry-bulb temperature [°C].
    /// </summary>
    /// <param name="dryBulbTemperature">Dry-bulb temperature [°C]</param>
    /// <returns>Dynamic viscosity [Pa·s]</returns>
    public static double GetViscosity(double dryBulbTemperature)
    {
      return (0.0074237 / (dryBulbTemperature + 390.15))
          * Math.Pow(PhysicsConstants.ToKelvin(dryBulbTemperature) / 293.15, 1.5);
    }

    /// <summary>
    /// Gets the kinematic viscosity of moist air [m²/s]
    /// from the dry-bulb temperature [°C], humidity ratio [kg/kg(DA)],
    /// and atmospheric pressure [kPa].
    /// </summary>
    /// <param name="dryBulbTemperature">Dry-bulb temperature [°C]</param>
    /// <param name="humidityRatio">Humidity ratio [kg/kg(DA)]</param>
    /// <param name="atmosphericPressure">Atmospheric pressure [kPa]</param>
    /// <returns>Kinematic viscosity [m²/s]</returns>
    public static double GetDynamicViscosity(
        double dryBulbTemperature, double humidityRatio, double atmosphericPressure)
    {
      return GetSpecificVolumeFromDryBulbTemperatureAndHumidityRatio(
          dryBulbTemperature, humidityRatio, atmosphericPressure)
          * GetViscosity(dryBulbTemperature);
    }

    /// <summary>
    /// Gets the thermal conductivity of moist air [W/(m·K)]
    /// from the dry-bulb temperature [°C].
    /// </summary>
    /// <param name="dryBulbTemperature">Dry-bulb temperature [°C]</param>
    /// <returns>Thermal conductivity [W/(m·K)]</returns>
    public static double GetThermalConductivity(double dryBulbTemperature)
    {
      return 0.0241 + 0.000077 * dryBulbTemperature;
    }

    /// <summary>
    /// Gets the volumetric thermal expansion coefficient of moist air [1/K]
    /// from the dry-bulb temperature [°C].
    /// </summary>
    /// <param name="dryBulbTemperature">Dry-bulb temperature [°C]</param>
    /// <returns>Volumetric thermal expansion coefficient [1/K]</returns>
    public static double GetExpansionCoefficient(double dryBulbTemperature)
    {
      return 1.0 / PhysicsConstants.ToKelvin(dryBulbTemperature);
    }

    /// <summary>
    /// Gets the thermal diffusivity of moist air [m²/s]
    /// from the dry-bulb temperature [°C], humidity ratio [kg/kg(DA)],
    /// and atmospheric pressure [kPa].
    /// </summary>
    /// <param name="dryBulbTemperature">Dry-bulb temperature [°C]</param>
    /// <param name="humidityRatio">Humidity ratio [kg/kg(DA)]</param>
    /// <param name="atmosphericPressure">Atmospheric pressure [kPa]</param>
    /// <returns>Thermal diffusivity [m²/s]</returns>
    public static double GetThermalDiffusivity(
        double dryBulbTemperature, double humidityRatio, double atmosphericPressure)
    {
      double lambda = GetThermalConductivity(dryBulbTemperature);
      double cp = GetSpecificHeat(humidityRatio);
      double v = GetSpecificVolumeFromDryBulbTemperatureAndHumidityRatio(
          dryBulbTemperature, humidityRatio, atmosphericPressure);
      return lambda / (1000.0 * cp * v);
    }

    /// <summary>
    /// Gets the latent heat of vaporization of water [kJ/kg]
    /// at the given temperature [°C].
    /// </summary>
    /// <param name="temperature">Water temperature [°C]</param>
    /// <returns>Latent heat of vaporization [kJ/kg]</returns>
    public static double GetLatentHeatOfVaporization(double temperature)
    {
      return VaporizationLatentHeat
          - temperature * (WaterIsobaricSpecificHeat - VaporIsobaricSpecificHeat);
    }

    #endregion

    #region 空気混合

    /// <summary>
    /// Blends multiple moist air streams by volume-weighted averaging.
    /// </summary>
    /// <param name="air">Array of moist air streams to blend.</param>
    /// <param name="volume">Array of volume flow rates corresponding to each stream.</param>
    /// <returns>The resulting blended moist air state.</returns>
    /// <exception cref="PopoloArgumentException">
    /// Thrown when the length of <paramref name="air"/> and <paramref name="volume"/> differ.
    /// </exception>
    public static MoistAir BlendAir(IReadOnlyMoistAir[] air, double[] volume)
    {
      if (air.Length != volume.Length)
        throw new PopoloArgumentException(
            $"The number of air streams ({air.Length}) must match "
            + $"the number of volume values ({volume.Length}).",
            nameof(volume));

      int airNum = air.Length;
      double rSum = 0, tSum = 0, trSum = 0, hSum = 0, hrSum = 0;
      for (int i = 0; i < airNum; i++)
      {
        volume[i] = Math.Max(0, volume[i]);
        rSum += volume[i];
        tSum += air[i].DryBulbTemperature;
        trSum += air[i].DryBulbTemperature * volume[i];
        hSum += air[i].HumidityRatio;
        hrSum += air[i].HumidityRatio * volume[i];
      }

      double dryBulbTempOut, absHumidOut;
      if (rSum >= 1.0e-5d)
      {
        dryBulbTempOut = trSum / rSum;
        absHumidOut = hrSum / rSum;
      }
      else
      {
        //割合の積算が小さい場合は発散を防ぐために混合空気の数で割る
        dryBulbTempOut = tSum / airNum;
        absHumidOut = hSum / airNum;
      }
      //出口空気状態を計算（飽和した場合の処理は未実装）
      return new MoistAir(dryBulbTempOut, absHumidOut);
    }

    /// <summary>
    /// Blends two moist air streams by volume-weighted averaging.
    /// </summary>
    /// <param name="air1">First moist air stream.</param>
    /// <param name="air2">Second moist air stream.</param>
    /// <param name="air1Volume">Volume flow rate of the first stream.</param>
    /// <param name="air2Volume">Volume flow rate of the second stream.</param>
    /// <returns>The resulting blended moist air state.</returns>
    public static MoistAir BlendAir(
        IReadOnlyMoistAir air1, IReadOnlyMoistAir air2, double air1Volume, double air2Volume)
    {
      if (air1Volume <= 0) return new MoistAir(air2.DryBulbTemperature, air2.HumidityRatio);
      if (air2Volume <= 0) return new MoistAir(air1.DryBulbTemperature, air1.HumidityRatio);

      double vSum = air1Volume + air2Volume;
      double dbt = (air1.DryBulbTemperature * air1Volume
          + air2.DryBulbTemperature * air2Volume) / vSum;
      double ahd = (air1.HumidityRatio * air1Volume
          + air2.HumidityRatio * air2Volume) / vSum;
      return new MoistAir(dbt, ahd);
    }

    #endregion

    #region 空気状態のコピー

    /// <summary>
    /// Copies this moist air state to the specified <see cref="MoistAir"/> instance.
    /// </summary>
    /// <param name="destination">The destination moist air instance.</param>
    public void CopyTo(MoistAir destination)
    {
      destination.AtmosphericPressure = AtmosphericPressure;
      destination.DryBulbTemperature = DryBulbTemperature;
      destination.WetBulbTemperature = WetBulbTemperature;
      destination.HumidityRatio = HumidityRatio;
      destination.RelativeHumidity = RelativeHumidity;
      destination.Enthalpy = Enthalpy;
      destination.SpecificVolume = SpecificVolume;
    }

    #endregion

  }
}
