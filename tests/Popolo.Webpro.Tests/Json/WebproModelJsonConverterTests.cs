/* WebproModelJsonConverterTests.cs
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
    /// <summary>Unit tests for <see cref="WebproModelJsonConverter"/>.</summary>
    public class WebproModelJsonConverterTests
    {
        private static JsonSerializerOptions CreateOptions()
            => WebproJsonReader.CreateDefaultOptions();

        // ================================================================
        #region Normal cases - minimal

        [Fact]
        public void Read_OnlyBuilding_WorksAndReturnsEmptyCollections()
        {
            const string json = """
                {
                  "Building": { "Region": "6" }
                }
                """;
            var m = JsonSerializer.Deserialize<WebproModel>(json, CreateOptions())!;

            Assert.Equal("6", m.Building.Region);
            Assert.Empty(m.Rooms);
            Assert.Empty(m.Envelopes);
            Assert.Empty(m.WallConfigurations);
            Assert.Empty(m.WindowConfigurations);
            Assert.Empty(m.AirConditionedRoomNames);
        }

        [Fact]
        public void Read_SmallComplete()
        {
            const string json = """
                {
                  "Building": { "Region": "6", "Name": "test" },
                  "Rooms": {
                    "R1": {
                      "buildingType": "事務所等",
                      "roomType": "執務室",
                      "floorHeight": 3.8,
                      "ceilingHeight": 2.7,
                      "roomArea": 100
                    }
                  },
                  "EnvelopeSet": {
                    "R1": {
                      "isAirconditioned": "有",
                      "WallList": [
                        {
                          "Direction": "南",
                          "EnvelopeArea": 30,
                          "WallSpec": "W1",
                          "WallType": "日の当たる外壁",
                          "WindowList": []
                        }
                      ]
                    }
                  },
                  "WallConfigure": {
                    "W1": {
                      "structureType": "その他",
                      "inputMethod":   "熱貫流率を入力",
                      "layers":        []
                    }
                  },
                  "WindowConfigure": {
                    "G1": {
                      "windowArea": 1,
                      "inputMethod": "ガラスの種類を入力",
                      "frameType":   "金属製",
                      "layerType":   "単層",
                      "glassID":     "T"
                    }
                  },
                  "AirConditioningZone": {
                    "R1": { "whatever": "ignored" }
                  }
                }
                """;
            var m = JsonSerializer.Deserialize<WebproModel>(json, CreateOptions())!;

            Assert.Equal("6", m.Building.Region);
            Assert.Single(m.Rooms);
            Assert.True(m.Rooms.ContainsKey("R1"));
            Assert.Single(m.Envelopes);
            Assert.Single(m.WallConfigurations);
            Assert.Single(m.WindowConfigurations);
            Assert.Single(m.AirConditionedRoomNames);
            Assert.Contains("R1", m.AirConditionedRoomNames);
        }

        #endregion

        // ================================================================
        #region AirConditioningZone ignores the value

        [Fact]
        public void Read_AirConditioningZone_ValuesDiscarded()
        {
            // AHU や load 情報(実 builelib-style)はすべて捨てて、キーだけ取る
            const string json = """
                {
                  "Building": { "Region": "6" },
                  "AirConditioningZone": {
                    "1F_ロビー": {
                      "isNatualVentilation":     "無",
                      "isSimultaneousSupply":    "無",
                      "AHU_cooling_insideLoad":  "FCU1-1",
                      "AHU_cooling_outdoorLoad": "FCU1-1",
                      "AHU_heating_insideLoad":  "FCU1-1",
                      "AHU_heating_outdoorLoad": "FCU1-1",
                      "Info": null
                    },
                    "1F_EVホール": {
                      "isNatualVentilation":     "無",
                      "AHU_cooling_insideLoad":  "FCU1-2"
                    }
                  }
                }
                """;
            var m = JsonSerializer.Deserialize<WebproModel>(json, CreateOptions())!;

            Assert.Equal(2, m.AirConditionedRoomNames.Count);
            Assert.Contains("1F_ロビー", m.AirConditionedRoomNames);
            Assert.Contains("1F_EVホール", m.AirConditionedRoomNames);
        }

        #endregion

        // ================================================================
        #region Skipping non-heat-load sections

        [Fact]
        public void Read_NonThermalSectionsIgnored()
        {
            const string json = """
                {
                  "Building": { "Region": "6" },
                  "CalculationMode":       { "x": 1 },
                  "HeatsourceSystem":      { "HS1": { "value": 1 } },
                  "LightingSystems":       { "L1": { "wattage": 10 } },
                  "HotwaterSupplySystems": { "H1": { } },
                  "Elevators":             { "E1": { } },
                  "PhotovoltaicSystems":   { "P1": { } },
                  "SpecialInputData":      { }
                }
                """;
            var m = JsonSerializer.Deserialize<WebproModel>(json, CreateOptions())!;
            Assert.Equal("6", m.Building.Region);
        }

        [Fact]
        public void Read_UnknownSectionIgnored()
        {
            const string json = """
                {
                  "Building":      { "Region": "6" },
                  "FutureSection": { "anything": "goes" },
                  "NewThing":      [1, 2, 3]
                }
                """;
            var m = JsonSerializer.Deserialize<WebproModel>(json, CreateOptions())!;
            Assert.Equal("6", m.Building.Region);
        }

        #endregion

        // ================================================================
        #region Error handling

        [Fact]
        public void Read_MissingBuilding_Throws()
        {
            const string json = """
                {
                  "Rooms": {}
                }
                """;
            Assert.Throws<JsonException>(() =>
                JsonSerializer.Deserialize<WebproModel>(json, CreateOptions()));
        }

        [Fact]
        public void Read_NonObjectRoot_Throws()
        {
            Assert.Throws<JsonException>(() =>
                JsonSerializer.Deserialize<WebproModel>("[]", CreateOptions()));
        }

        [Fact]
        public void Read_RoomsIsArray_Throws()
        {
            const string json = """
                {
                  "Building": { "Region": "6" },
                  "Rooms":    []
                }
                """;
            Assert.Throws<JsonException>(() =>
                JsonSerializer.Deserialize<WebproModel>(json, CreateOptions()));
        }

        [Fact]
        public void Write_Throws()
        {
            var m = new WebproModel();
            Assert.Throws<NotSupportedException>(() =>
                JsonSerializer.Serialize(m, CreateOptions()));
        }

        #endregion
    }
}
