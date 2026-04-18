/* WebproEnvelopeSet.cs
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

using System.Collections.Generic;

namespace Popolo.Webpro.Domain
{
  /// <summary>
  /// Data transfer object representing a single envelope set entry within the
  /// WEBPRO <c>EnvelopeSet</c> dictionary.
  /// </summary>
  /// <remarks>
  /// <para>
  /// An envelope set describes the enclosure (walls, windows) of a single
  /// room. The enclosing dictionary's key identifies the room; this DTO
  /// represents only the value object.
  /// </para>
  /// <para>
  /// The <c>EnvelopeSet</c> section is sparse compared to <c>Rooms</c>: only
  /// rooms that have an actual envelope (i.e. adjoin the outside or ground)
  /// appear here.
  /// </para>
  /// </remarks>
  public sealed class WebproEnvelopeSet
  {
    /// <summary>Gets or sets a value indicating whether the room is air-conditioned.</summary>
    /// <remarks>Parsed from the <c>isAirconditioned</c> JSON property: <c>"有"</c> → true, <c>"無"</c> → false.</remarks>
    public bool IsAirconditioned { get; set; }

    /// <summary>Gets the collection of walls that make up the envelope.</summary>
    public List<WebproWall> Walls { get; } = new List<WebproWall>();
  }
}
