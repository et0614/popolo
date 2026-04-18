/* StructureTypeJsonConverterTests.cs
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
