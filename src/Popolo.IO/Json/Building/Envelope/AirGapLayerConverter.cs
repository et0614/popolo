/* AirGapLayerConverter.cs
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

    #region 定数

    private const string PropKind = "kind";
    private const string PropName = "name";
    private const string PropIsSealed = "isSealed";
    private const string PropThickness = "thickness";

    /// <summary>Expected discriminator value for this converter.</summary>
    private const string ExpectedKind = "airGapLayer";

    #endregion

    #region JsonConverter 実装

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

      // kind 識別子の検証
      if (kind != ExpectedKind)
        throw new JsonException(
          $"Expected '{PropKind}' = '{ExpectedKind}' for {nameof(AirGapLayer)}, but got '{kind ?? "(missing)"}'.");

      // 必須項目の検証
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
      writer.WriteString(PropKind, value.Kind); // 具象型から取得("airGapLayer")
      writer.WriteString(PropName, value.Name);
      writer.WriteBoolean(PropIsSealed, value.IsSealed);
      writer.WriteNumber(PropThickness, value.Thickness);
      writer.WriteEndObject();
    }

    #endregion

  }
}