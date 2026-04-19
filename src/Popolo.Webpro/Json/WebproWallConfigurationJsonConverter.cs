/* WebproWallConfigurationJsonConverter.cs
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

    #region 定数

    private const string PropStructureType = "structureType";
    private const string PropSolarAbsorptionRatio = "solarAbsorptionRatio";
    private const string PropInputMethod = "inputMethod";
    private const string PropLayers = "layers";
    private const string PropInfo = "Info";

    #endregion

    #region JsonConverter 実装

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

    #region layers の読み取り

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
