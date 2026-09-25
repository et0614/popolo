/* VenetianBlindConverterTests.cs
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
  /// <summary>Unit tests for <see cref="VenetianBlindConverter"/>.</summary>
  public class VenetianBlindConverterTests
  {
    #region Helpers

    private static JsonSerializerOptions CreateOptions()
    {
      var opts = new JsonSerializerOptions();
      opts.Converters.Add(new VenetianBlindConverter());
      return opts;
    }

    private static VenetianBlind MakeBlind(double slatAngle = 0.5)
    {
      var vb = new VenetianBlind(
          slatWidth: 25.0, slatSpan: 21.0,
          upsideTransmittance: 0.05, downsideTransmittance: 0.02,
          upsideReflectance: 0.60, downsideReflectance: 0.45);
      vb.SlatAngle = slatAngle;
      return vb;
    }

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
    public void Write_ProducesEightProperties()
    {
      var json = JsonSerializer.Serialize(MakeBlind(), CreateOptions());
      using var doc = JsonDocument.Parse(json);
      var root = doc.RootElement;

      Assert.Equal(8, CountProperties(root));
      Assert.Equal("venetianBlind", root.GetProperty("kind").GetString());
    }

    [Fact]
    public void Write_PreservesSlatAngle()
    {
      var json = JsonSerializer.Serialize(MakeBlind(0.7854), CreateOptions());
      using var doc = JsonDocument.Parse(json);
      Assert.InRange(doc.RootElement.GetProperty("slatAngle").GetDouble(), 0.7853, 0.7855);
    }

    #endregion

    // ================================================================
    #region Deserialization

    [Fact]
    public void Read_WellFormedJson_ProducesExpectedValues()
    {
      const string json = """
                {
                  "kind": "venetianBlind",
                  "slatAngle": 0.5,
                  "slatWidth": 25.0,
                  "slatSpan": 21.0,
                  "upsideTransmittance": 0.05,
                  "downsideTransmittance": 0.02,
                  "upsideReflectance": 0.60,
                  "downsideReflectance": 0.45
                }
                """;
      var vb = JsonSerializer.Deserialize<VenetianBlind>(json, CreateOptions())!;

      Assert.Equal(0.5, vb.SlatAngle);
      Assert.Equal(25.0, vb.SlatWidth);
      Assert.Equal(21.0, vb.SlatSpan);
      Assert.Equal(0.05, vb.UpsideTransmittance);
      Assert.Equal(0.02, vb.DownsideTransmittance);
      Assert.Equal(0.60, vb.UpsideReflectance);
      Assert.Equal(0.45, vb.DownsideReflectance);
    }

    [Fact]
    public void Read_UnknownProperties_Ignored()
    {
      const string json = """
                {
                  "kind": "venetianBlind",
                  "slatAngle": 0, "slatWidth": 25, "slatSpan": 21,
                  "upsideTransmittance": 0, "downsideTransmittance": 0,
                  "upsideReflectance": 0.6, "downsideReflectance": 0.6,
                  "futureField": "x"
                }
                """;
      var vb = JsonSerializer.Deserialize<VenetianBlind>(json, CreateOptions())!;
      Assert.Equal(25.0, vb.SlatWidth);
    }

    #endregion

    // ================================================================
    #region Round trip

    [Fact]
    public void RoundTrip_PreservesAllFields()
    {
      var original = MakeBlind(0.7);
      var json = JsonSerializer.Serialize(original, CreateOptions());
      var restored = JsonSerializer.Deserialize<VenetianBlind>(json, CreateOptions())!;

      Assert.Equal(original.SlatAngle, restored.SlatAngle);
      Assert.Equal(original.SlatWidth, restored.SlatWidth);
      Assert.Equal(original.SlatSpan, restored.SlatSpan);
      Assert.Equal(original.UpsideTransmittance, restored.UpsideTransmittance);
      Assert.Equal(original.DownsideTransmittance, restored.DownsideTransmittance);
      Assert.Equal(original.UpsideReflectance, restored.UpsideReflectance);
      Assert.Equal(original.DownsideReflectance, restored.DownsideReflectance);
    }

    #endregion

    // ================================================================
    #region Error handling

    [Fact]
    public void Read_MissingKind_Throws()
    {
      const string json = """
                {
                  "slatAngle": 0, "slatWidth": 25, "slatSpan": 21,
                  "upsideTransmittance": 0, "downsideTransmittance": 0,
                  "upsideReflectance": 0.6, "downsideReflectance": 0.6
                }
                """;
      Assert.Throws<JsonException>(() =>
          JsonSerializer.Deserialize<VenetianBlind>(json, CreateOptions()));
    }

    [Fact]
    public void Read_WrongKind_Throws()
    {
      const string json = """
                {
                  "kind": "simpleShadingDevice",
                  "slatAngle": 0, "slatWidth": 25, "slatSpan": 21,
                  "upsideTransmittance": 0, "downsideTransmittance": 0,
                  "upsideReflectance": 0.6, "downsideReflectance": 0.6
                }
                """;
      Assert.Throws<JsonException>(() =>
          JsonSerializer.Deserialize<VenetianBlind>(json, CreateOptions()));
    }

    [Fact]
    public void Read_MissingSlatAngle_Throws()
    {
      const string json = """
                {
                  "kind": "venetianBlind",
                  "slatWidth": 25, "slatSpan": 21,
                  "upsideTransmittance": 0, "downsideTransmittance": 0,
                  "upsideReflectance": 0.6, "downsideReflectance": 0.6
                }
                """;
      Assert.Throws<JsonException>(() =>
          JsonSerializer.Deserialize<VenetianBlind>(json, CreateOptions()));
    }

    [Fact]
    public void Read_MissingSlatWidth_Throws()
    {
      const string json = """
                {
                  "kind": "venetianBlind",
                  "slatAngle": 0, "slatSpan": 21,
                  "upsideTransmittance": 0, "downsideTransmittance": 0,
                  "upsideReflectance": 0.6, "downsideReflectance": 0.6
                }
                """;
      Assert.Throws<JsonException>(() =>
          JsonSerializer.Deserialize<VenetianBlind>(json, CreateOptions()));
    }

    [Fact]
    public void Read_NotAnObject_Throws()
    {
      Assert.Throws<JsonException>(() =>
          JsonSerializer.Deserialize<VenetianBlind>("[1]", CreateOptions()));
    }

    #endregion
  }
}