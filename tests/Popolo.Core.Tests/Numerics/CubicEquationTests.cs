/* CubicEquationTests.cs
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
using Popolo.Core.Numerics;

namespace Popolo.Core.Tests.Numerics
{
    /// <summary>CubicEquation のテスト</summary>
    public class CubicEquationTests
    {
        /// <summary>
        /// 3つの実数根を持つ場合：x^3 - 6x^2 + 11x - 6 = 0
        /// 根は x=1, x=2, x=3
        /// </summary>
        [Fact]
        public void Solve_ThreeRealRoots_ReturnsCorrectRoots()
        {
            // x^3 - 6x^2 + 11x - 6 = (x-1)(x-2)(x-3)
            double[] a = { 1.0, -6.0, 11.0, -6.0 };

            CubicEquation.Solve(a, out double x0, out double x1, out double x2, out bool hasMultiSolution);

            Assert.True(hasMultiSolution);

            // 3つの根が 1, 2, 3 であることを確認（順序不定のため全根をソートして比較）
            double[] roots = { x0, x1, x2 };
            Array.Sort(roots);
            Assert.Equal(1.0, roots[0], precision: 6);
            Assert.Equal(2.0, roots[1], precision: 6);
            Assert.Equal(3.0, roots[2], precision: 6);
        }

        /// <summary>
        /// 実数根1つの場合：x^3 - 1 = 0
        /// 実数根は x=1
        /// </summary>
        [Fact]
        public void Solve_OneRealRoot_ReturnsCorrectRoot()
        {
            // x^3 - 1 = 0 → 実数根 x=1
            double[] a = { 1.0, 0.0, 0.0, -1.0 };

            CubicEquation.Solve(a, out double x0, out double x1, out double x2, out bool hasMultiSolution);

            Assert.False(hasMultiSolution);
            Assert.Equal(1.0, x0, precision: 6);
        }

        /// <summary>
        /// 3重根の場合：(x-2)^3 = x^3 - 6x^2 + 12x - 8 = 0
        /// 根は x=2 の3重根
        /// </summary>
        [Fact]
        public void Solve_TripleRoot_ReturnsSameRoot()
        {
            // (x-2)^3 = x^3 - 6x^2 + 12x - 8
            double[] a = { 1.0, -6.0, 12.0, -8.0 };

            CubicEquation.Solve(a, out double x0, out double x1, out double x2, out bool hasMultiSolution);

            Assert.False(hasMultiSolution);
            Assert.Equal(2.0, x0, precision: 6);
        }

        /// <summary>
        /// 各根が方程式を満たすことを確認（残差がほぼゼロ）
        /// </summary>
        [Fact]
        public void Solve_ThreeRealRoots_EachRootSatisfiesEquation()
        {
            double[] a = { 1.0, -6.0, 11.0, -6.0 };

            CubicEquation.Solve(a, out double x0, out double x1, out double x2, out bool _);

            Assert.Equal(0.0, Evaluate(a, x0), precision: 6);
            Assert.Equal(0.0, Evaluate(a, x1), precision: 6);
            Assert.Equal(0.0, Evaluate(a, x2), precision: 6);
        }

        /// <summary>
        /// 実数根1つの場合も根が方程式を満たすことを確認
        /// </summary>
        [Fact]
        public void Solve_OneRealRoot_RootSatisfiesEquation()
        {
            // x^3 + x + 2 = 0 → 実数根 x=-1
            double[] a = { 1.0, 0.0, 1.0, 2.0 };

            CubicEquation.Solve(a, out double x0, out double x1, out double x2, out bool hasMultiSolution);

            Assert.False(hasMultiSolution);
            Assert.Equal(0.0, Evaluate(a, x0), precision: 6);
        }

        /// <summary>係数が負の場合も正しく解ける</summary>
        [Fact]
        public void Solve_NegativeLeadingCoefficient_ReturnsCorrectRoots()
        {
            // -(x-1)(x-2)(x-3) = -x^3 + 6x^2 - 11x + 6
            double[] a = { -1.0, 6.0, -11.0, 6.0 };

            CubicEquation.Solve(a, out double x0, out double x1, out double x2, out bool hasMultiSolution);

            Assert.True(hasMultiSolution);

            double[] roots = { x0, x1, x2 };
            Array.Sort(roots);
            Assert.Equal(1.0, roots[0], precision: 6);
            Assert.Equal(2.0, roots[1], precision: 6);
            Assert.Equal(3.0, roots[2], precision: 6);
        }

        /// <summary>3次方程式を評価するヘルパーメソッド</summary>
        private static double Evaluate(double[] a, double x)
        {
            return a[0] * x * x * x + a[1] * x * x + a[2] * x + a[3];
        }
    }
}
