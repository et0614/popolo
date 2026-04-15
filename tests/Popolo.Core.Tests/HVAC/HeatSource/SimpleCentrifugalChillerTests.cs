/* SimpleCentrifugalChillerTests.cs
 *
 * Copyright (C) 2026 E.Togashi
 * GNU General Public License v3 — see accompanying LICENSE file.
 */

using System;
using Xunit;
using Popolo.Core.HVAC.HeatSource;

namespace Popolo.Core.Tests.HVAC.HeatSource
{
    /// <summary>Unit tests for <see cref="SimpleCentrifugalChiller"/>.</summary>
    /// <remarks>
    /// SimpleCentrifugalChiller models a centrifugal chiller using characteristic equations.
    /// Supports both inverter (variable speed) and constant-speed drives.
    ///
    /// Constructor:
    ///   (nominalInput, minimumPartialLoadRatio,
    ///    chilledWaterInletTemp, chilledWaterOutletTemp, coolingWaterOutletTemp,
    ///    chilledWaterFlowRate, hasInverter)
    ///
    /// Update(coolingWaterInletTemp, chilledWaterInletTemp,
    ///        coolingWaterFlowRate, chilledWaterFlowRate):
    ///   IsOperating must be true for cooling to occur.
    ///   IsOverLoad = true when CoolingLoad > NominalCapacity.
    ///
    /// Sign conventions:
    ///   CoolingLoad > 0 always (heat removed from chilled water)
    ///   ElectricConsumption > 0
    ///   ChilledWaterOutletTemperature &lt; ChilledWaterInletTemperature
    /// </remarks>
    public class SimpleCentrifugalChillerTests
    {
        #region ヘルパー

        /// <summary>
        /// 標準的なターボ冷凍機を生成する（インバータあり）。
        /// 定格: 入力350kW, 下限30%, 冷水12→7°C, 冷却水37°C, 流量10kg/s。
        /// </summary>
        private static SimpleCentrifugalChiller MakeChiller(bool hasInverter = true)
        {
            var c = new SimpleCentrifugalChiller(
                nominalInput:              350.0,
                minimumPartialLoadRatio:   0.3,
                chilledWaterInletTemperature:  12.0,
                chilledWaterOutletTemperature:  7.0,
                coolingWaterOutletTemperature: 37.0,
                chilledWaterFlowRate:      10.0,
                hasInverter:               hasInverter);
            c.IsOperating = true;
            c.ChilledWaterOutletSetPointTemperature = 7.0;
            return c;
        }

        #endregion

        // ================================================================
        #region コンストラクタ・プロパティ

        /// <summary>NominalCapacity が正（冷水流量 × cp × ΔT）。</summary>
        [Fact]
        public void Constructor_NominalCapacity_IsPositive()
        {
            var c = MakeChiller();
            Assert.True(c.NominalCapacity > 0,
                $"NominalCapacity={c.NominalCapacity:F2} kW > 0");
        }

        /// <summary>NominalCOP = NominalCapacity / NominalInput。</summary>
        [Fact]
        public void Constructor_NominalCOP_EqualsCapacityOverInput()
        {
            var c = MakeChiller();
            double expected = c.NominalCapacity / c.NominalInput;
            Assert.InRange(c.NominalCOP, expected - 0.01, expected + 0.01);
        }

        /// <summary>HasInverter プロパティがコンストラクタ指定値と一致する。</summary>
        [Fact]
        public void Constructor_HasInverter_MatchesInput()
        {
            Assert.True(MakeChiller(true).HasInverter);
            Assert.False(MakeChiller(false).HasInverter);
        }

        /// <summary>MaxChilledWaterFlowRate がコンストラクタ指定値と一致する。</summary>
        [Fact]
        public void Constructor_MaxChilledWaterFlowRate_MatchesInput()
        {
            var c = MakeChiller();
            Assert.InRange(c.MaxChilledWaterFlowRate, 9.99, 10.01);
        }

        #endregion

        // ================================================================
        #region Update — 通常冷却運転

        /// <summary>
        /// 冷水出口温度が設定値になる（非過負荷時）。
        /// </summary>
        [Fact]
        public void Update_Normal_OutletReachesSetpoint()
        {
            var c = MakeChiller();
            c.Update(32.0, 12.0, 15.0, 10.0);
            if (!c.IsOverLoad)
                Assert.InRange(c.ChilledWaterOutletTemperature, 6.5, 7.5);
        }

