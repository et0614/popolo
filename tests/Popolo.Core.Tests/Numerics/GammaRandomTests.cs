/* GammaRandomTests.cs
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
    /// <summary>GammaRandom のテスト</summary>
    public class GammaRandomTests
    {

        #region コンストラクタのテスト

        /// <summary>α・βが正しく保持される</summary>
        [Fact]
        public void Constructor_StoresAlphaAndBeta()
        {
            var gr = new GammaRandom(42U, alpha: 2.0, beta: 1.5);
            Assert.Equal(2.0, gr.Alpha);
            Assert.Equal(1.5, gr.Beta);
        }

        /// <summary>alpha が0以下のとき PopoloArgumentException が発生する</summary>
        [Theory]
        [InlineData(0.0)]
        [InlineData(-1.0)]
        public void Constructor_NonPositiveAlpha_ThrowsPopoloArgumentException(double alpha)
        {
            var ex = Assert.Throws<PopoloArgumentException>(
                () => new GammaRandom(1U, alpha, 1.0));
            Assert.Equal("alpha", ex.ParamName);
        }

        /// <summary>beta が0以下のとき PopoloArgumentException が発生する</summary>
        [Theory]
        [InlineData(0.0)]
        [InlineData(-1.0)]
        public void Constructor_NonPositiveBeta_ThrowsPopoloArgumentException(double beta)
        {
            var ex = Assert.Throws<PopoloArgumentException>(
                () => new GammaRandom(1U, 1.0, beta));
            Assert.Equal("beta", ex.ParamName);
        }

        /// <summary>MersenneTwisterを渡すコンストラクタも正しく動作する</summary>
        [Fact]
        public void Constructor_WithMersenneTwister_WorksCorrectly()
        {
            var mt = new MersenneTwister(42U);
            var gr = new GammaRandom(mt, alpha: 3.0, beta: 2.0);
            Assert.Equal(3.0, gr.Alpha);
        }

        #endregion

        #region NextDouble のテスト

        /// <summary>生成される値は常に正</summary>
        [Theory]
        [InlineData(0.1, 1.0)]   // alpha < 0.4
        [InlineData(1.0, 1.0)]   // 0.4 < alpha <= 4
        [InlineData(5.0, 1.0)]   // alpha > 4
        public void NextDouble_AlwaysPositive(double alpha, double beta)
        {
            var gr = new GammaRandom(123U, alpha, beta);
            for (int i = 0; i < 1000; i++)
                Assert.True(gr.NextDouble() > 0);
        }

        /// <summary>同じシードで同じ乱数列が生成される（再現性）</summary>
        [Fact]
        public void NextDouble_SameSeed_ProducesSameSequence()
        {
            var gr1 = new GammaRandom(42U, 2.0, 1.0);
            var gr2 = new GammaRandom(42U, 2.0, 1.0);

            for (int i = 0; i < 20; i++)
                Assert.Equal(gr1.NextDouble(), gr2.NextDouble());
        }

        /// <summary>大量サンプルの平均が理論値 α*β に近い</summary>
        [Theory]
        [InlineData(1.0, 1.0)]   // mean = 1.0
        [InlineData(2.0, 3.0)]   // mean = 6.0
        [InlineData(5.0, 2.0)]   // mean = 10.0
        public void NextDouble_LargeSample_MeanNearTheoreticalValue(
            double alpha, double beta)
        {
            var gr = new GammaRandom(999U, alpha, beta);
            double expectedMean = alpha * beta;

            double sum = 0;
            int n = 100000;
            for (int i = 0; i < n; i++) sum += gr.NextDouble();

            double mean = sum / n;
            // 許容誤差は理論値の5%
            Assert.InRange(mean,
                expectedMean * 0.95,
                expectedMean * 1.05);
        }

        /// <summary>betaを2倍にすると平均も2倍になる（スケール変換）</summary>
        [Fact]
        public void NextDouble_DoubleBeta_DoublesMean()
        {
            int n = 100000;
            double alpha = 3.0;

            var gr1 = new GammaRandom(777U, alpha, beta: 1.0);
            var gr2 = new GammaRandom(777U, alpha, beta: 2.0);

            double sum1 = 0, sum2 = 0;
            for (int i = 0; i < n; i++)
            {
                sum1 += gr1.NextDouble();
                sum2 += gr2.NextDouble();
            }

            double ratio = (sum2 / n) / (sum1 / n);
            Assert.InRange(ratio, 1.9, 2.1);
        }

        #endregion

    }
}
