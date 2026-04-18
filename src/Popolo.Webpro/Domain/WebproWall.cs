/* WebproWall.cs
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
  /// Data transfer object representing a single wall entry within an envelope
  /// set's <c>WallList</c>.
  /// </summary>
  /// <remarks>
  /// <para>
  /// Each <see cref="WebproWall"/> corresponds to one face of a room's
  /// enclosing envelope — a specific direction, area, and construction type.
  /// </para>
  /// <para>
  /// <b>Dimensions:</b> <see cref="Area"/> is typically supplied directly; when
  /// it is null, the caller may compute it from <see cref="Width"/> ×
  /// <see cref="Height"/>. In practice, WEBPRO files observed so far always
  /// populate <see cref="Area"/> and leave width/height null.
  /// </para>
  /// <para>
  /// <b>U-value fallback:</b> <see cref="HeatTransferCoefficient"/> holds an
  /// optional U-value [W/(m²·K)] read from the WEBPRO JSON <c>Uvalue</c> key
  /// if present. The default <see cref="double.NaN"/> indicates that no
  /// override is provided; the converter should then resolve the U-value
  /// from the referenced <see cref="WallSpec"/> via the <c>WallConfigure</c>
  /// catalog. The <c>Uvalue</c> key is not present in typical WEBPRO output
  /// but is retained for forward compatibility.
  /// </para>
  /// </remarks>
  public sealed class WebproWall
  {
    /// <summary>Gets or sets the surface orientation (方位).</summary>
    /// <remarks>Corresponds to the WEBPRO JSON property <c>Direction</c>.</remarks>
    public Orientation SurfaceOrientation { get; set; }

    /// <summary>Gets or sets the wall surface area [m²], or null if to be derived from width × height.</summary>
    public double? Area { get; set; }

    /// <summary>Gets or sets the wall surface width [m], or null if not specified.</summary>
    public double? Width { get; set; }

    /// <summary>Gets or sets the wall surface height [m], or null if not specified.</summary>
    public double? Height { get; set; }

    /// <summary>Gets or sets the wall specification ID, referring to an entry in the top-level <c>WallConfigure</c> dictionary.</summary>
    public string WallSpec { get; set; } = "";

    /// <summary>Gets or sets the wall classification (external / shading external / ground / inner).</summary>
    public WallType Type { get; set; }

    /// <summary>Gets or sets the overridden U-value [W/(m²·K)], or <see cref="double.NaN"/> if the catalog default should be used.</summary>
    public double HeatTransferCoefficient { get; set; } = double.NaN;

    /// <summary>Gets the collection of window placements on this wall.</summary>
    public List<WebproWindow> Windows { get; } = new List<WebproWindow>();
  }
}
