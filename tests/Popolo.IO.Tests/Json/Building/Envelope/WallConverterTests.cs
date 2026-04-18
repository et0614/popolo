/* WallConverterTests.cs
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

using Popolo.Core.Building.Envelope;
using Popolo.IO.Json.Building.Envelope;

namespace Popolo.IO.Tests.Json.Building.Envelope
{
    /// <summary>Unit tests for <see cref="WallConverter"/>.</summary>
    public class WallConverterTests
    {
        #region ヘルパー

        /// <summary>
        /// Options with WallConverter + WallLayerConverter + AirGapLayerConverter.
        /// </summary>
        private static JsonSerializerOptions CreateOptions()
        {
            var opts = new JsonSerializerOptions();
            opts.Converters.Add(new WallConverter());
            opts.Converters.Add(new WallLayerConverter());
            opts.Converters.Add(new AirGapLayerConverter());
            return opts;
        }

        /// <summary>Three-layer external wall: [Concrete 150 mm][Air Gap 20 mm][Plaster 12 mm].</summary>
        private static Wall MakeThreeLayerWall()
        {
            var layers = new WallLayer[]
            {
                new WallLayer("Concrete", 1.4, 1934.0, 0.15),
                new AirGapLayer("Sealed Air Gap", isSealed: true, thickness: 0.02),
                new WallLayer("Plaster", 0.7, 1300.0, 0.012),
            };
            var wall = new Wall(area: 12.0, layers: layers);
            wall.ID = 42;
            wall.ConvectiveCoefficientF = 9.3;
            wall.ShortWaveAbsorptanceF = 0.7;
            wall.LongWaveEmissivityF = 0.9;
            wall.ConvectiveCoefficientB = 9.3;
            wall.ShortWaveAbsorptanceB = 0.6;
            wall.LongWaveEmissivityB = 0.85;
            return wall;
        }

        private static Wall MakeSingleLayerWall()
        {
            var layers = new WallLayer[]
            {
                new WallLayer("Steel", 45.0, 3770.0, 0.005),
            };
            return new Wall(1.0, layers);
        }

        private static int CountProperties(JsonElement obj)
        {
            int count = 0;
            foreach (var _ in obj.EnumerateObject()) count++;
            return count;
        }

        #endregion

        // ================================================================
        #region シリアライズ

        [Fact]
        public void Write_ProducesExpectedTopLevelFields()
        {
            var json = JsonSerializer.Serialize(MakeThreeLayerWall(), CreateOptions());
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.Equal("wall", root.GetProperty("kind").GetString());
            Assert.Equal(42, root.GetProperty("id").GetInt32());
            Assert.Equal(12.0, root.GetProperty("area").GetDouble());
            Assert.False(root.GetProperty("computeMoistureTransfer").GetBoolean());
            Assert.Equal(JsonValueKind.Array, root.GetProperty("layers").ValueKind);
            Assert.Equal(JsonValueKind.Object, root.GetProperty("surfaceF").ValueKind);
            Assert.Equal(JsonValueKind.Object, root.GetProperty("surfaceB").ValueKind);
        }

        [Fact]
        public void Write_SurfaceFGroupsTheThreeCoefficients()
        {
            var json = JsonSerializer.Serialize(MakeThreeLayerWall(), CreateOptions());
            using var doc = JsonDocument.Parse(json);
            var sf = doc.RootElement.GetProperty("surfaceF");

            Assert.Equal(3, CountProperties(sf));
            Assert.Equal(9.3, sf.GetProperty("convectiveCoefficient").GetDouble());
            Assert.Equal(0.7, sf.GetProperty("shortWaveAbsorptance").GetDouble());
            Assert.Equal(0.9, sf.GetProperty("longWaveEmissivity").GetDouble());
        }

        [Fact]
        public void Write_SurfaceBGroupsTheThreeCoefficients()
        {
            var json = JsonSerializer.Serialize(MakeThreeLayerWall(), CreateOptions());
            using var doc = JsonDocument.Parse(json);
            var sb = doc.RootElement.GetProperty("surfaceB");

            Assert.Equal(0.6, sb.GetProperty("shortWaveAbsorptance").GetDouble());
            Assert.Equal(0.85, sb.GetProperty("longWaveEmissivity").GetDouble());
        }

        [Fact]
        public void Write_LayersAreFlatObjectsWithKind()
        {
            var json = JsonSerializer.Serialize(MakeThreeLayerWall(), CreateOptions());
            using var doc = JsonDocument.Parse(json);
            var arr = doc.RootElement.GetProperty("layers");

            Assert.Equal(3, arr.GetArrayLength());

            string[] expectedKinds = { "wallLayer", "airGapLayer", "wallLayer" };
            int i = 0;
            foreach (var entry in arr.EnumerateArray())
            {
                Assert.Equal(JsonValueKind.Object, entry.ValueKind);
                Assert.Equal(expectedKinds[i], entry.GetProperty("kind").GetString());
                i++;
            }
        }

        [Fact]
        public void Write_SingleLayerWall_WritesOneEntry()
        {
            var json = JsonSerializer.Serialize(MakeSingleLayerWall(), CreateOptions());
            using var doc = JsonDocument.Parse(json);
            Assert.Equal(1, doc.RootElement.GetProperty("layers").GetArrayLength());
        }

        #endregion

        // ================================================================
        #region デシリアライズ

        [Fact]
        public void Read_WellFormedJson_ProducesExpectedWall()
        {
            const string json = """
                {
                  "kind": "wall",
                  "id": 7,
                  "area": 10.0,
                  "computeMoistureTransfer": false,
                  "layers": [
                    { "kind": "wallLayer",   "name": "C", "thermalConductivity": 1.4, "volSpecificHeat": 1934.0, "thickness": 0.15 },
                    { "kind": "airGapLayer", "name": "A", "isSealed": true, "thickness": 0.02 }
                  ],
                  "surfaceF": { "convectiveCoefficient": 9.3, "shortWaveAbsorptance": 0.7, "longWaveEmissivity": 0.9 },
                  "surfaceB": { "convectiveCoefficient": 9.3, "shortWaveAbsorptance": 0.7, "longWaveEmissivity": 0.9 }
                }
                """;
            var wall = JsonSerializer.Deserialize<Wall>(json, CreateOptions())!;

            Assert.Equal(7, wall.ID);
            Assert.Equal(10.0, wall.Area);
            Assert.Equal(2, wall.Layers.Length);
            Assert.IsType<WallLayer>(wall.Layers[0]);
            Assert.IsType<AirGapLayer>(wall.Layers[1]);
            Assert.Equal("C", wall.Layers[0].Name);
            Assert.Equal("A", wall.Layers[1].Name);
        }

        [Fact]
        public void Read_LayerDispatch_WorksForWallLayer()
        {
            const string json = """
                {
                  "kind": "wall", "area": 1.0,
                  "layers": [
                    { "kind": "wallLayer", "name": "X", "thermalConductivity": 1.0, "volSpecificHeat": 1000, "thickness": 0.1 }
                  ]
                }
                """;
            var wall = JsonSerializer.Deserialize<Wall>(json, CreateOptions())!;
            Assert.Single(wall.Layers);
            Assert.IsType<WallLayer>(wall.Layers[0]);
            Assert.IsNotType<AirGapLayer>(wall.Layers[0]);
        }

        [Fact]
        public void Read_LayerDispatch_WorksForAirGapLayer()
        {
            const string json = """
                {
                  "kind": "wall", "area": 1.0,
                  "layers": [
                    { "kind": "airGapLayer", "name": "Air", "isSealed": true, "thickness": 0.02 }
                  ]
                }
                """;
            var wall = JsonSerializer.Deserialize<Wall>(json, CreateOptions())!;
            Assert.Single(wall.Layers);
            Assert.IsType<AirGapLayer>(wall.Layers[0]);
        }

        [Fact]
        public void Read_OnlyRequiredFields_UsesDefaultsForOptionals()
        {
            const string json = """
                {
                  "kind": "wall", "area": 5.0,
                  "layers": [
                    { "kind": "wallLayer", "name": "X", "thermalConductivity": 1.0, "volSpecificHeat": 1000, "thickness": 0.1 }
                  ]
                }
                """;
            var wall = JsonSerializer.Deserialize<Wall>(json, CreateOptions())!;

            Assert.Equal(5.0, wall.Area);
            Assert.Single(wall.Layers);
            // デフォルト値
            Assert.Equal(0.7, wall.ShortWaveAbsorptanceF);
            Assert.Equal(0.9, wall.LongWaveEmissivityF);
        }

        [Fact]
        public void Read_PropertyOrderIndependent()
        {
            const string json = """
                {
                  "layers": [
                    { "kind": "wallLayer", "name": "X", "thermalConductivity": 1.0, "volSpecificHeat": 1000, "thickness": 0.1 }
                  ],
                  "area": 5.0, "kind": "wall", "id": 3
                }
                """;
            var wall = JsonSerializer.Deserialize<Wall>(json, CreateOptions())!;
            Assert.Equal(5.0, wall.Area);
            Assert.Equal(3, wall.ID);
        }

        [Fact]
        public void Read_UnknownProperties_Ignored()
        {
            const string json = """
                {
                  "kind": "wall", "area": 1,
                  "layers": [
                    { "kind": "wallLayer", "name": "X", "thermalConductivity": 1.0, "volSpecificHeat": 1000, "thickness": 0.1 }
                  ],
                  "futureTopLevel": "ignored"
                }
                """;
            var wall = JsonSerializer.Deserialize<Wall>(json, CreateOptions())!;
            Assert.Equal(1.0, wall.Area);
        }

        #endregion

        // ================================================================
        #region ラウンドトリップ

        [Fact]
        public void RoundTrip_ThreeLayerWall_PreservesLayersAndFields()
        {
            var original = MakeThreeLayerWall();
            var json = JsonSerializer.Serialize(original, CreateOptions());
            var restored = JsonSerializer.Deserialize<Wall>(json, CreateOptions())!;

            Assert.Equal(original.ID, restored.ID);
            Assert.Equal(original.Area, restored.Area);
            Assert.Equal(original.Layers.Length, restored.Layers.Length);
            for (int i = 0; i < original.Layers.Length; i++)
            {
                Assert.Equal(original.Layers[i].GetType(), restored.Layers[i].GetType());
                Assert.Equal(original.Layers[i].Name, restored.Layers[i].Name);
                Assert.Equal(original.Layers[i].Thickness, restored.Layers[i].Thickness);
                // Kind プロパティも正しく復元される
                Assert.Equal(original.Layers[i].Kind, restored.Layers[i].Kind);
            }
        }

        [Fact]
        public void RoundTrip_PreservesAllSurfaceCoefficients()
        {
            var original = MakeThreeLayerWall();
            var json = JsonSerializer.Serialize(original, CreateOptions());
            var restored = JsonSerializer.Deserialize<Wall>(json, CreateOptions())!;

            Assert.Equal(original.ConvectiveCoefficientF, restored.ConvectiveCoefficientF);
            Assert.Equal(original.ShortWaveAbsorptanceF, restored.ShortWaveAbsorptanceF);
            Assert.Equal(original.LongWaveEmissivityF, restored.LongWaveEmissivityF);
            Assert.Equal(original.ConvectiveCoefficientB, restored.ConvectiveCoefficientB);
            Assert.Equal(original.ShortWaveAbsorptanceB, restored.ShortWaveAbsorptanceB);
            Assert.Equal(original.LongWaveEmissivityB, restored.LongWaveEmissivityB);
        }

        #endregion

        // ================================================================
        #region エラー処理

        [Fact]
        public void Read_MissingKind_Throws()
        {
            const string json = """
                {
                  "area": 1.0,
                  "layers": [
                    { "kind": "wallLayer", "name": "X", "thermalConductivity": 1.0, "volSpecificHeat": 1000, "thickness": 0.1 }
                  ]
                }
                """;
            Assert.Throws<JsonException>(() =>
                JsonSerializer.Deserialize<Wall>(json, CreateOptions()));
        }

        [Fact]
        public void Read_WrongKind_Throws()
        {
            const string json = """
                {
                  "kind": "window", "area": 1.0,
                  "layers": [
                    { "kind": "wallLayer", "name": "X", "thermalConductivity": 1.0, "volSpecificHeat": 1000, "thickness": 0.1 }
                  ]
                }
                """;
            Assert.Throws<JsonException>(() =>
                JsonSerializer.Deserialize<Wall>(json, CreateOptions()));
        }

        [Fact]
        public void Read_MissingArea_Throws()
        {
            const string json = """
                {
                  "kind": "wall",
                  "layers": [
                    { "kind": "wallLayer", "name": "X", "thermalConductivity": 1.0, "volSpecificHeat": 1000, "thickness": 0.1 }
                  ]
                }
                """;
            Assert.Throws<JsonException>(() =>
                JsonSerializer.Deserialize<Wall>(json, CreateOptions()));
        }

        [Fact]
        public void Read_MissingLayers_Throws()
        {
            const string json = """{ "kind": "wall", "area": 1.0 }""";
            Assert.Throws<JsonException>(() =>
                JsonSerializer.Deserialize<Wall>(json, CreateOptions()));
        }

        [Fact]
        public void Read_EmptyLayers_Throws()
        {
            const string json = """{ "kind": "wall", "area": 1.0, "layers": [] }""";
            Assert.Throws<JsonException>(() =>
                JsonSerializer.Deserialize<Wall>(json, CreateOptions()));
        }

        [Fact]
        public void Read_UnknownLayerKind_Throws()
        {
            const string json = """
                {
                  "kind": "wall", "area": 1.0,
                  "layers": [ { "kind": "fooLayer", "foo": 1 } ]
                }
                """;
            Assert.Throws<JsonException>(() =>
                JsonSerializer.Deserialize<Wall>(json, CreateOptions()));
        }

        [Fact]
        public void Read_LayerMissingKind_Throws()
        {
            const string json = """
                {
                  "kind": "wall", "area": 1.0,
                  "layers": [ { "name": "X", "thermalConductivity": 1.0, "volSpecificHeat": 1000, "thickness": 0.1 } ]
                }
                """;
            Assert.Throws<JsonException>(() =>
                JsonSerializer.Deserialize<Wall>(json, CreateOptions()));
        }

        [Fact]
        public void Read_SurfaceFMissingCoefficient_Throws()
        {
            // surfaceF があるのに convectiveCoefficient が欠落
            const string json = """
                {
                  "kind": "wall", "area": 1.0,
                  "layers": [
                    { "kind": "wallLayer", "name": "X", "thermalConductivity": 1.0, "volSpecificHeat": 1000, "thickness": 0.1 }
                  ],
                  "surfaceF": { "shortWaveAbsorptance": 0.7, "longWaveEmissivity": 0.9 }
                }
                """;
            Assert.Throws<JsonException>(() =>
                JsonSerializer.Deserialize<Wall>(json, CreateOptions()));
        }

        [Fact]
        public void Read_NotAnObject_Throws()
        {
            Assert.Throws<JsonException>(() =>
                JsonSerializer.Deserialize<Wall>("[1,2]", CreateOptions()));
        }

        #endregion
    }
}
