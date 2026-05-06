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
  }
}
