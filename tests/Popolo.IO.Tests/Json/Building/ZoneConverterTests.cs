/* ZoneConverterTests.cs
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
using Popolo.IO.Json.Building;
using Popolo.IO.Json.Climate;
using Popolo.IO.Json.Building.Envelope;

namespace Popolo.IO.Tests.Json.Building
{
  /// <summary>Unit tests for <see cref="ZoneConverter"/>.</summary>
  public class ZoneConverterTests
  {
    #region ヘルパー

    /// <summary>
    /// Options with ZoneConverter plus all converters needed for nested Windows.
    /// </summary>
    private static JsonSerializerOptions CreateOptions()
    {
      var opts = new JsonSerializerOptions();
      opts.Converters.Add(new ZoneConverter());
      opts.Converters.Add(new WindowConverter());
      opts.Converters.Add(new InclineConverter());
      opts.Converters.Add(new NoShadingDeviceConverter());
      opts.Converters.Add(new SimpleShadingDeviceConverter());
      opts.Converters.Add(new VenetianBlindConverter());
      opts.Converters.Add(new SunShadeConverter());
      return opts;
    }

    /// <summary>Zone with minimal fields only.</summary>
    private static Zone MakeMinimalZone()
        => new Zone("Room A", airMass: 360.0, floorArea: 30.0);

    /// <summary>Zone with all fields configured.</summary>
    private static Zone MakeFullyConfiguredZone()
    {
      var zone = new Zone("Living Room", airMass: 360.0, floorArea: 30.0);
      zone.HeatCapacity = 360000; // J/K
      zone.MoistureCapacity = 0.5;
      zone.HeatingCapacity = 5000;
      zone.CoolingCapacity = 5000;
      zone.HumidifyingCapacity = 0.001;
      zone.DehumidifyingCapacity = 0.001;
      zone.SetBaseHeatGain(
          convectiveHeatGain: 100,
          radiativeHeatGain: 50,
          moistureGain: 0.0001);
      return zone;
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
    public void Write_MinimalZone_ProducesExpectedBaseFields()
    {
      var json = JsonSerializer.Serialize(MakeMinimalZone(), CreateOptions());
      using var doc = JsonDocument.Parse(json);
      var root = doc.RootElement;

      Assert.Equal("zone", root.GetProperty("kind").GetString());
      Assert.Equal("Room A", root.GetProperty("name").GetString());
      Assert.Equal(360.0, root.GetProperty("airMass").GetDouble());
      Assert.Equal(30.0, root.GetProperty("floorArea").GetDouble());
      Assert.Equal(0.0, root.GetProperty("heatCapacity").GetDouble());
      Assert.Equal(0.0, root.GetProperty("moistureCapacity").GetDouble());
      // 壁も窓も無いが、空配列として出力される
      Assert.Equal(0, root.GetProperty("walls").GetArrayLength());
      Assert.Equal(0, root.GetProperty("windows").GetArrayLength());
    }

    [Fact]
    public void Write_MinimalZone_OmitsCapacitiesWhenAllInfinite()
    {
      var json = JsonSerializer.Serialize(MakeMinimalZone(), CreateOptions());
      using var doc = JsonDocument.Parse(json);
      Assert.False(doc.RootElement.TryGetProperty("capacities", out _));
    }

    [Fact]
    public void Write_MinimalZone_OmitsBaseHeatGainWhenAllZero()
    {
      var json = JsonSerializer.Serialize(MakeMinimalZone(), CreateOptions());
      using var doc = JsonDocument.Parse(json);
      Assert.False(doc.RootElement.TryGetProperty("baseHeatGain", out _));
    }

    [Fact]
    public void Write_FullyConfiguredZone_IncludesAllSections()
    {
      var json = JsonSerializer.Serialize(MakeFullyConfiguredZone(), CreateOptions());
      using var doc = JsonDocument.Parse(json);
      var root = doc.RootElement;

      Assert.True(root.TryGetProperty("capacities", out var cap));
      Assert.Equal(4, CountProperties(cap));
      Assert.Equal(5000, cap.GetProperty("heating").GetDouble());

      Assert.True(root.TryGetProperty("baseHeatGain", out var bg));
      Assert.Equal(3, CountProperties(bg));
      Assert.Equal(100, bg.GetProperty("convectiveHeatGain").GetDouble());
    }

    [Fact]
    public void Write_PartialCapacities_OnlyIncludesFiniteValues()
    {
      // heating のみ有限、他は無限
      var zone = new Zone("Partial", 360.0, 30.0);
      zone.HeatingCapacity = 3000;
      // 他はデフォルトで Infinity のまま

      var json = JsonSerializer.Serialize(zone, CreateOptions());
      using var doc = JsonDocument.Parse(json);
      var cap = doc.RootElement.GetProperty("capacities");

      Assert.Equal(1, CountProperties(cap));
      Assert.Equal(3000, cap.GetProperty("heating").GetDouble());
    }

    #endregion

    // ================================================================
    #region デシリアライズ - 基本

    [Fact]
    public void Read_MinimalZone_Succeeds()
    {
      const string json = """
                {
                  "kind": "zone",
                  "name": "Room A",
                  "airMass": 360.0,
                  "floorArea": 30.0,
                  "heatCapacity": 0.0,
                  "moistureCapacity": 0.0,
                  "walls": [],
                  "windows": []
                }
                """;
      var zone = JsonSerializer.Deserialize<Zone>(json, CreateOptions())!;

      Assert.Equal("Room A", zone.Name);
      Assert.Equal(360.0, zone.AirMass);
      Assert.Equal(30.0, zone.FloorArea);
      // デフォルト = 無限大
      Assert.True(double.IsPositiveInfinity(zone.HeatingCapacity));
      Assert.True(double.IsPositiveInfinity(zone.CoolingCapacity));
    }

    [Fact]
    public void Read_MissingCapacities_RestoresAllInfinite()
    {
      const string json = """
                { "kind": "zone", "name": "X", "airMass": 100, "floorArea": 10,
                  "heatCapacity": 0, "moistureCapacity": 0,
                  "walls": [], "windows": [] }
                """;
      var zone = JsonSerializer.Deserialize<Zone>(json, CreateOptions())!;

      Assert.True(double.IsPositiveInfinity(zone.HeatingCapacity));
      Assert.True(double.IsPositiveInfinity(zone.CoolingCapacity));
      Assert.True(double.IsPositiveInfinity(zone.HumidifyingCapacity));
      Assert.True(double.IsPositiveInfinity(zone.DehumidifyingCapacity));
    }

    [Fact]
    public void Read_PartialCapacities_FillsOthersWithInfinity()
    {
      const string json = """
                { "kind": "zone", "name": "X", "airMass": 100, "floorArea": 10,
                  "heatCapacity": 0, "moistureCapacity": 0,
                  "capacities": { "heating": 3000 },
                  "walls": [], "windows": [] }
                """;
      var zone = JsonSerializer.Deserialize<Zone>(json, CreateOptions())!;

      Assert.Equal(3000, zone.HeatingCapacity);
      Assert.True(double.IsPositiveInfinity(zone.CoolingCapacity));
      Assert.True(double.IsPositiveInfinity(zone.HumidifyingCapacity));
      Assert.True(double.IsPositiveInfinity(zone.DehumidifyingCapacity));
    }

    [Fact]
    public void Read_BaseHeatGain_SetsSimpleHeatGainValues()
    {
      const string json = """
                { "kind": "zone", "name": "X", "airMass": 100, "floorArea": 10,
                  "heatCapacity": 0, "moistureCapacity": 0,
                  "baseHeatGain": { "convectiveHeatGain": 80, "radiativeHeatGain": 40, "moistureGain": 0.00008 },
                  "walls": [], "windows": [] }
                """;
      var zone = JsonSerializer.Deserialize<Zone>(json, CreateOptions())!;

      var sg = Assert.IsType<SimpleHeatGain>(zone.BaseHeatGain);
      Assert.Equal(80, sg.ConvectiveHeatGain);
      Assert.Equal(40, sg.RadiativeHeatGain);
      Assert.Equal(0.00008, sg.MoistureGain);
    }

    [Fact]
    public void Read_UnknownProperties_Ignored()
    {
      const string json = """
                { "kind": "zone", "name": "X", "airMass": 100, "floorArea": 10,
                  "heatCapacity": 0, "moistureCapacity": 0,
                  "walls": [], "windows": [],
                  "futureField": "x", "nested": { "a": 1 } }
                """;
      var zone = JsonSerializer.Deserialize<Zone>(json, CreateOptions())!;
      Assert.Equal("X", zone.Name);
    }

    #endregion

    // ================================================================
    #region Deserialization Context - 壁参照と Windows

    [Fact]
    public void Read_WallReferences_AttachedToContext()
    {
      const string json = """
                { "kind": "zone", "name": "X", "airMass": 100, "floorArea": 10,
                  "heatCapacity": 0, "moistureCapacity": 0,
                  "walls": [
                    { "wallId": 42, "sideF": true },
                    { "wallId": 43, "sideF": false }
                  ],
                  "windows": [] }
                """;
      var zone = JsonSerializer.Deserialize<Zone>(json, CreateOptions())!;

      var ctx = ZoneDeserializationContext.TryGet(zone);
      Assert.NotNull(ctx);
      Assert.Equal(2, ctx!.WallReferences.Count);
      Assert.Equal(42, ctx.WallReferences[0].WallId);
      Assert.True(ctx.WallReferences[0].IsSideF);
      Assert.Equal(43, ctx.WallReferences[1].WallId);
      Assert.False(ctx.WallReferences[1].IsSideF);
    }

    [Fact]
    public void Read_Windows_AttachedToContext()
    {
      const string json = """
                { "kind": "zone", "name": "X", "airMass": 100, "floorArea": 10,
                  "heatCapacity": 0, "moistureCapacity": 0,
                  "walls": [],
                  "windows": [
                    { "kind": "window", "area": 2.0,
                      "outsideIncline": { "kind": "incline", "horizontalAngle": 0, "verticalAngle": 1.5708 },
                      "glazings": [
                        { "transmittanceF": 0.79, "reflectanceF": 0.07,
                          "transmittanceB": 0.79, "reflectanceB": 0.07, "resistance": 0.006 }
                      ] }
                  ] }
                """;
      var zone = JsonSerializer.Deserialize<Zone>(json, CreateOptions())!;

      var ctx = ZoneDeserializationContext.TryGet(zone);
      Assert.NotNull(ctx);
      Assert.Single(ctx!.Windows);
      Assert.Equal(2.0, ctx.Windows[0].Area);
    }

    [Fact]
    public void Read_EmptyWallsAndWindows_ContextHasEmptyLists()
    {
      const string json = """
                { "kind": "zone", "name": "X", "airMass": 100, "floorArea": 10,
                  "heatCapacity": 0, "moistureCapacity": 0,
                  "walls": [], "windows": [] }
                """;
      var zone = JsonSerializer.Deserialize<Zone>(json, CreateOptions())!;

      var ctx = ZoneDeserializationContext.TryGet(zone);
      Assert.NotNull(ctx);
      Assert.Empty(ctx!.WallReferences);
      Assert.Empty(ctx.Windows);
    }

    [Fact]
    public void Read_Context_CanBeClearedWithoutAffectingZone()
    {
      const string json = """
                { "kind": "zone", "name": "X", "airMass": 100, "floorArea": 10,
                  "heatCapacity": 0, "moistureCapacity": 0,
                  "walls": [{"wallId":1,"sideF":true}], "windows": [] }
                """;
      var zone = JsonSerializer.Deserialize<Zone>(json, CreateOptions())!;

      Assert.NotNull(ZoneDeserializationContext.TryGet(zone));
      ZoneDeserializationContext.Clear(zone);
      Assert.Null(ZoneDeserializationContext.TryGet(zone));

      // Zone 自体は健全
      Assert.Equal("X", zone.Name);
    }

    #endregion

    // ================================================================
    #region ラウンドトリップ

    [Fact]
    public void RoundTrip_MinimalZone_PreservesBasics()
    {
      var original = MakeMinimalZone();
      var json = JsonSerializer.Serialize(original, CreateOptions());
      var restored = JsonSerializer.Deserialize<Zone>(json, CreateOptions())!;

      Assert.Equal(original.Name, restored.Name);
      Assert.Equal(original.AirMass, restored.AirMass);
      Assert.Equal(original.FloorArea, restored.FloorArea);
      Assert.Equal(original.HeatCapacity, restored.HeatCapacity);
    }

    [Fact]
    public void RoundTrip_FullyConfiguredZone_PreservesAllFields()
    {
      var original = MakeFullyConfiguredZone();
      var json = JsonSerializer.Serialize(original, CreateOptions());
      var restored = JsonSerializer.Deserialize<Zone>(json, CreateOptions())!;

      Assert.Equal(original.Name, restored.Name);
      Assert.Equal(original.HeatCapacity, restored.HeatCapacity);
      Assert.Equal(original.MoistureCapacity, restored.MoistureCapacity);
      Assert.Equal(original.HeatingCapacity, restored.HeatingCapacity);
      Assert.Equal(original.CoolingCapacity, restored.CoolingCapacity);
      Assert.Equal(original.HumidifyingCapacity, restored.HumidifyingCapacity);
      Assert.Equal(original.DehumidifyingCapacity, restored.DehumidifyingCapacity);

      var origGain = (SimpleHeatGain)original.BaseHeatGain;
      var restGain = (SimpleHeatGain)restored.BaseHeatGain;
      Assert.Equal(origGain.ConvectiveHeatGain, restGain.ConvectiveHeatGain);
      Assert.Equal(origGain.RadiativeHeatGain, restGain.RadiativeHeatGain);
      Assert.Equal(origGain.MoistureGain, restGain.MoistureGain);
    }

    [Fact]
    public void RoundTrip_InfiniteCapacitiesPreserved()
    {
      var original = MakeMinimalZone();
      // 明示的に一部を有限にし、他を Infinity のまま
      original.HeatingCapacity = 2500;
      // CoolingCapacity は Infinity のまま

      var json = JsonSerializer.Serialize(original, CreateOptions());
      var restored = JsonSerializer.Deserialize<Zone>(json, CreateOptions())!;

      Assert.Equal(2500, restored.HeatingCapacity);
      Assert.True(double.IsPositiveInfinity(restored.CoolingCapacity));
    }

    #endregion

    // ================================================================
    #region エラー処理

    [Fact]
    public void Read_MissingKind_Throws()
    {
      const string json = """{ "name": "X", "airMass": 100, "floorArea": 10, "walls": [], "windows": [] }""";
      Assert.Throws<JsonException>(() =>
          JsonSerializer.Deserialize<Zone>(json, CreateOptions()));
    }

    [Fact]
    public void Read_WrongKind_Throws()
    {
      const string json = """
                { "kind": "wall", "name": "X", "airMass": 100, "floorArea": 10, "walls": [], "windows": [] }
                """;
      Assert.Throws<JsonException>(() =>
          JsonSerializer.Deserialize<Zone>(json, CreateOptions()));
    }

    [Fact]
    public void Read_MissingName_Throws()
    {
      const string json = """{ "kind": "zone", "airMass": 100, "floorArea": 10, "walls": [], "windows": [] }""";
      Assert.Throws<JsonException>(() =>
          JsonSerializer.Deserialize<Zone>(json, CreateOptions()));
    }

    [Fact]
    public void Read_MissingAirMass_Throws()
    {
      const string json = """{ "kind": "zone", "name": "X", "floorArea": 10, "walls": [], "windows": [] }""";
      Assert.Throws<JsonException>(() =>
          JsonSerializer.Deserialize<Zone>(json, CreateOptions()));
    }

    [Fact]
    public void Read_WallReferenceMissingWallId_Throws()
    {
      const string json = """
                { "kind": "zone", "name": "X", "airMass": 100, "floorArea": 10,
                  "heatCapacity": 0, "moistureCapacity": 0,
                  "walls": [ { "sideF": true } ], "windows": [] }
                """;
      Assert.Throws<JsonException>(() =>
          JsonSerializer.Deserialize<Zone>(json, CreateOptions()));
    }

    [Fact]
    public void Read_BaseHeatGainMissingField_Throws()
    {
      const string json = """
                { "kind": "zone", "name": "X", "airMass": 100, "floorArea": 10,
                  "heatCapacity": 0, "moistureCapacity": 0,
                  "baseHeatGain": { "convectiveHeatGain": 80, "radiativeHeatGain": 40 },
                  "walls": [], "windows": [] }
                """;
      Assert.Throws<JsonException>(() =>
          JsonSerializer.Deserialize<Zone>(json, CreateOptions()));
    }

    [Fact]
    public void Read_NotAnObject_Throws()
    {
      Assert.Throws<JsonException>(() =>
          JsonSerializer.Deserialize<Zone>("[1,2]", CreateOptions()));
    }

    #endregion
  }
}