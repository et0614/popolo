/* HotWaterBoilerTests.cs
 *
 * Copyright (C) 2026 E.Togashi
 * GNU General Public License v3 — see accompanying LICENSE file.
 */

using System;
using Xunit;
using Popolo.Core.HVAC.HeatSource;

namespace Popolo.Core.Tests.HVAC.HeatSource
{
    /// <summary>Unit tests for <see cref="HotWaterBoiler"/>.</summary>
    /// <remarks>
    /// HotWaterBoiler models a fuel-fired hot-water boiler.
    ///
    /// Constructor: (inletWaterTemp, outletWaterTemp, waterFlowRate,
    ///               fuelConsumption, electricConsumption, ambientTemp,
    ///               airRatio, fuel, smokeTemperature)
    ///   → computes NominalCapacity and heat loss coefficient from rated conditions
    ///
    /// Update(inletWaterTemp, waterFlowRate):
    ///   Controls outlet temperature to OutletWaterSetPointTemperature.
    ///   IsOverLoad = true when nominal fuel consumption is insufficient.
    ///
    /// ShutOff():
    ///   Sets flow rates and fuel consumption to zero.
    /// </remarks>
    public class HotWaterBoilerTests
    {
        #region ヘルパー

        /// <summary>
        /// 標準的な温水ボイラを生成する。
        /// 定格: 入口60°C→出口80°C, 流量1kg/s, 都市ガス13A, 排ガス200°C。
        /// </summary>
        private static HotWaterBoiler MakeBoiler()
        {
            // 定格燃料消費量を計算
            double nomCap = (80.0 - 60.0) * 1.0 * 4.182; // ≈ 83.6 kW
            double nomFuel = Boiler.GetFuelConsumption(
                nomCap, 80.0, 15.0, Boiler.Fuel.Gas13A, 200, 1.1, 0, 15.0, 80.0);

            return new HotWaterBoiler(
                inletWaterTemperature:  60.0,
                outletWaterTemperature: 80.0,
                waterFlowRate:          1.0,
                fuelConsumption:        nomFuel,
                electricConsumption:    0.5,
                ambientTemperature:     15.0,
                airRatio:               1.1,
                fuel:                   Boiler.Fuel.Gas13A,
                smokeTemperature:       200.0);
        }

        #endregion

        // ================================================================
        #region コンストラクタ・プロパティ

        /// <summary>NominalCapacity が正の値になる。</summary>
        [Fact]
        public void Constructor_NominalCapacity_IsPositive()
        {
            var boiler = MakeBoiler();
            Assert.True(boiler.NominalCapacity > 0,
                $"NominalCapacity={boiler.NominalCapacity:F2} kW > 0");
        }

        /// <summary>MaxWaterFlowRate がコンストラクタ指定値と一致する。</summary>
        [Fact]
        public void Constructor_MaxWaterFlowRate_MatchesInput()
        {
            var boiler = MakeBoiler();
            Assert.InRange(boiler.MaxWaterFlowRate, 0.99, 1.01);
        }

        /// <summary>コンストラクタ直後は ShutOff 状態。</summary>
        [Fact]
        public void Constructor_InitialState_IsShutOff()
        {
            var boiler = MakeBoiler();
            Assert.Equal(0.0, boiler.HeatLoad);
            Assert.Equal(0.0, boiler.FuelConsumption);
            Assert.Equal(0.0, boiler.WaterFlowRate);
        }

        /// <summary>Fuel プロパティがコンストラクタ指定値と一致する。</summary>
        [Fact]
        public void Constructor_Fuel_MatchesInput()
        {
            var boiler = MakeBoiler();
            Assert.Equal(Boiler.Fuel.Gas13A, boiler.Fuel);
        }

        #endregion

        // ================================================================
        #region Update — 通常運転

        /// <summary>
        /// 設定値より低い入口温度で Update すると出口温度が設定値になる（非過負荷時）。
        /// </summary>
        [Fact]
        public void Update_Normal_OutletReachesSetpoint()
        {
            var boiler = MakeBoiler();
            boiler.OutletWaterSetPointTemperature = 80.0;
            boiler.Update(60.0, 1.0);
            if (!boiler.IsOverLoad)
                Assert.InRange(boiler.OutletWaterTemperature, 79.5, 80.5);
        }

        /// <summary>HeatLoad が正（加熱）。</summary>
        [Fact]
        public void Update_Normal_HeatLoadIsPositive()
        {
            var boiler = MakeBoiler();
            boiler.Update(60.0, 1.0);
            Assert.True(boiler.HeatLoad > 0, $"HeatLoad={boiler.HeatLoad:F2} kW > 0");
        }

        /// <summary>FuelConsumption が正。</summary>
        [Fact]
        public void Update_Normal_FuelConsumptionIsPositive()
        {
            var boiler = MakeBoiler();
            boiler.Update(60.0, 1.0);
            Assert.True(boiler.FuelConsumption > 0,
                $"FuelConsumption={boiler.FuelConsumption:F6} > 0");
        }

        /// <summary>出口設定温度 &lt; 入口温度のとき ShutOff（加熱不要）。</summary>
        [Fact]
        public void Update_SetpointBelowInlet_ShutOff()
        {
            var boiler = MakeBoiler();
            boiler.OutletWaterSetPointTemperature = 50.0;
            boiler.Update(60.0, 1.0); // 入口60°C > 設定50°C → 停止
            Assert.Equal(0.0, boiler.HeatLoad);
        }

        /// <summary>流量ゼロのとき ShutOff。</summary>
        [Fact]
        public void Update_ZeroFlowRate_ShutOff()
        {
            var boiler = MakeBoiler();
            boiler.Update(60.0, 0.0);
            Assert.Equal(0.0, boiler.HeatLoad);
        }

        /// <summary>
        /// 設定温度が高いほど燃料消費量が多い（同じ入口温度・流量）。
        /// </summary>
        [Fact]
        public void Update_HigherSetpoint_MoreFuelConsumption()
        {
            var boiler = MakeBoiler();
            boiler.OutletWaterSetPointTemperature = 70.0;
            boiler.Update(60.0, 1.0);
            double fc70 = boiler.FuelConsumption;

            boiler.OutletWaterSetPointTemperature = 80.0;
            boiler.Update(60.0, 1.0);
            double fc80 = boiler.FuelConsumption;

            Assert.True(fc80 > fc70,
                $"Setpoint80 fc={fc80:F6} > Setpoint70 fc={fc70:F6}");
        }

        #endregion

        // ================================================================
        #region 過負荷

        /// <summary>要求熱量が定格能力を超えると IsOverLoad = true。</summary>
        [Fact]
        public void Update_ExcessiveDemand_IsOverLoad()
        {
            var boiler = MakeBoiler();
            // 設定温度を非常に高くして過負荷を引き起こす
            boiler.OutletWaterSetPointTemperature = 200.0;
            boiler.Update(60.0, 1.0);
            Assert.True(boiler.IsOverLoad);
        }

        #endregion

        // ================================================================
        #region ShutOff

        /// <summary>ShutOff 後は HeatLoad・FuelConsumption・WaterFlowRate がゼロ。</summary>
        [Fact]
        public void ShutOff_ResetsOutputs()
        {
            var boiler = MakeBoiler();
            boiler.Update(60.0, 1.0);
            Assert.True(boiler.HeatLoad > 0);

            boiler.ShutOff();
            Assert.Equal(0.0, boiler.HeatLoad);
            Assert.Equal(0.0, boiler.FuelConsumption);
            Assert.Equal(0.0, boiler.WaterFlowRate);
        }

        #endregion
    }
}
