/* ShadingViewFactorTests.cs
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

using Xunit;
using Popolo.Core.Geometry;

namespace Popolo.Core.Tests.Geometry
{
  /// <summary>ShadingViewFactor のテスト。</summary>
  public class ShadingViewFactorTests
  {
    private const double TOL = 1e-10;

    #region 退化条件

    /// <summary>庇の出が 0 のとき、遮蔽率は 0。</summary>
    [Fact]
    public void OverhangDepthZero_ReturnsZero()
    {
      double f = ShadingViewFactor.GetViewFactorWindowToOverhang(
          windowWidth: 3.0, windowHeight: 2.0, overhangDepth: 0.0);
      Assert.Equal(0.0, f, precision: 10);
    }

    /// <summary>庇の出が負値のとき、遮蔽率は 0。</summary>
    [Fact]
    public void OverhangDepthNegative_ReturnsZero()
    {
      double f = ShadingViewFactor.GetViewFactorWindowToOverhang(
          windowWidth: 3.0, windowHeight: 2.0, overhangDepth: -0.5);
      Assert.Equal(0.0, f, precision: 10);
    }

    /// <summary>窓寸法 0 のとき、遮蔽率は 0。</summary>
    [Theory]
    [InlineData(0.0, 2.0)]
    [InlineData(3.0, 0.0)]
    public void ZeroWindowDimension_ReturnsZero(double winW, double winH)
    {
      double f = ShadingViewFactor.GetViewFactorWindowToOverhang(
          winW, winH, overhangDepth: 1.0);
      Assert.Equal(0.0, f, precision: 10);
    }

    #endregion

    #region 物理的妥当性の境界条件

    /// <summary>
    /// 非常に大きな庇の出に対して、view factor は単調増加し 0.5 を超えない上限に漸近する。
    /// (庇の幅は窓と同じ Y 方向幅で固定されるため、横方向のエッジから視線が抜ける分、
    ///  上限は 0.5 未満になる。窓に対して庇が非常に幅広く、かつ深い場合は 0.5 に近づく。)
    /// </summary>
    [Fact]
    public void OverhangDepthVeryLarge_StaysBelow0p5_AndIsMonotonic()
    {
      double f1 = ShadingViewFactor.GetViewFactorWindowToOverhang(3, 2, 1.0);
      double f2 = ShadingViewFactor.GetViewFactorWindowToOverhang(3, 2, 10.0);
      double f3 = ShadingViewFactor.GetViewFactorWindowToOverhang(3, 2, 100.0);
      Assert.True(f1 < f2 && f2 < f3, $"Should monotonically increase: f1={f1}, f2={f2}, f3={f3}");
      Assert.True(f3 <= 0.5 + 1e-9, $"Bounded above by 0.5: f3 = {f3}");
    }

    /// <summary>
    /// 庇の幅 (Y 方向, 形態係数の共有エッジ長) と深さの両方を非常に大きくすると 0.5 に近づく。
    /// 窓を扁平 (幅広・低高さ) にし、庇を深くすると、Y 方向のエッジ抜けの寄与が無視小となり、
    /// 上半球の視野はほぼ庇で覆われるため F → ~0.5。
    /// </summary>
    [Fact]
    public void OverhangVeryWideAndDeep_ApproachesHalf()
    {
      // 窓: 100m wide × 1m tall (扁平)、庇: 同幅 100m × 深さ 100m
      double f = ShadingViewFactor.GetViewFactorWindowToOverhang(
          windowWidth: 100.0, windowHeight: 1.0, overhangDepth: 100.0);
      Assert.True(f > 0.48, $"Should be > 0.48 (close to 0.5); got {f}");
      Assert.True(f <= 0.5 + 1e-9, $"Bounded above by 0.5: f = {f}");
    }

    /// <summary>
    /// さらに極端な比率では F が 0.5 にもっと近づく。
    /// </summary>
    [Fact]
    public void EvenMoreExtremeAspect_GetsCloserToHalf()
    {
      double f1 = ShadingViewFactor.GetViewFactorWindowToOverhang(100, 1, 100);
      double f2 = ShadingViewFactor.GetViewFactorWindowToOverhang(1000, 1, 1000);
      double f3 = ShadingViewFactor.GetViewFactorWindowToOverhang(10000, 1, 10000);
      Assert.True(f1 < f2, $"f1={f1} should < f2={f2}");
      Assert.True(f2 < f3, $"f2={f2} should < f3={f3}");
      Assert.True(f3 > 0.499, $"f3={f3} should approach 0.5");
      Assert.True(f3 <= 0.5 + 1e-9, $"f3={f3} bounded by 0.5");
    }

    /// <summary>view factor は常に 0 以上 0.5 以下。</summary>
    [Theory]
    [InlineData(3, 2, 0.5, 0)]
    [InlineData(3, 2, 1.0, 0)]
    [InlineData(3, 2, 1.0, 0.5)]
    [InlineData(6, 2, 1.0, 0)]
    [InlineData(1, 5, 1.0, 0)]
    [InlineData(1, 1, 100, 0)]
    public void ViewFactor_AlwaysWithinRange(double w, double h, double d, double g)
    {
      double f = ShadingViewFactor.GetViewFactorWindowToOverhang(w, h, d, g);
      Assert.InRange(f, 0.0, 0.5 + 1e-12);
    }

    #endregion

    #region 単調性

    /// <summary>庇の出が深くなるほど遮蔽率は単調増加。</summary>
    [Fact]
    public void DeeperOverhang_IncreasesViewFactor()
    {
      double[] depths = { 0.1, 0.5, 1.0, 2.0, 5.0, 20.0 };
      double prev = -1;
      foreach (var d in depths)
      {
        double f = ShadingViewFactor.GetViewFactorWindowToOverhang(3, 2, d);
        Assert.True(f > prev,
            $"View factor should increase with depth: depth={d}, f={f}, prev={prev}");
        prev = f;
      }
    }

    /// <summary>窓上端と庇下端の間隙が大きくなるほど遮蔽率は単調減少。</summary>
    [Fact]
    public void LargerGap_DecreasesViewFactor()
    {
      double[] gaps = { 0.0, 0.1, 0.5, 1.0, 2.0, 5.0, 20.0 };
      double prev = double.PositiveInfinity;
      foreach (var g in gaps)
      {
        double f = ShadingViewFactor.GetViewFactorWindowToOverhang(3, 2, 1.0, g);
        Assert.True(f < prev || (f == prev && g == 0),
            $"View factor should decrease with gap: gap={g}, f={f}, prev={prev}");
        prev = f;
      }
    }

    /// <summary>窓の幅を広くすると遮蔽率は単調増加 (庇との対向面積が増える)。</summary>
    [Fact]
    public void WiderWindow_IncreasesViewFactor()
    {
      double[] widths = { 0.5, 1.0, 2.0, 5.0, 20.0, 100.0 };
      double prev = -1;
      foreach (var w in widths)
      {
        double f = ShadingViewFactor.GetViewFactorWindowToOverhang(w, 2.0, 1.0);
        Assert.True(f > prev,
            $"View factor should increase with window width: w={w}, f={f}, prev={prev}");
        prev = f;
      }
    }

    /// <summary>窓を高くすると庇が「相対的に小さく」見えるため遮蔽率は単調減少。</summary>
    [Fact]
    public void TallerWindow_DecreasesViewFactor()
    {
      double[] heights = { 0.5, 1.0, 2.0, 5.0, 20.0 };
      double prev = double.PositiveInfinity;
      foreach (var h in heights)
      {
        double f = ShadingViewFactor.GetViewFactorWindowToOverhang(3.0, h, 1.0);
        Assert.True(f < prev,
            $"View factor should decrease with window height: h={h}, f={f}, prev={prev}");
        prev = h == 0 ? prev : f;
      }
    }

    /// <summary>庇の出が窓高さに対して非常に小さい場合、view factor は小さい (≪ 0.5)。</summary>
    [Fact]
    public void ShallowOverhang_HasSmallViewFactor()
    {
      double f = ShadingViewFactor.GetViewFactorWindowToOverhang(3, 2, 0.05);
      Assert.True(f < 0.05,
          $"Shallow 0.05m overhang on a 3×2m window should yield f < 0.05; got {f}");
    }

    #endregion

    #region BESTEST 形状の妥当性チェック

    /// <summary>
    /// BESTEST Case 610 相当 (窓 3m × 2m, 庇深さ 1m, 隙間 0.5m) で view factor が
    /// 中庸な値 (0.05 〜 0.4) になることを確認。
    /// </summary>
    [Fact]
    public void BESTEST_C610Like_GeometryProducesReasonableViewFactor()
    {
      double f = ShadingViewFactor.GetViewFactorWindowToOverhang(
          windowWidth: 3.0, windowHeight: 2.0, overhangDepth: 1.0, gap: 0.5);
      Assert.InRange(f, 0.05, 0.4);
    }

    /// <summary>
    /// 隙間 0 / 庇深さ ≈ 窓高さ の正方形配置: 既知文献値 (≈ 0.20) に整合。
    /// </summary>
    [Fact]
    public void UnitSquareSharedEdge_MatchesLiteratureValue()
    {
      double f = ShadingViewFactor.GetViewFactorWindowToOverhang(
          windowWidth: 1.0, windowHeight: 1.0, overhangDepth: 1.0, gap: 0.0);
      // 正方形・直角・共有辺の view factor: ≈ 0.2 (Hamilton-Morgan 文献値)
      Assert.Equal(0.20004, f, precision: 5);
    }

    /// <summary>窓 SkyView と遮蔽率の合計が 0.5 (垂直壁の天空視野総量)。</summary>
    [Theory]
    [InlineData(3.0, 2.0, 1.0, 0.5)]
    [InlineData(6.0, 2.0, 1.0, 0.0)]
    [InlineData(1.0, 1.0, 1.0, 0.0)]
    public void SkyViewPlusBlocked_Equals_HalfFractionOfHemisphere(
        double w, double h, double d, double g)
    {
      double blocked = ShadingViewFactor.GetSkyBlockedFractionByOverhang(w, h, d, g);
      double sky = ShadingViewFactor.GetWindowSkyViewFactorWithOverhang(w, h, d, g);
      Assert.Equal(0.5, blocked + sky, precision: 10);
    }

    #endregion

  }
}
