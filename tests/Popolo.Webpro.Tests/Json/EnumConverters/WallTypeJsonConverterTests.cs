/* WallTypeJsonConverterTests.cs
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
    /// <summary>Unit tests for <see cref="WallTypeJsonConverter"/>.</summary>
    public class WallTypeJsonConverterTests
    {
        private static JsonSerializerOptions CreateOptions()
        {
            var opts = new JsonSerializerOptions
            {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.Create(
                    System.Text.Unicode.UnicodeRanges.All),
            };
            opts.Converters.Add(new WallTypeJsonConverter());
            return opts;
        }

        [Theory]
        [InlineData("\"日の当たる外壁\"", WallType.ExternalWall)]
        [InlineData("\"日の当たらない外壁\"", WallType.ShadingExternalWall)]
        [InlineData("\"地盤に接する外壁\"", WallType.GroundWall)]
        [InlineData("\"内壁\"", WallType.InnerWall)]
        public void Read_AllValues(string json, WallType expected)
        {
            var result = JsonSerializer.Deserialize<WallType>(json, CreateOptions());
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData(WallType.ExternalWall, "\"日の当たる外壁\"")]
        [InlineData(WallType.ShadingExternalWall, "\"日の当たらない外壁\"")]
        [InlineData(WallType.GroundWall, "\"地盤に接する外壁\"")]
        [InlineData(WallType.InnerWall, "\"内壁\"")]
        public void Write_AllValues(WallType value, string expected)
        {
            var json = JsonSerializer.Serialize(value, CreateOptions());
            Assert.Equal(expected, json);
        }

        [Theory]
        [InlineData(WallType.ExternalWall)]
        [InlineData(WallType.ShadingExternalWall)]
        [InlineData(WallType.GroundWall)]
        [InlineData(WallType.InnerWall)]
        public void RoundTrip(WallType value)
        {
            var opts = CreateOptions();
            var json = JsonSerializer.Serialize(value, opts);
            var restored = JsonSerializer.Deserialize<WallType>(json, opts);
            Assert.Equal(value, restored);
        }

        [Fact]
        public void Read_UnknownString_Throws()
        {
            Assert.Throws<JsonException>(() =>
                JsonSerializer.Deserialize<WallType>("\"外壁\"", CreateOptions()));
        }

        [Fact]
        public void Read_NonStringToken_Throws()
        {
            Assert.Throws<JsonException>(() =>
                JsonSerializer.Deserialize<WallType>("42", CreateOptions()));
        }
    }
}
