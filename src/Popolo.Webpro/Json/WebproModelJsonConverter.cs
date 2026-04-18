/* WebproModelJsonConverter.cs
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

using Popolo.Webpro.Domain;

namespace Popolo.Webpro.Json
{
  /// <summary>
  /// JSON converter for <see cref="WebproModel"/> — the top-level WEBPRO
  /// input JSON file.
  /// </summary>
  /// <remarks>
  /// <para>
  /// Expected top-level JSON shape (truncated):
  /// </para>
  /// <code>
  /// {
  ///   "Building":            { ... },
  ///   "Rooms":               { "1F_ロビー": { ... }, ... },
  ///   "EnvelopeSet":         { "1F_ロビー": { ... }, ... },
  ///   "WallConfigure":       { "W1": { ... }, "W2": { ... } },
  ///   "WindowConfigure":     { "G1": { ... } },
  ///   "AirConditioningZone": { "1F_ロビー": { ... }, ... },
  ///   // ... many other sections (HVAC, lighting, DHW, ...) ignored
  /// }
  /// </code>
  /// <para>
  /// <b>Read only.</b> Writing throws <see cref="NotSupportedException"/>.
  /// </para>
  /// <para>
  /// <b>Required:</b> <c>Building</c>. Missing throws <see cref="JsonException"/>.
  /// </para>
  /// <para>
  /// <b>Optional but expected:</b> <c>Rooms</c>, <c>EnvelopeSet</c>,
  /// <c>WallConfigure</c>, <c>WindowConfigure</c>, <c>AirConditioningZone</c>.
  /// If absent, an empty collection is retained.
  /// </para>
  /// <para>
  /// <b>Intentionally ignored:</b> <c>CalculationMode</c>,
  /// <c>ShadingConfigure</c>, <c>HeatsourceSystem</c>,
  /// <c>SecondaryPumpSystem</c>, <c>AirHandlingSystem</c>,
  /// <c>VentilationRoom</c>, <c>VentilationUnit</c>, <c>LightingSystems</c>,
  /// <c>HotwaterRoom</c>, <c>HotwaterSupplySystems</c>, <c>Elevators</c>,
  /// <c>PhotovoltaicSystems</c>, <c>CogenerationSystems</c>,
  /// <c>SpecialInputData</c>, and any other future sections. These describe
  /// WEBPRO equipment subsystems and are not relevant to thermal load
  /// calculation.
  /// </para>
  /// <para>
  /// <b>AirConditioningZone:</b> Only the set of keys is retained. The AHU
  /// and load-assignment values are discarded.
  /// </para>
  /// </remarks>
  public sealed class WebproModelJsonConverter : JsonConverter<WebproModel>
  {

    #region 定数

    private const string PropBuilding = "Building";
    private const string PropRooms = "Rooms";
    private const string PropEnvelopeSet = "EnvelopeSet";
    private const string PropWallConfigure = "WallConfigure";
    private const string PropWindowConfigure = "WindowConfigure";
    private const string PropAirConditioningZone = "AirConditioningZone";

    #endregion

    #region JsonConverter 実装

    public override WebproModel Read(
      ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
      if (reader.TokenType != JsonTokenType.StartObject)
        throw new JsonException(
          $"Expected StartObject for {nameof(WebproModel)}, but got {reader.TokenType}.");

      WebproBuilding? building = null;
      var result = new WebproModel();

      while (reader.Read())
      {
        if (reader.TokenType == JsonTokenType.EndObject) break;
        if (reader.TokenType != JsonTokenType.PropertyName)
          throw new JsonException($"Expected PropertyName, but got {reader.TokenType}.");

        string? propName = reader.GetString();
        if (!reader.Read())
          throw new JsonException($"Unexpected end of JSON while reading '{propName}'.");

        switch (propName)
        {
          case PropBuilding:
            building = JsonSerializer.Deserialize<WebproBuilding>(ref reader, options)
              ?? throw new JsonException($"{PropBuilding} deserialization returned null.");
            break;
          case PropRooms:
            ReadDictionary(ref reader, result.Rooms, options, PropRooms);
            break;
          case PropEnvelopeSet:
            ReadDictionary(ref reader, result.Envelopes, options, PropEnvelopeSet);
            break;
          case PropWallConfigure:
            ReadDictionary(ref reader, result.WallConfigurations, options, PropWallConfigure);
            break;
          case PropWindowConfigure:
            ReadDictionary(ref reader, result.WindowConfigurations, options, PropWindowConfigure);
            break;
          case PropAirConditioningZone:
            ReadAirConditioningZoneKeys(ref reader, result.AirConditionedRoomNames);
            break;
          default:
            // All other sections (CalculationMode, HeatsourceSystem, ...) are
            // intentionally skipped.
            reader.Skip();
            break;
        }
      }

      if (building is null)
        throw new JsonException(
          $"Required top-level property '{PropBuilding}' is missing from the WEBPRO JSON.");
      result.Building = building;

      return result;
    }

    /// <summary>Write is not supported — WEBPRO integration is import-only.</summary>
    public override void Write(
      Utf8JsonWriter writer, WebproModel value, JsonSerializerOptions options)
    {
      throw new NotSupportedException(
        $"Writing {nameof(WebproModel)} to JSON is not supported; " +
        $"use the Popolo native JSON schema for outbound serialization.");
    }

    #endregion

    #region ヘルパー

    /// <summary>
    /// Reads a JSON object as a string-keyed dictionary and populates the
    /// supplied target. Null JSON values produce an empty dictionary.
    /// </summary>
    private static void ReadDictionary<T>(
      ref Utf8JsonReader reader, Dictionary<string, T> target,
      JsonSerializerOptions options, string sectionName)
    {
      if (reader.TokenType == JsonTokenType.Null) return;
      if (reader.TokenType != JsonTokenType.StartObject)
        throw new JsonException(
          $"'{sectionName}' must be an object, but got {reader.TokenType}.");

      while (reader.Read())
      {
        if (reader.TokenType == JsonTokenType.EndObject) return;
        if (reader.TokenType != JsonTokenType.PropertyName)
          throw new JsonException(
            $"Expected PropertyName inside '{sectionName}', but got {reader.TokenType}.");

        string key = reader.GetString()
          ?? throw new JsonException($"Null key inside '{sectionName}'.");

        if (!reader.Read())
          throw new JsonException(
            $"Unexpected end of JSON while reading entry '{key}' in '{sectionName}'.");

        var value = JsonSerializer.Deserialize<T>(ref reader, options)
          ?? throw new JsonException(
            $"Entry '{key}' in '{sectionName}' deserialized to null.");

        target[key] = value;
      }
    }

    /// <summary>
    /// Reads the AirConditioningZone section, collecting only the entry keys
    /// into <paramref name="target"/>. The values are skipped wholesale since
    /// WEBPRO HVAC-plant data is not needed for thermal load calculation.
    /// </summary>
    private static void ReadAirConditioningZoneKeys(
      ref Utf8JsonReader reader, HashSet<string> target)
    {
      if (reader.TokenType == JsonTokenType.Null) return;
      if (reader.TokenType != JsonTokenType.StartObject)
        throw new JsonException(
          $"'{PropAirConditioningZone}' must be an object, but got {reader.TokenType}.");

      while (reader.Read())
      {
        if (reader.TokenType == JsonTokenType.EndObject) return;
        if (reader.TokenType != JsonTokenType.PropertyName)
          throw new JsonException(
            $"Expected PropertyName inside '{PropAirConditioningZone}', but got {reader.TokenType}.");

        string key = reader.GetString()
          ?? throw new JsonException($"Null key inside '{PropAirConditioningZone}'.");
        target.Add(key);

        if (!reader.Read())
          throw new JsonException(
            $"Unexpected end of JSON while reading entry '{key}' in '{PropAirConditioningZone}'.");

        // Skip the value entirely — AHU/load fields are not needed.
        reader.Skip();
      }
    }

    #endregion
  }
}
