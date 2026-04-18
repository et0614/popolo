/* SimpleShadingDeviceConverter.cs
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
  /// JSON converter for <see cref="SimpleShadingDevice"/>.
  /// </summary>
  /// <remarks>
  /// Serialized JSON schema:
  /// <code>
  /// {
  ///   "kind":          "simpleShadingDevice",
  ///   "transmittance": 0.05,
  ///   "reflectance":   0.55
  /// }
  /// </code>
  /// </remarks>
  public sealed class SimpleShadingDeviceConverter : JsonConverter<SimpleShadingDevice>
  {

    #region 定数

    private const string PropKind = "kind";
    private const string PropTransmittance = "transmittance";
    private const string PropReflectance = "reflectance";

    private const string ExpectedKind = "simpleShadingDevice";

    #endregion

    #region JsonConverter 実装

    /// <summary>Reads a <see cref="SimpleShadingDevice"/> from JSON.</summary>
    /// <param name="reader">UTF-8 JSON reader positioned at the start of the object.</param>
    /// <param name="typeToConvert">Target type.</param>
    /// <param name="options">Serializer options (ignored).</param>
    /// <returns>Deserialized <see cref="SimpleShadingDevice"/>.</returns>
    /// <exception cref="JsonException">Thrown when the JSON is malformed or a required property is missing.</exception>
    public override SimpleShadingDevice Read(
      ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
      if (reader.TokenType != JsonTokenType.StartObject)
        throw new JsonException($"Expected StartObject at the beginning of a {nameof(SimpleShadingDevice)}, but got {reader.TokenType}.");

      string? kind = null;
      double? transmittance = null;
      double? reflectance = null;

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
          case PropTransmittance:
            transmittance = reader.GetDouble();
            break;
          case PropReflectance:
            reflectance = reader.GetDouble();
            break;
          default:
            reader.Skip();
            break;
        }
      }

      if (kind != ExpectedKind)
        throw new JsonException(
          $"Expected '{PropKind}' = '{ExpectedKind}' for {nameof(SimpleShadingDevice)}, but got '{kind ?? "(missing)"}'.");
      if (transmittance is null)
        throw new JsonException($"Required property '{PropTransmittance}' is missing from {nameof(SimpleShadingDevice)} JSON.");
      if (reflectance is null)
        throw new JsonException($"Required property '{PropReflectance}' is missing from {nameof(SimpleShadingDevice)} JSON.");

      return new SimpleShadingDevice(transmittance.Value, reflectance.Value);
    }

    /// <summary>Writes a <see cref="SimpleShadingDevice"/> to JSON.</summary>
    /// <param name="writer">UTF-8 JSON writer.</param>
    /// <param name="value">Shading device to serialize.</param>
    /// <param name="options">Serializer options (ignored).</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public override void Write(
      Utf8JsonWriter writer, SimpleShadingDevice value, JsonSerializerOptions options)
    {
      if (value is null)
        throw new ArgumentNullException(nameof(value));

      writer.WriteStartObject();
      writer.WriteString(PropKind, ExpectedKind);
      writer.WriteNumber(PropTransmittance, value.Transmittance);
      writer.WriteNumber(PropReflectance, value.Reflectance);
      writer.WriteEndObject();
    }

    #endregion

  }
}