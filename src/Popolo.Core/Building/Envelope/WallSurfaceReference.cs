/* WallSurfaceReference.cs
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

namespace Popolo.Core.Building.Envelope
{
  /// <summary>
  /// Represents a reference to one side of a <see cref="Wall"/> that faces a zone,
  /// as a lightweight value carrying only the Wall ID and side flag.
  /// </summary>
  /// <remarks>
  /// Used primarily by zone-level APIs that need to report wall attachment
  /// without exposing the internal <c>BoundarySurface</c> type. Consumers can
  /// resolve the referenced wall by matching <see cref="WallId"/> against a
  /// wall collection.
  /// </remarks>
  public readonly struct WallSurfaceReference
  {
    /// <summary>Gets the ID of the referenced wall.</summary>
    public int WallId { get; }

    /// <summary>Gets a value indicating whether the zone faces the F side of the wall.</summary>
    /// <value>True if the zone is on the F side; false if on the B side.</value>
    public bool IsSideF { get; }

    /// <summary>Initializes a new instance of <see cref="WallSurfaceReference"/>.</summary>
    /// <param name="wallId">ID of the referenced wall.</param>
    /// <param name="isSideF">True if the zone is on the F side; false if on the B side.</param>
    public WallSurfaceReference(int wallId, bool isSideF)
    {
      WallId = wallId;
      IsSideF = isSideF;
    }
  }
}
