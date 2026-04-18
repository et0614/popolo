/* WebproBuildingJsonConverterTests.cs
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
using Xunit;

using Popolo.Webpro.Domain;
using Popolo.Webpro.Json;

namespace Popolo.Webpro.Tests.Json
{
    /// <summary>Unit tests for <see cref="WebproBuildingJsonConverter"/>.</summary>
    public class WebproBuildingJsonConverterTests
    {
        private static JsonSerializerOptions CreateOptions()
        {
            var opts = new JsonSerializerOptions();
            opts.Converters.Add(new WebproBuildingJsonConverter());
            return opts;
        }

        // ================================================================
        #region 正常ケース

        [Fact]
        public void Read_RealSample()
        {
            // builelib_input.json の Building ブロックそのもの
            const string json = """
                {
                  "BuildingAddress": {
                    "Prefecture": "東京都",
                    "City":       "千代田区",
                    "Address":    null
                  },
                  "Coefficient_DHC": {
                    "Cooling": 1.36,
                    "Heating": 1.36
                  },
                  "Name":              "サンプル事務所ビル",
                  "Region":            "6",
                  "AnnualSolarRegion": "A3",
                  "BuildingFloorArea": 10352.79
                }
                """;
            var b = JsonSerializer.Deserialize<WebproBuilding>(json, CreateOptions())!;

            Assert.Equal("サンプル事務所ビル", b.Name);
            Assert.Equal("6", b.Region);
            Assert.Equal("A3", b.AnnualSolarRegion);
            Assert.Equal(10352.79, b.FloorArea);
        }

        [Fact]
        public void Read_MinimalJson_OnlyRegion()
        {
            const string json = """{ "Region": "3" }""";
            var b = JsonSerializer.Deserialize<WebproBuilding>(json, CreateOptions())!;

            Assert.Equal("3", b.Region);
            Assert.Null(b.Name);
            Assert.Null(b.AnnualSolarRegion);
            Assert.Null(b.FloorArea);
        }

        [Fact]
        public void Read_NullOptionals()
        {
            const string json = """
                {
                  "Name":              null,
                  "Region":            "1",
                  "AnnualSolarRegion": null,
                  "BuildingFloorArea": null
                }
                """;
            var b = JsonSerializer.Deserialize<WebproBuilding>(json, CreateOptions())!;

            Assert.Null(b.Name);
            Assert.Equal("1", b.Region);
            Assert.Null(b.AnnualSolarRegion);
            Assert.Null(b.FloorArea);
        }

        [Fact]
        public void Read_BuildingAddressSkipped()
        {
            // BuildingAddress は明示的にスキップされる(熱負荷計算に不要)
            const string json = """
                {
                  "Region":          "6",
                  "BuildingAddress": { "Prefecture": "東京都", "City": "千代田区" }
                }
                """;
            var b = JsonSerializer.Deserialize<WebproBuilding>(json, CreateOptions())!;
            Assert.Equal("6", b.Region);
        }

        [Fact]
        public void Read_CoefficientDhcSkipped()
        {
            const string json = """
                {
                  "Region":          "6",
                  "Coefficient_DHC": { "Cooling": 1.36, "Heating": 1.36 }
                }
                """;
            var b = JsonSerializer.Deserialize<WebproBuilding>(json, CreateOptions())!;
            Assert.Equal("6", b.Region);
        }

        [Fact]
        public void Read_UnknownPropertiesIgnored()
        {
            const string json = """
                {
                  "Region":           "6",
                  "FutureField":      "x",
                  "NestedThing":      { "a": 1, "b": [1, 2, 3] }
                }
                """;
            var b = JsonSerializer.Deserialize<WebproBuilding>(json, CreateOptions())!;
            Assert.Equal("6", b.Region);
        }

        #endregion

        // ================================================================
        #region エラー処理

        [Fact]
        public void Read_MissingRegion_Throws()
        {
            const string json = """
                {
                  "Name":              "some name",
                  "AnnualSolarRegion": "A3",
                  "BuildingFloorArea": 100
                }
                """;
            Assert.Throws<JsonException>(() =>
                JsonSerializer.Deserialize<WebproBuilding>(json, CreateOptions()));
        }

        [Fact]
        public void Read_NullRegion_Throws()
        {
            const string json = """
                {
                  "Region": null
                }
                """;
            Assert.Throws<JsonException>(() =>
                JsonSerializer.Deserialize<WebproBuilding>(json, CreateOptions()));
        }

        [Fact]
        public void Read_NonStringRegion_Throws()
        {
            const string json = """{ "Region": 6 }""";
            Assert.Throws<JsonException>(() =>
                JsonSerializer.Deserialize<WebproBuilding>(json, CreateOptions()));
        }

        [Fact]
        public void Read_NonObjectRoot_Throws()
        {
            Assert.Throws<JsonException>(() =>
                JsonSerializer.Deserialize<WebproBuilding>("[]", CreateOptions()));
        }

        [Fact]
        public void Write_Throws()
        {
            var b = new WebproBuilding { Region = "6" };
            Assert.Throws<NotSupportedException>(() =>
                JsonSerializer.Serialize(b, CreateOptions()));
        }

        #endregion
    }
}
