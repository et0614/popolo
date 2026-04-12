/* LevenbergMarquardtTests.cs
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
using Popolo.Numerics.LinearAlgebra;
using Popolo.Exceptions;

namespace Popolo.Core.Tests.Numerics
{
  /// <summary>LevenbergMarquardt のテスト</summary>
  public class LevenbergMarquardtTests
  {
    #region コンストラクタのテスト

    /// <summary>numberOfFunctions が numberOfVariables より小さい場合に例外が発生する</summary>
    [Fact]
    public void Constructor_FunctionsLessThanVariables_ThrowsPopoloArgumentException()
    {
      LevenbergMarquardt.ErrorFunction eFnc =
          (IVector inputs, ref IVector outputs) => { };

      var ex = Assert.Throws<PopoloArgumentException>(
          () => new LevenbergMarquardt(eFnc,
              numberOfFunctions: 1,
              numberOfVariables: 2));
      Assert.Equal("numberOfFunctions", ex.ParamName);
    }

    /// <summary>numberOfFunctions == numberOfVariables は正常に作成できる</summary>
    [Fact]
    public void Constructor_FunctionsEqualsVariables_Succeeds()
    {
      LevenbergMarquardt.ErrorFunction eFnc =
          (IVector inputs, ref IVector outputs) => { };

      var lm = new LevenbergMarquardt(eFnc,
          numberOfFunctions: 2,
          numberOfVariables: 2);

      Assert.Equal(2, lm.NumberOfFunctions);
      Assert.Equal(2, lm.NumberOfVariables);
    }

    #endregion

    #region Minimize のテスト

    /// <summary>線形最小二乗問題を正しく解ける</summary>
    [Fact]
    public void Minimize_LinearLeastSquares_ConvergesToCorrectSolution()
    {
      // 過決定系（3方程式2変数）でLMが安定する
      // f0 = x0 - 2 = 0
      // f1 = x1 - 3 = 0
      // f2 = x0 + x1 - 5 = 0  （上2式と整合）
      LevenbergMarquardt.ErrorFunction eFnc =
          (IVector inputs, ref IVector outputs) =>
          {
            outputs[0] = inputs[0] - 2.0;
            outputs[1] = inputs[1] - 3.0;
            outputs[2] = inputs[0] + inputs[1] - 5.0;
          };

      var lm = new LevenbergMarquardt(eFnc,
          numberOfFunctions: 3,
          numberOfVariables: 2);

      IVector x = new Vector(new double[] { 1.0, 1.0 });
      lm.Minimize(ref x);

      Assert.True(lm.SuccessfullyConverged);
      Assert.Equal(2.0, x[0], precision: 4);
      Assert.Equal(3.0, x[1], precision: 4);
    }

    /// <summary>既存テストと同じ非線形問題を解ける</summary>
    /// <remarks>
    /// 既存テストより: 解は x[0]≈0.0434, x[1]≈0.2106
    /// </remarks>
    [Fact]
    public void Minimize_NonlinearSystem_ConvergesToKnownSolution()
    {
      LevenbergMarquardt.ErrorFunction eFnc =
          (IVector inputs, ref IVector outputs) =>
          {
            double x1 = inputs[0];
            double x2 = inputs[1];
            outputs[0] = x1 * x1 + 2 * x2 * x2;
            outputs[1] = -0.3 * Math.Cos(3.0 * Math.PI * x1
                      + 4 * Math.PI * x2)
                      * Math.Cos(4 * Math.PI * x2) + 0.3;
          };

      var lm = new LevenbergMarquardt(eFnc,
          numberOfFunctions: 2,
          numberOfVariables: 2);

      IVector x = new Vector(new double[] { 1.2, 1.3 });
      lm.Minimize(ref x);

      Assert.True(lm.SuccessfullyConverged);
      // 既存テストの期待値
      Assert.Equal(0.04335597048969896, x[0], precision: 4);
      Assert.Equal(0.21061107305129576, x[1], precision: 4);
    }

    /// <summary>収束後の出力ベクトルが取得できる</summary>
    [Fact]
    public void Minimize_AfterConvergence_OutputsAreAccessible()
    {
      LevenbergMarquardt.ErrorFunction eFnc =
          (IVector inputs, ref IVector outputs) =>
          {
            outputs[0] = inputs[0] - 1.0;
            outputs[1] = inputs[1] - 1.0;
          };

      var lm = new LevenbergMarquardt(eFnc, 2, 2);
      IVector x = new Vector(new double[] { 0.0, 0.0 });
      lm.Minimize(ref x);

      Assert.NotNull(lm.Outputs);
      Assert.Equal(2, lm.Outputs.Length);
    }

    #endregion
  }
}
