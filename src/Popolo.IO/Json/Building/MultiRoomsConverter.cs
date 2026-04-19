/* MultiRoomsConverter.cs
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

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

using Popolo.Core.Building;
using Popolo.Core.Building.Envelope;
using Popolo.Core.Climate;

namespace Popolo.IO.Json.Building
{
  /// <summary>
  /// JSON converter for <see cref="MultiRoom"/>.
  /// </summary>
  /// <remarks>
  /// <para>
  /// Serialized JSON schema:
  /// </para>
  /// <code>
  /// {
  ///   "kind":    "multiRooms",
  ///   "albedo":  0.4,
  ///   "rooms": [
  ///     { "zones": [ {...zone...}, {...zone...} ] },
  ///     { "zones": [ {...zone...} ] }
  ///   ],
  ///   "outsideWalls": [
  ///     { "wallId": 42, "sideF": true, "incline": { "kind": "incline", ... } }
  ///   ],
  ///   "groundWalls": [
  ///     { "wallId": 43, "sideF": false, "conductance": 5.0 }
  ///   ],
  ///   "adjacentSpaces": [
  ///     { "wallId": 44, "sideF": true, "temperatureDifferenceFactor": 0.7 }
  ///   ],
  ///   "interZoneAirflows": [
  ///     { "fromZoneIndex": 0, "toZoneIndex": 1, "flowRate": 0.1 }
  ///   ]
  /// }
  /// </code>
  /// <para>
  /// <b>Two-pass deserialization:</b> A <see cref="MultiRoom"/> instance cannot be
  /// constructed without a <see cref="Wall"/>[] array, which lives at the
  /// <c>BuildingThermalModel</c> level. This converter therefore supports only
  /// the <b>Write</b> path directly; <see cref="Read"/> throws a
  /// <see cref="NotSupportedException"/> with a descriptive message.
  /// </para>
  /// <para>
  /// For deserialization, use <see cref="ReadDto"/> — called by
  /// <c>BuildingThermalModelConverter</c> — which reads the JSON into a
  /// <see cref="MultiRoomsDto"/>. The enclosing converter then resolves wall
  /// references against its wall table and materializes the live
  /// <see cref="MultiRoom"/> via <see cref="BuildMultiRooms"/>.
  /// </para>
  /// <para>
  /// <b>Interzone airflows</b> are serialized sparsely: only entries with a
  /// non-zero flow rate are written.
  /// </para>
  /// <para>
  /// <b>Required sibling converters:</b>
  /// <see cref="ZoneConverter"/>, <see cref="Popolo.IO.Json.Climate.InclineConverter"/>,
  /// and all transitive dependencies (Window, shading devices, etc.).
  /// </para>
  /// </remarks>
  public sealed class MultiRoomsConverter : JsonConverter<MultiRoom>
  {

    #region 定数

    private const string PropKind = "kind";
    private const string PropAlbedo = "albedo";
    private const string PropRooms = "rooms";
    private const string PropZones = "zones";
    private const string PropOutsideWalls = "outsideWalls";
    private const string PropGroundWalls = "groundWalls";
    private const string PropAdjacentSpaces = "adjacentSpaces";
    private const string PropInterZoneAirflows = "interZoneAirflows";

    // 壁参照・隣室・airflow 内のキー
    private const string PropWallId = "wallId";
    private const string PropSideF = "sideF";
    private const string PropIncline = "incline";
    private const string PropConductance = "conductance";
    private const string PropTemperatureDifferenceFactor = "temperatureDifferenceFactor";
    private const string PropFromZoneIndex = "fromZoneIndex";
    private const string PropToZoneIndex = "toZoneIndex";
    private const string PropFlowRate = "flowRate";

    private const string ExpectedKind = "multiRooms";

    #endregion

    #region JsonConverter 実装

    /// <summary>
    /// Throws — MultiRooms cannot be constructed without the wall table.
    /// Use <see cref="ReadDto"/> via <c>BuildingThermalModelConverter</c>.
    /// </summary>
    public override MultiRoom Read(
      ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
      throw new NotSupportedException(
        $"{nameof(MultiRoom)} cannot be deserialized directly because its construction " +
        $"requires a {nameof(Wall)}[] array that is defined at the {nameof(Popolo.Core.Building.BuildingThermalModel)} level. " +
        $"Deserialize a {nameof(Popolo.Core.Building.BuildingThermalModel)} instead, or call " +
        $"{nameof(MultiRoomsConverter)}.{nameof(ReadDto)} directly from a higher-level converter.");
    }

    /// <summary>Writes a <see cref="MultiRoom"/> to JSON.</summary>
    public override void Write(
      Utf8JsonWriter writer, MultiRoom value, JsonSerializerOptions options)
    {
      if (value is null)
        throw new ArgumentNullException(nameof(value));

      writer.WriteStartObject();
      writer.WriteString(PropKind, ExpectedKind);
      writer.WriteNumber(PropAlbedo, value.Albedo);

      // rooms: rooms の数は RoomCount。各 zone の RoomIndex で分類。
      WriteRooms(writer, value, options);

      // outsideWalls / groundWalls / adjacentSpaces
      WriteOutsideWalls(writer, value, options);
      WriteGroundWalls(writer, value);
      WriteAdjacentSpaces(writer, value);

      // interZoneAirflows(スパース)
      WriteInterZoneAirflows(writer, value);

      writer.WriteEndObject();
    }

    #endregion

    #region DTO 読み取り(BuildingThermalModelConverter が利用)

    /// <summary>
    /// Reads a <see cref="MultiRoom"/> JSON block into an intermediate
    /// <see cref="MultiRoomsDto"/> without resolving wall references.
    /// </summary>
    /// <param name="reader">UTF-8 JSON reader positioned at the start of the MultiRooms object.</param>
    /// <param name="options">Serializer options; must include ZoneConverter, InclineConverter, etc.</param>
    /// <returns>A populated <see cref="MultiRoomsDto"/>.</returns>
    /// <exception cref="JsonException">Thrown on malformed JSON or missing required fields.</exception>
    internal static MultiRoomsDto ReadDto(
      ref Utf8JsonReader reader, JsonSerializerOptions options)
    {
      if (reader.TokenType != JsonTokenType.StartObject)
        throw new JsonException($"Expected StartObject at the beginning of a {nameof(MultiRoom)}, but got {reader.TokenType}.");

      var dto = new MultiRoomsDto();
      string? kind = null;
      bool seenAlbedo = false;

      while (reader.Read())
      {
        if (reader.TokenType == JsonTokenType.EndObject) break;
        if (reader.TokenType != JsonTokenType.PropertyName)
          throw new JsonException($"Expected PropertyName, but got {reader.TokenType}.");

        string? propName = reader.GetString();
        if (!reader.Read())
          throw new JsonException($"Unexpected end of JSON while reading property '{propName}'.");

        switch (propName)
        {
          case PropKind:
            kind = reader.GetString();
            break;
          case PropAlbedo:
            dto.Albedo = reader.GetDouble();
            seenAlbedo = true;
            break;
          case PropRooms:
            ReadRooms(ref reader, dto, options);
            break;
          case PropOutsideWalls:
            ReadOutsideWalls(ref reader, dto, options);
            break;
          case PropGroundWalls:
            ReadGroundWalls(ref reader, dto);
            break;
          case PropAdjacentSpaces:
            ReadAdjacentSpaces(ref reader, dto);
            break;
          case PropInterZoneAirflows:
            ReadInterZoneAirflows(ref reader, dto);
            break;
          default: reader.Skip(); break;
        }
      }

      if (kind != ExpectedKind)
        throw new JsonException(
          $"Expected '{PropKind}' = '{ExpectedKind}' for {nameof(MultiRoom)}, but got '{kind ?? "(missing)"}'.");
      if (!seenAlbedo)
        throw new JsonException($"Required property '{PropAlbedo}' is missing from {nameof(MultiRoom)} JSON.");

      return dto;
    }

    #endregion

    #region DTO → MultiRooms 構築(BuildingThermalModelConverter が利用)

    /// <summary>
    /// Builds a live <see cref="MultiRoom"/> from an intermediate
    /// <see cref="MultiRoomsDto"/> by resolving wall references against the
    /// provided wall table.
    /// </summary>
    /// <param name="dto">Intermediate DTO produced by <see cref="ReadDto"/>.</param>
    /// <param name="wallsById">Map of wall ID to <see cref="Wall"/> instance.</param>
    /// <returns>A fully configured <see cref="MultiRoom"/>.</returns>
    /// <exception cref="JsonException">Thrown when a wall reference cannot be resolved.</exception>
    internal static MultiRoom BuildMultiRooms(
      MultiRoomsDto dto, IReadOnlyDictionary<int, Wall> wallsById)
    {
      // 1. Zone / Window / Wall 配列を準備
      var flatZones = dto.FlattenZones();
      var windows = new List<Window>();
      // 各 Zone に付随する windows を集める(順序保存)
      foreach (var zone in flatZones)
      {
        var ctx = ZoneDeserializationContext.TryGet(zone);
        if (ctx is not null) windows.AddRange(ctx.Windows);
      }

      // Wall[] は wallsById の全てを配列化。順序は ID 昇順で決定。
      var wallsSorted = new List<Wall>(wallsById.Values);
      wallsSorted.Sort((a, b) => a.ID.CompareTo(b.ID));
      var walls = wallsSorted.ToArray();

      // 2. MultiRooms インスタンス生成
      var mRooms = new MultiRoom(
        rmCount: dto.Rooms.Count,
        zones: flatZones.ToArray(),
        walls: walls,
        windows: windows.ToArray());
      mRooms.Albedo = dto.Albedo;

      // 3. 各 zone を正しい roomIndex に配属
      for (int zoneIdx = 0; zoneIdx < flatZones.Count; zoneIdx++)
      {
        int roomIdx = dto.FindRoomIndexOf(flatZones[zoneIdx]);
        if (roomIdx < 0)
          throw new JsonException($"Zone at index {zoneIdx} is not associated with any room.");
        mRooms.AddZone(roomIdx, zoneIdx);
      }

      // 4. 各 Zone の壁参照を解決して結合
      int globalWindowIdx = 0;
      for (int zoneIdx = 0; zoneIdx < flatZones.Count; zoneIdx++)
      {
        var zone = flatZones[zoneIdx];
        var ctx = ZoneDeserializationContext.TryGet(zone);
        if (ctx is null) continue;

        // 壁結合
        foreach (var wallRef in ctx.WallReferences)
        {
          int wallIdx = FindWallIndex(walls, wallRef.WallId);
          mRooms.AddWall(zoneIdx, wallIdx, wallRef.IsSideF);
        }

        // 窓結合
        foreach (var _ in ctx.Windows)
        {
          mRooms.AddWindow(zoneIdx, globalWindowIdx);
          globalWindowIdx++;
        }

        // コンテキストはもう不要
        ZoneDeserializationContext.Clear(zone);
      }

      // 5. 外壁 / 地中壁 / 隣室壁
      foreach (var ow in dto.OutsideWalls)
      {
        int wallIdx = FindWallIndex(walls, ow.WallId);
        mRooms.SetOutsideWall(wallIdx, ow.IsSideF, ow.Incline);
      }
      foreach (var gw in dto.GroundWalls)
      {
        int wallIdx = FindWallIndex(walls, gw.WallId);
        mRooms.SetGroundWall(wallIdx, gw.IsSideF, gw.Conductance);
      }
      foreach (var asw in dto.AdjacentSpaces)
      {
        int wallIdx = FindWallIndex(walls, asw.WallId);
        mRooms.UseAdjacentSpaceFactor(wallIdx, asw.IsSideF, asw.TemperatureDifferenceFactor);
      }

      // 6. Zone 間の換気(スパース)
      foreach (var flow in dto.InterZoneAirflows)
        mRooms.SetAirFlow(flow.FromZoneIndex, flow.ToZoneIndex, flow.FlowRate);

      return mRooms;
    }

    /// <summary>Finds the array index of a wall by its ID.</summary>
    private static int FindWallIndex(Wall[] walls, int id)
    {
      for (int i = 0; i < walls.Length; i++)
        if (walls[i].ID == id) return i;
      throw new JsonException($"Wall with ID {id} not found in the wall table.");
    }

    #endregion

    #region rooms の読み書き

    private static void ReadRooms(
      ref Utf8JsonReader reader, MultiRoomsDto dto, JsonSerializerOptions options)
    {
      if (reader.TokenType != JsonTokenType.StartArray)
        throw new JsonException($"Expected StartArray for '{PropRooms}', but got {reader.TokenType}.");

      while (reader.Read())
      {
        if (reader.TokenType == JsonTokenType.EndArray) break;
        if (reader.TokenType != JsonTokenType.StartObject)
          throw new JsonException($"Each '{PropRooms}' entry must be an object, but got {reader.TokenType}.");

        var room = new List<Zone>();

        while (reader.Read())
        {
          if (reader.TokenType == JsonTokenType.EndObject) break;
          if (reader.TokenType != JsonTokenType.PropertyName)
            throw new JsonException($"Expected PropertyName in room, but got {reader.TokenType}.");

          string? propName = reader.GetString();
          if (!reader.Read())
            throw new JsonException($"Unexpected end of JSON while reading room property '{propName}'.");

          switch (propName)
          {
            case PropZones:
              if (reader.TokenType != JsonTokenType.StartArray)
                throw new JsonException($"Expected StartArray for '{PropZones}', but got {reader.TokenType}.");
              while (reader.Read())
              {
                if (reader.TokenType == JsonTokenType.EndArray) break;
                var zone = JsonSerializer.Deserialize<Zone>(ref reader, options)
                  ?? throw new JsonException($"{nameof(Zone)} deserialization returned null.");
                room.Add(zone);
              }
              break;
            default: reader.Skip(); break;
          }
        }

        dto.Rooms.Add(room);
      }
    }

    private static void WriteRooms(
      Utf8JsonWriter writer, MultiRoom value, JsonSerializerOptions options)
    {
      // 各 zone の RoomIndex に基づいて rooms を組み立てる
      int roomCount = value.RoomCount;
      var byRoom = new List<List<Zone>>();
      for (int i = 0; i < roomCount; i++) byRoom.Add(new List<Zone>());

      foreach (IReadOnlyZone rz in value.Zones)
      {
        if (rz is not Zone z)
          throw new JsonException(
            $"Unexpected {nameof(IReadOnlyZone)} implementation: {rz?.GetType().FullName ?? "null"}.");
        if (z.RoomIndex < 0 || z.RoomIndex >= roomCount)
          throw new JsonException($"Zone '{z.Name}' has invalid RoomIndex {z.RoomIndex} (RoomCount={roomCount}).");
        byRoom[z.RoomIndex].Add(z);
      }

      writer.WritePropertyName(PropRooms);
      writer.WriteStartArray();
      foreach (var room in byRoom)
      {
        writer.WriteStartObject();
        writer.WritePropertyName(PropZones);
        writer.WriteStartArray();
        foreach (var z in room)
          JsonSerializer.Serialize(writer, z, options);
        writer.WriteEndArray();
        writer.WriteEndObject();
      }
      writer.WriteEndArray();
    }

    #endregion

    #region outsideWalls の読み書き

    private static void ReadOutsideWalls(
      ref Utf8JsonReader reader, MultiRoomsDto dto, JsonSerializerOptions options)
    {
      if (reader.TokenType != JsonTokenType.StartArray)
        throw new JsonException($"Expected StartArray for '{PropOutsideWalls}', but got {reader.TokenType}.");

      while (reader.Read())
      {
        if (reader.TokenType == JsonTokenType.EndArray) break;
        if (reader.TokenType != JsonTokenType.StartObject)
          throw new JsonException($"Each '{PropOutsideWalls}' entry must be an object, but got {reader.TokenType}.");

        int? wallId = null;
        bool? sideF = null;
        Incline? incline = null;

        while (reader.Read())
        {
          if (reader.TokenType == JsonTokenType.EndObject) break;
          if (reader.TokenType != JsonTokenType.PropertyName)
            throw new JsonException($"Expected PropertyName in outside wall entry, but got {reader.TokenType}.");

          string? propName = reader.GetString();
          if (!reader.Read())
            throw new JsonException($"Unexpected end of JSON while reading outside wall property '{propName}'.");

          switch (propName)
          {
            case PropWallId: wallId = reader.GetInt32(); break;
            case PropSideF: sideF = reader.GetBoolean(); break;
            case PropIncline:
              incline = JsonSerializer.Deserialize<Incline>(ref reader, options);
              break;
            default: reader.Skip(); break;
          }
        }

        if (wallId is null) throw new JsonException($"Required '{PropOutsideWalls}[].{PropWallId}' is missing.");
        if (sideF is null) throw new JsonException($"Required '{PropOutsideWalls}[].{PropSideF}' is missing.");
        if (incline is null) throw new JsonException($"Required '{PropOutsideWalls}[].{PropIncline}' is missing.");

        dto.OutsideWalls.Add(new OutsideWallDto
        {
          WallId = wallId.Value,
          IsSideF = sideF.Value,
          Incline = incline,
        });
      }
    }

    private static void WriteOutsideWalls(
      Utf8JsonWriter writer, MultiRoom value, JsonSerializerOptions options)
    {
      var refs = value.GetOutsideWallReferences();
      writer.WritePropertyName(PropOutsideWalls);
      writer.WriteStartArray();
      foreach (var r in refs)
      {
        writer.WriteStartObject();
        writer.WriteNumber(PropWallId, r.WallId);
        writer.WriteBoolean(PropSideF, r.IsSideF);
        writer.WritePropertyName(PropIncline);
        JsonSerializer.Serialize(writer, new Incline(r.Incline), options);
        writer.WriteEndObject();
      }
      writer.WriteEndArray();
    }

    #endregion

    #region groundWalls の読み書き

    private static void ReadGroundWalls(ref Utf8JsonReader reader, MultiRoomsDto dto)
    {
      if (reader.TokenType != JsonTokenType.StartArray)
        throw new JsonException($"Expected StartArray for '{PropGroundWalls}', but got {reader.TokenType}.");

      while (reader.Read())
      {
        if (reader.TokenType == JsonTokenType.EndArray) break;
        if (reader.TokenType != JsonTokenType.StartObject)
          throw new JsonException($"Each '{PropGroundWalls}' entry must be an object, but got {reader.TokenType}.");

        int? wallId = null;
        bool? sideF = null;
        double? conductance = null;

        while (reader.Read())
        {
          if (reader.TokenType == JsonTokenType.EndObject) break;
          if (reader.TokenType != JsonTokenType.PropertyName)
            throw new JsonException($"Expected PropertyName in ground wall entry, but got {reader.TokenType}.");

          string? propName = reader.GetString();
          if (!reader.Read())
            throw new JsonException($"Unexpected end of JSON while reading ground wall property '{propName}'.");

          switch (propName)
          {
            case PropWallId: wallId = reader.GetInt32(); break;
            case PropSideF: sideF = reader.GetBoolean(); break;
            case PropConductance: conductance = reader.GetDouble(); break;
            default: reader.Skip(); break;
          }
        }

        if (wallId is null) throw new JsonException($"Required '{PropGroundWalls}[].{PropWallId}' is missing.");
        if (sideF is null) throw new JsonException($"Required '{PropGroundWalls}[].{PropSideF}' is missing.");
        if (conductance is null) throw new JsonException($"Required '{PropGroundWalls}[].{PropConductance}' is missing.");

        dto.GroundWalls.Add(new GroundWallDto
        {
          WallId = wallId.Value,
          IsSideF = sideF.Value,
          Conductance = conductance.Value,
        });
      }
    }

    private static void WriteGroundWalls(Utf8JsonWriter writer, MultiRoom value)
    {
      var refs = value.GetGroundWallReferences();
      writer.WritePropertyName(PropGroundWalls);
      writer.WriteStartArray();
      foreach (var r in refs)
      {
        writer.WriteStartObject();
        writer.WriteNumber(PropWallId, r.WallId);
        writer.WriteBoolean(PropSideF, r.IsSideF);
        writer.WriteNumber(PropConductance, r.Conductance);
        writer.WriteEndObject();
      }
      writer.WriteEndArray();
    }

    #endregion

    #region adjacentSpaces の読み書き

    private static void ReadAdjacentSpaces(ref Utf8JsonReader reader, MultiRoomsDto dto)
    {
      if (reader.TokenType != JsonTokenType.StartArray)
        throw new JsonException($"Expected StartArray for '{PropAdjacentSpaces}', but got {reader.TokenType}.");

      while (reader.Read())
      {
        if (reader.TokenType == JsonTokenType.EndArray) break;
        if (reader.TokenType != JsonTokenType.StartObject)
          throw new JsonException($"Each '{PropAdjacentSpaces}' entry must be an object, but got {reader.TokenType}.");

        int? wallId = null;
        bool? sideF = null;
        double? factor = null;

        while (reader.Read())
        {
          if (reader.TokenType == JsonTokenType.EndObject) break;
          if (reader.TokenType != JsonTokenType.PropertyName)
            throw new JsonException($"Expected PropertyName in adjacent space entry, but got {reader.TokenType}.");

          string? propName = reader.GetString();
          if (!reader.Read())
            throw new JsonException($"Unexpected end of JSON while reading adjacent space property '{propName}'.");

          switch (propName)
          {
            case PropWallId: wallId = reader.GetInt32(); break;
            case PropSideF: sideF = reader.GetBoolean(); break;
            case PropTemperatureDifferenceFactor: factor = reader.GetDouble(); break;
            default: reader.Skip(); break;
          }
        }

        if (wallId is null) throw new JsonException($"Required '{PropAdjacentSpaces}[].{PropWallId}' is missing.");
        if (sideF is null) throw new JsonException($"Required '{PropAdjacentSpaces}[].{PropSideF}' is missing.");
        if (factor is null) throw new JsonException($"Required '{PropAdjacentSpaces}[].{PropTemperatureDifferenceFactor}' is missing.");

        dto.AdjacentSpaces.Add(new AdjacentSpaceWallDto
        {
          WallId = wallId.Value,
          IsSideF = sideF.Value,
          TemperatureDifferenceFactor = factor.Value,
        });
      }
    }

    private static void WriteAdjacentSpaces(Utf8JsonWriter writer, MultiRoom value)
    {
      var refs = value.GetAdjacentSpaceWallReferences();
      writer.WritePropertyName(PropAdjacentSpaces);
      writer.WriteStartArray();
      foreach (var r in refs)
      {
        writer.WriteStartObject();
        writer.WriteNumber(PropWallId, r.WallId);
        writer.WriteBoolean(PropSideF, r.IsSideF);
        writer.WriteNumber(PropTemperatureDifferenceFactor, r.TemperatureDifferenceFactor);
        writer.WriteEndObject();
      }
      writer.WriteEndArray();
    }

    #endregion

    #region interZoneAirflows の読み書き

    private static void ReadInterZoneAirflows(ref Utf8JsonReader reader, MultiRoomsDto dto)
    {
      if (reader.TokenType != JsonTokenType.StartArray)
        throw new JsonException($"Expected StartArray for '{PropInterZoneAirflows}', but got {reader.TokenType}.");

      while (reader.Read())
      {
        if (reader.TokenType == JsonTokenType.EndArray) break;
        if (reader.TokenType != JsonTokenType.StartObject)
          throw new JsonException($"Each '{PropInterZoneAirflows}' entry must be an object, but got {reader.TokenType}.");

        int? from = null, to = null;
        double? flow = null;

        while (reader.Read())
        {
          if (reader.TokenType == JsonTokenType.EndObject) break;
          if (reader.TokenType != JsonTokenType.PropertyName)
            throw new JsonException($"Expected PropertyName in airflow entry, but got {reader.TokenType}.");

          string? propName = reader.GetString();
          if (!reader.Read())
            throw new JsonException($"Unexpected end of JSON while reading airflow property '{propName}'.");

          switch (propName)
          {
            case PropFromZoneIndex: from = reader.GetInt32(); break;
            case PropToZoneIndex: to = reader.GetInt32(); break;
            case PropFlowRate: flow = reader.GetDouble(); break;
            default: reader.Skip(); break;
          }
        }

        if (from is null) throw new JsonException($"Required '{PropInterZoneAirflows}[].{PropFromZoneIndex}' is missing.");
        if (to is null) throw new JsonException($"Required '{PropInterZoneAirflows}[].{PropToZoneIndex}' is missing.");
        if (flow is null) throw new JsonException($"Required '{PropInterZoneAirflows}[].{PropFlowRate}' is missing.");

        dto.InterZoneAirflows.Add(new InterZoneAirflowDto
        {
          FromZoneIndex = from.Value,
          ToZoneIndex = to.Value,
          FlowRate = flow.Value,
        });
      }
    }

    private static void WriteInterZoneAirflows(Utf8JsonWriter writer, MultiRoom value)
    {
      writer.WritePropertyName(PropInterZoneAirflows);
      writer.WriteStartArray();
      int n = value.ZoneCount;
      for (int i = 0; i < n; i++)
      {
        for (int j = 0; j < n; j++)
        {
          double flow = value.GetAirFlow(i, j);
          if (flow == 0) continue; // スパース
          writer.WriteStartObject();
          writer.WriteNumber(PropFromZoneIndex, i);
          writer.WriteNumber(PropToZoneIndex, j);
          writer.WriteNumber(PropFlowRate, flow);
          writer.WriteEndObject();
        }
      }
      writer.WriteEndArray();
    }

    #endregion

  }
}
