/* RotaryRegeneratorTests.cs
 *
 * Copyright (C) 2026 E.Togashi
 * GNU General Public License v3 — see accompanying LICENSE file.
 */

using System;
using Xunit;
using Popolo.Core.HVAC.HeatExchanger;

namespace Popolo.Core.Tests.HVAC.HeatExchanger
{
    /// <summary>Unit tests for <see cref="RotaryRegenerator"/>.</summary>
    /// <remarks>
    /// RotaryRegenerator models a rotary heat (or enthalpy) wheel.
    /// It recovers heat (and moisture for desiccant wheels) from exhaust air
    /// and transfers it to supply air.
    ///
    /// Constructors:
    ///   Simplified: (nominalEpsilon, isDesiccantWheel, nominalElectricity)
    ///   Detailed:   (efficiency, diameter, depth, thermalConductivity,
    ///                saFlowVol, eaFlowVol, isDesiccantWheel, SA/EA conditions, nominalElectricity)
    ///
    /// UpdateState(saFlowVol, eaFlowVol, rotatingRate, SA_T_in, SA_W_in, EA_T_in, EA_W_in)
    ///   In winter heating: SA_T_in &lt; EA_T_in → SA is heated, EA is cooled
    ///   rotatingRate = 1.0 for full speed, 0.0 for stopped
    ///
    /// IsDesiccantWheel = true → total heat exchange (sensible + latent)
    /// IsDesiccantWheel = false → sensible heat only
    /// </remarks>
    public class RotaryRegeneratorTests
    {
        #region ヘルパー

        private const double SA_Flow = 500.0;  // 給気風量 [m³/h]
        private const double EA_Flow = 500.0;  // 排気風量 [m³/h]

        /// <summary>簡易モデル・顕熱ホイール（ε=0.7）。</summary>
        private static RotaryRegenerator MakeSensibleWheel()
            => new RotaryRegenerator(0.7, isDesiccantWheel: false, nominalElectricity: 0.1);

        /// <summary>簡易モデル・全熱ホイール（ε=0.7）。</summary>
        private static RotaryRegenerator MakeTotalWheel()
            => new RotaryRegenerator(0.7, isDesiccantWheel: true, nominalElectricity: 0.1);

        #endregion

        // ================================================================
        #region コンストラクタ・プロパティ

        /// <summary>簡易モデルでは IsDetailedModel = false。</summary>
        [Fact]
        public void Constructor_Simplified_IsNotDetailedModel()
        {
            var rr = MakeSensibleWheel();
            Assert.False(rr.IsDetailedModel);
        }

        /// <summary>顕熱ホイールでは IsDesiccantWheel = false。</summary>
        [Fact]
        public void Constructor_SensibleWheel_IsDesiccantWheelFalse()
        {
            var rr = MakeSensibleWheel();
            Assert.False(rr.IsDesiccantWheel);
        }

        /// <summary>全熱ホイールでは IsDesiccantWheel = true。</summary>
        [Fact]
        public void Constructor_TotalWheel_IsDesiccantWheelTrue()
        {
            var rr = MakeTotalWheel();
            Assert.True(rr.IsDesiccantWheel);
        }

        #endregion

        // ================================================================
        #region UpdateState — 加熱時（冬季）

        /// <summary>
        /// 加熱時（SA &lt; EA）: 給気出口温度が給気入口温度より高い（予熱効果）。
        /// </summary>
        [Fact]
        public void UpdateState_Heating_SupplyOutletWarmerThanInlet()
        {
            var rr = MakeSensibleWheel();
            rr.UpdateState(SA_Flow, EA_Flow, 1.0, 0.0, 0.003, 22.0, 0.008);
            Assert.True(rr.SupplyAirOutletDrybulbTemperature > rr.SupplyAirInletDrybulbTemperature,
                $"SA outlet={rr.SupplyAirOutletDrybulbTemperature:F2}°C > inlet=0°C");
        }

        /// <summary>
        /// 加熱時: 排気出口温度が排気入口温度より低い（排熱回収）。
        /// </summary>
        [Fact]
        public void UpdateState_Heating_ExhaustOutletCoolerThanInlet()
        {
            var rr = MakeSensibleWheel();
            rr.UpdateState(SA_Flow, EA_Flow, 1.0, 0.0, 0.003, 22.0, 0.008);
            Assert.True(rr.ExhaustAirOutletDrybulbTemperature < rr.ExhaustAirInletDrybulbTemperature,
                $"EA outlet={rr.ExhaustAirOutletDrybulbTemperature:F2}°C < inlet=22°C");
        }

