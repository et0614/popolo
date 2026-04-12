/* MinimizationTests.cs
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

        #region GoldenSection のテスト

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

        #endregion
    }
}
