/* InclineConverterTests.cs
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

using Popolo.Core.Climate;
using Popolo.IO.Json.Climate;

namespace Popolo.IO.Tests.Json.Climate
{
  /// <summary>Unit tests for <see cref="InclineConverter"/>.</summary>
  public class InclineConverterTests
  {
    #region Helpers

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
    #region Serialization

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
    #region Deserialization

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
    #region Round trip

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
    #region Error handling

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