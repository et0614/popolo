/* SunConverter.cs
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

using Popolo.Core.Climate;

namespace Popolo.IO.Json.Climate
{
  /// <summary>
  /// JSON converter for <see cref="Sun"/>.
  /// </summary>
  /// <remarks>
  /// Serialized JSON schema:
  /// <code>
  /// {
  ///   "kind":              "sun",
  ///   "latitude":          35.6812,
  ///   "longitude":         139.7671,
  ///   "standardLongitude": 135.0
  /// }
  /// </code>
  /// <para>
  /// All angles are in degrees. <c>latitude</c> is positive northward,
  /// <c>longitude</c> is positive eastward, <c>standardLongitude</c> is the
  /// reference meridian for the local standard time (e.g. 135° for JST).
  /// </para>
  /// <para>
  /// Only the observer-location state is persisted; dynamic state
  /// (current date/time, solar position) is not serialized and must be
  /// re-established by the caller after deserialization.
  /// </para>
  /// </remarks>
  public sealed class SunConverter : JsonConverter<Sun>
  {

    #region 定数

    private const string PropKind = "kind";
    private const string PropLatitude = "latitude";
    private const string PropLongitude = "longitude";
    private const string PropStandardLongitude = "standardLongitude";

    private const string ExpectedKind = "sun";

    #endregion

    #region JsonConverter 実装

    /// <summary>Reads a <see cref="Sun"/> from JSON.</summary>
    /// <param name="reader">UTF-8 JSON reader positioned at the start of the object.</param>
    /// <param name="typeToConvert">Target type.</param>
    /// <param name="options">Serializer options (ignored).</param>
    /// <returns>Deserialized <see cref="Sun"/>.</returns>
    /// <exception cref="JsonException">Thrown when the JSON is malformed or a required property is missing.</exception>
    public override Sun Read(
      ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
      if (reader.TokenType != JsonTokenType.StartObject)
        throw new JsonException($"Expected StartObject at the beginning of a {nameof(Sun)}, but got {reader.TokenType}.");

      string? kind = null;
      double? latitude = null;
      double? longitude = null;
      double? standardLongitude = null;

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
          case PropLatitude: latitude = reader.GetDouble(); break;
          case PropLongitude: longitude = reader.GetDouble(); break;
          case PropStandardLongitude: standardLongitude = reader.GetDouble(); break;
          default: reader.Skip(); break;
        }
      }

      if (kind != ExpectedKind)
        throw new JsonException(
          $"Expected '{PropKind}' = '{ExpectedKind}' for {nameof(Sun)}, but got '{kind ?? "(missing)"}'.");
      if (latitude is null)
        throw new JsonException($"Required property '{PropLatitude}' is missing from {nameof(Sun)} JSON.");
      if (longitude is null)
        throw new JsonException($"Required property '{PropLongitude}' is missing from {nameof(Sun)} JSON.");
      if (standardLongitude is null)
        throw new JsonException($"Required property '{PropStandardLongitude}' is missing from {nameof(Sun)} JSON.");

      return new Sun(latitude.Value, longitude.Value, standardLongitude.Value);
    }

    /// <summary>Writes a <see cref="Sun"/> to JSON.</summary>
    /// <param name="writer">UTF-8 JSON writer.</param>
    /// <param name="value">Sun to serialize.</param>
    /// <param name="options">Serializer options (ignored).</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public override void Write(
      Utf8JsonWriter writer, Sun value, JsonSerializerOptions options)
    {
      if (value is null)
        throw new ArgumentNullException(nameof(value));

      writer.WriteStartObject();
      writer.WriteString(PropKind, ExpectedKind);
      writer.WriteNumber(PropLatitude, value.Latitude);
      writer.WriteNumber(PropLongitude, value.Longitude);
      writer.WriteNumber(PropStandardLongitude, value.StandardLongitude);
      writer.WriteEndObject();
    }

    #endregion

  }
}
