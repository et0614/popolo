/* MultiMinimizationTests.cs
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

        #region QuasiNewton tests

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

        /// <summary>定数オフセットをもつローゼンブロック関数でも収束判定が成立する</summary>
        /// <remarks>
        /// 相対変化が小さいときに収束判定へ進むべきところ、判定条件が逆転していると
        /// 相対変化が大きい場合にしか収束判定が行われず、収束しても失敗と判定される。
        /// </remarks>
        [Fact]
        public void QuasiNewton_OffsetRosenbrock_Converges()
        {
            MultiMinimization.MinimizeFunction f = (x, iter) => 100.0 + Rosenbrock(x, iter);
            IVector x = new Vector(new double[] { -1.0, 1.0 });

            bool success = MultiMinimization.QuasiNewton(
                ref x, f, 400, 1e-5, 1e-5, 1e-4, out int iter);

            Assert.True(success);
            Assert.Equal(1.0, x[0], precision: 3);
            Assert.Equal(1.0, x[1], precision: 3);
        }

        /// <summary>大きな定数オフセットをもつ2次関数でも収束判定が成立する</summary>
        /// <remarks>
        /// オフセットが 1e6 だと数値勾配が丸めにより厳密に 0 となる。勾配 0 の停留点では
        /// 探索方向が得られないため、反復上限まで空回りせずに収束として終了する。
        /// </remarks>
        [Fact]
        public void QuasiNewton_LargeOffsetQuadratic_Converges()
        {
            MultiMinimization.MinimizeFunction f =
                (x, iter) => 1e6 + Math.Pow(x[0] - 1, 2) + Math.Pow(x[1] - 2, 2);
            IVector x = new Vector(new double[] { 0.0, 0.0 });

            bool success = MultiMinimization.QuasiNewton(
                ref x, f, 400, 1e-5, 1e-5, 1e-4, out int iter);

            Assert.True(success);
            Assert.Equal(1.0, x[0], precision: 3);
            Assert.Equal(2.0, x[1], precision: 3);
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

        #region Newton tests

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
