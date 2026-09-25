/* WallSurfaceReference.cs
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
  /// Represents a reference to one side of a <see cref="Wall"/> that faces a zone,
  /// as a lightweight value carrying only the Wall ID and side flag.
  /// </summary>
  /// <remarks>
  /// Used primarily by zone-level APIs that need to report wall attachment
  /// as a flat value reference (rather than via the per-side
  /// <see cref="EnvelopeSurface"/> objects, which are part of the solver's
  /// mutable state). Consumers can
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
