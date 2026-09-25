/* WebproWall.cs
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
  /// but is retained for forward compatibility. The converter currently uses
  /// it only as a fallback when the referenced configuration uses
  /// <see cref="WallInputMethod.HeatTransferCoefficient"/> but carries no
  /// configuration-level U-value.
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
