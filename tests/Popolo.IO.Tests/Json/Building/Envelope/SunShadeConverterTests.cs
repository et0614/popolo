/* SunShadeConverterTests.cs
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
using Popolo.Core.Climate;
using Popolo.IO.Json.Climate;
using Popolo.IO.Json.Building.Envelope;

namespace Popolo.IO.Tests.Json.Building.Envelope
{
  /// <summary>Unit tests for <see cref="SunShadeConverter"/>.</summary>
  /// <remarks>
  /// Tests all 10 values of <see cref="SunShade.ShapeType"/>:
  /// None, Horizontal, LongHorizontal, VerticalLeft, VerticalRight,
  /// LongVerticalLeft, LongVerticalRight, VerticalBoth, LongVerticalBoth, Grid.
  /// </remarks>
  public class SunShadeConverterTests
  {
    #region ヘルパー

    /// <summary>
    /// Options with <see cref="SunShadeConverter"/> and <see cref="InclineConverter"/>.
    /// </summary>
    private static JsonSerializerOptions CreateOptions()
    {
      var opts = new JsonSerializerOptions();
      opts.Converters.Add(new SunShadeConverter());
      opts.Converters.Add(new InclineConverter());
      return opts;
    }

    private static Incline SouthVerticalIncline()
        => new Incline(horizontalAngle: 0.0, verticalAngle: Math.PI / 2);

    private const double W = 1.5, H = 1.8, D = 0.6;

    private static SunShade Roundtrip(SunShade original)
    {
      var opts = CreateOptions();
      var json = JsonSerializer.Serialize(original, opts);
      return JsonSerializer.Deserialize<SunShade>(json, opts)!;
    }

    private static int CountProperties(JsonElement obj)
    {
      int count = 0;
      foreach (var _ in obj.EnumerateObject()) count++;
      return count;
    }

    #endregion

    // ================================================================
    #region None

    [Fact]
    public void Write_None_HasOnlyKindAndShape()
    {
      var original = SunShade.MakeEmptySunShade();
      var json = JsonSerializer.Serialize(original, CreateOptions());

      using var doc = JsonDocument.Parse(json);
      var root = doc.RootElement;
      Assert.Equal(2, CountProperties(root));
      Assert.Equal("sunShade", root.GetProperty("kind").GetString());
      Assert.Equal("None", root.GetProperty("shape").GetString());
    }

    [Fact]
    public void Read_None_DoesNotRequireIncline()
    {
      const string json = """{ "kind": "sunShade", "shape": "None" }""";
      var ss = JsonSerializer.Deserialize<SunShade>(json, CreateOptions())!;
      Assert.Equal(SunShade.ShapeType.None, ss.Shape);
    }

    [Fact]
    public void RoundTrip_None_PreservesShape()
    {
      var restored = Roundtrip(SunShade.MakeEmptySunShade());
      Assert.Equal(SunShade.ShapeType.None, restored.Shape);
    }

    #endregion

    // ================================================================
    #region Horizontal / LongHorizontal

    [Fact]
    public void RoundTrip_LongHorizontal_PreservesFields()
    {
      var original = SunShade.MakeHorizontalSunShade(W, H, D,
          tMargin: 0.2, SouthVerticalIncline());
      var restored = Roundtrip(original);

      Assert.Equal(SunShade.ShapeType.LongHorizontal, restored.Shape);
      Assert.Equal(W, restored.WinWidth);
      Assert.Equal(H, restored.WinHeight);
      Assert.Equal(D, restored.Overhang);
      Assert.Equal(0.2, restored.TopMargin);
    }

    [Fact]
    public void RoundTrip_Horizontal_PreservesAllMargins()
    {
      var original = SunShade.MakeHorizontalSunShade(W, H, D,
          lMargin: 0.1, rMargin: 0.15, tMargin: 0.2, SouthVerticalIncline());
      var restored = Roundtrip(original);

      Assert.Equal(SunShade.ShapeType.Horizontal, restored.Shape);
      Assert.Equal(0.1, restored.LeftMargin);
      Assert.Equal(0.15, restored.RightMargin);
      Assert.Equal(0.2, restored.TopMargin);
    }

    #endregion

    // ================================================================
    #region Vertical 系

    [Fact]
    public void RoundTrip_LongVerticalLeft_PreservesFields()
    {
      var original = SunShade.MakeVerticalSunShade(W, H, D,
          sMargin: 0.12, isLeftSide: true, SouthVerticalIncline());
      var restored = Roundtrip(original);

      Assert.Equal(SunShade.ShapeType.LongVerticalLeft, restored.Shape);
      Assert.Equal(0.12, restored.LeftMargin);
    }

    [Fact]
    public void RoundTrip_LongVerticalRight_PreservesFields()
    {
      var original = SunShade.MakeVerticalSunShade(W, H, D,
          sMargin: 0.08, isLeftSide: false, SouthVerticalIncline());
      var restored = Roundtrip(original);

      Assert.Equal(SunShade.ShapeType.LongVerticalRight, restored.Shape);
      Assert.Equal(0.08, restored.RightMargin);
    }

    [Fact]
    public void RoundTrip_VerticalLeft_PreservesAllMargins()
    {
      var original = SunShade.MakeVerticalSunShade(W, H, D,
          sMargin: 0.10, isLeftSide: true,
          tMargin: 0.05, bMargin: 0.07, SouthVerticalIncline());
      var restored = Roundtrip(original);

      Assert.Equal(SunShade.ShapeType.VerticalLeft, restored.Shape);
      Assert.Equal(0.10, restored.LeftMargin);
      Assert.Equal(0.05, restored.TopMargin);
      Assert.Equal(0.07, restored.BottomMargin);
    }

    [Fact]
    public void RoundTrip_VerticalRight_PreservesAllMargins()
    {
      var original = SunShade.MakeVerticalSunShade(W, H, D,
          sMargin: 0.10, isLeftSide: false,
          tMargin: 0.05, bMargin: 0.07, SouthVerticalIncline());
      var restored = Roundtrip(original);

      Assert.Equal(SunShade.ShapeType.VerticalRight, restored.Shape);
      Assert.Equal(0.10, restored.RightMargin);
    }

    [Fact]
    public void RoundTrip_LongVerticalBoth_PreservesFields()
    {
      var original = SunShade.MakeVerticalSunShade(W, H, D,
          lMargin: 0.12, rMargin: 0.13, SouthVerticalIncline());
      var restored = Roundtrip(original);

      Assert.Equal(SunShade.ShapeType.LongVerticalBoth, restored.Shape);
      Assert.Equal(0.12, restored.LeftMargin);
      Assert.Equal(0.13, restored.RightMargin);
    }

    [Fact]
    public void RoundTrip_VerticalBoth_PreservesAllMargins()
    {
      var original = SunShade.MakeVerticalSunShade(W, H, D,
          lMargin: 0.10, rMargin: 0.11,
          tMargin: 0.05, bMargin: 0.07, SouthVerticalIncline());
      var restored = Roundtrip(original);

      Assert.Equal(SunShade.ShapeType.VerticalBoth, restored.Shape);
      Assert.Equal(0.05, restored.TopMargin);
      Assert.Equal(0.07, restored.BottomMargin);
    }

    #endregion

    // ================================================================
    #region Grid

    [Fact]
    public void RoundTrip_Grid_PreservesAllMargins()
    {
      var original = SunShade.MakeGridSunShade(W, H, D,
          lMargin: 0.1, rMargin: 0.1, tMargin: 0.2, bMargin: 0.2,
          SouthVerticalIncline());
      var restored = Roundtrip(original);

      Assert.Equal(SunShade.ShapeType.Grid, restored.Shape);
      Assert.Equal(0.1, restored.LeftMargin);
      Assert.Equal(0.2, restored.TopMargin);
    }

    #endregion

    // ================================================================
    #region 直接 public コンストラクタ経由のラウンドトリップ

    /// <summary>
    /// 9 引数コンストラクタ(public)で作った SunShade がラウンドトリップで保存されることを確認。
    /// Popolo.Core の SunShade.cs の remarks 記載通り、このコンストラクタはシリアライズ用途で使える。
    /// </summary>
    [Fact]
    public void RoundTrip_Direct9ArgConstructor_PreservesAllMargins()
    {
      var original = new SunShade(
          SunShade.ShapeType.VerticalBoth, SouthVerticalIncline(),
          winHeight: 1.8, winWidth: 1.5, overhang: 0.6,
          topMargin: 0.05, bottomMargin: 0.07,
          leftMargin: 0.10, rightMargin: 0.11);
      var restored = Roundtrip(original);

      Assert.Equal(SunShade.ShapeType.VerticalBoth, restored.Shape);
      Assert.Equal(0.10, restored.LeftMargin);
      Assert.Equal(0.11, restored.RightMargin);
      Assert.Equal(0.05, restored.TopMargin);
      Assert.Equal(0.07, restored.BottomMargin);
    }

    #endregion

    // ================================================================
    #region Incline のネスト

    [Fact]
    public void Write_IncludesNestedInclineWithKind()
    {
      var original = SunShade.MakeHorizontalSunShade(W, H, D, 0.2, SouthVerticalIncline());
      var json = JsonSerializer.Serialize(original, CreateOptions());

      using var doc = JsonDocument.Parse(json);
      var inclineElem = doc.RootElement.GetProperty("incline");
      Assert.Equal(JsonValueKind.Object, inclineElem.ValueKind);
      Assert.Equal("incline", inclineElem.GetProperty("kind").GetString());
      Assert.InRange(inclineElem.GetProperty("verticalAngle").GetDouble(),
          Math.PI / 2 - 1e-9, Math.PI / 2 + 1e-9);
    }

    [Fact]
    public void RoundTrip_PreservesInclineAngles()
    {
      var inc = new Incline(horizontalAngle: -Math.PI / 6, verticalAngle: Math.PI / 4);
      var original = SunShade.MakeHorizontalSunShade(W, H, D, 0.2, inc);
      var restored = Roundtrip(original);

      Assert.InRange(restored.Incline.HorizontalAngle,
          inc.HorizontalAngle - 1e-12, inc.HorizontalAngle + 1e-12);
      Assert.InRange(restored.Incline.VerticalAngle,
          inc.VerticalAngle - 1e-12, inc.VerticalAngle + 1e-12);
    }

    #endregion

    // ================================================================
    #region エラー処理

    [Fact]
    public void Read_MissingKind_Throws()
    {
      const string json = """{ "shape": "None" }""";
      Assert.Throws<JsonException>(() =>
          JsonSerializer.Deserialize<SunShade>(json, CreateOptions()));
    }

    [Fact]
    public void Read_WrongKind_Throws()
    {
      const string json = """{ "kind": "wall", "shape": "None" }""";
      Assert.Throws<JsonException>(() =>
          JsonSerializer.Deserialize<SunShade>(json, CreateOptions()));
    }

    [Fact]
    public void Read_MissingShape_Throws()
    {
      const string json = """{ "kind": "sunShade", "winHeight": 1.8 }""";
      Assert.Throws<JsonException>(() =>
          JsonSerializer.Deserialize<SunShade>(json, CreateOptions()));
    }

    [Fact]
    public void Read_UnknownShape_Throws()
    {
      const string json = """{ "kind": "sunShade", "shape": "QuantumShade" }""";
      Assert.Throws<JsonException>(() =>
          JsonSerializer.Deserialize<SunShade>(json, CreateOptions()));
    }

    [Fact]
    public void Read_NonNoneWithoutIncline_Throws()
    {
      const string json = """
                {
                  "kind": "sunShade", "shape": "Horizontal",
                  "winHeight": 1.8, "winWidth": 1.5, "overhang": 0.6,
                  "topMargin": 0.2, "bottomMargin": 0, "leftMargin": 0.1, "rightMargin": 0.1
                }
                """;
      Assert.Throws<JsonException>(() =>
          JsonSerializer.Deserialize<SunShade>(json, CreateOptions()));
    }

    [Fact]
    public void Read_NotAnObject_Throws()
    {
      Assert.Throws<JsonException>(() =>
          JsonSerializer.Deserialize<SunShade>("""[1,2,3]""", CreateOptions()));
    }

    #endregion
  }
}