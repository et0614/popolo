/* WallTypeJsonConverter.cs
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
  /// JSON converter for <see cref="WallType"/>.
  /// </summary>
  /// <remarks>
  /// <list type="table">
  ///   <listheader><term>WallType</term><description>JSON string</description></listheader>
  ///   <item><term><see cref="WallType.ExternalWall"/></term>         <description>日の当たる外壁</description></item>
  ///   <item><term><see cref="WallType.ShadingExternalWall"/></term>  <description>日の当たらない外壁</description></item>
  ///   <item><term><see cref="WallType.GroundWall"/></term>           <description>地盤に接する外壁</description></item>
  ///   <item><term><see cref="WallType.InnerWall"/></term>            <description>内壁</description></item>
  /// </list>
  /// </remarks>
  public sealed class WallTypeJsonConverter : JsonConverter<WallType>
  {

    /// <summary>Reads a <see cref="WallType"/> from a JSON string token.</summary>
    public override WallType Read(
      ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
      if (reader.TokenType != JsonTokenType.String)
        throw new JsonException(
          $"Expected String token for {nameof(WallType)}, but got {reader.TokenType}.");

      string? s = reader.GetString();
      return s switch
      {
        "日の当たる外壁" => WallType.ExternalWall,
        "日の当たらない外壁" => WallType.ShadingExternalWall,
        "地盤に接する外壁" => WallType.GroundWall,
        "内壁" => WallType.InnerWall,
        _ => throw new JsonException(
          $"Unknown {nameof(WallType)} value: '{s ?? "(null)"}'."),
      };
    }

    /// <summary>Writes a <see cref="WallType"/> as a JSON string.</summary>
    public override void Write(
      Utf8JsonWriter writer, WallType value, JsonSerializerOptions options)
    {
      string s = value switch
      {
        WallType.ExternalWall => "日の当たる外壁",
        WallType.ShadingExternalWall => "日の当たらない外壁",
        WallType.GroundWall => "地盤に接する外壁",
        WallType.InnerWall => "内壁",
        _ => throw new JsonException(
          $"{nameof(WallType)} value '{value}' has no canonical string form."),
      };
      writer.WriteStringValue(s);
    }
  }
}
