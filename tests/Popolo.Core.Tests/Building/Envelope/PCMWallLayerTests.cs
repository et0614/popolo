/* PCMWallLayerTests.cs
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
    /// <summary>PCMWallLayer のテスト</summary>
    /// <remarks>
    /// 相変化材料の代表例としてパラフィン系PCM（凝固点18°C, 融点20°C）を使用。
    ///
    /// 相の判定ロジック：
    ///   temp &lt;= FreezingTemperature → Solid
    ///   FreezingTemperature &lt; temp &lt; MeltingTemperature → Equilibrium
    ///   temp &gt;= MeltingTemperature → Liquid
    ///
    /// 各相の熱物性（テスト用仮定値）：
    ///   固体: λ=0.5 W/(m·K), C=400 kJ/(m³·K)
    ///   平衡: λ=0.4 W/(m·K), C=300 kJ/(m³·K)
    ///   液体: λ=0.3 W/(m·K), C=200 kJ/(m³·K)
    /// </remarks>
    public class PCMWallLayerTests
    {
        #region テスト用定数・ヘルパー

        private const double FreezingTemp   = 18.0; // 凝固点[°C]
        private const double MeltingTemp    = 20.0; // 融点[°C]
        private const double LayerThickness = 0.02; // 厚み[m]

        // 各相の熱物性
        private const double LambdaSolid = 0.5;  // 固体熱伝導率[W/(m·K)]
        private const double LambdaEquil = 0.4;  // 平衡熱伝導率[W/(m·K)]
        private const double LambdaLiquid = 0.3; // 液体熱伝導率[W/(m·K)]
        private const double CpSolid  = 400.0;   // 固体容積比熱[kJ/(m³·K)]
        private const double CpEquil  = 300.0;   // 平衡容積比熱[kJ/(m³·K)]
        private const double CpLiquid = 200.0;   // 液体容積比熱[kJ/(m³·K)]

        /// <summary>代表的なPCM壁層を生成する</summary>
        private static PCMWallLayer MakeTypicalPCM()
        {
            var solid = new WallLayer("PCM-Solid",       LambdaSolid,  CpSolid,  LayerThickness);
            var equil = new WallLayer("PCM-Equilibrium", LambdaEquil,  CpEquil,  LayerThickness);
            var liquid = new WallLayer("PCM-Liquid",     LambdaLiquid, CpLiquid, LayerThickness);
            return new PCMWallLayer("PCM", FreezingTemp, MeltingTemp, LayerThickness, solid, equil, liquid);
        }

        /// <summary>両側が同じ相のときの理論的な熱コンダクタンスを計算する</summary>
        private static double ExpectedConductance(double lambda)
        {
            // HeatConductance = 2 / (d/λ + d/λ) = λ/d
            return lambda / LayerThickness;
        }

        #endregion

        #region コンストラクタのテスト

        /// <summary>コンストラクタでプロパティが正しく設定される</summary>
        [Fact]
        public void Constructor_SetsProperties()
        {
            var pcm = MakeTypicalPCM();

            Assert.Equal("PCM",          pcm.Name);
            Assert.Equal(FreezingTemp,   pcm.FreezingTemperature, precision: 6);
            Assert.Equal(MeltingTemp,    pcm.MeltingTemperature,  precision: 6);
            Assert.Equal(LayerThickness, pcm.Thickness,           precision: 6);
            Assert.True(pcm.IsVariableProperties);
        }

        /// <summary>
        /// コンストラクタ後の初期相状態はSolid。
        /// UpdateState(freezingTemperature, freezingTemperature) が呼ばれ、
        /// GetState(freezingTemperature) = Solid（≦条件）になる。
        /// </summary>
        [Fact]
        public void Constructor_InitialState_IsSolid()
        {
            var pcm = MakeTypicalPCM();

            Assert.Equal(PCMWallLayer.State.Solid, pcm.CurrentState_F);
            Assert.Equal(PCMWallLayer.State.Solid, pcm.CurrentState_B);
        }

        #endregion

        #region 相判定のテスト

        /// <summary>凝固点以下では固体になる</summary>
        [Theory]
        [InlineData(15.0)]
        [InlineData(18.0)] // 境界値（<=）
        [InlineData(0.0)]
        public void UpdateState_BelowFreezing_SolidState(double temp)
        {
            var pcm = MakeTypicalPCM();
            pcm.UpdateState(temp, temp);

            Assert.Equal(PCMWallLayer.State.Solid, pcm.CurrentState_F);
            Assert.Equal(PCMWallLayer.State.Solid, pcm.CurrentState_B);
        }

        /// <summary>凝固点超・融点未満では平衡状態になる</summary>
        [Theory]
        [InlineData(18.5)]
        [InlineData(19.0)]
        [InlineData(19.9)]
        public void UpdateState_BetweenFreezingAndMelting_EquilibriumState(double temp)
        {
            var pcm = MakeTypicalPCM();
            pcm.UpdateState(temp, temp);

            Assert.Equal(PCMWallLayer.State.Equilibrium, pcm.CurrentState_F);
            Assert.Equal(PCMWallLayer.State.Equilibrium, pcm.CurrentState_B);
        }

        /// <summary>融点以上では液体になる</summary>
        [Theory]
        [InlineData(20.0)] // 境界値（>=）
        [InlineData(25.0)]
        [InlineData(40.0)]
        public void UpdateState_AboveMelting_LiquidState(double temp)
        {
            var pcm = MakeTypicalPCM();
            pcm.UpdateState(temp, temp);

            Assert.Equal(PCMWallLayer.State.Liquid, pcm.CurrentState_F);
            Assert.Equal(PCMWallLayer.State.Liquid, pcm.CurrentState_B);
        }

        /// <summary>F側とB側で温度が異なる場合に独立して相が判定される</summary>
        [Fact]
        public void UpdateState_DifferentTemperatures_IndependentStates()
        {
            var pcm = MakeTypicalPCM();
            // F側=固体温度、B側=液体温度
            pcm.UpdateState(15.0, 25.0);

            Assert.Equal(PCMWallLayer.State.Solid,  pcm.CurrentState_F);
            Assert.Equal(PCMWallLayer.State.Liquid, pcm.CurrentState_B);
        }

        #endregion

        #region 相変化検出のテスト

        /// <summary>相変化があった場合にtrueを返す</summary>
        [Fact]
        public void UpdateState_PhaseChange_ReturnsTrue()
        {
            var pcm = MakeTypicalPCM();
            pcm.UpdateState(15.0, 15.0); // Solid

            // Solid → Liquid に変化
            bool changed = pcm.UpdateState(25.0, 25.0);
            Assert.True(changed);
        }

        /// <summary>相変化がない場合にfalseを返す</summary>
        [Fact]
        public void UpdateState_NoPhaseChange_ReturnsFalse()
        {
            var pcm = MakeTypicalPCM();
            pcm.UpdateState(15.0, 15.0); // Solid

            // Solid のまま変化なし
            bool changed = pcm.UpdateState(10.0, 5.0);
            Assert.False(changed);
        }

        /// <summary>前回相状態が正しく記録される</summary>
        [Fact]
        public void UpdateState_PhaseChange_LastStateRecorded()
        {
            var pcm = MakeTypicalPCM();
            pcm.UpdateState(15.0, 15.0); // Solid

            pcm.UpdateState(25.0, 25.0); // Liquid
            Assert.Equal(PCMWallLayer.State.Solid, pcm.LastState_F);
            Assert.Equal(PCMWallLayer.State.Solid, pcm.LastState_B);
        }

        #endregion

        #region 熱物性値のテスト

        /// <summary>固体状態では固体の熱コンダクタンスが適用される</summary>
        [Fact]
        public void UpdateState_SolidState_SolidConductance()
        {
            var pcm = MakeTypicalPCM();
            pcm.UpdateState(10.0, 10.0); // 確実に固体

            double expected = ExpectedConductance(LambdaSolid);
            Assert.Equal(expected, pcm.HeatConductance, precision: 4);
        }

        /// <summary>液体状態では液体の熱コンダクタンスが適用される</summary>
        [Fact]
        public void UpdateState_LiquidState_LiquidConductance()
        {
            var pcm = MakeTypicalPCM();
            pcm.UpdateState(25.0, 25.0); // 確実に液体

            double expected = ExpectedConductance(LambdaLiquid);
            Assert.Equal(expected, pcm.HeatConductance, precision: 4);
        }

        /// <summary>固体の熱コンダクタンスは液体より大きい（λ_solid > λ_liquid）</summary>
        [Fact]
        public void UpdateState_SolidConductance_GreaterThanLiquid()
        {
            var pcm = MakeTypicalPCM();

            pcm.UpdateState(10.0, 10.0);
            double solidConductance = pcm.HeatConductance;

            pcm.UpdateState(25.0, 25.0);
            double liquidConductance = pcm.HeatConductance;

            Assert.True(solidConductance > liquidConductance,
                $"Solid ({solidConductance:F4}) should be > Liquid ({liquidConductance:F4})");
        }

        /// <summary>固体状態では固体の熱容量が適用される</summary>
        [Fact]
        public void UpdateState_SolidState_SolidHeatCapacity()
        {
            var pcm = MakeTypicalPCM();
            pcm.UpdateState(10.0, 10.0);

            double expected = 0.5 * CpSolid * LayerThickness * 1000.0;
            Assert.Equal(expected, pcm.HeatCapacity_F, precision: 4);
            Assert.Equal(expected, pcm.HeatCapacity_B, precision: 4);
        }

        /// <summary>液体状態では液体の熱容量が適用される</summary>
        [Fact]
        public void UpdateState_LiquidState_LiquidHeatCapacity()
        {
            var pcm = MakeTypicalPCM();
            pcm.UpdateState(25.0, 25.0);

            double expected = 0.5 * CpLiquid * LayerThickness * 1000.0;
            Assert.Equal(expected, pcm.HeatCapacity_F, precision: 4);
            Assert.Equal(expected, pcm.HeatCapacity_B, precision: 4);
        }

        /// <summary>F側とB側が異なる相のとき、各側の熱容量が独立して適用される</summary>
        [Fact]
        public void UpdateState_MixedState_IndependentHeatCapacity()
        {
            var pcm = MakeTypicalPCM();
            pcm.UpdateState(10.0, 25.0); // F=固体, B=液体

            double expectedSolid  = 0.5 * CpSolid  * LayerThickness * 1000.0;
            double expectedLiquid = 0.5 * CpLiquid * LayerThickness * 1000.0;

            Assert.Equal(expectedSolid,  pcm.HeatCapacity_F, precision: 4);
            Assert.Equal(expectedLiquid, pcm.HeatCapacity_B, precision: 4);
        }

        #endregion

        #region GetHeatCapacityのテスト

        /// <summary>GetHeatCapacityが各相の正しい値を返す</summary>
        [Theory]
        [InlineData(PCMWallLayer.State.Solid,       CpSolid)]
        [InlineData(PCMWallLayer.State.Equilibrium, CpEquil)]
        [InlineData(PCMWallLayer.State.Liquid,      CpLiquid)]
        public void GetHeatCapacity_ReturnsCorrectValue(PCMWallLayer.State state, double cp)
        {
            var pcm = MakeTypicalPCM();
            double expected = 0.5 * cp * LayerThickness * 1000.0;

            Assert.Equal(expected, pcm.GetHeatCapacity(state), precision: 4);
        }

        #endregion
    }
}
