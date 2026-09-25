/* AirGapLayerConverter.cs
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
using Popolo.Core.Building.Envelope;

namespace Popolo.IO.Json.Building.Envelope
{
  /// <summary>
  /// JSON converter for <see cref="AirGapLayer"/>.
  /// </summary>
  /// <remarks>
  /// Serialized JSON schema:
  /// <code>
  /// {
  ///   "kind":      "airGapLayer",
  ///   "name":      "Sealed Air Gap",
  ///   "isSealed":  true,
  ///   "thickness": 0.02
  /// }
  /// </code>
  /// <para>
  /// Only the standard-thermal-resistance construction path is supported
  /// (name + isSealed + thickness). Instances constructed with a custom
  /// thermal resistance are still serialized using this schema and will be
  /// rehydrated as a sealed air gap; the custom resistance value is not
  /// preserved by this converter.
  /// </para>
  /// </remarks>
  public sealed class AirGapLayerConverter : JsonConverter<AirGapLayer>
  {

    #region Constants

    private const string PropKind = "kind";
    private const string PropName = "name";
    private const string PropIsSealed = "isSealed";
    private const string PropThickness = "thickness";

    /// <summary>Expected discriminator value for this converter.</summary>
    private const string ExpectedKind = "airGapLayer";

    #endregion

    #region JsonConverter implementation

    /// <summary>Reads an <see cref="AirGapLayer"/> from JSON.</summary>
    /// <param name="reader">UTF-8 JSON reader positioned at the start of the object.</param>
    /// <param name="typeToConvert">Target type.</param>
    /// <param name="options">Serializer options (ignored).</param>
    /// <returns>Deserialized <see cref="AirGapLayer"/>.</returns>
    /// <exception cref="JsonException">Thrown when the JSON is malformed or a required property is missing.</exception>
    public override AirGapLayer Read(
      ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
      if (reader.TokenType != JsonTokenType.StartObject)
        throw new JsonException($"Expected StartObject at the beginning of an {nameof(AirGapLayer)}, but got {reader.TokenType}.");

      string? kind = null;
      string? name = null;
      bool? isSealed = null;
      double? thickness = null;

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
          case PropKind:
            kind = reader.GetString();
            break;
          case PropName:
            name = reader.GetString();
            break;
          case PropIsSealed:
            isSealed = reader.GetBoolean();
            break;
          case PropThickness:
            thickness = reader.GetDouble();
            break;
          default:
            reader.Skip();
            break;
        }
      }

      // Validate the kind discriminator
      if (kind != ExpectedKind)
        throw new JsonException(
          $"Expected '{PropKind}' = '{ExpectedKind}' for {nameof(AirGapLayer)}, but got '{kind ?? "(missing)"}'.");

      // Validate required properties
      if (name is null)
        throw new JsonException($"Required property '{PropName}' is missing from {nameof(AirGapLayer)} JSON.");
      if (isSealed is null)
        throw new JsonException($"Required property '{PropIsSealed}' is missing from {nameof(AirGapLayer)} JSON.");
      if (thickness is null)
        throw new JsonException($"Required property '{PropThickness}' is missing from {nameof(AirGapLayer)} JSON.");

      return new AirGapLayer(name, isSealed.Value, thickness.Value);
    }

    /// <summary>Writes an <see cref="AirGapLayer"/> to JSON.</summary>
    /// <param name="writer">UTF-8 JSON writer.</param>
    /// <param name="value">Layer to serialize.</param>
    /// <param name="options">Serializer options (ignored).</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public override void Write(
      Utf8JsonWriter writer, AirGapLayer value, JsonSerializerOptions options)
    {
      if (value is null)
        throw new ArgumentNullException(nameof(value));

      writer.WriteStartObject();
      writer.WriteString(PropKind, value.Kind); // obtained from the concrete type ("airGapLayer")
      writer.WriteString(PropName, value.Name);
      writer.WriteBoolean(PropIsSealed, value.IsSealed);
      writer.WriteNumber(PropThickness, value.Thickness);
      writer.WriteEndObject();
    }

    #endregion

  }
}