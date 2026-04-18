/* BuilelibInputIntegrationTests.cs
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
using System.IO;
using System.Linq;
using Xunit;

using Popolo.Webpro.Domain;
using Popolo.Webpro.Domain.Enums;
using Popolo.Webpro.Json;

namespace Popolo.Webpro.Tests.Integration
{
    /// <summary>
    /// End-to-end tests that read the real <c>builelib_input.json</c>
    /// reference file (288 KB, 129 rooms) and verify that the full DTO
    /// graph is populated correctly.
    /// </summary>
    /// <remarks>
    /// The test file is expected to be copied to the test output directory
    /// at <c>TestData/builelib_input.json</c> via the test project's
    /// <c>&lt;CopyToOutputDirectory&gt;</c> setting.
    /// </remarks>
    public class BuilelibInputIntegrationTests
    {
        private static string TestFilePath =>
            Path.Combine(AppContext.BaseDirectory, "TestData", "builelib_input.json");

        // ================================================================
        #region ファイルアクセス

        [Fact]
        public void TestFile_Exists()
        {
            Assert.True(File.Exists(TestFilePath),
                $"Test file not found at '{TestFilePath}'. " +
                $"Check that TestData/builelib_input.json is copied to the test output directory.");
        }

        #endregion

        // ================================================================
        #region 全体読み込み

        [Fact]
        public void Read_FullFile_Succeeds()
        {
            var model = WebproJsonReader.ReadFromFile(TestFilePath);
            Assert.NotNull(model);
        }

        [Fact]
        public void Read_BuildingFields_AsExpected()
        {
            var m = WebproJsonReader.ReadFromFile(TestFilePath);

            Assert.Equal("サンプル事務所ビル", m.Building.Name);
            Assert.Equal("6", m.Building.Region);
            Assert.Equal("A3", m.Building.AnnualSolarRegion);
            Assert.Equal(10352.79, m.Building.FloorArea);
        }

        [Fact]
        public void Read_Counts_MatchExpected()
        {
            var m = WebproJsonReader.ReadFromFile(TestFilePath);

            Assert.Equal(129, m.Rooms.Count);
            Assert.Equal(20, m.Envelopes.Count);
            Assert.Equal(3, m.WallConfigurations.Count);
            Assert.Single(m.WindowConfigurations);
            Assert.Equal(26, m.AirConditionedRoomNames.Count);
        }

        #endregion

        // ================================================================
        #region Rooms の中身

        [Fact]
        public void Read_FirstRoom_1F_風除け室()
        {
            var m = WebproJsonReader.ReadFromFile(TestFilePath);
            Assert.True(m.Rooms.TryGetValue("1F_風除け室", out var r));
            Assert.NotNull(r);

            Assert.Equal(BuildingType.Office, r!.BuildingType);
            Assert.Equal("廊下", r.RoomType);
            Assert.Equal(5.0, r.FloorHeight);
            Assert.Equal(2.6, r.CeilingHeight);
            Assert.Equal(21.12, r.RoomArea);
        }

        [Fact]
        public void Read_AllRoomsHaveSaneValues()
        {
            var m = WebproJsonReader.ReadFromFile(TestFilePath);
            foreach (var (name, r) in m.Rooms)
            {
                Assert.NotEmpty(r.RoomType);
                Assert.True(r.FloorHeight > 0, $"{name}: FloorHeight must be positive");
                Assert.True(r.CeilingHeight > 0, $"{name}: CeilingHeight must be positive");
                Assert.True(r.RoomArea > 0, $"{name}: RoomArea must be positive");
            }
        }

        #endregion

        // ================================================================
        #region EnvelopeSet の中身

        [Fact]
        public void Read_Envelope_1F_ロビー()
        {
            var m = WebproJsonReader.ReadFromFile(TestFilePath);
            Assert.True(m.Envelopes.TryGetValue("1F_ロビー", out var e));
            Assert.NotNull(e);

            Assert.True(e!.IsAirconditioned);
            Assert.Equal(2, e.Walls.Count);

            // 1本目: 南面、W1、日の当たる外壁、窓 G1
            var southWall = e.Walls[0];
            Assert.Equal(Orientation.S, southWall.SurfaceOrientation);
            Assert.Equal(50.0, southWall.Area);
            Assert.Equal("W1", southWall.WallSpec);
            Assert.Equal(WallType.ExternalWall, southWall.Type);
            Assert.Single(southWall.Windows);
            Assert.Equal("G1", southWall.Windows[0].ID);
            Assert.Equal(16.64, southWall.Windows[0].Number);

            // 2本目: 北面、FG1、地盤に接する外壁、窓なし(sentinel "無")
            var northWall = e.Walls[1];
            Assert.Equal(Orientation.N, northWall.SurfaceOrientation);
            Assert.Equal(114.12, northWall.Area);
            Assert.Equal("FG1", northWall.WallSpec);
            Assert.Equal(WallType.GroundWall, northWall.Type);
            Assert.Single(northWall.Windows);
            Assert.Equal("無", northWall.Windows[0].ID);
            Assert.Null(northWall.Windows[0].Number);
        }

        [Fact]
        public void Read_AllEnvelopeKeysExistInRooms()
        {
            var m = WebproJsonReader.ReadFromFile(TestFilePath);
            foreach (var envKey in m.Envelopes.Keys)
            {
                Assert.True(m.Rooms.ContainsKey(envKey),
                    $"Envelope '{envKey}' has no matching entry in Rooms.");
            }
        }

        [Fact]
        public void Read_AllAirConditionedRoomKeysExistInRooms()
        {
            var m = WebproJsonReader.ReadFromFile(TestFilePath);
            foreach (var acKey in m.AirConditionedRoomNames)
            {
                Assert.True(m.Rooms.ContainsKey(acKey),
                    $"AirConditioningZone '{acKey}' has no matching entry in Rooms.");
            }
        }

        #endregion

        // ================================================================
        #region WallConfigure / WindowConfigure

        [Fact]
        public void Read_WallConfigure_R1_LayersPopulated()
        {
            var m = WebproJsonReader.ReadFromFile(TestFilePath);
            Assert.True(m.WallConfigurations.TryGetValue("R1", out var wc));

            Assert.Equal(StructureType.Others, wc!.Structure);
            Assert.Equal(WallInputMethod.MaterialNumberAndThickness, wc.Method);
            Assert.Equal(9, wc.Layers.Count);

            // 最初のレイヤ: ロックウール化粧吸音板, 12mm
            Assert.Equal("ロックウール化粧吸音板", wc.Layers[0].MaterialID);
            Assert.Equal(12.0, wc.Layers[0].Thickness);

            // 3層目: 非密閉中空層(thickness は null)
            Assert.Equal("非密閉中空層", wc.Layers[2].MaterialID);
            Assert.Null(wc.Layers[2].Thickness);
        }

        [Fact]
        public void Read_WallConfigure_W1_And_FG1_Exist()
        {
            var m = WebproJsonReader.ReadFromFile(TestFilePath);
            Assert.True(m.WallConfigurations.ContainsKey("W1"));
            Assert.True(m.WallConfigurations.ContainsKey("FG1"));
        }

        [Fact]
        public void Read_WindowConfigure_G1()
        {
            var m = WebproJsonReader.ReadFromFile(TestFilePath);
            Assert.True(m.WindowConfigurations.TryGetValue("G1", out var wnc));

            Assert.Equal(1, wnc!.Area);
            Assert.Equal(WindowInputMethod.FrameAndGlazingType, wnc.Method);
            Assert.Equal(WindowFrame.MetalAndWood, wnc.Frame);
            Assert.True(wnc.IsSingleGlazing);
            Assert.Equal("T", wnc.GlazingID);
        }

        #endregion

        // ================================================================
        #region すべての Wall の WallSpec が WallConfigurations に解決できる

        [Fact]
        public void Read_AllWallSpecs_ResolveAgainstWallConfigurations()
        {
            var m = WebproJsonReader.ReadFromFile(TestFilePath);

            foreach (var (envKey, env) in m.Envelopes)
            {
                foreach (var wall in env.Walls)
                {
                    Assert.True(m.WallConfigurations.ContainsKey(wall.WallSpec),
                        $"Envelope '{envKey}' references wall spec '{wall.WallSpec}' " +
                        $"that is not in WallConfigurations.");
                }
            }
        }

        [Fact]
        public void Read_AllWindowIDs_ResolveOrAreSentinel()
        {
            var m = WebproJsonReader.ReadFromFile(TestFilePath);

            foreach (var env in m.Envelopes.Values)
            {
                foreach (var wall in env.Walls)
                {
                    foreach (var window in wall.Windows)
                    {
                        // ID は WindowConfigurations のキー、または sentinel "無"
                        bool resolved = window.ID == "無"
                            || m.WindowConfigurations.ContainsKey(window.ID);
                        Assert.True(resolved,
                            $"Window ID '{window.ID}' is neither '無' nor present in WindowConfigurations.");
                    }
                }
            }
        }

        #endregion

        // ================================================================
        #region Read(Stream) 経由も同じ結果

        [Fact]
        public void Read_ViaStream_SameResult()
        {
            using var stream = File.OpenRead(TestFilePath);
            var m = WebproJsonReader.Read(stream);
            Assert.Equal(129, m.Rooms.Count);
            Assert.Equal("6", m.Building.Region);
        }

        [Fact]
        public void Read_ViaString_SameResult()
        {
            var text = File.ReadAllText(TestFilePath);
            var m = WebproJsonReader.Read(text);
            Assert.Equal(129, m.Rooms.Count);
            Assert.Equal("6", m.Building.Region);
        }

        #endregion
    }
}
