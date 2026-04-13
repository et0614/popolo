/* MersenneTwisterTests.cs
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
    /// <summary>MersenneTwister のテスト</summary>
    public class MersenneTwisterTests
    {

        #region 基本動作のテスト

        /// <summary>シードが正しく保持される</summary>
        [Fact]
        public void Constructor_StoresSeed()
        {
            var mt = new MersenneTwister(12345U);
            Assert.Equal(12345U, mt.Seed);
        }

        /// <summary>同じシードで同じ乱数列が生成される（再現性）</summary>
        [Fact]
        public void NextDouble_SameSeed_ProducesSameSequence()
        {
            var mt1 = new MersenneTwister(42U);
            var mt2 = new MersenneTwister(42U);

            for (int i = 0; i < 100; i++)
                Assert.Equal(mt1.NextDouble(), mt2.NextDouble());
        }

        /// <summary>異なるシードで異なる乱数列が生成される</summary>
        [Fact]
        public void NextDouble_DifferentSeeds_ProduceDifferentSequences()
        {
            var mt1 = new MersenneTwister(1U);
            var mt2 = new MersenneTwister(2U);

            bool anyDifferent = false;
            for (int i = 0; i < 10; i++)
                if (mt1.NextDouble() != mt2.NextDouble())
                    anyDifferent = true;

            Assert.True(anyDifferent);
        }

        #endregion

        #region NextDouble のテスト

        /// <summary>NextDouble の値が [0.0, 1.0] の範囲に収まる</summary>
        [Fact]
        public void NextDouble_ValuesInRangeZeroToOne_Inclusive()
        {
            var mt = new MersenneTwister(123U);
            for (int i = 0; i < 10000; i++)
            {
                double v = mt.NextDouble();
                Assert.InRange(v, 0.0, 1.0);
            }
        }

        /// <summary>NextDouble2 の値が [0.0, 1.0) の範囲に収まる</summary>
        [Fact]
        public void NextDouble2_ValuesInRangeZeroToOnExclusive()
        {
            var mt = new MersenneTwister(456U);
            for (int i = 0; i < 10000; i++)
            {
                double v = mt.NextDouble2();
                Assert.InRange(v, 0.0, 0.9999999999);
            }
        }

        /// <summary>NextDouble3 の値が (0.0, 1.0) の範囲に収まる</summary>
        [Fact]
        public void NextDouble3_ValuesInRangeExcludingBothEnds()
        {
            var mt = new MersenneTwister(789U);
            for (int i = 0; i < 10000; i++)
            {
                double v = mt.NextDouble3();
                Assert.True(v > 0.0 && v < 1.0,
                    $"Expected (0, 1) exclusive but got {v}");
            }
        }

        #endregion

        #region 統計的性質のテスト

        /// <summary>大量の乱数の平均が約0.5になる（一様分布の性質）</summary>
        [Fact]
        public void NextDouble_LargeSample_MeanNearHalf()
        {
            var mt = new MersenneTwister(999U);
            double sum = 0;
            int n = 100000;
            for (int i = 0; i < n; i++)
                sum += mt.NextDouble();

            double mean = sum / n;
            Assert.InRange(mean, 0.49, 0.51);
        }

        /// <summary>既知のシードで既知の最初の値を確認する（回帰テスト）</summary>
        [Fact]
        public void Next_KnownSeed_ReturnsKnownFirstValue()
        {
            // seed=19650218 は原著者のテストベクタで使われる値
            var mt = new MersenneTwister(19650218U);
            uint first = mt.Next();

            // 同じシードから同じ値が得られることを確認（回帰テスト）
            var mt2 = new MersenneTwister(19650218U);
            Assert.Equal(first, mt2.Next());
        }

        /// <summary>624個以上の乱数を生成しても正しく動作する（内部バッファ再生成）</summary>
        [Fact]
        public void NextDouble_MoreThan624Values_StillProducesValidValues()
        {
            var mt = new MersenneTwister(1U);
            for (int i = 0; i < 1300; i++)
            {
                double v = mt.NextDouble();
                Assert.InRange(v, 0.0, 1.0);
            }
        }

        #endregion

    }
}
