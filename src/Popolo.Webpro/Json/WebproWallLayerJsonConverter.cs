/* WebproWallLayerJsonConverter.cs
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

    #region Constants

    private const string PropMaterialId = "materialID";
    private const string PropConductivity = "conductivity";
    private const string PropThickness = "thickness";
    private const string PropInfo = "Info";

    #endregion

    #region JsonConverter implementation

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
