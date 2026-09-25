/* WindowInputMethodJsonConverterTests.cs
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
