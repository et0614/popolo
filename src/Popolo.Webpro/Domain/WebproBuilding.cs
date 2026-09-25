/* WebproBuilding.cs
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

namespace Popolo.Webpro.Domain
{
  /// <summary>
  /// Data transfer object representing the top-level <c>Building</c> block of a
  /// WEBPRO input file.
  /// </summary>
  /// <remarks>
  /// <para>
  /// Only fields relevant to thermal load calculation are carried on the DTO.
  /// Fields such as <c>BuildingAddress</c> and <c>Coefficient_DHC</c> (used by
  /// the WEBPRO HVAC/DHW/lighting subsystems) are intentionally skipped by the
  /// converter — see <see cref="Popolo.Webpro.Json.WebproBuildingJsonConverter"/>.
  /// </para>
  /// <para>
  /// The key field for thermal calculation is <see cref="Region"/>, which
  /// selects the Japanese climatic region (1–8).
  /// </para>
  /// </remarks>
  public sealed class WebproBuilding
  {
    /// <summary>Gets or sets the building name (free-form Japanese text).</summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the Japanese climatic region code (<c>"1"</c>..<c>"8"</c>).
    /// </summary>
    /// <remarks>
    /// Stored as a string because the WEBPRO JSON encodes it that way, though
    /// the semantic is numeric. Parse to int at the call site if needed.
    /// </remarks>
    public string Region { get; set; } = "";

    /// <summary>Gets or sets the annual-solar-radiation region code (e.g. <c>"A3"</c>).</summary>
    public string? AnnualSolarRegion { get; set; }

    /// <summary>Gets or sets the total building floor area [m²].</summary>
    public double? FloorArea { get; set; }
  }
}
