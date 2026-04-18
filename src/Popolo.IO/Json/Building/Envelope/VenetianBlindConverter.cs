/* VenetianBlindConverter.cs
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
  /// JSON converter for <see cref="VenetianBlind"/>.
  /// </summary>
  /// <remarks>
  /// Serialized JSON schema:
  /// <code>
  /// {
  ///   "kind":                  "venetianBlind",
  ///   "slatAngle":             0.0,
  ///   "slatWidth":             25.0,
  ///   "slatSpan":              21.0,
  ///   "upsideTransmittance":   0.0,
  ///   "downsideTransmittance": 0.0,
  ///   "upsideReflectance":     0.6,
  ///   "downsideReflectance":   0.6
  /// }
  /// </code>
  /// <para>
  /// <c>slatWidth</c> and <c>slatSpan</c> use the same length unit as the constructor
  /// (arbitrary, but must be consistent).
  /// <c>slatAngle</c> is in radians.
  /// </para>
  /// </remarks>
  public sealed class VenetianBlindConverter : JsonConverter<VenetianBlind>
  {

    #region 定数

    private const string PropKind = "kind";
    private const string PropSlatAngle = "slatAngle";
    private const string PropSlatWidth = "slatWidth";
    private const string PropSlatSpan = "slatSpan";
    private const string PropUpsideTransmittance = "upsideTransmittance";
    private const string PropDownsideTransmittance = "downsideTransmittance";
    private const string PropUpsideReflectance = "upsideReflectance";
    private const string PropDownsideReflectance = "downsideReflectance";

    private const string ExpectedKind = "venetianBlind";

    #endregion

    #region JsonConverter 実装

    /// <summary>Reads a <see cref="VenetianBlind"/> from JSON.</summary>
    /// <param name="reader">UTF-8 JSON reader positioned at the start of the object.</param>
    /// <param name="typeToConvert">Target type.</param>
    /// <param name="options">Serializer options (ignored).</param>
    /// <returns>Deserialized <see cref="VenetianBlind"/>.</returns>
    /// <exception cref="JsonException">Thrown when the JSON is malformed or a required property is missing.</exception>
    public override VenetianBlind Read(
      ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
      if (reader.TokenType != JsonTokenType.StartObject)
        throw new JsonException($"Expected StartObject at the beginning of a {nameof(VenetianBlind)}, but got {reader.TokenType}.");

      string? kind = null;
      double? slatAngle = null;
      double? slatWidth = null;
      double? slatSpan = null;
      double? upsideTransmittance = null;
      double? downsideTransmittance = null;
      double? upsideReflectance = null;
      double? downsideReflectance = null;

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
          case PropSlatAngle: slatAngle = reader.GetDouble(); break;
          case PropSlatWidth: slatWidth = reader.GetDouble(); break;
          case PropSlatSpan: slatSpan = reader.GetDouble(); break;
          case PropUpsideTransmittance: upsideTransmittance = reader.GetDouble(); break;
          case PropDownsideTransmittance: downsideTransmittance = reader.GetDouble(); break;
          case PropUpsideReflectance: upsideReflectance = reader.GetDouble(); break;
          case PropDownsideReflectance: downsideReflectance = reader.GetDouble(); break;
          default: reader.Skip(); break;
        }
      }

      if (kind != ExpectedKind)
        throw new JsonException(
          $"Expected '{PropKind}' = '{ExpectedKind}' for {nameof(VenetianBlind)}, but got '{kind ?? "(missing)"}'.");
      if (slatAngle is null) throw new JsonException($"Required property '{PropSlatAngle}' is missing from {nameof(VenetianBlind)} JSON.");
      if (slatWidth is null) throw new JsonException($"Required property '{PropSlatWidth}' is missing from {nameof(VenetianBlind)} JSON.");
      if (slatSpan is null) throw new JsonException($"Required property '{PropSlatSpan}' is missing from {nameof(VenetianBlind)} JSON.");
      if (upsideTransmittance is null) throw new JsonException($"Required property '{PropUpsideTransmittance}' is missing from {nameof(VenetianBlind)} JSON.");
      if (downsideTransmittance is null) throw new JsonException($"Required property '{PropDownsideTransmittance}' is missing from {nameof(VenetianBlind)} JSON.");
      if (upsideReflectance is null) throw new JsonException($"Required property '{PropUpsideReflectance}' is missing from {nameof(VenetianBlind)} JSON.");
      if (downsideReflectance is null) throw new JsonException($"Required property '{PropDownsideReflectance}' is missing from {nameof(VenetianBlind)} JSON.");

      var vb = new VenetianBlind(
        slatWidth.Value, slatSpan.Value,
        upsideTransmittance.Value, downsideTransmittance.Value,
        upsideReflectance.Value, downsideReflectance.Value);
      vb.SlatAngle = slatAngle.Value;
      return vb;
    }

    /// <summary>Writes a <see cref="VenetianBlind"/> to JSON.</summary>
    /// <param name="writer">UTF-8 JSON writer.</param>
    /// <param name="value">Venetian blind to serialize.</param>
    /// <param name="options">Serializer options (ignored).</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public override void Write(
      Utf8JsonWriter writer, VenetianBlind value, JsonSerializerOptions options)
    {
      if (value is null)
        throw new ArgumentNullException(nameof(value));

      writer.WriteStartObject();
      writer.WriteString(PropKind, ExpectedKind);
      writer.WriteNumber(PropSlatAngle, value.SlatAngle);
      writer.WriteNumber(PropSlatWidth, value.SlatWidth);
      writer.WriteNumber(PropSlatSpan, value.SlatSpan);
      writer.WriteNumber(PropUpsideTransmittance, value.UpsideTransmittance);
      writer.WriteNumber(PropDownsideTransmittance, value.DownsideTransmittance);
      writer.WriteNumber(PropUpsideReflectance, value.UpsideReflectance);
      writer.WriteNumber(PropDownsideReflectance, value.DownsideReflectance);
      writer.WriteEndObject();
    }

    #endregion

  }
}