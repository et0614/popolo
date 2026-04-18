/* StructureTypeJsonConverter.cs
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
  /// JSON converter for <see cref="StructureType"/>.
  /// </summary>
  /// <remarks>
  /// <list type="table">
  ///   <listheader><term>StructureType</term><description>JSON string</description></listheader>
  ///   <item><term><see cref="StructureType.Wood"/></term>               <description>木造</description></item>
  ///   <item><term><see cref="StructureType.ReinforcedConcrete"/></term> <description>鉄筋コンクリート造等</description></item>
  ///   <item><term><see cref="StructureType.Steel"/></term>              <description>鉄骨造</description></item>
  ///   <item><term><see cref="StructureType.Others"/></term>             <description>その他</description></item>
  /// </list>
  /// <para>
  /// <see cref="StructureType.None"/> has no canonical string form. A null JSON
  /// token reads as <see cref="StructureType.None"/>. Writing
  /// <see cref="StructureType.None"/> throws <see cref="JsonException"/>.
  /// </para>
  /// </remarks>
  public sealed class StructureTypeJsonConverter : JsonConverter<StructureType>
  {

    /// <inheritdoc/>
    public override bool HandleNull => true;

    /// <summary>Reads a <see cref="StructureType"/> from a JSON string or null token.</summary>
    public override StructureType Read(
      ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
      if (reader.TokenType == JsonTokenType.Null)
        return StructureType.None;

      if (reader.TokenType != JsonTokenType.String)
        throw new JsonException(
          $"Expected String token for {nameof(StructureType)}, but got {reader.TokenType}.");

      string? s = reader.GetString();
      return s switch
      {
        "木造" => StructureType.Wood,
        "鉄筋コンクリート造等" => StructureType.ReinforcedConcrete,
        "鉄骨造" => StructureType.Steel,
        "その他" => StructureType.Others,
        _ => throw new JsonException(
          $"Unknown {nameof(StructureType)} value: '{s ?? "(null)"}'."),
      };
    }

    /// <summary>Writes a <see cref="StructureType"/> as a JSON string.</summary>
    public override void Write(
      Utf8JsonWriter writer, StructureType value, JsonSerializerOptions options)
    {
      string s = value switch
      {
        StructureType.Wood => "木造",
        StructureType.ReinforcedConcrete => "鉄筋コンクリート造等",
        StructureType.Steel => "鉄骨造",
        StructureType.Others => "その他",
        StructureType.None => throw new JsonException(
          $"{nameof(StructureType)}.{nameof(StructureType.None)} has no canonical string form; the enclosing converter should suppress the property."),
        _ => throw new JsonException(
          $"{nameof(StructureType)} value '{value}' has no canonical string form."),
      };
      writer.WriteStringValue(s);
    }
  }
}
