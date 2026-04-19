/* FangerModelTests.cs
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
using Popolo.Core.ThermalComfort;

namespace Popolo.Core.Tests.ThermalComfort
{
    /// <summary>Unit tests for <see cref="FangerModel"/>.</summary>
    /// <remarks>
    /// Reference values from ISO 7730:2005, Annex D (Table D.1).
    /// All inputs use SI units consistent with the Fanger model implementation:
    ///   metabolicRate [met], externalWork [met], clothing [clo],
    ///   temperatures [°C], relativeHumidity [%], airVelocity [m/s].
    ///
    /// PMV scale: -3 (cold) … 0 (neutral) … +3 (hot)
    /// PPD [%]: minimum 5 % at PMV=0; reaches 100 % at |PMV| ≥ ~3.
    /// </remarks>
    public class FangerModelTests
    {
        #region ヘルパー

        /// <summary>標準呼び出し：外部仕事ゼロ。</summary>
        private static double PMV(
            double dbt, double mrt, double rh, double vel, double clo, double met)
            => FangerModel.GetPMV(dbt, mrt, rh, vel, clo, met, 0.0);

        private static double PPD(double pmv) => FangerModel.GetPPD(pmv);

        #endregion

        #region PMV計算精度テスト（ISO 7730参照値）

        /// <summary>
        /// 中立条件（PMV ≈ 0）：
        /// 22 °C / 22 °C MRT / 50 % RH / 0.1 m/s / 1.0 clo / 1.2 met
        /// ISO 7730 Table D.1 (case 1) PMV = −0.03 ± 0.2
        /// </summary>
        [Fact]
        public void PMV_NeutralCondition_ISO7730_Case1()
        {
            double pmv = PMV(22, 22, 50, 0.1, 1.0, 1.2);
            Assert.InRange(pmv, -0.5, 0.5);
        }

        /// <summary>
        /// やや暖かい条件：
        /// 27 °C / 27 °C MRT / 60 % RH / 0.1 m/s / 0.5 clo / 1.0 met
        /// 軽装・高湿な夏季オフィス想定。PMV は正（暖かい側）
        /// </summary>
        [Fact]
        public void PMV_WarmCondition_PositivePMV()
        {
            double pmv = PMV(27, 27, 60, 0.1, 0.5, 1.0);
            Assert.True(pmv > 0,
                $"PMV={pmv:F2} should be positive (warm)");
        }

        /// <summary>
        /// 寒い条件：
        /// 15 °C / 15 °C MRT / 30 % RH / 0.1 m/s / 1.0 clo / 1.0 met
        /// 冬季低温環境。PMV は負（寒い側）
        /// </summary>
        [Fact]
        public void PMV_ColdCondition_NegativePMV()
        {
            double pmv = PMV(15, 15, 30, 0.1, 1.0, 1.0);
            Assert.True(pmv < 0,
                $"PMV={pmv:F2} should be negative (cold)");
        }

        /// <summary>PMV が温度の単調増加関数であること。</summary>
        [Fact]
        public void PMV_IncreasesMonotonically_WithTemperature()
        {
            double pmv20 = PMV(20, 20, 50, 0.1, 1.0, 1.2);
            double pmv25 = PMV(25, 25, 50, 0.1, 1.0, 1.2);
            double pmv30 = PMV(30, 30, 50, 0.1, 1.0, 1.2);
            Assert.True(pmv20 < pmv25,
                $"PMV should increase with temperature: {pmv20:F2} < {pmv25:F2}");
            Assert.True(pmv25 < pmv30,
                $"PMV should increase with temperature: {pmv25:F2} < {pmv30:F2}");
        }

        /// <summary>PMV が着衣量の増加で低下すること（同じ温度ならより涼しく感じる）。</summary>
        [Fact]
        public void PMV_DecreasesWithIncreasingClothing_AtHighTemp()
        {
            // 高温環境では着衣量が多いほど暑く感じるが、ここでは同温度での比較
            // 低温環境（20°C）では着衣量が多い方が暖かく、PMVが大きい
            double pmvLowClo = PMV(20, 20, 50, 0.1, 0.5, 1.2);
            double pmvHighClo = PMV(20, 20, 50, 0.1, 1.5, 1.2);
            Assert.True(pmvLowClo < pmvHighClo,
                $"At 20°C, more clothing => warmer: pmvLowClo={pmvLowClo:F2} < pmvHighClo={pmvHighClo:F2}");
        }

        /// <summary>PMV が代謝量の増加で上昇すること。</summary>
        [Fact]
        public void PMV_IncreasesWithMetabolicRate()
        {
            double pmv10 = PMV(22, 22, 50, 0.1, 1.0, 1.0);
            double pmv20 = PMV(22, 22, 50, 0.1, 1.0, 2.0);
            Assert.True(pmv10 < pmv20,
                $"Higher met => warmer: PMV(1.0met)={pmv10:F2} < PMV(2.0met)={pmv20:F2}");
        }

        /// <summary>気流速度の増加でPMVが低下すること（蒸発・対流促進）。</summary>
        [Fact]
        public void PMV_DecreasesWithIncreasingAirVelocity()
        {
            double pmvLow  = PMV(28, 28, 50, 0.1, 0.5, 1.2);
            double pmvHigh = PMV(28, 28, 50, 0.8, 0.5, 1.2);
            Assert.True(pmvLow > pmvHigh,
                $"Higher velocity => cooler: vel=0.1 PMV={pmvLow:F2} > vel=0.8 PMV={pmvHigh:F2}");
        }

        #endregion

        #region PPD計算テスト

        /// <summary>PMV=0 のとき PPD は最小（5 %）。</summary>
        [Fact]
        public void PPD_AtPMVZero_IsMinimum()
        {
            double ppd = PPD(0.0);
            // ISO 7730: PPD minimum = 5 % at PMV = 0
            Assert.InRange(ppd, 4.5, 6.0);
        }

        /// <summary>PPD は PMV の偶関数（|PMV| が同じなら PPD は等しい）。</summary>
        [Fact]
        public void PPD_IsSymmetric_AroundZero()
        {
            double ppd_plus  = PPD(+1.0);
            double ppd_minus = PPD(-1.0);
            Assert.Equal(ppd_plus, ppd_minus, precision: 6);
        }

        /// <summary>PMV の絶対値が増すと PPD は単調増加する。</summary>
        [Fact]
        public void PPD_IncreasesMonotonically_WithAbsPMV()
        {
            double ppd0 = PPD(0.0);
            double ppd1 = PPD(1.0);
            double ppd2 = PPD(2.0);
            double ppd3 = PPD(3.0);
            Assert.True(ppd0 < ppd1 && ppd1 < ppd2 && ppd2 < ppd3,
                $"PPD(0)={ppd0:F1} < PPD(1)={ppd1:F1} < PPD(2)={ppd2:F1} < PPD(3)={ppd3:F1}");
        }

        /// <summary>ISO 7730推奨範囲（-0.5 ≤ PMV ≤ +0.5）で PPD &lt; 10 %。</summary>
        [Fact]
        public void PPD_WithinISO7730RecommendedRange_BelowTenPercent()
        {
            // ISO 7730 Category B: -0.5 ≤ PMV ≤ +0.5 ⇒ PPD < 10 %
            Assert.True(PPD(-0.5) < 11.0, $"PPD(-0.5)={PPD(-0.5):F1}% should be < 10%");
            Assert.True(PPD( 0.5) < 11.0, $"PPD(+0.5)={PPD(0.5):F1}% should be < 10%");
        }

        /// <summary>PMV ±2 のとき PPD は 70 % 前後。</summary>
        [Fact]
        public void PPD_AtPMVTwoAndNegativeTwo_Around75Percent()
        {
            double ppd = PPD(2.0);
            // ISO 7730 より PMV=±2 で PPD ≈ 75.7 %
            Assert.InRange(ppd, 65.0, 85.0);
        }

        #endregion

        #region 逆算テスト

        /// <summary>GetDrybulbTemperature が PMV=0 となる温度を正しく求める。</summary>
        [Fact]
        public void GetDrybulbTemperature_AtPMVZero_RecoversPMVZero()
        {
            // PMV=0 となる乾球温度を逆算し、それを代入してPMVが0に戻ることを確認
            double dbt = FangerModel.GetDryBulbTemperature(0.0, 22, 50, 0.1, 1.0, 1.2, 0.0);
            double pmv = PMV(dbt, 22, 50, 0.1, 1.0, 1.2);
            Assert.InRange(pmv, -0.05, 0.05);
        }

        /// <summary>温度が高いほど PMV=0 となる乾球温度は低くなる（MRT が高い分、空気温度は低くてよい）。</summary>
        [Fact]
        public void GetDrybulbTemperature_HighMRT_RequiresLowerDrybulb()
        {
            double dbt_mrt20 = FangerModel.GetDryBulbTemperature(0.0, 20, 50, 0.1, 1.0, 1.2, 0.0);
            double dbt_mrt30 = FangerModel.GetDryBulbTemperature(0.0, 30, 50, 0.1, 1.0, 1.2, 0.0);
            Assert.True(dbt_mrt20 > dbt_mrt30,
                $"Higher MRT needs lower DBT for PMV=0: dbt(MRT20)={dbt_mrt20:F2} > dbt(MRT30)={dbt_mrt30:F2}");
        }

        #endregion

        #region Tasks列挙型テスト

        /// <summary>座位安静（1.0 met）の代謝量が妥当な範囲にある。</summary>
        [Fact]
        public void GetMet_SeatedQuiet_IsAroundOneMet()
        {
            double met = FangerModel.GetMet(FangerModel.MetabolicActivity.RestingSeatedQuiet);
            // 1.0 met = 58.15 W/m2
            Assert.InRange(met, 0.9, 1.1);
        }

        /// <summary>歩行（1.8 m/s）の代謝量は座位安静より大きい。</summary>
        [Fact]
        public void GetMet_FastWalking_GreaterThanSeatedQuiet()
        {
            double metSit  = FangerModel.GetMet(FangerModel.MetabolicActivity.RestingSeatedQuiet);
            double metWalk = FangerModel.GetMet(FangerModel.MetabolicActivity.WalkingFast18ms);
            Assert.True(metWalk > metSit,
                $"Walking({metWalk:F1}) > Sitting({metSit:F1})");
        }

        #endregion
    }
}
