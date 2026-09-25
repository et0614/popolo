/* AirToWaterCrossFinHeatExchangerTests.cs
 *
 * Copyright (C) 2026 E.Togashi
 * Licensed under the Apache License, Version 2.0 - see the accompanying LICENSE file.
 */

using System;
using Xunit;
using Popolo.Core.HVAC.HeatExchanger;

namespace Popolo.Core.Tests.HVAC.HeatExchanger
{
    /// <summary>Unit tests for <see cref="AirToWaterCrossFinHeatExchanger"/>.</summary>
    /// <remarks>
    /// AirToWaterCrossFinHeatExchanger models a plate-fin-and-tube air-water coil.
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
    public class AirToWaterCrossFinHeatExchangerTests
    {
        #region Helpers

        /// <summary>
        /// 冷却コイル（簡易モデル）を生成する。
        /// 定格: 風量1.5kg/s, 入口空気27°C/W=0.011, 冷水7°C/0.5kg/s, 能力10kW。
        /// </summary>
        private static AirToWaterCrossFinHeatExchanger MakeCoolingCoil()
            => new AirToWaterCrossFinHeatExchanger(
                0.6, 0.4,           // width, height [m]
                4, 6,               // rowNumber, columnNumber
                1.5, 27.0, 0.011,   // ratedAirFlow, ratedInletAirTemp, ratedInletAirHumidity
                80.0,               // borderRelativeHumidity [%]
                0.5, 1.0,           // ratedWaterFlow, maxWaterFlow [kg/s]
                7.0,                // ratedInletWaterTemp [°C]
                AirToWaterCrossFinHeatExchanger.WaterFlowType.SingleFlow,
                10.0,               // heatTransfer [kW]
                false);             // useCorrectionFactor

        /// <summary>
        /// 加熱コイル（簡易モデル）を生成する。
        /// 定格: 風量1.5kg/s, 入口空気15°C/W=0.006, 温水60°C/0.3kg/s, 能力15kW。
        /// </summary>
        private static AirToWaterCrossFinHeatExchanger MakeHeatingCoil()
            => new AirToWaterCrossFinHeatExchanger(
                0.6, 0.4,
                4, 6,
                1.5, 15.0, 0.006,
                80.0,
                0.3, 0.8,
                60.0,
                AirToWaterCrossFinHeatExchanger.WaterFlowType.SingleFlow,
                15.0,
                false);

        #endregion

        // ================================================================
        #region Constructors and properties

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
        #region UpdateOutletState — cooling coil

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
        #region UpdateOutletState — heating coil

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
        #region Static solvers

        /// <summary>乾湿境界相対湿度 [%]（MakeCoolingCoil と同じ）。</summary>
        private const double BORDER_RH = 80.0;

        private static double Hr(double dbt, double rh)
            => Popolo.Core.Physics.MoistAir.GetHumidityRatioFromDryBulbTemperatureAndRelativeHumidity(
                dbt, rh, Popolo.Core.Physics.PhysicsConstants.StandardAtmosphericPressure);

        /// <summary>MakeCoolingCoil と同じ詳細モデル形状（既定の管径・フィン諸元）。</summary>
        private static void GetGeometry(out double asr, out double car, out double eqr,
            out double eqd, out double area)
        {
            AirToWaterCrossFinHeatExchanger.GetGeometricConfiguration(
                4 * 0.0329, 0.6, 0.4, 4, 6, 0.0029, 0.0002, 0.0146, 0.0158,
                out asr, out car, out eqr, out eqd, out double asa);
            area = asa * 4;
        }

        /// <summary>
        /// 入口空気の相対湿度が乾湿境界相対湿度を上回る場合、コイル全面が湿りとなり
        /// （乾きコイル比率0）、例外を出さずに冷却・除湿された出口状態を返す。
        /// （旧実装では乾きコイル比率の Brent 法の両端が同符号となり例外になっていた）
        /// </summary>
        [Fact]
        public void GetOutletState_InletAboveBorderHumidity_FullyWet()
        {
            GetGeometry(out _, out _, out _, out _, out double area);
            AirToWaterCrossFinHeatExchanger.GetHeatTransferCoefficient(1.0, 3.0, out double kd, out double kw);
            double hr = Hr(27.0, 90.0);
            AirToWaterCrossFinHeatExchanger.GetOutletState(27.0, hr, BORDER_RH, 7.0, 1.5, 0.5,
                kd, kw, area, out double ta, out double xa, out double tw, out double dr);
            Assert.Equal(0.0, dr);
            Assert.True(ta < 27.0, $"outlet air {ta:F2}°C cooled");
            Assert.True(xa < hr, $"outlet humidity {xa:F5} dehumidified");
            Assert.True(7.0 < tw && tw < 27.0, $"outlet water {tw:F2}°C warmed");

            //空気側と水側の熱収支が一致する
            double qAir = 1.5 * (Popolo.Core.Physics.MoistAir.GetEnthalpyFromDryBulbTemperatureAndHumidityRatio(27.0, hr)
                - Popolo.Core.Physics.MoistAir.GetEnthalpyFromDryBulbTemperatureAndHumidityRatio(ta, xa));
            double qWater = 0.5 * 0.001 * Popolo.Core.Physics.PhysicsConstants.NominalWaterIsobaricSpecificHeat * (tw - 7.0);
            Assert.True(Math.Abs(qAir - qWater) < 0.02 * qWater, $"air {qAir:F3} kW vs water {qWater:F3} kW");
        }

