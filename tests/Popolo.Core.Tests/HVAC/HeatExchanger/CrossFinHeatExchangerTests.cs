/* CrossFinHeatExchangerTests.cs
 *
 * Copyright (C) 2026 E.Togashi
 * GNU General Public License v3 — see accompanying LICENSE file.
 */

using System;
using Xunit;
using Popolo.Core.HVAC.HeatExchanger;

namespace Popolo.Core.Tests.HVAC.HeatExchanger
{
    /// <summary>Unit tests for <see cref="CrossFinHeatExchanger"/>.</summary>
    /// <remarks>
    /// CrossFinHeatExchanger models a plate-fin-and-tube air-water coil.
    /// Supports both simplified (rated-condition) and detailed (geometric) models.
    ///
    /// Simplified constructor (ctor2):
    ///   (width, height, rowNumber, columnNumber, ratedAirFlow, ratedInletAirTemp,
    ///    ratedInletAirHumidity, borderRH, ratedWaterFlow, maxWaterFlow,
    ///    ratedInletWaterTemp, flowType, heatTransfer, useCorrectionFactor)
    ///
    /// UpdateOutletState(inletAirTemp, inletAirHumidity, inletWaterTemp, airFlow, waterFlow)
    ///   → computes outlet air and water temperatures, heat transfer rate
    ///
    /// ControlOutletAirTemperature(inletAirTemp, inletAirHumidity, inletWaterTemp, airFlow, setpoint)
    ///   → adjusts water flow to reach the outlet air temperature setpoint
    ///   → returns true if achievable, false if overloaded
    ///
    /// Cooling coil: inletWaterTemp &lt; inletAirTemp → outlet air cooled, HeatTransfer &gt; 0
    /// Heating coil: inletWaterTemp &gt; inletAirTemp → outlet air heated, HeatTransfer &gt; 0
    /// </remarks>
    public class CrossFinHeatExchangerTests
    {
        #region ヘルパー

        /// <summary>
        /// 冷却コイル（簡易モデル）を生成する。
        /// 定格: 風量1.5kg/s, 入口空気27°C/W=0.011, 冷水7°C/0.5kg/s, 能力10kW。
        /// </summary>
        private static CrossFinHeatExchanger MakeCoolingCoil()
            => new CrossFinHeatExchanger(
                0.6, 0.4,           // width, height [m]
                4, 6,               // rowNumber, columnNumber
                1.5, 27.0, 0.011,   // ratedAirFlow, ratedInletAirTemp, ratedInletAirHumidity
                80.0,               // borderRelativeHumidity [%]
                0.5, 1.0,           // ratedWaterFlow, maxWaterFlow [kg/s]
                7.0,                // ratedInletWaterTemp [°C]
                CrossFinHeatExchanger.WaterFlowType.SingleFlow,
                10.0,               // heatTransfer [kW]
                false);             // useCorrectionFactor

        /// <summary>
        /// 加熱コイル（簡易モデル）を生成する。
        /// 定格: 風量1.5kg/s, 入口空気15°C/W=0.006, 温水60°C/0.3kg/s, 能力15kW。
        /// </summary>
        private static CrossFinHeatExchanger MakeHeatingCoil()
            => new CrossFinHeatExchanger(
                0.6, 0.4,
                4, 6,
                1.5, 15.0, 0.006,
                80.0,
                0.3, 0.8,
                60.0,
                CrossFinHeatExchanger.WaterFlowType.SingleFlow,
                15.0,
                false);

        #endregion

        // ================================================================
        #region コンストラクタ・プロパティ

        /// <summary>SurfaceArea が正の値になる。</summary>
        [Fact]
        public void Constructor_SurfaceArea_IsPositive()
        {
            var coil = MakeCoolingCoil();
            Assert.True(coil.SurfaceArea > 0, $"SurfaceArea={coil.SurfaceArea:F4} m² > 0");
        }

        /// <summary>RatedAirFlowRate がコンストラクタ指定値と一致する。</summary>
        [Fact]
        public void Constructor_RatedAirFlowRate_MatchesInput()
        {
            var coil = MakeCoolingCoil();
            Assert.InRange(coil.RatedAirFlowRate, 1.49, 1.51);
        }

        /// <summary>MaxWaterFlowRate がコンストラクタ指定値と一致する。</summary>
        [Fact]
        public void Constructor_MaxWaterFlowRate_MatchesInput()
        {
            var coil = MakeCoolingCoil();
            Assert.InRange(coil.MaxWaterFlowRate, 0.99, 1.01);
        }

        #endregion

        // ================================================================
        #region UpdateOutletState — 冷却コイル

        /// <summary>冷却コイル: 出口空気温度が入口より低い。</summary>
        [Fact]
        public void UpdateOutletState_CoolingCoil_OutletAirCooled()
        {
            var coil = MakeCoolingCoil();
            coil.UpdateOutletState(27.0, 0.011, 7.0, 1.5, 0.5);
            Assert.True(coil.OutletAirTemperature < coil.InletAirTemperature,
                $"Outlet={coil.OutletAirTemperature:F2}°C < Inlet={coil.InletAirTemperature:F2}°C");
        }

