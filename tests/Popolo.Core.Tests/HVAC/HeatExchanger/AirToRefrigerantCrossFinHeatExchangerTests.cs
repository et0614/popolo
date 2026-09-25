/* AirToRefrigerantCrossFinHeatExchangerTests.cs
 *
 * Copyright (C) 2026 E.Togashi
 * Licensed under the Apache License, Version 2.0 - see the accompanying LICENSE file.
 */

using System;
using Xunit;
using Popolo.Core.Physics;
using Popolo.Core.HVAC.VRF;
using Coil = Popolo.Core.HVAC.HeatExchanger.AirToRefrigerantCrossFinHeatExchanger;

namespace Popolo.Core.Tests.HVAC.HeatExchanger
{
    /// <summary>Unit tests for <see cref="Coil"/>.</summary>
    /// <remarks>
    /// The class was extracted verbatim from the static coil physics of <see cref="VRFUnit"/>;
    /// these tests therefore assert BIT-IDENTICAL results between the unified API of the new
    /// core and the delegating wrappers kept on <see cref="VRFUnit"/>, across the dry, wet and
    /// frosted regimes, with and without water spray, for both solve directions.
    /// </remarks>
    public class AirToRefrigerantCrossFinHeatExchangerTests
    {
        private const double K = VRFUnit.HeatTransferCoefficient;
        private static readonly double AirFlow = 167.0 / 60 * 1.2;

        private static double Hr(double dbt, double rh)
            => MoistAir.GetHumidityRatioFromDryBulbTemperatureAndRelativeHumidity(
                dbt, rh, PhysicsConstants.StandardAtmosphericPressure);

        /// <summary>Evaporator surface area at the rated condition of VRFUnitTests.</summary>
        private static double EvpArea()
            => VRFUnit.GetEvaporatorSurfaceArea(AirFlow, 2.0, -13.0, 7.0, Hr(7.0, 85.0), 95.0);

        /// <summary>Condenser surface area at the rated condition of VRFUnitTests.</summary>
        private static double CndArea()
            => VRFUnit.GetCondenserSurfaceArea(AirFlow, 45.0, 25.0, 35.0, Hr(35.0, 55.0));

        // ================================================================
        #region Surface area (unified dispatch)

