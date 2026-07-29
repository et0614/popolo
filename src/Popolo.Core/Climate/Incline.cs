/* Incline.cs
 *
 * Copyright (C) 2008 E.Togashi
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

namespace Popolo.Core.Climate
{
  /// <inheritdoc cref="IReadOnlyIncline"/>
  /// <remarks>
  /// <para>
  /// This is the mutable implementation of <see cref="IReadOnlyIncline"/>. An
  /// <see cref="Incline"/> is immutable in its core angle state once constructed —
  /// the angles are accepted in the constructor, normalized, and cached along
  /// with their trigonometric values for fast repeated queries. The only
  /// mutating operation is <see cref="Copy(IReadOnlyIncline)"/>, which
  /// re-imports state from another incline.
  /// </para>
  /// <para>
  /// Three constructor overloads are provided:
  /// <list type="bullet">
  ///   <item><description>(horizontal, vertical) — raw radians.</description></item>
  ///   <item><description>(<see cref="Orientation"/>, vertical) — 16-point compass direction with a numeric tilt angle.</description></item>
  ///   <item><description>(<see cref="IReadOnlyIncline"/>) — copy from an existing incline.</description></item>
  /// </list>
  /// Angles passed in are normalized so that <c>HorizontalAngle</c> lies in
  /// (-π, π] and <c>VerticalAngle</c> lies in [0, π]; an out-of-range input
  /// is not an error. The reversed-orientation helper
  /// <see cref="MakeReverseIncline"/> produces the incline that faces the
  /// opposite direction (useful for representing the B side of a wall).
  /// </para>
  /// </remarks>
  public class Incline : IReadOnlyIncline
  {

    #region Enumerations

    /// <summary>
    /// Specifies one of 16 compass directions for surface orientation.
    /// </summary>
    public enum Orientation
    {
      /// <summary>North-northeast</summary>
      NNE = -7,
      /// <summary>Northeast</summary>
      NE = -6,
      /// <summary>East-northeast</summary>
      ENE = -5,
      /// <summary>East</summary>
      E = -4,
      /// <summary>East-southeast</summary>
      ESE = -3,
      /// <summary>Southeast</summary>
      SE = -2,
      /// <summary>South-southeast</summary>
      SSE = -1,
      /// <summary>South</summary>
      S = 0,
      /// <summary>South-southwest</summary>
      SSW = 1,
      /// <summary>Southwest</summary>
      SW = 2,
      /// <summary>West-southwest</summary>
      WSW = 3,
      /// <summary>West</summary>
      W = 4,
      /// <summary>West-northwest</summary>
      WNW = 5,
      /// <summary>Northwest</summary>
      NW = 6,
      /// <summary>North-northwest</summary>
      NNW = 7,
      /// <summary>North</summary>
      N = 8
    }

    #endregion

    #region Instance variables

    /// <summary>Precomputed sine and cosine of the azimuth and tilt angles (cached for performance).</summary>
    private double _sinBeta, _cosBeta, _sinAlpha, _cosAlpha;

    #endregion

    #region Properties

    /// <summary>
    /// Gets the horizontal (azimuth) angle [radian].
    /// South = 0, east = negative, west = positive.
    /// </summary>
    public double HorizontalAngle { get; private set; }

    /// <summary>
    /// Gets the vertical (tilt) angle [radian].
    /// Horizontal = 0, vertical = π/2.
    /// </summary>
    public double VerticalAngle { get; private set; }

    /// <summary>Gets the view factor from the surface to the sky [-].</summary>
    public double ConfigurationFactorToSky { get; private set; }

    /// <summary>Gets the view factor from the surface to the ground [-].</summary>
    public double ConfigurationFactorToGround => 1.0 - ConfigurationFactorToSky;

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new instance with the specified azimuth and tilt angles.
    /// </summary>
    /// <param name="horizontalAngle">
    /// Azimuth angle [radian]. South = 0, east = negative, west = positive.
    /// </param>
    /// <param name="verticalAngle">
    /// Tilt angle [radian]. Horizontal = 0, vertical = π/2.
    /// </param>
    public Incline(double horizontalAngle, double verticalAngle)
    {
      //Normalize the azimuth to the range [-π, π]
      double pi2 = Math.PI * 2;
      horizontalAngle = horizontalAngle % pi2;
      if (Math.PI < horizontalAngle) HorizontalAngle = horizontalAngle - pi2;
      else if (horizontalAngle < -Math.PI) HorizontalAngle = horizontalAngle + pi2;
      else HorizontalAngle = horizontalAngle;

      //Normalize the tilt angle to the range [0, π]
      //VerticalAngle = verticalAngle % Math.PI;
      //if (Math.PI < VerticalAngle) VerticalAngle = Math.PI - VerticalAngle;
      //Normalize the tilt angle to the range [0, π] (π is kept as a downward-facing horizontal surface) 2026.04.18 Bug fix.
      verticalAngle = verticalAngle % pi2;            // (-2π, 2π)
      if (verticalAngle < 0) verticalAngle += pi2;    // [0, 2π)
      if (Math.PI < verticalAngle) verticalAngle = pi2 - verticalAngle; // [0, π]
      VerticalAngle = verticalAngle;

      ConfigurationFactorToSky = GetConfigurationFactorToSky(VerticalAngle);

      //Precompute trigonometric functions
      _sinBeta = Math.Sin(VerticalAngle);
      _cosBeta = Math.Cos(VerticalAngle);
      _sinAlpha = Math.Sin(HorizontalAngle);
      _cosAlpha = Math.Cos(HorizontalAngle);
    }

    /// <summary>
    /// Initializes a new instance by copying from an <see cref="IReadOnlyIncline"/>.
    /// </summary>
    /// <param name="incline">The source incline to copy.</param>
    public Incline(IReadOnlyIncline incline)
        : this(incline.HorizontalAngle, incline.VerticalAngle) { }

    /// <summary>
    /// Initializes a new instance with a compass direction and tilt angle.
    /// </summary>
    /// <param name="orientation">16-point compass direction.</param>
    /// <param name="verticalAngle">
    /// Tilt angle [radian]. Horizontal = 0, vertical = π/2, downward horizontal = π.
    /// </param>
    public Incline(Orientation orientation, double verticalAngle)
        : this(Math.PI / 8 * (int)orientation, verticalAngle) { }

    #endregion

    #region Solar radiation on inclined surfaces

    /// <summary>
    /// Gets the cosine of the angle of incidence of direct solar radiation
    /// on the surface normal (cosθ) [-].
    /// </summary>
    /// <param name="altitude">Solar altitude [radian]</param>
    /// <param name="orientation">Solar azimuth [radian]</param>
    /// <returns>cosθ [-] (clamped to 0 when the sun is behind the surface)</returns>
    public double GetDirectSolarRadiationRatio(double altitude, double orientation)
    {
      double sh = Math.Sin(altitude);
      double ch = Math.Cos(altitude);
      double caa = Math.Cos(orientation - HorizontalAngle);
      return Math.Max(0, sh * _cosBeta + ch * _sinBeta * caa);
    }

    /// <summary>
    /// Gets the cosine of the angle of incidence of direct solar radiation
    /// on the surface normal (cosθ) [-].
    /// </summary>
    /// <param name="sun">Solar state</param>
    /// <returns>cosθ [-]</returns>
    public double GetDirectSolarRadiationRatio(IReadOnlySun sun)
        => GetDirectSolarRadiationRatio(sun.Altitude, sun.Azimuth);

    /// <summary>
    /// Gets the direct solar irradiance on the tilted surface [W/m²].
    /// </summary>
    /// <param name="sun">Solar state</param>
    /// <returns>Direct solar irradiance [W/m²]</returns>
    public double GetDirectSolarIrradiance(IReadOnlySun sun)
        => GetDirectSolarRadiationRatio(sun) * sun.DirectNormalRadiation;

    /// <summary>
    /// Gets the diffuse solar irradiance on the tilted surface [W/m²],
    /// including sky-diffuse and ground-reflected components. The sky
    /// component uses the Perez (1990) all-weather anisotropic model by
    /// default; see <see cref="GetDiffuseSolarIrradiance(IReadOnlySun, double, SkyDiffuseModel)"/>
    /// for isotropic-sky behaviour.
    /// </summary>
    /// <param name="sun">Solar state</param>
    /// <param name="albedo">Ground reflectance [-]</param>
    /// <returns>Diffuse solar irradiance [W/m²]</returns>
    public double GetDiffuseSolarIrradiance(IReadOnlySun sun, double albedo)
        => GetDiffuseSolarIrradiance(sun, albedo, SkyDiffuseModel.Perez);

    /// <summary>
    /// Gets the diffuse solar irradiance on the tilted surface [W/m²]
    /// using the specified sky model.
    /// </summary>
    /// <param name="sun">Solar state</param>
    /// <param name="albedo">Ground reflectance [-]</param>
    /// <param name="model">
    /// Sky-diffuse projection model. <see cref="SkyDiffuseModel.Perez"/>
    /// applies the Perez (1990) all-weather anisotropic model to the
    /// horizontal diffuse; <see cref="SkyDiffuseModel.Isotropic"/> projects
    /// the diffuse uniformly via the view factor to the sky.
    /// </param>
    /// <returns>Diffuse solar irradiance [W/m²]</returns>
    /// <remarks>
    /// <para>
    /// The ground-reflected component,
    /// <c>albedo · (1 − cos β)/2 · GHI</c>, is independent of the sky
    /// model and is added to the sky-diffuse contribution regardless of
    /// <paramref name="model"/>.
    /// </para>
    /// <para>
    /// Perez, R., Ineichen, P., Seals, R., Michalsky, J., Stewart, R.,
    /// "Modeling daylight availability and irradiance components from
    /// direct and global irradiance," Solar Energy, Vol. 44, No. 5, 1990,
    /// pp. 271-289.
    /// </para>
    /// </remarks>
    public double GetDiffuseSolarIrradiance(
        IReadOnlySun sun, double albedo, SkyDiffuseModel model)
    {
      double groundReflected = albedo * ConfigurationFactorToGround
                               * sun.GlobalHorizontalRadiation;
      double skyDiffuse = model switch
      {
        SkyDiffuseModel.Isotropic =>
            ConfigurationFactorToSky * sun.DiffuseHorizontalRadiation,
        SkyDiffuseModel.Perez => Sky.GetPerezSkyDiffuseOnPlane(
            directNormalRadiation: sun.DirectNormalRadiation,
            diffuseHorizontalRadiation: sun.DiffuseHorizontalRadiation,
            surfaceTilt: VerticalAngle,
            cosIncidenceAngle: GetDirectSolarRadiationRatio(sun),
            solarZenith: 0.5 * Math.PI - sun.Altitude,
            airMass: Sun.GetAirMass(sun.Altitude),
            extraterrestrialNormalRadiation: sun.GetExtraterrestrialRadiation()),
        _ => throw new Popolo.Core.Exceptions.PopoloArgumentException(
            $"Unknown SkyDiffuseModel: {model}", nameof(model)),
      };
      return skyDiffuse + groundReflected;
    }

    /// <summary>
    /// Gets the total solar irradiance on the tilted surface [W/m²].
    /// </summary>
    /// <param name="sun">Solar state</param>
    /// <param name="albedo">Ground reflectance [-]</param>
    /// <returns>Total solar irradiance [W/m²]</returns>
    public double GetSolarIrradiance(IReadOnlySun sun, double albedo)
        => GetDirectSolarIrradiance(sun) + GetDiffuseSolarIrradiance(sun, albedo);

    #endregion

    #region Illuminance on inclined surfaces

    /// <summary>
    /// Gets the direct solar illuminance on the tilted surface [lx].
    /// </summary>
    /// <param name="sun">Solar state</param>
    /// <returns>Direct solar illuminance [lx]</returns>
    public double GetDirectSolarIlluminance(IReadOnlySun sun)
        => GetDirectSolarRadiationRatio(sun) * sun.DirectNormalIlluminance;

    /// <summary>
    /// Gets the diffuse solar illuminance on the tilted surface [lx].
    /// </summary>
    /// <param name="sun">Solar state</param>
    /// <param name="albedo">Ground reflectance [-]</param>
    /// <returns>Diffuse solar illuminance [lx]</returns>
    public double GetDiffuseSolarIlluminance(IReadOnlySun sun, double albedo)
        => ConfigurationFactorToSky * sun.DiffuseIlluminance
           + albedo * ConfigurationFactorToGround * sun.GlobalHorizontalIlluminance;

    #endregion

    #region Profile angle calculation

    /// <summary>
    /// Gets the tangent of the profile angle (apparent solar altitude) [-].
    /// </summary>
    /// <param name="altitude">Solar altitude [radian]</param>
    /// <param name="orientation">Solar azimuth [radian]</param>
    /// <returns>Tangent of profile angle [-]</returns>
    public double GetTangentProfileAngle(double altitude, double orientation)
    {
      double cosTheta = GetDirectSolarRadiationRatio(altitude, orientation);
      if (cosTheta <= 0) return -Math.PI;
      return (Math.Sin(altitude) * _sinBeta
          - Math.Cos(altitude) * _cosBeta
          * (Math.Sin(orientation) * _sinAlpha - Math.Cos(orientation) * _cosAlpha))
          / cosTheta;
    }

    /// <summary>
    /// Gets the tangent of the profile angle (apparent solar altitude) [-].
    /// </summary>
    /// <param name="sun">Solar state</param>
    /// <returns>Tangent of profile angle [-]</returns>
    public double GetTangentProfileAngle(IReadOnlySun sun)
        => GetTangentProfileAngle(sun.Altitude, sun.Azimuth);

    /// <summary>
    /// Gets the profile angle (apparent solar altitude) [radian].
    /// </summary>
    /// <param name="altitude">Solar altitude [radian]</param>
    /// <param name="orientation">Solar azimuth [radian]</param>
    /// <returns>Profile angle [radian]</returns>
    public double GetProfileAngle(double altitude, double orientation)
        => Math.Atan(GetTangentProfileAngle(altitude, orientation));

    /// <summary>
    /// Gets the profile angle (apparent solar altitude) [radian].
    /// </summary>
    /// <param name="sun">Solar state</param>
    /// <returns>Profile angle [radian]</returns>
    public double GetProfileAngle(IReadOnlySun sun)
        => GetProfileAngle(sun.Altitude, sun.Azimuth);

    #endregion

    #region Other instance methods

    /// <summary>
    /// Creates a new <see cref="Incline"/> facing the reverse direction.
    /// </summary>
    /// <returns>A reversed incline.</returns>
    public Incline MakeReverseIncline()
        => new Incline(HorizontalAngle + Math.PI, VerticalAngle + Math.PI);

    /// <summary>
    /// Copies the orientation state from another <see cref="IReadOnlyIncline"/>.
    /// </summary>
    /// <param name="incline">The source incline to copy from.</param>
    public void Copy(IReadOnlyIncline incline)
    {
      HorizontalAngle = incline.HorizontalAngle;
      VerticalAngle = incline.VerticalAngle;
      ConfigurationFactorToSky = incline.ConfigurationFactorToSky;
      _sinBeta = Math.Sin(VerticalAngle);
      _cosBeta = Math.Cos(VerticalAngle);
      _sinAlpha = Math.Sin(HorizontalAngle);
      _cosAlpha = Math.Cos(HorizontalAngle);
    }

    #endregion

    #region Static methods

    /// <summary>
    /// Gets the view factor from a tilted surface to the sky [-].
    /// </summary>
    /// <param name="verticalAngle">Tilt angle [radian] (0 = horizontal)</param>
    /// <returns>View factor to sky [-]</returns>
    public static double GetConfigurationFactorToSky(double verticalAngle)
        => (1.0 + Math.Cos(verticalAngle)) / 2.0;

    /// <summary>
    /// Gets the cosine of the angle of incidence of direct solar radiation
    /// on a tilted surface (cosθ) [-].
    /// </summary>
    /// <param name="horizontalAngle">Surface azimuth [radian] (south=0, east=neg, west=pos)</param>
    /// <param name="verticalAngle">Surface tilt [radian] (horizontal=0, vertical=π/2)</param>
    /// <param name="altitude">Solar altitude [radian]</param>
    /// <param name="orientation">Solar azimuth [radian]</param>
    /// <returns>cosθ [-]</returns>
    public static double GetDirectSolarRadiationRatioToIncline(
        double horizontalAngle, double verticalAngle,
        double altitude, double orientation)
    {
      double sh = Math.Sin(altitude);
      double cb = Math.Cos(verticalAngle);
      double ch = Math.Cos(altitude);
      double sb = Math.Sin(verticalAngle);
      double caa = Math.Cos(orientation - horizontalAngle);
      return sh * cb + ch * sb * caa;
    }

    #endregion

  }
}
