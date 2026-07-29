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

    #region Constants

    private const string PropSchemaVersion = "$schemaVersion";
    private const string PropKind = "kind";
    private const string PropTimeStep = "timeStep";
    private const string PropCurrentDateTime = "currentDateTime";
    private const string PropInitialState = "initialState";
    private const string PropSun = "sun";
    private const string PropWalls = "walls";
    private const string PropMultiRooms = "multiRooms";

    // Keys inside initialState
    private const string PropTemperature = "temperature";
    private const string PropHumidityRatio = "humidityRatio";

    private const string ExpectedKind = "buildingThermalModel";
    private const string CurrentSchemaVersion = "3.0";

    // ISO 8601 format (no time zone) -- "2026-04-18T12:00:00"
    private const string Iso8601Format = "yyyy-MM-ddTHH:mm:ss";

    #endregion

    #region JsonConverter implementation

    /// <summary>Reads a <see cref="BuildingThermalModel"/> from JSON.</summary>
    public override BuildingThermalModel Read(
      ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
      if (reader.TokenType != JsonTokenType.StartObject)
        throw new JsonException($"Expected StartObject at the beginning of a {nameof(BuildingThermalModel)}, but got {reader.TokenType}.");

      // Load into a JsonDocument for two-pass processing
      using var doc = JsonDocument.ParseValue(ref reader);
      var root = doc.RootElement;

      // Validate $schemaVersion and kind
      string? schemaVersion = GetOptionalString(root, PropSchemaVersion);
      // The $schemaVersion value is not enforced at this point (read only, for future use).
      // Handling of invalid values will be considered in a future version.

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

      // walls (read first)
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

      // Build the wall dictionary (ID → Wall)
      var wallsById = new Dictionary<int, Wall>();
      foreach (var w in wallList)
      {
        if (wallsById.ContainsKey(w.ID))
          throw new JsonException($"Duplicate wall ID {w.ID} in '{PropWalls}' array.");
        wallsById[w.ID] = w;
      }

      // multiRooms (read via DTOs, resolve against the wall dictionary, then build MultiRooms)
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

      // Build the BuildingThermalModel
      var model = new BuildingThermalModel(mRoomsList.ToArray());
      model.TimeStep = timeStep;

      // Initial temperature and humidity
      model.InitializeAirState(initialTemperature, initialHumidityRatio);

      // External conditions (apply Sun and CurrentDateTime)
      // Outdoor temperature/humidity/nocturnal radiation are not stored in JSON. Initialize them to 0.
      model.UpdateOutdoorCondition(currentDateTime, sun, 0.0, 0.0, 0.0);

      return model;
    }

    /// <summary>Writes a <see cref="BuildingThermalModel"/> to JSON.</summary>
    public override void Write(
      Utf8JsonWriter writer, BuildingThermalModel value, JsonSerializerOptions options)
    {
      if (value is null)
        throw new ArgumentNullException(nameof(value));

      // Reassign sequential IDs to walls in preparation for serialization
      // (so the IDs referenced from MultiRooms/Zone reliably match the IDs in the walls array)
      AssignSequentialWallIds(value);

      writer.WriteStartObject();

      writer.WriteString(PropSchemaVersion, CurrentSchemaVersion);
      writer.WriteString(PropKind, ExpectedKind);
      writer.WriteNumber(PropTimeStep, value.TimeStep);

      // currentDateTime(ISO 8601)
      writer.WriteString(PropCurrentDateTime,
        value.CurrentDateTime.ToString(Iso8601Format, CultureInfo.InvariantCulture));

      // initialState (the initial temperature is arbitrary. Runtime state is not saved,
      // so BuildingThermalModel has no API that directly reports the "initial
      // temperature and humidity", but in practice Temperature / HumidityRatio may be used.
      // Read the current values of the Zones in MultiRooms; with multiple zones, use the first Zone as the representative.)
      WriteInitialState(writer, value);

      // sun
      writer.WritePropertyName(PropSun);
      if (value.Sun is IReadOnlySun readOnlySun)
      {
        // Sun is a concrete type, so cast directly and serialize
        if (readOnlySun is Sun concreteSun)
          JsonSerializer.Serialize(writer, concreteSun, options);
        else
        {
          // If another implementation existed, it would be copied into Sun (none today; just in case)
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

    #region Helpers

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
      innerReader.Read(); // Advance to StartObject
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

      // Use the current state of the first zone as the representative value
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
