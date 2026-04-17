/* HotWaterAbsorptionChillerTests.cs
 *
 * Copyright (C) 2026 E.Togashi
 * GNU General Public License v3 — see accompanying LICENSE file.
 */

using System;
using Xunit;
using Popolo.Core.HVAC.HeatSource;

namespace Popolo.Core.Tests.HVAC.HeatSource
{
    /// <summary>Unit tests for <see cref="HotWaterAbsorptionChiller"/>.</summary>
    /// <remarks>
    /// Rated conditions from HotWaterAbsorptionChillerTest() sample:
    ///   new HotWaterAbsorptionChiller(12.5, 7, 274.9/60, 31, 35, 918/60, 88, 83, 432/60)
    ///   Chilled water: 12.5C -> 7C,  274.9/60 kg/s
    ///   Cooling water: 31C   -> 35C, 918/60   kg/s
    ///   Hot water:     88C   -> 83C, 432/60   kg/s
    ///   Nominal COP ~= 0.70
    ///
    /// Update(chWaterInlet, chWaterFlow, cdWaterInlet, cdWaterFlow, htWaterInlet, htWaterFlow)
    ///   ChilledWaterOutletSetPointTemperature = 0 → free-running mode
    /// </remarks>
    public class HotWaterAbsorptionChillerTests
    {
        #region 定格条件

        private static readonly double ChWM = 274.9 / 60.0;
        private static readonly double CdWM = 918.0 / 60.0;
        private static readonly double HtWM = 432.0 / 60.0;
        private const double ChWI = 12.5;
        private const double CdWI = 31.0;
        private const double HtWI = 88.0;

        #endregion

        #region ヘルパー

        /// <summary>サンプルコードと同じ定格条件で初期化。成り行き運転。</summary>
        private static HotWaterAbsorptionChiller MakeChiller()
        {
            var c = new HotWaterAbsorptionChiller(
                12.5, 7.0, ChWM,
                31.0, 35.0, CdWM,
                88.0, 83.0, HtWM);
            c.ChilledWaterOutletSetpointTemperature = 0; // 成り行き運転
            return c;
        }

        #endregion

        // ================================================================
        #region コンストラクタ

        /// <summary>NominalCapacity が正。</summary>
        [Fact]
        public void Constructor_NominalCapacity_IsPositive()
        {
            var c = MakeChiller();
            Assert.True(c.NominalCapacity > 0,
                $"NominalCapacity={c.NominalCapacity:F2} kW > 0");
        }

        /// <summary>定格流量プロパティが入力値と一致する。</summary>
        [Fact]
        public void Constructor_NominalFlowRates_MatchInput()
        {
            var c = MakeChiller();
            Assert.InRange(c.NominalChilledWaterFlowRate, ChWM * 0.99, ChWM * 1.01);
            Assert.InRange(c.NominalCoolingWaterFlowRate, CdWM * 0.99, CdWM * 1.01);
            Assert.InRange(c.NominalHotWaterFlowRate,     HtWM * 0.99, HtWM * 1.01);
        }

        /// <summary>コンストラクタ直後は ShutOff 状態。</summary>
        [Fact]
        public void Constructor_InitialState_IsShutOff()
        {
            var c = MakeChiller();
            Assert.Equal(0.0, c.CoolingLoad);
        }

        #endregion

        // ================================================================
        #region Update — 定格条件

        /// <summary>定格条件で Update すると冷凍能力が正。</summary>
        [Fact]
        public void Update_RatedCondition_CoolingLoadIsPositive()
        {
            var c = MakeChiller();
            c.Update(ChWI, ChWM, CdWI, CdWM, HtWI, HtWM);
            Assert.True(c.CoolingLoad > 0,
                $"CoolingLoad={c.CoolingLoad:F2} kW > 0");
        }

        /// <summary>冷水出口温度が入口温度より低い。</summary>
        [Fact]
        public void Update_RatedCondition_ChilledWaterOutletCoolerThanInlet()
        {
            var c = MakeChiller();
            c.Update(ChWI, ChWM, CdWI, CdWM, HtWI, HtWM);
            Assert.True(c.ChilledWaterOutletTemperature < c.ChilledWaterInletTemperature,
                $"CHW out={c.ChilledWaterOutletTemperature:F2}C < in={c.ChilledWaterInletTemperature}C");
        }

