/* WebproWindowConfiguration.cs
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

using Popolo.Webpro.Domain.Enums;

namespace Popolo.Webpro.Domain
{
  /// <summary>
  /// Data transfer object representing a named WEBPRO window specification
  /// (<c>WindowConfigure[key]</c> in the input JSON).
  /// </summary>
  /// <remarks>
  /// <para>
  /// Corresponds to the legacy <c>WebproWindowConfigureJson</c> in
  /// Popolo v2.3. This is a pure POCO — JSON parsing lives in
  /// <see cref="Popolo.Webpro.Json.WebproWindowConfigurationJsonConverter"/>.
  /// </para>
  /// <para>
  /// <b>NaN convention for missing numeric fields:</b> WEBPRO JSON often
  /// omits numeric fields or encodes them as <c>null</c> when the
  /// corresponding quantity is not relevant to the chosen
  /// <see cref="Method"/>. To preserve this intent without introducing
  /// nullable doubles throughout, missing or null numeric fields are
  /// stored as <see cref="double.NaN"/>, matching the legacy Popolo v2.3
  /// convention. Callers should use <see cref="double.IsNaN(double)"/> to
  /// test for the unset state before consuming these values.
  /// </para>
  /// </remarks>
  public sealed class WebproWindowConfiguration
  {
    /// <summary>Gets or sets the representative window area [m²].</summary>
    public double Area { get; set; }

    /// <summary>Gets or sets the representative window width [m].</summary>
    public double Width { get; set; }

    /// <summary>Gets or sets the representative window height [m].</summary>
    public double Height { get; set; }

    /// <summary>Gets or sets the method used to describe the thermal performance.</summary>
    public WindowInputMethod Method { get; set; } = WindowInputMethod.None;

    /// <summary>Gets or sets the frame material classification.</summary>
    public WindowFrame Frame { get; set; } = WindowFrame.None;

    /// <summary>Gets or sets the glazing ID as referenced from the WEBPRO glazing catalog.</summary>
    /// <remarks>
    /// Typical values are short codes such as <c>"3WgG06"</c>, <c>"T"</c>, or
    /// <c>"S"</c>. See <see cref="Popolo.Webpro.Conversion.GlazingCatalog"/>.
    /// </remarks>
    public string GlazingID { get; set; } = "";

    /// <summary>
    /// Gets or sets a value indicating whether the glazing is a single pane.
    /// Derived from the WEBPRO <c>layerType</c> property: <c>true</c> iff the
    /// JSON value is <c>"単層"</c>; otherwise <c>false</c>.
    /// </summary>
    public bool IsSingleGlazing { get; set; }

    /// <summary>Gets or sets the glazing heat transfer coefficient [W/(m²·K)], or <see cref="double.NaN"/> if unspecified.</summary>
    public double GlazingHeatTransferCoefficient { get; set; } = double.NaN;

    /// <summary>Gets or sets the glazing solar heat gain rate [-], or <see cref="double.NaN"/> if unspecified.</summary>
    public double GlazingSolarHeatGainRate { get; set; } = double.NaN;

    /// <summary>Gets or sets the overall window heat transfer coefficient [W/(m²·K)], or <see cref="double.NaN"/> if unspecified.</summary>
    public double WindowHeatTransferCoefficient { get; set; } = double.NaN;

    /// <summary>Gets or sets the overall window solar heat gain rate [-], or <see cref="double.NaN"/> if unspecified.</summary>
    public double WindowSolarHeatGainRate { get; set; } = double.NaN;

    /// <summary>Gets or sets a free-form remark string from the JSON <c>Info</c> property.</summary>
    public string? Information { get; set; }
  }
}
