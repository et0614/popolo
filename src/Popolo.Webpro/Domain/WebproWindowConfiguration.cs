/* WebproWindowConfiguration.cs
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
