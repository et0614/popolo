/* RootsTests.cs
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
    /// <summary>Roots のテスト</summary>
    public class RootsTests
    {
        // 既存テストで使用していたポンプ特性・配管抵抗の誤差関数
        // 解は約 66.604
        private static readonly Roots.ErrorFunction PumpPipeFunction = mw =>
        {
            double p1 = mw * (-0.314 + mw * (2.08e-3 + mw * (-4.9e-6 - 1.32e-6 * mw))) + 333;
            double p2 = mw * (0.567 + 4.66e-2 * mw) + 49.4;
            return p1 - p2;
        };

        // sin(x) = 0 の解（x=π付近）
        private static readonly Roots.ErrorFunction SinFunction = x => Math.Sin(x);

        // x^2 - 2 = 0 の解（√2 ≈ 1.41421）
        private static readonly Roots.ErrorFunction SqrtTwoFunction = x => x * x - 2.0;

        #region Bisection method tests

        /// <summary>二分法でポンプ特性の解が正しく求まる</summary>
        [Fact]
        public void Bisection_PumpPipeFunction_ReturnsCorrectRoot()
        {
            double result = Roots.Bisection(PumpPipeFunction, 0, 100, 0.001, 0.00001, 20);
            Assert.Equal(0.0, PumpPipeFunction(result), precision: 3);
        }

        /// <summary>二分法で√2が正しく求まる</summary>
        [Fact]
        public void Bisection_SqrtTwo_ReturnsCorrectRoot()
        {
            double result = Roots.Bisection(SqrtTwoFunction, 1.0, 2.0, 1e-10, 1e-10, 100);
            Assert.Equal(Math.Sqrt(2.0), result, precision: 8);
        }

        /// <summary>根が囲い込まれていない場合に PopoloArgumentException が発生する</summary>
        [Fact]
        public void Bisection_RootNotBracketed_ThrowsPopoloArgumentException()
        {
            // f(1)=1, f(2)=4 → 同符号なので囲い込めていない
            Assert.Throws<PopoloArgumentException>(
                () => Roots.Bisection(SqrtTwoFunction, 1.5, 2.0, 1e-6, 1e-6, 100));
        }

        /// <summary>最大反復回数超過で PopoloNumericalException が発生する</summary>
        [Fact]
        public void Bisection_MaxIterationExceeded_ThrowsPopoloNumericalException()
        {
            // 許容誤差を極小・反復回数を1に設定して強制的に収束失敗させる
            Assert.Throws<PopoloNumericalException>(
                () => Roots.Bisection(SqrtTwoFunction, 1.0, 2.0, 1e-20, 1e-20, 1));
        }

    #endregion

    #region Brent's method tests

    /// <summary>Brent法でポンプ特性の解が正しく求まる</summary>
    [Fact]
    public void Brent_PumpPipeFunction_ReturnsCorrectRoot()
    {
      double result = Roots.Brent(0, 100, 1e-8, PumpPipeFunction);
      Assert.Equal(0.0, PumpPipeFunction(result), precision: 6);
    }

    /// <summary>Brent法でsin(x)=0の解（x=π）が正しく求まる</summary>
    [Fact]
        public void Brent_SinFunction_ReturnsCorrectRoot()
        {
            double result = Roots.Brent(2.0, 4.0, 1e-10, SinFunction);
            Assert.Equal(Math.PI, result, precision: 8);
        }

        /// <summary>Brent法で√2が正しく求まる</summary>
        [Fact]
        public void Brent_SqrtTwo_ReturnsCorrectRoot()
        {
            double result = Roots.Brent(1.0, 2.0, 1e-10, SqrtTwoFunction);
            Assert.Equal(Math.Sqrt(2.0), result, precision: 8);
        }

        /// <summary>根が囲い込まれていない場合、Brent法は PopoloArgumentException を投げる（黙って非解を返さない）</summary>
        [Fact]
        public void Brent_RootNotBracketed_ThrowsPopoloArgumentException()
        {
            // f(1.5)=0.25, f(2)=2 → 同符号
            Assert.Throws<PopoloArgumentException>(
                () => Roots.Brent(1.5, 2.0, 1e-10, SqrtTwoFunction));
        }

        /// <summary>重根（区間が片側からしか縮まない難しいケース）でも許容誤差内に収束する</summary>
        [Theory]
        [InlineData(3)]
        [InlineData(5)]
        public void Brent_MultipleRoot_Converges(int multiplicity)
        {
            double result = Roots.Brent(0.0, 3.0, 1e-10, x => Math.Pow(x - 1.0, multiplicity));
            Assert.True(Math.Abs(result - 1.0) < 1e-9, $"root={result}");
        }

        /// <summary>不連続な関数でも符号変化点を許容誤差内で囲い込む</summary>
        [Fact]
        public void Brent_StepFunction_LocatesSignChange()
        {
            double result = Roots.Brent(0.0, 1.0, 1e-8, x => x < 0.3 ? -1.0 : 1.0);
            Assert.True(Math.Abs(result - 0.3) < 2e-8, $"root={result}");
        }

        /// <summary>端点の値を与える多重定義は、関数を端点で再評価せずに同じ解を返す</summary>
        [Fact]
        public void Brent_WithEndValues_DoesNotReevaluateEndpoints()
        {
            int calls = 0;
            Roots.ErrorFunction f = x => { calls++; return x * x - 2.0; };
            double result = Roots.Brent(f, 1.0, 2.0, -1.0, 2.0, 1e-10);
            Assert.Equal(Math.Sqrt(2.0), result, precision: 8);
            // 端点(1.0, 2.0)を評価しないため、通常版より2回少ない
            int callsWith = calls;
            calls = 0;
            Roots.Brent(1.0, 2.0, 1e-10, f);
            Assert.Equal(calls - 2, callsWith);
        }

        #endregion

        #region Newton's method tests

        /// <summary>ニュートン法（数値微分）でポンプ特性の解が正しく求まる</summary>
        [Fact]
        public void Newton_Numerical_PumpPipeFunction_ReturnsCorrectRoot()
        {
            double result = Roots.Newton(PumpPipeFunction, 0, 0.0001, 0.001, 0.001, 100);
            Assert.Equal(0.0, PumpPipeFunction(result), precision: 3);
        }

        /// <summary>ニュートン法（解析的微分）でポンプ特性の解が正しく求まる</summary>
        [Fact]
        public void Newton_Analytical_SqrtTwo_ReturnsCorrectRoot()
        {
            // f(x) = x^2 - 2, f'(x) = 2x
            Roots.ErrorFunction f = x => x * x - 2.0;
            Roots.ErrorFunction df = x => 2.0 * x;

            double result = Roots.Newton(f, df, 1.5, 1e-10, 1e-10, 100);
            Assert.Equal(Math.Sqrt(2.0), result, precision: 8);
        }

        /// <summary>最大反復回数超過で PopoloNumericalException が発生する</summary>
        [Fact]
        public void Newton_MaxIterationExceeded_ThrowsPopoloNumericalException()
        {
            // 発散する関数で強制的に収束失敗させる
            Roots.ErrorFunction diverging = x => Math.Exp(x);
            Assert.Throws<PopoloNumericalException>(
                () => Roots.Newton(diverging, 0.0, 0.001, 1e-10, 1e-10, 5));
        }

    #endregion

    #region Newton-bisection hybrid tests

    /// <summary>ニュートン・二分法でポンプ特性の解が正しく求まる</summary>
    [Fact]
    public void NewtonBisection_PumpPipeFunction_ReturnsCorrectRoot()
    {
      double result = Roots.NewtonBisection(PumpPipeFunction, 0, 0.0001, 1e-8, 1e-8, 100);
      Assert.Equal(0.0, PumpPipeFunction(result), precision: 6);
    }

    /// <summary>NewtonBisection で二分法へのフォールバックが正しく動作する</summary>
    [Fact]
        public void NewtonBisection_SqrtTwo_ReturnsCorrectRoot()
        {
            double result = Roots.NewtonBisection(SqrtTwoFunction, 1.0, 0.001, 1e-8, 1e-8, 100);
            Assert.Equal(Math.Sqrt(2.0), result, precision: 6);
        }

        #endregion
    }
}
