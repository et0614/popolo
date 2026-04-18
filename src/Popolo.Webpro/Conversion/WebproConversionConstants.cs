/* WebproConversionConstants.cs
 *
 * Copyright (C) 2026 E.Togashi
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

namespace Popolo.Webpro.Conversion
{
  /// <summary>
  /// Constants used when converting a <see cref="Domain.WebproModel"/> into a
  /// Popolo.Core <c>BuildingThermalModel</c>.
  /// </summary>
  /// <remarks>
  /// These values reproduce the defaults used by the legacy Popolo v2.3
  /// WEBPRO reader. They encode assumptions specific to the simplified
  /// Japanese energy-code (省エネ法) thermal calculation and should not be
  /// used outside that context.
  /// </remarks>
  public static class WebproConversionConstants
  {
    /// <summary>
    /// Thermal conductance between the soil and the inside wall surface for
    /// ground-contact walls [W/(m²·K)].
    /// </summary>
    /// <remarks>
    /// Passed to <c>MultiRooms.SetGroundWall(wall, isSideF, value)</c>. The
    /// soil mass itself is represented by a thin layer inside the wall
    /// construction; this coefficient represents the equivalent surface
    /// resistance between soil and wall.
    /// </remarks>
    public const double GroundWallConductance = 0.1;

    /// <summary>
    /// Adjacent-space temperature difference factor [-] for interior walls.
    /// </summary>
    /// <remarks>
    /// Passed to <c>MultiRooms.UseAdjacentSpaceFactor(wall, isSideF, value)</c>.
    /// Represents the fraction of the outdoor-indoor temperature difference
    /// experienced by the unconditioned adjacent space. Legacy default.
    /// </remarks>
    public const double AdjacentSpaceFactor = 0.4;

    /// <summary>
    /// Additional sensible heat capacity per unit floor area representing
    /// furniture and interior objects [J/(K·m²)].
    /// </summary>
    /// <remarks>
    /// Added to <c>Zone.HeatCapacity</c> during conversion. Represents the
    /// thermal inertia of objects that are not modelled explicitly.
    /// </remarks>
    public const double ZoneHeatCapacityRate = 10000;

    /// <summary>Air density at reference conditions [kg/m³].</summary>
    public const double AirDensity = 1.2;

    /// <summary>
    /// Solar absorption ratio of the outside wall surface [-] used when the
    /// WEBPRO JSON <c>solarAbsorptionRatio</c> field is null.
    /// </summary>
    public const double DefaultSolarAbsorptionRatio = 0.7;

    /// <summary>Default window frame ratio [-] (frame area / overall window area).</summary>
    /// <remarks>
    /// Applied when the converter needs to reduce the glazing area to account
    /// for the opaque frame. Legacy value.
    /// </remarks>
    public const double DefaultFrameRatio = 0.2;

    /// <summary>
    /// Sentinel string appearing in WEBPRO JSON meaning "no window placed at
    /// this location".
    /// </summary>
    public const string NoWindowSentinel = "無";
  }
}
