/* WebproWallLayer.cs
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
    /// In practice WEBPRO files rarely supply this value. When given, the
    /// converter uses it instead of the catalog conductivity for solid and
    /// soil materials (air-gap resistances stay fixed by the catalog).
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
