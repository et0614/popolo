/* StructureTypeJsonConverter.cs
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
