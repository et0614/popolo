/* WebproWallConfiguration.cs
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
    /// Gets or sets the U-value of the whole construction [W/(m²·K)]
    /// (surface resistances included), or <see cref="double.NaN"/> if
    /// unspecified.
    /// </summary>
    /// <remarks>
    /// Read from the WallConfigure-level <c>Uvalue</c> JSON key (builelib
    /// convention). Only meaningful when <see cref="Method"/> is
    /// <see cref="WallInputMethod.HeatTransferCoefficient"/> (熱貫流率を入力),
    /// in which case <see cref="Layers"/> is normally empty.
    /// </remarks>
    public double HeatTransferCoefficient { get; set; } = double.NaN;

    /// <summary>
    /// Gets the layered construction of the wall, listed from the
    /// <b>room (inside) side to the outdoor side</b>, as in the WEBPRO input.
    /// </summary>
    /// <remarks>
    /// Empty by default. The list is populated by the JSON converter based on
    /// the <c>layers</c> array in the WEBPRO JSON. WEBPRO/builelib lists the
    /// room-side finish first (e.g. gypsum board … tile for an exterior wall,
    /// vinyl flooring … concrete for a floor). The converter reverses this
    /// order because Popolo walls are built with layer 0 on the outdoor
    /// (F) side.
    /// </remarks>
    public List<WebproWallLayer> Layers { get; } = new List<WebproWallLayer>();

    /// <summary>Gets or sets a free-form remark string from the JSON <c>Info</c> property.</summary>
    public string? Information { get; set; }
  }
}
