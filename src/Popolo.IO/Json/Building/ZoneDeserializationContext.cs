/* ZoneDeserializationContext.cs
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
using System.Runtime.CompilerServices;

using Popolo.Core.Building;
using Popolo.Core.Building.Envelope;

namespace Popolo.IO.Json.Building
{
  /// <summary>
  /// Side-band context attached to a just-deserialized <see cref="Zone"/> holding
  /// information that does not fit on the Zone itself — specifically, wall references
  /// to be resolved and <see cref="Window"/> instances to be attached at the
  /// <c>MultiRooms</c> level.
  /// </summary>
  /// <remarks>
  /// <para>
  /// This context is populated by <see cref="ZoneConverter"/> during deserialization,
  /// then consumed by <c>MultiRoomsConverter</c> which has access to the wall table
  /// needed for reference resolution.
  /// </para>
  /// <para>
  /// Storage uses <see cref="ConditionalWeakTable{TKey,TValue}"/> so that the context
  /// is automatically garbage-collected when the associated Zone is no longer
  /// reachable — no explicit cleanup is required. This keeps the Zone's public API
  /// clean of deserialization plumbing.
  /// </para>
  /// <para>
  /// Access is thread-safe via <see cref="ConditionalWeakTable{TKey,TValue}"/>'s
  /// own synchronization.
  /// </para>
  /// </remarks>
  internal sealed class ZoneDeserializationContext
  {
    /// <summary>Wall references parsed from <c>walls</c>, pending resolution against the wall table.</summary>
    public List<WallSurfaceReference> WallReferences { get; }

    /// <summary>Windows parsed from <c>windows</c>, pending attachment to <c>MultiRooms</c>.</summary>
    public List<Window> Windows { get; }

    public ZoneDeserializationContext(
      List<WallSurfaceReference> wallReferences, List<Window> windows)
    {
      WallReferences = wallReferences;
      Windows = windows;
    }

    /// <summary>Weak association from <see cref="Zone"/> to its deserialization context.</summary>
    private static readonly ConditionalWeakTable<Zone, ZoneDeserializationContext> Table
      = new ConditionalWeakTable<Zone, ZoneDeserializationContext>();

    /// <summary>Attaches context to the specified <paramref name="zone"/>.</summary>
    /// <remarks>If the zone already has a context, it is replaced.</remarks>
    public static void Attach(Zone zone, ZoneDeserializationContext context)
    {
      Table.Remove(zone);
      Table.Add(zone, context);
    }

    /// <summary>Retrieves the context associated with <paramref name="zone"/>, if any.</summary>
    /// <returns>The context, or <c>null</c> if none was attached.</returns>
    public static ZoneDeserializationContext? TryGet(Zone zone)
    {
      return Table.TryGetValue(zone, out var ctx) ? ctx : null;
    }

    /// <summary>Removes the context from <paramref name="zone"/>, if present.</summary>
    /// <remarks>
    /// Called by <c>MultiRoomsConverter</c> after consuming the context,
    /// to avoid holding on to deserialization scaffolding past the point where it
    /// is needed. Not strictly required (the table is weak-keyed), but keeps
    /// memory use tight for long-lived Zone instances.
    /// </remarks>
    public static void Clear(Zone zone)
    {
      Table.Remove(zone);
    }
  }
}
