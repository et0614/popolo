/* Sky.cs
 *
 * Copyright (C) 2008 E.Togashi
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 2 of the License, or (at
 * your option) any later version.
 *
 * This program is distributed in the hope that it will be useful, but
 * WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
 * General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 675 Mass Ave, Cambridge, MA 02139, USA.
 *
 * References:
 *   Perez, R., Ineichen, P., Seals, R., Michalsky, J., Stewart, R.,
 *     "Modeling daylight availability and irradiance components from
 *      direct and global irradiance,"
 *     Solar Energy, Vol. 44, No. 5, 1990, pp. 271-289.
 *     — all-weather anisotropic sky diffuse model used by
 *     GetPerezSkyDiffuseOnPlane. The F11..F23 coefficient table is
 *     Table II of that paper.
 *
 *   Kasten, F., Young, A. T.,
 *     "Revised optical air mass tables and approximation formula,"
 *     Applied Optics, Vol. 28, No. 22, 1989, pp. 4735-4738.
 *     — air mass formula consumed by the Perez brightness Δ; the
 *     implementation itself lives in Sun.GetAirMass.
 */

using System;
using Popolo.Core.Physics;

namespace Popolo.Core.Climate
{
  /// <summary>
  /// Provides static methods for long-wave sky radiation exchange and related
  /// atmospheric estimates used by the building thermal model.
  /// </summary>
  /// <remarks>
  /// <para>
  /// In an outdoor heat balance, every surface facing the sky loses heat to
  /// the cold upper atmosphere (nocturnal radiation) and gains heat from its
  /// own downwelling infrared emission (atmospheric radiation). This class
  /// computes both sides of that exchange from three common weather inputs:
  /// the outdoor dry-bulb temperature, the cloud cover on a 0–10 scale, and
  /// the water-vapor partial pressure of the ambient air.
  /// </para>
  /// <para>
  /// The sky emissivity under clear-sky conditions is approximated by
  /// <see cref="GetSkyEmissivity"/> as an increasing function of water-vapor
  /// partial pressure; cloud cover then shifts the sky toward a near-black-body
  /// emitter. Results feed
  /// <see cref="Building.IReadOnlyBuildingThermalModel.NocturnalRadiation"/>
  /// and, ultimately, the sol-air temperature on each exterior surface.
  /// </para>
  /// <para>
  /// <see cref="GetPrecipitableWater"/> provides a separate utility used
  /// primarily for atmospheric transmissivity calculations (see
  /// <see cref="Sun"/>).
  /// </para>
  /// <para>
  /// References:
  /// <list type="bullet">
  ///   <item><description>Shukuya, M., "Light and Heat in the Architectural Environment — Numerical Approaches," Maruzen, 1993, p. 20.</description></item>
  ///   <item><description>Udagawa, M., "Air Conditioning Calculations with Personal Computers," 1986.</description></item>
  /// </list>
  /// </para>
  /// </remarks>
  public static class Sky
  {

    #region 放射関連

    /// <summary>
    /// Gets the nocturnal (outgoing longwave) radiation [W/m²].
    /// </summary>
    /// <param name="temperature">Outdoor dry-bulb temperature [°C]</param>
    /// <param name="cloudCover">Cloud cover [-] (0: clear, 10: overcast)</param>
    /// <param name="waterVaporPartialPressure">Water vapor partial pressure [kPa]</param>
    /// <returns>Nocturnal radiation [W/m²]</returns>
    public static double GetNocturnalRadiation(
        double temperature, int cloudCover, double waterVaporPartialPressure)
    {
      double br = GetSkyEmissivity(waterVaporPartialPressure);
      return (1.0 - 0.062 * cloudCover) * (1.0 - br)
          * BlackBodyRadiation(temperature);
    }

