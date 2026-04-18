/* WebproWallLayer.cs
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
  /// Data transfer object representing a single layer within a WEBPRO wall
  /// construction (<c>WallConfigure.layers[i]</c>).
  /// </summary>
  /// <remarks>
  /// <para>
  /// Corresponds to the legacy <c>WebproSingleWallConfigureJson</c> type in
  /// Popolo v2.3. This is a pure POCO — JSON parsing lives in
  /// <see cref="Popolo.Webpro.Json.WebproWallLayerJsonConverter"/>, and
  /// domain conversion to a Popolo.Core <c>WallLayer</c> lives in the
  /// Conversion layer.
  /// </para>
  /// <para>
  /// <b>Unit note:</b> <see cref="Thickness"/> is in <b>millimetres</b>,
  /// matching the raw WEBPRO JSON unit.
  /// </para>
  /// </remarks>
  public sealed class WebproWallLayer
  {
    /// <summary>
    /// Gets or sets the material ID referenced from the WEBPRO material catalog
    /// (e.g. <c>"コンクリート"</c>, <c>"非密閉中空層"</c>, <c>"土壌"</c>).
    /// </summary>
    public string MaterialID { get; set; } = "";

    /// <summary>
    /// Gets or sets an explicitly given thermal conductivity [W/(m·K)], or null
    /// if the catalog's default for the material is to be used.
    /// </summary>
    /// <remarks>
    /// In practice WEBPRO files rarely supply this value; it is overriding
    /// metadata that callers may use when interpreting the layer.
    /// </remarks>
    public double? Conductivity { get; set; }

    /// <summary>
    /// Gets or sets the layer thickness in <b>millimetres</b>, or null when
    /// inapplicable (e.g. air-gap materials whose thickness is fixed by the
    /// catalog).
    /// </summary>
    public double? Thickness { get; set; }

    /// <summary>
    /// Gets or sets a free-form remark string from the WEBPRO JSON <c>Info</c>
    /// property, or null if absent.
    /// </summary>
    public string? Information { get; set; }
  }
}
