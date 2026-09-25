/* WebproConversionConstants.cs
 *
 * Copyright (C) 2026 E.Togashi
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
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