    /// <summary>
    /// Gets the atmospheric (downwelling longwave) radiation from the sky [W/m²].
    /// </summary>
    /// <param name="temperature">Outdoor dry-bulb temperature [°C]</param>
    /// <param name="cloudCover">Cloud cover [-] (0: clear, 10: overcast)</param>
    /// <param name="waterVaporPartialPressure">Water vapor partial pressure [kPa]</param>
    /// <returns>Atmospheric infrared radiation [W/m²]</returns>
    public static double GetInfraredRadiationFromSky(
        double temperature, int cloudCover, double waterVaporPartialPressure)
    {
      double br = GetSkyEmissivity(waterVaporPartialPressure);
      return ((1.0 - 0.062 * cloudCover) * br + 0.062 * cloudCover)
          * BlackBodyRadiation(temperature);
    }

    /// <summary>
    /// Gets the cloud cover [-] from the atmospheric radiation, temperature,
    /// and water vapor partial pressure.
    /// </summary>
    /// <param name="infraredRadFromSky">Atmospheric infrared radiation [W/m²]</param>
    /// <param name="temperature">Outdoor dry-bulb temperature [°C]</param>
    /// <param name="waterVaporPartialPressure">Water vapor partial pressure [kPa]</param>
    /// <returns>Cloud cover [-] (0 to 10, integer)</returns>
    public static int GetCloudCover(
        double infraredRadFromSky, double temperature, double waterVaporPartialPressure)
    {
      double br = GetSkyEmissivity(waterVaporPartialPressure);
      double bf = BlackBodyRadiation(temperature);
      double cc = (br * bf - infraredRadFromSky) / ((br - 1.0) * bf) / 0.062;
      return (int)Math.Max(0, Math.Min(10, cc));
    }

    /// <summary>
    /// Gets the sky emissivity [-] from the water vapor partial pressure [kPa].
    /// </summary>
    /// <param name="waterVaporPartialPressure">Water vapor partial pressure [kPa]</param>
    /// <returns>Sky emissivity [-]</returns>
    public static double GetSkyEmissivity(double waterVaporPartialPressure)
        => 0.526 + 0.209 * Math.Sqrt(waterVaporPartialPressure);

    /// <summary>Computes the black-body radiation σT⁴ [W/m²].</summary>
    private static double BlackBodyRadiation(double temperature)
        => PhysicsConstants.StefanBoltzmannConstant
           * Math.Pow(PhysicsConstants.ToKelvin(temperature), 4);

    #endregion

    #region 降水量関連

    /// <summary>
    /// Estimates the precipitable water [mm] from the elevation and dew point temperature.
    /// </summary>
    /// <param name="elevation">Elevation above sea level [m]</param>
    /// <param name="dewpointTemperature">Dew point temperature [°C]</param>
    /// <returns>Precipitable water [mm]</returns>
    /// <remarks>
    /// Kondo, J.: An empirical formula to estimate precipitable water from surface dew point
    /// temperature, Journal of Japan Society of Hydrology and Water Resources,
    /// Vol.9, No.5, 1996.
    /// </remarks>
    public static double GetPrecipitableWater(double elevation, double dewpointTemperature)
    {
      double atm = MoistAir.GetAtmosphericPressure(elevation);
      double x0 = 1.0 - Math.Sqrt(atm / PhysicsConstants.StandardAtmosphericPressure);
      double x;
      if (dewpointTemperature < -5)
        x = 0.027 * dewpointTemperature - 0.150 - x0;
      else if (dewpointTemperature < 23)
        x = 0.031 * dewpointTemperature - 0.130 - x0;
      else
        x = 0.015 * dewpointTemperature - 0.238 - x0;
      return 10 * Math.Pow(10, x) / 0.8;
    }

    #endregion

    #region Perez 全天候型異方性モデル

    /// <summary>Upper limits of the sky-clearness bins (Perez 1990, Table II).</summary>
    /// <remarks>
    /// Eight bins in total. A sky with clearness <c>ε</c> falls into the
    /// lowest bin whose upper limit is not less than <c>ε</c>. The eighth
    /// bin has no upper limit.
    /// </remarks>
    private static readonly double[] _perezEpsilonUpper =
        { 1.065, 1.230, 1.500, 1.950, 2.800, 4.500, 6.200, double.PositiveInfinity };

