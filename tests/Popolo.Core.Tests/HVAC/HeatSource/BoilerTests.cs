/* BoilerTests.cs
 *
 * Copyright (C) 2026 E.Togashi
 * GNU General Public License v3 — see accompanying LICENSE file.
 */

using System;
using Xunit;
using Popolo.Core.HVAC.HeatSource;

namespace Popolo.Core.Tests.HVAC.HeatSource
{
    /// <summary>Unit tests for <see cref="Boiler"/> (static methods).</summary>
    /// <remarks>
    /// Boiler provides static methods for fuel/boiler calculations:
    ///   GetHeatingValue(fuel, isHighValue)      — heating value [MJ/Nm³ or MJ/kg]
    ///   GetTheoreticalAir(fuel)                 — theoretical air [Nm³/Nm³ or Nm³/kg]
    ///   GetFuelConsumption(...)                 — fuel consumption [kg/s or Nm³/s]
    ///   GetHeatLossCoefficient(...)             — heat loss coefficient [kW/K]
    ///   GetOutletWaterTemperature(...)          — hot-water boiler outlet state
    ///   GetSteamFlowRate(...)                   — steam boiler flow rate
    ///
    /// Physical relationships:
    ///   High heating value > Low heating value  (for all fuels)
    ///   More heat load → more fuel consumption
    ///   Higher air ratio → more flue gas, lower efficiency
    /// </remarks>
    public class BoilerTests
    {
        // ================================================================
        #region GetHeatingValue

        /// <summary>都市ガス13A の高位発熱量が 45.6 MJ/Nm³。</summary>
        [Fact]
        public void GetHeatingValue_Gas13A_HighValue_Is45p6()
        {
            double hv = Boiler.GetHeatingValue(Boiler.Fuel.Gas13A, true);
            Assert.InRange(hv, 45.5, 45.7);
        }

        /// <summary>都市ガス13A の低位発熱量が 41.0 MJ/Nm³。</summary>
        [Fact]
        public void GetHeatingValue_Gas13A_LowValue_Is41p0()
        {
            double hv = Boiler.GetHeatingValue(Boiler.Fuel.Gas13A, false);
            Assert.InRange(hv, 40.9, 41.1);
        }

        /// <summary>全燃料で高位 &gt; 低位。</summary>
        [Theory]
        [InlineData(Boiler.Fuel.Gas13A)]
        [InlineData(Boiler.Fuel.LNG)]
        [InlineData(Boiler.Fuel.LPG)]
        [InlineData(Boiler.Fuel.Coal)]
        public void GetHeatingValue_HighValueGreaterThanLow(Boiler.Fuel fuel)
        {
            double high = Boiler.GetHeatingValue(fuel, true);
            double low  = Boiler.GetHeatingValue(fuel, false);
            Assert.True(high > low, $"{fuel}: high={high} > low={low}");
        }

        /// <summary>LNG の高位発熱量が 54.6 MJ/Nm³。</summary>
        [Fact]
        public void GetHeatingValue_LNG_HighValue_Is54p6()
        {
            Assert.InRange(Boiler.GetHeatingValue(Boiler.Fuel.LNG, true), 54.5, 54.7);
        }

        #endregion

        // ================================================================
        #region GetTheoreticalAir

        /// <summary>都市ガス13A の理論空気量が 10.949 Nm³/Nm³。</summary>
        [Fact]
        public void GetTheoreticalAir_Gas13A_IsAbout10p95()
        {
            double ta = Boiler.GetTheoreticalAir(Boiler.Fuel.Gas13A);
            Assert.InRange(ta, 10.9, 11.0);
        }

        /// <summary>全燃料で理論空気量が正。</summary>
        [Theory]
        [InlineData(Boiler.Fuel.Gas13A)]
        [InlineData(Boiler.Fuel.LNG)]
        [InlineData(Boiler.Fuel.LPG)]
        [InlineData(Boiler.Fuel.Coal)]
        public void GetTheoreticalAir_AllFuels_IsPositive(Boiler.Fuel fuel)
        {
            Assert.True(Boiler.GetTheoreticalAir(fuel) > 0);
        }

        #endregion

        // ================================================================
        #region GetFuelConsumption

