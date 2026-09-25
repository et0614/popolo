/* RoomTypeMapperTests.cs
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
using System.Collections.Generic;
using Xunit;

using Popolo.Webpro.Conversion;
using Popolo.Webpro.Domain;
using Popolo.Webpro.Domain.Enums;

namespace Popolo.Webpro.Tests.Conversion
{
    /// <summary>Unit tests for <see cref="RoomTypeMapper"/>.</summary>
    public class RoomTypeMapperTests
    {
        #region Helpers

        private const string SampleMappingJson = """
            {
              "mappings": [
                { "buildingType": "Office",         "roomType": "事務室",        "schedulerRoomType": "Office_Office" },
                { "buildingType": "Office",         "roomType": "会議室",        "schedulerRoomType": "Office_Meeting" },
                { "buildingType": "Hotel",          "roomType": "客室",          "schedulerRoomType": "Hotel_GuestRoom" },
                { "buildingType": "ApartmentHouse", "roomType": "ロビー",        "schedulerRoomType": "ApartmentHouse_Lobby" }
              ]
            }
            """;

        private static RoomTypeMapper CreateSample() => RoomTypeMapper.LoadFromString(SampleMappingJson);

        #endregion

        // ================================================================
        #region Loading

        [Fact]
        public void LoadFromString_ParsesEntries()
        {
            var mapper = CreateSample();
            Assert.Equal(4, mapper.Count);
        }

        [Fact]
        public void LoadFromString_Null_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => RoomTypeMapper.LoadFromString(null!));
        }

        [Fact]
        public void LoadFromString_MissingMappingsArray_Throws()
        {
            Assert.Throws<InvalidOperationException>(() =>
                RoomTypeMapper.LoadFromString("""{ "other": [] }"""));
        }

        [Fact]
        public void LoadFromString_InvalidBuildingType_Throws()
        {
            const string bad = """
                {
                  "mappings": [
                    { "buildingType": "NotAType", "roomType": "X", "schedulerRoomType": "Office_Office" }
                  ]
                }
                """;
            Assert.Throws<InvalidOperationException>(() =>
                RoomTypeMapper.LoadFromString(bad));
        }

        [Fact]
        public void LoadFromString_InvalidSchedulerRoomType_Throws()
        {
            const string bad = """
                {
                  "mappings": [
                    { "buildingType": "Office", "roomType": "X", "schedulerRoomType": "Nonexistent" }
                  ]
                }
                """;
            Assert.Throws<InvalidOperationException>(() =>
                RoomTypeMapper.LoadFromString(bad));
        }

        [Fact]
        public void LoadFromString_DuplicateKey_Throws()
        {
            const string dup = """
                {
                  "mappings": [
                    { "buildingType": "Office", "roomType": "事務室", "schedulerRoomType": "Office_Office" },
                    { "buildingType": "Office", "roomType": "事務室", "schedulerRoomType": "Office_Meeting" }
                  ]
                }
                """;
            Assert.Throws<InvalidOperationException>(() =>
                RoomTypeMapper.LoadFromString(dup));
        }

        #endregion

        // ================================================================
        #region TryGet / Get

        [Fact]
        public void TryGet_Found_ReturnsTrueAndValue()
        {
            var mapper = CreateSample();
            var ok = mapper.TryGet(BuildingType.Office, "事務室", out var sch);
            Assert.True(ok);
            Assert.Equal(WebproHeatGainScheduler.RoomType.Office_Office, sch);
        }

        [Fact]
        public void TryGet_NotFound_ReturnsFalse()
        {
            var mapper = CreateSample();
            var ok = mapper.TryGet(BuildingType.Office, "存在しない部屋", out _);
            Assert.False(ok);
        }

        [Fact]
        public void TryGet_NullRoomType_Throws()
        {
            var mapper = CreateSample();
            Assert.Throws<ArgumentNullException>(() =>
                mapper.TryGet(BuildingType.Office, null!, out _));
        }

        [Fact]
        public void Get_Found_ReturnsValue()
        {
            var mapper = CreateSample();
            var sch = mapper.Get(BuildingType.Hotel, "客室");
            Assert.Equal(WebproHeatGainScheduler.RoomType.Hotel_GuestRoom, sch);
        }

        [Fact]
        public void Get_NotFound_Throws()
        {
            var mapper = CreateSample();
            Assert.Throws<KeyNotFoundException>(() =>
                mapper.Get(BuildingType.Plant, "なにか"));
        }

        [Fact]
        public void Get_WrongBuildingType_Throws()
        {
            // Office 側に "事務室" はあるが、Hotel 側には無い
            var mapper = CreateSample();
            Assert.Throws<KeyNotFoundException>(() =>
                mapper.Get(BuildingType.Hotel, "事務室"));
        }

        #endregion

        // ================================================================
        #region Default - embedded resource

        [Fact]
        public void Default_Has265Entries()
        {
            // 旧版 v2.3 から抽出した MakeWebproHeatGain マッピング数
            Assert.Equal(265, RoomTypeMapper.Default.Count);
        }

        [Fact]
        public void Default_ContainsWellKnownPairs()
        {
            var mapper = RoomTypeMapper.Default;
            Assert.Equal(WebproHeatGainScheduler.RoomType.Office_Office,
                mapper.Get(BuildingType.Office, "事務室"));
            Assert.Equal(WebproHeatGainScheduler.RoomType.Office_Lobby,
                mapper.Get(BuildingType.Office, "ロビー"));
            Assert.Equal(WebproHeatGainScheduler.RoomType.Hotel_GuestRoom,
                mapper.Get(BuildingType.Hotel, "客室"));
        }

        [Fact]
        public void Default_LockerRoomAliasesAllMap()
        {
            // 複数の別名が同じ enum 値にマップされるパターン(旧版踏襲)
            var mapper = RoomTypeMapper.Default;
            var expected = WebproHeatGainScheduler.RoomType.Office_LockerRoom;
            Assert.Equal(expected, mapper.Get(BuildingType.Office, "更衣室又は倉庫"));
            Assert.Equal(expected, mapper.Get(BuildingType.Office, "更衣室・倉庫"));
            Assert.Equal(expected, mapper.Get(BuildingType.Office, "更衣室"));
            Assert.Equal(expected, mapper.Get(BuildingType.Office, "倉庫"));
        }

        [Fact]
        public void Default_IsSingleton()
        {
            Assert.Same(RoomTypeMapper.Default, RoomTypeMapper.Default);
        }

        #endregion
    }
}
