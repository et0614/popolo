/* BuildingThermalModelConverterTests.cs
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

using Popolo.Core.Building;
using Popolo.Core.Building.Envelope;
using Popolo.Core.Climate;
using Popolo.IO.Json;
using Popolo.IO.Json.Building;

namespace Popolo.IO.Tests.Json.Building
{
    /// <summary>Unit tests for <see cref="BuildingThermalModelConverter"/>.</summary>
    public class BuildingThermalModelConverterTests
    {
        #region ヘルパー

        /// <summary>Options with all converters pre-registered.</summary>
        private static JsonSerializerOptions CreateOptions()
            => PopoloJsonSerializer.CreateDefaultOptions();

        private static Wall MakeExternalWall(int id)
        {
            var layers = new WallLayer[] { new WallLayer("Concrete", 1.4, 1934, 0.15) };
            var wall = new Wall(12.0, layers);
            wall.ID = id;
            return wall;
        }

        /// <summary>Build a minimal but non-trivial BuildingThermalModel.</summary>
        private static BuildingThermalModel MakeSimpleModel()
        {
            var zoneA = new Zone("Room A", 120.0, 10.0);
            var wall = MakeExternalWall(0);
            var mRooms = new MultiRooms(1,
                new[] { zoneA }, new[] { wall }, Array.Empty<Window>());
            mRooms.AddZone(0, 0);
            mRooms.AddWall(0, 0, true);
            mRooms.SetOutsideWall(0, true, new Incline(0d, Math.PI / 2));

            var model = new BuildingThermalModel(new[] { mRooms });
            model.TimeStep = 3600;
            model.UpdateOutdoorCondition(
                new DateTime(2026, 4, 18, 12, 0, 0),
                new Sun(35.6812, 139.7671, 135.0),
                15.0, 0.008, 0.0);
            return model;
        }

        /// <summary>Build a larger model with 2 MultiRooms and shared walls.</summary>
        private static BuildingThermalModel MakeTwoMultiRoomsModel()
        {
            // MultiRooms 1: 1 zone, 1 external wall
            var zone1 = new Zone("Zone 1", 100, 10);
            var wall1 = MakeExternalWall(0);
            var mr1 = new MultiRooms(1, new[] { zone1 }, new[] { wall1 }, Array.Empty<Window>());
            mr1.AddZone(0, 0);
            mr1.AddWall(0, 0, true);
            mr1.SetOutsideWall(0, true, new Incline(0d, Math.PI / 2));

            // MultiRooms 2: 1 zone, 1 ground wall
            var zone2 = new Zone("Zone 2", 200, 20);
            var wall2 = MakeExternalWall(1);
            var mr2 = new MultiRooms(1, new[] { zone2 }, new[] { wall2 }, Array.Empty<Window>());
            mr2.AddZone(0, 0);
            mr2.AddWall(0, 0, true);
            mr2.SetGroundWall(0, true, 3.5);

            var model = new BuildingThermalModel(new[] { mr1, mr2 });
            model.TimeStep = 1800;
            model.UpdateOutdoorCondition(
                new DateTime(2026, 7, 15, 14, 30, 0),
                new Sun(35.68, 139.77, 135.0), 28.0, 0.015, 0.0);
            return model;
        }

        private static int CountProperties(JsonElement obj)
        {
            int count = 0;
            foreach (var _ in obj.EnumerateObject()) count++;
            return count;
        }

        #endregion

        // ================================================================
        #region シリアライズ - 基本構造

        [Fact]
        public void Write_TopLevel_HasSchemaVersionAndKind()
        {
            var model = MakeSimpleModel();
            var json = JsonSerializer.Serialize(model, CreateOptions());
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.Equal("3.0", root.GetProperty("$schemaVersion").GetString());
            Assert.Equal("buildingThermalModel", root.GetProperty("kind").GetString());
        }

        [Fact]
        public void Write_ContainsAllRequiredSections()
        {
            var model = MakeSimpleModel();
            var json = JsonSerializer.Serialize(model, CreateOptions());
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.True(root.TryGetProperty("timeStep", out _));
            Assert.True(root.TryGetProperty("currentDateTime", out _));
            Assert.True(root.TryGetProperty("initialState", out _));
            Assert.True(root.TryGetProperty("sun", out _));
            Assert.True(root.TryGetProperty("walls", out _));
            Assert.True(root.TryGetProperty("multiRooms", out _));
        }

        [Fact]
        public void Write_CurrentDateTime_Iso8601Format()
        {
            var model = MakeSimpleModel();
            var json = JsonSerializer.Serialize(model, CreateOptions());
            using var doc = JsonDocument.Parse(json);

            Assert.Equal("2026-04-18T12:00:00",
                doc.RootElement.GetProperty("currentDateTime").GetString());
        }

        [Fact]
        public void Write_InitialState_HasTwoFields()
        {
            var model = MakeSimpleModel();
            var json = JsonSerializer.Serialize(model, CreateOptions());
            using var doc = JsonDocument.Parse(json);
            var state = doc.RootElement.GetProperty("initialState");

            Assert.Equal(2, CountProperties(state));
            Assert.True(state.TryGetProperty("temperature", out _));
            Assert.True(state.TryGetProperty("humidityRatio", out _));
        }

        [Fact]
        public void Write_Walls_AssignsSequentialIds()
        {
            var model = MakeTwoMultiRoomsModel();
            var json = JsonSerializer.Serialize(model, CreateOptions());
            using var doc = JsonDocument.Parse(json);
            var walls = doc.RootElement.GetProperty("walls");

            Assert.Equal(2, walls.GetArrayLength());
            Assert.Equal(0, walls[0].GetProperty("id").GetInt32());
            Assert.Equal(1, walls[1].GetProperty("id").GetInt32());
        }

        [Fact]
        public void Write_Sun_IncludedAsObject()
        {
            var model = MakeSimpleModel();
            var json = JsonSerializer.Serialize(model, CreateOptions());
            using var doc = JsonDocument.Parse(json);
            var sun = doc.RootElement.GetProperty("sun");

            Assert.Equal("sun", sun.GetProperty("kind").GetString());
            Assert.InRange(sun.GetProperty("latitude").GetDouble(), 35.68, 35.69);
        }

        [Fact]
        public void Write_MultiRooms_ArrayLengthMatchesModel()
        {
            var model = MakeTwoMultiRoomsModel();
            var json = JsonSerializer.Serialize(model, CreateOptions());
            using var doc = JsonDocument.Parse(json);

            Assert.Equal(2, doc.RootElement.GetProperty("multiRooms").GetArrayLength());
        }

        #endregion

        // ================================================================
        #region デシリアライズ

        [Fact]
        public void Read_SimpleModel_Succeeds()
        {
            var original = MakeSimpleModel();
            var json = JsonSerializer.Serialize(original, CreateOptions());
            var restored = JsonSerializer.Deserialize<BuildingThermalModel>(json, CreateOptions())!;

            Assert.NotNull(restored);
            Assert.Equal(3600.0, restored.TimeStep);
            Assert.Single(restored.MultiRoom);
        }

        [Fact]
        public void Read_CurrentDateTime_Preserved()
        {
            var original = MakeSimpleModel();
            var json = JsonSerializer.Serialize(original, CreateOptions());
            var restored = JsonSerializer.Deserialize<BuildingThermalModel>(json, CreateOptions())!;

            Assert.Equal(new DateTime(2026, 4, 18, 12, 0, 0), restored.CurrentDateTime);
        }

        [Fact]
        public void Read_Sun_Preserved()
        {
            var original = MakeSimpleModel();
            var json = JsonSerializer.Serialize(original, CreateOptions());
            var restored = JsonSerializer.Deserialize<BuildingThermalModel>(json, CreateOptions())!;

            Assert.InRange(restored.Sun.Latitude, 35.68, 35.69);
            Assert.Equal(135.0, restored.Sun.StandardLongitude);
        }

        #endregion

        // ================================================================
        #region ラウンドトリップ

        [Fact]
        public void RoundTrip_SimpleModel_PreservesBasics()
        {
            var original = MakeSimpleModel();
            var json = JsonSerializer.Serialize(original, CreateOptions());
            var restored = JsonSerializer.Deserialize<BuildingThermalModel>(json, CreateOptions())!;

            Assert.Equal(original.TimeStep, restored.TimeStep);
            Assert.Equal(original.CurrentDateTime, restored.CurrentDateTime);
            Assert.Equal(original.MultiRoom.Length, restored.MultiRoom.Length);
        }

        [Fact]
        public void RoundTrip_TwoMultiRoomsModel_PreservesStructure()
        {
            var original = MakeTwoMultiRoomsModel();
            var json = JsonSerializer.Serialize(original, CreateOptions());
            var restored = JsonSerializer.Deserialize<BuildingThermalModel>(json, CreateOptions())!;

            Assert.Equal(2, restored.MultiRoom.Length);
            Assert.Equal(original.MultiRoom[0].ZoneCount, restored.MultiRoom[0].ZoneCount);
            Assert.Equal(original.MultiRoom[1].ZoneCount, restored.MultiRoom[1].ZoneCount);
        }

        [Fact]
        public void RoundTrip_WallReferences_Resolved()
        {
            var original = MakeSimpleModel();
            var json = JsonSerializer.Serialize(original, CreateOptions());
            var restored = JsonSerializer.Deserialize<BuildingThermalModel>(json, CreateOptions())!;

            // 外壁参照が正しく復元されているか
            var origRefs = ((MultiRooms)original.MultiRoom[0]).GetOutsideWallReferences();
            var restRefs = ((MultiRooms)restored.MultiRoom[0]).GetOutsideWallReferences();

            Assert.Equal(origRefs.Length, restRefs.Length);
            Assert.Equal(origRefs[0].IsSideF, restRefs[0].IsSideF);
        }

        [Fact]
        public void RoundTrip_GroundWallConductance_Preserved()
        {
            var original = MakeTwoMultiRoomsModel();
            var json = JsonSerializer.Serialize(original, CreateOptions());
            var restored = JsonSerializer.Deserialize<BuildingThermalModel>(json, CreateOptions())!;

            // MultiRooms[1] は地中壁設定 conductance=3.5
            var groundRefs = ((MultiRooms)restored.MultiRoom[1]).GetGroundWallReferences();
            Assert.Single(groundRefs);
            Assert.Equal(3.5, groundRefs[0].Conductance);
        }

        [Fact]
        public void RoundTrip_ZoneNames_Preserved()
        {
            var original = MakeTwoMultiRoomsModel();
            var json = JsonSerializer.Serialize(original, CreateOptions());
            var restored = JsonSerializer.Deserialize<BuildingThermalModel>(json, CreateOptions())!;

            Assert.Equal("Zone 1", restored.MultiRoom[0].Zones[0].Name);
            Assert.Equal("Zone 2", restored.MultiRoom[1].Zones[0].Name);
        }

        #endregion

        // ================================================================
        #region エラー処理

        [Fact]
        public void Read_MissingKind_Throws()
        {
            const string json = """
                {
                  "$schemaVersion": "3.0",
                  "timeStep": 3600,
                  "currentDateTime": "2026-04-18T12:00:00",
                  "sun": { "kind": "sun", "latitude": 35, "longitude": 139, "standardLongitude": 135 },
                  "walls": [],
                  "multiRooms": []
                }
                """;
            Assert.Throws<JsonException>(() =>
                JsonSerializer.Deserialize<BuildingThermalModel>(json, CreateOptions()));
        }

        [Fact]
        public void Read_WrongKind_Throws()
        {
            const string json = """
                {
                  "$schemaVersion": "3.0", "kind": "wall",
                  "timeStep": 3600, "currentDateTime": "2026-04-18T12:00:00",
                  "sun": { "kind": "sun", "latitude": 35, "longitude": 139, "standardLongitude": 135 },
                  "walls": [], "multiRooms": []
                }
                """;
            Assert.Throws<JsonException>(() =>
                JsonSerializer.Deserialize<BuildingThermalModel>(json, CreateOptions()));
        }

        [Fact]
        public void Read_InvalidIso8601_Throws()
        {
            const string json = """
                {
                  "$schemaVersion": "3.0", "kind": "buildingThermalModel",
                  "timeStep": 3600, "currentDateTime": "not a date",
                  "sun": { "kind": "sun", "latitude": 35, "longitude": 139, "standardLongitude": 135 },
                  "walls": [], "multiRooms": []
                }
                """;
            Assert.Throws<JsonException>(() =>
                JsonSerializer.Deserialize<BuildingThermalModel>(json, CreateOptions()));
        }

        [Fact]
        public void Read_MissingSun_Throws()
        {
            const string json = """
                {
                  "$schemaVersion": "3.0", "kind": "buildingThermalModel",
                  "timeStep": 3600, "currentDateTime": "2026-04-18T12:00:00",
                  "walls": [], "multiRooms": []
                }
                """;
            Assert.Throws<JsonException>(() =>
                JsonSerializer.Deserialize<BuildingThermalModel>(json, CreateOptions()));
        }

        [Fact]
        public void Read_MissingWalls_Throws()
        {
            const string json = """
                {
                  "$schemaVersion": "3.0", "kind": "buildingThermalModel",
                  "timeStep": 3600, "currentDateTime": "2026-04-18T12:00:00",
                  "sun": { "kind": "sun", "latitude": 35, "longitude": 139, "standardLongitude": 135 },
                  "multiRooms": []
                }
                """;
            Assert.Throws<JsonException>(() =>
                JsonSerializer.Deserialize<BuildingThermalModel>(json, CreateOptions()));
        }

        [Fact]
        public void Read_MissingMultiRooms_Throws()
        {
            const string json = """
                {
                  "$schemaVersion": "3.0", "kind": "buildingThermalModel",
                  "timeStep": 3600, "currentDateTime": "2026-04-18T12:00:00",
                  "sun": { "kind": "sun", "latitude": 35, "longitude": 139, "standardLongitude": 135 },
                  "walls": []
                }
                """;
            Assert.Throws<JsonException>(() =>
                JsonSerializer.Deserialize<BuildingThermalModel>(json, CreateOptions()));
        }

        [Fact]
        public void Read_DuplicateWallId_Throws()
        {
            const string json = """
                {
                  "$schemaVersion": "3.0", "kind": "buildingThermalModel",
                  "timeStep": 3600, "currentDateTime": "2026-04-18T12:00:00",
                  "sun": { "kind": "sun", "latitude": 35, "longitude": 139, "standardLongitude": 135 },
                  "walls": [
                    { "kind": "wall", "id": 5, "area": 10, "computeMoistureTransfer": false,
                      "layers": [{"kind":"wallLayer","name":"X","thermalConductivity":1,"volSpecificHeat":1000,"thickness":0.1}] },
                    { "kind": "wall", "id": 5, "area": 12, "computeMoistureTransfer": false,
                      "layers": [{"kind":"wallLayer","name":"Y","thermalConductivity":1,"volSpecificHeat":1000,"thickness":0.1}] }
                  ],
                  "multiRooms": []
                }
                """;
            Assert.Throws<JsonException>(() =>
                JsonSerializer.Deserialize<BuildingThermalModel>(json, CreateOptions()));
        }

        #endregion
    }
}
