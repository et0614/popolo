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
    /// <remarks>
    /// Simple linear cloud-cover correction. Treats opaque and thin clouds together
    /// and ignores cloud height. For higher accuracy when opaque cloud cover and
    /// ceiling height are available, see the
    /// <see cref="GetInfraredRadiationFromSky(double, double, double, double, double, int)"/>
    /// overload that implements the Martin-Berdahl (1984) model used in ANSI/ASHRAE
    /// Standard 140-2023 Tsky-Informative.
    /// </remarks>
    public static double GetInfraredRadiationFromSky(
        double temperature, int cloudCover, double waterVaporPartialPressure)
    {
      double br = GetSkyEmissivity(waterVaporPartialPressure);
      return ((1.0 - 0.062 * cloudCover) * br + 0.062 * cloudCover)
          * BlackBodyRadiation(temperature);
    }

    /// <summary>
    /// Gets the atmospheric (downwelling longwave) radiation from the sky [W/m²]
    /// using the Martin-Berdahl (1984) model with separate opaque and thin cloud
    /// contributions, ceiling-height correction, and station-pressure correction.
    /// This is the model used by ANSI/ASHRAE Standard 140-2023 to generate the
    /// informative Tsky values for the Section 7 BESTEST cases.
    /// </summary>
    /// <param name="temperature">Outdoor dry-bulb temperature [°C].</param>
    /// <param name="dewPointTemperature">Outdoor dew-point temperature [°C].</param>
    /// <param name="atmosphericPressure">Station atmospheric pressure [mbar = hPa].
    /// 1 mbar = 0.1 kPa.</param>
    /// <param name="totalCloudCover">Total cloud cover [-] (0 = clear, 1 = fully covered).</param>
    /// <param name="opaqueCloudCover">
    /// Opaque cloud cover [-] (0 = clear, 1 = fully covered with opaque clouds).
    /// Always ≤ <paramref name="totalCloudCover"/>; the difference is treated as thin clouds.
    /// </param>
    /// <param name="ceilingHeight">Ceiling height [m] (lowest opaque cloud base).
    /// Pass <see cref="double.NaN"/> when the source data reports clouds without
    /// a measurable ceiling base (e.g., when the ceilometer detects no ceiling
    /// despite reported opaque cover) — the Martin-Berdahl convention then sets
    /// <c>Γ_opaque = exp(2000/82000) ≈ 1.025</c>, treating the unmeasured base
    /// as effectively very low. Source-format-specific sentinel values (e.g.,
    /// TMY3 77777) should be normalised to <see cref="double.NaN"/> by the
    /// reader before calling this method.</param>
    /// <param name="hour">Hour of day [0–23] for the small diurnal correction.</param>
    /// <returns>Atmospheric infrared radiation [W/m²].</returns>
    /// <remarks>
    /// <para>Formulation (consistent with ASHRAE 140-2023 Tsky-Informative.xlsx):</para>
    /// <list type="bullet">
    ///   <item><description><c>ε_clr = 0.711 + 0.56·(Tdp/100) + 0.73·(Tdp/100)²
    ///     + 0.013·cos(2π·hr/24) + 0.00012·(P − 1000)</c></description></item>
    ///   <item><description><c>Γ_opaque = isNaN(CeilHgt) ? exp(2000/82000)
    ///     : exp(−CeilHgt/8200)</c></description></item>
    ///   <item><description><c>C_opaque = OpqCld · Γ_opaque</c>,
    ///     <c>C_thin = (TotCld − OpqCld) · 0.4 · exp(−8000/8200)</c></description></item>
    ///   <item><description><c>ε_sky = ε_clr + (1 − ε_clr) · (C_opaque + C_thin)</c></description></item>
    ///   <item><description><c>R = ε_sky · σ · (T+273.15)⁴</c></description></item>
    /// </list>
    /// </remarks>
    public static double GetInfraredRadiationFromSky(
        double temperature,
        double dewPointTemperature,
        double atmosphericPressure,
        double totalCloudCover,
        double opaqueCloudCover,
        double ceilingHeight,
        int hour)
    {
      double tdp100 = dewPointTemperature / 100.0;
      double epsClear = 0.711
                      + 0.56    * tdp100
                      + 0.73    * tdp100 * tdp100
                      + 0.013   * Math.Cos(2.0 * Math.PI * hour / 24.0)
                      + 0.00012 * (atmosphericPressure - 1000.0);

      // Martin-Berdahl 規約:
      //   通常: Γ_opaque = exp(−CeilHgt/8200)
      //   天井未測 (NaN, 例: 雲は報告されているがその底面が測定不能): exp(2000/82000) ≈ 1.025
      //     (= 雲底を非常に低い高さとして扱う近似)
      double gammaOpaque = double.IsNaN(ceilingHeight)
          ? Math.Exp(2000.0 / 82000.0)
          : Math.Exp(-ceilingHeight / 8200.0);
      double thinFraction = Math.Max(0.0, totalCloudCover - opaqueCloudCover);
      double cOpaque = opaqueCloudCover * gammaOpaque;
      double cThin   = thinFraction * 0.4 * Math.Exp(-8000.0 / 8200.0);
      double cTotal  = cOpaque + cThin;

      double epsSky = epsClear + (1.0 - epsClear) * cTotal;
      // Clamp to avoid pathological values from extreme inputs.
      if (epsSky < 0.0) epsSky = 0.0;
      if (epsSky > 1.0) epsSky = 1.0;
      return epsSky * BlackBodyRadiation(temperature);
    }

    /// <summary>
    /// Backward-compatible overload of
    /// <see cref="GetInfraredRadiationFromSky(double, double, double, double, double, double, int)"/>
    /// that omits the station-pressure correction (assumes P = 1000 mbar). Use
    /// the seven-argument overload for full Std 140-2023 fidelity.
    /// </summary>
    public static double GetInfraredRadiationFromSky(
        double temperature,
        double dewPointTemperature,
        double totalCloudCover,
        double opaqueCloudCover,
        double ceilingHeight,
        int hour)
        => GetInfraredRadiationFromSky(temperature, dewPointTemperature, 1000.0,
            totalCloudCover, opaqueCloudCover, ceilingHeight, hour);

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

    /// <summary>
    /// Gets the apparent (effective) sky temperature [°C] from the downwelling
    /// atmospheric (long-wave) radiation [W/m²]. Inverts Stefan–Boltzmann
    /// assuming a black emitter:
    /// <c>T_sky = (R / σ)^(1/4)</c>.
    /// </summary>
    /// <param name="infraredRadiationFromSky">
    /// Downwelling atmospheric infrared radiation from the sky [W/m²].
    /// </param>
    /// <returns>Apparent sky temperature [°C].</returns>
    /// <remarks>
    /// This is the equivalent black-body temperature of the sky as seen by an
    /// upward-facing horizontal surface, not the physical temperature of the
    /// upper atmosphere. It is the form usually reported as "sky temperature"
    /// in weather-driver test suites such as ANSI/ASHRAE Standard 140-2023
    /// Section 6.
    /// </remarks>
    public static double GetSkyTemperature(double infraredRadiationFromSky)
        => PhysicsConstants.ToCelsius(
            Math.Pow(infraredRadiationFromSky / PhysicsConstants.StefanBoltzmannConstant, 0.25));

    /// <summary>Computes the black-body radiation σT⁴ [W/m²].</summary>
    private static double BlackBodyRadiation(double temperature)
        => PhysicsConstants.StefanBoltzmannConstant
           * Math.Pow(PhysicsConstants.ToKelvin(temperature), 4);

    #endregion

    #region 屋外側対流熱伝達率

    /// <summary>
    /// Returns the windward exterior convective heat transfer coefficient [W/(m²·K)]
    /// from local wind speed and surface-air temperature difference, using the
    /// MoWiTT correlation (Yazdanian and Klems 1994).
    /// </summary>
    /// <param name="windSpeed">Local wind speed at the surface [m/s] (typically the
    /// 10 m weather-station value; building-height correction is the caller's
    /// responsibility).</param>
    /// <param name="surfaceAirDeltaT">Surface-to-air temperature difference [K]
    /// (sign ignored; |Ts − Tair| is used for the natural-convection term).</param>
    /// <returns>Combined forced + natural exterior convective coefficient [W/(m²·K)].</returns>
    /// <remarks>
    /// <para>
    /// The MoWiTT correlation has the form
    /// <c>h_c = sqrt( (Ct·|ΔT|^(1/3))² + (a·v^b)² )</c>
    /// with windward coefficients <c>Ct = 0.84</c>, <c>a = 3.26</c>, <c>b = 0.89</c>.
    /// It was fitted to outdoor measurements on smooth vertical surfaces (window
    /// glass) at the Mobile Window Thermal Test (MoWiTT) facility and is the
    /// EnergyPlus "DOE-2" default.
    /// </para>
    /// <para>
    /// The returned value is windward; leeward surfaces typically run 30–40%
    /// lower. Because most building surfaces alternate between windward and
    /// leeward over time, this function returns the single windward value as a
    /// physically meaningful upper-bound. Callers that need to distinguish the
    /// two faces should apply their own directional weighting.
    /// </para>
    /// <para>
    /// Reference: Yazdanian, M., Klems, J. H., 1994. <i>Measurement of the exterior
    /// convective film coefficient for windows in low-rise buildings.</i>
    /// ASHRAE Transactions 100 (1).
    /// </para>
    /// </remarks>
    public static double GetExteriorConvectiveCoefficient(double windSpeed, double surfaceAirDeltaT)
        => GetExteriorConvectiveCoefficient(windSpeed, surfaceAirDeltaT, 1.0, WindOrientation.Windward);

    /// <summary>
    /// Same as <see cref="GetExteriorConvectiveCoefficient(double, double)"/>
    /// with an explicit surface-roughness multiplier R_f applied to the
    /// forced-convection term, per ASHRAE Handbook — Fundamentals (2009)
    /// Ch. 26 Table 4 (also adopted by EnergyPlus DOE-2 / MoWiTT model).
    /// Uses windward MoWiTT coefficients; for explicit windward/leeward
    /// selection use the four-argument overload.
    /// </summary>
    /// <param name="windSpeed">Wind speed at the surface [m/s].</param>
    /// <param name="surfaceAirDeltaT">Surface − outdoor air temperature difference [K].</param>
    /// <param name="roughnessMultiplier">
    /// Forced-convection roughness multiplier R_f [-]. 1.0 (default smooth glass) up to
    /// 2.17 (very rough). See <see cref="Building.Envelope.SurfaceRoughness"/>.
    /// </param>
    /// <returns>Combined exterior convective heat transfer coefficient [W/(m²·K)].</returns>
    public static double GetExteriorConvectiveCoefficient(double windSpeed, double surfaceAirDeltaT, double roughnessMultiplier)
        => GetExteriorConvectiveCoefficient(windSpeed, surfaceAirDeltaT, roughnessMultiplier, WindOrientation.Windward);

    /// <summary>
    /// Same as <see cref="GetExteriorConvectiveCoefficient(double, double, double)"/>
    /// with explicit selection of the windward vs. leeward MoWiTT correlation.
    /// </summary>
    /// <param name="windSpeed">Wind speed at the surface [m/s].</param>
    /// <param name="surfaceAirDeltaT">Surface − outdoor air temperature difference [K].</param>
    /// <param name="roughnessMultiplier">Forced-convection roughness multiplier R_f [-].</param>
    /// <param name="orientation">
    /// Windward (wind blowing toward the surface) or leeward (wind blowing
    /// away from the surface, i.e. surface in the wake). The two regimes use
    /// different MoWiTT fitted constants reflecting the underlying flow
    /// physics (impingement vs. recirculation).
    /// </param>
    /// <returns>Combined exterior convective heat transfer coefficient [W/(m²·K)].</returns>
    /// <remarks>
    /// <para>
    /// <c>h_c = sqrt( h_n² + (R_f · h_glass_forced)² )</c> with the natural-convection
    /// term unchanged. The forced term uses
    /// <c>h_glass_forced = a · v^b</c> with windward
    /// (<c>a = 3.26, b = 0.89</c>) or leeward (<c>a = 3.55, b = 0.617</c>)
    /// coefficients per Yazdanian and Klems (1994).
    /// </para>
    /// <para>
    /// Reference: Yazdanian, M., Klems, J. H., 1994. <i>Measurement of the exterior
    /// convective film coefficient for windows in low-rise buildings.</i>
    /// ASHRAE Transactions 100 (1).
    /// </para>
    /// </remarks>
    public static double GetExteriorConvectiveCoefficient(
        double windSpeed, double surfaceAirDeltaT, double roughnessMultiplier, WindOrientation orientation)
    {
      const double Ct = 0.84;
      var (a, b) = orientation == WindOrientation.Windward ? (3.26, 0.89) : (3.55, 0.617);
      double v = windSpeed > 0 ? windSpeed : 0;
      double dT = Math.Abs(surfaceAirDeltaT);
      double natural = Ct * Math.Pow(dT, 1.0 / 3.0);
      double forced = roughnessMultiplier * a * Math.Pow(v, b);
      return Math.Sqrt(natural * natural + forced * forced);
    }

    /// <summary>
    /// Adjusts a wind speed measured at one height/terrain to the equivalent
    /// wind speed at another height/terrain using the ASHRAE two-terrain
    /// power-law form (2021 Handbook—Fundamentals Ch. 24):
    /// <c>V_local = V_meteo · (δ_meteo/H_meteo)^a_meteo · (H_local/δ_local)^a_local</c>.
    /// </summary>
    /// <param name="meteoWindSpeed">Wind speed at the source (meteorological station) [m/s].</param>
    /// <param name="meteoHeight">Anemometer height at the station [m].</param>
    /// <param name="meteoTerrain">Terrain category of the station surroundings.</param>
    /// <param name="localHeight">Target (e.g., wall mid-) height above ground [m].</param>
    /// <param name="localTerrain">Terrain category at the target site.</param>
    /// <returns>Wind speed at the target height [m/s].</returns>
    public static double CorrectWindSpeedForHeight(
        double meteoWindSpeed, double meteoHeight, TerrainCategory meteoTerrain,
        double localHeight, TerrainCategory localTerrain)
    {
      if (meteoWindSpeed <= 0 || meteoHeight <= 0 || localHeight <= 0) return meteoWindSpeed;
      var (aMeteo, dMeteo) = meteoTerrain.GetParameters();
      var (aLocal, dLocal) = localTerrain.GetParameters();
      double factor = Math.Pow(dMeteo / meteoHeight, aMeteo) * Math.Pow(localHeight / dLocal, aLocal);
      return meteoWindSpeed * factor;
    }

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