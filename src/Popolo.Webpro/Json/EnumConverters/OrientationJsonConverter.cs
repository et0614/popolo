/* OrientationJsonConverter.cs
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
