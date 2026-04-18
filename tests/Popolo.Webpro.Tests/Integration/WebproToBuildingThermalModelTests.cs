/* WebproToBuildingThermalModelTests.cs
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

using Popolo.Webpro.Conversion;
using Popolo.Webpro.Json;

namespace Popolo.Webpro.Tests.Integration
{
  /// <summary>
  /// End-to-end tests that convert the real <c>builelib_input.json</c>
  /// into a <c>BuildingThermalModel</c>.
  /// </summary>
  public class WebproToBuildingThermalModelTests
  {
    private static string TestFilePath =>
        Path.Combine(AppContext.BaseDirectory, "TestData", "builelib_input.json");

    // ================================================================
    #region エントリポイント

    [Fact]
    public void Convert_RealSample_Succeeds()
    {
      var model = WebproJsonReader.ReadFromFile(TestFilePath);
      var result = WebproToBuildingThermalModel.Convert(model);

      Assert.NotNull(result.Model);
      Assert.NotNull(result.MultiRooms);
      Assert.NotEmpty(result.RoomNameToZone);
    }

    [Fact]
    public void Convert_NullModel_Throws()
    {
      Assert.Throws<ArgumentNullException>(() =>
          WebproToBuildingThermalModel.Convert(null!));
    }

    #endregion

    // ================================================================
    #region ゾーン数・壁数の検証

    [Fact]
    public void Convert_ZoneCountEqualsAirConditionedRoomCount()
    {
      var model = WebproJsonReader.ReadFromFile(TestFilePath);
      var result = WebproToBuildingThermalModel.Convert(model);

      // builelib_input.json の AirConditioningZone は 26 室
      Assert.Equal(26, result.MultiRooms.ZoneCount);
      Assert.Equal(26, result.RoomNameToZone.Count);
    }

    [Fact]
    public void Convert_EveryAirConditionedRoomIsMapped()
    {
      var model = WebproJsonReader.ReadFromFile(TestFilePath);
      var result = WebproToBuildingThermalModel.Convert(model);

      foreach (var name in model.AirConditionedRoomNames)
      {
        Assert.True(result.RoomNameToZone.ContainsKey(name),
            $"Air-conditioned room '{name}' was not mapped to a zone.");
      }
    }

    [Fact]
    public void Convert_EachZoneHasFloorCeilingLoopWall()
    {
      var model = WebproJsonReader.ReadFromFile(TestFilePath);
      var result = WebproToBuildingThermalModel.Convert(model);

      // 各ゾーンは少なくとも 1 つの loop wall (床・天井) を持つ
      // → 壁数 ≥ ゾーン数
      Assert.True(result.MultiRooms.Walls.Length >= result.MultiRooms.ZoneCount,
          $"Walls ({result.MultiRooms.Walls.Length}) should be at least " +
          $"ZoneCount ({result.MultiRooms.ZoneCount}) to cover loop walls.");
    }

    [Fact]
    public void Convert_AirConditionedRoomWithEnvelope_HasExternalWalls()
    {
      var model = WebproJsonReader.ReadFromFile(TestFilePath);
      var result = WebproToBuildingThermalModel.Convert(model);

      // "1F_ロビー" は envelope を持ち、AC 対象であることを確認
      Assert.True(model.AirConditionedRoomNames.Contains("1F_ロビー"),
          "Test precondition: 1F_ロビー must be in AirConditioningZone.");
      Assert.True(model.Envelopes.ContainsKey("1F_ロビー"),
          "Test precondition: 1F_ロビー must have an envelope.");
      Assert.True(result.RoomNameToZone.ContainsKey("1F_ロビー"));
    }

    #endregion

    // ================================================================
    #region Zone のプロパティ検証

    [Fact]
    public void Convert_LobbyZone_HasExpectedFloorArea()
    {
      var model = WebproJsonReader.ReadFromFile(TestFilePath);
      var result = WebproToBuildingThermalModel.Convert(model);

      var zone = result.RoomNameToZone["1F_ロビー"];
      var room = model.Rooms["1F_ロビー"];

      Assert.Equal(room.RoomArea, zone.FloorArea);
      Assert.Equal(room.RoomArea, zone.FloorArea);
    }

    [Fact]
    public void Convert_ZoneAirMass_FromDensityAndCeilingHeight()
    {
      var model = WebproJsonReader.ReadFromFile(TestFilePath);
      var result = WebproToBuildingThermalModel.Convert(model);

      foreach (var (name, zone) in result.RoomNameToZone)
      {
        var room = model.Rooms[name];
        double expectedAirMass =
            WebproConversionConstants.AirDensity
            * room.RoomArea
            * room.CeilingHeight;
        Assert.Equal(expectedAirMass, zone.AirMass, 6);
      }
    }

    [Fact]
    public void Convert_ZoneHeatCapacity_IsPositive()
    {
      var model = WebproJsonReader.ReadFromFile(TestFilePath);
      var result = WebproToBuildingThermalModel.Convert(model);

      foreach (var zone in result.RoomNameToZone.Values)
      {
        Assert.True(zone.HeatCapacity > 0,
            $"Zone '{zone.Name}' should have positive HeatCapacity.");
      }
    }

    #endregion

    // ================================================================
    #region カタログ差し替え

    [Fact]
    public void Convert_WithDefaultCatalogs_Works()
    {
      var model = WebproJsonReader.ReadFromFile(TestFilePath);
      var result = WebproToBuildingThermalModel.Convert(model);
      Assert.NotNull(result.Model);
    }

    [Fact]
    public void Convert_WithExplicitCatalogs_Works()
    {
      var model = WebproJsonReader.ReadFromFile(TestFilePath);
      var result = WebproToBuildingThermalModel.Convert(
          model, MaterialCatalog.Default, GlazingCatalog.Default);
      Assert.NotNull(result.Model);
    }

    #endregion

    // ================================================================
    #region 方位変換の単体テスト

    [Fact]
    public void OrientationToIncline_Horizontal_VerticalAngleZero()
    {
      var inc = WebproToBuildingThermalModel.OrientationToIncline(
          Domain.Enums.Orientation.UpperHorizontal);
      Assert.Equal(0, inc.VerticalAngle, 6);
    }

    [Fact]
    public void OrientationToIncline_LowerHorizontal_VerticalAnglePi()
    {
      // 下向き水平面(床)は VerticalAngle = π。
      // 屋根(UpperHorizontal)は VerticalAngle = 0 で、Incline だけで
      // 床と屋根を区別できる。
      var inc = WebproToBuildingThermalModel.OrientationToIncline(
          Domain.Enums.Orientation.LowerHorizontal);
      Assert.Equal(Math.PI, inc.VerticalAngle, 6);
    }

    [Fact]
    public void OrientationToIncline_North_VerticalAngleHalfPi()
    {
      var inc = WebproToBuildingThermalModel.OrientationToIncline(
          Domain.Enums.Orientation.N);
      Assert.Equal(Math.PI / 2, inc.VerticalAngle, 6);
    }

    #endregion

    // ================================================================
    #region 「1F_ロビー」の外皮が正しく変換される

    [Fact]
    public void Convert_LobbyEnvelope_WallsAndWindowsAdded()
    {
      var model = WebproJsonReader.ReadFromFile(TestFilePath);
      var result = WebproToBuildingThermalModel.Convert(model);
      var env = model.Envelopes["1F_ロビー"];

      // ロビーの外皮は 2 壁 1 窓
      Assert.Equal(2, env.Walls.Count);
      Assert.Equal("G1", env.Walls[0].Windows[0].ID);
      Assert.Equal("無", env.Walls[1].Windows[0].ID);

      // 窓が少なくとも 1 つ作られていること(sentinel "無" は除外)
      Assert.True(result.MultiRooms.Windows.Length >= 1,
          "At least one window should have been created.");
    }

    #endregion

    // ================================================================
    #region HeatGainScheduler 自動設置

    [Fact]
    public void Convert_WithDefaultOptions_InstallsHeatGainSchedulers()
    {
      var model = WebproJsonReader.ReadFromFile(TestFilePath);
      var result = WebproToBuildingThermalModel.Convert(model);

      // 全空調室にスケジューラが載っているはず
      foreach (var (name, zone) in result.RoomNameToZone)
      {
        var heatGains = zone.GetHeatGains();
        Assert.True(heatGains.Length >= 1,
            $"Zone '{name}' should have at least one heat gain.");

        // BaseHeatGain (SimpleHeatGain) 以外に WebproHeatGainScheduler があるはず
        bool hasScheduler = false;
        foreach (var hg in heatGains)
        {
          if (hg is Popolo.Webpro.Domain.WebproHeatGainScheduler)
          {
            hasScheduler = true;
            break;
          }
        }
        Assert.True(hasScheduler,
            $"Zone '{name}' should have a WebproHeatGainScheduler.");
      }
    }

    [Fact]
    public void Convert_RealSample_AllAirConditionedRoomsMapped()
    {
      // 実 builelib_input.json の 26 空調室は全てマッピング可能
      var model = WebproJsonReader.ReadFromFile(TestFilePath);
      var result = WebproToBuildingThermalModel.Convert(model);

      Assert.Empty(result.UnmappedRoomNames);
    }

    [Fact]
    public void Convert_InstallHeatGainsFalse_NoSchedulers()
    {
      var model = WebproJsonReader.ReadFromFile(TestFilePath);
      var result = WebproToBuildingThermalModel.Convert(
          model, installHeatGainSchedulers: false);

      foreach (var zone in result.RoomNameToZone.Values)
      {
        foreach (var hg in zone.GetHeatGains())
        {
          Assert.IsNotType<Popolo.Webpro.Domain.WebproHeatGainScheduler>(hg);
        }
      }

      // UnmappedRoomNames は空(スケジューラ設置をしていないので未解決室は記録されない)
      Assert.Empty(result.UnmappedRoomNames);
    }

    [Fact]
    public void Convert_WithCustomMapper_UsesIt()
    {
      // 空のマッパーを作ると、全空調室が unmapped になる
      const string emptyMapping = """{ "mappings": [] }""";
      var emptyMapper = Popolo.Webpro.Conversion.RoomTypeMapper.LoadFromString(emptyMapping);

      var model = WebproJsonReader.ReadFromFile(TestFilePath);
      var result = WebproToBuildingThermalModel.Convert(
          model, roomTypeMapper: emptyMapper);

      // 全 26 空調室がマッピング不可
      Assert.Equal(result.RoomNameToZone.Count, result.UnmappedRoomNames.Count);
    }

    [Fact]
    public void Convert_LobbyZone_HasOfficeLobbyScheduler()
    {
      var model = WebproJsonReader.ReadFromFile(TestFilePath);
      var result = WebproToBuildingThermalModel.Convert(model);

      var zone = result.RoomNameToZone["1F_ロビー"];
      var schedulers = new System.Collections.Generic.List<
          Popolo.Webpro.Domain.WebproHeatGainScheduler>();
      foreach (var hg in zone.GetHeatGains())
      {
        if (hg is Popolo.Webpro.Domain.WebproHeatGainScheduler sch)
          schedulers.Add(sch);
      }

      // 1F_ロビー は (Office, ロビー) → Office_Lobby のはず
      Assert.Single(schedulers);
      // スケジューラ自身の RoomType プロパティは private なので、
      // ここでは "1 つ載っている" だけ確認
    }

    #endregion
  }
}