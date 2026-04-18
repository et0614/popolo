/* WebproWindow.cs
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
  /// <b>Number field:</b> Despite its name, WEBPRO uses the <c>WindowNumber</c>
  /// JSON property to encode <i>window area</i> in m², not a count. The field
  /// is preserved as-is; callers should treat it as area.
  /// </para>
  /// </remarks>
  public sealed class WebproWindow
  {
    /// <summary>Gets or sets the window ID referenced from the enclosing model's <c>WindowConfigure</c> dictionary.</summary>
    /// <remarks>The value <c>"無"</c> acts as a sentinel meaning no window is placed at this slot.</remarks>
    public string ID { get; set; } = "";

    /// <summary>Gets or sets the total window area [m²], or null if unspecified.</summary>
    /// <remarks>
    /// The property is named <c>WindowNumber</c> in the WEBPRO JSON but
    /// semantically represents area, not a count.
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
