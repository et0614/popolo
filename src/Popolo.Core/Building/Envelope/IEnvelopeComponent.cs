/* IEnvelopeComponent.cs
 *
 * Copyright (C) 2025 E.Togashi
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

using Popolo.Core.Climate;

namespace Popolo.Core.Building.Envelope
{
  /// <summary>
  /// Abstract envelope component — a building shell element that separates
  /// two zones (or a zone and an exterior boundary) and participates in the
  /// multi-zone heat balance.
  /// </summary>
  /// <remarks>
  /// <para>
  /// Concrete implementations include <see cref="Wall"/> (multi-layer
  /// transient conduction with optional embedded radiant pipes and PCM
  /// layers) and <see cref="Window"/> (multi-pane glazing with internal
  /// shading devices and external sun-shading geometry).
  /// </para>
  /// <para>
  /// Each component exposes two opposing <see cref="EnvelopeSurface"/>
  /// instances (F and B). F and B are positional labels only — neither
  /// is intrinsically "indoor" or "outdoor"; the user assigns orientation
  /// by registering one side via <c>MultiRoom.SetOutsideWall</c> /
  /// <c>SetGroundWall</c> and the other to a zone via
  /// <c>MultiRoom.AddWall(zoneIndex, wallIndex, isSideF)</c>.
  /// </para>
  /// <para>
  /// Upper-layer code (<see cref="MultiRoom"/>, <see cref="Zone"/>) drives
  /// the heat balance through this interface and the surfaces it exposes,
  /// without distinguishing walls from windows.
  /// </para>
  /// </remarks>
  public interface IEnvelopeComponent
  {
    /// <summary>Gets the surface area [m²] of this envelope component.</summary>
    double Area { get; }

    /// <summary>Gets the boundary surface element on the F side.</summary>
    EnvelopeSurface SurfaceF { get; }

    /// <summary>Gets the boundary surface element on the B side.</summary>
    EnvelopeSurface SurfaceB { get; }

    /// <summary>
    /// Computes this component's short-wave (solar) radiation contribution to
    /// the indoor space at one time step, using its own optical model and the
    /// solar state at its outdoor-facing surface.
    /// </summary>
    /// <param name="indoorSurface">
    /// The indoor-facing <see cref="EnvelopeSurface"/> (one of <see cref="SurfaceF"/> /
    /// <see cref="SurfaceB"/>) from which the room is observing this component.
    /// </param>
    /// <param name="sun">The current solar geometry / radiation state.</param>
    /// <param name="albedo">Ground albedo [-].</param>
    /// <param name="useGivenIrradiance">
    /// If <c>true</c>, read the outdoor-side direct/diffuse irradiance from the
    /// component's own outdoor-surface fields
    /// (<see cref="EnvelopeSurface.DirectSolarIrradiance"/> /
    /// <see cref="EnvelopeSurface.DiffuseSolarIrradiance"/>) instead of computing
    /// them from <paramref name="sun"/> and the surface incline.
    /// </param>
    /// <returns>
    /// A <see cref="ShortWaveEmission"/> describing the flux absorbed at the
    /// indoor surface and the power transmitted into the room. Opaque
    /// components return <see cref="ShortWaveEmission.Zero"/>; their outdoor
    /// short-wave absorption is folded into the sol-air temperature on the
    /// outdoor face elsewhere.
    /// </returns>
    ShortWaveEmission EmitShortWaveToIndoor(
      EnvelopeSurface indoorSurface,
      IReadOnlySun sun,
      double albedo,
      bool useGivenIrradiance);

    /// <summary>
    /// Gets the effective absorptance [-] for indoor diffuse short-wave
    /// arriving on this component's indoor-facing surface from interior
    /// inter-reflection (the Gebhart-distributed remainder).
    /// </summary>
    /// <remarks>
    /// Opaque components return 1.0 — all incident diffuse short-wave is
    /// absorbed at first hit, and room-level multi-reflection is already
    /// captured by the Gebhart matrix. Translucent components (windows)
    /// return a factor that accounts for inter-layer back-and-forth
    /// (typically <c>DiffuseAbsorptance / (1 − DiffuseReflectance)</c>).
    /// </remarks>
    double IndoorDiffuseAbsorptanceFactor { get; }
  }
}
