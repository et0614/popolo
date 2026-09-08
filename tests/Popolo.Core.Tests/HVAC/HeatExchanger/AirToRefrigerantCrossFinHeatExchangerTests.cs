/* AirToRefrigerantCrossFinHeatExchangerTests.cs
 *
 * Copyright (C) 2026 E.Togashi
 * GNU General Public License v3 — see accompanying LICENSE file.
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

    }
}
