/* BoundaryWallReferences.cs
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

using Popolo.Core.Climate;

namespace Popolo.Core.Building.Envelope
{
  /// <summary>
  /// Represents a reference to an outside (outdoor-facing) wall surface, carrying
  /// the wall ID, side flag, and the outdoor incline (orientation).
  /// </summary>
  /// <remarks>
  /// Used by <see cref="MultiRooms"/> to enumerate its outside-wall boundary
  /// surfaces without exposing the internal <c>BoundarySurface</c> type.
  /// </remarks>
  public readonly struct OutsideWallReference
  {
    /// <summary>Gets the ID of the referenced wall.</summary>
    public int WallId { get; }

    /// <summary>Gets a value indicating whether the outdoor-facing side is the F side.</summary>
    public bool IsSideF { get; }

    /// <summary>Gets the outdoor-facing tilted surface orientation.</summary>
    public IReadOnlyIncline Incline { get; }

    /// <summary>Initializes a new instance.</summary>
    public OutsideWallReference(int wallId, bool isSideF, IReadOnlyIncline incline)
    {
      WallId = wallId;
      IsSideF = isSideF;
      Incline = incline;
    }
  }

  /// <summary>
  /// Represents a reference to a ground-contact wall surface, carrying
  /// the wall ID, side flag, and soil-to-wall conductance.
  /// </summary>
  public readonly struct GroundWallReference
  {
    /// <summary>Gets the ID of the referenced wall.</summary>
    public int WallId { get; }

    /// <summary>Gets a value indicating whether the ground-facing side is the F side.</summary>
    public bool IsSideF { get; }

    /// <summary>Gets the soil-to-wall thermal conductance [W/(m²·K)].</summary>
    public double Conductance { get; }

    /// <summary>Initializes a new instance.</summary>
    public GroundWallReference(int wallId, bool isSideF, double conductance)
    {
      WallId = wallId;
      IsSideF = isSideF;
      Conductance = conductance;
    }
  }

  /// <summary>
  /// Represents a reference to a wall surface facing an adjacent (non-simulated)
  /// space, carrying the wall ID, side flag, and temperature-difference factor.
  /// </summary>
  public readonly struct AdjacentSpaceWallReference
  {
    /// <summary>Gets the ID of the referenced wall.</summary>
    public int WallId { get; }

    /// <summary>Gets a value indicating whether the adjacent-space-facing side is the F side.</summary>
    public bool IsSideF { get; }

    /// <summary>
    /// Gets the temperature-difference factor [-] used to weight the adjacent space temperature
    /// against the outdoor temperature.
    /// </summary>
    public double TemperatureDifferenceFactor { get; }

    /// <summary>Initializes a new instance.</summary>
    public AdjacentSpaceWallReference(int wallId, bool isSideF, double temperatureDifferenceFactor)
    {
      WallId = wallId;
      IsSideF = isSideF;
      TemperatureDifferenceFactor = temperatureDifferenceFactor;
    }
  }
}
