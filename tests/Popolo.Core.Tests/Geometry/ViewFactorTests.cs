/* ViewFactorTests.cs
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
using Popolo.Core.Geometry;
using Popolo.Core.Exceptions;

namespace Popolo.Core.Tests.Geometry
{
    /// <summary>ViewFactor のテスト</summary>
    public class ViewFactorTests
    {

        #region GetViewFactorParallelRectangles のテスト

        /// <summary>距離0のとき形態係数は1（完全対向）</summary>
        [Fact]
        public void GetViewFactorParallelRectangles_ZeroDistance_ReturnsOne()
        {
            double f = ViewFactor.GetViewFactorParallelRectangles(1.0, 1.0, 0.0);
            Assert.Equal(1.0, f, precision: 10);
        }

        /// <summary>幅または高さが0のとき形態係数は0</summary>
        [Theory]
        [InlineData(0.0, 1.0, 1.0)]
        [InlineData(1.0, 0.0, 1.0)]
        public void GetViewFactorParallelRectangles_ZeroDimension_ReturnsZero(
            double width, double height, double distance)
        {
            Assert.Equal(0.0, ViewFactor.GetViewFactorParallelRectangles(width, height, distance));
        }

        /// <summary>1x1の正方形が距離1で対向する形態係数は約0.1998（文献値）</summary>
        [Fact]
        public void GetViewFactorParallelRectangles_UnitSquareUnitDistance_MatchesLiterature()
        {
            double f = ViewFactor.GetViewFactorParallelRectangles(1.0, 1.0, 1.0);
            Assert.Equal(0.1998, f, precision: 3);
        }

        /// <summary>距離が大きくなると形態係数は0に近づく</summary>
        [Fact]
        public void GetViewFactorParallelRectangles_LargeDistance_ApproachesZero()
        {
            double f = ViewFactor.GetViewFactorParallelRectangles(1.0, 1.0, 1000.0);
            Assert.InRange(f, 0.0, 0.001);
        }

        /// <summary>形態係数は常に0以上1以下</summary>
        [Theory]
        [InlineData(1.0, 1.0, 0.5)]
        [InlineData(2.0, 3.0, 1.0)]
        [InlineData(0.5, 0.5, 2.0)]
        public void GetViewFactorParallelRectangles_AlwaysInRange(
            double width, double height, double distance)
        {
            double f = ViewFactor.GetViewFactorParallelRectangles(width, height, distance);
            Assert.InRange(f, 0.0, 1.0);
        }

        #endregion

        #region GetViewFactorPerpendicularRectangles のテスト

        /// <summary>depth=∞のとき形態係数は0.5</summary>
        [Fact]
        public void GetViewFactorPerpendicularRectangles_InfiniteDepth_ReturnsHalf()
        {
            double f = ViewFactor.GetViewFactorPerpendicularRectangles(
                1.0, 1.0, double.PositiveInfinity);
            Assert.Equal(0.5, f, precision: 10);
        }

        /// <summary>形態係数は常に0以上1以下</summary>
        [Theory]
        [InlineData(1.0, 1.0, 1.0)]
        [InlineData(2.0, 1.0, 0.5)]
        [InlineData(1.0, 3.0, 2.0)]
        public void GetViewFactorPerpendicularRectangles_AlwaysInRange(
            double width, double height, double depth)
        {
            double f = ViewFactor.GetViewFactorPerpendicularRectangles(width, height, depth);
            Assert.InRange(f, 0.0, 1.0);
        }

        /// <summary>deltaZ=0のとき基本形と同じ結果になる</summary>
        [Fact]
        public void GetViewFactorPerpendicularRectangles_ZeroDeltaZ_SameAsBasic()
        {
            double f1 = ViewFactor.GetViewFactorPerpendicularRectangles(1.0, 1.0, 1.0);
            double f2 = ViewFactor.GetViewFactorPerpendicularRectangles(1.0, 1.0, 1.0, 0.0);
            Assert.Equal(f1, f2, precision: 10);
        }

        /// <summary>deltaX=0のとき deltaZ版と同じ結果になる</summary>
        [Fact]
        public void GetViewFactorPerpendicularRectangles_ZeroDeltaX_SameAsDeltaZOnly()
        {
            double f1 = ViewFactor.GetViewFactorPerpendicularRectangles(1.0, 1.0, 1.0, 0.5);
            double f2 = ViewFactor.GetViewFactorPerpendicularRectangles(1.0, 1.0, 1.0, 0.5, 0.0);
            Assert.Equal(f1, f2, precision: 10);
        }

        /// <summary>負の deltaZ で PopoloArgumentException が発生する</summary>
        [Fact]
        public void GetViewFactorPerpendicularRectangles_NegativeDeltaZ_ThrowsPopoloArgumentException()
        {
            var ex = Assert.Throws<PopoloArgumentException>(
                () => ViewFactor.GetViewFactorPerpendicularRectangles(1.0, 1.0, 1.0, -0.1));
            Assert.Equal("deltaZ", ex.ParamName);
        }

        /// <summary>負の deltaX で PopoloArgumentException が発生する</summary>
        [Fact]
        public void GetViewFactorPerpendicularRectangles_NegativeDeltaX_ThrowsPopoloArgumentException()
        {
            var ex = Assert.Throws<PopoloArgumentException>(
                () => ViewFactor.GetViewFactorPerpendicularRectangles(1.0, 1.0, 1.0, 0.0, -0.1));
            Assert.Equal("deltaX", ex.ParamName);
        }

        #endregion

    }
}
