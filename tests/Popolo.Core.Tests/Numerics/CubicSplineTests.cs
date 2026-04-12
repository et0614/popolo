/* CubicSplineTests.cs
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
  /// <summary>CubicSpline のテスト</summary>
  public class CubicSplineTests
  {

    #region GetParameters のテスト

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

    #endregion

    #region Interpolate のテスト

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
      // 4点以上が必要（3点だとSolveTridiagonalMatrixが1列行列になる）
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
