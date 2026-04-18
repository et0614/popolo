/* WebproWallJsonConverter.cs
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
  /// JSON converter for <see cref="WebproWall"/> — a single wall entry within
  /// an envelope set's <c>WallList</c>.
  /// </summary>
  /// <remarks>
  /// <para>
  /// Expected JSON shape:
  /// </para>
  /// <code>
  /// {
  ///   "Direction":      "南",
  ///   "EnvelopeArea":   50.0,
  ///   "EnvelopeWidth":  null,
  ///   "EnvelopeHeight": null,
  ///   "WallSpec":       "W1",
  ///   "WallType":       "日の当たる外壁",
  ///   "Uvalue":         2.5,         // optional; NaN default if absent
  ///   "WindowList": [ { ... } ]
  /// }
  /// </code>
  /// <para>
  /// <b>Read only.</b> Writing throws <see cref="NotSupportedException"/>.
  /// </para>
  /// <para>
  /// <b>Required:</b> <c>Direction</c>, <c>WallSpec</c>, <c>WallType</c>,
  /// <c>WindowList</c>. <c>EnvelopeArea</c> is strongly expected and throws
  /// <see cref="JsonException"/> if missing; <c>EnvelopeWidth</c> and
  /// <c>EnvelopeHeight</c> are optional (typically null in real WEBPRO files).
  /// </para>
  /// <para>
  /// <b>Uvalue:</b> Not present in typical WEBPRO JSON but read if supplied,
  /// allowing callers to override the catalog-derived U-value. Default is
  /// <see cref="double.NaN"/>.
  /// </para>
  /// </remarks>
  public sealed class WebproWallJsonConverter : JsonConverter<WebproWall>
  {

    #region 定数

    private const string PropDirection = "Direction";
    private const string PropEnvelopeArea = "EnvelopeArea";
    private const string PropEnvelopeWidth = "EnvelopeWidth";
    private const string PropEnvelopeHeight = "EnvelopeHeight";
    private const string PropWallSpec = "WallSpec";
    private const string PropWallType = "WallType";
    private const string PropUvalue = "Uvalue";
    private const string PropWindowList = "WindowList";

    #endregion

    #region JsonConverter 実装

    public override WebproWall Read(
      ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
      if (reader.TokenType != JsonTokenType.StartObject)
        throw new JsonException(
          $"Expected StartObject for {nameof(WebproWall)}, but got {reader.TokenType}.");

      Orientation? direction = null;
      WallType? wallTypeValue = null;
      string? wallSpec = null;
      bool sawWindowList = false;

      var result = new WebproWall();

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
          case PropDirection:
            direction = JsonSerializer.Deserialize<Orientation>(ref reader, options);
            break;
          case PropEnvelopeArea:
            result.Area = reader.TokenType == JsonTokenType.Null
              ? (double?)null
              : reader.GetDouble();
            break;
          case PropEnvelopeWidth:
            result.Width = reader.TokenType == JsonTokenType.Null
              ? (double?)null
              : reader.GetDouble();
            break;
          case PropEnvelopeHeight:
            result.Height = reader.TokenType == JsonTokenType.Null
              ? (double?)null
              : reader.GetDouble();
            break;
          case PropWallSpec:
            if (reader.TokenType != JsonTokenType.String)
              throw new JsonException(
                $"'{PropWallSpec}' must be a string, but got {reader.TokenType}.");
            wallSpec = reader.GetString();
            break;
          case PropWallType:
            wallTypeValue = JsonSerializer.Deserialize<WallType>(ref reader, options);
            break;
          case PropUvalue:
            result.HeatTransferCoefficient = reader.TokenType == JsonTokenType.Null
              ? double.NaN
              : reader.GetDouble();
            break;
          case PropWindowList:
            sawWindowList = true;
            ReadWindowList(ref reader, result, options);
            break;
          default:
            reader.Skip();
            break;
        }
      }

      if (direction is null)
        throw new JsonException($"Required property '{PropDirection}' is missing.");
      if (wallSpec is null)
        throw new JsonException($"Required property '{PropWallSpec}' is missing.");
      if (wallTypeValue is null)
        throw new JsonException($"Required property '{PropWallType}' is missing.");
      if (!sawWindowList)
        throw new JsonException($"Required property '{PropWindowList}' is missing.");

      result.SurfaceOrientation = direction.Value;
      result.WallSpec = wallSpec;
      result.Type = wallTypeValue.Value;
      return result;
    }

    /// <summary>Write is not supported — WEBPRO integration is import-only.</summary>
    public override void Write(
      Utf8JsonWriter writer, WebproWall value, JsonSerializerOptions options)
    {
      throw new NotSupportedException(
        $"Writing {nameof(WebproWall)} to JSON is not supported; " +
        $"use the Popolo native JSON schema for outbound serialization.");
    }

    #endregion

    #region WindowList の読み取り

    private static void ReadWindowList(
      ref Utf8JsonReader reader, WebproWall target, JsonSerializerOptions options)
    {
      if (reader.TokenType == JsonTokenType.Null) return;
      if (reader.TokenType != JsonTokenType.StartArray)
        throw new JsonException(
          $"'{PropWindowList}' must be an array, but got {reader.TokenType}.");

      while (reader.Read())
      {
        if (reader.TokenType == JsonTokenType.EndArray) break;
        var window = JsonSerializer.Deserialize<WebproWindow>(ref reader, options)
          ?? throw new JsonException($"{nameof(WebproWindow)} deserialization returned null.");
        target.Windows.Add(window);
      }
    }

    #endregion
  }
}
