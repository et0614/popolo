/* WebproWallLayerJsonConverter.cs
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
  /// JSON converter for <see cref="WebproWallLayer"/> — a single layer entry
  /// inside a <c>WallConfigure.layers</c> array.
  /// </summary>
  /// <remarks>
  /// <para>
  /// Expected JSON shape:
  /// </para>
  /// <code>
  /// {
  ///   "materialID":   "コンクリート",
  ///   "conductivity": null,   // or a number
  ///   "thickness":    150.0,  // in millimetres, or null
  ///   "Info":         null    // or a remark string
  /// }
  /// </code>
  /// <para>
  /// <b>Read only.</b> Writing is not supported because WEBPRO integration is
  /// import-only in v3.0; any outbound serialization should use the native
  /// Popolo JSON schema via <c>PopoloJsonSerializer</c>.
  /// </para>
  /// <para>
  /// <b>Required:</b> <c>materialID</c>. Missing or non-string values throw
  /// <see cref="JsonException"/>.
  /// </para>
  /// <para>
  /// <b>Optional:</b> <c>conductivity</c>, <c>thickness</c>, <c>Info</c>.
  /// Null JSON values become null <see cref="Nullable{T}"/> for numeric fields
  /// and null <see cref="string"/> for <c>Info</c>.
  /// </para>
  /// <para>
  /// <b>Unknown properties are skipped</b> for forward compatibility.
  /// </para>
  /// </remarks>
  public sealed class WebproWallLayerJsonConverter : JsonConverter<WebproWallLayer>
  {

    #region 定数

    private const string PropMaterialId = "materialID";
    private const string PropConductivity = "conductivity";
    private const string PropThickness = "thickness";
    private const string PropInfo = "Info";

    #endregion

    #region JsonConverter 実装

    /// <summary>Reads a <see cref="WebproWallLayer"/> from JSON.</summary>
    public override WebproWallLayer Read(
      ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
      if (reader.TokenType != JsonTokenType.StartObject)
        throw new JsonException(
          $"Expected StartObject for {nameof(WebproWallLayer)}, but got {reader.TokenType}.");

      string? materialId = null;
      double? conductivity = null;
      double? thickness = null;
      string? info = null;

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
          case PropMaterialId:
            if (reader.TokenType != JsonTokenType.String)
              throw new JsonException(
                $"'{PropMaterialId}' must be a string, but got {reader.TokenType}.");
            materialId = reader.GetString();
            break;
          case PropConductivity:
            conductivity = reader.TokenType == JsonTokenType.Null
              ? (double?)null
              : reader.GetDouble();
            break;
          case PropThickness:
            thickness = reader.TokenType == JsonTokenType.Null
              ? (double?)null
              : reader.GetDouble();
            break;
          case PropInfo:
            info = reader.TokenType == JsonTokenType.Null ? null : reader.GetString();
            break;
          default:
            reader.Skip();
            break;
        }
      }

      if (materialId is null)
        throw new JsonException(
          $"Required property '{PropMaterialId}' is missing from {nameof(WebproWallLayer)} JSON.");

      return new WebproWallLayer
      {
        MaterialID = materialId,
        Conductivity = conductivity,
        Thickness = thickness,
        Information = info,
      };
    }

    /// <summary>Write is not supported — WEBPRO integration is import-only.</summary>
    public override void Write(
      Utf8JsonWriter writer, WebproWallLayer value, JsonSerializerOptions options)
    {
      throw new NotSupportedException(
        $"Writing {nameof(WebproWallLayer)} to JSON is not supported; " +
        $"use the Popolo native JSON schema for outbound serialization.");
    }

    #endregion
  }
}
