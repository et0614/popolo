/* ShortWaveEmission.cs
 *
 * Copyright (C) 2026 E.Togashi
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
  /// Describes how an <see cref="IEnvelopeComponent"/> contributes short-wave
  /// (solar) radiation to the indoor space at a single time step, given the
  /// solar irradiance on its outdoor-facing side.
  /// </summary>
  /// <remarks>
  /// <para>
  /// Returned by
  /// <see cref="IEnvelopeComponent.EmitShortWaveToIndoor(EnvelopeSurface, Climate.IReadOnlySun, double)"/>.
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
