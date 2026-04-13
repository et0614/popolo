/* LogNormalRandomTests.cs
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
    /// <summary>LogNormalRandom のテスト</summary>
    public class LogNormalRandomTests
    {

        #region コンストラクタのテスト

        /// <summary>平均・標準偏差が正しく保持される</summary>
        [Fact]
        public void Constructor_StoresMeanAndStandardDeviation()
        {
            var lr = new LogNormalRandom(42U, mean: 2.0, standardDeviation: 1.0);
            Assert.Equal(2.0, lr.Mean);
            Assert.Equal(1.0, lr.StandardDeviation);
        }

        /// <summary>mean が0以下のとき PopoloArgumentException が発生する</summary>
        [Theory]
        [InlineData(0.0)]
        [InlineData(-1.0)]
        public void Constructor_NonPositiveMean_ThrowsPopoloArgumentException(double mean)
        {
            var ex = Assert.Throws<PopoloArgumentException>(
                () => new LogNormalRandom(1U, mean, 1.0));
            Assert.Equal("mean", ex.ParamName);
        }

        /// <summary>standardDeviation が0以下のとき PopoloArgumentException が発生する</summary>
        [Theory]
        [InlineData(0.0)]
        [InlineData(-1.0)]
        public void Constructor_NonPositiveStandardDeviation_ThrowsPopoloArgumentException(
            double standardDeviation)
        {
            var ex = Assert.Throws<PopoloArgumentException>(
                () => new LogNormalRandom(1U, 1.0, standardDeviation));
            Assert.Equal("standardDeviation", ex.ParamName);
        }

        #endregion

        #region NextDouble のテスト

        /// <summary>生成される値は常に正</summary>
        [Fact]
        public void NextDouble_AlwaysPositive()
        {
            var lr = new LogNormalRandom(123U, mean: 1.0, standardDeviation: 1.0);
            for (int i = 0; i < 1000; i++)
                Assert.True(lr.NextDouble() > 0);
        }

        /// <summary>同じシードで同じ乱数列が生成される（再現性）</summary>
        [Fact]
        public void NextDouble_SameSeed_ProducesSameSequence()
        {
            var lr1 = new LogNormalRandom(42U, 2.0, 1.0);
            var lr2 = new LogNormalRandom(42U, 2.0, 1.0);

            for (int i = 0; i < 20; i++)
                Assert.Equal(lr1.NextDouble(), lr2.NextDouble());
        }

        /// <summary>大量サンプルの平均が指定した平均に近い</summary>
        [Theory]
        [InlineData(1.0, 0.5)]
        [InlineData(2.0, 1.0)]
        [InlineData(5.0, 2.0)]
        public void NextDouble_LargeSample_MeanNearSpecifiedMean(
            double mean, double standardDeviation)
        {
            var lr = new LogNormalRandom(999U, mean, standardDeviation);

            double sum = 0;
            int n = 100000;
            for (int i = 0; i < n; i++) sum += lr.NextDouble();

            double sampleMean = sum / n;
            // 許容誤差は指定平均の10%
            Assert.InRange(sampleMean, mean * 0.90, mean * 1.10);
        }

        /// <summary>大量サンプルの中央値が exp(μ) に近い</summary>
        /// <remarks>
        /// 対数正規分布の中央値は exp(μ) = mean / sqrt(1 + σ²/μ²) の近似で確認
        /// </remarks>
        [Fact]
        public void NextDouble_LargeSample_MedianNearTheoreticalValue()
        {
            double mean = 2.0;
            double sd = 1.0;
            var lr = new LogNormalRandom(777U, mean, sd);

            int n = 100000;
            double[] samples = new double[n];
            for (int i = 0; i < n; i++) samples[i] = lr.NextDouble();
            Array.Sort(samples);

            double median = samples[n / 2];
            // 理論中央値: exp(mu) where mu = log(mean^2) - 0.5*log(mean^2 + sd^2)
            double m2 = mean * mean;
            double s2 = sd * sd;
            double mu = Math.Log(m2) - 0.5 * Math.Log(m2 + s2);
            double theoreticalMedian = Math.Exp(mu);

            Assert.InRange(median,
                theoreticalMedian * 0.95,
                theoreticalMedian * 1.05);
        }

        #endregion

    }
}
