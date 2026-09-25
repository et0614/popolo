/* WebproEnvelopeSet.cs
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
