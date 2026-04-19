/* IReadOnlySun.cs
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
 */

using System;

namespace Popolo.Core.Climate
{
  /// <summary>
  /// Represents a read-only view of the instantaneous sun state at a specific
  /// site and moment — solar position plus direct, diffuse, and global
  /// irradiance (and optionally illuminance).
  /// </summary>
  /// <remarks>
  /// <para>
  /// A <see cref="IReadOnlySun"/> bundles everything a building envelope needs
  /// to know about the sun at <see cref="CurrentDateTime"/>:
  /// <list type="bullet">
  ///   <item><description><b>Position</b>: <see cref="Altitude"/> and <see cref="Azimuth"/>, evaluated from <see cref="Latitude"/>, <see cref="Longitude"/>, and <see cref="StandardLongitude"/> (standard-time meridian).</description></item>
  ///   <item><description><b>Irradiance</b>: direct normal (DNI), diffuse horizontal (DHI), and global horizontal (GHI), which are linked by <c>GHI ≈ DNI · sin(altitude) + DHI</c>.</description></item>
  ///   <item><description><b>Illuminance</b>: the same three components converted to [lx] via a luminous efficacy; populated only when <see cref="CalculateIlluminance"/> is enabled.</description></item>
  /// </list>
  /// </para>
  /// <para>
  /// A sun is a time-varying object: the concrete <see cref="Sun"/>
  /// implementation exposes setters that advance <see cref="CurrentDateTime"/>
  /// and recompute the position and irradiance. Consumers such as
  /// <see cref="IReadOnlyIncline"/> and <see cref="Building.Envelope.IReadOnlyWindow"/>
  /// read the state through this interface to compute tilted-surface
  /// irradiance, incidence angles, and shading-device profile angles. The
  /// site parameters (lat / lon / standard meridian) are fixed for the life
  /// of the sun instance.
  /// </para>
  /// </remarks>
  public interface IReadOnlySun
  {
    /// <summary>Gets the solar altitude angle [radian].</summary>
    double Altitude { get; }

    /// <summary>Gets the solar azimuth angle [radian].</summary>
    double Azimuth { get; }

    /// <summary>Gets the direct normal irradiance (DNI) [W/m²].</summary>
    double DirectNormalRadiation { get; }

    /// <summary>Gets the diffuse horizontal irradiance (DHI) [W/m²].</summary>
    double DiffuseHorizontalRadiation { get; }

    /// <summary>Gets the global horizontal irradiance (GHI) [W/m²].</summary>
    double GlobalHorizontalRadiation { get; }

    /// <summary>Gets the latitude of the calculation site (positive north) [degree].</summary>
    double Latitude { get; }

    /// <summary>Gets the longitude of the calculation site (positive east) [degree].</summary>
    double Longitude { get; }

    /// <summary>Gets the longitude of the standard time meridian (positive east) [degree].</summary>
    double StandardLongitude { get; }

    /// <summary>Gets the current date and time.</summary>
    DateTime CurrentDateTime { get; }

    /// <summary>Gets the direct normal illuminance [lx].</summary>
    double DirectNormalIlluminance { get; }

    /// <summary>Gets the diffuse horizontal illuminance [lx].</summary>
    double DiffuseIlluminance { get; }

    /// <summary>Gets the global horizontal illuminance [lx].</summary>
    double GlobalHorizontalIlluminance { get; }

    /// <summary>Gets a value indicating whether illuminance calculation is enabled.</summary>
    bool CalculateIlluminance { get; }

    /// <summary>
    /// Gets the extraterrestrial radiation [W/m²] for the current date.
    /// </summary>
    /// <returns>Extraterrestrial radiation [W/m²]</returns>
    double GetExtraterrestrialRadiation();

    /// <summary>Gets the sunrise time for the current date.</summary>
    /// <returns>Sunrise time</returns>
    DateTime GetSunRiseTime();

    /// <summary>Gets the sunset time for the current date.</summary>
    /// <returns>Sunset time</returns>
    DateTime GetSunSetTime();
  }
}
