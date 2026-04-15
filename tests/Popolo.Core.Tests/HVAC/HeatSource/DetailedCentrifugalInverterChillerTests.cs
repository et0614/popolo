/* DetailedCentrifugalInverterChillerTests.cs
 *
 * Copyright (C) 2026 E.Togashi
 * GNU General Public License v3 — see accompanying LICENSE file.
 */

using System;
using Xunit;
using Popolo.Core.HVAC.HeatSource;

namespace Popolo.Core.Tests.HVAC.HeatSource
{
    /// <summary>Unit tests for <see cref="DetailedCentrifugalInverterChiller"/>.</summary>
    /// <remarks>
    /// DetailedCentrifugalInverterChiller uses compressor characteristic equations
    /// (adiabatic head vs volume flow, head efficiency vs volume flow) to model
    /// an inverter-driven centrifugal chiller with physical accuracy.
    ///
    /// Constructor (simplified):
    ///   (nominalInput, minimumPartialLoadRatio,
    ///    chilledWaterInletTemp, chilledWaterOutletTemp,
    ///    coolingWaterInletTemp, chilledWaterFlowRate, coolingWaterFlowRate)
    ///
    /// Constructor (detailed):
    ///   same + Parameters (custom characteristic coefficients)
    ///
    /// Update(coolingWaterInletTemp, chilledWaterInletTemp,
    ///        coolingWaterFlowRate, chilledWaterFlowRate):
    ///   IsOperating must be true.
    /// </remarks>
    public class DetailedCentrifugalInverterChillerTests
    {
        #region ヘルパー

        /// <summary>
        /// 標準的な詳細インバータターボ冷凍機を生成する（デフォルトパラメータ）。
        /// 定格: 入力350kW, 下限30%, 冷水12→7°C, 冷却水32°C, 冷水10kg/s, 冷却水15kg/s。
        /// </summary>
        private static DetailedCentrifugalInverterChiller MakeChiller()
        {
            var c = new DetailedCentrifugalInverterChiller(
                nominalInput:                  350.0,
                minimumPartialLoadRatio:        0.3,
                chilledWaterInletTemperature:   12.0,
                chilledWaterOutletTemperature:   7.0,
                coolingWaterInletTemperature:   32.0,
                chilledWaterFlowRate:           10.0,
                coolingWaterFlowRate:           15.0);
            c.IsOperating = true;
            c.ChilledWaterOutletSetPointTemperature = 7.0;
            return c;
        }

        #endregion

        // ================================================================
        #region コンストラクタ・プロパティ

        /// <summary>NominalCapacity が正。</summary>
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
            Assert.InRange(c.NominalCOP, expected - 0.05, expected + 0.05);
        }

        /// <summary>ModelParameters が null でない（デフォルト値が設定される）。</summary>
        [Fact]
        public void Constructor_ModelParameters_NotNull()
        {
            var c = MakeChiller();
            Assert.NotNull(c.ModelParameters);
        }

        /// <summary>HasInverter = true（詳細モデルはインバータ機のみ）。</summary>
        [Fact]
        public void Constructor_HasInverter_IsTrue()
        {
            var c = MakeChiller();
            Assert.True(c.HasInverter);
        }

        #endregion

        // ================================================================
        #region Update — 通常冷却運転

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

        /// <summary>
        /// 冷水出口温度が設定値付近になる（非過負荷時）。
        /// </summary>
        [Fact]
        public void Update_Normal_OutletTemperatureNearSetpoint()
        {
            var c = MakeChiller();
            c.Update(32.0, 12.0, 15.0, 10.0);
            if (!c.IsOverLoad)
                Assert.InRange(c.ChilledWaterOutletTemperature, 6.0, 8.0);
        }

        /// <summary>冷却水出口温度が入口より高い（冷却水が熱を受け取る）。</summary>
        [Fact]
        public void Update_Normal_CoolingWaterOutletHigherThanInlet()
        {
            var c = MakeChiller();
            c.Update(32.0, 12.0, 15.0, 10.0);
            Assert.True(c.CoolingWaterOutletTemperature > c.CoolingWaterInletTemperature,
                $"CW out={c.CoolingWaterOutletTemperature:F2}°C > CW in={c.CoolingWaterInletTemperature:F2}°C");
        }

        /// <summary>COP が正かつ現実的な範囲（1〜15）にある。</summary>
        [Fact]
        public void Update_Normal_COPInRealisticRange()
        {
            var c = MakeChiller();
            c.Update(32.0, 12.0, 15.0, 10.0);
            Assert.InRange(c.COP, 1.0, 15.0);
        }

        /// <summary>冷却水入口温度が低いほど COP が高い。</summary>
        [Fact]
        public void Update_LowerCoolingWaterTemp_HigherCOP()
        {
            var cHot = MakeChiller();
            cHot.Update(35.0, 12.0, 15.0, 10.0);

            var cCold = MakeChiller();
            cCold.Update(28.0, 12.0, 15.0, 10.0);

            Assert.True(cCold.COP > cHot.COP,
                $"Cold CW COP={cCold.COP:F3} > Hot CW COP={cHot.COP:F3}");
        }

        #endregion

        // ================================================================
        #region IsOperating / ShutOff

        /// <summary>IsOperating = false のとき CoolingLoad = 0。</summary>
        [Fact]
        public void Update_NotOperating_ZeroCoolingLoad()
        {
            var c = MakeChiller();
            c.IsOperating = false;
            c.Update(32.0, 12.0, 15.0, 10.0);
            Assert.Equal(0.0, c.CoolingLoad);
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
        }

        #endregion

        // ================================================================
        #region カスタムパラメータ

        /// <summary>
        /// カスタム Parameters を渡したコンストラクタが正常に動作する。
        /// </summary>
        [Fact]
        public void Constructor_WithCustomParameters_Works()
        {
            var p = new DetailedCentrifugalInverterChiller.Parameters();
            var c = new DetailedCentrifugalInverterChiller(
                350.0, 0.3, 12.0, 7.0, 32.0, 10.0, 15.0, p);
            c.IsOperating = true;
            c.ChilledWaterOutletSetPointTemperature = 7.0;
            var ex = Record.Exception(() => c.Update(32.0, 12.0, 15.0, 10.0));
            Assert.Null(ex);
        }

        #endregion
    }
}
