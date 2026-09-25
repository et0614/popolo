/* NMSimplexTests.cs
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
  /// <summary>NMSimplex のテスト</summary>
  public class NMSimplexTests
  {
    // 2次関数 f(x,y) = x^2 + y^2 → 最小値 0、最小点 (0,0)
    private static readonly NelderMeadSimplex.OptimizeFunction QuadraticFunction =
        x => x[0] * x[0] + x[1] * x[1];

    // ローゼンブロック関数 f(x,y) = 100(y-x^2)^2 + (1-x)^2
    // → 最小値 0、最小点 (1,1)
    private static readonly NelderMeadSimplex.OptimizeFunction RosenbrockFunction =
        x => 100 * Math.Pow(x[1] - x[0] * x[0], 2) + Math.Pow(1 - x[0], 2);

    #region GetSolution (unconstrained) tests

    /// <summary>2次関数の最小点が原点になる</summary>
    [Fact]
    public void GetSolution_QuadraticFunction_FindsMinimumAtOrigin()
    {
      double[] minX = { -10.0, -10.0 };
      double[] maxX = { 10.0, 10.0 };

      double[] result = NelderMeadSimplex.GetSolution(
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

      double[] result = NelderMeadSimplex.GetSolution(
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

      double[] result = NelderMeadSimplex.GetSolution(
          QuadraticFunction, minX, maxX, out bool success);

      Assert.True(success);
      Assert.Equal(0.0, QuadraticFunction(result), precision: 4);
    }

    /// <summary>関数値のスケールが小さい目的関数でも誤って収束と判定しない</summary>
    /// <remarks>
    /// 関数値の絶対誤差だけで収束判定すると、関数値の差が初期単体の段階で許容値を
    /// 下回るため、最小点から遠い点で「収束」してしまう。
    /// </remarks>
    [Fact]
    public void GetSolution_SmallScaleObjective_FindsTrueMinimum()
    {
      NelderMeadSimplex.OptimizeFunction f =
          x => 1e-8 * (Math.Pow(x[0] - 3.0, 2) + Math.Pow(x[1] + 1.0, 2));

      double[] result = NelderMeadSimplex.GetSolution(
          f, new double[] { -10.0, -10.0 }, new double[] { 10.0, 10.0 }, out bool success);

      Assert.True(success);
      Assert.Equal(3.0, result[0], precision: 3);
      Assert.Equal(-1.0, result[1], precision: 3);
    }

    /// <summary>探索範囲外の遠い最小点へ拡大ステップで効率よく到達する</summary>
    /// <remarks>
    /// 拡大点は反射方向 c + γα(c − p) にとる必要がある。最悪点側 (2p − c) に
    /// とると拡大が働かず、反射だけで少しずつ移動するため評価回数が膨大になる。
    /// </remarks>
    [Fact]
    public void GetSolution_DistantMinimum_ExpansionReachesItEfficiently()
    {
      int evalCount = 0;
      NelderMeadSimplex.OptimizeFunction f = x =>
      {
        evalCount++;
        return Math.Pow(x[0] - 1000.0, 2) + Math.Pow(x[1] - 1000.0, 2);
      };

      double[] result = NelderMeadSimplex.GetSolution(
          f, new double[] { -1.0, -1.0 }, new double[] { 1.0, 1.0 }, out bool success);

      Assert.True(success);
      Assert.Equal(1000.0, result[0], precision: 3);
      Assert.Equal(1000.0, result[1], precision: 3);
      Assert.True(evalCount < 1000, $"evalCount={evalCount}");
    }

    /// <summary>ローゼンブロック関数を高精度に解ける（単体の大きさによる収束判定）</summary>
    [Fact]
    public void GetSolution_RosenbrockFunction_ConvergesTightly()
    {
      double[] result = NelderMeadSimplex.GetSolution(
          RosenbrockFunction, new double[] { -5.0, -5.0 }, new double[] { 5.0, 5.0 }, out bool success);

      Assert.True(success);
      Assert.Equal(1.0, result[0], precision: 4);
      Assert.Equal(1.0, result[1], precision: 4);
    }

    /// <summary>平坦な関数（全域で一定）でも単体が縮小して収束する</summary>
    [Fact]
    public void GetSolution_FlatFunction_Converges()
    {
      double[] result = NelderMeadSimplex.GetSolution(
          x => 5.0, new double[] { -1.0, -1.0 }, new double[] { 1.0, 1.0 }, out bool success);

      Assert.True(success);
      Assert.InRange(result[0], -1.0, 1.0);
      Assert.InRange(result[1], -1.0, 1.0);
    }

    /// <summary>minX が null のとき PopoloArgumentException が発生する</summary>
    [Fact]
    public void GetSolution_NullMinX_ThrowsPopoloArgumentException()
    {
      var ex = Assert.Throws<PopoloArgumentException>(
          () => NelderMeadSimplex.GetSolution(
              QuadraticFunction, null!, new double[] { 1.0, 1.0 }, out _));
      Assert.Equal("minX", ex.ParamName);
    }

    /// <summary>minX と maxX の長さが異なるとき PopoloArgumentException が発生する</summary>
    [Fact]
    public void GetSolution_MismatchedLength_ThrowsPopoloArgumentException()
    {
      var ex = Assert.Throws<PopoloArgumentException>(
          () => NelderMeadSimplex.GetSolution(
              QuadraticFunction,
              new double[] { -1.0 },
              new double[] { 1.0, 2.0 },
              out _));
      Assert.Equal("maxX", ex.ParamName);
    }

    #endregion

    #region GetSolution (constrained) tests

    /// <summary>制約付きで円上の最小点を探索できる</summary>
    [Fact]
    public void GetSolution_WithConstraint_FindsConstrainedMinimum()
    {
      NelderMeadSimplex.OptimizeFunction objective = x => x[0] + x[1];
      NelderMeadSimplex.OptimizeFunction constraint = x => x[0] * x[0] + x[1] * x[1] - 1.0;

      double[] minX = { -2.0, -2.0 };
      double[] maxX = { 2.0, 2.0 };

      double[] result = NelderMeadSimplex.GetSolution(
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
