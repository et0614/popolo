/* WebproWindowJsonConverter.cs
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
  /// JSON converter for <see cref="WebproWindow"/> — a single entry inside a
  /// wall's <c>WindowList</c> array.
  /// </summary>
  /// <remarks>
  /// <para>
  /// Expected JSON shape:
  /// </para>
  /// <code>
  /// {
  ///   "WindowID":     "G1",    // or "無"
  ///   "WindowNumber": 16.64,   // area in m², or null
  ///   "isBlind":      "無",    // "有" or "無"
  ///   "EavesID":      "無",
  ///   "Info":         null
  /// }
  /// </code>
  /// <para>
  /// <b>Read only.</b> Writing throws <see cref="NotSupportedException"/>.
  /// </para>
  /// <para>
  /// <b>Key names follow the actual WEBPRO JSON</b> (builelib-style):
  /// <c>WindowID</c> and <c>WindowNumber</c> — not <c>ID</c> or <c>Number</c>
  /// as used by the legacy Popolo v2.3 reader.
  /// </para>
  /// <para>
  /// <b>isBlind parsing:</b> String <c>"有"</c> sets <c>HasBlind = true</c>;
  /// any other value (<c>"無"</c> or otherwise) sets it to false. Null is
  /// treated as false.
  /// </para>
  /// </remarks>
  public sealed class WebproWindowJsonConverter : JsonConverter<WebproWindow>
  {

    #region 定数

    private const string PropWindowId = "WindowID";
    private const string PropWindowNumber = "WindowNumber";
    private const string PropIsBlind = "isBlind";
    private const string PropEavesId = "EavesID";
    private const string PropInfo = "Info";

    private const string BlindYes = "有";

    #endregion

    #region JsonConverter 実装

    public override WebproWindow Read(
      ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
      if (reader.TokenType != JsonTokenType.StartObject)
        throw new JsonException(
          $"Expected StartObject for {nameof(WebproWindow)}, but got {reader.TokenType}.");

      var result = new WebproWindow();

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
          case PropWindowId:
            if (reader.TokenType == JsonTokenType.Null)
              result.ID = "";
            else if (reader.TokenType != JsonTokenType.String)
              throw new JsonException(
                $"'{PropWindowId}' must be a string, but got {reader.TokenType}.");
            else
              result.ID = reader.GetString() ?? "";
            break;
          case PropWindowNumber:
            result.Number = reader.TokenType == JsonTokenType.Null
              ? (double?)null
              : reader.GetDouble();
            break;
          case PropIsBlind:
            {
              string? blindValue = reader.TokenType == JsonTokenType.Null
                ? null
                : reader.GetString();
              result.HasBlind = blindValue == BlindYes;
            }
            break;
          case PropEavesId:
            if (reader.TokenType == JsonTokenType.Null)
              result.EavesID = "";
            else if (reader.TokenType != JsonTokenType.String)
              throw new JsonException(
                $"'{PropEavesId}' must be a string, but got {reader.TokenType}.");
            else
              result.EavesID = reader.GetString() ?? "";
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
      Utf8JsonWriter writer, WebproWindow value, JsonSerializerOptions options)
    {
      throw new NotSupportedException(
        $"Writing {nameof(WebproWindow)} to JSON is not supported; " +
        $"use the Popolo native JSON schema for outbound serialization.");
    }

    #endregion
  }
}
