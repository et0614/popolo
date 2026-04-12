/* NMSimplexTests.cs
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
  /// <summary>NMSimplex のテスト</summary>
  public class NMSimplexTests
  {
    // 2次関数 f(x,y) = x^2 + y^2 → 最小値 0、最小点 (0,0)
    private static readonly NMSimplex.OptimizeFunction QuadraticFunction =
        x => x[0] * x[0] + x[1] * x[1];

    // ローゼンブロック関数 f(x,y) = 100(y-x^2)^2 + (1-x)^2
    // → 最小値 0、最小点 (1,1)
    private static readonly NMSimplex.OptimizeFunction RosenbrockFunction =
        x => 100 * Math.Pow(x[1] - x[0] * x[0], 2) + Math.Pow(1 - x[0], 2);

    #region GetSolution（制約なし）のテスト

    /// <summary>2次関数の最小点が原点になる</summary>
    [Fact]
    public void GetSolution_QuadraticFunction_FindsMinimumAtOrigin()
    {
      double[] minX = { -10.0, -10.0 };
      double[] maxX = { 10.0, 10.0 };

      double[] result = NMSimplex.GetSolution(
          QuadraticFunction, minX, maxX, out bool success);

      Assert.True(success);
      Assert.InRange(result[0], -0.1, 0.1);
      Assert.InRange(result[1], -0.1, 0.1);
    }

    /// <summary>ローゼンブロック関数の最小点が (1,1) になる</summary>
    [Fact]
    public void GetSolution_RosenbrockFunction_FindsMinimumAtOne()
    {
      double[] minX = { -5.0, -5.0 };
      double[] maxX = { 5.0, 5.0 };

      double[] result = NMSimplex.GetSolution(
          RosenbrockFunction, minX, maxX, out bool success);

      Assert.True(success);
      Assert.InRange(result[0], 0.9, 1.1);
      Assert.InRange(result[1], 0.9, 1.1);
    }

    /// <summary>最小値の関数値が 0 に近い（品質確認）</summary>
    [Fact]
    public void GetSolution_QuadraticFunction_FunctionValueNearZero()
    {
      double[] minX = { -10.0, -10.0 };
      double[] maxX = { 10.0, 10.0 };

      double[] result = NMSimplex.GetSolution(
          QuadraticFunction, minX, maxX, out bool success);

      Assert.True(success);
      Assert.Equal(0.0, QuadraticFunction(result), precision: 4);
    }

    /// <summary>minX が null のとき PopoloArgumentException が発生する</summary>
    [Fact]
    public void GetSolution_NullMinX_ThrowsPopoloArgumentException()
    {
      var ex = Assert.Throws<PopoloArgumentException>(
          () => NMSimplex.GetSolution(
              QuadraticFunction, null!, new double[] { 1.0, 1.0 }, out _));
      Assert.Equal("minX", ex.ParamName);
    }

    /// <summary>minX と maxX の長さが異なるとき PopoloArgumentException が発生する</summary>
    [Fact]
    public void GetSolution_MismatchedLength_ThrowsPopoloArgumentException()
    {
      var ex = Assert.Throws<PopoloArgumentException>(
          () => NMSimplex.GetSolution(
              QuadraticFunction,
              new double[] { -1.0 },
              new double[] { 1.0, 2.0 },
              out _));
      Assert.Equal("maxX", ex.ParamName);
    }

    #endregion

    #region GetSolution（制約付き）のテスト

    /// <summary>制約付きで円上の最小点を探索できる</summary>
    [Fact]
    public void GetSolution_WithConstraint_FindsConstrainedMinimum()
    {
      NMSimplex.OptimizeFunction objective = x => x[0] + x[1];
      NMSimplex.OptimizeFunction constraint = x => x[0] * x[0] + x[1] * x[1] - 1.0;

      double[] minX = { -2.0, -2.0 };
      double[] maxX = { 2.0, 2.0 };

      double[] result = NMSimplex.GetSolution(
          objective, constraint, minX, maxX, out bool success);

      Assert.True(success);
      // 制約 x^2 + y^2 ≈ 1 を緩めに確認
      Assert.InRange(result[0] * result[0] + result[1] * result[1], 0.9, 1.1);
      // 目的関数値が -√2 ≈ -1.414 付近
      Assert.InRange(result[0] + result[1], -1.6, -1.1);
    }

    #endregion

  }
}
