/* BuildingThermalModelConverterTests.cs
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

using Popolo.Core.Building;
using Popolo.Core.Building.Envelope;
using Popolo.Core.Climate;
using Popolo.Core.Climate.Weather;
using Popolo.IO.Json;
using Popolo.IO.Json.Building;

namespace Popolo.IO.Tests.Json.Building
{
    /// <summary>Unit tests for <see cref="BuildingThermalModelConverter"/>.</summary>
    public class BuildingThermalModelConverterTests
    {
        #region Helpers

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
            var mRooms = new MultiRoom(1,
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
            var mr1 = new MultiRoom(1, new[] { zone1 }, new[] { wall1 }, Array.Empty<Window>());
            mr1.AddZone(0, 0);
            mr1.AddWall(0, 0, true);
            mr1.SetOutsideWall(0, true, new Incline(0d, Math.PI / 2));

            // MultiRooms 2: 1 zone, 1 ground wall
            var zone2 = new Zone("Zone 2", 200, 20);
            var wall2 = MakeExternalWall(1);
            var mr2 = new MultiRoom(1, new[] { zone2 }, new[] { wall2 }, Array.Empty<Window>());
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
        #region Serialization - basic structure

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
        #region Deserialization

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
        #region Round trip

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
            var origRefs = ((MultiRoom)original.MultiRoom[0]).GetOutsideWallReferences();
            var restRefs = ((MultiRoom)restored.MultiRoom[0]).GetOutsideWallReferences();

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
            var groundRefs = ((MultiRoom)restored.MultiRoom[1]).GetGroundWallReferences();
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

        /// <summary>item 14 の設定値をすべて既定値以外にした単一 MultiRoom モデル。</summary>
        private static BuildingThermalModel MakeConfiguredModel()
        {
            var model = MakeSimpleModel();
            var mr = (MultiRoom)model.MultiRoom[0];
            var wall = (Wall)mr.Walls[0];

            // Zone: 換気量・給気条件
            mr.SetVentilationRate(0, 0.05);
            mr.SetSupplyAir(0, 16.0, 0.008, 0.3);

            // MultiRoom: 気象観測点・地表面粗度区分・動的係数フラグ
            model.SetWeatherStation(new WeatherStationInfo("Tokyo", 35.69, 139.69, 25.0)
                .WithAnemometer(10.0, TerrainCategory.Suburban));
            model.SetSiteTerrainCategory(TerrainCategory.LargeCity);
            mr.DynamicIndoorRadiativeCoefficient = true;
            mr.DynamicOutdoorRadiativeCoefficient = false;
            mr.DynamicOutdoorConvectiveCoefficient = true;
            mr.DynamicIndoorConvectiveCoefficient = true;

            // Wall: 風曝露フラグ（SetOutsideWall が自動で立てた F 側を明示的に倒す）・粗度・中央高さ
            wall.IsWindExposedF = false;
            wall.IsWindExposedB = true;
            wall.SurfaceRoughnessMultiplierF = 2.17;
            wall.SurfaceRoughnessMultiplierB = 1.52;
            wall.SetMidHeightAboveGround(4.5);
            return model;
        }

        /// <summary>
        /// Zone / MultiRoom / Wall の設定値（換気量・給気条件、気象観測点・地表面粗度区分・
        /// 動的係数フラグ、風曝露フラグ・表面粗度係数・中央高さ）が往復で保存されることを確認する。
        /// 従来はこれらが黙って欠落し、既定値で復元されていた。
        /// </summary>
        [Fact]
        public void RoundTrip_ConfigurationProperties_Preserved()
        {
            var json = JsonSerializer.Serialize(MakeConfiguredModel(), CreateOptions());
            var restored = JsonSerializer.Deserialize<BuildingThermalModel>(json, CreateOptions())!;
            var mr = (MultiRoom)restored.MultiRoom[0];
            var zone = mr.Zones[0];
            var wall = (Wall)mr.Walls[0];

            Assert.Equal(0.05, zone.VentilationRate);
            Assert.Equal(0.3, zone.SupplyAirFlowRate);
            Assert.Equal(16.0, zone.SupplyAirTemperature);
            Assert.Equal(0.008, zone.SupplyAirHumidityRatio);

            Assert.NotNull(mr.WeatherStation);
            var st = mr.WeatherStation!.Value;
            Assert.Equal("Tokyo", st.Name);
            Assert.Equal(35.69, st.Latitude);
            Assert.Equal(139.69, st.Longitude);
            Assert.Equal(25.0, st.Elevation);
            Assert.Equal(10.0, st.AnemometerHeight);
            Assert.Equal(TerrainCategory.Suburban, st.StationTerrain);
            Assert.Equal(TerrainCategory.LargeCity, mr.SiteTerrainCategory);
            // 全 MultiRoom で共通なので建物レベルの値も復元される
            Assert.Equal(st, restored.WeatherStation);
            Assert.Equal(TerrainCategory.LargeCity, restored.SiteTerrainCategory);

            Assert.True(mr.DynamicIndoorRadiativeCoefficient);
            Assert.False(mr.DynamicOutdoorRadiativeCoefficient);
            Assert.True(mr.DynamicOutdoorConvectiveCoefficient);
            Assert.True(mr.DynamicIndoorConvectiveCoefficient);

            Assert.False(wall.IsWindExposedF);
            Assert.True(wall.IsWindExposedB);
            Assert.Equal(2.17, wall.SurfaceRoughnessMultiplierF);
            Assert.Equal(1.52, wall.SurfaceRoughnessMultiplierB);
            Assert.Equal(4.5, wall.MidHeightAboveGround);
        }

        /// <summary>
        /// 新しい設定プロパティを持たない旧形式 JSON は、従来通り既定値で読み込まれる
        /// （外壁は SetOutsideWall により風曝露＝true になる）ことを確認する。
        /// </summary>
        [Fact]
        public void Read_LegacyJsonWithoutConfigurationProperties_UsesDefaults()
        {
            var json = JsonSerializer.Serialize(MakeConfiguredModel(), CreateOptions());
            var node = System.Text.Json.Nodes.JsonNode.Parse(json)!;
            foreach (var w in node["walls"]!.AsArray())
                foreach (var p in new[] { "isWindExposedF", "isWindExposedB",
                    "surfaceRoughnessMultiplierF", "surfaceRoughnessMultiplierB", "midHeightAboveGround" })
                    w!.AsObject().Remove(p);
            foreach (var m in node["multiRooms"]!.AsArray())
            {
                foreach (var p in new[] { "weatherStation", "siteTerrainCategory", "dynamicCoefficients" })
                    m!.AsObject().Remove(p);
                foreach (var room in m!["rooms"]!.AsArray())
                    foreach (var z in room!["zones"]!.AsArray())
                        foreach (var p in new[] { "ventilationRate", "supplyAir" })
                            z!.AsObject().Remove(p);
            }

            var restored = JsonSerializer.Deserialize<BuildingThermalModel>(node.ToJsonString(), CreateOptions())!;
            var mr = (MultiRoom)restored.MultiRoom[0];

            Assert.Equal(0.0, mr.Zones[0].VentilationRate);
            Assert.Equal(0.0, mr.Zones[0].SupplyAirFlowRate);
            Assert.Null(mr.WeatherStation);
            Assert.Null(mr.SiteTerrainCategory);
            Assert.Null(restored.WeatherStation);
            Assert.False(mr.DynamicIndoorRadiativeCoefficient);
            Assert.False(mr.DynamicOutdoorConvectiveCoefficient);
            Assert.True(((Wall)mr.Walls[0]).IsWindExposedF);   // SetOutsideWall による自動設定
            Assert.False(((Wall)mr.Walls[0]).IsWindExposedB);
            // Wall コンストラクタの既定値（Rough）
            Assert.Equal(MakeExternalWall(0).SurfaceRoughnessMultiplierF, ((Wall)mr.Walls[0]).SurfaceRoughnessMultiplierF);
            Assert.Null(((Wall)mr.Walls[0]).MidHeightAboveGround);
        }

        /// <summary>
        /// 2 つの MultiRoom が壁を共有するモデル。MR1 の壁順 [wC, wShared] は
        /// 書き出し時の通し ID (wA=0, wShared=1, wC=2) の昇順とは異なる。
        /// </summary>
        private static BuildingThermalModel MakeSharedWallTwoMultiRoomsModel()
        {
            var incline = new Incline(0d, Math.PI / 2);
            var wA = MakeExternalWall(100);
            var wShared = MakeExternalWall(101);
            var wC = MakeExternalWall(102);

            var z0 = new Zone("Z0", 100, 10);
            var mr0 = new MultiRoom(1, new[] { z0 }, new[] { wA, wShared }, Array.Empty<Window>());
            mr0.AddWall(0, 0, true);
            mr0.SetOutsideWall(0, false, incline);
            mr0.AddWall(0, 1, true);       // 共有壁の F 側が Z0 に面する

            var z1 = new Zone("Z1", 100, 10);
            var mr1 = new MultiRoom(1, new[] { z1 }, new[] { wC, wShared }, Array.Empty<Window>());
            mr1.AddWall(0, 0, true);
            mr1.SetOutsideWall(0, false, incline);
            mr1.AddWall(0, 1, false);      // 共有壁の B 側が Z1 に面する

            var model = new BuildingThermalModel(new[] { mr0, mr1 });
            model.TimeStep = 3600;
            model.UpdateOutdoorCondition(new DateTime(2026, 1, 1, 0, 0, 0),
                new Sun(35.68, 139.77, 135.0), 5.0, 0.004, 0.0);
            return model;
        }

        /// <summary>
        /// 複数 MultiRoom の往復で、各 MultiRoom の Walls 配列の長さと順序
        /// （＝壁インデックス）が保存されることを確認する。
        /// 従来は全 MultiRoom に建物全体の壁表 (ID 昇順) が渡され、長さも順序も変わっていた。
        /// </summary>
        [Fact]
        public void RoundTrip_MultipleMultiRooms_PreservesPerMultiRoomWallOrder()
        {
            var original = MakeSharedWallTwoMultiRoomsModel();
            var json = JsonSerializer.Serialize(original, CreateOptions());
            var restored = JsonSerializer.Deserialize<BuildingThermalModel>(json, CreateOptions())!;

            for (int m = 0; m < 2; m++)
            {
                var o = original.MultiRoom[m].Walls;
                var r = restored.MultiRoom[m].Walls;
                Assert.Equal(o.Length, r.Length);
                for (int i = 0; i < o.Length; i++)
                    Assert.Equal(o[i].ID, r[i].ID);
            }
            // 共有壁は同一インスタンスとして復元される
            Assert.Same(restored.MultiRoom[0].Walls[1], restored.MultiRoom[1].Walls[1]);
            // MR1 の壁インデックス 0 に外壁が接続されている（インデックスが保たれている）
            var ow = ((MultiRoom)restored.MultiRoom[1]).GetOutsideWallReferences();
            Assert.Single(ow);
            Assert.Equal(restored.MultiRoom[1].Walls[0].ID, ow[0].WallId);
        }

        /// <summary>
        /// wallIds を持たない旧形式 JSON（複数 MultiRoom）では、各 MultiRoom が
        /// 参照する壁のみを ID 昇順で持つ（建物全体の壁表を渡さない）ことを確認する。
        /// </summary>
        [Fact]
        public void Read_LegacyJsonWithoutWallIds_MultipleMultiRooms_UsesReferencedWallsOnly()
        {
            var json = JsonSerializer.Serialize(MakeSharedWallTwoMultiRoomsModel(), CreateOptions());
            var node = System.Text.Json.Nodes.JsonNode.Parse(json)!;
            foreach (var mr in node["multiRooms"]!.AsArray())
                mr!.AsObject().Remove("wallIds");
            var legacy = node.ToJsonString();

            var restored = JsonSerializer.Deserialize<BuildingThermalModel>(legacy, CreateOptions())!;

            Assert.Equal(new[] { 0, 1 }, Array.ConvertAll(restored.MultiRoom[0].Walls, w => w.ID));
            Assert.Equal(new[] { 1, 2 }, Array.ConvertAll(restored.MultiRoom[1].Walls, w => w.ID));
        }

        /// <summary>
        /// wallIds を持たない旧形式 JSON（単一 MultiRoom）は従来通り
        /// 壁表全体を ID 昇順で持つ（既存ファイルの読み込み結果を変えない）ことを確認する。
        /// </summary>
        [Fact]
        public void Read_LegacyJsonWithoutWallIds_SingleMultiRoom_UsesWholeWallTable()
        {
            var json = JsonSerializer.Serialize(MakeSimpleModel(), CreateOptions());
            var node = System.Text.Json.Nodes.JsonNode.Parse(json)!;
            // 参照されていない壁を壁表に追加しても、単一 MultiRoom では従来通り含まれる
            var extraWall = node["walls"]![0]!.DeepClone();
            extraWall["id"] = 7;
            node["walls"]!.AsArray().Add(extraWall);
            node["multiRooms"]![0]!.AsObject().Remove("wallIds");

            var restored = JsonSerializer.Deserialize<BuildingThermalModel>(node.ToJsonString(), CreateOptions())!;

            Assert.Equal(new[] { 0, 7 }, Array.ConvertAll(restored.MultiRoom[0].Walls, w => w.ID));
        }

        #endregion

        // ================================================================
        #region Error handling

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
