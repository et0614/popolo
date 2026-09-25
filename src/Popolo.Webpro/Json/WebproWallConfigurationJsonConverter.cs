/* WebproWallConfigurationJsonConverter.cs
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

using System;
using System.Text.Json;
using System.Text.Json.Serialization;

using Popolo.Webpro.Domain;
using Popolo.Webpro.Domain.Enums;

namespace Popolo.Webpro.Json
{
  /// <summary>
  /// JSON converter for <see cref="WebproWallConfiguration"/> — a named wall
  /// construction found as a value inside <c>WallConfigure</c>.
  /// </summary>
  /// <remarks>
  /// <para>
  /// Expected JSON shape:
  /// </para>
  /// <code>
  /// {
  ///   "wall_type_webpro":      "外壁",          // optional; ignored
  ///   "structureType":         "その他",        // null → None
  ///   "solarAbsorptionRatio":  null,
  ///   "inputMethod":           "建材構成を入力", // null → None
  ///   "layers": [ { "materialID": "...", ... } ],
  ///   "Info":                  null
  /// }
  /// </code>
  /// <para>
  /// <b>Read only.</b> Writing throws <see cref="NotSupportedException"/>.
  /// </para>
  /// <para>
  /// <b>Required converters in options:</b>
  /// <see cref="WebproWallLayerJsonConverter"/>,
  /// <see cref="EnumConverters.StructureTypeJsonConverter"/>, and
  /// <see cref="EnumConverters.WallInputMethodJsonConverter"/>.
  /// </para>
  /// <para>
  /// <b>Intentionally ignored:</b> <c>wall_type_webpro</c> (the wall's role
  /// is determined at the envelope level via <see cref="WallType"/>).
  /// Unknown properties are skipped for forward compatibility.
  /// </para>
  /// </remarks>
  public sealed class WebproWallConfigurationJsonConverter : JsonConverter<WebproWallConfiguration>
  {

    #region Constants

    private const string PropStructureType = "structureType";
    private const string PropSolarAbsorptionRatio = "solarAbsorptionRatio";
    private const string PropInputMethod = "inputMethod";
    private const string PropLayers = "layers";
    private const string PropInfo = "Info";

    #endregion

    #region JsonConverter implementation

    public override WebproWallConfiguration Read(
      ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
      if (reader.TokenType != JsonTokenType.StartObject)
        throw new JsonException(
          $"Expected StartObject for {nameof(WebproWallConfiguration)}, but got {reader.TokenType}.");

      var result = new WebproWallConfiguration();

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
          case PropStructureType:
            result.Structure = JsonSerializer.Deserialize<StructureType>(ref reader, options);
            break;
          case PropSolarAbsorptionRatio:
            result.SolarAbsorptionRatio = reader.TokenType == JsonTokenType.Null
              ? (double?)null
              : reader.GetDouble();
            break;
          case PropInputMethod:
            result.Method = JsonSerializer.Deserialize<WallInputMethod>(ref reader, options);
            break;
          case PropLayers:
            ReadLayers(ref reader, result, options);
            break;
          case PropInfo:
            result.Information = reader.TokenType == JsonTokenType.Null ? null : reader.GetString();
            break;
          default:
            reader.Skip();
            break;
        }
      }

      return result;
    }

    /// <summary>Write is not supported — WEBPRO integration is import-only.</summary>
    public override void Write(
      Utf8JsonWriter writer, WebproWallConfiguration value, JsonSerializerOptions options)
    {
      throw new NotSupportedException(
        $"Writing {nameof(WebproWallConfiguration)} to JSON is not supported; " +
        $"use the Popolo native JSON schema for outbound serialization.");
    }

    #endregion

    #region Reading layers

    private static void ReadLayers(
      ref Utf8JsonReader reader, WebproWallConfiguration target, JsonSerializerOptions options)
    {
      if (reader.TokenType == JsonTokenType.Null) return;
      if (reader.TokenType != JsonTokenType.StartArray)
        throw new JsonException(
          $"'{PropLayers}' must be an array, but got {reader.TokenType}.");

      while (reader.Read())
      {
        if (reader.TokenType == JsonTokenType.EndArray) break;
        var layer = JsonSerializer.Deserialize<WebproWallLayer>(ref reader, options)
          ?? throw new JsonException($"{nameof(WebproWallLayer)} deserialization returned null.");
        target.Layers.Add(layer);
      }
    }

    #endregion
  }
}
