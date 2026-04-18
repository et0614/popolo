/* WebproBuilding.cs
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
