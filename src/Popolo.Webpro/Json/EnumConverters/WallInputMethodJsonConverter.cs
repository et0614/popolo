/* WallInputMethodJsonConverter.cs
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
  /// JSON converter for <see cref="WallInputMethod"/>.
  /// </summary>
  /// <remarks>
  /// <list type="table">
  ///   <listheader><term>WallInputMethod</term><description>JSON string</description></listheader>
  ///   <item><term><see cref="WallInputMethod.HeatTransferCoefficient"/></term>    <description>熱貫流率を入力</description></item>
  ///   <item><term><see cref="WallInputMethod.MaterialNumberAndThickness"/></term> <description>建材構成を入力</description></item>
  ///   <item><term><see cref="WallInputMethod.InsulationType"/></term>             <description>断熱材種類を入力</description></item>
  /// </list>
  /// <para>
  /// <see cref="WallInputMethod.None"/> has no canonical string form. A null JSON
  /// token is read as <see cref="WallInputMethod.None"/>. Attempting to write
  /// <see cref="WallInputMethod.None"/> throws <see cref="JsonException"/>;
  /// callers should suppress the property entirely for <see cref="WallInputMethod.None"/>
  /// at the enclosing converter level.
  /// </para>
  /// </remarks>
  public sealed class WallInputMethodJsonConverter : JsonConverter<WallInputMethod>
  {

    /// <inheritdoc/>
    public override bool HandleNull => true;

    /// <summary>Reads a <see cref="WallInputMethod"/> from a JSON string or null token.</summary>
    public override WallInputMethod Read(
      ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
      if (reader.TokenType == JsonTokenType.Null)
        return WallInputMethod.None;

      if (reader.TokenType != JsonTokenType.String)
        throw new JsonException(
          $"Expected String token for {nameof(WallInputMethod)}, but got {reader.TokenType}.");

      string? s = reader.GetString();
      return s switch
      {
        "熱貫流率を入力" => WallInputMethod.HeatTransferCoefficient,
        "建材構成を入力" => WallInputMethod.MaterialNumberAndThickness,
        "断熱材種類を入力" => WallInputMethod.InsulationType,
        _ => throw new JsonException(
          $"Unknown {nameof(WallInputMethod)} value: '{s ?? "(null)"}'."),
      };
    }

    /// <summary>Writes a <see cref="WallInputMethod"/> as a JSON string.</summary>
    public override void Write(
      Utf8JsonWriter writer, WallInputMethod value, JsonSerializerOptions options)
    {
      string s = value switch
      {
        WallInputMethod.HeatTransferCoefficient => "熱貫流率を入力",
        WallInputMethod.MaterialNumberAndThickness => "建材構成を入力",
        WallInputMethod.InsulationType => "断熱材種類を入力",
        WallInputMethod.None => throw new JsonException(
          $"{nameof(WallInputMethod)}.{nameof(WallInputMethod.None)} has no canonical string form; the enclosing converter should suppress the property."),
        _ => throw new JsonException(
          $"{nameof(WallInputMethod)} value '{value}' has no canonical string form."),
      };
      writer.WriteStringValue(s);
    }
  }
}
