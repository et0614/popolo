/* WebproEnvelopeSetJsonConverter.cs
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

namespace Popolo.Webpro.Json
{
  /// <summary>
  /// JSON converter for <see cref="WebproEnvelopeSet"/> — a single envelope
  /// set within the WEBPRO <c>EnvelopeSet</c> dictionary.
  /// </summary>
  /// <remarks>
  /// <para>
  /// Expected JSON shape:
  /// </para>
  /// <code>
  /// {
  ///   "isAirconditioned": "有",
  ///   "WallList": [
  ///     { "Direction": "南", "EnvelopeArea": 50.0, ... },
  ///     ...
  ///   ]
  /// }
  /// </code>
  /// <para>
  /// <b>Read only.</b> Writing throws <see cref="NotSupportedException"/>.
  /// </para>
  /// <para>
  /// <b>Required:</b> <c>isAirconditioned</c>, <c>WallList</c>.
  /// </para>
  /// <para>
  /// <b>isAirconditioned parsing:</b> <c>"有"</c> → true, any other value
  /// → false. Null is treated as false.
  /// </para>
  /// </remarks>
  public sealed class WebproEnvelopeSetJsonConverter : JsonConverter<WebproEnvelopeSet>
  {

    #region 定数

    private const string PropIsAirconditioned = "isAirconditioned";
    private const string PropWallList = "WallList";

    private const string AirconditionedYes = "有";

    #endregion

    #region JsonConverter 実装

    public override WebproEnvelopeSet Read(
      ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
      if (reader.TokenType != JsonTokenType.StartObject)
        throw new JsonException(
          $"Expected StartObject for {nameof(WebproEnvelopeSet)}, but got {reader.TokenType}.");

      bool sawIsAirconditioned = false;
      bool sawWallList = false;
      var result = new WebproEnvelopeSet();

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
          case PropIsAirconditioned:
            {
              sawIsAirconditioned = true;
              string? value = reader.TokenType == JsonTokenType.Null
                ? null
                : reader.GetString();
              result.IsAirconditioned = value == AirconditionedYes;
            }
            break;
          case PropWallList:
            sawWallList = true;
            ReadWallList(ref reader, result, options);
            break;
          default:
            reader.Skip();
            break;
        }
      }

      if (!sawIsAirconditioned)
        throw new JsonException($"Required property '{PropIsAirconditioned}' is missing.");
      if (!sawWallList)
        throw new JsonException($"Required property '{PropWallList}' is missing.");

      return result;
    }

    /// <summary>Write is not supported — WEBPRO integration is import-only.</summary>
    public override void Write(
      Utf8JsonWriter writer, WebproEnvelopeSet value, JsonSerializerOptions options)
    {
      throw new NotSupportedException(
        $"Writing {nameof(WebproEnvelopeSet)} to JSON is not supported; " +
        $"use the Popolo native JSON schema for outbound serialization.");
    }

    #endregion

    #region WallList の読み取り

    private static void ReadWallList(
      ref Utf8JsonReader reader, WebproEnvelopeSet target, JsonSerializerOptions options)
    {
      if (reader.TokenType == JsonTokenType.Null) return;
      if (reader.TokenType != JsonTokenType.StartArray)
        throw new JsonException(
          $"'{PropWallList}' must be an array, but got {reader.TokenType}.");

      while (reader.Read())
      {
        if (reader.TokenType == JsonTokenType.EndArray) break;
        var wall = JsonSerializer.Deserialize<WebproWall>(ref reader, options)
          ?? throw new JsonException($"{nameof(WebproWall)} deserialization returned null.");
        target.Walls.Add(wall);
      }
    }

    #endregion
  }
}
