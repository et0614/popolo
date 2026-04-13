/* MultiMinimizationTests.cs
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
using Popolo.Core.Numerics.LinearAlgebra;

namespace Popolo.Core.Tests.Numerics
{
    /// <summary>MultiMinimization のテスト</summary>
    public class MultiMinimizationTests
    {
        // f(x,y) = x^2 + y^2 → 最小値 0、最小点 (0,0)
        private static readonly MultiMinimization.MinimizeFunction Quadratic =
            (x, iter) => x[0] * x[0] + x[1] * x[1];

        // ローゼンブロック関数: f(x,y) = 100(y-x^2)^2 + (1-x)^2
        // → 最小値 0、最小点 (1,1)
        private static readonly MultiMinimization.MinimizeFunction Rosenbrock =
            (x, iter) => 100 * Math.Pow(x[1] - x[0] * x[0], 2) + Math.Pow(1 - x[0], 2);

        // Wood-Colville関数（既存テストより）
        // → 最小値 0、最小点 (1,1,1,1)
        private static readonly MultiMinimization.MinimizeFunction WoodColville =
            (x, iter) =>
                100 * Math.Pow(x[1] - x[0] * x[0], 2)
                + Math.Pow(1 - x[0], 2)
                + 90 * Math.Pow(x[3] - x[2] * x[2], 2)
                + Math.Pow(1 - x[2], 2)
                + 10.1 * (Math.Pow(x[1] - 1, 2) + Math.Pow(x[3] - 1, 2))
                + 19.8 * (x[1] - 1) * (x[3] - 1);

        #region QuasiNewton のテスト

        /// <summary>2次関数の最小点が原点になる</summary>
        [Fact]
        public void QuasiNewton_QuadraticFunction_FindsMinimumAtOrigin()
        {
            IVector x = new Vector(new double[] { 3.0, 4.0 });

            bool success = MultiMinimization.QuasiNewton(
                ref x, Quadratic, 400, 1e-5, 1e-5, 1e-4, out int iter);

            Assert.True(success);
            Assert.Equal(0.0, x[0], precision: 4);
            Assert.Equal(0.0, x[1], precision: 4);
        }

        /// <summary>ローゼンブロック関数の最小点が (1,1) になる</summary>
        [Fact]
        public void QuasiNewton_RosenbrockFunction_FindsMinimumAtOne()
        {
            IVector x = new Vector(new double[] { -1.0, 1.0 });

            bool success = MultiMinimization.QuasiNewton(
                ref x, Rosenbrock, 400, 1e-5, 1e-5, 1e-4, out int iter);

            Assert.True(success);
            Assert.Equal(1.0, x[0], precision: 3);
            Assert.Equal(1.0, x[1], precision: 3);
        }

        /// <summary>Wood-Colville関数の最小点が (1,1,1,1) になる（既存テストの再現）</summary>
        [Fact]
        public void QuasiNewton_WoodColvilleFunction_FindsMinimumAtOne()
        {
            IVector x = new Vector(new double[] { -3.0, -1.0, -3.0, -1.0 });

            bool success = MultiMinimization.QuasiNewton(
                ref x, WoodColville, 400, 1e-5, 1e-5, 1e-4, out int iter);

            Assert.True(success);
            // 関数値が十分小さいことを確認
            double fval = WoodColville(x, 0);
            Assert.InRange(fval, 0.0, 0.01);
        }

        /// <summary>最大反復回数を超えると false を返す</summary>
        [Fact]
        public void QuasiNewton_MaxIterationExceeded_ReturnsFalse()
        {
            IVector x = new Vector(new double[] { 10.0, 10.0 });

            bool success = MultiMinimization.QuasiNewton(
                ref x, Rosenbrock, 1, 1e-20, 1e-20, 1e-20, out _);

            Assert.False(success);
        }

        #endregion

        #region Newton のテスト

        /// <summary>2次関数の最小点が原点になる</summary>
        [Fact]
        public void Newton_QuadraticFunction_FindsMinimumAtOrigin()
        {
            IVector x = new Vector(new double[] { 3.0, 4.0 });

            bool success = MultiMinimization.Newton(
                ref x, Quadratic, 400, 1e-5, 1e-5, 1e-4, out int iter);

            Assert.True(success);
            Assert.Equal(0.0, x[0], precision: 4);
            Assert.Equal(0.0, x[1], precision: 4);
        }

        #endregion

    }
}
