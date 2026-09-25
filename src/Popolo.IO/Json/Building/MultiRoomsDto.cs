/* MultiRoomsDto.cs
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

using Popolo.Core.Building;
using Popolo.Core.Climate;

namespace Popolo.IO.Json.Building
{
  /// <summary>
  /// Intermediate data transfer object for deserializing a <c>MultiRooms</c> JSON
  /// block before the <c>Wall</c> table is available for reference resolution.
  /// </summary>
  /// <remarks>
  /// <para>
  /// A <c>MultiRooms</c> instance cannot be constructed directly from its JSON
  /// because the constructor requires a <c>Wall[]</c> array that is defined
  /// separately at the <c>BuildingThermalModel</c> level. This DTO captures
  /// everything the <c>MultiRooms</c> JSON block contains, leaves wall
  /// references as IDs, and defers construction of the live
  /// <c>MultiRooms</c> instance to
  /// <see cref="Popolo.IO.Json.BuildingThermalModelConverter"/> (which owns
  /// the wall table and can resolve IDs).
  /// </para>
  /// <para>
  /// Zones, as fully-formed <see cref="Zone"/> instances, are held in the DTO
  /// already; each zone has side-band
  /// <see cref="ZoneDeserializationContext"/> data attached carrying its pending
  /// wall references and windows.
  /// </para>
  /// </remarks>
  internal sealed class MultiRoomsDto
  {
    /// <summary>Ground reflectance [-].</summary>
    public double Albedo { get; set; } = 0.4;

    /// <summary>Rooms → zones grouping. Zone membership to its room is positional.</summary>
    public List<List<Zone>> Rooms { get; } = new List<List<Zone>>();

    /// <summary>Outside walls: wall references with an associated incline (orientation).</summary>
    public List<OutsideWallDto> OutsideWalls { get; } = new List<OutsideWallDto>();

    /// <summary>Ground walls: wall references with ground-side conductance.</summary>
    public List<GroundWallDto> GroundWalls { get; } = new List<GroundWallDto>();

    /// <summary>Adjacent-space walls: wall references with a temperature-difference factor.</summary>
    public List<AdjacentSpaceWallDto> AdjacentSpaces { get; } = new List<AdjacentSpaceWallDto>();

    /// <summary>Sparse interzone airflows: only non-zero entries.</summary>
    public List<InterZoneAirflowDto> InterZoneAirflows { get; } = new List<InterZoneAirflowDto>();

    /// <summary>Returns a flat list of all zones across all rooms, in insertion order.</summary>
    public List<Zone> FlattenZones()
    {
      var result = new List<Zone>();
      foreach (var room in Rooms)
        foreach (var zone in room)
          result.Add(zone);
      return result;
    }

    /// <summary>
    /// Returns the room index (0-based) for the given zone, based on its position
    /// within the <see cref="Rooms"/> nested lists.
    /// </summary>
    public int FindRoomIndexOf(Zone zone)
    {
      for (int i = 0; i < Rooms.Count; i++)
        if (Rooms[i].Contains(zone)) return i;
      return -1;
    }
  }

  /// <summary>DTO for an outside-wall entry.</summary>
  internal sealed class OutsideWallDto
  {
    public int WallId { get; set; }
    public bool IsSideF { get; set; }
    public Incline Incline { get; set; } = null!;
  }

  /// <summary>DTO for a ground-wall entry.</summary>
  internal sealed class GroundWallDto
  {
    public int WallId { get; set; }
    public bool IsSideF { get; set; }
    public double Conductance { get; set; }
  }

  /// <summary>DTO for an adjacent-space-wall entry.</summary>
  internal sealed class AdjacentSpaceWallDto
  {
    public int WallId { get; set; }
    public bool IsSideF { get; set; }
    public double TemperatureDifferenceFactor { get; set; }
  }

  /// <summary>DTO for a single sparse interzone airflow entry.</summary>
  internal sealed class InterZoneAirflowDto
  {
    public int FromZoneIndex { get; set; }
    public int ToZoneIndex { get; set; }
    public double FlowRate { get; set; }
  }
}
