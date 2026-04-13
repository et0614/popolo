/* HorizontalAirChamberTests.cs
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

using Xunit;
using Popolo.Core.Building.Envelope;

namespace Popolo.Core.Tests.Building.Envelope
{
    /// <summary>HorizontalAirChamber のテスト</summary>
    /// <remarks>
    /// F側が上面、B側が下面。
    /// 対流の発生条件：下面温度 > 上面温度（温かい空気が下にある場合）。
    /// 臨界レイリー数 Ra_cr = 1708。
    ///
    /// RECALC_TMP = 0.1K のため、温度変化は0.1K超が必要。
    /// </remarks>
    public class HorizontalAirChamberTests
    {
        #region テスト用ヘルパー

        /// <summary>代表的な天井裏エアチャンバを生成する（厚み0.3m, 放射率0.9）</summary>
        private static HorizontalAirChamber MakeTypicalChamber()
            => new HorizontalAirChamber("Attic Air Chamber", 0.3, 0.9, 0.9);

        #endregion

        #region コンストラクタのテスト

        /// <summary>コンストラクタでプロパティが正しく設定される</summary>
        [Fact]
        public void Constructor_SetsProperties()
        {
            var chamber = new HorizontalAirChamber("Test", 0.3, 0.8, 0.7);

            Assert.Equal("Test", chamber.Name);
            Assert.Equal(0.3,  chamber.Thickness,        precision: 6);
            Assert.Equal(0.8,  chamber.UpperEmissivity,  precision: 6);
            Assert.Equal(0.7,  chamber.LowerEmissivity,  precision: 6);
            Assert.True(chamber.IsVariableProperties);
        }

        /// <summary>デフォルトコンストラクタで放射率が0.9に設定される</summary>
        [Fact]
        public void Constructor_Default_EmissivityIs09()
        {
            var chamber = new HorizontalAirChamber("Test", 0.3);

            Assert.Equal(0.9, chamber.UpperEmissivity, precision: 6);
            Assert.Equal(0.9, chamber.LowerEmissivity, precision: 6);
        }

        /// <summary>コンストラクタ後の熱コンダクタンスが正の値である</summary>
        [Fact]
        public void Constructor_HeatConductance_IsPositive()
        {
            var chamber = MakeTypicalChamber();
            Assert.True(chamber.HeatConductance > 0);
        }

        #endregion

        #region 対流のテスト

        /// <summary>
        /// 上面温度 > 下面温度（逆転なし）のとき対流が生じない。
        /// 熱コンダクタンスは放射と伝導のみ（= 伝導 + 放射）。
        /// </summary>
        [Fact]
        public void UpdateState_UpperWarmer_NoConvection()
        {
            var chamber = MakeTypicalChamber();
            // F（上）= 30°C > B（下）= 20°C → 安定成層、対流なし
            chamber.UpdateState(30, 20);

            // 対流なしの場合: 対流熱伝達率 = lambda/d（伝導のみ）
            double conductionOnly = chamber.ThermalConductivity / chamber.Thickness;
            Assert.Equal(conductionOnly, chamber.ConvectiveHeatTransferCoefficient, precision: 4);
        }

        /// <summary>
        /// 等温のとき対流が生じない。
        /// </summary>
        [Fact]
        public void UpdateState_SameTemperature_NoConvection()
        {
            var chamber = MakeTypicalChamber();
            // F = B → 温度差なし、対流なし
            // RECALC_TMP=0.1Kのしきい値を超えるため初期値から離れた値を使用
            chamber.UpdateState(30, 30);

            double conductionOnly = chamber.ThermalConductivity / chamber.Thickness;
            Assert.Equal(conductionOnly, chamber.ConvectiveHeatTransferCoefficient, precision: 4);
        }

        /// <summary>
        /// 下面温度 > 上面温度（不安定成層）かつ十分な温度差があるとき対流が発生する。
        /// 熱コンダクタンスが伝導のみの場合より大きくなる。
        /// </summary>
        [Fact]
        public void UpdateState_LowerWarmer_ConvectionOccurs()
        {
            var chamber = MakeTypicalChamber(); // 厚み0.3m → Ra >> 1708

            // 対流なし（上 > 下）の基準値を取得
            chamber.UpdateState(30, 20);
            double noConvectionConductance = chamber.HeatConductance;

            // 対流あり（下 > 上、十分な温度差）
            chamber.UpdateState(20, 30);
            double withConvectionConductance = chamber.HeatConductance;

            Assert.True(withConvectionConductance > noConvectionConductance,
                $"With convection ({withConvectionConductance:F4}) should be > without ({noConvectionConductance:F4})");
        }

        /// <summary>
        /// 温度差が大きいほど熱コンダクタンスが大きい（対流が強まる）。
        /// </summary>
        [Fact]
        public void UpdateState_LargerTemperatureDiff_LargerConductance()
        {
            var chamber = MakeTypicalChamber();

            // 小さな温度差（下 > 上）
            chamber.UpdateState(25, 30);
            double conductance5K = chamber.HeatConductance;

            // 大きな温度差（下 > 上）
            chamber.UpdateState(15, 35);
            double conductance20K = chamber.HeatConductance;

            Assert.True(conductance20K > conductance5K,
                $"20K diff ({conductance20K:F4}) should be > 5K diff ({conductance5K:F4})");
        }

        /// <summary>
        /// 薄い層では臨界レイリー数を超えにくいため対流が発生しない。
        /// Ra ∝ d³ なので厚みが薄いと Ra &lt; 1708 になる。
        /// </summary>
        [Fact]
        public void UpdateState_ThinChamber_NoConvection()
        {
            // 厚み0.01m: Ra ≈ 1031 < 1708 → 対流なし
            var thinChamber = new HorizontalAirChamber("Thin", 0.01, 0.9, 0.9);
            thinChamber.UpdateState(20, 30); // 下 > 上だが薄いので対流なし

            double conductionOnly = thinChamber.ThermalConductivity / thinChamber.Thickness;
            Assert.Equal(conductionOnly, thinChamber.ConvectiveHeatTransferCoefficient, precision: 4);
        }

        #endregion

        #region 放射のテスト

        /// <summary>放射熱伝達率は正の値である</summary>
        [Fact]
        public void UpdateState_RadiativeCoefficient_IsPositive()
        {
            var chamber = MakeTypicalChamber();
            chamber.UpdateState(26, 20);
            Assert.True(chamber.RadiativeHeatTransferCoefficient > 0);
        }

        /// <summary>放射率が高いほど放射熱伝達率が大きい</summary>
        [Fact]
        public void UpdateState_HigherEmissivity_HigherRadiativeCoefficient()
        {
            var lowE  = new HorizontalAirChamber("Low-E",  0.3, 0.1, 0.1);
            var highE = new HorizontalAirChamber("High-E", 0.3, 0.9, 0.9);

            lowE.UpdateState(30, 20);
            highE.UpdateState(30, 20);

            Assert.True(highE.RadiativeHeatTransferCoefficient > lowE.RadiativeHeatTransferCoefficient,
                $"High-E ({highE.RadiativeHeatTransferCoefficient:F4}) should be > Low-E ({lowE.RadiativeHeatTransferCoefficient:F4})");
        }

        /// <summary>高温では放射熱伝達率が大きい（T³ に比例する線形近似）</summary>
        [Fact]
        public void UpdateState_HigherTemperature_HigherRadiativeCoefficient()
        {
            var chamber = MakeTypicalChamber();

            chamber.UpdateState(10, 20); // 平均15°C
            double radLow = chamber.RadiativeHeatTransferCoefficient;

            chamber.UpdateState(50, 60); // 平均55°C
            double radHigh = chamber.RadiativeHeatTransferCoefficient;

            Assert.True(radHigh > radLow,
                $"55°C ({radHigh:F4}) should be > 15°C ({radLow:F4})");
        }

        #endregion

        #region 更新判定のテスト

        /// <summary>温度変化がRECALC_TMP（0.1K）未満のとき更新されない</summary>
        [Fact]
        public void UpdateState_SmallChange_ReturnsFalse()
        {
            var chamber = MakeTypicalChamber();
            chamber.UpdateState(26, 20);

            // 0.05K の変化 → 更新されない
            bool updated = chamber.UpdateState(26.05, 20.05);
            Assert.False(updated);
        }

        /// <summary>温度変化がRECALC_TMP（0.1K）以上のとき更新される</summary>
        [Fact]
        public void UpdateState_LargeChange_ReturnsTrue()
        {
            var chamber = MakeTypicalChamber();
            chamber.UpdateState(26, 20);

            // 0.2K の変化 → 更新される
            bool updated = chamber.UpdateState(26.2, 20);
            Assert.True(updated);
        }

        #endregion
    }
}
