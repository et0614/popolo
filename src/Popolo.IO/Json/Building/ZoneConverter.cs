/* ZoneConverter.cs
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

namespace Popolo.IO.Json.Building
{
  /// <summary>
  /// JSON converter for <see cref="Zone"/>.
  /// </summary>
  /// <remarks>
  /// <para>
  /// Serialized JSON schema:
  /// </para>
  /// <code>
  /// {
  ///   "kind":             "zone",
  ///   "name":             "Living Room",
  ///   "airMass":          360.0,
  ///   "floorArea":        30.0,
  ///   "heatCapacity":     0.0,
  ///   "moistureCapacity": 0.0,
  ///   "capacities": {
  ///     "heating":      5000,
  ///     "cooling":      5000,
  ///     "humidifying":  0.001,
  ///     "dehumidifying":0.001
  ///   },
  ///   "baseHeatGain": {
  ///     "convectiveHeatGain": 100,
  ///     "radiativeHeatGain":  50,
  ///     "moistureGain":       0.0001
  ///   },
  ///   "walls": [
  ///     { "wallId": 42, "sideF": true },
  ///     { "wallId": 43, "sideF": false }
  ///   ],
  ///   "windows": [
  ///     { "kind": "window", ... }
  ///   ]
  /// }
  /// </code>
  /// <para>
  /// <b>Optional capacities:</b> Any of the four capacities that equals
  /// <see cref="double.PositiveInfinity"/> is omitted from the JSON. On read,
  /// missing capacities default to <see cref="double.PositiveInfinity"/>. If all
  /// four are infinite, the entire <c>capacities</c> object is omitted.
  /// </para>
  /// <para>
  /// <b>Optional baseHeatGain:</b> If the base heat gain is
  /// <see cref="SimpleHeatGain"/> with all three fields at zero, the object is
  /// omitted on write.
  /// </para>
  /// <para>
  /// <b>Wall references and windows pending resolution:</b>
  /// The <c>walls</c> array and the deserialized <see cref="Window"/> list
  /// cannot be attached to the <see cref="Zone"/> directly — wall attachment is
  /// a <c>MultiRooms</c>-level operation that requires access to the wall table.
  /// After reading, this converter stashes the parsed wall references and
  /// windows on a side-band <see cref="ZoneDeserializationContext"/> keyed to
  /// the returned <see cref="Zone"/>, where <c>MultiRoomsConverter</c> later
  /// retrieves them.
  /// </para>
  /// <para>
  /// <b>Required sibling converters:</b>
  /// <see cref="Popolo.IO.Json.Envelope.WindowConverter"/> (for the nested
  /// windows) and its own transitive dependencies must be registered.
  /// </para>
  /// <para>
  /// <b>Out of scope:</b> Runtime state (<see cref="Zone.Temperature"/>,
  /// <see cref="Zone.HumidityRatio"/>), dynamic <c>heatGains</c> lists,
  /// setpoint / control flags, and <see cref="Zone.RoomIndex"/> are not
  /// serialized. RoomIndex is implied by position in the enclosing
  /// <c>MultiRooms.rooms</c> array.
  /// </para>
  /// </remarks>
  public sealed class ZoneConverter : JsonConverter<Zone>
  {

    #region 定数

    private const string PropKind = "kind";
    private const string PropName = "name";
    private const string PropAirMass = "airMass";
    private const string PropFloorArea = "floorArea";
    private const string PropHeatCapacity = "heatCapacity";
    private const string PropMoistureCapacity = "moistureCapacity";
    private const string PropCapacities = "capacities";
    private const string PropBaseHeatGain = "baseHeatGain";
    private const string PropWalls = "walls";
    private const string PropWindows = "windows";

    // capacities 内のキー
    private const string PropHeating = "heating";
    private const string PropCooling = "cooling";
    private const string PropHumidifying = "humidifying";
    private const string PropDehumidifying = "dehumidifying";

    // baseHeatGain 内のキー
    private const string PropConvectiveHeatGain = "convectiveHeatGain";
    private const string PropRadiativeHeatGain = "radiativeHeatGain";
    private const string PropMoistureGain = "moistureGain";

    // walls 内のキー
    private const string PropWallId = "wallId";
    private const string PropSideF = "sideF";

    private const string ExpectedKind = "zone";

    #endregion

    #region 内部型

    private readonly struct Capacities
    {
      public double Heating { get; }
      public double Cooling { get; }
      public double Humidifying { get; }
      public double Dehumidifying { get; }

      public Capacities(double h, double c, double hu, double de)
      {
        Heating = h; Cooling = c; Humidifying = hu; Dehumidifying = de;
      }

      public static Capacities Unlimited => new Capacities(
        double.PositiveInfinity, double.PositiveInfinity,
        double.PositiveInfinity, double.PositiveInfinity);
    }

    private readonly struct HeatGainValues
    {
      public double ConvectiveHeatGain { get; }
      public double RadiativeHeatGain { get; }
      public double MoistureGain { get; }

      public HeatGainValues(double conv, double rad, double moist)
      {
        ConvectiveHeatGain = conv; RadiativeHeatGain = rad; MoistureGain = moist;
      }
    }

    #endregion

    #region JsonConverter 実装

    /// <summary>Reads a <see cref="Zone"/> from JSON.</summary>
    public override Zone Read(
      ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
      if (reader.TokenType != JsonTokenType.StartObject)
        throw new JsonException($"Expected StartObject at the beginning of a {nameof(Zone)}, but got {reader.TokenType}.");

      string? kind = null;
      string? name = null;
      double? airMass = null;
      double? floorArea = null;
      double? heatCapacity = null;
      double? moistureCapacity = null;
      Capacities capacities = Capacities.Unlimited;
      HeatGainValues? baseHeatGain = null;
      List<WallSurfaceReference> wallRefs = new List<WallSurfaceReference>();
      List<Window> windows = new List<Window>();

      while (reader.Read())
      {
        if (reader.TokenType == JsonTokenType.EndObject)
          break;

        if (reader.TokenType != JsonTokenType.PropertyName)
          throw new JsonException($"Expected PropertyName, but got {reader.TokenType}.");

        string? propName = reader.GetString();
        if (!reader.Read())
          throw new JsonException($"Unexpected end of JSON while reading property '{propName}'.");

        switch (propName)
        {
          case PropKind: kind = reader.GetString(); break;
          case PropName: name = reader.GetString(); break;
          case PropAirMass: airMass = reader.GetDouble(); break;
          case PropFloorArea: floorArea = reader.GetDouble(); break;
          case PropHeatCapacity: heatCapacity = reader.GetDouble(); break;
          case PropMoistureCapacity: moistureCapacity = reader.GetDouble(); break;
          case PropCapacities: capacities = ReadCapacities(ref reader); break;
          case PropBaseHeatGain: baseHeatGain = ReadBaseHeatGain(ref reader); break;
          case PropWalls: wallRefs = ReadWallReferences(ref reader); break;
          case PropWindows: windows = ReadWindows(ref reader, options); break;
          default: reader.Skip(); break;
        }
      }

      if (kind != ExpectedKind)
        throw new JsonException(
          $"Expected '{PropKind}' = '{ExpectedKind}' for {nameof(Zone)}, but got '{kind ?? "(missing)"}'.");
      if (name is null)
        throw new JsonException($"Required property '{PropName}' is missing from {nameof(Zone)} JSON.");
      if (airMass is null)
        throw new JsonException($"Required property '{PropAirMass}' is missing from {nameof(Zone)} JSON.");

      Zone zone = floorArea is not null
        ? new Zone(name, airMass.Value, floorArea.Value)
        : new Zone(name, airMass.Value);

      if (heatCapacity is not null) zone.HeatCapacity = heatCapacity.Value;
      if (moistureCapacity is not null) zone.MoistureCapacity = moistureCapacity.Value;

      zone.HeatingCapacity = capacities.Heating;
      zone.CoolingCapacity = capacities.Cooling;
      zone.HumidifyingCapacity = capacities.Humidifying;
      zone.DehumidifyingCapacity = capacities.Dehumidifying;

      if (baseHeatGain is not null)
      {
        zone.SetBaseHeatGain(
          baseHeatGain.Value.ConvectiveHeatGain,
          baseHeatGain.Value.RadiativeHeatGain,
          baseHeatGain.Value.MoistureGain);
      }

      // 壁参照と windows を side-band コンテキストに保管。
      // MultiRoomsConverter がこれを拾って MultiRooms 側で結合を行う。
      ZoneDeserializationContext.Attach(zone,
        new ZoneDeserializationContext(wallRefs, windows));

      return zone;
    }

    /// <summary>Writes a <see cref="Zone"/> to JSON.</summary>
    public override void Write(
      Utf8JsonWriter writer, Zone value, JsonSerializerOptions options)
    {
      if (value is null)
        throw new ArgumentNullException(nameof(value));

      writer.WriteStartObject();
      writer.WriteString(PropKind, ExpectedKind);
      writer.WriteString(PropName, value.Name);
      writer.WriteNumber(PropAirMass, value.AirMass);
      writer.WriteNumber(PropFloorArea, value.FloorArea);
      writer.WriteNumber(PropHeatCapacity, value.HeatCapacity);
      writer.WriteNumber(PropMoistureCapacity, value.MoistureCapacity);

      WriteCapacitiesIfAny(writer,
        value.HeatingCapacity, value.CoolingCapacity,
        value.HumidifyingCapacity, value.DehumidifyingCapacity);

      WriteBaseHeatGainIfAny(writer, value.BaseHeatGain);

      WriteWallReferences(writer, value.GetWallReferences());

      WriteWindows(writer, value.GetWindows(), options);

      writer.WriteEndObject();
    }

    #endregion

    #region capacities の読み書き

    private static Capacities ReadCapacities(ref Utf8JsonReader reader)
    {
      if (reader.TokenType != JsonTokenType.StartObject)
        throw new JsonException($"Expected StartObject for '{PropCapacities}', but got {reader.TokenType}.");

      double h = double.PositiveInfinity;
      double c = double.PositiveInfinity;
      double hu = double.PositiveInfinity;
      double de = double.PositiveInfinity;

      while (reader.Read())
      {
        if (reader.TokenType == JsonTokenType.EndObject) break;
        if (reader.TokenType != JsonTokenType.PropertyName)
          throw new JsonException($"Expected PropertyName in '{PropCapacities}', but got {reader.TokenType}.");

        string? propName = reader.GetString();
        if (!reader.Read())
          throw new JsonException($"Unexpected end of JSON while reading '{PropCapacities}.{propName}'.");

        switch (propName)
        {
          case PropHeating: h = reader.GetDouble(); break;
          case PropCooling: c = reader.GetDouble(); break;
          case PropHumidifying: hu = reader.GetDouble(); break;
          case PropDehumidifying: de = reader.GetDouble(); break;
          default: reader.Skip(); break;
        }
      }

      return new Capacities(h, c, hu, de);
    }

    private static void WriteCapacitiesIfAny(
      Utf8JsonWriter writer,
      double heating, double cooling, double humidifying, double dehumidifying)
    {
      bool hasH = !double.IsPositiveInfinity(heating);
      bool hasC = !double.IsPositiveInfinity(cooling);
      bool hasHu = !double.IsPositiveInfinity(humidifying);
      bool hasDe = !double.IsPositiveInfinity(dehumidifying);

      if (!hasH && !hasC && !hasHu && !hasDe) return;

      writer.WritePropertyName(PropCapacities);
      writer.WriteStartObject();
      if (hasH) writer.WriteNumber(PropHeating, heating);
      if (hasC) writer.WriteNumber(PropCooling, cooling);
      if (hasHu) writer.WriteNumber(PropHumidifying, humidifying);
      if (hasDe) writer.WriteNumber(PropDehumidifying, dehumidifying);
      writer.WriteEndObject();
    }

    #endregion

    #region baseHeatGain の読み書き

    private static HeatGainValues ReadBaseHeatGain(ref Utf8JsonReader reader)
    {
      if (reader.TokenType != JsonTokenType.StartObject)
        throw new JsonException($"Expected StartObject for '{PropBaseHeatGain}', but got {reader.TokenType}.");

      double? conv = null, rad = null, moist = null;

      while (reader.Read())
      {
        if (reader.TokenType == JsonTokenType.EndObject) break;
        if (reader.TokenType != JsonTokenType.PropertyName)
          throw new JsonException($"Expected PropertyName in '{PropBaseHeatGain}', but got {reader.TokenType}.");

        string? propName = reader.GetString();
        if (!reader.Read())
          throw new JsonException($"Unexpected end of JSON while reading '{PropBaseHeatGain}.{propName}'.");

        switch (propName)
        {
          case PropConvectiveHeatGain: conv = reader.GetDouble(); break;
          case PropRadiativeHeatGain: rad = reader.GetDouble(); break;
          case PropMoistureGain: moist = reader.GetDouble(); break;
          default: reader.Skip(); break;
        }
      }

      if (conv is null) throw new JsonException($"Required '{PropBaseHeatGain}.{PropConvectiveHeatGain}' is missing.");
      if (rad is null) throw new JsonException($"Required '{PropBaseHeatGain}.{PropRadiativeHeatGain}' is missing.");
      if (moist is null) throw new JsonException($"Required '{PropBaseHeatGain}.{PropMoistureGain}' is missing.");

      return new HeatGainValues(conv.Value, rad.Value, moist.Value);
    }

    private static void WriteBaseHeatGainIfAny(Utf8JsonWriter writer, IHeatGain baseGain)
    {
      if (baseGain is not SimpleHeatGain sg) return;
      if (sg.ConvectiveHeatGain == 0 && sg.RadiativeHeatGain == 0 && sg.MoistureGain == 0)
        return;

      writer.WritePropertyName(PropBaseHeatGain);
      writer.WriteStartObject();
      writer.WriteNumber(PropConvectiveHeatGain, sg.ConvectiveHeatGain);
      writer.WriteNumber(PropRadiativeHeatGain, sg.RadiativeHeatGain);
      writer.WriteNumber(PropMoistureGain, sg.MoistureGain);
      writer.WriteEndObject();
    }

    #endregion

    #region walls 参照配列の読み書き

    private static List<WallSurfaceReference> ReadWallReferences(ref Utf8JsonReader reader)
    {
      if (reader.TokenType != JsonTokenType.StartArray)
        throw new JsonException($"Expected StartArray for '{PropWalls}', but got {reader.TokenType}.");

      var result = new List<WallSurfaceReference>();
      while (reader.Read())
      {
        if (reader.TokenType == JsonTokenType.EndArray) break;
        if (reader.TokenType != JsonTokenType.StartObject)
          throw new JsonException($"Each '{PropWalls}' entry must be an object, but got {reader.TokenType}.");

        int? wallId = null;
        bool? sideF = null;

        while (reader.Read())
        {
          if (reader.TokenType == JsonTokenType.EndObject) break;
          if (reader.TokenType != JsonTokenType.PropertyName)
            throw new JsonException($"Expected PropertyName in wall reference, but got {reader.TokenType}.");

          string? propName = reader.GetString();
          if (!reader.Read())
            throw new JsonException($"Unexpected end of JSON while reading wall reference property '{propName}'.");

          switch (propName)
          {
            case PropWallId: wallId = reader.GetInt32(); break;
            case PropSideF: sideF = reader.GetBoolean(); break;
            default: reader.Skip(); break;
          }
        }

        if (wallId is null) throw new JsonException($"Required '{PropWalls}[].{PropWallId}' is missing.");
        if (sideF is null) throw new JsonException($"Required '{PropWalls}[].{PropSideF}' is missing.");

        result.Add(new WallSurfaceReference(wallId.Value, sideF.Value));
      }
      return result;
    }

    private static void WriteWallReferences(
      Utf8JsonWriter writer, WallSurfaceReference[] wallRefs)
    {
      writer.WritePropertyName(PropWalls);
      writer.WriteStartArray();
      foreach (var r in wallRefs)
      {
        writer.WriteStartObject();
        writer.WriteNumber(PropWallId, r.WallId);
        writer.WriteBoolean(PropSideF, r.IsSideF);
        writer.WriteEndObject();
      }
      writer.WriteEndArray();
    }

    #endregion

    #region windows の読み書き

    private static List<Window> ReadWindows(
      ref Utf8JsonReader reader, JsonSerializerOptions options)
    {
      if (reader.TokenType != JsonTokenType.StartArray)
        throw new JsonException($"Expected StartArray for '{PropWindows}', but got {reader.TokenType}.");

      var result = new List<Window>();
      while (reader.Read())
      {
        if (reader.TokenType == JsonTokenType.EndArray) break;
        var window = JsonSerializer.Deserialize<Window>(ref reader, options)
          ?? throw new JsonException($"{nameof(Window)} deserialization returned null.");
        result.Add(window);
      }
      return result;
    }

    private static void WriteWindows(
      Utf8JsonWriter writer, IReadOnlyWindow[] windows, JsonSerializerOptions options)
    {
      writer.WritePropertyName(PropWindows);
      writer.WriteStartArray();
      foreach (var w in windows)
      {
        if (w is Window concrete)
          JsonSerializer.Serialize(writer, concrete, options);
        else
          throw new JsonException(
            $"Unsupported {nameof(IReadOnlyWindow)} implementation: {w?.GetType().FullName ?? "null"}.");
      }
      writer.WriteEndArray();
    }

    #endregion

  }
}
