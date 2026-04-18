/* OrientationJsonConverter.cs
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

using Popolo.Webpro.Domain.Enums;

namespace Popolo.Webpro.Json.EnumConverters
{
  /// <summary>
  /// JSON converter for <see cref="Orientation"/> (WEBPRO 方位).
  /// </summary>
  /// <remarks>
  /// <para>The eight compass directions use single-character Japanese strings
  /// (北, 北西, 西, 南西, 南, 南東, 東, 北東). Horizontal surfaces use
  /// 水平（上） for roofs and 水平（下） for floors. Legacy WEBPRO versions may also
  /// emit 日陰 (shade) and 水平 (horizontal).</para>
  /// <para>Note: The horizontal strings use the Japanese full-width parentheses
  /// 「（」「）」 (U+FF08, U+FF09), matching the actual WEBPRO output; ASCII
  /// parentheses are not accepted.</para>
  /// </remarks>
  public sealed class OrientationJsonConverter : JsonConverter<Orientation>
  {

    /// <summary>Reads an <see cref="Orientation"/> from a JSON string token.</summary>
    public override Orientation Read(
      ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
      if (reader.TokenType != JsonTokenType.String)
        throw new JsonException(
          $"Expected String token for {nameof(Orientation)}, but got {reader.TokenType}.");

      string? s = reader.GetString();
      return s switch
      {
        "北" => Orientation.N,
        "北西" => Orientation.NW,
        "西" => Orientation.W,
        "南西" => Orientation.SW,
        "南" => Orientation.S,
        "南東" => Orientation.SE,
        "東" => Orientation.E,
        "北東" => Orientation.NE,
        "水平（上）" => Orientation.UpperHorizontal,
        "水平（下）" => Orientation.LowerHorizontal,
        "日陰" => Orientation.Shade,
        "水平" => Orientation.Horizontal,
        _ => throw new JsonException(
          $"Unknown {nameof(Orientation)} value: '{s ?? "(null)"}'."),
      };
    }

    /// <summary>Writes an <see cref="Orientation"/> as a JSON string.</summary>
    public override void Write(
      Utf8JsonWriter writer, Orientation value, JsonSerializerOptions options)
    {
      string s = value switch
      {
        Orientation.N => "北",
        Orientation.NW => "北西",
        Orientation.W => "西",
        Orientation.SW => "南西",
        Orientation.S => "南",
        Orientation.SE => "南東",
        Orientation.E => "東",
        Orientation.NE => "北東",
        Orientation.UpperHorizontal => "水平（上）",
        Orientation.LowerHorizontal => "水平（下）",
        Orientation.Shade => "日陰",
        Orientation.Horizontal => "水平",
        _ => throw new JsonException(
          $"{nameof(Orientation)} value '{value}' has no canonical string form."),
      };
      writer.WriteStringValue(s);
    }
  }
}
