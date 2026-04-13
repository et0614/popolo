/* NormalRandomTests.cs
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
    /// <summary>NormalRandom のテスト</summary>
    public class NormalRandomTests
    {

        #region コンストラクタのテスト

        /// <summary>平均・標準偏差が正しく設定される</summary>
        [Fact]
        public void Constructor_StoresMeanAndStandardDeviation()
        {
            var nr = new NormalRandom(42U, mean: 5.0, standardDeviation: 2.0);
            Assert.Equal(5.0, nr.Mean);
            Assert.Equal(2.0, nr.StandardDeviation);
        }

        /// <summary>標準偏差が0以下のとき PopoloArgumentException が発生する</summary>
        [Theory]
        [InlineData(0.0)]
        [InlineData(-1.0)]
        public void Constructor_NonPositiveStandardDeviation_ThrowsPopoloArgumentException(
            double standardDeviation)
        {
            var ex = Assert.Throws<PopoloArgumentException>(
                () => new NormalRandom(1U, standardDeviation: standardDeviation));
            Assert.Equal("standardDeviation", ex.ParamName);
        }

        /// <summary>MersenneTwisterを渡すコンストラクタも正しく動作する</summary>
        [Fact]
        public void Constructor_WithMersenneTwister_WorksCorrectly()
        {
            var mt = new MersenneTwister(42U);
            var nr = new NormalRandom(mt, mean: 1.0, standardDeviation: 0.5);
            Assert.Equal(1.0, nr.Mean);
            Assert.Equal(0.5, nr.StandardDeviation);
        }

        #endregion

        #region NextDouble のテスト

        /// <summary>同じシードで同じ乱数列が生成される（再現性）</summary>
        [Fact]
        public void NextDouble_SameSeed_ProducesSameSequence()
        {
            var nr1 = new NormalRandom(123U);
            var nr2 = new NormalRandom(123U);

            for (int i = 0; i < 100; i++)
                Assert.Equal(nr1.NextDouble(), nr2.NextDouble());
        }

        /// <summary>大量の乱数の平均が指定した平均に近い</summary>
        [Fact]
        public void NextDouble_LargeSample_MeanNearSpecifiedMean()
        {
            const double expectedMean = 3.0;
            var nr = new NormalRandom(999U, mean: expectedMean, standardDeviation: 1.0);

            double sum = 0;
            int n = 100000;
            for (int i = 0; i < n; i++)
                sum += nr.NextDouble();

            Assert.InRange(sum / n, expectedMean - 0.05, expectedMean + 0.05);
        }

        /// <summary>大量の乱数の標準偏差が指定値に近い</summary>
        [Fact]
        public void NextDouble_LargeSample_StandardDeviationNearSpecifiedValue()
        {
            const double expectedSd = 2.0;
            var nr = new NormalRandom(777U, mean: 0.0, standardDeviation: expectedSd);

            double sum = 0, sumSq = 0;
            int n = 100000;
            for (int i = 0; i < n; i++)
            {
                double v = nr.NextDouble();
                sum += v;
                sumSq += v * v;
            }
            double mean = sum / n;
            double variance = sumSq / n - mean * mean;
            double sd = Math.Sqrt(variance);

            Assert.InRange(sd, expectedSd - 0.05, expectedSd + 0.05);
        }

        /// <summary>NextDouble_Standard の結果が標準正規分布に近い（平均≒0、標準偏差≒1）</summary>
        [Fact]
        public void NextDouble_Standard_ProducesStandardNormalDistribution()
        {
            var nr = new NormalRandom(555U);

            double sum = 0, sumSq = 0;
            int n = 100000;
            for (int i = 0; i < n; i++)
            {
                double v = nr.NextDouble_Standard();
                sum += v;
                sumSq += v * v;
            }
            double mean = sum / n;
            double variance = sumSq / n - mean * mean;

            Assert.InRange(mean, -0.05, 0.05);
            Assert.InRange(Math.Sqrt(variance), 0.95, 1.05);
        }

        #endregion

        #region CumulativeDistribution のテスト

        /// <summary>標準正規分布で CDF(0) = 0.5 となる</summary>
        [Fact]
        public void CumulativeDistribution_AtMean_ReturnsHalf()
        {
            double result = NormalRandom.CumulativeDistribution(0.0);
            Assert.Equal(0.5, result, precision: 6);
        }

        /// <summary>標準正規分布で CDF(1.96) ≈ 0.975 となる</summary>
        [Fact]
        public void CumulativeDistribution_At196_Returns975()
        {
            double result = NormalRandom.CumulativeDistribution(1.96);
            Assert.Equal(0.975, result, precision: 3);
        }

        /// <summary>平均・標準偏差を指定した場合も正しく計算される</summary>
        [Fact]
        public void CumulativeDistribution_WithMeanAndSd_ReturnsCorrectValue()
        {
            // N(mean=2, sd=2) で x=2（平均値）なら 0.5
            double result = NormalRandom.CumulativeDistribution(2.0, mean: 2.0, standardDeviation: 2.0);
            Assert.Equal(0.5, result, precision: 6);
        }

        /// <summary>standardDeviation が0以下のとき PopoloArgumentException が発生する</summary>
        [Fact]
        public void CumulativeDistribution_NonPositiveSd_ThrowsPopoloArgumentException()
        {
            var ex = Assert.Throws<PopoloArgumentException>(
                () => NormalRandom.CumulativeDistribution(0.0, standardDeviation: 0.0));
            Assert.Equal("standardDeviation", ex.ParamName);
        }

        #endregion

        #region CumulativeDistributionInverse のテスト

        /// <summary>CumulativeDistributionInverse(0.5) = 0.0（標準正規分布）</summary>
        [Fact]
        public void CumulativeDistributionInverse_AtHalf_ReturnsZero()
        {
            double result = NormalRandom.CumulativeDistributionInverse(0.5);
            Assert.Equal(0.0, result, precision: 6);
        }

        /// <summary>CDF と逆関数が互いに逆であることを確認</summary>
        [Theory]
        [InlineData(0.1)]
        [InlineData(0.25)]
        [InlineData(0.5)]
        [InlineData(0.75)]
        [InlineData(0.9)]
        public void CumulativeDistributionInverse_IsInverseOfCDF(double p)
        {
            double x = NormalRandom.CumulativeDistributionInverse(p);
            double p2 = NormalRandom.CumulativeDistribution(x);
            Assert.Equal(p, p2, precision: 6);
        }

        /// <summary>p=0 のとき負の無限大を返す</summary>
        [Fact]
        public void CumulativeDistributionInverse_AtZero_ReturnsNegativeInfinity()
        {
            double result = NormalRandom.CumulativeDistributionInverse(0.0);
            Assert.Equal(double.NegativeInfinity, result);
        }

        /// <summary>p=1 のとき正の無限大を返す</summary>
        [Fact]
        public void CumulativeDistributionInverse_AtOne_ReturnsPositiveInfinity()
        {
            double result = NormalRandom.CumulativeDistributionInverse(1.0);
            Assert.Equal(double.PositiveInfinity, result);
        }

        /// <summary>standardDeviation が0以下のとき PopoloArgumentException が発生する</summary>
        [Fact]
        public void CumulativeDistributionInverse_NonPositiveSd_ThrowsPopoloArgumentException()
        {
            var ex = Assert.Throws<PopoloArgumentException>(
                () => NormalRandom.CumulativeDistributionInverse(0.5, standardDeviation: -1.0));
            Assert.Equal("standardDeviation", ex.ParamName);
        }

        #endregion

    }
}
