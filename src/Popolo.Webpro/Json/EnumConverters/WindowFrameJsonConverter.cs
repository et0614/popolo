/* WindowFrameJsonConverter.cs
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
  /// JSON converter for <see cref="WindowFrame"/>.
  /// </summary>
  /// <remarks>
  /// <list type="table">
  ///   <listheader><term>WindowFrame</term><description>JSON string</description></listheader>
  ///   <item><term><see cref="WindowFrame.Resin"/></term>         <description>樹脂製</description></item>
  ///   <item><term><see cref="WindowFrame.Wood"/></term>          <description>木製</description></item>
  ///   <item><term><see cref="WindowFrame.Metal"/></term>         <description>金属製</description></item>
  ///   <item><term><see cref="WindowFrame.MetalAndResin"/></term> <description>金属樹脂複合製</description></item>
  ///   <item><term><see cref="WindowFrame.MetalAndWood"/></term>  <description>金属木複合製</description></item>
  /// </list>
  /// <para>
  /// <see cref="WindowFrame.None"/> has no canonical string form. A null JSON
  /// token reads as <see cref="WindowFrame.None"/>. Writing
  /// <see cref="WindowFrame.None"/> throws <see cref="JsonException"/>.
  /// </para>
  /// </remarks>
  public sealed class WindowFrameJsonConverter : JsonConverter<WindowFrame>
  {

    /// <inheritdoc/>
    public override bool HandleNull => true;

    /// <summary>Reads a <see cref="WindowFrame"/> from a JSON string or null token.</summary>
    public override WindowFrame Read(
      ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
      if (reader.TokenType == JsonTokenType.Null)
        return WindowFrame.None;

      if (reader.TokenType != JsonTokenType.String)
        throw new JsonException(
          $"Expected String token for {nameof(WindowFrame)}, but got {reader.TokenType}.");

      string? s = reader.GetString();
      return s switch
      {
        "樹脂製" => WindowFrame.Resin,
        "木製" => WindowFrame.Wood,
        "金属製" => WindowFrame.Metal,
        "金属樹脂複合製" => WindowFrame.MetalAndResin,
        "金属木複合製" => WindowFrame.MetalAndWood,
        _ => throw new JsonException(
          $"Unknown {nameof(WindowFrame)} value: '{s ?? "(null)"}'."),
      };
    }

    /// <summary>Writes a <see cref="WindowFrame"/> as a JSON string.</summary>
    public override void Write(
      Utf8JsonWriter writer, WindowFrame value, JsonSerializerOptions options)
    {
      string s = value switch
      {
        WindowFrame.Resin => "樹脂製",
        WindowFrame.Wood => "木製",
        WindowFrame.Metal => "金属製",
        WindowFrame.MetalAndResin => "金属樹脂複合製",
        WindowFrame.MetalAndWood => "金属木複合製",
        WindowFrame.None => throw new JsonException(
          $"{nameof(WindowFrame)}.{nameof(WindowFrame.None)} has no canonical string form; the enclosing converter should suppress the property."),
        _ => throw new JsonException(
          $"{nameof(WindowFrame)} value '{value}' has no canonical string form."),
      };
      writer.WriteStringValue(s);
    }
  }
}
