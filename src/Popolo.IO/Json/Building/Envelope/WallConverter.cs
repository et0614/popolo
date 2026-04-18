/* WallConverter.cs
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
using Popolo.Core.Building.Envelope;

namespace Popolo.IO.Json.Building.Envelope
{
  /// <summary>
  /// JSON converter for <see cref="Wall"/>.
  /// </summary>
  /// <remarks>
  /// <para>
  /// Serialized JSON schema:
  /// </para>
  /// <code>
  /// {
  ///   "kind":                    "wall",
  ///   "id":                      42,
  ///   "area":                    12.0,
  ///   "computeMoistureTransfer": false,
  ///   "layers": [
  ///     { "kind": "wallLayer",   "name": "Concrete", ... },
  ///     { "kind": "airGapLayer", "name": "Air Gap",  ... },
  ///     { "kind": "wallLayer",   "name": "Plaster",  ... }
  ///   ],
  ///   "surfaceF": {
  ///     "convectiveCoefficient": 9.3,
  ///     "shortWaveAbsorptance":  0.7,
  ///     "longWaveEmissivity":    0.9
  ///   },
  ///   "surfaceB": {
  ///     "convectiveCoefficient": 9.3,
  ///     "shortWaveAbsorptance":  0.7,
  ///     "longWaveEmissivity":    0.9
  ///   }
  /// }
  /// </code>
  /// <para>
  /// <b>Layer encoding:</b> Each element of <c>layers</c> is a flat object with a
  /// <c>kind</c> discriminator (<c>"wallLayer"</c> or <c>"airGapLayer"</c>). Layer
  /// order from F-side to B-side is preserved.
  /// </para>
  /// <para>
  /// <b>Required sibling converters:</b> Both <see cref="WallLayerConverter"/>
  /// and <see cref="AirGapLayerConverter"/> must be registered in the same
  /// <see cref="JsonSerializerOptions"/>.
  /// </para>
  /// </remarks>
  public sealed class WallConverter : JsonConverter<Wall>
  {

    #region 定数

    private const string PropKind = "kind";
    private const string PropId = "id";
    private const string PropArea = "area";
    private const string PropComputeMoistureTransfer = "computeMoistureTransfer";
    private const string PropLayers = "layers";
    private const string PropSurfaceF = "surfaceF";
    private const string PropSurfaceB = "surfaceB";

    // surfaceF/surfaceB 内のキー
    private const string PropConvectiveCoefficient = "convectiveCoefficient";
    private const string PropShortWaveAbsorptance = "shortWaveAbsorptance";
    private const string PropLongWaveEmissivity = "longWaveEmissivity";

    // layers 内の kind 値
    private const string KindWallLayer = "wallLayer";
    private const string KindAirGapLayer = "airGapLayer";

    private const string ExpectedKind = "wall";

    #endregion

    #region 内部型

    /// <summary>Bundle of the three surface coefficients for one side of a wall.</summary>
    private readonly struct SurfaceCoefficients
    {
      public double ConvectiveCoefficient { get; }
      public double ShortWaveAbsorptance { get; }
      public double LongWaveEmissivity { get; }

      public SurfaceCoefficients(double conv, double absorp, double emi)
      {
        ConvectiveCoefficient = conv;
        ShortWaveAbsorptance = absorp;
        LongWaveEmissivity = emi;
      }
    }

    #endregion

    #region JsonConverter 実装

    /// <summary>Reads a <see cref="Wall"/> from JSON.</summary>
    /// <param name="reader">UTF-8 JSON reader positioned at the start of the object.</param>
    /// <param name="typeToConvert">Target type.</param>
    /// <param name="options">Serializer options; must include <see cref="WallLayerConverter"/> and <see cref="AirGapLayerConverter"/>.</param>
    /// <returns>Deserialized <see cref="Wall"/>.</returns>
    /// <exception cref="JsonException">Thrown when the JSON is malformed or a required property is missing.</exception>
    public override Wall Read(
      ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
      if (reader.TokenType != JsonTokenType.StartObject)
        throw new JsonException($"Expected StartObject at the beginning of a {nameof(Wall)}, but got {reader.TokenType}.");

      string? kind = null;
      int? id = null;
      double? area = null;
      bool? computeMoistureTransfer = null;
      List<WallLayer>? layers = null;
      SurfaceCoefficients? surfaceF = null;
      SurfaceCoefficients? surfaceB = null;

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
          case PropId: id = reader.GetInt32(); break;
          case PropArea: area = reader.GetDouble(); break;
          case PropComputeMoistureTransfer: computeMoistureTransfer = reader.GetBoolean(); break;
          case PropLayers: layers = ReadLayerArray(ref reader, options); break;
          case PropSurfaceF: surfaceF = ReadSurfaceCoefficients(ref reader, PropSurfaceF); break;
          case PropSurfaceB: surfaceB = ReadSurfaceCoefficients(ref reader, PropSurfaceB); break;
          default: reader.Skip(); break;
        }
      }

      // kind 識別子
      if (kind != ExpectedKind)
        throw new JsonException(
          $"Expected '{PropKind}' = '{ExpectedKind}' for {nameof(Wall)}, but got '{kind ?? "(missing)"}'.");

      // 必須フィールド
      if (area is null)
        throw new JsonException($"Required property '{PropArea}' is missing from {nameof(Wall)} JSON.");
      if (layers is null)
        throw new JsonException($"Required property '{PropLayers}' is missing from {nameof(Wall)} JSON.");
      if (layers.Count == 0)
        throw new JsonException($"Property '{PropLayers}' must contain at least one layer.");

      // Wall 生成
      bool cmt = computeMoistureTransfer ?? false;
      var wall = new Wall(area.Value, layers.ToArray(), cmt);

      // オプションプロパティ
      if (id is not null) wall.ID = id.Value;
      if (surfaceF is not null)
      {
        wall.ConvectiveCoefficientF = surfaceF.Value.ConvectiveCoefficient;
        wall.ShortWaveAbsorptanceF = surfaceF.Value.ShortWaveAbsorptance;
        wall.LongWaveEmissivityF = surfaceF.Value.LongWaveEmissivity;
      }
      if (surfaceB is not null)
      {
        wall.ConvectiveCoefficientB = surfaceB.Value.ConvectiveCoefficient;
        wall.ShortWaveAbsorptanceB = surfaceB.Value.ShortWaveAbsorptance;
        wall.LongWaveEmissivityB = surfaceB.Value.LongWaveEmissivity;
      }

      return wall;
    }

    /// <summary>Writes a <see cref="Wall"/> to JSON.</summary>
    /// <param name="writer">UTF-8 JSON writer.</param>
    /// <param name="value">Wall to serialize.</param>
    /// <param name="options">Serializer options; must include <see cref="WallLayerConverter"/> and <see cref="AirGapLayerConverter"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public override void Write(
      Utf8JsonWriter writer, Wall value, JsonSerializerOptions options)
    {
      if (value is null)
        throw new ArgumentNullException(nameof(value));

      writer.WriteStartObject();
      writer.WriteString(PropKind, ExpectedKind);
      writer.WriteNumber(PropId, value.ID);
      writer.WriteNumber(PropArea, value.Area);
      writer.WriteBoolean(PropComputeMoistureTransfer, value.ComputeMoistureTransfer);

      writer.WritePropertyName(PropLayers);
      WriteLayerArray(writer, value.Layers, options);

      WriteSurface(writer, PropSurfaceF,
        value.ConvectiveCoefficientF, value.ShortWaveAbsorptanceF, value.LongWaveEmissivityF);
      WriteSurface(writer, PropSurfaceB,
        value.ConvectiveCoefficientB, value.ShortWaveAbsorptanceB, value.LongWaveEmissivityB);

      writer.WriteEndObject();
    }

    #endregion

    #region レイヤー配列の読み書き

    /// <summary>Reads the <c>layers</c> array from JSON. Each element dispatches on its <c>kind</c>.</summary>
    private static List<WallLayer> ReadLayerArray(
      ref Utf8JsonReader reader, JsonSerializerOptions options)
    {
      if (reader.TokenType != JsonTokenType.StartArray)
        throw new JsonException($"Expected StartArray for '{PropLayers}', but got {reader.TokenType}.");

      var result = new List<WallLayer>();

      while (reader.Read())
      {
        if (reader.TokenType == JsonTokenType.EndArray)
          break;

        if (reader.TokenType != JsonTokenType.StartObject)
          throw new JsonException($"Each '{PropLayers}' entry must be an object, but got {reader.TokenType}.");

        // kind で分岐するため、現在位置をメモして先読み → 巻き戻しはできないので、
        // コピーの Utf8JsonReader でプリスキャンしてから本体を読む。
        Utf8JsonReader peekReader = reader;
        string? layerKind = PeekKind(ref peekReader);

        WallLayer layer = layerKind switch
        {
          KindWallLayer => JsonSerializer.Deserialize<WallLayer>(ref reader, options)
            ?? throw new JsonException($"{nameof(WallLayer)} deserialization returned null."),
          KindAirGapLayer => JsonSerializer.Deserialize<AirGapLayer>(ref reader, options)
            ?? throw new JsonException($"{nameof(AirGapLayer)} deserialization returned null."),
          _ => throw new JsonException(
            $"Unknown layer kind: '{layerKind ?? "(missing)"}'. Expected '{KindWallLayer}' or '{KindAirGapLayer}'."),
        };
        result.Add(layer);
      }

      return result;
    }

    /// <summary>
    /// Peeks the <c>kind</c> property of the object that <paramref name="reader"/>
    /// is currently pointing at (StartObject), without consuming the original reader.
    /// </summary>
    private static string? PeekKind(ref Utf8JsonReader reader)
    {
      // reader は StartObject を指している。そこから EndObject まで走査して
      // "kind" プロパティを見つける。見つからなければ null を返す。
      if (reader.TokenType != JsonTokenType.StartObject)
        return null;

      int depth = 0;
      while (reader.Read())
      {
        if (reader.TokenType == JsonTokenType.StartObject || reader.TokenType == JsonTokenType.StartArray)
          depth++;
        else if (reader.TokenType == JsonTokenType.EndObject || reader.TokenType == JsonTokenType.EndArray)
        {
          if (depth == 0) return null; // 外側 EndObject、kind が見つからなかった
          depth--;
        }
        else if (depth == 0 && reader.TokenType == JsonTokenType.PropertyName)
        {
          if (reader.GetString() == PropKind)
          {
            if (!reader.Read()) return null;
            if (reader.TokenType == JsonTokenType.String)
              return reader.GetString();
            return null;
          }
        }
      }
      return null;
    }

    /// <summary>Writes the <c>layers</c> array to JSON.</summary>
    private static void WriteLayerArray(
      Utf8JsonWriter writer, IReadOnlyWallLayer[] layers, JsonSerializerOptions options)
    {
      writer.WriteStartArray();
      foreach (var layer in layers)
      {
        // Runtime type dispatch。AirGapLayer は WallLayer を継承しているので順序が重要。
        if (layer is AirGapLayer ag)
          JsonSerializer.Serialize(writer, ag, options);
        else if (layer is WallLayer wl)
          JsonSerializer.Serialize(writer, wl, options);
        else
          throw new JsonException(
            $"Unsupported layer type: {layer?.GetType().FullName ?? "null"}.");
      }
      writer.WriteEndArray();
    }

    #endregion

    #region 表面係数オブジェクトの読み書き

    /// <summary>Reads a nested <c>surfaceF</c> or <c>surfaceB</c> object.</summary>
    private static SurfaceCoefficients ReadSurfaceCoefficients(
      ref Utf8JsonReader reader, string surfaceKey)
    {
      if (reader.TokenType != JsonTokenType.StartObject)
        throw new JsonException($"Expected StartObject for '{surfaceKey}', but got {reader.TokenType}.");

      double? conv = null;
      double? absorp = null;
      double? emi = null;

      while (reader.Read())
      {
        if (reader.TokenType == JsonTokenType.EndObject)
          break;
        if (reader.TokenType != JsonTokenType.PropertyName)
          throw new JsonException($"Expected PropertyName in '{surfaceKey}', but got {reader.TokenType}.");

        string? propName = reader.GetString();
        if (!reader.Read())
          throw new JsonException($"Unexpected end of JSON while reading '{surfaceKey}.{propName}'.");

        switch (propName)
        {
          case PropConvectiveCoefficient: conv = reader.GetDouble(); break;
          case PropShortWaveAbsorptance: absorp = reader.GetDouble(); break;
          case PropLongWaveEmissivity: emi = reader.GetDouble(); break;
          default: reader.Skip(); break;
        }
      }

      if (conv is null)
        throw new JsonException($"Required property '{surfaceKey}.{PropConvectiveCoefficient}' is missing.");
      if (absorp is null)
        throw new JsonException($"Required property '{surfaceKey}.{PropShortWaveAbsorptance}' is missing.");
      if (emi is null)
        throw new JsonException($"Required property '{surfaceKey}.{PropLongWaveEmissivity}' is missing.");

      return new SurfaceCoefficients(conv.Value, absorp.Value, emi.Value);
    }

    /// <summary>Writes a nested <c>surfaceF</c> or <c>surfaceB</c> object.</summary>
    private static void WriteSurface(
      Utf8JsonWriter writer, string surfaceKey,
      double conv, double absorp, double emi)
    {
      writer.WritePropertyName(surfaceKey);
      writer.WriteStartObject();
      writer.WriteNumber(PropConvectiveCoefficient, conv);
      writer.WriteNumber(PropShortWaveAbsorptance, absorp);
      writer.WriteNumber(PropLongWaveEmissivity, emi);
      writer.WriteEndObject();
    }

    #endregion

  }
}
