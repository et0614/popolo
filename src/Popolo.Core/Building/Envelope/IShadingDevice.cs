/* IShadingDevice.cs
 *
 * Copyright (C) 2016 E.Togashi
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

namespace Popolo.Core.Building.Envelope
{
  /// <summary>
  /// Represents an interior solar shading device placed inside the window assembly
  /// — either between glazing layers (e.g., a blind embedded in the air gap) or
  /// on the indoor side of the innermost glazing.
  /// </summary>
  /// <remarks>
  /// <para>
  /// Interior shading devices modify the optical path of solar radiation through
  /// a <see cref="Window"/> by attenuating and redirecting transmitted light and
  /// absorbing or reflecting the rest. They are positioned by layer index in the
  /// window, where 0 is the outdoor side and N+1 is the indoor side; a
  /// <see cref="NoShadingDevice"/> is installed by default at every position.
  /// For <b>exterior</b> shading (overhangs, fins, louvers mounted outside the
  /// window), use <see cref="SunShade"/> instead.
  /// </para>
  /// <para>
  /// Implementations evaluate transmittance and reflectance at runtime, possibly
  /// as functions of solar position (<see cref="ProfileAngle"/>) and slat angle.
  /// <see cref="HasPropertyChanged"/> reports whether optical properties changed
  /// since the last call to <see cref="ComputeOpticalProperties"/>, allowing
  /// callers to skip redundant recomputation in the window solver.
  /// </para>
  /// <para>
  /// Available implementations:
  /// <list type="bullet">
  ///   <item><description><see cref="NoShadingDevice"/> — null object (no shading).</description></item>
  ///   <item><description><see cref="SimpleShadingDevice"/> — constant transmittance and reflectance.</description></item>
  ///   <item><description><see cref="VenetianBlind"/> — adjustable slat angle with ISO 15099 geometry.</description></item>
  /// </list>
  /// The <see cref="Kind"/> discriminator identifies the concrete type and
  /// allows reflection-free serialization.
  /// </para>
  /// </remarks>
  public interface IShadingDevice
  {
    /// <summary>
    /// Gets the discriminator identifying the concrete shading device type.
    /// Used by serializers to distinguish implementations without reflection.
    /// </summary>
    /// <remarks>
    /// Expected values:
    /// <list type="bullet">
    ///   <item><description><c>"noShadingDevice"</c> — null-object (no shading).</description></item>
    ///   <item><description><c>"simpleShadingDevice"</c> — simple constant transmittance/reflectance.</description></item>
    ///   <item><description><c>"venetianBlind"</c> — venetian blind with adjustable slat angle.</description></item>
    /// </list>
    /// Implementations should return their own discriminator.
    /// </remarks>
    string Kind { get; }

    /// <summary>
    /// Gets or sets a value indicating whether the shading device is deployed (pulled down).
    /// </summary>
    bool Pulldowned { get; set; }

    /// <summary>
    /// Gets a value indicating whether the optical properties have changed
    /// since the last call to <see cref="ComputeOpticalProperties"/>.
    /// </summary>
    bool HasPropertyChanged { get; }

    /// <summary>
    /// Gets or sets the profile angle (apparent solar altitude) [radian].
    /// </summary>
    double ProfileAngle { get; set; }

    /// <summary>
    /// Computes the optical properties of the shading device.
    /// </summary>
    /// <param name="isDiffuseIrradianceProperties">
    /// True to compute properties for diffuse irradiance; false for direct irradiance.
    /// </param>
    /// <param name="irradianceFromSideF">
    /// True if the irradiance is incident from the F side (outdoor side).
    /// </param>
    /// <param name="transmittance">Transmittance [-]</param>
    /// <param name="reflectance">Reflectance [-]</param>
    void ComputeOpticalProperties(
        bool isDiffuseIrradianceProperties, bool irradianceFromSideF,
        out double transmittance, out double reflectance);
  }

  /// <summary>
  /// A null-object implementation of <see cref="IShadingDevice"/> that represents
  /// the absence of any shading device (transmittance = 1, reflectance = 0).
  /// </summary>
  /// <remarks>
  /// Used as the default occupant of every shading slot in a <see cref="Window"/>
  /// so that the solver can treat all positions uniformly without null checks.
  /// Swap in a real <see cref="IShadingDevice"/> (e.g., <see cref="SimpleShadingDevice"/>
  /// or <see cref="VenetianBlind"/>) via <c>Window.SetShadingDevice</c> to activate shading.
  /// </remarks>
  public class NoShadingDevice : IShadingDevice
  {
    /// <summary>Gets the discriminator; always <c>"noShadingDevice"</c>.</summary>
    public string Kind => "noShadingDevice";

    /// <summary>
    /// Gets or sets a value indicating whether the device is deployed.
    /// Always false for <see cref="NoShadingDevice"/>.
    /// </summary>
    public bool Pulldowned { get; set; } = false;

    /// <summary>Gets a value indicating whether the optical properties have changed.</summary>
    public bool HasPropertyChanged { get; private set; } = true;

    /// <summary>Gets or sets the profile angle [radian]. Not used by this implementation.</summary>
    public double ProfileAngle { get; set; }

    /// <summary>Initializes a new instance of <see cref="NoShadingDevice"/>.</summary>
    public NoShadingDevice() { HasPropertyChanged = true; }

    /// <summary>
    /// Returns full transmittance (1.0) and zero reflectance regardless of input.
    /// </summary>
    /// <param name="isDiffuseIrradianceProperties">Not used.</param>
    /// <param name="irradianceFromSideF">Not used.</param>
    /// <param name="transmittance">Always 1.0.</param>
    /// <param name="reflectance">Always 0.0.</param>
    public void ComputeOpticalProperties(
        bool isDiffuseIrradianceProperties, bool irradianceFromSideF,
        out double transmittance, out double reflectance)
    {
      transmittance = 1.0;
      reflectance = 0.0;
      HasPropertyChanged = false;
    }
  }
}