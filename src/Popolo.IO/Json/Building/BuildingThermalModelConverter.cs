/* BuildingThermalModelConverter.cs
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
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

using Popolo.Core.Building;
using Popolo.Core.Building.Envelope;
using Popolo.Core.Climate;

namespace Popolo.IO.Json.Building
{
  /// <summary>
  /// JSON converter for <see cref="BuildingThermalModel"/>.
  /// </summary>
  /// <remarks>
  /// <para>
  /// Serialized JSON schema (schema version 3.0):
  /// </para>
  /// <code>
  /// {
  ///   "$schemaVersion":  "3.0",
  ///   "kind":            "buildingThermalModel",
  ///   "timeStep":        3600,
  ///   "currentDateTime": "2026-04-18T12:00:00",
  ///   "initialState": {
  ///     "temperature":   25.0,
  ///     "humidityRatio": 0.015
  ///   },
  ///   "sun":   { "kind": "sun", ... },
  ///   "walls": [
  ///     { "kind": "wall", "id": 0, ... },
  ///     { "kind": "wall", "id": 1, ... }
  ///   ],
  ///   "multiRooms": [
  ///     { "kind": "multiRooms", ... }
  ///   ]
  /// }
  /// </code>
  /// <para>
  /// <b>Centralized wall storage:</b> All walls are stored at this level with
  /// sequential IDs. <see cref="Zone"/> and <see cref="MultiRoom"/> refer to
  /// walls by ID only. On write, IDs are assigned sequentially so that round-trip
  /// IDs are stable.
  /// </para>
  /// <para>
  /// <b>$schemaVersion:</b> The top-level <c>$schemaVersion</c> marks the file's
  /// format. Currently <c>"3.0"</c>. Future format changes will bump this value;
  /// readers do not presently enforce the version but will in subsequent
  /// revisions.
  /// </para>
  /// <para>
  /// <b>currentDateTime:</b> Serialized as ISO 8601 string
  /// (e.g. <c>"2026-04-18T12:00:00"</c>) without time zone suffix.
  /// Parsed with <see cref="DateTime.ParseExact(string,string,IFormatProvider?)"/>
  /// using the invariant culture.
  /// </para>
  /// <para>
  /// <b>Runtime state excluded:</b> Live state such as outdoor temperature,
  /// humidity ratio, nocturnal radiation, and wall / zone temperatures and
  /// humidity ratios are <b>not</b> persisted. Only <c>initialState</c>
  /// (initial zone temperature and humidity ratio) is kept, applied via
  /// <see cref="BuildingThermalModel.InitializeAirState"/> on read.
  /// </para>
  /// <para>
  /// <b>Two-pass deserialization:</b> Walls must be read before multi-rooms so
  /// that wall references in MultiRooms can be resolved. Because JSON property
  /// order is not guaranteed, this converter uses <see cref="JsonDocument"/> to
  /// buffer the object and read walls first, then MultiRooms.
  /// </para>
  /// <para>
  /// <b>Required sibling converters:</b>
  /// <see cref="Popolo.IO.Json.Building.Envelope.WallConverter"/>, <see cref="Popolo.IO.Json.Building.Envelope.WallLayerConverter"/>,
  /// <see cref="Popolo.IO.Json.Building.Envelope.AirGapLayerConverter"/>, <see cref="Popolo.IO.Json.Building.Envelope.WindowConverter"/>,
  /// <see cref="Popolo.IO.Json.Climate.InclineConverter"/>, <see cref="Popolo.IO.Json.Climate.SunConverter"/>,
  /// <see cref="ZoneConverter"/>, <see cref="MultiRoomsConverter"/>, and all
  /// their transitive dependencies.
  /// </para>
  /// </remarks>
  public sealed class BuildingThermalModelConverter : JsonConverter<BuildingThermalModel>
  {

    #region 定数

    private const string PropSchemaVersion = "$schemaVersion";
    private const string PropKind = "kind";
    private const string PropTimeStep = "timeStep";
    private const string PropCurrentDateTime = "currentDateTime";
    private const string PropInitialState = "initialState";
    private const string PropSun = "sun";
    private const string PropWalls = "walls";
    private const string PropMultiRooms = "multiRooms";

    // initialState 内のキー
    private const string PropTemperature = "temperature";
    private const string PropHumidityRatio = "humidityRatio";

    private const string ExpectedKind = "buildingThermalModel";
    private const string CurrentSchemaVersion = "3.0";

    // ISO 8601 形式(タイムゾーンなし) -- "2026-04-18T12:00:00"
    private const string Iso8601Format = "yyyy-MM-ddTHH:mm:ss";

    #endregion

    #region JsonConverter 実装

    /// <summary>Reads a <see cref="BuildingThermalModel"/> from JSON.</summary>
    public override BuildingThermalModel Read(
      ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
      if (reader.TokenType != JsonTokenType.StartObject)
        throw new JsonException($"Expected StartObject at the beginning of a {nameof(BuildingThermalModel)}, but got {reader.TokenType}.");

      // 2 パス処理のために JsonDocument に取り込む
      using var doc = JsonDocument.ParseValue(ref reader);
      var root = doc.RootElement;

      // $schemaVersion と kind の検証
      string? schemaVersion = GetOptionalString(root, PropSchemaVersion);
      // 現時点では $schemaVersion の値を強制チェックしない(将来のために読み取りのみ)
      // 不正値の場合の扱いは将来バージョンで検討。

      string? kind = GetOptionalString(root, PropKind);
      if (kind != ExpectedKind)
        throw new JsonException(
          $"Expected '{PropKind}' = '{ExpectedKind}' for {nameof(BuildingThermalModel)}, but got '{kind ?? "(missing)"}'.");

      // timeStep
      if (!root.TryGetProperty(PropTimeStep, out var timeStepElem))
        throw new JsonException($"Required property '{PropTimeStep}' is missing from {nameof(BuildingThermalModel)} JSON.");
      double timeStep = timeStepElem.GetDouble();

      // currentDateTime
      if (!root.TryGetProperty(PropCurrentDateTime, out var dateElem))
        throw new JsonException($"Required property '{PropCurrentDateTime}' is missing from {nameof(BuildingThermalModel)} JSON.");
      string? dateStr = dateElem.GetString();
      if (dateStr is null)
        throw new JsonException($"'{PropCurrentDateTime}' must be a string.");
      if (!DateTime.TryParseExact(
            dateStr, Iso8601Format, CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var currentDateTime))
      {
        throw new JsonException(
          $"'{PropCurrentDateTime}' = '{dateStr}' is not a valid ISO 8601 date-time (expected format '{Iso8601Format}').");
      }

      // initialState
      double initialTemperature = 25.0;
      double initialHumidityRatio = 0.015;
      if (root.TryGetProperty(PropInitialState, out var isElem))
      {
        if (isElem.ValueKind != JsonValueKind.Object)
          throw new JsonException($"'{PropInitialState}' must be an object.");
        if (isElem.TryGetProperty(PropTemperature, out var tempElem))
          initialTemperature = tempElem.GetDouble();
        if (isElem.TryGetProperty(PropHumidityRatio, out var humElem))
          initialHumidityRatio = humElem.GetDouble();
      }

      // sun
      Sun? sun = null;
      if (root.TryGetProperty(PropSun, out var sunElem))
      {
        sun = sunElem.Deserialize<Sun>(options)
          ?? throw new JsonException($"{nameof(Sun)} deserialization returned null.");
      }
      else
      {
        throw new JsonException($"Required property '{PropSun}' is missing from {nameof(BuildingThermalModel)} JSON.");
      }

      // walls(先に読む)
      if (!root.TryGetProperty(PropWalls, out var wallsElem))
        throw new JsonException($"Required property '{PropWalls}' is missing from {nameof(BuildingThermalModel)} JSON.");
      if (wallsElem.ValueKind != JsonValueKind.Array)
        throw new JsonException($"'{PropWalls}' must be an array.");

      var wallList = new List<Wall>();
      foreach (var wallElem in wallsElem.EnumerateArray())
      {
        var wall = wallElem.Deserialize<Wall>(options)
          ?? throw new JsonException($"{nameof(Wall)} deserialization returned null.");
        wallList.Add(wall);
      }

      // Wall 辞書構築(ID → Wall)
      var wallsById = new Dictionary<int, Wall>();
      foreach (var w in wallList)
      {
        if (wallsById.ContainsKey(w.ID))
          throw new JsonException($"Duplicate wall ID {w.ID} in '{PropWalls}' array.");
        wallsById[w.ID] = w;
      }

      // multiRooms(DTO 経由で読み、Wall 辞書で解決して MultiRooms を構築)
      if (!root.TryGetProperty(PropMultiRooms, out var mRoomsElem))
        throw new JsonException($"Required property '{PropMultiRooms}' is missing from {nameof(BuildingThermalModel)} JSON.");
      if (mRoomsElem.ValueKind != JsonValueKind.Array)
        throw new JsonException($"'{PropMultiRooms}' must be an array.");

      var mRoomsList = new List<MultiRoom>();
      foreach (var mRoomElem in mRoomsElem.EnumerateArray())
      {
        var dto = ReadMultiRoomsDtoFromElement(mRoomElem, options);
        var mRooms = MultiRoomsConverter.BuildMultiRooms(dto, wallsById);
        mRoomsList.Add(mRooms);
      }

      // BuildingThermalModel 構築
      var model = new BuildingThermalModel(mRoomsList.ToArray());
      model.TimeStep = timeStep;

      // 初期温湿度
      model.InitializeAirState(initialTemperature, initialHumidityRatio);

      // 外部条件(Sun と CurrentDateTime を反映)
      // 屋外気温/湿度/夜間放射は JSON に保存されない。0 で初期化する。
      model.UpdateOutdoorCondition(currentDateTime, sun, 0.0, 0.0, 0.0);

      return model;
    }

    /// <summary>Writes a <see cref="BuildingThermalModel"/> to JSON.</summary>
    public override void Write(
      Utf8JsonWriter writer, BuildingThermalModel value, JsonSerializerOptions options)
    {
      if (value is null)
        throw new ArgumentNullException(nameof(value));

      // シリアライズに備えて Wall に連番 ID を付け直す
      // (MultiRooms/Zone から参照される ID が確実に Wall 配列内の ID と一致するように)
      AssignSequentialWallIds(value);

      writer.WriteStartObject();

      writer.WriteString(PropSchemaVersion, CurrentSchemaVersion);
      writer.WriteString(PropKind, ExpectedKind);
      writer.WriteNumber(PropTimeStep, value.TimeStep);

      // currentDateTime(ISO 8601)
      writer.WriteString(PropCurrentDateTime,
        value.CurrentDateTime.ToString(Iso8601Format, CultureInfo.InvariantCulture));

      // initialState(初期温度は任意値。runtime state は保存しないので、
      // BuildingThermalModel には「初期温湿度」を直接問う API がないが、
      // 実用上は Temperature / HumidityRatio を使ってよい。MultiRooms 内の
      // Zone の現在値を読み出す。複数 zone がある場合は最初の Zone を代表値として使用。)
      WriteInitialState(writer, value);

      // sun
      writer.WritePropertyName(PropSun);
      if (value.Sun is IReadOnlySun readOnlySun)
      {
        // Sun は具象型なのでそのままキャストしてシリアライズ
        if (readOnlySun is Sun concreteSun)
          JsonSerializer.Serialize(writer, concreteSun, options);
        else
        {
          // 別実装があれば Sun にコピー(現状ないが念のため)
          throw new JsonException(
            $"Unsupported {nameof(IReadOnlySun)} implementation: {readOnlySun.GetType().FullName}.");
        }
      }
      else
      {
        throw new JsonException($"{nameof(BuildingThermalModel)}.{nameof(BuildingThermalModel.Sun)} is null; cannot serialize.");
      }

      // walls
      writer.WritePropertyName(PropWalls);
      writer.WriteStartArray();
      foreach (var w in EnumerateDistinctWalls(value))
        JsonSerializer.Serialize(writer, w, options);
      writer.WriteEndArray();

      // multiRooms
      writer.WritePropertyName(PropMultiRooms);
      writer.WriteStartArray();
      foreach (var mr in value.MultiRoom)
      {
        if (mr is MultiRoom concrete)
          JsonSerializer.Serialize(writer, concrete, options);
        else
          throw new JsonException(
            $"Unsupported {nameof(IReadOnlyMultiRoom)} implementation: {mr?.GetType().FullName ?? "null"}.");
      }
      writer.WriteEndArray();

      writer.WriteEndObject();
    }

    #endregion

    #region ヘルパー

    /// <summary>Reads a MultiRooms JSON sub-element into a DTO by re-tokenizing its raw bytes.</summary>
    /// <remarks>
    /// <see cref="JsonElement"/> does not directly expose a <see cref="Utf8JsonReader"/>,
    /// so we re-tokenize the element's UTF-8 bytes. Cost is acceptable at this scale
    /// (a handful of MultiRooms per model).
    /// </remarks>
    private static MultiRoomsDto ReadMultiRoomsDtoFromElement(
      JsonElement element, JsonSerializerOptions options)
    {
      var raw = element.GetRawText();
      var bytes = System.Text.Encoding.UTF8.GetBytes(raw);
      var innerReader = new Utf8JsonReader(bytes);
      innerReader.Read(); // StartObject へ移動
      return MultiRoomsConverter.ReadDto(ref innerReader, options);
    }

    /// <summary>Gets an optional string property; returns null if absent or not a string.</summary>
    private static string? GetOptionalString(JsonElement root, string propertyName)
    {
      if (!root.TryGetProperty(propertyName, out var elem)) return null;
      return elem.ValueKind == JsonValueKind.String ? elem.GetString() : null;
    }

    /// <summary>
    /// Assigns sequential IDs (0, 1, 2, ...) to all distinct walls referenced
    /// from the model's multi-rooms, so that Wall → ID → reference round-trips
    /// are stable.
    /// </summary>
    private static void AssignSequentialWallIds(BuildingThermalModel model)
    {
      int id = 0;
      var seen = new HashSet<Wall>();
      foreach (var mr in model.MultiRoom)
      {
        foreach (var rw in mr.Walls)
        {
          if (rw is Wall wall && seen.Add(wall))
          {
            wall.ID = id++;
          }
        }
      }
    }

    /// <summary>Enumerates all distinct walls in the model (deduplicated by reference).</summary>
    private static IEnumerable<Wall> EnumerateDistinctWalls(BuildingThermalModel model)
    {
      var seen = new HashSet<Wall>();
      foreach (var mr in model.MultiRoom)
      {
        foreach (var rw in mr.Walls)
        {
          if (rw is Wall wall && seen.Add(wall))
            yield return wall;
        }
      }
    }

    /// <summary>Writes the <c>initialState</c> nested object using the first zone's state as representative.</summary>
    private static void WriteInitialState(Utf8JsonWriter writer, BuildingThermalModel model)
    {
      double temperature = 25.0;
      double humidityRatio = 0.015;

      // 最初の zone の現在状態を代表値として使用
      if (model.MultiRoom.Length > 0 && model.MultiRoom[0].Zones.Length > 0)
      {
        var firstZone = model.MultiRoom[0].Zones[0];
        temperature = firstZone.Temperature;
        humidityRatio = firstZone.HumidityRatio;
      }

      writer.WritePropertyName(PropInitialState);
      writer.WriteStartObject();
      writer.WriteNumber(PropTemperature, temperature);
      writer.WriteNumber(PropHumidityRatio, humidityRatio);
      writer.WriteEndObject();
    }

    #endregion

  }
}
