/* SpecialFunctionsTests.cs
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
using Popolo.Core.Exceptions;

namespace Popolo.Core.Tests.Numerics
{
    /// <summary>SpecialFunctions のテスト</summary>
    public class SpecialFunctionsTests
    {

        #region GammaP のテスト

        /// <summary>GammaP(a=1, x=0) = 0 となる</summary>
        [Fact]
        public void GammaP_WhenXIsZero_ReturnsZero()
        {
            double result = SpecialFunctions.GammaP(1.0, 0.0);
            Assert.Equal(0.0, result, precision: 10);
        }

        /// <summary>GammaP(a=1, x) = 1 - exp(-x) となる（指数分布の累積分布関数）</summary>
        [Theory]
        [InlineData(1.0, 1.0, 0.6321205588285578)]
        [InlineData(1.0, 2.0, 0.8646647167633873)]
        [InlineData(1.0, 5.0, 0.9932620530009145)]
        [InlineData(2.0, 1.0, 0.2642411176571153)]
        [InlineData(2.0, 3.0, 0.8008517265285442)]
        public void GammaP_KnownValues_ReturnsCorrectResult(
            double a, double x, double expected)
        {
            double result = SpecialFunctions.GammaP(a, x);
            Assert.Equal(expected, result, precision: 6);
        }

        /// <summary>GammaP + GammaQ = 1 となる（補完関係）</summary>
        [Theory]
        [InlineData(1.0, 1.0)]
        [InlineData(2.0, 3.0)]
        [InlineData(5.0, 2.0)]
        public void GammaP_PlusGammaQ_EqualsOne(double a, double x)
        {
            double p = SpecialFunctions.GammaP(a, x);
            double q = SpecialFunctions.GammaQ(a, x);
            Assert.Equal(1.0, p + q, precision: 6);
        }

        /// <summary>a が0以下のとき PopoloArgumentException が発生する</summary>
        [Theory]
        [InlineData(0.0, 1.0)]
        [InlineData(-1.0, 1.0)]
        public void GammaP_WhenAIsNotPositive_ThrowsPopoloArgumentException(
            double a, double x)
        {
            var ex = Assert.Throws<PopoloArgumentException>(
                () => SpecialFunctions.GammaP(a, x));
            Assert.Equal("a", ex.ParamName);
        }

        /// <summary>x が負のとき PopoloArgumentException が発生する</summary>
        [Fact]
        public void GammaP_WhenXIsNegative_ThrowsPopoloArgumentException()
        {
            var ex = Assert.Throws<PopoloArgumentException>(
                () => SpecialFunctions.GammaP(1.0, -1.0));
            Assert.Equal("x", ex.ParamName);
        }

        #endregion

        #region GammaQ のテスト

        /// <summary>GammaQ(a=1, x) = exp(-x) となる（指数分布の生存関数）</summary>
        [Theory]
        [InlineData(1.0, 1.0, 0.36787944117144233)]
        [InlineData(1.0, 2.0, 0.13533528323661270)]
        [InlineData(1.0, 5.0, 0.006737946999085467)]
        public void GammaQ_KnownValues_ReturnsCorrectResult(
            double a, double x, double expected)
        {
            double result = SpecialFunctions.GammaQ(a, x);
            Assert.Equal(expected, result, precision: 6);
        }

        /// <summary>GammaQ(a, 0) = 1 となる</summary>
        [Fact]
        public void GammaQ_WhenXIsZero_ReturnsOne()
        {
            double result = SpecialFunctions.GammaQ(1.0, 0.0);
            Assert.Equal(1.0, result, precision: 10);
        }

        /// <summary>a が0以下のとき PopoloArgumentException が発生する</summary>
        [Fact]
        public void GammaQ_WhenAIsNotPositive_ThrowsPopoloArgumentException()
        {
            var ex = Assert.Throws<PopoloArgumentException>(
                () => SpecialFunctions.GammaQ(0.0, 1.0));
            Assert.Equal("a", ex.ParamName);
        }

        /// <summary>x が負のとき PopoloArgumentException が発生する</summary>
        [Fact]
        public void GammaQ_WhenXIsNegative_ThrowsPopoloArgumentException()
        {
            var ex = Assert.Throws<PopoloArgumentException>(
                () => SpecialFunctions.GammaQ(1.0, -1.0));
            Assert.Equal("x", ex.ParamName);
        }

        #endregion

        #region ComplementaryErrorFunction のテスト

        /// <summary>erfc(0) = 1 となる</summary>
        [Fact]
        public void ComplementaryErrorFunction_WhenXIsZero_ReturnsOne()
        {
            double result = SpecialFunctions.ComplementaryErrorFunction(0.0);
            Assert.Equal(1.0, result, precision: 10);
        }

        /// <summary>erfc(x) の既知の値と一致する</summary>
        [Theory]
        [InlineData(1.0, 0.15729920705028513)]
        [InlineData(2.0, 0.004677734981047265)]
        [InlineData(-1.0, 1.8427007929497149)]
        public void ComplementaryErrorFunction_KnownValues_ReturnsCorrectResult(
            double x, double expected)
        {
            double result = SpecialFunctions.ComplementaryErrorFunction(x);
            Assert.Equal(expected, result, precision: 6);
        }

        /// <summary>erfc(x) + erfc(-x) = 2 となる（対称性）</summary>
        [Theory]
        [InlineData(0.5)]
        [InlineData(1.0)]
        [InlineData(2.0)]
        public void ComplementaryErrorFunction_Symmetry_SumsToTwo(double x)
        {
            double pos = SpecialFunctions.ComplementaryErrorFunction(x);
            double neg = SpecialFunctions.ComplementaryErrorFunction(-x);
            Assert.Equal(2.0, pos + neg, precision: 6);
        }

        #endregion

    }
}
