/* AirGapLayerConverterTests.cs
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
using Popolo.IO.Json.Building.Envelope;

namespace Popolo.IO.Tests.Json.Building.Envelope
{
  /// <summary>Unit tests for <see cref="AirGapLayerConverter"/>.</summary>
  public class AirGapLayerConverterTests
  {
    #region Helpers

    private static JsonSerializerOptions CreateOptions()
    {
      var opts = new JsonSerializerOptions();
      opts.Converters.Add(new AirGapLayerConverter());
      return opts;
    }

    private static AirGapLayer MakeSealedLayer()
        => new AirGapLayer("Sealed Air Gap Layer", isSealed: true, thickness: 0.02);

    private static AirGapLayer MakeVentilatedLayer()
        => new AirGapLayer("Air Gap Layer", isSealed: false, thickness: 0.01);

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
    public void Write_ProducesExpectedJson()
    {
      var json = JsonSerializer.Serialize(MakeSealedLayer(), CreateOptions());
      using var doc = JsonDocument.Parse(json);
      var root = doc.RootElement;

      Assert.Equal(4, CountProperties(root));
      Assert.Equal("airGapLayer", root.GetProperty("kind").GetString());
      Assert.Equal("Sealed Air Gap Layer", root.GetProperty("name").GetString());
      Assert.True(root.GetProperty("isSealed").GetBoolean());
      Assert.Equal(0.02, root.GetProperty("thickness").GetDouble());
    }

    [Fact]
    public void Write_KindComesFromCoreOverride()
    {
      // AirGapLayer.Kind は override で "airGapLayer" を返す。
      // 具象型のプロパティが正しく使われているかを確認。
      var json = JsonSerializer.Serialize(MakeSealedLayer(), CreateOptions());
      using var doc = JsonDocument.Parse(json);
      Assert.Equal("airGapLayer", doc.RootElement.GetProperty("kind").GetString());
    }

    [Fact]
    public void Write_VentilatedLayer_IsSealedIsFalse()
    {
      var json = JsonSerializer.Serialize(MakeVentilatedLayer(), CreateOptions());
      using var doc = JsonDocument.Parse(json);
      Assert.False(doc.RootElement.GetProperty("isSealed").GetBoolean());
    }

    [Fact]
    public void Write_UnicodeName_Preserved()
    {
      var layer = new AirGapLayer("密閉空気層①", isSealed: true, thickness: 0.03);
      var json = JsonSerializer.Serialize(layer, CreateOptions());
      using var doc = JsonDocument.Parse(json);
      Assert.Equal("密閉空気層①", doc.RootElement.GetProperty("name").GetString());
    }

    #endregion

    // ================================================================
    #region Deserialization

    [Fact]
    public void Read_WellFormedJson_ProducesExpectedLayer()
    {
      const string json = """
                { "kind": "airGapLayer", "name": "Sealed Air Gap Layer", "isSealed": true, "thickness": 0.02 }
                """;
      var layer = JsonSerializer.Deserialize<AirGapLayer>(json, CreateOptions())!;
      Assert.Equal("Sealed Air Gap Layer", layer.Name);
      Assert.True(layer.IsSealed);
      Assert.Equal(0.02, layer.Thickness);
    }

    [Fact]
    public void Read_PropertyOrderIndependent()
    {
      const string json = """
                { "thickness": 0.015, "isSealed": false, "name": "X", "kind": "airGapLayer" }
                """;
      var layer = JsonSerializer.Deserialize<AirGapLayer>(json, CreateOptions())!;
      Assert.Equal("X", layer.Name);
      Assert.False(layer.IsSealed);
      Assert.Equal(0.015, layer.Thickness);
    }

    [Fact]
    public void Read_UnknownProperties_Ignored()
    {
      const string json = """
                {
                  "kind": "airGapLayer", "name": "L", "isSealed": true, "thickness": 0.02,
                  "futureField": 42, "nestedUnknown": { "x": [1, 2, 3] }
                }
                """;
      var layer = JsonSerializer.Deserialize<AirGapLayer>(json, CreateOptions())!;
      Assert.Equal("L", layer.Name);
    }

    #endregion

    // ================================================================
    #region Round trip

    [Fact]
    public void RoundTrip_SealedLayer_PreservesFields()
    {
      var original = MakeSealedLayer();
      var json = JsonSerializer.Serialize(original, CreateOptions());
      var restored = JsonSerializer.Deserialize<AirGapLayer>(json, CreateOptions())!;

      Assert.Equal(original.Name, restored.Name);
      Assert.Equal(original.IsSealed, restored.IsSealed);
      Assert.Equal(original.Thickness, restored.Thickness);
    }

    [Fact]
    public void RoundTrip_VentilatedLayer_PreservesFields()
    {
      var original = MakeVentilatedLayer();
      var json = JsonSerializer.Serialize(original, CreateOptions());
      var restored = JsonSerializer.Deserialize<AirGapLayer>(json, CreateOptions())!;

      Assert.Equal(original.Name, restored.Name);
      Assert.Equal(original.IsSealed, restored.IsSealed);
      Assert.Equal(original.Thickness, restored.Thickness);
    }

    [Fact]
    public void RoundTrip_RestoresStandardHeatConductance()
    {
      var sealedLayer = MakeSealedLayer();
      var restoredSealed = JsonSerializer.Deserialize<AirGapLayer>(
          JsonSerializer.Serialize(sealedLayer, CreateOptions()), CreateOptions())!;
      Assert.InRange(restoredSealed.HeatConductance, 1.0 / 0.1501, 1.0 / 0.1499);

      var ventLayer = MakeVentilatedLayer();
      var restoredVent = JsonSerializer.Deserialize<AirGapLayer>(
          JsonSerializer.Serialize(ventLayer, CreateOptions()), CreateOptions())!;
      Assert.InRange(restoredVent.HeatConductance, 1.0 / 0.0701, 1.0 / 0.0699);
    }

    #endregion

    // ================================================================
    #region Error handling

    [Fact]
    public void Read_MissingKind_Throws()
    {
      const string json = """{ "name": "L", "isSealed": true, "thickness": 0.02 }""";
      Assert.Throws<JsonException>(() =>
          JsonSerializer.Deserialize<AirGapLayer>(json, CreateOptions()));
    }

    [Fact]
    public void Read_WrongKind_Throws()
    {
      const string json = """
                { "kind": "wallLayer", "name": "L", "isSealed": true, "thickness": 0.02 }
                """;
      Assert.Throws<JsonException>(() =>
          JsonSerializer.Deserialize<AirGapLayer>(json, CreateOptions()));
    }

    [Fact]
    public void Read_MissingName_Throws()
    {
      const string json = """{ "kind": "airGapLayer", "isSealed": true, "thickness": 0.02 }""";
      Assert.Throws<JsonException>(() =>
          JsonSerializer.Deserialize<AirGapLayer>(json, CreateOptions()));
    }

    [Fact]
    public void Read_MissingIsSealed_Throws()
    {
      const string json = """{ "kind": "airGapLayer", "name": "L", "thickness": 0.02 }""";
      Assert.Throws<JsonException>(() =>
          JsonSerializer.Deserialize<AirGapLayer>(json, CreateOptions()));
    }

    [Fact]
    public void Read_MissingThickness_Throws()
    {
      const string json = """{ "kind": "airGapLayer", "name": "L", "isSealed": true }""";
      Assert.Throws<JsonException>(() =>
          JsonSerializer.Deserialize<AirGapLayer>(json, CreateOptions()));
    }

    [Fact]
    public void Read_NotAnObject_Throws()
    {
      Assert.Throws<JsonException>(() =>
          JsonSerializer.Deserialize<AirGapLayer>("[1,2,3]", CreateOptions()));
    }

    #endregion
  }
}