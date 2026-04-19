/* WebproWallConfiguration.cs
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

using System.Collections.Generic;

using Popolo.Webpro.Domain.Enums;

namespace Popolo.Webpro.Domain
{
  /// <summary>
  /// Data transfer object representing a named WEBPRO wall construction
  /// (<c>WallConfiguration[key]</c> in the input JSON).
  /// </summary>
  /// <remarks>
  /// <para>
  /// Corresponds to the legacy <c>WebproWallConfigureJson</c> type in
  /// Popolo v2.3. This is a pure POCO — JSON parsing lives in
  /// <see cref="Popolo.Webpro.Json.WebproWallConfigurationJsonConverter"/>.
  /// </para>
  /// <para>
  /// The <c>wall_type_webpro</c> property sometimes present in WEBPRO JSON
  /// (e.g. <c>"外壁"</c>, <c>"接地壁"</c>) is intentionally ignored by the
  /// converter; the wall's exterior/interior role is determined at the
  /// envelope level via <see cref="WallType"/> instead.
  /// </para>
  /// </remarks>
  public sealed class WebproWallConfiguration
  {
    /// <summary>Gets or sets the structural classification (木造, 鉄筋コンクリート造等, etc.).</summary>
    /// <remarks>
    /// Defaults to <see cref="StructureType.None"/> when the JSON omits the
    /// <c>structureType</c> property (or provides a null value).
    /// </remarks>
    public StructureType Structure { get; set; } = StructureType.None;

    /// <summary>
    /// Gets or sets the solar absorption ratio of the outside surface [-], or
    /// null when unspecified.
    /// </summary>
    public double? SolarAbsorptionRatio { get; set; }

    /// <summary>Gets or sets the method used to describe the thermal performance.</summary>
    /// <remarks>
    /// Defaults to <see cref="WallInputMethod.None"/> when the JSON omits the
    /// <c>inputMethod</c> property (or provides a null value).
    /// </remarks>
    public WallInputMethod Method { get; set; } = WallInputMethod.None;

    /// <summary>
    /// Gets the layered construction of the wall, from outside to inside.
    /// </summary>
    /// <remarks>
    /// Empty by default. The list is populated by the JSON converter based on
    /// the <c>layers</c> array in the WEBPRO JSON.
    /// </remarks>
    public List<WebproWallLayer> Layers { get; } = new List<WebproWallLayer>();

    /// <summary>Gets or sets a free-form remark string from the JSON <c>Info</c> property.</summary>
    public string? Information { get; set; }
  }
}
