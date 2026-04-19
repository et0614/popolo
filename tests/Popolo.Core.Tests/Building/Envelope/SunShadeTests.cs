/* SunShadeTests.cs
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
using Xunit;
using Popolo.Core.Building.Envelope;
using Popolo.Core.Climate;

namespace Popolo.Core.Tests.Building.Envelope
{
  /// <summary>SunShade のテスト</summary>
  /// <remarks>
  /// 方位角の符号規約：南=0, 東=負, 西=正（radianで表現）
  /// 傾斜角の符号規約：水平=0, 垂直=π/2
  ///
  /// テストに使用する窓：幅1.0m × 高さ1.5m（南向き鉛直面）
  /// テストに使用する太陽：高度45°（π/4）, 方位0（真南）
  /// </remarks>
  public class SunShadeTests
  {
    #region テスト用ヘルパー

    private const double WinWidth = 1.0;  // 窓幅[m]
    private const double WinHeight = 1.5;  // 窓高さ[m]

    /// <summary>南向き鉛直面の傾斜面を生成する</summary>
    private static Incline MakeSouthVerticalIncline()
        => new Incline(0.0, Math.PI / 2); // 方位0（南）, 傾斜90°

    /// <summary>指定した高度・方位の太陽状態を生成する</summary>
    private static Sun MakeSun(double altitudeDeg, double orientationDeg = 0.0)
    {
      var sun = new Sun(35.7, 139.7, 135.0); // 東京の緯度経度
      sun.Altitude = altitudeDeg * Math.PI / 180.0;
      sun.Orientation = orientationDeg * Math.PI / 180.0;
      return sun;
    }

    #endregion

    #region MakeEmptySunShadeのテスト

    /// <summary>日除けなし（Shape=None）では影率が0</summary>
    [Fact]
    public void MakeEmptySunShade_ShadowRateIsZero()
    {
      var shade = SunShade.MakeEmptySunShade();
      var sun = MakeSun(45);

      Assert.Equal(0.0, shade.GetShadowRatio(sun), precision: 6);
    }

    /// <summary>Shape=NoneはShapeプロパティがNone</summary>
    [Fact]
    public void MakeEmptySunShade_ShapeIsNone()
    {
      var shade = SunShade.MakeEmptySunShade();
      Assert.Equal(SunShade.Shapes.None, shade.Shape);
    }

    #endregion

    #region 日没後のテスト

    /// <summary>太陽高度が0以下（日没後）では影率が1</summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(-10.0)]
    [InlineData(-45.0)]
    public void GetShadowRate_SunBelowHorizon_ReturnsOne(double altitudeDeg)
    {
      var incline = MakeSouthVerticalIncline();
      var shade = SunShade.MakeHorizontalSunShade(WinWidth, WinHeight, 0.5, 0.0, incline);
      var sun = MakeSun(altitudeDeg);

      Assert.Equal(1.0, shade.GetShadowRatio(sun), precision: 6);
    }

    #endregion

    #region 無限長水平庇（LongHorizontal）のテスト

    /// <summary>無限長水平庇のShapeがLongHorizontal</summary>
    [Fact]
    public void MakeHorizontalSunShade_Infinite_ShapeIsLongHorizontal()
    {
      var incline = MakeSouthVerticalIncline();
      var shade = SunShade.MakeHorizontalSunShade(WinWidth, WinHeight, 0.5, 0.0, incline);

      Assert.Equal(SunShade.Shapes.LongHorizontal, shade.Shape);
    }

    /// <summary>無限長水平庇で庇が大きいほど影率が大きい</summary>
    [Fact]
    public void MakeHorizontalSunShade_Infinite_LargerOverhang_LargerShadowRate()
    {
      var incline = MakeSouthVerticalIncline();
      var sun = MakeSun(45); // 高度45°、真南

      var small = SunShade.MakeHorizontalSunShade(WinWidth, WinHeight, 0.3, 0.0, incline);
      var medium = SunShade.MakeHorizontalSunShade(WinWidth, WinHeight, 0.6, 0.0, incline);
      var large = SunShade.MakeHorizontalSunShade(WinWidth, WinHeight, 1.2, 0.0, incline);

      double sr1 = small.GetShadowRatio(sun);
      double sr2 = medium.GetShadowRatio(sun);
      double sr3 = large.GetShadowRatio(sun);

      Assert.True(sr1 < sr2, $"small({sr1:F4}) should be < medium({sr2:F4})");
      Assert.True(sr2 < sr3, $"medium({sr2:F4}) should be < large({sr3:F4})");
    }

    /// <summary>
    /// 無限長水平庇で太陽高度が高いほど影率が大きい。
    /// プロファイル角 = atan(tan(altitude) / cos(方位差)) であり、
    /// 太陽高度が高い（南中に近い）ほどプロファイル角が大きく、庇の影が窓に深く落ちる。
    /// </summary>
    [Fact]
    public void MakeHorizontalSunShade_Infinite_HigherAltitude_LargerShadowRate()
    {
      var incline = MakeSouthVerticalIncline();
      var shade = SunShade.MakeHorizontalSunShade(WinWidth, WinHeight, 0.5, 0.0, incline);

      double sr30 = shade.GetShadowRatio(MakeSun(30)); // 高度低い→プロファイル角小→影短い
      double sr60 = shade.GetShadowRatio(MakeSun(60)); // 高度高い→プロファイル角大→影長い

      Assert.True(sr60 > sr30,
          $"60° ({sr60:F4}) should be > 30° ({sr30:F4})");
    }

    /// <summary>無限長水平庇で影率は0以上1以下</summary>
    [Theory]
    [InlineData(20)]
    [InlineData(45)]
    [InlineData(70)]
    public void MakeHorizontalSunShade_Infinite_ShadowRateInRange(double altitudeDeg)
    {
      var incline = MakeSouthVerticalIncline();
      var shade = SunShade.MakeHorizontalSunShade(WinWidth, WinHeight, 0.5, 0.0, incline);
      var sun = MakeSun(altitudeDeg);

      double sr = shade.GetShadowRatio(sun);
      Assert.InRange(sr, 0.0, 1.0);
    }

    /// <summary>庇張り出しが0のとき影率が0</summary>
    [Fact]
    public void MakeHorizontalSunShade_Infinite_ZeroOverhang_ShadowRateIsZero()
    {
      var incline = MakeSouthVerticalIncline();
      var shade = SunShade.MakeHorizontalSunShade(WinWidth, WinHeight, 0.0, 0.0, incline);
      var sun = MakeSun(45);

      Assert.Equal(0.0, shade.GetShadowRatio(sun), precision: 6);
    }

    #endregion

    #region 有限長水平庇（Horizontal）のテスト

    /// <summary>有限長水平庇のShapeがHorizontal</summary>
    [Fact]
    public void MakeHorizontalSunShade_Finite_ShapeIsHorizontal()
    {
      var incline = MakeSouthVerticalIncline();
      var shade = SunShade.MakeHorizontalSunShade(
          WinWidth, WinHeight, 0.5, 0.0, 0.0, 0.0, incline);

      Assert.Equal(SunShade.Shapes.Horizontal, shade.Shape);
    }

    /// <summary>有限長庇は無限長庇より影率が小さいか等しい</summary>
    [Fact]
    public void MakeHorizontalSunShade_Finite_ShadowRateLessOrEqualInfinite()
    {
      var incline = MakeSouthVerticalIncline();
      var sun = MakeSun(45);
      var finite = SunShade.MakeHorizontalSunShade(WinWidth, WinHeight, 0.5, 0.0, 0.0, 0.0, incline);
      var infinite = SunShade.MakeHorizontalSunShade(WinWidth, WinHeight, 0.5, 0.0, incline);

      Assert.True(finite.GetShadowRatio(sun) <= infinite.GetShadowRatio(sun));
    }

    #endregion

    #region 袖壁（LongVertical）のテスト

    /// <summary>左袖壁のShapeがLongVerticalLeft</summary>
    [Fact]
    public void MakeVerticalSunShade_Left_ShapeIsLongVerticalLeft()
    {
      var incline = MakeSouthVerticalIncline();
      var shade = SunShade.MakeVerticalSunShade(WinWidth, WinHeight, 0.5, 0.0, true, incline);

      Assert.Equal(SunShade.Shapes.LongVerticalLeft, shade.Shape);
    }

    /// <summary>右袖壁のShapeがLongVerticalRight</summary>
    [Fact]
    public void MakeVerticalSunShade_Right_ShapeIsLongVerticalRight()
    {
      var incline = MakeSouthVerticalIncline();
      var shade = SunShade.MakeVerticalSunShade(WinWidth, WinHeight, 0.5, 0.0, false, incline);

      Assert.Equal(SunShade.Shapes.LongVerticalRight, shade.Shape);
    }

    /// <summary>袖壁で影率は0以上1以下</summary>
    [Theory]
    [InlineData(true, 10)]
    [InlineData(true, -10)]
    [InlineData(false, 10)]
    [InlineData(false, -10)]
    public void MakeVerticalSunShade_ShadowRateInRange(bool isLeft, double orientationDeg)
    {
      var incline = MakeSouthVerticalIncline();
      var shade = SunShade.MakeVerticalSunShade(WinWidth, WinHeight, 0.5, 0.0, isLeft, incline);
      var sun = MakeSun(45, orientationDeg);

      Assert.InRange(shade.GetShadowRatio(sun), 0.0, 1.0);
    }

    #endregion

    #region コピーコンストラクタのテスト

    /// <summary>コピーコンストラクタで全プロパティが複製される</summary>
    [Fact]
    public void CopyConstructor_CopiesAllProperties()
    {
      var incline = MakeSouthVerticalIncline();
      var original = SunShade.MakeHorizontalSunShade(WinWidth, WinHeight, 0.5, 0.1, incline);
      var copy = new SunShade(original);

      Assert.Equal(original.Shape, copy.Shape);
      Assert.Equal(original.WinWidth, copy.WinWidth, precision: 6);
      Assert.Equal(original.WinHeight, copy.WinHeight, precision: 6);
      Assert.Equal(original.Overhang, copy.Overhang, precision: 6);
      Assert.Equal(original.TopMargin, copy.TopMargin, precision: 6);
    }

    /// <summary>コピーは元オブジェクトと独立している（Inclineが深いコピー）</summary>
    [Fact]
    public void CopyConstructor_InclineIsDeepCopy()
    {
      var incline = MakeSouthVerticalIncline();
      var original = SunShade.MakeHorizontalSunShade(WinWidth, WinHeight, 0.5, 0.0, incline);
      var copy = new SunShade(original);

      // Inclineが独立したオブジェクト参照であることを確認
      Assert.NotSame(original.Incline, copy.Incline);
    }

    #endregion
  }
}
