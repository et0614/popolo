/* WebproWindow.cs
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
  /// Data transfer object representing a single window placement entry within
  /// a wall's <c>WindowList</c>.
  /// </summary>
  /// <remarks>
  /// <para>
  /// Corresponds to the legacy <c>WebproWindowJson</c> in Popolo v2.3. This
  /// is a pure POCO — JSON parsing lives in
  /// <see cref="Popolo.Webpro.Json.WebproWindowJsonConverter"/>.
  /// </para>
  /// <para>
  /// <b>Sentinel ID "無":</b> When <see cref="ID"/> is the Japanese string
  /// <c>"無"</c> (meaning "none"), the placement indicates that the wall has
  /// no window. This class preserves the raw ID; higher-level conversion logic
  /// decides whether to skip the placement.
  /// </para>
  /// <para>
  /// <b>Number field:</b> The <c>WindowNumber</c> JSON property is the
  /// <i>number of windows</i> (枚数) of the referenced specification placed on
  /// the wall. Following builelib, the placed window area is
  /// <c>WindowConfigure[ID].windowArea × WindowNumber</c>, where
  /// <c>windowArea</c> falls back to <c>windowWidth × windowHeight</c> when
  /// not given. Because many WEBPRO files use <c>windowArea = 1</c>, the
  /// count frequently coincides numerically with the area in m².
  /// </para>
  /// </remarks>
  public sealed class WebproWindow
  {
    /// <summary>Gets or sets the window ID referenced from the enclosing model's <c>WindowConfigure</c> dictionary.</summary>
    /// <remarks>The value <c>"無"</c> acts as a sentinel meaning no window is placed at this slot.</remarks>
    public string ID { get; set; } = "";

    /// <summary>Gets or sets the number of windows (count, 枚数) [-], or null if unspecified.</summary>
    /// <remarks>
    /// Read from the WEBPRO JSON property <c>WindowNumber</c>. The placed
    /// window area is this count multiplied by the per-window area of the
    /// referenced <see cref="WebproWindowConfiguration"/>. The converter treats
    /// null as a single window. The value may be fractional in real files.
    /// </remarks>
    public double? Number { get; set; }

    /// <summary>Gets or sets a value indicating whether a blind/curtain is installed.</summary>
    /// <remarks>Parsed from the <c>isBlind</c> JSON property: <c>"有"</c> → true, <c>"無"</c> → false.</remarks>
    public bool HasBlind { get; set; }

    /// <summary>Gets or sets the eaves/sunshade ID, or <c>"無"</c> if none.</summary>
    public string EavesID { get; set; } = "";

    /// <summary>Gets or sets a free-form remark string from the JSON <c>Info</c> property.</summary>
    public string? Information { get; set; }
  }
}
