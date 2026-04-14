/* TakakusakiComfortModelTests.cs
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
using System.Linq;
using Xunit;
using Popolo.Core.ThermalComfort;

namespace Popolo.Core.Tests.ThermalComfort
{
    /// <summary>Unit tests for <see cref="TakakusakiComfortModel"/>.</summary>
    /// <remarks>
    /// Takakusaki (1998) models individual differences in thermal preference.
    /// Each occupant has a personal optimum PMV drawn from N(0, σ_m) where σ_m = 0.85.
    /// Dissatisfaction follows a Weibull distribution (shape β = 7.0) separately
    /// for the hot and cold sides.
    ///
    /// Key statistical property (the basis of the original paper):
    ///   When many occupants with Gaussian-distributed OptimumPMV are exposed to
    ///   the same environmental PMV, the aggregate dissatisfaction rate converges
    ///   to the PPD predicted by the Fanger model (ISO 7730).
    ///
    /// Reference:
    ///   Takakusaki, A. (1998). A method for estimating the probability of
    ///   occupant dissatisfaction taking into account variability of indoor
    ///   thermal environment. Journal of Architecture, Planning and Environmental
    ///   Engineering (Transactions of AIJ), 63(13).
    /// </remarks>
    public class TakakusakiComfortModelTests
    {
        #region 定数

        private const int  OccupantCount = 10000;
        private const uint RndSeed       = 0;

        #endregion

        #region ヘルパー

        /// <summary>
        /// 10,000 人の執務者集団を生成して返す。
        /// サンプルコードと同じ乱数シード列を使用。
        /// </summary>
        private static TakakusakiComfortModel[] CreateOccupants()
        {
            // MersenneTwister を直接使えない環境のため、System.Random で代替シードを生成
            var rng = new Random((int)RndSeed);
            var ocps = new TakakusakiComfortModel[OccupantCount];
            for (int i = 0; i < OccupantCount; i++)
                ocps[i] = new TakakusakiComfortModel((uint)(OccupantCount * rng.NextDouble()));
            return ocps;
        }

        #endregion

        #region 個体パラメータテスト

        /// <summary>コンストラクタ（シードのみ）で正常に生成できる。</summary>
        [Fact]
        public void Constructor_DefaultSeed_Succeeds()
        {
            var ocp = new TakakusakiComfortModel(42);
            Assert.NotNull(ocp);
        }

        /// <summary>コンストラクタ（シード + OptimumPMV 指定）で OptimumPMV が設定される。</summary>
        [Fact]
        public void Constructor_WithOptimumPMV_SetsProperty()
        {
            var ocp = new TakakusakiComfortModel(42, 0.5);
            Assert.Equal(0.5, ocp.OptimumPMV, precision: 10);
        }

        /// <summary>EtaZero_Hot, EtaZero_Cold が正の値を持つ。</summary>
        [Fact]
        public void Parameters_EtaZero_ArePositive()
        {
            var ocp = new TakakusakiComfortModel(42, 0.0);
            Assert.True(ocp.EtaZero_Hot  > 0, $"EtaZero_Hot={ocp.EtaZero_Hot:E3} should be positive");
            Assert.True(ocp.EtaZero_Cold > 0, $"EtaZero_Cold={ocp.EtaZero_Cold:E3} should be positive");
        }

        /// <summary>OptimumPMV = 0 のとき EtaZero_Hot == EtaZero_Cold（対称性）。</summary>
        [Fact]
        public void Parameters_NeutralOptimumPMV_IsSymmetric()
        {
            var ocp = new TakakusakiComfortModel(42, 0.0);
            Assert.Equal(ocp.EtaZero_Hot, ocp.EtaZero_Cold, precision: 10);
        }

        #endregion

        #region SetPMV / 不満確率テスト

        /// <summary>環境 PMV = OptimumPMV のとき不満確率は最小（≈ 0）。</summary>
        [Fact]
        public void SetPMV_AtOptimum_DissatisfiedProbability_NearZero()
        {
            var ocp = new TakakusakiComfortModel(42, 0.3);
            ocp.SetPMV(ocp.OptimumPMV);
            Assert.True(ocp.DissatisfiedProbability_Hot  < 0.01,
                $"At optimum PMV, P_hot={ocp.DissatisfiedProbability_Hot:F4} should be ~0");
            Assert.True(ocp.DissatisfiedProbability_Cold < 0.01,
                $"At optimum PMV, P_cold={ocp.DissatisfiedProbability_Cold:F4} should be ~0");
        }

        /// <summary>環境 PMV が最適値より高いとき、暑い不満確率のみ正。</summary>
        [Fact]
        public void SetPMV_AboveOptimum_OnlyHotDissatisfaction()
        {
            var ocp = new TakakusakiComfortModel(42, 0.0);
            ocp.SetPMV(1.5); // 最適値0より高い
            Assert.True(ocp.DissatisfiedProbability_Hot  > 0,   "P_hot should be > 0");
            Assert.Equal(0.0, ocp.DissatisfiedProbability_Cold,   precision: 10);
        }

        /// <summary>環境 PMV が最適値より低いとき、寒い不満確率のみ正。</summary>
        [Fact]
        public void SetPMV_BelowOptimum_OnlyColdDissatisfaction()
        {
            var ocp = new TakakusakiComfortModel(42, 0.0);
            ocp.SetPMV(-1.5); // 最適値0より低い
            Assert.True(ocp.DissatisfiedProbability_Cold > 0,   "P_cold should be > 0");
            Assert.Equal(0.0, ocp.DissatisfiedProbability_Hot,   precision: 10);
        }

        /// <summary>|PMV - OptimumPMV| が大きいほど不満確率が高くなる。</summary>
        [Fact]
        public void SetPMV_LargerDeviation_HigherDissatisfaction()
        {
            var ocp = new TakakusakiComfortModel(42, 0.0);

            ocp.SetPMV(0.5);
            double pHot_small = ocp.DissatisfiedProbability_Hot;

            ocp.SetPMV(2.0);
            double pHot_large = ocp.DissatisfiedProbability_Hot;

            Assert.True(pHot_large > pHot_small,
                $"P_hot(PMV=2.0)={pHot_large:F4} > P_hot(PMV=0.5)={pHot_small:F4}");
        }

        /// <summary>不満確率は [0, 1] の範囲に収まる。</summary>
        [Theory]
        [InlineData(-3.0)]
        [InlineData(-1.0)]
        [InlineData( 0.0)]
        [InlineData( 1.0)]
        [InlineData( 3.0)]
        public void SetPMV_DissatisfiedProbability_InRange(double pmv)
        {
            var ocp = new TakakusakiComfortModel(42, 0.0);
            ocp.SetPMV(pmv);
            Assert.InRange(ocp.DissatisfiedProbability_Hot,  0.0, 1.0);
            Assert.InRange(ocp.DissatisfiedProbability_Cold, 0.0, 1.0);
        }

        #endregion

        #region UpdateThermalSensationVote テスト

        /// <summary>投票結果は Hot/Cold/Neutral のいずれかである。</summary>
        [Fact]
        public void UpdateThermalSensationVote_ReturnsValidSensation()
        {
            var ocp = new TakakusakiComfortModel(42, 0.0);
            ocp.SetPMV(1.0);
            var sensation = ocp.UpdateThermalSensationVote();
            Assert.True(
                sensation == TakakusakiComfortModel.ThermalSensation.Hot ||
                sensation == TakakusakiComfortModel.ThermalSensation.Cold ||
                sensation == TakakusakiComfortModel.ThermalSensation.Neutral);
        }

        /// <summary>PMV が最適値より高い状況では Hot しか返さない（Cold は返さない）。</summary>
        [Fact]
        public void UpdateThermalSensationVote_AboveOptimum_NeverReturnsCold()
        {
            // OptimumPMV = 0, PMV = 3.0 → Hot 不満確率が非常に高い
            // 100回試行して Cold が出ないことを確認
            var ocp = new TakakusakiComfortModel(1, 0.0);
            ocp.SetPMV(3.0);
            for (int i = 0; i < 100; i++)
            {
                var s = ocp.UpdateThermalSensationVote();
                Assert.NotEqual(TakakusakiComfortModel.ThermalSensation.Cold, s);
            }
        }

        /// <summary>PMV が最適値より低い状況では Cold しか返さない（Hot は返さない）。</summary>
        [Fact]
        public void UpdateThermalSensationVote_BelowOptimum_NeverReturnsHot()
        {
            var ocp = new TakakusakiComfortModel(1, 0.0);
            ocp.SetPMV(-3.0);
            for (int i = 0; i < 100; i++)
            {
                var s = ocp.UpdateThermalSensationVote();
                Assert.NotEqual(TakakusakiComfortModel.ThermalSensation.Hot, s);
            }
        }

        #endregion

        #region 集団統計テスト（不満足者率 ≈ PPD）

        /// <summary>
        /// 10,000 人の集団でPMVを変化させたとき、不満足者率（暑い + 寒い）が
        /// FangerモデルのPPDと近似的に一致する。
        ///
        /// 高草木モデルの核心的な性質：個人差のある執務者集団の集計値は
        /// Fanger の PPD と整合する。
        /// 許容誤差: ±15 % ポイント（統計的ばらつきと近似誤差を考慮）
        /// </summary>
        [Theory]
        [InlineData(-2.0)]
        [InlineData(-1.0)]
        [InlineData( 0.0)]
        [InlineData( 1.0)]
        [InlineData( 2.0)]
        public void GroupDissatisfaction_ConvergesToPPD(double pmv)
        {
            var ocps = CreateOccupants();
            int numDissatisfied = 0;

            foreach (var ocp in ocps)
            {
                ocp.SetPMV(pmv);
                var s = ocp.UpdateThermalSensationVote();
                if (s != TakakusakiComfortModel.ThermalSensation.Neutral)
                    numDissatisfied++;
            }

            double actualPPD   = 100.0 * numDissatisfied / OccupantCount;
            double expectedPPD = FangerModel.GetPPD(pmv);

            Assert.True(Math.Abs(actualPPD - expectedPPD) < 15.0,
                $"PMV={pmv:F1}: actual PPD={actualPPD:F1}% vs expected PPD={expectedPPD:F1}%");
        }

        /// <summary>
        /// PMV = 0 のとき Hot/Cold 不満足者数がほぼ均等に分布する（対称性）。
        /// </summary>
        [Fact]
        public void GroupVotes_AtPMVZero_HotAndCold_AreRoughlyEqual()
        {
            var ocps = CreateOccupants();
            int numHot = 0, numCold = 0;

            foreach (var ocp in ocps)
            {
                ocp.SetPMV(0.0);
                var s = ocp.UpdateThermalSensationVote();
                if (s == TakakusakiComfortModel.ThermalSensation.Hot)  numHot++;
                if (s == TakakusakiComfortModel.ThermalSensation.Cold) numCold++;
            }

            // PMV=0では暑い・寒い不満はほぼ同数（±2%ポイント以内）
            double diff = Math.Abs(numHot - numCold) / (double)OccupantCount * 100;
            Assert.True(diff < 2.0,
                $"PMV=0: Hot={numHot}, Cold={numCold}, diff={diff:F1}% should be < 2%");
        }

        /// <summary>
        /// 高温側（PMV = +2）では Hot 不満足者が Cold より多い。
        /// </summary>
        [Fact]
        public void GroupVotes_AtPositivePMV_HotDissatisfied_Dominant()
        {
            var ocps = CreateOccupants();
            int numHot = 0, numCold = 0;

            foreach (var ocp in ocps)
            {
                ocp.SetPMV(2.0);
                var s = ocp.UpdateThermalSensationVote();
                if (s == TakakusakiComfortModel.ThermalSensation.Hot)  numHot++;
                if (s == TakakusakiComfortModel.ThermalSensation.Cold) numCold++;
            }

            Assert.True(numHot > numCold,
                $"PMV=+2: Hot({numHot}) should dominate Cold({numCold})");
        }

        #endregion

        #region IComparable テスト

        /// <summary>OptimumPMV の大小が CompareTo に反映される。</summary>
        [Fact]
        public void CompareTo_ReflectsOptimumPMV_Ordering()
        {
            var ocpLow  = new TakakusakiComfortModel(1, -0.5);
            var ocpHigh = new TakakusakiComfortModel(2,  0.5);
            Assert.True(ocpLow.CompareTo(ocpHigh) < 0,
                "ocpLow.CompareTo(ocpHigh) should be negative");
        }

        #endregion
    }
}
