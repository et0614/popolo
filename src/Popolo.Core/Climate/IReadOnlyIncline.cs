/* IReadOnlyIncline.cs
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

namespace Popolo.Core.Climate
{
  /// <summary>
  /// Represents a read-only view of a tilted surface orientation — the
  /// outward-facing direction of a wall, window, roof, or photovoltaic panel
  /// — and the solar-radiation queries associated with it.
  /// </summary>
  /// <remarks>
  /// <para>
  /// An incline carries two angles: a <b>horizontal angle</b> (azimuth) and a
  /// <b>vertical angle</b> (tilt from horizontal), which together define a
  /// surface normal in the local sky dome. The sign and zero conventions are:
  /// <list type="bullet">
  ///   <item><description>Horizontal angle [radian]: <b>south = 0</b>, east = negative, west = positive, north = ±π.</description></item>
  ///   <item><description>Vertical angle [radian]: 0 = upward-facing horizontal surface (e.g., a flat roof), π/2 = vertical surface (e.g., a wall), π = downward-facing horizontal surface (e.g., an overhang soffit).</description></item>
  /// </list>
  /// From these two angles, the <see cref="ConfigurationFactorToSky"/> and
  /// its complement <see cref="ConfigurationFactorToGround"/> are derived for
  /// the diffuse-irradiance and long-wave-radiation balance.
  /// </para>
  /// <para>
  /// In the building thermal model, every exterior envelope element that sees
  /// the sun carries an <see cref="IReadOnlyIncline"/>:
  /// <see cref="Building.Envelope.IReadOnlyWindow.OutsideIncline"/>,
  /// outdoor-facing walls through
  /// <see cref="Building.Envelope.OutsideWallReference"/>, photovoltaic panels,
  /// and so on. The <c>GetDirectSolarIrradiance</c> /
  /// <c>GetDiffuseSolarIrradiance</c> methods combine the surface orientation
  /// with a <see cref="IReadOnlySun"/> to produce the tilted-surface
  /// irradiance that drives sol-air temperatures on those surfaces. The
  /// profile-angle methods compute the apparent solar altitude used by slat
  /// / overhang shading calculations.
  /// </para>
  /// </remarks>
  public interface IReadOnlyIncline
  {
    /// <summary>
    /// Gets the horizontal (azimuth) angle [radian].
    /// South = 0, east = negative, west = positive.
    /// </summary>
    double HorizontalAngle { get; }

    /// <summary>
    /// Gets the vertical (tilt) angle [radian].
    /// Horizontal = 0, vertical = π/2.
    /// </summary>
    double VerticalAngle { get; }

    /// <summary>Gets the view factor from the surface to the sky [-].</summary>
    double ConfigurationFactorToSky { get; }

    /// <summary>Gets the view factor from the surface to the ground [-].</summary>
    double ConfigurationFactorToGround { get; }

    /// <summary>
    /// Creates a new <see cref="Incline"/> facing the reverse direction.
    /// </summary>
    /// <returns>A reversed incline.</returns>
    Incline MakeReverseIncline();

    /// <summary>
    /// Gets the cosine of the angle of incidence of direct solar radiation
    /// on the surface normal (cosθ) [-].
    /// </summary>
    /// <param name="altitude">Solar altitude [radian]</param>
    /// <param name="orientation">Solar azimuth [radian]</param>
    /// <returns>cosθ [-] (clamped to 0 when the sun is behind the surface)</returns>
    double GetDirectSolarRadiationRatio(double altitude, double orientation);

    /// <summary>
    /// Gets the cosine of the angle of incidence of direct solar radiation
    /// on the surface normal (cosθ) [-].
    /// </summary>
    /// <param name="sun">Solar state</param>
    /// <returns>cosθ [-]</returns>
    double GetDirectSolarRadiationRatio(IReadOnlySun sun);

    /// <summary>
    /// Gets the direct solar irradiance on the tilted surface [W/m²].
    /// </summary>
    /// <param name="sun">Solar state</param>
    /// <returns>Direct solar irradiance [W/m²]</returns>
    double GetDirectSolarIrradiance(IReadOnlySun sun);

    /// <summary>
    /// Gets the diffuse solar irradiance on the tilted surface [W/m²],
    /// including sky diffuse and ground-reflected components.
    /// </summary>
    /// <param name="sun">Solar state</param>
    /// <param name="albedo">Ground reflectance [-]</param>
    /// <returns>Diffuse solar irradiance [W/m²]</returns>
    double GetDiffuseSolarIrradiance(IReadOnlySun sun, double albedo);

    /// <summary>
    /// Gets the total solar irradiance on the tilted surface [W/m²].
    /// </summary>
    /// <param name="sun">Solar state</param>
    /// <param name="albedo">Ground reflectance [-]</param>
    /// <returns>Total solar irradiance [W/m²]</returns>
    double GetSolarIrradiance(IReadOnlySun sun, double albedo);

    /// <summary>
    /// Gets the direct solar illuminance on the tilted surface [lx].
    /// </summary>
    /// <param name="sun">Solar state</param>
    /// <returns>Direct solar illuminance [lx]</returns>
    double GetDirectSolarIlluminance(IReadOnlySun sun);

    /// <summary>
    /// Gets the diffuse solar illuminance on the tilted surface [lx].
    /// </summary>
    /// <param name="sun">Solar state</param>
    /// <param name="albedo">Ground reflectance [-]</param>
    /// <returns>Diffuse solar illuminance [lx]</returns>
    double GetDiffuseSolarIlluminance(IReadOnlySun sun, double albedo);

    /// <summary>
    /// Gets the tangent of the profile angle (apparent solar altitude) [-].
    /// </summary>
    /// <param name="altitude">Solar altitude [radian]</param>
    /// <param name="orientation">Solar azimuth [radian]</param>
    /// <returns>Tangent of profile angle [-]</returns>
    double GetTangentProfileAngle(double altitude, double orientation);

    /// <summary>
    /// Gets the tangent of the profile angle (apparent solar altitude) [-].
    /// </summary>
    /// <param name="sun">Solar state</param>
    /// <returns>Tangent of profile angle [-]</returns>
    double GetTangentProfileAngle(IReadOnlySun sun);

    /// <summary>
    /// Gets the profile angle (apparent solar altitude) [radian].
    /// </summary>
    /// <param name="altitude">Solar altitude [radian]</param>
    /// <param name="orientation">Solar azimuth [radian]</param>
    /// <returns>Profile angle [radian]</returns>
    double GetProfileAngle(double altitude, double orientation);

    /// <summary>
    /// Gets the profile angle (apparent solar altitude) [radian].
    /// </summary>
    /// <param name="sun">Solar state</param>
    /// <returns>Profile angle [radian]</returns>
    double GetProfileAngle(IReadOnlySun sun);
  }
}
