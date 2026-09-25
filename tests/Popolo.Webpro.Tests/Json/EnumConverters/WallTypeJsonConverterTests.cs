/* WallTypeJsonConverterTests.cs
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
