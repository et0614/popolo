/* ShortWaveEmission.cs
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

namespace Popolo.Core.Building.Envelope
{
  /// <summary>
  /// Describes how an <see cref="OpticalLayeredEnvelope"/> contributes short-wave
  /// (solar) radiation to the indoor space at a single time step, given the
  /// solar irradiance on its outdoor-facing side.
  /// </summary>
  /// <remarks>
  /// <para>
  /// Returned by
  /// <see cref="OpticalLayeredEnvelope.EmitShortWaveToIndoor(EnvelopeSurface, Climate.IReadOnlySun, double)"/>.
  /// Opaque components (a typical wall) return <see cref="Zero"/> — they
  /// neither absorb nor transmit any short-wave at the indoor side, since
  /// outdoor solar absorption on opaque surfaces is already accounted for in
  /// the sol-air temperature on the outdoor face.
  /// </para>
  /// <para>
  /// Translucent components (windows, and future translucent walls) report
  /// (i) the flux absorbed at the indoor-facing surface and (ii) the
  /// transmitted power split between direct and diffuse components, so the
  /// caller can apply preferential floor distribution to the direct beam
  /// before redistributing the rest by the Gebhart matrix.
  /// </para>
  /// </remarks>
  public readonly struct ShortWaveEmission
  {
    /// <summary>Gets the short-wave flux absorbed at the indoor-facing surface [W/m²].</summary>
    public double InsideAbsorbedFlux { get; }

    /// <summary>Gets the direct beam power transmitted into the indoor space [W].</summary>
    public double TransmittedDirectPower { get; }

    /// <summary>Gets the diffuse power transmitted into the indoor space [W].</summary>
    public double TransmittedDiffusePower { get; }

    /// <summary>Initializes a new instance.</summary>
    public ShortWaveEmission(
      double insideAbsorbedFlux,
      double transmittedDirectPower,
      double transmittedDiffusePower)
    {
      InsideAbsorbedFlux = insideAbsorbedFlux;
      TransmittedDirectPower = transmittedDirectPower;
      TransmittedDiffusePower = transmittedDiffusePower;
    }

    /// <summary>Zero-emission descriptor returned by opaque components.</summary>
    public static ShortWaveEmission Zero => default;

    /// <summary>Gets a value indicating whether this emission contributes nothing to the indoor side.</summary>
    public bool IsZero
        => InsideAbsorbedFlux == 0.0
        && TransmittedDirectPower == 0.0
        && TransmittedDiffusePower == 0.0;
  }
}
