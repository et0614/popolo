/* ZoneDeserializationContext.cs
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
