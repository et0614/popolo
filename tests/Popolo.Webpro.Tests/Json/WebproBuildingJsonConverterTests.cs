/* WebproBuildingJsonConverterTests.cs
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
        #region Normal cases

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
        #region Error handling

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