    /// <summary>F1, F2 coefficient table from Perez 1990, Table II.</summary>
    /// <remarks>
    /// <para>
    /// Rows are sky-clearness bins (1..8); columns are
    /// <c>{ f11, f12, f13, f21, f22, f23 }</c> where
    /// <c>F1 = max(0, f11 + f12·Δ + f13·z)</c> (circumsolar brightening) and
    /// <c>F2 = f21 + f22·Δ + f23·z</c> (horizon brightening), with <c>z</c>
    /// the solar zenith in radians.
    /// </para>
    /// <para>
    /// Values taken directly from Perez, R., Ineichen, P., Seals, R.,
    /// Michalsky, J., Stewart, R., "Modeling daylight availability and
    /// irradiance components from direct and global irradiance," Solar
    /// Energy, Vol. 44, No. 5, 1990, Table II, p. 275. The same figures
    /// are reproduced in ASHRAE Handbook of Fundamentals and in pvlib's
    /// reference implementation of <c>get_sky_diffuse</c>.
    /// </para>
    /// </remarks>
    private static readonly double[,] _perezCoefficients = new double[,]
    {
      //  f11      f12      f13      f21      f22      f23          ε bin  range
      { -0.0083,  0.5877, -0.0621, -0.0596,  0.0721, -0.0220 },  //   1    ε ≤ 1.065
      {  0.1299,  0.6826, -0.1514, -0.0189,  0.0660, -0.0289 },  //   2    1.065 < ε ≤ 1.230
      {  0.3297,  0.4869, -0.2211,  0.0554, -0.0640, -0.0261 },  //   3    1.230 < ε ≤ 1.500
      {  0.5682,  0.1875, -0.2951,  0.1089, -0.1519, -0.0140 },  //   4    1.500 < ε ≤ 1.950
      {  0.8730, -0.3920, -0.3616,  0.2256, -0.4620,  0.0012 },  //   5    1.950 < ε ≤ 2.800
      {  1.1326, -1.2367, -0.4118,  0.2878, -0.8230,  0.0559 },  //   6    2.800 < ε ≤ 4.500
      {  1.0602, -1.5999, -0.3589,  0.2642, -1.1272,  0.1311 },  //   7    4.500 < ε ≤ 6.200
      {  0.6777, -0.3273, -0.2504,  0.1561, -1.3765,  0.2506 },  //   8    ε > 6.200
    };

