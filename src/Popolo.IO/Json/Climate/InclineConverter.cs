/* InclineConverter.cs
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

using Popolo.Core.Climate;

namespace Popolo.IO.Json.Climate
{
  /// <summary>
  /// JSON converter for <see cref="Incline"/>.
  /// </summary>
  /// <remarks>
  /// Serialized JSON schema:
  /// <code>
  /// {
  ///   "kind":            "incline",
  ///   "horizontalAngle": 0.0,
  ///   "verticalAngle":   1.5707963267948966
  /// }
  /// </code>
  /// <para>
  /// Both angles are in radians:
  /// <c>horizontalAngle</c> — azimuth (south = 0, east &lt; 0, west &gt; 0);
  /// <c>verticalAngle</c> — tilt (horizontal = 0, vertical = π/2).
  /// </para>
  /// </remarks>
  public sealed class InclineConverter : JsonConverter<Incline>
  {

    #region Constants

    private const string PropKind = "kind";
    private const string PropHorizontalAngle = "horizontalAngle";
    private const string PropVerticalAngle = "verticalAngle";

    private const string ExpectedKind = "incline";

    #endregion

    #region JsonConverter implementation

    /// <summary>Reads an <see cref="Incline"/> from JSON.</summary>
    /// <param name="reader">UTF-8 JSON reader positioned at the start of the object.</param>
    /// <param name="typeToConvert">Target type.</param>
    /// <param name="options">Serializer options (ignored).</param>
    /// <returns>Deserialized <see cref="Incline"/>.</returns>
    /// <exception cref="JsonException">Thrown when the JSON is malformed or a required property is missing.</exception>
    public override Incline Read(
      ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
      if (reader.TokenType != JsonTokenType.StartObject)
        throw new JsonException($"Expected StartObject at the beginning of an {nameof(Incline)}, but got {reader.TokenType}.");

      string? kind = null;
      double? horizontalAngle = null;
      double? verticalAngle = null;

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
          case PropHorizontalAngle: horizontalAngle = reader.GetDouble(); break;
          case PropVerticalAngle: verticalAngle = reader.GetDouble(); break;
          default: reader.Skip(); break;
        }
      }

      if (kind != ExpectedKind)
        throw new JsonException(
          $"Expected '{PropKind}' = '{ExpectedKind}' for {nameof(Incline)}, but got '{kind ?? "(missing)"}'.");
      if (horizontalAngle is null)
        throw new JsonException($"Required property '{PropHorizontalAngle}' is missing from {nameof(Incline)} JSON.");
      if (verticalAngle is null)
        throw new JsonException($"Required property '{PropVerticalAngle}' is missing from {nameof(Incline)} JSON.");

      return new Incline(horizontalAngle.Value, verticalAngle.Value);
    }

    /// <summary>Writes an <see cref="Incline"/> to JSON.</summary>
    /// <param name="writer">UTF-8 JSON writer.</param>
    /// <param name="value">Incline to serialize.</param>
    /// <param name="options">Serializer options (ignored).</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public override void Write(
      Utf8JsonWriter writer, Incline value, JsonSerializerOptions options)
    {
      if (value is null)
        throw new ArgumentNullException(nameof(value));

      writer.WriteStartObject();
      writer.WriteString(PropKind, ExpectedKind);
      writer.WriteNumber(PropHorizontalAngle, value.HorizontalAngle);
      writer.WriteNumber(PropVerticalAngle, value.VerticalAngle);
      writer.WriteEndObject();
    }

    #endregion

  }
}