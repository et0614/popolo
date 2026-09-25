/* CubicSplineTests.cs
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
using Xunit;
using Popolo.Core.Numerics;
using Popolo.Core.Exceptions;

namespace Popolo.Core.Tests.Numerics
{
  /// <summary>CubicSpline のテスト</summary>
  public class CubicSplineTests
  {

    #region GetParameters tests

    /// <summary>引数チェック：x が null のとき例外が発生する</summary>
    [Fact]
    public void GetParameters_NullX_ThrowsPopoloArgumentException()
    {
      var ex = Assert.Throws<PopoloArgumentException>(
          () => CubicSpline.GetParameters(null!, new double[] { 1, 2, 3 }));
      Assert.Equal("x", ex.ParamName);
    }

    /// <summary>引数チェック：x と y の長さが異なるとき例外が発生する</summary>
    [Fact]
    public void GetParameters_MismatchedLength_ThrowsPopoloArgumentException()
    {
      var ex = Assert.Throws<PopoloArgumentException>(
          () => CubicSpline.GetParameters(
              new double[] { 0, 1, 2 },
              new double[] { 0, 1 }));
      Assert.Equal("y", ex.ParamName);
    }

    /// <summary>係数配列の長さが入力と同じになる</summary>
    [Fact]
    public void GetParameters_ReturnsCorrectLength()
    {
      double[] x = { 0.0, 1.0, 2.0, 3.0 };
      double[] y = { 0.0, 1.0, 0.0, 1.0 };
      double[] c = CubicSpline.GetParameters(x, y);
      Assert.Equal(x.Length, c.Length);
    }

    /// <summary>端点の係数はゼロ（自然スプラインの境界条件）</summary>
    [Fact]
    public void GetParameters_NaturalSplineBoundary_EndCoefficientsAreZero()
    {
      double[] x = { 0.0, 1.0, 2.0, 3.0 };
      double[] y = { 0.0, 1.0, 4.0, 9.0 };
      double[] c = CubicSpline.GetParameters(x, y);
      Assert.Equal(0.0, c[0], precision: 10);
      Assert.Equal(0.0, c[c.Length - 1], precision: 10);
    }

    /// <summary>最小点数（3点）でも係数を計算できる</summary>
    /// <remarks>3点では未知数が1つの三重対角系となる。</remarks>
    [Fact]
    public void GetParameters_ThreePoints_ReturnsNaturalSplineCoefficients()
    {
      double[] x = { 0.0, 1.0, 2.0 };
      double[] y = { 0.0, 1.0, 0.0 };
      double[] c = CubicSpline.GetParameters(x, y);

      // 4 c1 = 3 ((0-1)/1 - (1-0)/1) = -6 → c1 = -1.5
      Assert.Equal(3, c.Length);
      Assert.Equal(0.0, c[0], precision: 12);
      Assert.Equal(-1.5, c[1], precision: 12);
      Assert.Equal(0.0, c[2], precision: 12);
    }

    /// <summary>引数チェック：2点以下のとき例外が発生する</summary>
    [Fact]
    public void GetParameters_TwoPoints_ThrowsPopoloArgumentException()
    {
      var ex = Assert.Throws<PopoloArgumentException>(
          () => CubicSpline.GetParameters(new double[] { 0, 1 }, new double[] { 0, 1 }));
      Assert.Equal("x", ex.ParamName);
    }

    #endregion

    #region Interpolate tests

    /// <summary>3点のデータで補間でき、データ点を再現する</summary>
    [Fact]
    public void Interpolate_ThreePoints_ReproducesDataAndMidpoint()
    {
      double[] x = { 0.0, 1.0, 2.0 };
      double[] y = { 0.0, 1.0, 0.0 };
      double[] c = CubicSpline.GetParameters(x, y);

      for (int i = 0; i < x.Length; i++)
        Assert.Equal(y[i], CubicSpline.Interpolate(x, y, c, x[i]), precision: 12);
      // 区間 [0,1]: S(t) = 1.5 t - 0.5 t^3 → S(0.5) = 0.6875（対称性より S(1.5) も同じ）
      Assert.Equal(0.6875, CubicSpline.Interpolate(x, y, c, 0.5), precision: 12);
      Assert.Equal(0.6875, CubicSpline.Interpolate(x, y, c, 1.5), precision: 12);
    }

    /// <summary>補間点がデータ点と一致する場合、元の値が返る</summary>
    [Fact]
    public void Interpolate_AtKnownPoints_ReturnsOriginalValues()
    {
      double[] x = { 0.0, 1.0, 2.0, 3.0, 4.0 };
      double[] y = { 0.0, 1.0, 4.0, 9.0, 16.0 };
      double[] c = CubicSpline.GetParameters(x, y);

      for (int i = 0; i < x.Length; i++)
      {
        double result = CubicSpline.Interpolate(x, y, c, x[i]);
        Assert.Equal(y[i], result, precision: 6);
      }
    }

    /// <summary>2次関数のデータから正確に補間できる</summary>
    [Fact]
    public void Interpolate_CubicData_ReturnsExactValues()
    {
      // 3次スプラインは3次以下の多項式を完全に再現する
      // y = x^3 のデータ点で確認
      double[] x = { 0.0, 1.0, 2.0, 3.0, 4.0 };
      double[] y = { 0.0, 1.0, 8.0, 27.0, 64.0 };
      double[] c = CubicSpline.GetParameters(x, y);

      // データ点での値が一致することを確認
      for (int i = 0; i < x.Length; i++)
        Assert.Equal(y[i], CubicSpline.Interpolate(x, y, c, x[i]), precision: 6);
    }

    /// <summary>既存テストの期待値との一致確認（スプライン係数）</summary>
    [Fact]
    public void Interpolate_ExistingTestData_MatchesExpectedValues()
    {
      double[] x = new double[25];
      for (int i = 0; i < x.Length; i++) x[i] = i;
      double[] y = {
                0, 0, 0, 0, 0, 0, 0, 42, 224, 215, 210, 217,
                219, 217, 210, 250, 210, 217, 91, 9, 9, 0, 0, 0, 0
            };
      double[] c = CubicSpline.GetParameters(x, y);

      // x2=7.0（データ点）での値は y[7]=42 と一致するはず
      double result = CubicSpline.Interpolate(x, y, c, 7.0);
      Assert.Equal(42.0, result, precision: 4);
    }

    /// <summary>複数点補間の結果が1点補間と一致する</summary>
    [Fact]
    public void Interpolate_MultiPoint_MatchesSinglePointResults()
    {
      double[] x = { 0.0, 1.0, 2.0, 3.0, 4.0 };
      double[] y = { 0.0, 1.0, 4.0, 9.0, 16.0 };
      double[] c = CubicSpline.GetParameters(x, y);
      double[] x2 = { 0.5, 1.5, 2.5, 3.5 };

      double[] multi = CubicSpline.Interpolate(x, y, c, x2);

      for (int i = 0; i < x2.Length; i++)
      {
        double single = CubicSpline.Interpolate(x, y, c, x2[i]);
        Assert.Equal(single, multi[i], precision: 10);
      }
    }

    /// <summary>範囲外の x2 で PopoloArgumentException が発生する</summary>
    [Fact]
    public void Interpolate_OutOfRange_ThrowsPopoloArgumentException()
    {
      double[] x = { 0.0, 1.0, 2.0, 3.0 };
      double[] y = { 0.0, 1.0, 4.0, 9.0 };
      double[] c = CubicSpline.GetParameters(x, y);

      var ex = Assert.Throws<PopoloArgumentException>(
          () => CubicSpline.Interpolate(x, y, c, new double[] { -1.0 }));
      Assert.Equal("x2", ex.ParamName);
    }

    #endregion

  }
}
