/* WallInputMethodJsonConverter.cs
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