        /// <summary>冷却コイル: 出口水温が入口水温より高い（水が加熱される）。</summary>
        [Fact]
        public void UpdateOutletState_CoolingCoil_OutletWaterWarmer()
        {
            var coil = MakeCoolingCoil();
            coil.UpdateOutletState(27.0, 0.011, 7.0, 1.5, 0.5);
            Assert.True(coil.OutletWaterTemperature > coil.InletWaterTemperature,
                $"Water outlet={coil.OutletWaterTemperature:F2}°C > inlet={coil.InletWaterTemperature:F2}°C");
        }

        /// <summary>冷却コイル: HeatTransfer が正。</summary>
        [Fact]
        public void UpdateOutletState_CoolingCoil_HeatTransferPositive()
        {
            var coil = MakeCoolingCoil();
            coil.UpdateOutletState(27.0, 0.011, 7.0, 1.5, 0.5);
            Assert.True(coil.HeatTransfer > 0, $"Q={coil.HeatTransfer:F2} kW > 0");
        }

        /// <summary>水流量が多いほど熱交換量が増える。</summary>
        [Fact]
        public void UpdateOutletState_HigherWaterFlow_IncreasesHeatTransfer()
        {
            var coil = MakeCoolingCoil();
            coil.UpdateOutletState(27.0, 0.011, 7.0, 1.5, 0.3);
            double qLow = coil.HeatTransfer;

            coil.UpdateOutletState(27.0, 0.011, 7.0, 1.5, 0.8);
            double qHigh = coil.HeatTransfer;

            Assert.True(qHigh > qLow,
                $"High water flow Q={qHigh:F2} kW > Low Q={qLow:F2} kW");
        }

        /// <summary>風量が多いほど熱交換量が増える。</summary>
        [Fact]
        public void UpdateOutletState_HigherAirFlow_IncreasesHeatTransfer()
        {
            var coil = MakeCoolingCoil();
            coil.UpdateOutletState(27.0, 0.011, 7.0, 1.0, 0.5);
            double qLow = coil.HeatTransfer;

            coil.UpdateOutletState(27.0, 0.011, 7.0, 2.0, 0.5);
            double qHigh = coil.HeatTransfer;

            Assert.True(qHigh > qLow,
                $"High air flow Q={qHigh:F2} kW > Low Q={qLow:F2} kW");
        }

        #endregion

        // ================================================================
        #region UpdateOutletState — 加熱コイル

        /// <summary>加熱コイル: 出口空気温度が入口より高い。</summary>
        [Fact]
        public void UpdateOutletState_HeatingCoil_OutletAirHeated()
        {
            var coil = MakeHeatingCoil();
            coil.UpdateOutletState(15.0, 0.006, 60.0, 1.5, 0.3);
            Assert.True(coil.OutletAirTemperature > coil.InletAirTemperature,
                $"Outlet={coil.OutletAirTemperature:F2}°C > Inlet={coil.InletAirTemperature:F2}°C");
        }

        /// <summary>加熱コイル: 出口水温が入口水温より低い（水が冷却される）。</summary>
        [Fact]
        public void UpdateOutletState_HeatingCoil_OutletWaterCooler()
        {
            var coil = MakeHeatingCoil();
            coil.UpdateOutletState(15.0, 0.006, 60.0, 1.5, 0.3);
            Assert.True(coil.OutletWaterTemperature < coil.InletWaterTemperature,
                $"Water outlet={coil.OutletWaterTemperature:F2}°C < inlet={coil.InletWaterTemperature:F2}°C");
        }

        #endregion

        // ================================================================
        #region ControlOutletAirTemperature

        /// <summary>
        /// 非過負荷時: 出口空気温度が設定値に一致する。
        /// </summary>
        [Fact]
        public void ControlOutletAirTemperature_Normal_OutletReachesSetpoint()
        {
            var coil = MakeCoolingCoil();
            double setpoint = 18.0;
            bool ok = coil.ControlOutletAirTemperature(27.0, 0.011, 7.0, 1.5, setpoint);
            if (ok)
                Assert.InRange(coil.OutletAirTemperature, setpoint - 0.5, setpoint + 0.5);
        }

        /// <summary>
        /// 冷水温度が空気温度より高い（逆転）場合は制御不能（false を返す）。
        /// </summary>
        [Fact]
        public void ControlOutletAirTemperature_Reversed_ReturnsFalse()
        {
            var coil = MakeCoolingCoil();
            // 冷水温度(30°C) > 空気温度(27°C) → 冷却不能
            bool ok = coil.ControlOutletAirTemperature(27.0, 0.011, 30.0, 1.5, 18.0);
            Assert.False(ok);
        }

        #endregion

        // ================================================================
        #region ShutOff

        /// <summary>ShutOff 後は HeatTransfer = 0。</summary>
        [Fact]
        public void ShutOff_ZeroHeatTransfer()
        {
            var coil = MakeCoolingCoil();
            coil.UpdateOutletState(27.0, 0.011, 7.0, 1.5, 0.5);
            Assert.True(coil.HeatTransfer > 0);

            coil.ShutOff();
            Assert.Equal(0.0, coil.HeatTransfer);
        }

        #endregion
    }
}