        /// <summary>冷却負荷が正。</summary>
        [Fact]
        public void Update_Normal_CoolingLoadIsPositive()
        {
            var c = MakeChiller();
            c.Update(32.0, 12.0, 15.0, 10.0);
            Assert.True(c.CoolingLoad > 0, $"CoolingLoad={c.CoolingLoad:F2} kW > 0");
        }

        /// <summary>消費電力が正。</summary>
        [Fact]
        public void Update_Normal_ElectricConsumptionIsPositive()
        {
            var c = MakeChiller();
            c.Update(32.0, 12.0, 15.0, 10.0);
            Assert.True(c.ElectricConsumption > 0,
                $"ElectricConsumption={c.ElectricConsumption:F2} kW > 0");
        }

        /// <summary>冷却水出口温度が冷却水入口温度より高い（冷却水が加熱される）。</summary>
        [Fact]
        public void Update_Normal_CoolingWaterOutletHigherThanInlet()
        {
            var c = MakeChiller();
            c.Update(32.0, 12.0, 15.0, 10.0);
            Assert.True(c.CoolingWaterOutletTemperature > c.CoolingWaterInletTemperature,
                $"CW outlet={c.CoolingWaterOutletTemperature:F2}°C > CW inlet={c.CoolingWaterInletTemperature:F2}°C");
        }

        /// <summary>冷却水入口温度が低いほど COP が高い（温度差縮小で効率向上）。</summary>
        [Fact]
        public void Update_LowerCoolingWaterTemp_HigherCOP()
        {
            var cHot  = MakeChiller();
            cHot.Update(35.0, 12.0, 15.0, 10.0);
            double copHot = cHot.COP;

            var cCold = MakeChiller();
            cCold.Update(28.0, 12.0, 15.0, 10.0);
            double copCold = cCold.COP;

            Assert.True(copCold > copHot,
                $"Cold CW COP={copCold:F3} > Hot CW COP={copHot:F3}");
        }

        #endregion

        // ================================================================
        #region IsOperating / ShutOff

        /// <summary>IsOperating = false のとき ShutOff 状態（冷却なし）。</summary>
        [Fact]
        public void Update_NotOperating_ZeroCoolingLoad()
        {
            var c = MakeChiller();
            c.IsOperating = false;
            c.Update(32.0, 12.0, 15.0, 10.0);
            Assert.Equal(0.0, c.CoolingLoad);
            Assert.Equal(0.0, c.ElectricConsumption);
        }

        /// <summary>ShutOff 後は全出力ゼロ。</summary>
        [Fact]
        public void ShutOff_ResetsOutputs()
        {
            var c = MakeChiller();
            c.Update(32.0, 12.0, 15.0, 10.0);
            Assert.True(c.CoolingLoad > 0);

            c.ShutOff();
            Assert.Equal(0.0, c.CoolingLoad);
            Assert.Equal(0.0, c.ElectricConsumption);
            Assert.Equal(0.0, c.ChilledWaterFlowRate);
        }

        #endregion

        // ================================================================
        #region 過負荷

        /// <summary>負荷が定格能力を超えると IsOverLoad = true かつ CoolingLoad = NominalCapacity。</summary>
        [Fact]
        public void Update_Overload_CoolingLoadCappedAtNominal()
        {
            var c = MakeChiller();
            // 冷水入口温度を非常に高くして過負荷を引き起こす
            c.ChilledWaterOutletSetPointTemperature = 7.0;
            c.Update(32.0, 40.0, 15.0, 10.0);
            Assert.True(c.IsOverLoad);
            Assert.InRange(c.CoolingLoad, c.NominalCapacity - 0.1, c.NominalCapacity + 0.1);
        }

        #endregion

        // ================================================================
        #region インバータ vs 定速

        /// <summary>インバータ機と定速機でCOPが異なる（特性係数が違う）。</summary>
        [Fact]
        public void Update_InverterVsConstantSpeed_COPDiffers()
        {
            var inv  = MakeChiller(hasInverter: true);
            var noInv = MakeChiller(hasInverter: false);
            inv.Update(32.0, 12.0, 15.0, 10.0);
            noInv.Update(32.0, 12.0, 15.0, 10.0);
            // COPが異なることを確認（同じでないこと）
            Assert.False(Math.Abs(inv.COP - noInv.COP) < 0.001,
                $"INV COP={inv.COP:F3} should differ from non-INV COP={noInv.COP:F3}");
        }

        #endregion
    }
}
