/* WallConverter.cs
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
  /// <b>Unsupported constructs:</b> Only layers whose runtime type is exactly
  /// <see cref="WallLayer"/> or <see cref="AirGapLayer"/> can be written.
  /// <see cref="PCMWallLayer"/>, <see cref="HorizontalAirChamber"/> and walls with
  /// buried pipes (<see cref="Wall.AddPipe"/>) are not part of the schema; writing
  /// them throws <see cref="JsonException"/> instead of silently losing their physics.
  /// </para>
  /// <para>
  /// <b>Required sibling converters:</b> Both <see cref="WallLayerConverter"/>
  /// and <see cref="AirGapLayerConverter"/> must be registered in the same
  /// <see cref="JsonSerializerOptions"/>.
  /// </para>
  /// </remarks>
  public sealed class WallConverter : JsonConverter<Wall>
  {

    #region Constants

    private const string PropKind = "kind";
    private const string PropId = "id";
    private const string PropArea = "area";
    private const string PropComputeMoistureTransfer = "computeMoistureTransfer";
    private const string PropLayers = "layers";
    private const string PropSurfaceF = "surfaceF";
    private const string PropSurfaceB = "surfaceB";

    // Keys inside surfaceF/surfaceB
    private const string PropConvectiveCoefficient = "convectiveCoefficient";
    private const string PropShortWaveAbsorptance = "shortWaveAbsorptance";
    private const string PropLongWaveEmissivity = "longWaveEmissivity";

    // kind values inside layers
    private const string KindWallLayer = "wallLayer";
    private const string KindAirGapLayer = "airGapLayer";

    private const string ExpectedKind = "wall";

    #endregion

    #region Internal types

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

    #region JsonConverter implementation

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

      // kind discriminator
      if (kind != ExpectedKind)
        throw new JsonException(
          $"Expected '{PropKind}' = '{ExpectedKind}' for {nameof(Wall)}, but got '{kind ?? "(missing)"}'.");

      // Required fields
      if (area is null)
        throw new JsonException($"Required property '{PropArea}' is missing from {nameof(Wall)} JSON.");
      if (layers is null)
        throw new JsonException($"Required property '{PropLayers}' is missing from {nameof(Wall)} JSON.");
      if (layers.Count == 0)
        throw new JsonException($"Property '{PropLayers}' must contain at least one layer.");

      // Create the Wall
      bool cmt = computeMoistureTransfer ?? false;
      var wall = new Wall(area.Value, layers.ToArray(), cmt);

      // Optional properties
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

      // Refuse constructs the schema cannot represent before emitting anything.
      EnsureNoBuriedPipes(value);
      foreach (var layer in value.Layers)
        EnsureSupportedLayerType(layer);

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

    #region Reading and writing the layer array

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

        // To dispatch on kind we would need to note the current position, look ahead,
        // and rewind — which is impossible, so pre-scan with a copied Utf8JsonReader before reading the body.
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
      // The reader is pointing at StartObject. Scan from there to EndObject to
      // find the "kind" property. Return null if it is not found.
      if (reader.TokenType != JsonTokenType.StartObject)
        return null;

      int depth = 0;
      while (reader.Read())
      {
        if (reader.TokenType == JsonTokenType.StartObject || reader.TokenType == JsonTokenType.StartArray)
          depth++;
        else if (reader.TokenType == JsonTokenType.EndObject || reader.TokenType == JsonTokenType.EndArray)
        {
          if (depth == 0) return null; // outer EndObject; kind was not found
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
      // Validate every layer before writing anything so an unsupported layer does
      // not leave a half-written array behind.
      foreach (var layer in layers)
        EnsureSupportedLayerType(layer);

      writer.WriteStartArray();
      foreach (var layer in layers)
      {
        // Dispatch on the EXACT runtime type. Other WallLayer subclasses
        // (PCMWallLayer, HorizontalAirChamber, user-defined subclasses) do not
        // override Kind and would otherwise be written as a plain "wallLayer",
        // silently losing their physics.
        if (layer.GetType() == typeof(AirGapLayer))
          JsonSerializer.Serialize(writer, (AirGapLayer)layer, options);
        else
          JsonSerializer.Serialize(writer, (WallLayer)layer, options);
      }
      writer.WriteEndArray();
    }

    /// <summary>
    /// Throws <see cref="JsonException"/> unless <paramref name="layer"/> is exactly
    /// <see cref="WallLayer"/> or <see cref="AirGapLayer"/>.
    /// </summary>
    internal static void EnsureSupportedLayerType(IReadOnlyWallLayer? layer)
    {
      Type? t = layer?.GetType();
      if (t == typeof(WallLayer) || t == typeof(AirGapLayer)) return;
      throw new JsonException(
        $"Wall layer type '{t?.FullName ?? "null"}' (name: '{layer?.Name}') is not supported by "
        + "Popolo JSON serialization. Only WallLayer and AirGapLayer can be serialized; "
        + "PCMWallLayer and HorizontalAirChamber would otherwise be silently written as a "
        + "plain 'wallLayer' and lose their physics.");
    }

    /// <summary>
    /// Throws <see cref="JsonException"/> when <paramref name="wall"/> has buried pipes
    /// (<see cref="Wall.AddPipe"/>), which are not part of the JSON schema.
    /// </summary>
    /// <remarks>
    /// Core exposes no "has pipe" query, so each node is probed with
    /// <see cref="Wall.GetPipe(int)"/> (which throws <see cref="KeyNotFoundException"/>
    /// for a node without a pipe).
    /// </remarks>
    private static void EnsureNoBuriedPipes(Wall wall)
    {
      for (int node = 0; node < wall.NodeCount; node++)
      {
        bool hasPipe;
        try { hasPipe = wall.GetPipe(node) != null; }
        catch (KeyNotFoundException) { hasPipe = false; }
        if (hasPipe)
          throw new JsonException(
            $"Wall (ID {wall.ID}) has a buried pipe at node {node}. Buried pipes "
            + "(Wall.AddPipe) are not supported by Popolo JSON serialization; writing the "
            + "wall would silently drop the pipe.");
      }
    }

    #endregion

    #region Reading and writing surface coefficient objects

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