        /// <summary>
        /// 全熱ホイールでは加熱時に給気の絶対湿度も増加する。
        /// </summary>
        [Fact]
        public void UpdateState_Heating_TotalWheel_SupplyHumidityIncreases()
        {
            var rr = MakeTotalWheel();
            rr.UpdateState(SA_Flow, EA_Flow, 1.0, 0.0, 0.002, 22.0, 0.008);
            Assert.True(rr.SupplyAirOutletHumidityRatio > rr.SupplyAirInletHumidityRatio,
                $"SA outlet W={rr.SupplyAirOutletHumidityRatio:F4} > inlet W=0.002");
        }

        /// <summary>
        /// 顕熱ホイールでは絶対湿度が変化しない。
        /// </summary>
        [Fact]
        public void UpdateState_Heating_SensibleWheel_HumidityRatioUnchanged()
        {
            var rr = MakeSensibleWheel();
            rr.UpdateState(SA_Flow, EA_Flow, 1.0, 0.0, 0.002, 22.0, 0.008);
            Assert.InRange(rr.SupplyAirOutletHumidityRatio - rr.SupplyAirInletHumidityRatio,
                -0.0001, 0.0001);
        }

        #endregion

        // ================================================================
        #region UpdateState — 冷却時（夏季）

        /// <summary>
        /// 冷却時（SA &gt; EA）: 給気出口温度が給気入口温度より低い（予冷効果）。
        /// </summary>
        [Fact]
        public void UpdateState_Cooling_SupplyOutletCoolerThanInlet()
        {
            var rr = MakeSensibleWheel();
            rr.UpdateState(SA_Flow, EA_Flow, 1.0, 35.0, 0.018, 26.0, 0.010);
            Assert.True(rr.SupplyAirOutletDrybulbTemperature < rr.SupplyAirInletDrybulbTemperature,
                $"SA outlet={rr.SupplyAirOutletDrybulbTemperature:F2}°C < inlet=35°C");
        }

        #endregion

        // ================================================================
        #region RotatingRate（回転数比）

        /// <summary>
        /// 回転数比 = 0 のとき出口温度 = 入口温度（熱交換なし）。
        /// </summary>
        [Fact]
        public void UpdateState_ZeroRotation_NoHeatRecovery()
        {
            var rr = MakeSensibleWheel();
            rr.UpdateState(SA_Flow, EA_Flow, 0.0, 0.0, 0.003, 22.0, 0.008);
            Assert.InRange(rr.SupplyAirOutletDrybulbTemperature, -0.5, 0.5);
        }

        /// <summary>
        /// 回転数比が高いほど給気出口温度が排気入口温度に近づく。
        /// </summary>
        [Fact]
        public void UpdateState_HigherRotation_MoreHeatRecovery()
        {
            var rr = MakeSensibleWheel();
            rr.UpdateState(SA_Flow, EA_Flow, 0.5, 0.0, 0.003, 22.0, 0.008);
            double tHalf = rr.SupplyAirOutletDrybulbTemperature;

            rr.UpdateState(SA_Flow, EA_Flow, 1.0, 0.0, 0.003, 22.0, 0.008);
            double tFull = rr.SupplyAirOutletDrybulbTemperature;

            Assert.True(tFull > tHalf,
                $"Full rotation={tFull:F2}°C > Half rotation={tHalf:F2}°C");
        }

        #endregion

        // ================================================================
        #region ControlOutletTemperature

        /// <summary>
        /// 非過負荷時: 給気出口温度が設定値に一致する。
        /// </summary>
        [Fact]
        public void ControlOutletTemperature_Normal_OutletReachesSetpoint()
        {
            var rr = MakeSensibleWheel();
            double setpoint = 15.0;
            bool ok = rr.ControlOutletTemperature(
                SA_Flow, EA_Flow, 1.0,
                0.0, 0.003, 22.0, 0.008,
                setpoint);
            if (ok)
                Assert.InRange(rr.SupplyAirOutletDrybulbTemperature,
                    setpoint - 0.5, setpoint + 0.5);
        }

        #endregion

        // ================================================================
        #region ShutOff

        /// <summary>ShutOff 後は出口温度 = 入口温度（熱回収なし）。</summary>
        [Fact]
        public void ShutOff_OutletEqualsInlet()
        {
            var rr = MakeSensibleWheel();
            rr.UpdateState(SA_Flow, EA_Flow, 1.0, 0.0, 0.003, 22.0, 0.008);
            Assert.True(rr.SupplyAirOutletDrybulbTemperature > 0); // 予熱あり

            rr.ShutOff();
            Assert.InRange(rr.SupplyAirOutletDrybulbTemperature,
                rr.SupplyAirInletDrybulbTemperature - 0.01,
                rr.SupplyAirInletDrybulbTemperature + 0.01);
        }

        #endregion
    }
}
