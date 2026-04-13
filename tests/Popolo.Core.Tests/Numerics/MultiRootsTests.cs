/* MultiRootsTests.cs
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
using Popolo.Core.Exceptions;

namespace Popolo.Core.Tests.Numerics
{
    /// <summary>MultiRoots のテスト</summary>
    public class MultiRootsTests
    {
        // 線形方程式 f(x) = Ax - b = 0
        // A = [[2,1],[1,3]], b = [5,10] → 解: x=[1,3]
        private static readonly MultiRoots.ErrorFunction LinearSystem = (IVector x, ref IVector fx) =>
        {
            fx[0] = 2 * x[0] + x[1] - 5;
            fx[1] = x[0] + 3 * x[1] - 10;
        };

        // 非線形方程式
        // f0 = x^2 + y^2 - 4 = 0  (円)
        // f1 = x - y = 0           (対角線)
        // → 解: x=y=√2 ≈ 1.4142
        private static readonly MultiRoots.ErrorFunction CircleAndLine = (IVector x, ref IVector fx) =>
        {
            fx[0] = x[0] * x[0] + x[1] * x[1] - 4.0;
            fx[1] = x[0] - x[1];
        };

        #region Newton (基本版) のテスト

        /// <summary>線形方程式を正しく解ける</summary>
        [Fact]
        public void Newton_LinearSystem_ConvergesAndReturnsCorrectRoot()
        {
            IVector x = new Vector(new double[] { 0.0, 0.0 });

            bool success = MultiRoots.Newton(
                LinearSystem, ref x,
                errorTolerance: 1e-8,
                collectionTolerance: 1e-8,
                maxIteration: 100,
                iteration: out int iter,
                error: out double err);

            Assert.True(success);
            Assert.Equal(1.0, x[0], precision: 6);
            Assert.Equal(3.0, x[1], precision: 6);
            Assert.True(err < 1e-8);
        }

        /// <summary>非線形方程式（円と直線の交点）を正しく解ける</summary>
        [Fact]
        public void Newton_NonlinearSystem_ConvergesAndReturnsCorrectRoot()
        {
            IVector x = new Vector(new double[] { 1.0, 1.0 });

            bool success = MultiRoots.Newton(
                CircleAndLine, ref x,
                errorTolerance: 1e-8,
                collectionTolerance: 1e-8,
                maxIteration: 100,
                iteration: out _,
                error: out _);

            Assert.True(success);
            Assert.Equal(Math.Sqrt(2.0), x[0], precision: 6);
            Assert.Equal(Math.Sqrt(2.0), x[1], precision: 6);
        }

        /// <summary>最大反復回数を超えると false を返す</summary>
        [Fact]
        public void Newton_MaxIterationExceeded_ReturnsFalse()
        {
            IVector x = new Vector(new double[] { 0.0, 0.0 });

            bool success = MultiRoots.Newton(
                LinearSystem, ref x,
                errorTolerance: 1e-20,  // 達成不可能な許容誤差
                collectionTolerance: 1e-20,
                maxIteration: 1,        // 反復回数を1に制限
                iteration: out _,
                error: out _);

            Assert.False(success);
        }

        /// <summary>x が null のとき PopoloArgumentException が発生する</summary>
        [Fact]
        public void Newton_NullX_ThrowsPopoloArgumentException()
        {
            IVector? x = null;
            var ex = Assert.Throws<PopoloArgumentException>(
                () => MultiRoots.Newton(
                    LinearSystem, ref x!,
                    1e-8, 1e-8, 100,
                    out _, out _));
            Assert.Equal("x", ex.ParamName);
        }

        #endregion

        #region Newton (振動防止係数付き) のテスト

        /// <summary>振動防止係数付きで線形方程式を正しく解ける</summary>
        [Fact]
        public void Newton_WithAntiVibration_ConvergesAndReturnsCorrectRoot()
        {
            IVector x = new Vector(new double[] { 0.0, 0.0 });

            bool success = MultiRoots.Newton(
                LinearSystem, ref x,
                errorTolerance: 1e-8,
                collectionTolerance: 1e-8,
                maxIteration: 200,
                antiVibrationC: 0.5,
                iteration: out _,
                error: out _);

            Assert.True(success);
            Assert.Equal(1.0, x[0], precision: 6);
            Assert.Equal(3.0, x[1], precision: 6);
        }

        /// <summary>antiVibrationC が範囲外のとき PopoloArgumentException が発生する</summary>
        [Theory]
        [InlineData(0.0)]
        [InlineData(-0.1)]
        [InlineData(1.1)]
        public void Newton_InvalidAntiVibrationC_ThrowsPopoloArgumentException(
            double antiVibrationC)
        {
            IVector x = new Vector(new double[] { 0.0, 0.0 });
            var ex = Assert.Throws<PopoloArgumentException>(
                () => MultiRoots.Newton(
                    LinearSystem, ref x,
                    1e-8, 1e-8, 100, antiVibrationC,
                    out _, out _));
            Assert.Equal("antiVibrationC", ex.ParamName);
        }

        #endregion
    }
}
