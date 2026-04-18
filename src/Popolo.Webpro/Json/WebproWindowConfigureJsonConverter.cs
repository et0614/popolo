/* WebproWindowConfigureJsonConverter.cs
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
  /// JSON converter for <see cref="WebproWindowConfigure"/> — a named window
  /// specification found as a value inside <c>WindowConfigure</c>.
  /// </summary>
  /// <remarks>
  /// <para>
  /// Expected JSON shape:
  /// </para>
  /// <code>
  /// {
  ///   "windowArea":    1,
  ///   "windowWidth":   null,
  ///   "windowHeight":  null,
  ///   "inputMethod":   "ガラスの種類を入力",
  ///   "frameType":     "金属木複合製",
  ///   "layerType":     "単層",
  ///   "glassID":       "T",
  ///   "glassUvalue":   null,
  ///   "glassIvalue":   null,
  ///   "windowUvalue":  null,
  ///   "windowIvalue":  null,
  ///   "Info":          null
  /// }
  /// </code>
  /// <para>
  /// <b>Read only.</b> Writing throws <see cref="NotSupportedException"/>.
  /// </para>
  /// <para>
  /// <b>NaN for null numerics:</b> Missing or null numeric fields
  /// (<c>glassUvalue</c>, <c>glassIvalue</c>, <c>windowUvalue</c>,
  /// <c>windowIvalue</c>, and the window area/width/height fields when null)
  /// are stored as <see cref="double.NaN"/> on the DTO, matching the legacy
  /// Popolo v2.3 convention.
  /// </para>
  /// <para>
  /// <b>layerType:</b> The string <c>"単層"</c> sets
  /// <see cref="WebproWindowConfigure.IsSingleGlazing"/> to true; any other
  /// value (including <c>"複層"</c>) sets it to false.
  /// </para>
  /// </remarks>
  public sealed class WebproWindowConfigureJsonConverter : JsonConverter<WebproWindowConfigure>
  {

    #region 定数

    private const string PropWindowArea = "windowArea";
    private const string PropWindowWidth = "windowWidth";
    private const string PropWindowHeight = "windowHeight";
    private const string PropInputMethod = "inputMethod";
    private const string PropFrameType = "frameType";
    private const string PropLayerType = "layerType";
    private const string PropGlassId = "glassID";
    private const string PropGlassUvalue = "glassUvalue";
    private const string PropGlassIvalue = "glassIvalue";
    private const string PropWindowUvalue = "windowUvalue";
    private const string PropWindowIvalue = "windowIvalue";
    private const string PropInfo = "Info";

    private const string SingleLayerString = "単層";

    #endregion

    #region JsonConverter 実装

    public override WebproWindowConfigure Read(
      ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
      if (reader.TokenType != JsonTokenType.StartObject)
        throw new JsonException(
          $"Expected StartObject for {nameof(WebproWindowConfigure)}, but got {reader.TokenType}.");

      var result = new WebproWindowConfigure();

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
          case PropWindowArea: result.Area = ReadDoubleOrNaN(ref reader); break;
          case PropWindowWidth: result.Width = ReadDoubleOrNaN(ref reader); break;
          case PropWindowHeight: result.Height = ReadDoubleOrNaN(ref reader); break;
          case PropInputMethod:
            result.Method = JsonSerializer.Deserialize<WindowInputMethod>(ref reader, options);
            break;
          case PropFrameType:
            result.Frame = JsonSerializer.Deserialize<WindowFrame>(ref reader, options);
            break;
          case PropLayerType:
            {
              string? layerType = reader.TokenType == JsonTokenType.Null
                ? null
                : reader.GetString();
              result.IsSingleGlazing = layerType == SingleLayerString;
            }
            break;
          case PropGlassId:
            if (reader.TokenType == JsonTokenType.Null)
              result.GlazingID = "";
            else if (reader.TokenType != JsonTokenType.String)
              throw new JsonException(
                $"'{PropGlassId}' must be a string, but got {reader.TokenType}.");
            else
              result.GlazingID = reader.GetString() ?? "";
            break;
          case PropGlassUvalue: result.GlazingHeatTransferCoefficient = ReadDoubleOrNaN(ref reader); break;
          case PropGlassIvalue: result.GlazingSolarHeatGainRate = ReadDoubleOrNaN(ref reader); break;
          case PropWindowUvalue: result.WindowHeatTransferCoefficient = ReadDoubleOrNaN(ref reader); break;
          case PropWindowIvalue: result.WindowSolarHeatGainRate = ReadDoubleOrNaN(ref reader); break;
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
      Utf8JsonWriter writer, WebproWindowConfigure value, JsonSerializerOptions options)
    {
      throw new NotSupportedException(
        $"Writing {nameof(WebproWindowConfigure)} to JSON is not supported; " +
        $"use the Popolo native JSON schema for outbound serialization.");
    }

    #endregion

    #region ヘルパー

    private static double ReadDoubleOrNaN(ref Utf8JsonReader reader)
    {
      return reader.TokenType == JsonTokenType.Null ? double.NaN : reader.GetDouble();
    }

    #endregion
  }
}
