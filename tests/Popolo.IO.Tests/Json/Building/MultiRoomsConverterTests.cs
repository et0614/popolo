/* MultiRoomsConverterTests.cs
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
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using Xunit;

using Popolo.Core.Building;
using Popolo.Core.Building.Envelope;
using Popolo.Core.Climate;
using Popolo.IO.Json.Building;
using Popolo.IO.Json.Climate;
using Popolo.IO.Json.Building.Envelope;

namespace Popolo.IO.Tests.Json.Building
{
    /// <summary>Unit tests for <see cref="MultiRoomsConverter"/>.</summary>
    /// <remarks>
    /// This test class uses <c>InternalsVisibleTo</c> to access the internal
    /// <c>ReadDto</c> and <c>BuildMultiRooms</c> methods of MultiRoomsConverter.
    /// The Popolo.IO project must declare
    /// <c>&lt;InternalsVisibleTo Include="Popolo.IO.Tests" /&gt;</c> for this to compile.
    /// </remarks>
    public class MultiRoomsConverterTests
    {
        #region ヘルパー

        private static JsonSerializerOptions CreateOptions()
        {
            var opts = new JsonSerializerOptions();
            opts.Converters.Add(new MultiRoomsConverter());
            opts.Converters.Add(new ZoneConverter());
            opts.Converters.Add(new WallConverter());
            opts.Converters.Add(new WallLayerConverter());
            opts.Converters.Add(new AirGapLayerConverter());
            opts.Converters.Add(new WindowConverter());
            opts.Converters.Add(new InclineConverter());
            opts.Converters.Add(new NoShadingDeviceConverter());
            opts.Converters.Add(new SimpleShadingDeviceConverter());
            opts.Converters.Add(new VenetianBlindConverter());
            opts.Converters.Add(new SunShadeConverter());
            return opts;
        }

        private static Wall MakeInternalWall(int id)
        {
            var layers = new WallLayer[]
            {
                new WallLayer("Gypsum", 0.17, 870.0, 0.012),
                new AirGapLayer("Air", isSealed: true, thickness: 0.05),
                new WallLayer("Gypsum", 0.17, 870.0, 0.012),
            };
            var wall = new Wall(10.0, layers);
            wall.ID = id;
            return wall;
        }

        private static Wall MakeExternalWall(int id)
        {
            var layers = new WallLayer[] { new WallLayer("Concrete", 1.4, 1934, 0.15) };
            var wall = new Wall(12.0, layers);
            wall.ID = id;
            return wall;
        }

        /// <summary>
        /// Build a two-zone MultiRooms model:
        ///  - 2 rooms, 1 zone each
        ///  - zone 0 has 1 external wall (south-vertical) + internal-wall F side
        ///  - zone 1 has internal-wall B side + 1 ground wall
        ///  - zone 0 → zone 1 airflow of 0.1 kg/s
        /// </summary>
        private static MultiRoom MakeTwoZoneModel(out Wall[] walls)
        {
            var zoneA = new Zone("Room A", 120.0, 10.0);
            var zoneB = new Zone("Room B", 240.0, 20.0);

            var extWall = MakeExternalWall(1);
            var intWall = MakeInternalWall(2);
            var gndWall = MakeExternalWall(3);
            walls = new Wall[] { extWall, intWall, gndWall };

            var mRooms = new MultiRoom(
                rmCount: 2,
                zones: new[] { zoneA, zoneB },
                walls: walls,
                windows: Array.Empty<Window>());
            mRooms.AddZone(0, 0);
            mRooms.AddZone(1, 1);
            mRooms.AddWall(0, 0, true);
            mRooms.AddWall(0, 1, true);
            mRooms.AddWall(1, 1, false);
            mRooms.AddWall(1, 2, true);
            mRooms.SetOutsideWall(0, true, new Incline(0d, Math.PI / 2));
            mRooms.SetGroundWall(2, true, 5.0);
            mRooms.SetAirFlow(0, 1, 0.1);
            return mRooms;
        }

        /// <summary>
        /// Helper that reads a JSON string into MultiRoomsDto via the internal ReadDto.
        /// Wraps the ref struct plumbing that reflection cannot handle.
        /// </summary>
        private static MultiRoomsDto ReadDtoFromJson(string json, JsonSerializerOptions options)
        {
            var bytes = Encoding.UTF8.GetBytes(json);
            var reader = new Utf8JsonReader(bytes);
            reader.Read(); // Advance to StartObject
            return MultiRoomsConverter.ReadDto(ref reader, options);
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
        public void Write_TwoZoneModel_ProducesExpectedTopLevelFields()
        {
            var mRooms = MakeTwoZoneModel(out _);
            var json = JsonSerializer.Serialize(mRooms, CreateOptions());
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.Equal("multiRooms", root.GetProperty("kind").GetString());
            Assert.Equal(0.4, root.GetProperty("albedo").GetDouble());
            Assert.Equal(2, root.GetProperty("rooms").GetArrayLength());
            Assert.Equal(JsonValueKind.Array, root.GetProperty("outsideWalls").ValueKind);
            Assert.Equal(JsonValueKind.Array, root.GetProperty("groundWalls").ValueKind);
            Assert.Equal(JsonValueKind.Array, root.GetProperty("adjacentSpaces").ValueKind);
            Assert.Equal(JsonValueKind.Array, root.GetProperty("interZoneAirflows").ValueKind);
        }

        [Fact]
        public void Write_Rooms_EachRoomHasItsZones()
        {
            var mRooms = MakeTwoZoneModel(out _);
            var json = JsonSerializer.Serialize(mRooms, CreateOptions());
            using var doc = JsonDocument.Parse(json);
            var rooms = doc.RootElement.GetProperty("rooms");

            Assert.Equal(2, rooms.GetArrayLength());
            Assert.Equal(1, rooms[0].GetProperty("zones").GetArrayLength());
            Assert.Equal(1, rooms[1].GetProperty("zones").GetArrayLength());
            Assert.Equal("Room A", rooms[0].GetProperty("zones")[0].GetProperty("name").GetString());
            Assert.Equal("Room B", rooms[1].GetProperty("zones")[0].GetProperty("name").GetString());
        }

        [Fact]
        public void Write_OutsideWalls_EntryHasWallIdSideFAndIncline()
        {
            var mRooms = MakeTwoZoneModel(out _);
            var json = JsonSerializer.Serialize(mRooms, CreateOptions());
            using var doc = JsonDocument.Parse(json);
            var outsideWalls = doc.RootElement.GetProperty("outsideWalls");

            Assert.Equal(1, outsideWalls.GetArrayLength());
            var e = outsideWalls[0];
            Assert.Equal(1, e.GetProperty("wallId").GetInt32());
            Assert.True(e.GetProperty("sideF").GetBoolean());
            Assert.Equal("incline", e.GetProperty("incline").GetProperty("kind").GetString());
        }

        [Fact]
        public void Write_GroundWalls_EntryHasConductance()
        {
            var mRooms = MakeTwoZoneModel(out _);
            var json = JsonSerializer.Serialize(mRooms, CreateOptions());
            using var doc = JsonDocument.Parse(json);
            var gws = doc.RootElement.GetProperty("groundWalls");

            Assert.Equal(1, gws.GetArrayLength());
            Assert.Equal(3, gws[0].GetProperty("wallId").GetInt32());
            Assert.Equal(5.0, gws[0].GetProperty("conductance").GetDouble());
        }

        [Fact]
        public void Write_InterZoneAirflows_Sparse()
        {
            var mRooms = MakeTwoZoneModel(out _);
            var json = JsonSerializer.Serialize(mRooms, CreateOptions());
            using var doc = JsonDocument.Parse(json);
            var flows = doc.RootElement.GetProperty("interZoneAirflows");

            Assert.Equal(1, flows.GetArrayLength());
            Assert.Equal(0, flows[0].GetProperty("fromZoneIndex").GetInt32());
            Assert.Equal(1, flows[0].GetProperty("toZoneIndex").GetInt32());
            Assert.Equal(0.1, flows[0].GetProperty("flowRate").GetDouble());
        }

        [Fact]
        public void Write_AdjacentSpaces_EmptyWhenNone()
        {
            var mRooms = MakeTwoZoneModel(out _);
            var json = JsonSerializer.Serialize(mRooms, CreateOptions());
            using var doc = JsonDocument.Parse(json);
            Assert.Equal(0, doc.RootElement.GetProperty("adjacentSpaces").GetArrayLength());
        }

        #endregion

        // ================================================================
        #region Read 直接は NotSupported

        [Fact]
        public void Read_Direct_ThrowsNotSupported()
        {
            const string json = """
                { "kind": "multiRooms", "albedo": 0.4, "rooms": [],
                  "outsideWalls": [], "groundWalls": [], "adjacentSpaces": [], "interZoneAirflows": [] }
                """;
            Assert.Throws<NotSupportedException>(() =>
                JsonSerializer.Deserialize<MultiRoom>(json, CreateOptions()));
        }

        #endregion

        // ================================================================
        #region DTO 読み取り

        [Fact]
        public void ReadDto_MinimalModel_Succeeds()
        {
            const string json = """
                { "kind": "multiRooms", "albedo": 0.35, "rooms": [],
                  "outsideWalls": [], "groundWalls": [], "adjacentSpaces": [], "interZoneAirflows": [] }
                """;
            var dto = ReadDtoFromJson(json, CreateOptions());

            Assert.Equal(0.35, dto.Albedo);
            Assert.Empty(dto.Rooms);
            Assert.Empty(dto.OutsideWalls);
        }

        [Fact]
        public void ReadDto_WithRoomsAndZones_PopulatesLists()
        {
            const string json = """
                {
                  "kind": "multiRooms", "albedo": 0.4,
                  "rooms": [
                    { "zones": [
                        { "kind": "zone", "name": "Room A", "airMass": 120, "floorArea": 10,
                          "heatCapacity": 0, "moistureCapacity": 0, "walls": [], "windows": [] }
                      ]
                    }
                  ],
                  "outsideWalls": [], "groundWalls": [], "adjacentSpaces": [], "interZoneAirflows": []
                }
                """;
            var dto = ReadDtoFromJson(json, CreateOptions());

            Assert.Single(dto.Rooms);
            Assert.Single(dto.Rooms[0]);
            Assert.Equal("Room A", dto.Rooms[0][0].Name);
        }

        [Fact]
        public void ReadDto_WithOutsideWalls_Populates()
        {
            const string json = """
                {
                  "kind": "multiRooms", "albedo": 0.4, "rooms": [],
                  "outsideWalls": [
                    { "wallId": 42, "sideF": true,
                      "incline": { "kind": "incline", "horizontalAngle": 0, "verticalAngle": 1.5708 } }
                  ],
                  "groundWalls": [], "adjacentSpaces": [], "interZoneAirflows": []
                }
                """;
            var dto = ReadDtoFromJson(json, CreateOptions());

            Assert.Single(dto.OutsideWalls);
            Assert.Equal(42, dto.OutsideWalls[0].WallId);
            Assert.True(dto.OutsideWalls[0].IsSideF);
            Assert.InRange(dto.OutsideWalls[0].Incline.VerticalAngle, 1.5707, 1.5709);
        }

        [Fact]
        public void ReadDto_WithGroundWalls_Populates()
        {
            const string json = """
                {
                  "kind": "multiRooms", "albedo": 0.4, "rooms": [], "outsideWalls": [],
                  "groundWalls": [ { "wallId": 7, "sideF": false, "conductance": 3.5 } ],
                  "adjacentSpaces": [], "interZoneAirflows": []
                }
                """;
            var dto = ReadDtoFromJson(json, CreateOptions());

            Assert.Single(dto.GroundWalls);
            Assert.Equal(7, dto.GroundWalls[0].WallId);
            Assert.False(dto.GroundWalls[0].IsSideF);
            Assert.Equal(3.5, dto.GroundWalls[0].Conductance);
        }

        [Fact]
        public void ReadDto_WithAdjacentSpaces_Populates()
        {
            const string json = """
                {
                  "kind": "multiRooms", "albedo": 0.4, "rooms": [], "outsideWalls": [], "groundWalls": [],
                  "adjacentSpaces": [ { "wallId": 9, "sideF": true, "temperatureDifferenceFactor": 0.7 } ],
                  "interZoneAirflows": []
                }
                """;
            var dto = ReadDtoFromJson(json, CreateOptions());

            Assert.Single(dto.AdjacentSpaces);
            Assert.Equal(9, dto.AdjacentSpaces[0].WallId);
            Assert.Equal(0.7, dto.AdjacentSpaces[0].TemperatureDifferenceFactor);
        }

        [Fact]
        public void ReadDto_WithInterzoneAirflows_Populates()
        {
            const string json = """
                {
                  "kind": "multiRooms", "albedo": 0.4, "rooms": [],
                  "outsideWalls": [], "groundWalls": [], "adjacentSpaces": [],
                  "interZoneAirflows": [
                    { "fromZoneIndex": 0, "toZoneIndex": 1, "flowRate": 0.15 },
                    { "fromZoneIndex": 2, "toZoneIndex": 0, "flowRate": 0.05 }
                  ]
                }
                """;
            var dto = ReadDtoFromJson(json, CreateOptions());

            Assert.Equal(2, dto.InterZoneAirflows.Count);
            Assert.Equal(0, dto.InterZoneAirflows[0].FromZoneIndex);
            Assert.Equal(1, dto.InterZoneAirflows[0].ToZoneIndex);
            Assert.Equal(0.15, dto.InterZoneAirflows[0].FlowRate);
        }

        #endregion

        // ================================================================
        #region ラウンドトリップ via DTO + BuildMultiRooms

        [Fact]
        public void RoundTrip_ViaDto_RestoresBasicStructure()
        {
            var original = MakeTwoZoneModel(out var walls);
            var json = JsonSerializer.Serialize(original, CreateOptions());
            var dto = ReadDtoFromJson(json, CreateOptions());

            var dict = new Dictionary<int, Wall>();
            foreach (var w in walls) dict[w.ID] = w;
            var restored = MultiRoomsConverter.BuildMultiRooms(dto, dict);

            Assert.Equal(original.RoomCount, restored.RoomCount);
            Assert.Equal(original.ZoneCount, restored.ZoneCount);
            Assert.Equal(original.Albedo, restored.Albedo);
        }

        [Fact]
        public void RoundTrip_ViaDto_RestoresOutsideWalls()
        {
            var original = MakeTwoZoneModel(out var walls);
            var json = JsonSerializer.Serialize(original, CreateOptions());
            var dto = ReadDtoFromJson(json, CreateOptions());
            var dict = new Dictionary<int, Wall>();
            foreach (var w in walls) dict[w.ID] = w;
            var restored = MultiRoomsConverter.BuildMultiRooms(dto, dict);

            var origRefs = original.GetOutsideWallReferences();
            var restRefs = restored.GetOutsideWallReferences();
            Assert.Equal(origRefs.Length, restRefs.Length);
            Assert.Equal(origRefs[0].WallId, restRefs[0].WallId);
            Assert.Equal(origRefs[0].IsSideF, restRefs[0].IsSideF);
        }

        [Fact]
        public void RoundTrip_ViaDto_RestoresGroundWalls()
        {
            var original = MakeTwoZoneModel(out var walls);
            var json = JsonSerializer.Serialize(original, CreateOptions());
            var dto = ReadDtoFromJson(json, CreateOptions());
            var dict = new Dictionary<int, Wall>();
            foreach (var w in walls) dict[w.ID] = w;
            var restored = MultiRoomsConverter.BuildMultiRooms(dto, dict);

            var origRefs = original.GetGroundWallReferences();
            var restRefs = restored.GetGroundWallReferences();
            Assert.Equal(origRefs.Length, restRefs.Length);
            Assert.Equal(origRefs[0].Conductance, restRefs[0].Conductance);
        }

        [Fact]
        public void RoundTrip_ViaDto_RestoresInterzoneAirflow()
        {
            var original = MakeTwoZoneModel(out var walls);
            var json = JsonSerializer.Serialize(original, CreateOptions());
            var dto = ReadDtoFromJson(json, CreateOptions());
            var dict = new Dictionary<int, Wall>();
            foreach (var w in walls) dict[w.ID] = w;
            var restored = MultiRoomsConverter.BuildMultiRooms(dto, dict);

            Assert.Equal(0.1, restored.GetAirFlow(0, 1));
            Assert.Equal(0.0, restored.GetAirFlow(1, 0));
        }

        [Fact]
        public void RoundTrip_ViaDto_AdjacentSpaceFactor()
        {
            var zoneA = new Zone("A", 120, 10);
            var wall = MakeExternalWall(7);
            var mRooms = new MultiRoom(1, new[] { zoneA }, new[] { wall }, Array.Empty<Window>());
            mRooms.AddZone(0, 0);
            mRooms.AddWall(0, 0, true);
            mRooms.UseAdjacentSpaceFactor(0, true, 0.7);

            var json = JsonSerializer.Serialize(mRooms, CreateOptions());
            var dto = ReadDtoFromJson(json, CreateOptions());
            var dict = new Dictionary<int, Wall> { { 7, wall } };
            var restored = MultiRoomsConverter.BuildMultiRooms(dto, dict);

            var refs = restored.GetAdjacentSpaceWallReferences();
            Assert.Single(refs);
            Assert.Equal(7, refs[0].WallId);
            Assert.Equal(0.7, refs[0].TemperatureDifferenceFactor);
        }

        [Fact]
        public void RoundTrip_ViaDto_PreservesZoneNames()
        {
            var original = MakeTwoZoneModel(out var walls);
            var json = JsonSerializer.Serialize(original, CreateOptions());
            var dto = ReadDtoFromJson(json, CreateOptions());
            var dict = new Dictionary<int, Wall>();
            foreach (var w in walls) dict[w.ID] = w;
            var restored = MultiRoomsConverter.BuildMultiRooms(dto, dict);

            Assert.Equal("Room A", restored.Zones[0].Name);
            Assert.Equal("Room B", restored.Zones[1].Name);
        }

        [Fact]
        public void RoundTrip_ViaDto_ThreeRoomsSixZones()
        {
            var zones = new Zone[6];
            for (int i = 0; i < 6; i++) zones[i] = new Zone($"Z{i}", 100, 10);
            var walls = new Wall[] { MakeExternalWall(1) };
            var mRooms = new MultiRoom(3, zones, walls, Array.Empty<Window>());
            mRooms.AddZone(0, 0); mRooms.AddZone(0, 1);
            mRooms.AddZone(1, 2); mRooms.AddZone(1, 3);
            mRooms.AddZone(2, 4); mRooms.AddZone(2, 5);
            mRooms.SetOutsideWall(0, true, new Incline(0d, Math.PI / 2));

            var json = JsonSerializer.Serialize(mRooms, CreateOptions());
            var dto = ReadDtoFromJson(json, CreateOptions());
            var dict = new Dictionary<int, Wall> { { 1, walls[0] } };
            var restored = MultiRoomsConverter.BuildMultiRooms(dto, dict);

            Assert.Equal(3, restored.RoomCount);
            Assert.Equal(6, restored.ZoneCount);
            for (int i = 0; i < 6; i++)
            {
                Assert.Equal(i / 2, ((Zone)restored.Zones[i]).RoomIndex);
            }
        }

        #endregion

        // ================================================================
        #region エラー処理

        [Fact]
        public void ReadDto_MissingKind_Throws()
        {
            const string json = """
                { "albedo": 0.4, "rooms": [], "outsideWalls": [], "groundWalls": [], "adjacentSpaces": [], "interZoneAirflows": [] }
                """;
            Assert.Throws<JsonException>(() => ReadDtoFromJson(json, CreateOptions()));
        }

        [Fact]
        public void ReadDto_WrongKind_Throws()
        {
            const string json = """
                { "kind": "zone", "albedo": 0.4, "rooms": [], "outsideWalls": [], "groundWalls": [], "adjacentSpaces": [], "interZoneAirflows": [] }
                """;
            Assert.Throws<JsonException>(() => ReadDtoFromJson(json, CreateOptions()));
        }

        [Fact]
        public void ReadDto_MissingAlbedo_Throws()
        {
            const string json = """
                { "kind": "multiRooms", "rooms": [], "outsideWalls": [], "groundWalls": [], "adjacentSpaces": [], "interZoneAirflows": [] }
                """;
            Assert.Throws<JsonException>(() => ReadDtoFromJson(json, CreateOptions()));
        }

        [Fact]
        public void BuildMultiRooms_UnknownWallId_Throws()
        {
            const string json = """
                {
                  "kind": "multiRooms", "albedo": 0.4, "rooms": [],
                  "outsideWalls": [ { "wallId": 999, "sideF": true,
                    "incline": { "kind": "incline", "horizontalAngle": 0, "verticalAngle": 1.5708 } } ],
                  "groundWalls": [], "adjacentSpaces": [], "interZoneAirflows": []
                }
                """;
            var dto = ReadDtoFromJson(json, CreateOptions());
            var dict = new Dictionary<int, Wall>();
            Assert.Throws<JsonException>(() => MultiRoomsConverter.BuildMultiRooms(dto, dict));
        }

        [Fact]
        public void ReadDto_OutsideWallMissingIncline_Throws()
        {
            const string json = """
                {
                  "kind": "multiRooms", "albedo": 0.4, "rooms": [],
                  "outsideWalls": [ { "wallId": 1, "sideF": true } ],
                  "groundWalls": [], "adjacentSpaces": [], "interZoneAirflows": []
                }
                """;
            Assert.Throws<JsonException>(() => ReadDtoFromJson(json, CreateOptions()));
        }

        [Fact]
        public void ReadDto_InterzoneAirflowMissingField_Throws()
        {
            const string json = """
                {
                  "kind": "multiRooms", "albedo": 0.4, "rooms": [],
                  "outsideWalls": [], "groundWalls": [], "adjacentSpaces": [],
                  "interZoneAirflows": [ { "fromZoneIndex": 0, "toZoneIndex": 1 } ]
                }
                """;
            Assert.Throws<JsonException>(() => ReadDtoFromJson(json, CreateOptions()));
        }

        #endregion
    }
}