    /// <summary>
    /// Gets the sky-diffuse plane-of-array irradiance [W/m²] on a tilted
    /// surface using the Perez (1990) all-weather anisotropic sky model.
    /// The returned value is the <b>sky component only</b>; ground-reflected
    /// diffuse must be added separately (usually as the isotropic
    /// <c>albedo · GHI · (1 − cos β) / 2</c> term).
    /// </summary>
    /// <param name="directNormalRadiation">
    /// Direct normal irradiance (DNI) [W/m²].
    /// </param>
    /// <param name="diffuseHorizontalRadiation">
    /// Diffuse horizontal irradiance (DHI) [W/m²].
    /// </param>
    /// <param name="surfaceTilt">
    /// Surface tilt β (0 = horizontal, π/2 = vertical) [radian].
    /// </param>
    /// <param name="cosIncidenceAngle">
    /// Cosine of the angle between the surface normal and the sun
    /// (<c>cos θ_i</c>), clamped to [0, 1] by the caller. The Perez model
    /// uses <c>a = max(0, cos θ_i)</c>.
    /// </param>
    /// <param name="solarZenith">
    /// Solar zenith angle (π/2 − altitude) [radian]. Must be in [0, π/2]
    /// for a well-defined result; values ≥ 85° are clamped internally by
    /// the denominator term.
    /// </param>
    /// <param name="airMass">
    /// Relative air mass at the solar altitude, typically obtained from
    /// <see cref="Sun.GetAirMass(double)"/> (Kasten &amp; Young 1989).
    /// </param>
    /// <param name="extraterrestrialNormalRadiation">
    /// Extraterrestrial normal irradiance I₀ [W/m²] at the current day of
    /// year, typically obtained from
    /// <see cref="Sun.GetExtraterrestrialRadiation()"/>.
    /// </param>
    /// <returns>
    /// Sky-diffuse plane-of-array irradiance [W/m²]. Returns 0 when DHI is
    /// 0 (no diffuse to project) or when the sun is below the horizon and
    /// DNI is 0 (no meaningful clearness / brightness definition); in
    /// those cases the caller should still add the ground-reflected term
    /// separately, which depends on GHI only.
    /// </returns>
    /// <remarks>
    /// <para>
    /// The model form:
    /// </para>
    /// <code>
    /// I_d,POA = DHI × [ (1 − F1) · (1 + cos β) / 2
    ///                  + F1 · a / b
    ///                  + F2 · sin β ]
    /// </code>
    /// <para>
    /// where <c>a = max(0, cos θ_i)</c>,
    /// <c>b = max(cos 85°, cos z)</c>, and F1, F2 come from
    /// <see cref="_perezCoefficients"/> as linear functions of the sky
    /// brightness Δ and the solar zenith z:
    /// </para>
    /// <code>
    /// ε = [ (DHI + DNI) / DHI + κ · z³ ] / (1 + κ · z³),  κ = 1.041   (z in rad)
    /// Δ = AM · DHI / I₀
    /// F1 = max(0, f11(ε) + f12(ε) · Δ + f13(ε) · z)
    /// F2 =         f21(ε) + f22(ε) · Δ + f23(ε) · z
    /// </code>
    /// <para>
    /// Reference: Perez, R., Ineichen, P., Seals, R., Michalsky, J.,
    /// Stewart, R., "Modeling daylight availability and irradiance
    /// components from direct and global irradiance," Solar Energy,
    /// Vol. 44, No. 5, 1990, pp. 271-289.
    /// </para>
    /// </remarks>
    public static double GetPerezSkyDiffuseOnPlane(
        double directNormalRadiation,
        double diffuseHorizontalRadiation,
        double surfaceTilt,
        double cosIncidenceAngle,
        double solarZenith,
        double airMass,
        double extraterrestrialNormalRadiation)
    {
      if (diffuseHorizontalRadiation <= 0) return 0.0;

      double cosBeta = Math.Cos(surfaceTilt);
      double sinBeta = Math.Sin(surfaceTilt);
      double viewFactorToSky = 0.5 * (1.0 + cosBeta);

      // 太陽が地平線以下で DNI=0 のとき、clearness ε の定義が退化する。
      // 等方項のみ (F1=F2=0) に倒して評価する。
      if (solarZenith >= 0.5 * Math.PI && directNormalRadiation <= 0)
        return diffuseHorizontalRadiation * viewFactorToSky;

      // sky clearness ε と brightness Δ
      const double kappa = 1.041;
      double z = solarZenith;
      double z3 = z * z * z;
      double epsilon = ((diffuseHorizontalRadiation + directNormalRadiation)
                         / diffuseHorizontalRadiation + kappa * z3)
                     / (1.0 + kappa * z3);
      double delta = airMass * diffuseHorizontalRadiation
                     / extraterrestrialNormalRadiation;

      // ε bin の決定
      int bin = 0;
      while (bin < _perezEpsilonUpper.Length - 1 && epsilon > _perezEpsilonUpper[bin])
        bin++;

      double f11 = _perezCoefficients[bin, 0];
      double f12 = _perezCoefficients[bin, 1];
      double f13 = _perezCoefficients[bin, 2];
      double f21 = _perezCoefficients[bin, 3];
      double f22 = _perezCoefficients[bin, 4];
      double f23 = _perezCoefficients[bin, 5];

      double f1 = Math.Max(0.0, f11 + f12 * delta + f13 * z);
      double f2 =                f21 + f22 * delta + f23 * z;

      // 周囲光成分 (circumsolar) の比率 a / b
      //   a = max(0, cos θ_i)    b = max(cos 85°, cos z)
      const double cos85 = 0.0871557427476582;     // = cos(85°)
      double a = Math.Max(0.0, cosIncidenceAngle);
      double b = Math.Max(cos85, Math.Cos(z));

      double term_iso        = (1.0 - f1) * viewFactorToSky;
      double term_circ       = f1 * a / b;
      double term_horizon    = f2 * sinBeta;

      return diffuseHorizontalRadiation * (term_iso + term_circ + term_horizon);
    }

    #endregion

  }
}