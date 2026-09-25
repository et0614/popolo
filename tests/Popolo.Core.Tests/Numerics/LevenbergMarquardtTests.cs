/* LevenbergMarquardtTests.cs
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
using Popolo.Core.Numerics.LinearAlgebra;
using Popolo.Core.Exceptions;

namespace Popolo.Core.Tests.Numerics
{
  /// <summary>LevenbergMarquardt のテスト</summary>
  public class LevenbergMarquardtTests
  {
    #region Constructor tests

    /// <summary>numberOfFunctions が numberOfVariables より小さい場合に例外が発生する</summary>
    [Fact]
    public void Constructor_FunctionsLessThanVariables_ThrowsPopoloArgumentException()
    {
      LevenbergMarquardt.ErrorFunction eFnc =
          (IVector inputs, ref IVector outputs) => { };

      var ex = Assert.Throws<PopoloArgumentException>(
          () => new LevenbergMarquardt(eFnc,
              functionCount: 1,
              variableCount: 2));
      Assert.Equal("functionCount", ex.ParamName);
    }

    /// <summary>numberOfFunctions == numberOfVariables は正常に作成できる</summary>
    [Fact]
    public void Constructor_FunctionsEqualsVariables_Succeeds()
    {
      LevenbergMarquardt.ErrorFunction eFnc =
          (IVector inputs, ref IVector outputs) => { };

      var lm = new LevenbergMarquardt(eFnc,
          functionCount: 2,
          variableCount: 2);

      Assert.Equal(2, lm.FunctionCount);
      Assert.Equal(2, lm.VariableCount);
    }

    #endregion

    #region Minimize tests

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
          functionCount: 3,
          variableCount: 2);

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
          functionCount: 2,
          variableCount: 2);

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

    /// <summary>1変数の当てはめで、解でない初期値から解へ移動する</summary>
    /// <remarks>
    /// 勾配ノルムの計算で対角項 (MINPACK の i = j) を落とすと、1変数問題では
    /// 勾配ノルムが常に 0 となり、初期値のまま info=4 で終了してしまう。
    /// </remarks>
    [Fact]
    public void Minimize_SingleVariableFit_MovesFromNonSolutionStart()
    {
      // r_i = x - c_i (c = 1, 2, 3) → 最小二乗解は x = 2
      LevenbergMarquardt.ErrorFunction eFnc =
          (IVector inputs, ref IVector outputs) =>
          {
            outputs[0] = inputs[0] - 1.0;
            outputs[1] = inputs[0] - 2.0;
            outputs[2] = inputs[0] - 3.0;
          };

      var lm = new LevenbergMarquardt(eFnc, functionCount: 3, variableCount: 1);
      IVector x = new Vector(new double[] { 10.0 });
      lm.Minimize(ref x);

      Assert.True(lm.SuccessfullyConverged);
      Assert.Equal(2.0, x[0], precision: 5);
    }

    /// <summary>非線形の1変数当てはめ（指数減衰の時定数）が解に収束する</summary>
    [Fact]
    public void Minimize_SingleVariableExponentialFit_ConvergesToTrueParameter()
    {
      // y = exp(-t / 2) のデータから時定数 τ = 2 を推定する
      double[] t = { 0.0, 0.5, 1.0, 2.0, 3.0, 5.0 };
      LevenbergMarquardt.ErrorFunction eFnc =
          (IVector inputs, ref IVector outputs) =>
          {
            for (int i = 0; i < t.Length; i++)
              outputs[i] = Math.Exp(-t[i] / inputs[0]) - Math.Exp(-t[i] / 2.0);
          };

      var lm = new LevenbergMarquardt(eFnc, functionCount: t.Length, variableCount: 1);
      IVector x = new Vector(new double[] { 1.0 });
      lm.Minimize(ref x);

      Assert.True(lm.SuccessfullyConverged);
      Assert.Equal(2.0, x[0], precision: 4);
    }

    /// <summary>初期値が厳密解（残差ゼロ）の場合は収束成功と判定される</summary>
    /// <remarks>MINPACK では info=4（勾配条件）も正常収束として扱う。</remarks>
    [Fact]
    public void Minimize_ZeroResidualAtStart_ReportsConverged()
    {
      LevenbergMarquardt.ErrorFunction eFnc =
          (IVector inputs, ref IVector outputs) =>
          {
            outputs[0] = inputs[0] - 2.0;
            outputs[1] = inputs[1] - 3.0;
          };

      var lm = new LevenbergMarquardt(eFnc, 2, 2);
      IVector x = new Vector(new double[] { 2.0, 3.0 });
      lm.Minimize(ref x);

      Assert.True(lm.SuccessfullyConverged);
      Assert.Equal(2.0, x[0], precision: 10);
      Assert.Equal(3.0, x[1], precision: 10);
    }

    /// <summary>Rosenbrock 問題（残差形式）が (1,1) に収束し、収束成功と判定される</summary>
    [Fact]
    public void Minimize_Rosenbrock_ConvergesAndReportsSuccess()
    {
      // r0 = 10 (x1 - x0^2), r1 = 1 - x0
      LevenbergMarquardt.ErrorFunction eFnc =
          (IVector inputs, ref IVector outputs) =>
          {
            outputs[0] = 10.0 * (inputs[1] - inputs[0] * inputs[0]);
            outputs[1] = 1.0 - inputs[0];
          };

      var lm = new LevenbergMarquardt(eFnc, 2, 2);
      IVector x = new Vector(new double[] { -1.2, 1.0 });
      lm.Minimize(ref x);

      Assert.True(lm.SuccessfullyConverged);
      Assert.Equal(1.0, x[0], precision: 4);
      Assert.Equal(1.0, x[1], precision: 4);
    }

    #endregion
  }
}