        [Fact]
        public void GetSurfaceArea_Cooling_BitIdenticalToVRFUnit()
        {
            double hr = Hr(7.0, 85.0);
            double expected = VRFUnit.GetEvaporatorSurfaceArea(AirFlow, 2.0, -13.0, 7.0, hr, 95.0);
            double actual = Coil.GetSurfaceArea(K, AirFlow, 2.0, -13.0, 7.0, hr, 95.0);
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void GetSurfaceArea_Heating_BitIdenticalToVRFUnit()
        {
            double hr = Hr(35.0, 55.0);
            double expected = VRFUnit.GetCondenserSurfaceArea(AirFlow, 45.0, 25.0, 35.0, hr);
            double actual = Coil.GetSurfaceArea(K, AirFlow, 45.0, 25.0, 35.0, hr, 95.0);
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void GetSurfaceArea_ZeroHeatTransfer_Throws()
        {
            Assert.ThrowsAny<Exception>(() =>
                Coil.GetSurfaceArea(K, AirFlow, 2.0, 0.0, 7.0, Hr(7.0, 85.0), 95.0));
        }

        #endregion

        // ================================================================
        #region Forward solve (unified dispatch)

        /// <summary>Cooling forward solve: dry, wet and frosted regimes.</summary>
        [Theory]
        [InlineData(15.0, 30.0, 30.0)] //Dry regime (warm dry air, high refrigerant temperature)
        [InlineData(2.0, 7.0, 85.0)]   //Wet regime (rated VRF condition)
        [InlineData(-10.0, 2.0, 85.0)] //Frosted regime (cold humid air, low refrigerant temperature)
        public void GetHeatTransfer_Cooling_BitIdenticalToVRFUnit(double refT, double tIn, double rhIn)
        {
            double hr = Hr(tIn, rhIn);
            double area = EvpArea();

            VRFUnit.GetEvaporatorHeatTransfer(refT, AirFlow, area, tIn, hr, 95.0,
                out double ht0, out double to0, out double wo0, out double sd0, out double sw0, out double dfl0);

            Coil.GetHeatTransfer(K, refT, AirFlow, area, tIn, hr, 95.0, 0,
                out double ht1, out double to1, out double wo1,
                out double sd1, out double sw1, out double dfl1, out double ws1);

            Assert.Equal(ht0, ht1);
            Assert.Equal(to0, to1);
            Assert.Equal(wo0, wo1);
            Assert.Equal(sd0, sd1);
            Assert.Equal(sw0, sw1);
            Assert.Equal(dfl0, dfl1);
            Assert.Equal(0.0, ws1);
        }

        /// <summary>Heating forward solve, with and without water spray.</summary>
        [Theory]
        [InlineData(0.0)]
        [InlineData(0.5)]
        public void GetHeatTransfer_Heating_BitIdenticalToVRFUnit(double spray)
        {
            double hr = Hr(35.0, 55.0);
            double area = CndArea();

            VRFUnit.GetCondenserHeatTransfer(45.0, AirFlow, AirFlow, area, 35.0, hr, spray,
                out double ht0, out double to0, out double wo0, out double ws0);

            Coil.GetHeatTransfer(K, 45.0, AirFlow, area, 35.0, hr, 95.0, spray,
                out double ht1, out double to1, out double wo1,
                out double sd1, out double sw1, out double dfl1, out double ws1);

            Assert.Equal(ht0, ht1);
            Assert.Equal(to0, to1);
            Assert.Equal(wo0, wo1);
            Assert.Equal(ws0, ws1);
            Assert.Equal(area, sd1);
            Assert.Equal(0.0, sw1);
            Assert.Equal(0.0, dfl1);
        }

        #endregion

        // ================================================================
        #region Inverse solve (unified dispatch)

        [Theory]
        [InlineData(-13.0, 7.0, 85.0, false)] //Wet regime, rated load
        [InlineData(-13.0, 7.0, 85.0, true)]  //Wet regime, deducting the defrost load
        [InlineData(-8.0, 2.0, 85.0, false)]  //Frost-prone condition
        public void GetRefrigerantTemperature_Cooling_BitIdenticalToVRFUnit(
            double heat, double tIn, double rhIn, bool deduct)
        {
            double hr = Hr(tIn, rhIn);
            double area = EvpArea();

            VRFUnit.GetEvaporatingTemperature(heat, AirFlow, area, tIn, hr, 95.0, deduct,
                out double te0, out double to0, out double wo0, out double sd0, out double sw0, out double dfl0);

            Coil.GetRefrigerantTemperature(K, heat, AirFlow, area, tIn, hr, 95.0, deduct, 0,
                out double te1, out double to1, out double wo1,
                out double sd1, out double sw1, out double dfl1, out double ws1);

            Assert.Equal(te0, te1);
            Assert.Equal(to0, to1);
            Assert.Equal(wo0, wo1);
            Assert.Equal(sd0, sd1);
            Assert.Equal(sw0, sw1);
            Assert.Equal(dfl0, dfl1);
            Assert.Equal(0.0, ws1);
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(0.5)]
        public void GetRefrigerantTemperature_Heating_BitIdenticalToVRFUnit(double spray)
        {
            double hr = Hr(35.0, 55.0);
            double area = CndArea();

            VRFUnit.GetCondensingTemperature(25.0, AirFlow, area, 35.0, hr, spray,
                out double tc0, out double to0, out double wo0, out double ws0);

            Coil.GetRefrigerantTemperature(K, 25.0, AirFlow, area, 35.0, hr, 95.0, false, spray,
                out double tc1, out double to1, out double wo1,
                out double sd1, out double sw1, out double dfl1, out double ws1);

            Assert.Equal(tc0, tc1);
            Assert.Equal(to0, to1);
            Assert.Equal(wo0, wo1);
            Assert.Equal(ws0, ws1);
            Assert.Equal(area, sd1);
            Assert.Equal(0.0, sw1);
            Assert.Equal(0.0, dfl1);
        }

        #endregion

        // ================================================================
        #region Outlet air setpoint solve (unified dispatch)

        [Fact]
        public void GetRefrigerantTemperatureForOutletAirTemperature_Cooling_BitIdenticalToVRFUnit()
        {
            double hr = Hr(7.0, 85.0);
            double area = EvpArea();

            VRFUnit.ControlOutletAirTemperature(4.0, AirFlow, area, 7.0, hr, 95.0,
                out double te0, out double ht0, out double wo0, out double sd0, out double sw0, out double dfl0);

            Coil.GetRefrigerantTemperatureForOutletAirTemperature(K, 4.0, AirFlow, area, 7.0, hr, 95.0, 0,
                out double te1, out double ht1, out double wo1,
                out double sd1, out double sw1, out double dfl1, out double ws1);

            Assert.Equal(te0, te1);
            Assert.Equal(ht0, ht1);
            Assert.Equal(wo0, wo1);
            Assert.Equal(sd0, sd1);
            Assert.Equal(sw0, sw1);
            Assert.Equal(dfl0, dfl1);
            Assert.Equal(0.0, ws1);
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(0.5)]
        public void GetRefrigerantTemperatureForOutletAirTemperature_Heating_BitIdenticalToVRFUnit(double spray)
        {
            double hr = Hr(35.0, 55.0);
            double area = CndArea();

            VRFUnit.ControlOutletAirTemperature(40.0, AirFlow, area, 35.0, hr, spray,
                out double tc0, out double ht0, out double wo0, out double ws0);

            Coil.GetRefrigerantTemperatureForOutletAirTemperature(K, 40.0, AirFlow, area, 35.0, hr, 95.0, spray,
                out double tc1, out double ht1, out double wo1,
                out double sd1, out double sw1, out double dfl1, out double ws1);

            Assert.Equal(tc0, tc1);
            Assert.Equal(ht0, ht1);
            Assert.Equal(wo0, wo1);
            Assert.Equal(ws0, ws1);
            Assert.Equal(area, sd1);
            Assert.Equal(0.0, sw1);
            Assert.Equal(0.0, dfl1);
        }

        #endregion

        // ================================================================
        #region Water spray helper

        [Fact]
        public void ApplyWaterSpray_CoolsAndHumidifiesInletAir()
        {
            double t = 35.0, w = Hr(35.0, 55.0);
            double t0 = t, w0 = w;
            double supply = Coil.ApplyWaterSpray(ref t, ref w, 0.5, AirFlow);
            Assert.True(t < t0, $"sprayed temperature {t:F2}°C < {t0:F2}°C");
            Assert.True(w0 < w, $"sprayed humidity ratio {w:F5} > {w0:F5}");
            Assert.True(0 < supply, $"water supply {supply:F6} kg/s > 0");
        }

        /// <summary>Water consumption equals the moisture gained by the air stream (mass balance).</summary>
        [Fact]
        public void ApplyWaterSpray_WaterSupplyMatchesMoistureGain()
        {
            double t = 35.0, w = Hr(35.0, 55.0);
            double w0 = w;
            double supply = Coil.ApplyWaterSpray(ref t, ref w, 0.5, AirFlow);
            double gain = AirFlow * (w - w0);
            Assert.True(Math.Abs(supply - gain) < 1e-12,
                $"water supply {supply:E6} kg/s equals air moisture gain {gain:E6} kg/s");
        }

        #endregion

        // ================================================================
        #region Frost penalty parameter

        /// <summary>着霜ペナルティ1.0（着霜フリー）は0.6より熱交換量が大きい。</summary>
        [Fact]
        public void FrostPenalty_One_TransfersMoreThanDefault()
        {
            double hr = Hr(2.0, 85.0);
            double area = EvpArea();

            Coil.GetHeatTransfer(K, -10.0, AirFlow, area, 2.0, hr, 95.0, 0,
                out double htDef, out _, out _, out _, out double swDef, out double dflDef, out _);
            Coil.GetHeatTransfer(K, -10.0, AirFlow, area, 2.0, hr, 95.0, 0, 1.0,
                out double htFree, out _, out _, out _, out _, out _, out _);

            Assert.True(swDef < area && 0 < dflDef,
                $"precondition: frosted section exists (sW={swDef:F3} m2, defrost={dflDef:F3} kW)");
            Assert.True(htFree < htDef,
                $"frost-free transfers more cooling: {htFree:F3} < {htDef:F3} kW");
        }

        /// <summary>着霜区分が生じない条件ではペナルティ値は結果に影響しない。</summary>
        [Fact]
        public void FrostPenalty_NoFrostRegime_HasNoEffect()
        {
            double hr = Hr(7.0, 85.0);
            double area = EvpArea();

            Coil.GetHeatTransfer(K, 2.0, AirFlow, area, 7.0, hr, 95.0, 0, 0.3,
                out double ht1, out double to1, out double wo1, out _, out _, out _, out _);
            Coil.GetHeatTransfer(K, 2.0, AirFlow, area, 7.0, hr, 95.0, 0, 1.0,
                out double ht2, out double to2, out double wo2, out _, out _, out _, out _);

            Assert.Equal(ht1, ht2);
            Assert.Equal(to1, to2);
            Assert.Equal(wo1, wo2);
        }

        /// <summary>旧シグネチャは DefaultFrostPenalty を渡す新オーバーロードと完全一致。</summary>
        [Fact]
        public void FrostPenalty_DefaultOverload_BitIdentical()
        {
            double hr = Hr(2.0, 85.0);
            double area = EvpArea();

            Coil.GetHeatTransfer(K, -10.0, AirFlow, area, 2.0, hr, 95.0, 0,
                out double ht0, out double to0, out double wo0, out double sd0, out double sw0, out double dfl0, out _);
            Coil.GetHeatTransfer(K, -10.0, AirFlow, area, 2.0, hr, 95.0, 0,
                Coil.DefaultFrostPenalty,
                out double ht1, out double to1, out double wo1, out double sd1, out double sw1, out double dfl1, out _);

            Assert.Equal(ht0, ht1);
            Assert.Equal(to0, to1);
            Assert.Equal(wo0, wo1);
            Assert.Equal(sd0, sd1);
            Assert.Equal(sw0, sw1);
            Assert.Equal(dfl0, dfl1);
        }

        #endregion

        // ================================================================
        #region Cooling inverse solve: bracket robustness

        /// <summary>定格14kW室内機（JIS定格で表面積を決定）の風量 [kg/s]。</summary>
        private static readonly double RatedIndoorAirFlow = 34.5 * 1.2 / 60.0;

        /// <summary>定格14kW室内機の蒸発器表面積 [m2]。</summary>
        private static double RatedIndoorArea()
            => VRFSystem.MakeIndoorUnit_Cooling(RatedIndoorAirFlow, 0, -14.0).EvaporatorSurfaceArea;

        /// <summary>
        /// 高湿度の入口空気（27°C, RH50〜95%）で定格の1.3倍までの冷却負荷を与えても
        /// 蒸発温度の逆算が例外を出さず、戻り値が残差式を満たす。
        /// （全顕熱の出口温度を初期値とする旧ブラケットでは潜熱分を賄えず失敗していた）
        /// </summary>
        [Theory]
        [InlineData(50.0, false)]
        [InlineData(60.0, false)]
        [InlineData(70.0, false)]
        [InlineData(80.0, false)]
        [InlineData(90.0, false)]
        [InlineData(95.0, false)]
        [InlineData(70.0, true)]
        [InlineData(95.0, true)]
        public void GetRefrigerantTemperature_Cooling_HumidAir_DoesNotThrowAndSatisfiesResidual(
            double rhIn, bool deduct)
        {
            double area = RatedIndoorArea();
            double hr = Hr(27.0, rhIn);
            foreach (double heat in new[] { -1.0, -3.0, -6.0, -10.0, -14.0, -16.0, -18.2 })
            {
                Coil.GetRefrigerantTemperature(K, heat, RatedIndoorAirFlow, area, 27.0, hr, 95.0, deduct, 0,
                    out double te, out double to, out double wo,
                    out _, out _, out double dfl, out _);

                Coil.GetHeatTransfer(K, te, RatedIndoorAirFlow, area, 27.0, hr, 95.0, 0,
                    out double ht, out double to2, out double wo2, out _, out _, out double dfl2, out _);
                double residual = deduct ? ht - dfl2 - heat : ht - heat;
                Assert.True(Math.Abs(residual) < 0.01,
                    $"RH={rhIn}%, Q={heat} kW: residual {residual:E3} kW at Te={te:F3}°C");
                Assert.True(te < 27.0, $"RH={rhIn}%, Q={heat} kW: Te={te:F3}°C below inlet air");
                Assert.Equal(to2, to);
                Assert.Equal(wo2, wo);
                Assert.Equal(dfl2, dfl);
            }
        }

        /// <summary>
        /// 乾き空気・小負荷など旧ブラケットで解けていた条件では、結果が旧ブラケット
        /// [T0-20, T0+5] で直接 Brent 法を適用した値とビット単位で一致する。
        /// </summary>
        [Theory]
        [InlineData(-13.0, 7.0, 85.0)]
        [InlineData(-8.0, 2.0, 85.0)]
        [InlineData(-5.0, 27.0, 40.0)]
        public void GetRefrigerantTemperature_Cooling_PreviouslyBracketed_Unchanged(
            double heat, double tIn, double rhIn)
        {
            double hr = Hr(tIn, rhIn);
            double area = EvpArea();
            Coil.GetRefrigerantTemperature(K, heat, AirFlow, area, tIn, hr, 95.0, false, 0,
                out double te, out _, out _, out _, out _, out _, out _);

            double t0 = tIn + heat / (AirFlow * 1.006);
            double expected = Popolo.Core.Numerics.Roots.Brent(t0 - 20, t0 + 5, 0.001, eTemp =>
            {
                Coil.GetHeatTransfer(K, eTemp, AirFlow, area, tIn, hr, 95.0, 0,
                    out double ht, out _, out _, out _, out _, out _, out _);
                return ht - heat;
            });
            Assert.Equal(expected, te);
        }

        /// <summary>
        /// 蒸発温度逆算の前提：冷却熱量（負値、除霜負荷控除の有無とも）は蒸発温度に対して
        /// 単調非減少で、入口空気温度では0となる。
        /// 注：入口相対湿度が境界相対湿度以上の場合、露点近傍（乾き判定と湿り判定の切替点）で
        /// 既存モデルに小さな不連続があるため、単調性は20°C以下の範囲で確認する。
        /// </summary>
        [Theory]
        [InlineData(50.0)]
        [InlineData(70.0)]
        [InlineData(95.0)]
        public void GetHeatTransfer_Cooling_MonotonicInRefrigerantTemperature(double rhIn)
        {
            double area = RatedIndoorArea();
            double hr = Hr(27.0, rhIn);
            double prevHt = double.NegativeInfinity, prevNet = double.NegativeInfinity;
            for (double te = -60.0; te <= 20.0; te += 0.25)
            {
                Coil.GetHeatTransfer(K, te, RatedIndoorAirFlow, area, 27.0, hr, 95.0, 0,
                    out double ht, out _, out _, out _, out _, out double dfl, out _);
                Assert.True(ht < 0, $"RH={rhIn}%: cooling at Te={te}°C");
                Assert.True(prevHt <= ht + 1e-9, $"RH={rhIn}%: ht not monotonic at Te={te}°C");
                Assert.True(prevNet <= ht - dfl + 1e-9, $"RH={rhIn}%: ht-dfl not monotonic at Te={te}°C");
                prevHt = ht;
                prevNet = ht - dfl;
            }
            Coil.GetHeatTransfer(K, 27.0, RatedIndoorAirFlow, area, 27.0, hr, 95.0, 0,
                out double ht0, out _, out _, out _, out _, out double dfl0, out _);
            Assert.Equal(0.0, ht0, 12);
            Assert.Equal(0.0, dfl0, 12);
        }

        /// <summary>コイルの処理能力を超える冷却負荷は明示的な数値例外となる。</summary>
        [Fact]
        public void GetRefrigerantTemperature_Cooling_LoadBeyondCapability_ThrowsNumericalException()
        {
            double area = RatedIndoorArea();
            Assert.Throws<Popolo.Core.Exceptions.PopoloNumericalException>(() =>
                Coil.GetRefrigerantTemperature(K, -300.0, RatedIndoorAirFlow, area, 27.0, Hr(27.0, 50.0),
                    95.0, false, 0, out _, out _, out _, out _, out _, out _, out _));
        }

        #endregion

    }
}
