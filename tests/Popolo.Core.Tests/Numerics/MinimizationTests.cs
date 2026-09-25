/* MinimizationTests.cs
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
    /// <summary>Minimization のテスト</summary>
    public class MinimizationTests
    {
        // 既存テストより：温度に関する多項式の最小化
        // f(x) = pct(x)*15 + ptb(x)*70
        // 解は約 x=24.17、最小値は約 60.136
        private static readonly Minimization.MinimizeFunction TemperatureFunction = wTemp =>
        {
            double pct = 3.4773e2 + wTemp * (-6.4390e1 + wTemp * (4.775 + wTemp *
                (-1.768e-1 + wTemp * (3.2651e-3 + wTemp * (-2.4048e-5)))));
            double ptb = 4.1472e-1 + wTemp * (5.1299e-3 + wTemp * (4.1126e-4));
            return pct * 15 + ptb * 70;
        };

        // 単純な2次関数 f(x) = (x-3)^2 → 最小値 0、極小点 x=3
        private static readonly Minimization.MinimizeFunction QuadraticFunction =
            x => (x - 3.0) * (x - 3.0);

        // sin関数 f(x) = sin(x) → [π, 2π] での最小値 -1、極小点 x=3π/2
        private static readonly Minimization.MinimizeFunction SinFunction =
            x => Math.Sin(x);

        #region GoldenSection tests

        /// <summary>温度関数の最小値が既存テストの値と一致する（既存テストの再現）</summary>
        [Fact]
        public void GoldenSection_TemperatureFunction_ReturnsCorrectMinimum()
        {
            double xMin = 20.0;
            double result = Minimization.GoldenSection(ref xMin, 32.0, TemperatureFunction);

            // 既存テストより：最小値は約 60.136
            Assert.Equal(60.135662745472565, result, precision: 6);
        }

        /// <summary>極小点のx値が正しく更新される</summary>
        [Fact]
        public void GoldenSection_TemperatureFunction_UpdatesXMin()
        {
            double xMin = 20.0;
            Minimization.GoldenSection(ref xMin, 32.0, TemperatureFunction);

            // 極小点は範囲内にあることを確認
            Assert.InRange(xMin, 20.0, 32.0);
            // 極小点での関数値が最小値と一致することを確認
            Assert.Equal(TemperatureFunction(xMin), 
                Minimization.GoldenSection(ref xMin, 32.0, TemperatureFunction), 
                precision: 3);
        }

        /// <summary>sin関数の最小値が正しく求まる</summary>
        [Fact]
        public void GoldenSection_SinFunction_ReturnsCorrectMinimum()
        {
            // sin(x) の [π, 2π] での最小値 = -1（x = 3π/2）
            double xMin = Math.PI;
            double result = Minimization.GoldenSection(ref xMin, 2.0 * Math.PI, SinFunction);

            Assert.Equal(-1.0, result, precision: 4);
            Assert.Equal(3.0 * Math.PI / 2.0, xMin, precision: 3);
        }

        /// <summary>searchOutside=false のとき通常の黄金分割法と同じ結果になる</summary>
        [Fact]
        public void GoldenSection_WithSearchOutsideFalse_SameAsBasicGoldenSection()
        {
            double xMin1 = 20.0;
            double xMin2 = 20.0;

            double result1 = Minimization.GoldenSection(ref xMin1, 32.0, TemperatureFunction);
            double result2 = Minimization.GoldenSection(ref xMin2, 32.0, TemperatureFunction, false);

            Assert.Equal(result1, result2, precision: 6);
        }

        /// <summary>searchOutside=true のとき範囲外にも探索が広がる</summary>
        [Fact]
        public void GoldenSection_WithSearchOutsideTrue_FindsMinimumOutsideInitialRange()
        {
            // f(x) = (x-10)^2 の最小点は x=10 で初期範囲 [0,5] の外にある
            Minimization.MinimizeFunction f = x => (x - 10.0) * (x - 10.0);
            double x1 = 0.0;

            double result = Minimization.GoldenSection(ref x1, 5.0, f, true);

            // 最小値はほぼ 0（x=10付近）
            Assert.Equal(0.0, result, precision: 2);
        }

        /// <summary>対称な試行点で関数値が等しくなっても極小点を通り過ぎない</summary>
        /// <remarks>
        /// (t−25)² を [10,40] で最小化すると、最初の2つの試行点 21.46 と 28.54 が
        /// 極小点に対して対称となり関数値が一致する。等値時に極小点側を捨てる
        /// 更新規則では 28.54 を返していた。
        /// </remarks>
        [Fact]
        public void GoldenSection_SymmetricTie_FindsTrueMinimum()
        {
            Minimization.MinimizeFunction f = t => (t - 25.0) * (t - 25.0);
            double xMin = 10.0;

            double result = Minimization.GoldenSection(ref xMin, 40.0, f);

            Assert.Equal(25.0, xMin, precision: 3);
            Assert.Equal(0.0, result, precision: 6);
        }

        /// <summary>下限と上限を逆に与えても正しく最小化できる</summary>
        [Fact]
        public void GoldenSection_ReversedBounds_FindsTrueMinimum()
        {
            Minimization.MinimizeFunction f = t => (t - 17.0) * (t - 17.0);
            double xMin = 40.0;

            double result = Minimization.GoldenSection(ref xMin, 10.0, f);

            Assert.Equal(17.0, xMin, precision: 3);
            Assert.Equal(0.0, result, precision: 6);
        }

        /// <summary>逆順の範囲でも対称な等値ケースで極小点を返す</summary>
        [Fact]
        public void GoldenSection_ReversedBoundsSymmetricTie_FindsTrueMinimum()
        {
            Minimization.MinimizeFunction f = t => (t - 25.0) * (t - 25.0);
            double xMin = 40.0;

            Minimization.GoldenSection(ref xMin, 10.0, f);

            Assert.Equal(25.0, xMin, precision: 3);
        }

        /// <summary>左側が平坦な関数では平坦部の右端（変化点）を返す（従来挙動の維持）</summary>
        /// <remarks>
        /// 平坦部で3点の関数値が等しい場合は平坦部の端へ探索を進める。
        /// AirHandlingUnit.OptimizeVAV など、この挙動に依存する呼び出し元がある。
        /// </remarks>
        [Fact]
        public void GoldenSection_LeftPlateau_ReturnsPlateauEdge()
        {
            Minimization.MinimizeFunction f = t => Math.Max(t - 20.0, 0.0);
            double xMin = 10.0;

            double result = Minimization.GoldenSection(ref xMin, 40.0, f);

            Assert.Equal(0.0, result);
            Assert.Equal(20.0, xMin, precision: 3);
        }

        /// <summary>右側が平坦な関数では平坦部の左端（変化点）を返す（従来挙動の維持）</summary>
        [Fact]
        public void GoldenSection_RightPlateau_ReturnsPlateauEdge()
        {
            Minimization.MinimizeFunction f = t => Math.Max(20.0 - t, 0.0);
            double xMin = 10.0;

            double result = Minimization.GoldenSection(ref xMin, 40.0, f);

            Assert.Equal(0.0, result);
            Assert.Equal(20.0, xMin, precision: 3);
        }

        /// <summary>単調関数では区間端の近くを返す</summary>
        [Theory]
        [InlineData(1.0, 10.0)]
        [InlineData(-1.0, 40.0)]
        public void GoldenSection_MonotonicFunction_ReturnsBoundary(double slope, double expected)
        {
            Minimization.MinimizeFunction f = t => slope * t;
            double xMin = 10.0;

            Minimization.GoldenSection(ref xMin, 40.0, f);

            Assert.Equal(expected, xMin, precision: 3);
        }

        #endregion
    }
}