        /// <summary>熱負荷が大きいほど燃料消費量が増える。</summary>
        [Fact]
        public void GetFuelConsumption_HigherLoad_IncreasesConsumption()
        {
            double fc50  = Boiler.GetFuelConsumption(50,  80, 15, Boiler.Fuel.Gas13A, 200, 1.1, 0, 15, 80);
            double fc100 = Boiler.GetFuelConsumption(100, 80, 15, Boiler.Fuel.Gas13A, 200, 1.1, 0, 15, 80);
            Assert.True(fc100 > fc50, $"fc100={fc100:F6} > fc50={fc50:F6}");
        }

        /// <summary>燃料消費量が正。</summary>
        [Fact]
        public void GetFuelConsumption_IsPositive()
        {
            double fc = Boiler.GetFuelConsumption(100, 80, 15, Boiler.Fuel.Gas13A, 200, 1.1, 0, 15, 80);
            Assert.True(fc > 0, $"FuelConsumption={fc:F6} > 0");
        }

        /// <summary>
        /// 熱損失ゼロ、定格条件での燃料消費量が定格値と逆算一致する。
        /// GetHeatLossCoefficient → GetFuelConsumption の往復整合性。
        /// </summary>
        [Fact]
        public void GetFuelConsumption_RoundTripWithHeatLossCoefficient()
        {
            double heatLoad = 100.0;  // kW
            double outletTemp = 80.0; // °C
            double ambientTemp = 15.0;
            double nominalFuelConsumption = Boiler.GetFuelConsumption(
                heatLoad, outletTemp, ambientTemp,
                Boiler.Fuel.Gas13A, 200, 1.1, 0, ambientTemp, outletTemp);

            // 定格燃料消費量と熱損失係数から逆算
            double heatLossCoef = Boiler.GetHeatLossCoefficient(
                heatLoad, outletTemp, ambientTemp,
                Boiler.Fuel.Gas13A, 200, 1.1, nominalFuelConsumption, ambientTemp);

            // 同じ燃料消費量で再計算
            double fcCheck = Boiler.GetFuelConsumption(
                heatLoad, outletTemp, ambientTemp,
                Boiler.Fuel.Gas13A, 200, 1.1, heatLossCoef, ambientTemp, outletTemp);

            Assert.InRange(fcCheck / nominalFuelConsumption, 0.99, 1.01);
        }

        #endregion

        // ================================================================
        #region GetHeatLossCoefficient

        /// <summary>熱損失係数が非負（断熱ボイラでは0）。</summary>
        [Fact]
        public void GetHeatLossCoefficient_IsNonNegative()
        {
            double fc = Boiler.GetFuelConsumption(100, 80, 15, Boiler.Fuel.Gas13A, 200, 1.1, 0, 15, 80);
            double hlc = Boiler.GetHeatLossCoefficient(100, 80, 15, Boiler.Fuel.Gas13A, 200, 1.1, fc, 15);
            Assert.True(hlc >= 0, $"HeatLossCoefficient={hlc:F4} >= 0");
        }

        #endregion

        // ================================================================
        #region GetOutletWaterTemperature

        /// <summary>
        /// 出口水温が入口水温より高い（加熱）。
        /// </summary>
        [Fact]
        public void GetOutletWaterTemperature_OutletHigherThanInlet()
        {
            double fc = Boiler.GetFuelConsumption(100, 80, 15, Boiler.Fuel.Gas13A, 200, 1.1, 0, 15, 80);
            double outletTemp, heatLoad;
            Boiler.GetOutletWaterTemperature(
                20.0, 0.5, fc, 15, Boiler.Fuel.Gas13A, 200, 1.1, 0, 15, 80,
                out outletTemp, out heatLoad);
            Assert.True(outletTemp > 20.0,
                $"Outlet={outletTemp:F2}°C > Inlet=20°C");
        }

        /// <summary>加熱量が正。</summary>
        [Fact]
        public void GetOutletWaterTemperature_HeatLoadIsPositive()
        {
            double fc = Boiler.GetFuelConsumption(100, 80, 15, Boiler.Fuel.Gas13A, 200, 1.1, 0, 15, 80);
            double outletTemp, heatLoad;
            Boiler.GetOutletWaterTemperature(
                20.0, 0.5, fc, 15, Boiler.Fuel.Gas13A, 200, 1.1, 0, 15, 80,
                out outletTemp, out heatLoad);
            Assert.True(heatLoad > 0, $"HeatLoad={heatLoad:F2} kW > 0");
        }

        #endregion
    }
}
