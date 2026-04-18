/* SimpleShadingDeviceConverterTests.cs
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
using Popolo.IO.Json.Building.Envelope;

namespace Popolo.IO.Tests.Json.Building.Envelope
{
  /// <summary>Unit tests for <see cref="SimpleShadingDeviceConverter"/>.</summary>
  public class SimpleShadingDeviceConverterTests
  {
    #region ヘルパー

    private static JsonSerializerOptions CreateOptions()
    {
      var opts = new JsonSerializerOptions();
      opts.Converters.Add(new SimpleShadingDeviceConverter());
      return opts;
    }

    private static SimpleShadingDevice MakeDevice()
        => new SimpleShadingDevice(transmittance: 0.05, reflectance: 0.55);

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
    public void Write_ProducesExpectedJson()
    {
      var json = JsonSerializer.Serialize(MakeDevice(), CreateOptions());
      using var doc = JsonDocument.Parse(json);
      var root = doc.RootElement;

      Assert.Equal(3, CountProperties(root));
      Assert.Equal("simpleShadingDevice", root.GetProperty("kind").GetString());
      Assert.Equal(0.05, root.GetProperty("transmittance").GetDouble());
      Assert.Equal(0.55, root.GetProperty("reflectance").GetDouble());
    }

    #endregion

    // ================================================================
    #region デシリアライズ

    [Fact]
    public void Read_WellFormedJson_ProducesExpectedValues()
    {
      const string json = """
                { "kind": "simpleShadingDevice", "transmittance": 0.1, "reflectance": 0.3 }
                """;
      var dev = JsonSerializer.Deserialize<SimpleShadingDevice>(json, CreateOptions())!;
      Assert.Equal(0.1, dev.Transmittance);
      Assert.Equal(0.3, dev.Reflectance);
    }

    [Fact]
    public void Read_PropertyOrderIndependent()
    {
      const string json = """
                { "reflectance": 0.3, "transmittance": 0.1, "kind": "simpleShadingDevice" }
                """;
      var dev = JsonSerializer.Deserialize<SimpleShadingDevice>(json, CreateOptions())!;
      Assert.Equal(0.1, dev.Transmittance);
      Assert.Equal(0.3, dev.Reflectance);
    }

    [Fact]
    public void Read_UnknownProperties_Ignored()
    {
      const string json = """
                { "kind": "simpleShadingDevice", "transmittance": 0.1, "reflectance": 0.3, "futureField": "x" }
                """;
      var dev = JsonSerializer.Deserialize<SimpleShadingDevice>(json, CreateOptions())!;
      Assert.Equal(0.1, dev.Transmittance);
    }

    #endregion

    // ================================================================
    #region ラウンドトリップ

    [Fact]
    public void RoundTrip_PreservesValues()
    {
      var original = MakeDevice();
      var json = JsonSerializer.Serialize(original, CreateOptions());
      var restored = JsonSerializer.Deserialize<SimpleShadingDevice>(json, CreateOptions())!;

      Assert.Equal(original.Transmittance, restored.Transmittance);
      Assert.Equal(original.Reflectance, restored.Reflectance);
    }

    [Fact]
    public void RoundTrip_PreservesDerivedAbsorptance()
    {
      var original = MakeDevice();
      var json = JsonSerializer.Serialize(original, CreateOptions());
      var restored = JsonSerializer.Deserialize<SimpleShadingDevice>(json, CreateOptions())!;

      Assert.InRange(restored.Absorptance,
          original.Absorptance - 1e-12, original.Absorptance + 1e-12);
    }

    #endregion

    // ================================================================
    #region エラー処理

    [Fact]
    public void Read_MissingKind_Throws()
    {
      Assert.Throws<JsonException>(() =>
          JsonSerializer.Deserialize<SimpleShadingDevice>(
              """{ "transmittance": 0.1, "reflectance": 0.3 }""", CreateOptions()));
    }

    [Fact]
    public void Read_WrongKind_Throws()
    {
      Assert.Throws<JsonException>(() =>
          JsonSerializer.Deserialize<SimpleShadingDevice>(
              """{ "kind": "venetianBlind", "transmittance": 0.1, "reflectance": 0.3 }""", CreateOptions()));
    }

    [Fact]
    public void Read_MissingTransmittance_Throws()
    {
      Assert.Throws<JsonException>(() =>
          JsonSerializer.Deserialize<SimpleShadingDevice>(
              """{ "kind": "simpleShadingDevice", "reflectance": 0.3 }""", CreateOptions()));
    }

    [Fact]
    public void Read_MissingReflectance_Throws()
    {
      Assert.Throws<JsonException>(() =>
          JsonSerializer.Deserialize<SimpleShadingDevice>(
              """{ "kind": "simpleShadingDevice", "transmittance": 0.1 }""", CreateOptions()));
    }

    [Fact]
    public void Read_NotAnObject_Throws()
    {
      Assert.Throws<JsonException>(() =>
          JsonSerializer.Deserialize<SimpleShadingDevice>("[1,2]", CreateOptions()));
    }

    #endregion
  }
}