        /// <summary>冷却水出口温度が入口温度より高い。</summary>
        [Fact]
        public void Update_RatedCondition_CoolingWaterOutletHigherThanInlet()
        {
            var c = MakeChiller();
            c.Update(ChWI, ChWM, CdWI, CdWM, HtWI, HtWM);
            Assert.True(c.CoolingWaterOutletTemperature > c.CoolingWaterInletTemperature,
                $"CDW out={c.CoolingWaterOutletTemperature:F2}C > in={c.CoolingWaterInletTemperature}C");
        }

        /// <summary>温水出口温度が入口温度より低い。</summary>
        [Fact]
        public void Update_RatedCondition_HotWaterOutletCoolerThanInlet()
        {
            var c = MakeChiller();
            c.Update(ChWI, ChWM, CdWI, CdWM, HtWI, HtWM);
            Assert.True(c.HotWaterOutletTemperature < c.HotWaterInletTemperature,
                $"HW out={c.HotWaterOutletTemperature:F2}C < in={c.HotWaterInletTemperature}C");
        }

        /// <summary>COP が現実的な範囲（0.4–0.9）。</summary>
        [Fact]
        public void Update_RatedCondition_COPInRealisticRange()
        {
            var c = MakeChiller();
            c.Update(ChWI, ChWM, CdWI, CdWM, HtWI, HtWM);
            Assert.InRange(c.COP, 0.4, 0.9);
        }

        #endregion

        // ================================================================
        #region 温水温度・冷却水温度の依存性（サンプルコードのループ条件より）

        /// <summary>
        /// 温水温度が高いほど冷凍能力が大きい（70C vs 90C）。
        /// サンプルコードの「温水温度・温水流量・処理熱量の関係」ループに対応。
        /// </summary>
        [Fact]
        public void Update_HigherHotWaterTemp_HigherCoolingLoad()
        {
            var c = MakeChiller();
            c.Update(ChWI, ChWM, CdWI, CdWM, 70.0, HtWM);
            double qLow = c.CoolingLoad;

            c.Update(ChWI, ChWM, CdWI, CdWM, 90.0, HtWM);
            double qHigh = c.CoolingLoad;

            Assert.True(qHigh > qLow,
                $"HW=90C: Q={qHigh:F2} kW > HW=70C: Q={qLow:F2} kW");
        }

        /// <summary>
        /// 冷却水入口温度が低いほど COP が高い（31C vs 28C vs 25C vs 22C）。
        /// サンプルコードの「負荷率・冷却水温度・COPの関係」ループに対応。
        /// </summary>
        [Theory]
        [InlineData(31.0, 28.0)]
        [InlineData(28.0, 25.0)]
        [InlineData(25.0, 22.0)]
        public void Update_LowerCoolingWaterTemp_HigherCOP(double cdHot, double cdCold)
        {
            var cHot = MakeChiller();
            cHot.ChilledWaterOutletSetpointTemperature = 7.0;
            cHot.Update(ChWI, ChWM, cdHot, CdWM, HtWI, HtWM);

            var cCold = MakeChiller();
            cCold.ChilledWaterOutletSetpointTemperature = 7.0;
            cCold.Update(ChWI, ChWM, cdCold, CdWM, HtWI, HtWM);

            // 冷却水温度が低いほど COP が高い（または少なくとも冷凍能力が落ちない）
            Assert.True(cCold.COP >= cHot.COP,
                $"CDW={cdCold}C COP={cCold.COP:F3} >= CDW={cdHot}C COP={cHot.COP:F3}");
        }

        /// <summary>
        /// 冷水流量を減らすと冷凍能力が下がる（流量50%）。
        /// サンプルコードの部分負荷ループに対応。
        /// </summary>
        [Fact]
        public void Update_ReducedChilledWaterFlow_ReducesCoolingLoad()
        {
            var c = MakeChiller();
            c.Update(ChWI, ChWM, CdWI, CdWM, HtWI, HtWM);
            double qFull = c.CoolingLoad;

            c.Update(ChWI, ChWM * 0.5, CdWI, CdWM, HtWI, HtWM);
            double qHalf = c.CoolingLoad;

            Assert.True(qHalf < qFull,
                $"Half flow Q={qHalf:F2} kW < Full flow Q={qFull:F2} kW");
        }

        #endregion

        // ================================================================
        #region ShutOff

        /// <summary>ShutOff 後は CoolingLoad = 0。</summary>
        [Fact]
        public void ShutOff_ZeroCoolingLoad()
        {
            var c = MakeChiller();
            c.Update(ChWI, ChWM, CdWI, CdWM, HtWI, HtWM);
            Assert.True(c.CoolingLoad > 0);
            c.ShutOff();
            Assert.Equal(0.0, c.CoolingLoad);
        }

        #endregion
    }
}
