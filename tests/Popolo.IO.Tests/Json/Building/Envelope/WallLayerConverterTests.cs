/* WallLayerConverterTests.cs
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

using Popolo.Core.Building.Envelope;
using Popolo.Core.Physics;
using Popolo.IO.Json.Building.Envelope;

namespace Popolo.IO.Tests.Json.Building.Envelope
{
  /// <summary>Unit tests for <see cref="WallLayerConverter"/>.</summary>
  public class WallLayerConverterTests
  {
    #region Helpers

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
    #region Serialization

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
    #region Deserialization

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
    #region Round trip

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
    #region Error handling

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

    // ================================================================
    #region Unsupported subclasses

    /// <summary>
    /// WallLayer 型として渡された PCMWallLayer / HorizontalAirChamber を
    /// 通常の "wallLayer" として黙って書き出さず、JsonException を投げることを確認する。
    /// </summary>
    [Fact]
    public void Write_UnsupportedSubclass_Throws()
    {
      var solid = new WallLayer("s", 0.2, 1500.0, 0.01);
      WallLayer pcm = new PCMWallLayer("PCM", 22.0, 24.0, 0.01, solid, solid, solid);
      WallLayer chamber = new HorizontalAirChamber("Chamber", 0.3, 0.9, 0.9);

      Assert.Throws<JsonException>(() => JsonSerializer.Serialize(pcm, CreateOptions()));
      Assert.Throws<JsonException>(() => JsonSerializer.Serialize(chamber, CreateOptions()));
    }

    #endregion
  }
}