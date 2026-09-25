/* WebproRoom.cs
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

using Popolo.Webpro.Domain.Enums;

namespace Popolo.Webpro.Domain
{
  /// <summary>
  /// Data transfer object representing a single room entry within the WEBPRO
  /// <c>Rooms</c> dictionary.
  /// </summary>
  /// <remarks>
  /// <para>
  /// The key of the enclosing dictionary (e.g. <c>"1F_ロビー"</c>) is the
  /// room name and is stored separately by the top-level model; this DTO
  /// represents only the value object.
  /// </para>
  /// <para>
  /// <b>Intentionally ignored fields</b> from the WEBPRO JSON:
  /// <c>mainbuildingType</c>, <c>modelBuildingType</c>, <c>buildingGroup</c>,
  /// <c>zone</c>. These fields are read by the legacy WEBPRO energy
  /// subsystems (equipment, schedules) but are not needed for thermal load
  /// calculation.
  /// </para>
  /// </remarks>
  public sealed class WebproRoom
  {
    /// <summary>Gets or sets the building-use category (事務所等, ホテル等, ...).</summary>
    /// <remarks>Corresponds to the WEBPRO JSON property <c>buildingType</c>.</remarks>
    public BuildingType BuildingType { get; set; }

    /// <summary>Gets or sets the sub-room classification (e.g. 執務室, 会議室, 廊下).</summary>
    /// <remarks>
    /// Stored as free-form string because the set of room types is large
    /// (≈ 30 subtypes per building type) and has no direct semantic impact
    /// on thermal simulation.
    /// </remarks>
    public string RoomType { get; set; } = "";

    /// <summary>Gets or sets the floor-to-floor height of the room [m].</summary>
    public double FloorHeight { get; set; }

    /// <summary>Gets or sets the floor-to-ceiling height of the room [m].</summary>
    public double CeilingHeight { get; set; }

    /// <summary>Gets or sets the floor area of the room [m²].</summary>
    public double RoomArea { get; set; }

    /// <summary>Gets or sets a free-form remark string from the JSON <c>Info</c> property.</summary>
    public string? Information { get; set; }
  }
}
