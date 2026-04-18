/* InclineConverterTests.cs
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

using Popolo.Core.Climate;
using Popolo.IO.Json.Climate;

namespace Popolo.IO.Tests.Json.Climate
{
  /// <summary>Unit tests for <see cref="InclineConverter"/>.</summary>
  public class InclineConverterTests
  {
    #region ヘルパー

    private static JsonSerializerOptions CreateOptions()
    {
      var opts = new JsonSerializerOptions();
      opts.Converters.Add(new InclineConverter());
      return opts;
    }

    private static Incline MakeSouthVertical()
        => new Incline(horizontalAngle: 0.0, verticalAngle: Math.PI / 2);

    private static Incline MakeTiltedEast30()
        => new Incline(horizontalAngle: -Math.PI / 6, verticalAngle: Math.PI / 4);

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
    public void Write_ProducesThreeProperties()
    {
      var json = JsonSerializer.Serialize(MakeSouthVertical(), CreateOptions());
      using var doc = JsonDocument.Parse(json);
      var root = doc.RootElement;

      Assert.Equal(3, CountProperties(root));
      Assert.Equal("incline", root.GetProperty("kind").GetString());
      Assert.InRange(root.GetProperty("horizontalAngle").GetDouble(), -1e-9, 1e-9);
      Assert.InRange(root.GetProperty("verticalAngle").GetDouble(),
          Math.PI / 2 - 1e-9, Math.PI / 2 + 1e-9);
    }

    [Fact]
    public void Write_NegativeAzimuth_Preserved()
    {
      var json = JsonSerializer.Serialize(MakeTiltedEast30(), CreateOptions());
      using var doc = JsonDocument.Parse(json);
      Assert.True(doc.RootElement.GetProperty("horizontalAngle").GetDouble() < 0);
    }

    #endregion

    // ================================================================
    #region デシリアライズ

    [Fact]
    public void Read_WellFormedJson_ProducesExpectedValues()
    {
      const string json = """
                { "kind": "incline", "horizontalAngle": 0.0, "verticalAngle": 1.5707963267948966 }
                """;
      var inc = JsonSerializer.Deserialize<Incline>(json, CreateOptions())!;
      Assert.InRange(inc.HorizontalAngle, -1e-9, 1e-9);
      Assert.InRange(inc.VerticalAngle, Math.PI / 2 - 1e-9, Math.PI / 2 + 1e-9);
    }

    [Fact]
    public void Read_PropertyOrderIndependent()
    {
      const string json = """
                { "verticalAngle": 0.5, "kind": "incline", "horizontalAngle": 0.3 }
                """;
      var inc = JsonSerializer.Deserialize<Incline>(json, CreateOptions())!;
      Assert.Equal(0.3, inc.HorizontalAngle);
      Assert.Equal(0.5, inc.VerticalAngle);
    }

    [Fact]
    public void Read_UnknownProperties_Ignored()
    {
      const string json = """
                { "kind": "incline", "horizontalAngle": 0.3, "verticalAngle": 0.5, "futureField": "x" }
                """;
      var inc = JsonSerializer.Deserialize<Incline>(json, CreateOptions())!;
      Assert.Equal(0.3, inc.HorizontalAngle);
    }

    #endregion

    // ================================================================
    #region ラウンドトリップ

    [Fact]
    public void RoundTrip_PreservesBothAngles()
    {
      var original = MakeTiltedEast30();
      var json = JsonSerializer.Serialize(original, CreateOptions());
      var restored = JsonSerializer.Deserialize<Incline>(json, CreateOptions())!;

      Assert.InRange(restored.HorizontalAngle,
          original.HorizontalAngle - 1e-12, original.HorizontalAngle + 1e-12);
      Assert.InRange(restored.VerticalAngle,
          original.VerticalAngle - 1e-12, original.VerticalAngle + 1e-12);
    }

    [Fact]
    public void RoundTrip_PreservesDerivedConfigurationFactor()
    {
      var original = MakeTiltedEast30();
      var json = JsonSerializer.Serialize(original, CreateOptions());
      var restored = JsonSerializer.Deserialize<Incline>(json, CreateOptions())!;

      Assert.InRange(restored.ConfigurationFactorToSky,
          original.ConfigurationFactorToSky - 1e-9,
          original.ConfigurationFactorToSky + 1e-9);
    }

    #endregion

    // ================================================================
    #region エラー処理

    [Fact]
    public void Read_MissingKind_Throws()
    {
      Assert.Throws<JsonException>(() =>
          JsonSerializer.Deserialize<Incline>(
              """{"horizontalAngle":0.0,"verticalAngle":1.57}""", CreateOptions()));
    }

    [Fact]
    public void Read_WrongKind_Throws()
    {
      Assert.Throws<JsonException>(() =>
          JsonSerializer.Deserialize<Incline>(
              """{"kind":"sun","horizontalAngle":0.0,"verticalAngle":1.57}""", CreateOptions()));
    }

    [Fact]
    public void Read_MissingHorizontalAngle_Throws()
    {
      Assert.Throws<JsonException>(() =>
          JsonSerializer.Deserialize<Incline>(
              """{"kind":"incline","verticalAngle":1.57}""", CreateOptions()));
    }

    [Fact]
    public void Read_MissingVerticalAngle_Throws()
    {
      Assert.Throws<JsonException>(() =>
          JsonSerializer.Deserialize<Incline>(
              """{"kind":"incline","horizontalAngle":0.0}""", CreateOptions()));
    }

    [Fact]
    public void Read_NotAnObject_Throws()
    {
      Assert.Throws<JsonException>(() =>
          JsonSerializer.Deserialize<Incline>("[0,1.57]", CreateOptions()));
    }

    #endregion
  }
}