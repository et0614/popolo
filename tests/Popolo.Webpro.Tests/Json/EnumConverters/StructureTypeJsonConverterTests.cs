/* StructureTypeJsonConverterTests.cs
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
    /// <summary>Unit tests for <see cref="StructureTypeJsonConverter"/>.</summary>
    public class StructureTypeJsonConverterTests
    {
        private static JsonSerializerOptions CreateOptions()
        {
            var opts = new JsonSerializerOptions
            {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.Create(
                    System.Text.Unicode.UnicodeRanges.All),
            };
            opts.Converters.Add(new StructureTypeJsonConverter());
            return opts;
        }

        [Theory]
        [InlineData("\"木造\"", StructureType.Wood)]
        [InlineData("\"鉄筋コンクリート造等\"", StructureType.ReinforcedConcrete)]
        [InlineData("\"鉄骨造\"", StructureType.Steel)]
        [InlineData("\"その他\"", StructureType.Others)]
        public void Read_NonNoneValues(string json, StructureType expected)
        {
            var result = JsonSerializer.Deserialize<StructureType>(json, CreateOptions());
            Assert.Equal(expected, result);
        }

        [Fact]
        public void Read_NullToken_MapsToNone()
        {
            var result = JsonSerializer.Deserialize<StructureType>("null", CreateOptions());
            Assert.Equal(StructureType.None, result);
        }

        [Theory]
        [InlineData(StructureType.Wood, "\"木造\"")]
        [InlineData(StructureType.ReinforcedConcrete, "\"鉄筋コンクリート造等\"")]
        [InlineData(StructureType.Steel, "\"鉄骨造\"")]
        [InlineData(StructureType.Others, "\"その他\"")]
        public void Write_NonNoneValues(StructureType value, string expected)
        {
            var json = JsonSerializer.Serialize(value, CreateOptions());
            Assert.Equal(expected, json);
        }

        [Fact]
        public void Write_None_Throws()
        {
            Assert.Throws<JsonException>(() =>
                JsonSerializer.Serialize(StructureType.None, CreateOptions()));
        }

        [Theory]
        [InlineData(StructureType.Wood)]
        [InlineData(StructureType.ReinforcedConcrete)]
        [InlineData(StructureType.Steel)]
        [InlineData(StructureType.Others)]
        public void RoundTrip_NonNone(StructureType value)
        {
            var opts = CreateOptions();
            var json = JsonSerializer.Serialize(value, opts);
            var restored = JsonSerializer.Deserialize<StructureType>(json, opts);
            Assert.Equal(value, restored);
        }

        [Fact]
        public void Read_UnknownString_Throws()
        {
            Assert.Throws<JsonException>(() =>
                JsonSerializer.Deserialize<StructureType>("\"RC造\"", CreateOptions()));
        }

        [Fact]
        public void Read_NonStringNonNullToken_Throws()
        {
            Assert.Throws<JsonException>(() =>
                JsonSerializer.Deserialize<StructureType>("[]", CreateOptions()));
        }
    }
}
