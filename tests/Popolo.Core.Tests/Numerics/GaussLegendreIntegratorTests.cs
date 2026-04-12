/* GaussLegendreIntegratorTests.cs
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
using Popolo.Numerics;
using Popolo.Exceptions;

namespace Popolo.Core.Tests.Numerics
{
  /// <summary>GaussLegendreIntegrator のテスト</summary>
  public class GaussLegendreIntegratorTests
  {
    #region インスタンスメソッドのテスト

    /// <summary>∫[0,1] 1 dx = 1（定数関数）</summary>
    [Fact]
    public void Integrate_ConstantFunction_ReturnsCorrectValue()
    {
      var integrator = new GaussLegendreIntegrator(x => 1.0, 5);
      double result = integrator.Integrate(0.0, 1.0);
      Assert.Equal(1.0, result, precision: 10);
    }

    /// <summary>∫[0,1] x dx = 0.5（1次関数）</summary>
    [Fact]
    public void Integrate_LinearFunction_ReturnsCorrectValue()
    {
      var integrator = new GaussLegendreIntegrator(x => x, 5);
      double result = integrator.Integrate(0.0, 1.0);
      Assert.Equal(0.5, result, precision: 10);
    }

    /// <summary>∫[0,1] x^2 dx = 1/3（2次関数）</summary>
    [Fact]
    public void Integrate_QuadraticFunction_ReturnsCorrectValue()
    {
      var integrator = new GaussLegendreIntegrator(x => x * x, 5);
      double result = integrator.Integrate(0.0, 1.0);
      Assert.Equal(1.0 / 3.0, result, precision: 10);
    }

    /// <summary>∫[0,π] sin(x) dx = 2</summary>
    [Fact]
    public void Integrate_SinFunction_ReturnsCorrectValue()
    {
      var integrator = new GaussLegendreIntegrator(x => Math.Sin(x), 10);
      double result = integrator.Integrate(0.0, Math.PI);
      Assert.Equal(2.0, result, precision: 8);
    }

    /// <summary>∫[0,1] exp(x) dx = e - 1</summary>
    [Fact]
    public void Integrate_ExpFunction_ReturnsCorrectValue()
    {
      var integrator = new GaussLegendreIntegrator(x => Math.Exp(x), 10);
      double result = integrator.Integrate(0.0, 1.0);
      Assert.Equal(Math.E - 1.0, result, precision: 9);
    }

    /// <summary>分点数を増やすと精度が上がる</summary>
    [Fact]
    public void Integrate_HigherNodeCount_IncreasesAccuracy()
    {
      // ∫[0,1] x^10 dx = 1/11 ≈ 0.0909...
      // 分点数が少ないと誤差が大きく、多いと精度が高い
      GaussLegendreIntegrator.IntegrateFunction f = x => Math.Pow(x, 10);

      var coarse = new GaussLegendreIntegrator(f, 3);
      var fine = new GaussLegendreIntegrator(f, 10);

      double exact = 1.0 / 11.0;
      double errCoarse = Math.Abs(coarse.Integrate(0.0, 1.0) - exact);
      double errFine = Math.Abs(fine.Integrate(0.0, 1.0) - exact);

      Assert.True(errFine < errCoarse,
          $"Fine ({errFine}) should be more accurate than coarse ({errCoarse}).");
    }

    /// <summary>UpdateNodeNumber で分点数を変更できる</summary>
    [Fact]
    public void UpdateNodeNumber_ChangesAccuracy()
    {
      var integrator = new GaussLegendreIntegrator(x => Math.Sin(x), 2);
      double before = integrator.Integrate(0.0, Math.PI);

      integrator.UpdateNodeNumber(10);
      double after = integrator.Integrate(0.0, Math.PI);

      // 分点数増加後の方が真値(2.0)に近い
      Assert.True(Math.Abs(after - 2.0) < Math.Abs(before - 2.0));
    }

    /// <summary>nodeNumber が0以下のとき PopoloArgumentException が発生する</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_InvalidNodeNumber_ThrowsPopoloArgumentException(int nodeNumber)
    {
      var ex = Assert.Throws<PopoloArgumentException>(
          () => new GaussLegendreIntegrator(x => x, nodeNumber));
      Assert.Equal("nodeNumber", ex.ParamName);
    }

    /// <summary>UpdateNodeNumber に0以下を渡すと PopoloArgumentException が発生する</summary>
    [Fact]
    public void UpdateNodeNumber_InvalidNodeNumber_ThrowsPopoloArgumentException()
    {
      var integrator = new GaussLegendreIntegrator(x => x, 5);
      var ex = Assert.Throws<PopoloArgumentException>(
          () => integrator.UpdateNodeNumber(0));
      Assert.Equal("nodeNumber", ex.ParamName);
    }

    #endregion

    #region 静的メソッドのテスト

    /// <summary>ComputeNodesAndWeights が正しい分点数の配列を返す</summary>
    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    [InlineData(3, 2)]
    [InlineData(5, 3)]
    [InlineData(10, 5)]
    public void ComputeNodesAndWeights_ReturnsCorrectArraySize(
        int nodeNumber, int expectedSize)
    {
      GaussLegendreIntegrator.ComputeNodesAndWeights(nodeNumber, out double[] x, out double[] w);
      Assert.Equal(expectedSize, x.Length);
      Assert.Equal(expectedSize, w.Length);
    }

    /// <summary>重みの総和は2になる（[-1,1]区間の性質）</summary>
    [Theory]
    [InlineData(5)]
    [InlineData(10)]
    public void ComputeNodesAndWeights_WeightsSumToTwo(int nodeNumber)
    {
      GaussLegendreIntegrator.ComputeNodesAndWeights(nodeNumber, out double[] x, out double[] w);

      double sum = 0;
      int n = x.Length;
      if (nodeNumber % 2 == 1)  // 奇数：中心点(x=0)が存在する
      {
        sum += w[n - 1];
        for (int i = 0; i < n - 1; i++) sum += 2.0 * w[i];
      }
      else  // 偶数：全て対称点
      {
        for (int i = 0; i < n; i++) sum += 2.0 * w[i];
      }
      Assert.Equal(2.0, sum, precision: 7);
    }

    /// <summary>静的Integrateメソッドで∫[0,1] x dx = 0.5</summary>
    [Fact]
    public void StaticIntegrate_LinearFunction_ReturnsCorrectValue()
    {
      GaussLegendreIntegrator.ComputeNodesAndWeights(5, out double[] x, out double[] w);
      double result = GaussLegendreIntegrator.Integrate(v => v, 0.0, 1.0, x, w);
      Assert.Equal(0.5, result, precision: 10);
    }

    /// <summary>number が0以下のとき PopoloArgumentException が発生する</summary>
    [Fact]
    public void ComputeNodesAndWeights_InvalidNumber_ThrowsPopoloArgumentException()
    {
      var ex = Assert.Throws<PopoloArgumentException>(
          () => GaussLegendreIntegrator.ComputeNodesAndWeights(0, out _, out _));
      Assert.Equal("number", ex.ParamName);
    }

    #endregion
  }
}
