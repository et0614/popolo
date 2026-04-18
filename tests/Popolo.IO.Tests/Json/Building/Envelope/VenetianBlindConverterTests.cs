/* VenetianBlindConverterTests.cs
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
  /// <summary>Unit tests for <see cref="VenetianBlindConverter"/>.</summary>
  public class VenetianBlindConverterTests
  {
    #region ヘルパー

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
    #region シリアライズ

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
    #region デシリアライズ

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
    #region ラウンドトリップ

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
    #region エラー処理

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