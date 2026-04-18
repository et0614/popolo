/* WallLayerConverterTests.cs
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
using Popolo.Core.Physics;
using Popolo.IO.Json.Building.Envelope;

namespace Popolo.IO.Tests.Json.Building.Envelope
{
  /// <summary>Unit tests for <see cref="WallLayerConverter"/>.</summary>
  public class WallLayerConverterTests
  {
    #region ヘルパー

    private static JsonSerializerOptions CreateOptions()
    {
      var opts = new JsonSerializerOptions();
      opts.Converters.Add(new WallLayerConverter());
      return opts;
    }

    private static WallLayer MakeConcreteLayer()
        => new WallLayer("Concrete", 1.4, 1934.0, 0.15);

    private static WallLayer MakePlywoodLayer()
        => new WallLayer("Plywood", 0.15, 720.0,
            moistureConductivity: 2.0e-10,
            voidage: 0.15,
            kappa: 0.1,
            nu: 0.01,
            thickness: 0.012);

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
    public void Write_DryLayer_HasFiveFieldsNoMoistureProps()
    {
      var json = JsonSerializer.Serialize(MakeConcreteLayer(), CreateOptions());
      using var doc = JsonDocument.Parse(json);
      var root = doc.RootElement;

      Assert.Equal(5, CountProperties(root));
      Assert.Equal("wallLayer", root.GetProperty("kind").GetString());
      Assert.Equal("Concrete", root.GetProperty("name").GetString());
      Assert.Equal(1.4, root.GetProperty("thermalConductivity").GetDouble());
      Assert.Equal(1934.0, root.GetProperty("volSpecificHeat").GetDouble());
      Assert.Equal(0.15, root.GetProperty("thickness").GetDouble());
      Assert.False(root.TryGetProperty("moistureProperties", out _));
    }

    [Fact]
    public void Write_MoistLayer_IncludesMoistureProperties()
    {
      var json = JsonSerializer.Serialize(MakePlywoodLayer(), CreateOptions());
      using var doc = JsonDocument.Parse(json);
      var root = doc.RootElement;

      Assert.Equal(6, CountProperties(root));
      Assert.True(root.TryGetProperty("moistureProperties", out var mp));
      Assert.Equal(JsonValueKind.Object, mp.ValueKind);
      Assert.Equal(4, CountProperties(mp));
      Assert.InRange(mp.GetProperty("conductivity").GetDouble(), 1.99e-10, 2.01e-10);
      Assert.InRange(mp.GetProperty("voidage").GetDouble(), 0.149, 0.151);
      Assert.InRange(mp.GetProperty("kappa").GetDouble(), 0.099, 0.101);
      Assert.InRange(mp.GetProperty("nu").GetDouble(), 0.0099, 0.0101);
    }

    [Fact]
    public void Write_MoistLayer_VoidageKappaNuRestoreInputUnits()
    {
      // Core 内部は半層集約形で保持している。出力は元の per-layer 単位に戻る。
      var layer = new WallLayer("Test", 0.2, 1000, 5.0e-10,
          voidage: 0.3, kappa: 0.05, nu: 0.005, thickness: 0.04);
      var json = JsonSerializer.Serialize(layer, CreateOptions());
      using var doc = JsonDocument.Parse(json);
      var mp = doc.RootElement.GetProperty("moistureProperties");

      Assert.InRange(mp.GetProperty("voidage").GetDouble(), 0.299, 0.301);
      Assert.InRange(mp.GetProperty("kappa").GetDouble(), 0.0499, 0.0501);
      Assert.InRange(mp.GetProperty("nu").GetDouble(), 0.00499, 0.00501);
    }

    #endregion

    // ================================================================
    #region デシリアライズ

    [Fact]
    public void Read_DryLayer_ProducesExpectedValues()
    {
      const string json = """
                {
                  "kind": "wallLayer",
                  "name": "Concrete",
                  "thermalConductivity": 1.4,
                  "volSpecificHeat": 1934.0,
                  "thickness": 0.15
                }
                """;
      var layer = JsonSerializer.Deserialize<WallLayer>(json, CreateOptions())!;
      Assert.Equal("Concrete", layer.Name);
      Assert.Equal(1.4, layer.ThermalConductivity);
      Assert.Equal(1934.0, layer.VolSpecificHeat);
      Assert.Equal(0.15, layer.Thickness);
      Assert.Equal(0.0, layer.MoistureConductivity);
    }

    [Fact]
    public void Read_MoistLayer_ProducesExpectedValues()
    {
      const string json = """
                {
                  "kind": "wallLayer",
                  "name": "Plywood",
                  "thermalConductivity": 0.15,
                  "volSpecificHeat": 720.0,
                  "thickness": 0.012,
                  "moistureProperties": {
                    "conductivity": 2.0e-10,
                    "voidage": 0.15,
                    "kappa": 0.1,
                    "nu": 0.01
                  }
                }
                """;
      var layer = JsonSerializer.Deserialize<WallLayer>(json, CreateOptions())!;
      Assert.Equal("Plywood", layer.Name);
      Assert.InRange(layer.MoistureConductivity, 1.99e-10, 2.01e-10);

      // WaterCapacity = 0.5 * voidage * thickness * ρ
      double expectedWC = 0.5 * 0.15 * 0.012 * PhysicsConstants.NominalMoistAirDensity;
      Assert.InRange(layer.WaterCapacity, expectedWC * 0.999, expectedWC * 1.001);
    }

    [Fact]
    public void Read_PropertyOrderIndependent()
    {
      const string json = """
                {
                  "thickness": 0.15, "volSpecificHeat": 1000,
                  "name": "X", "thermalConductivity": 1.0, "kind": "wallLayer"
                }
                """;
      var layer = JsonSerializer.Deserialize<WallLayer>(json, CreateOptions())!;
      Assert.Equal("X", layer.Name);
      Assert.Equal(0.15, layer.Thickness);
    }

    [Fact]
    public void Read_UnknownProperties_Ignored()
    {
      const string json = """
                {
                  "kind": "wallLayer",
                  "name": "L", "thermalConductivity": 1.0, "volSpecificHeat": 1000, "thickness": 0.1,
                  "futureField": 42, "nested": {"x": 1}
                }
                """;
      var layer = JsonSerializer.Deserialize<WallLayer>(json, CreateOptions())!;
      Assert.Equal("L", layer.Name);
    }

    #endregion

    // ================================================================
    #region ラウンドトリップ

    [Fact]
    public void RoundTrip_DryLayer_PreservesAllFields()
    {
      var original = MakeConcreteLayer();
      var json = JsonSerializer.Serialize(original, CreateOptions());
      var restored = JsonSerializer.Deserialize<WallLayer>(json, CreateOptions())!;

      Assert.Equal(original.Name, restored.Name);
      Assert.Equal(original.ThermalConductivity, restored.ThermalConductivity);
      Assert.Equal(original.VolSpecificHeat, restored.VolSpecificHeat);
      Assert.Equal(original.Thickness, restored.Thickness);
      Assert.Equal(original.MoistureConductivity, restored.MoistureConductivity);
    }

    [Fact]
    public void RoundTrip_MoistLayer_PreservesAllFields()
    {
      var original = MakePlywoodLayer();
      var json = JsonSerializer.Serialize(original, CreateOptions());
      var restored = JsonSerializer.Deserialize<WallLayer>(json, CreateOptions())!;

      Assert.Equal(original.Name, restored.Name);
      Assert.InRange(restored.MoistureConductivity,
          original.MoistureConductivity * 0.9999, original.MoistureConductivity * 1.0001);
      Assert.InRange(restored.WaterCapacity,
          original.WaterCapacity * 0.9999, original.WaterCapacity * 1.0001);
      Assert.InRange(restored.KappaC,
          original.KappaC * 0.9999, original.KappaC * 1.0001);
      Assert.InRange(restored.NuC,
          original.NuC * 0.9999, original.NuC * 1.0001);
    }

    #endregion

    // ================================================================
    #region エラー処理

    [Fact]
    public void Read_MissingKind_Throws()
    {
      const string json = """{"name":"L","thermalConductivity":1.0,"volSpecificHeat":1000,"thickness":0.1}""";
      Assert.Throws<JsonException>(() =>
          JsonSerializer.Deserialize<WallLayer>(json, CreateOptions()));
    }

    [Fact]
    public void Read_WrongKind_Throws()
    {
      const string json = """
                {"kind":"airGapLayer","name":"L","thermalConductivity":1.0,"volSpecificHeat":1000,"thickness":0.1}
                """;
      Assert.Throws<JsonException>(() =>
          JsonSerializer.Deserialize<WallLayer>(json, CreateOptions()));
    }

    [Fact]
    public void Read_MissingName_Throws()
    {
      const string json = """
                {"kind":"wallLayer","thermalConductivity":1.0,"volSpecificHeat":1000,"thickness":0.1}
                """;
      Assert.Throws<JsonException>(() =>
          JsonSerializer.Deserialize<WallLayer>(json, CreateOptions()));
    }

    [Fact]
    public void Read_MissingThickness_Throws()
    {
      const string json = """
                {"kind":"wallLayer","name":"L","thermalConductivity":1.0,"volSpecificHeat":1000}
                """;
      Assert.Throws<JsonException>(() =>
          JsonSerializer.Deserialize<WallLayer>(json, CreateOptions()));
    }

    [Fact]
    public void Read_MoisturePropertiesIncomplete_Throws()
    {
      // moistureProperties があるのに conductivity が欠落
      const string json = """
                {
                  "kind":"wallLayer", "name":"L",
                  "thermalConductivity":1.0, "volSpecificHeat":1000, "thickness":0.1,
                  "moistureProperties": { "voidage": 0.1, "kappa": 0.05, "nu": 0.005 }
                }
                """;
      Assert.Throws<JsonException>(() =>
          JsonSerializer.Deserialize<WallLayer>(json, CreateOptions()));
    }

    [Fact]
    public void Read_NotAnObject_Throws()
    {
      Assert.Throws<JsonException>(() =>
          JsonSerializer.Deserialize<WallLayer>("[1,2,3]", CreateOptions()));
    }

    #endregion
  }
}