/* IShadingDevice.cs
 *
 * Copyright (C) 2016 E.Togashi
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 2 of the License, or (at
 * your option) any later version.
 *
 * This program is distributed in the hope that it will be useful, but
 * WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
 * General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 675 Mass Ave, Cambridge, MA 02139, USA.
 */

namespace Popolo.Core.Building.Envelope
{
  /// <summary>
  /// Represents a solar shading device placed in the air gap of a window assembly.
  /// </summary>
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