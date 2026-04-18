/* WindowInputMethodJsonConverterTests.cs
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

using System.Text.Json;
using Xunit;

using Popolo.Webpro.Domain.Enums;
using Popolo.Webpro.Json.EnumConverters;

namespace Popolo.Webpro.Tests.Json.EnumConverters
{
    /// <summary>Unit tests for <see cref="WindowInputMethodJsonConverter"/>.</summary>
    public class WindowInputMethodJsonConverterTests
    {
        private static JsonSerializerOptions CreateOptions()
        {
            var opts = new JsonSerializerOptions
            {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.Create(
                    System.Text.Unicode.UnicodeRanges.All),
            };
            opts.Converters.Add(new WindowInputMethodJsonConverter());
            return opts;
        }

        [Theory]
        [InlineData("\"性能値を入力\"", WindowInputMethod.WindowSpec)]
        [InlineData("\"ガラスの性能を入力\"", WindowInputMethod.FrameTypeAndGlazingSpec)]
        [InlineData("\"ガラスの種類を入力\"", WindowInputMethod.FrameAndGlazingType)]
        public void Read_NonNoneValues(string json, WindowInputMethod expected)
        {
            var result = JsonSerializer.Deserialize<WindowInputMethod>(json, CreateOptions());
            Assert.Equal(expected, result);
        }

        [Fact]
        public void Read_NullToken_MapsToNone()
        {
            var result = JsonSerializer.Deserialize<WindowInputMethod>("null", CreateOptions());
            Assert.Equal(WindowInputMethod.None, result);
        }

        [Theory]
        [InlineData(WindowInputMethod.WindowSpec, "\"性能値を入力\"")]
        [InlineData(WindowInputMethod.FrameTypeAndGlazingSpec, "\"ガラスの性能を入力\"")]
        [InlineData(WindowInputMethod.FrameAndGlazingType, "\"ガラスの種類を入力\"")]
        public void Write_NonNoneValues(WindowInputMethod value, string expected)
        {
            var json = JsonSerializer.Serialize(value, CreateOptions());
            Assert.Equal(expected, json);
        }

        [Fact]
        public void Write_None_Throws()
        {
            Assert.Throws<JsonException>(() =>
                JsonSerializer.Serialize(WindowInputMethod.None, CreateOptions()));
        }

        [Theory]
        [InlineData(WindowInputMethod.WindowSpec)]
        [InlineData(WindowInputMethod.FrameTypeAndGlazingSpec)]
        [InlineData(WindowInputMethod.FrameAndGlazingType)]
        public void RoundTrip_NonNone(WindowInputMethod value)
        {
            var opts = CreateOptions();
            var json = JsonSerializer.Serialize(value, opts);
            var restored = JsonSerializer.Deserialize<WindowInputMethod>(json, opts);
            Assert.Equal(value, restored);
        }

        [Fact]
        public void Read_UnknownString_Throws()
        {
            Assert.Throws<JsonException>(() =>
                JsonSerializer.Deserialize<WindowInputMethod>("\"不明\"", CreateOptions()));
        }

        [Fact]
        public void Read_NonStringNonNullToken_Throws()
        {
            Assert.Throws<JsonException>(() =>
                JsonSerializer.Deserialize<WindowInputMethod>("true", CreateOptions()));
        }
    }
}
