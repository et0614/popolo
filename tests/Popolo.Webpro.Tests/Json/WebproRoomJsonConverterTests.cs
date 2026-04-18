/* WebproRoomJsonConverterTests.cs
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
using Popolo.Webpro.Domain.Enums;
using Popolo.Webpro.Json;
using Popolo.Webpro.Json.EnumConverters;

namespace Popolo.Webpro.Tests.Json
{
    /// <summary>Unit tests for <see cref="WebproRoomJsonConverter"/>.</summary>
    public class WebproRoomJsonConverterTests
    {
        private static JsonSerializerOptions CreateOptions()
        {
            var opts = new JsonSerializerOptions();
            opts.Converters.Add(new WebproRoomJsonConverter());
            opts.Converters.Add(new BuildingTypeJsonConverter());
            return opts;
        }

        // ================================================================
        #region 正常ケース

        [Fact]
        public void Read_RealSample_1F_風除け室()
        {
            // builelib_input.json の "1F_風除け室" エントリそのもの
            const string json = """
                {
                  "mainbuildingType":  "事務所等",
                  "buildingType":      "事務所等",
                  "roomType":          "廊下",
                  "floorHeight":       5.0,
                  "ceilingHeight":     2.6,
                  "roomArea":          21.12,
                  "zone":              null,
                  "modelBuildingType": "事務所モデル",
                  "buildingGroup":     null,
                  "Info":              null
                }
                """;
            var r = JsonSerializer.Deserialize<WebproRoom>(json, CreateOptions())!;

            Assert.Equal(BuildingType.Office, r.BuildingType);
            Assert.Equal("廊下", r.RoomType);
            Assert.Equal(5.0, r.FloorHeight);
            Assert.Equal(2.6, r.CeilingHeight);
            Assert.Equal(21.12, r.RoomArea);
            Assert.Null(r.Information);
        }

        [Fact]
        public void Read_OnlyRequired()
        {
            // 旧版が skip する optional キーは省略可能
            const string json = """
                {
                  "buildingType":  "ホテル等",
                  "roomType":      "客室",
                  "floorHeight":   3.2,
                  "ceilingHeight": 2.5,
                  "roomArea":      20.0
                }
                """;
            var r = JsonSerializer.Deserialize<WebproRoom>(json, CreateOptions())!;

            Assert.Equal(BuildingType.Hotel, r.BuildingType);
            Assert.Equal("客室", r.RoomType);
            Assert.Null(r.Information);
        }

        [Fact]
        public void Read_SkipsMainBuildingType_And_ModelBuildingType()
        {
            // mainbuildingType と buildingType が異なる場合でも、buildingType の値が優先される
            const string json = """
                {
                  "mainbuildingType":  "工場等",
                  "buildingType":      "事務所等",
                  "modelBuildingType": "事務所モデル",
                  "roomType":          "執務室",
                  "floorHeight":       4.0,
                  "ceilingHeight":     2.8,
                  "roomArea":          100.0
                }
                """;
            var r = JsonSerializer.Deserialize<WebproRoom>(json, CreateOptions())!;
            Assert.Equal(BuildingType.Office, r.BuildingType);
        }

        [Fact]
        public void Read_InfoPresent()
        {
            const string json = """
                {
                  "buildingType":  "事務所等",
                  "roomType":      "執務室",
                  "floorHeight":   4,
                  "ceilingHeight": 2.7,
                  "roomArea":      100,
                  "Info":          "note"
                }
                """;
            var r = JsonSerializer.Deserialize<WebproRoom>(json, CreateOptions())!;
            Assert.Equal("note", r.Information);
        }

        [Fact]
        public void Read_UnknownPropertyIgnored()
        {
            const string json = """
                {
                  "buildingType":   "事務所等",
                  "roomType":       "執務室",
                  "floorHeight":    4,
                  "ceilingHeight":  2.7,
                  "roomArea":       100,
                  "someNewField":   "x",
                  "nested":         { "a": 1 }
                }
                """;
            var r = JsonSerializer.Deserialize<WebproRoom>(json, CreateOptions())!;
            Assert.Equal(BuildingType.Office, r.BuildingType);
        }

        #endregion

        // ================================================================
        #region エラー処理

        [Fact]
        public void Read_MissingBuildingType_Throws()
        {
            const string json = """
                {
                  "roomType":      "執務室",
                  "floorHeight":   4,
                  "ceilingHeight": 2.7,
                  "roomArea":      100
                }
                """;
            Assert.Throws<JsonException>(() =>
                JsonSerializer.Deserialize<WebproRoom>(json, CreateOptions()));
        }

        [Fact]
        public void Read_MissingRoomType_Throws()
        {
            const string json = """
                {
                  "buildingType":  "事務所等",
                  "floorHeight":   4,
                  "ceilingHeight": 2.7,
                  "roomArea":      100
                }
                """;
            Assert.Throws<JsonException>(() =>
                JsonSerializer.Deserialize<WebproRoom>(json, CreateOptions()));
        }

        [Fact]
        public void Read_MissingFloorHeight_Throws()
        {
            const string json = """
                {
                  "buildingType":  "事務所等",
                  "roomType":      "執務室",
                  "ceilingHeight": 2.7,
                  "roomArea":      100
                }
                """;
            Assert.Throws<JsonException>(() =>
                JsonSerializer.Deserialize<WebproRoom>(json, CreateOptions()));
        }

        [Fact]
        public void Read_NonObjectRoot_Throws()
        {
            Assert.Throws<JsonException>(() =>
                JsonSerializer.Deserialize<WebproRoom>("[]", CreateOptions()));
        }

        [Fact]
        public void Write_Throws()
        {
            var r = new WebproRoom();
            Assert.Throws<NotSupportedException>(() =>
                JsonSerializer.Serialize(r, CreateOptions()));
        }

        #endregion
    }
}
