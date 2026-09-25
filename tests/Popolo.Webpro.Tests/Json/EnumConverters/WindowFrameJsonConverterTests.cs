/* WindowFrameJsonConverterTests.cs
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
    /// <summary>Unit tests for <see cref="WindowFrameJsonConverter"/>.</summary>
    public class WindowFrameJsonConverterTests
    {
        private static JsonSerializerOptions CreateOptions()
        {
            var opts = new JsonSerializerOptions
            {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.Create(
                    System.Text.Unicode.UnicodeRanges.All),
            };
            opts.Converters.Add(new WindowFrameJsonConverter());
            return opts;
        }

        [Theory]
        [InlineData("\"樹脂製\"", WindowFrame.Resin)]
        [InlineData("\"木製\"", WindowFrame.Wood)]
        [InlineData("\"金属製\"", WindowFrame.Metal)]
        [InlineData("\"金属樹脂複合製\"", WindowFrame.MetalAndResin)]
        [InlineData("\"金属木複合製\"", WindowFrame.MetalAndWood)]
        public void Read_NonNoneValues(string json, WindowFrame expected)
        {
            var result = JsonSerializer.Deserialize<WindowFrame>(json, CreateOptions());
            Assert.Equal(expected, result);
        }

        [Fact]
        public void Read_NullToken_MapsToNone()
        {
            var result = JsonSerializer.Deserialize<WindowFrame>("null", CreateOptions());
            Assert.Equal(WindowFrame.None, result);
        }

        [Theory]
        [InlineData(WindowFrame.Resin, "\"樹脂製\"")]
        [InlineData(WindowFrame.Wood, "\"木製\"")]
        [InlineData(WindowFrame.Metal, "\"金属製\"")]
        [InlineData(WindowFrame.MetalAndResin, "\"金属樹脂複合製\"")]
        [InlineData(WindowFrame.MetalAndWood, "\"金属木複合製\"")]
        public void Write_NonNoneValues(WindowFrame value, string expected)
        {
            var json = JsonSerializer.Serialize(value, CreateOptions());
            Assert.Equal(expected, json);
        }

        [Fact]
        public void Write_None_Throws()
        {
            Assert.Throws<JsonException>(() =>
                JsonSerializer.Serialize(WindowFrame.None, CreateOptions()));
        }

        [Theory]
        [InlineData(WindowFrame.Resin)]
        [InlineData(WindowFrame.Wood)]
        [InlineData(WindowFrame.Metal)]
        [InlineData(WindowFrame.MetalAndResin)]
        [InlineData(WindowFrame.MetalAndWood)]
        public void RoundTrip_NonNone(WindowFrame value)
        {
            var opts = CreateOptions();
            var json = JsonSerializer.Serialize(value, opts);
            var restored = JsonSerializer.Deserialize<WindowFrame>(json, opts);
            Assert.Equal(value, restored);
        }

        [Fact]
        public void Read_UnknownString_Throws()
        {
            Assert.Throws<JsonException>(() =>
                JsonSerializer.Deserialize<WindowFrame>("\"アルミ製\"", CreateOptions()));
        }

        [Fact]
        public void Read_NonStringNonNullToken_Throws()
        {
            Assert.Throws<JsonException>(() =>
                JsonSerializer.Deserialize<WindowFrame>("0", CreateOptions()));
        }
    }
}
