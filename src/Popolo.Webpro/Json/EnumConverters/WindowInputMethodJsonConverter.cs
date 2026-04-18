/* WindowInputMethodJsonConverter.cs
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
  /// JSON converter for <see cref="WindowInputMethod"/>.
  /// </summary>
  /// <remarks>
  /// <list type="table">
  ///   <listheader><term>WindowInputMethod</term><description>JSON string</description></listheader>
  ///   <item><term><see cref="WindowInputMethod.WindowSpec"/></term>              <description>性能値を入力</description></item>
  ///   <item><term><see cref="WindowInputMethod.FrameTypeAndGlazingSpec"/></term> <description>ガラスの性能を入力</description></item>
  ///   <item><term><see cref="WindowInputMethod.FrameAndGlazingType"/></term>     <description>ガラスの種類を入力</description></item>
  /// </list>
  /// <para>
  /// <see cref="WindowInputMethod.None"/> has no canonical string form. A null
  /// JSON token reads as <see cref="WindowInputMethod.None"/>. Writing
  /// <see cref="WindowInputMethod.None"/> throws <see cref="JsonException"/>;
  /// enclosing converters should suppress the property instead.
  /// </para>
  /// </remarks>
  public sealed class WindowInputMethodJsonConverter : JsonConverter<WindowInputMethod>
  {

    /// <inheritdoc/>
    public override bool HandleNull => true;

    /// <summary>Reads a <see cref="WindowInputMethod"/> from a JSON string or null token.</summary>
    public override WindowInputMethod Read(
      ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
      if (reader.TokenType == JsonTokenType.Null)
        return WindowInputMethod.None;

      if (reader.TokenType != JsonTokenType.String)
        throw new JsonException(
          $"Expected String token for {nameof(WindowInputMethod)}, but got {reader.TokenType}.");

      string? s = reader.GetString();
      return s switch
      {
        "性能値を入力" => WindowInputMethod.WindowSpec,
        "ガラスの性能を入力" => WindowInputMethod.FrameTypeAndGlazingSpec,
        "ガラスの種類を入力" => WindowInputMethod.FrameAndGlazingType,
        _ => throw new JsonException(
          $"Unknown {nameof(WindowInputMethod)} value: '{s ?? "(null)"}'."),
      };
    }

    /// <summary>Writes a <see cref="WindowInputMethod"/> as a JSON string.</summary>
    public override void Write(
      Utf8JsonWriter writer, WindowInputMethod value, JsonSerializerOptions options)
    {
      string s = value switch
      {
        WindowInputMethod.WindowSpec => "性能値を入力",
        WindowInputMethod.FrameTypeAndGlazingSpec => "ガラスの性能を入力",
        WindowInputMethod.FrameAndGlazingType => "ガラスの種類を入力",
        WindowInputMethod.None => throw new JsonException(
          $"{nameof(WindowInputMethod)}.{nameof(WindowInputMethod.None)} has no canonical string form; the enclosing converter should suppress the property."),
        _ => throw new JsonException(
          $"{nameof(WindowInputMethod)} value '{value}' has no canonical string form."),
      };
      writer.WriteStringValue(s);
    }
  }
}
