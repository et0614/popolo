/* NoShadingDeviceConverter.cs
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
  /// JSON converter for <see cref="NoShadingDevice"/>.
  /// </summary>
  /// <remarks>
  /// <para>
  /// Serialized JSON schema:
  /// </para>
  /// <code>
  /// { "kind": "noShadingDevice" }
  /// </code>
  /// <para>
  /// <see cref="NoShadingDevice"/> is a null-object representing the absence of a
  /// shading device. It has no state, so the JSON representation contains only
  /// the <c>kind</c> discriminator.
  /// </para>
  /// </remarks>
  public sealed class NoShadingDeviceConverter : JsonConverter<NoShadingDevice>
  {

    #region 定数

    private const string PropKind = "kind";
    private const string ExpectedKind = "noShadingDevice";

    #endregion

    #region JsonConverter 実装

    /// <summary>Reads a <see cref="NoShadingDevice"/> from JSON.</summary>
    /// <param name="reader">UTF-8 JSON reader positioned at the start of the object.</param>
    /// <param name="typeToConvert">Target type.</param>
    /// <param name="options">Serializer options (ignored).</param>
    /// <returns>A new <see cref="NoShadingDevice"/> instance.</returns>
    /// <exception cref="JsonException">Thrown when the JSON is malformed or the kind is missing/wrong.</exception>
    public override NoShadingDevice Read(
      ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
      if (reader.TokenType != JsonTokenType.StartObject)
        throw new JsonException($"Expected StartObject at the beginning of a {nameof(NoShadingDevice)}, but got {reader.TokenType}.");

      string? kind = null;

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
          default: reader.Skip(); break;
        }
      }

      if (kind != ExpectedKind)
        throw new JsonException(
          $"Expected '{PropKind}' = '{ExpectedKind}' for {nameof(NoShadingDevice)}, but got '{kind ?? "(missing)"}'.");

      return new NoShadingDevice();
    }

    /// <summary>Writes a <see cref="NoShadingDevice"/> to JSON.</summary>
    /// <param name="writer">UTF-8 JSON writer.</param>
    /// <param name="value">Device to serialize.</param>
    /// <param name="options">Serializer options (ignored).</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public override void Write(
      Utf8JsonWriter writer, NoShadingDevice value, JsonSerializerOptions options)
    {
      if (value is null)
        throw new ArgumentNullException(nameof(value));

      writer.WriteStartObject();
      writer.WriteString(PropKind, value.Kind);
      writer.WriteEndObject();
    }

    #endregion

  }
}
