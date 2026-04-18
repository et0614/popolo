/* BuildingTypeJsonConverter.cs
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
  /// JSON converter for <see cref="BuildingType"/> (WEBPRO 建物用途).
  /// </summary>
  /// <remarks>
  /// <para>
  /// Read accepts the canonical 「等」-suffixed form and also the shorter
  /// unsuffixed aliases that have appeared in historical WEBPRO inputs.
  /// Multiple aliases map to the same enum value:
  /// </para>
  /// <list type="table">
  ///   <listheader><term>BuildingType</term><description>Accepted strings</description></listheader>
  ///   <item><term><see cref="BuildingType.Office"/></term>           <description>事務所等, 事務所</description></item>
  ///   <item><term><see cref="BuildingType.Hotel"/></term>            <description>ホテル等, ホテル</description></item>
  ///   <item><term><see cref="BuildingType.Hospital"/></term>         <description>病院等, 病院</description></item>
  ///   <item><term><see cref="BuildingType.Retail"/></term>           <description>物販店舗等, 物品販売業を営む店舗等, 物品販売, 物販店舗, 百貨店等, 百貨店</description></item>
  ///   <item><term><see cref="BuildingType.School"/></term>           <description>学校等, 学校</description></item>
  ///   <item><term><see cref="BuildingType.Restaurant"/></term>       <description>飲食店等, 飲食店</description></item>
  ///   <item><term><see cref="BuildingType.Hall"/></term>             <description>集会所等, 集会所, 集会場</description></item>
  ///   <item><term><see cref="BuildingType.Plant"/></term>            <description>工場等, 工場</description></item>
  ///   <item><term><see cref="BuildingType.ApartmentHouse"/></term>   <description>共同住宅, 集合住宅</description></item>
  /// </list>
  /// <para>
  /// Write always emits the canonical 「等」-suffixed form.
  /// </para>
  /// </remarks>
  public sealed class BuildingTypeJsonConverter : JsonConverter<BuildingType>
  {

    /// <summary>Reads a <see cref="BuildingType"/> from a JSON string token.</summary>
    public override BuildingType Read(
      ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
      if (reader.TokenType != JsonTokenType.String)
        throw new JsonException(
          $"Expected String token for {nameof(BuildingType)}, but got {reader.TokenType}.");

      string? s = reader.GetString();
      return s switch
      {
        "事務所等" or "事務所" => BuildingType.Office,
        "ホテル等" or "ホテル" => BuildingType.Hotel,
        "病院等" or "病院" => BuildingType.Hospital,
        "物販店舗等"
          or "物品販売業を営む店舗等"
          or "物品販売"
          or "物販店舗"
          or "百貨店等"
          or "百貨店" => BuildingType.Retail,
        "学校等" or "学校" => BuildingType.School,
        "飲食店等" or "飲食店" => BuildingType.Restaurant,
        "集会所等" or "集会所" or "集会場" => BuildingType.Hall,
        "工場等" or "工場" => BuildingType.Plant,
        "共同住宅" or "集合住宅" => BuildingType.ApartmentHouse,
        _ => throw new JsonException(
          $"Unknown {nameof(BuildingType)} value: '{s ?? "(null)"}'."),
      };
    }

    /// <summary>Writes a <see cref="BuildingType"/> as a JSON string in canonical form.</summary>
    public override void Write(
      Utf8JsonWriter writer, BuildingType value, JsonSerializerOptions options)
    {
      string s = value switch
      {
        BuildingType.Office => "事務所等",
        BuildingType.Hotel => "ホテル等",
        BuildingType.Hospital => "病院等",
        BuildingType.Retail => "物販店舗等",
        BuildingType.School => "学校等",
        BuildingType.Restaurant => "飲食店等",
        BuildingType.Hall => "集会所等",
        BuildingType.Plant => "工場等",
        BuildingType.ApartmentHouse => "共同住宅",
        _ => throw new JsonException(
          $"{nameof(BuildingType)} value '{value}' has no canonical string form."),
      };
      writer.WriteStringValue(s);
    }
  }
}