        /// <summary>
        /// 部分的に乾きコイルとなる通常条件：乾きコイル比率は (0, 1) の範囲で、
        /// 空気側と水側の熱収支が一致する。
        /// </summary>
        [Fact]
        public void GetOutletState_PartiallyWet_EnergyBalance()
        {
            GetGeometry(out _, out _, out _, out _, out double area);
            AirToWaterCrossFinHeatExchanger.GetHeatTransferCoefficient(1.0, 3.0, out double kd, out double kw);
            double hr = Hr(27.0, 70.0);
            AirToWaterCrossFinHeatExchanger.GetOutletState(27.0, hr, BORDER_RH, 7.0, 1.5, 0.5,
                kd, kw, area, out double ta, out double xa, out double tw, out double dr);
            Assert.InRange(dr, 1e-6, 1 - 1e-6);
            double qAir = 1.5 * (Popolo.Core.Physics.MoistAir.GetEnthalpyFromDryBulbTemperatureAndHumidityRatio(27.0, hr)
                - Popolo.Core.Physics.MoistAir.GetEnthalpyFromDryBulbTemperatureAndHumidityRatio(ta, xa));
            double qWater = 0.5 * 0.001 * Popolo.Core.Physics.PhysicsConstants.NominalWaterIsobaricSpecificHeat * (tw - 7.0);
            Assert.True(Math.Abs(qAir - qWater) < 0.02 * qWater, $"air {qAir:F3} kW vs water {qWater:F3} kW");
        }

        /// <summary>
        /// 詳細モデルの静的 GetWaterFlowRate は熱通過率の計算に入口空気温度を渡す
        /// （旧実装は入口絶対湿度を温度の引数に渡していた）。
        /// 戻り値の水量±ソルバ許容差で、正しい熱通過率による出口空気温度が設定値を挟む。
        /// </summary>
        [Fact]
        public void GetWaterFlowRate_Detailed_UsesInletAirTemperatureForCoefficients()
        {
            GetGeometry(out double asr, out double car, out double eqr, out double eqd, out double area);
            const double TIN = 27.0, TW = 7.0, AF = 1.5, MAXW = 1.0, SP = 22.0;
            const double WPATH = 6, FT = 0.0002, TC = 237, ID = 0.0146, OD = 0.0158;
            double hr = Hr(TIN, 50.0);

            double wf = AirToWaterCrossFinHeatExchanger.GetWaterFlowRate(asr, car, eqr, eqd, WPATH, FT, TC,
                ID, OD, AF, TIN, hr, BORDER_RH, 0.5, TW, MAXW, area, SP);

            double OutletTemp(double w)
            {
                AirToWaterCrossFinHeatExchanger.GetHeatTransferCoefficient(asr, car, eqr, eqd, WPATH, FT, TC,
                    ID, OD, AF, TIN, hr, BORDER_RH, w, TW, out double kd, out double kw);
                AirToWaterCrossFinHeatExchanger.GetOutletState(TIN, hr, BORDER_RH, TW, AF, w, kd, kw, area,
                    out double ta, out _, out _, out _);
                return ta;
            }

            //Brent 法（許容差 0.01 kg/s）の解の位置誤差の範囲内で設定値を挟む
            Assert.True(0 < wf && wf < MAXW, $"wf={wf:F4} kg/s within (0, max)");
            double tLow = OutletTemp(Math.Max(1e-6, wf - 0.02));
            double tHigh = OutletTemp(Math.Min(MAXW, wf + 0.02));
            Assert.True(tHigh <= SP && SP <= tLow,
                $"setpoint {SP}°C between {tHigh:F4}°C and {tLow:F4}°C at wf={wf:F4} kg/s");

            //正しい引数で同じ手順を踏んだ結果と一致する
            double reference = Popolo.Core.Numerics.Roots.Brent(0, MAXW, 0.01, w => SP - OutletTemp(w));
            Assert.Equal(reference, wf);
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
