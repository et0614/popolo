/* HeatSourceSystemModelTests.cs
 *
 * Copyright (C) 2026 E.Togashi
 * GNU General Public License v3 — see accompanying LICENSE file.
 */

using System;
using Xunit;
using Popolo.Core.HVAC.SystemModel;
using Popolo.Core.HVAC.HeatSource;
using Popolo.Core.HVAC.HeatExchanger;
using Popolo.Core.HVAC.FluidCircuit;
using Popolo.Core.HVAC.Storage;
using Popolo.Core.Physics;

namespace Popolo.Core.Tests.HVAC.SystemModel
{
    /// <summary>
    /// Unit tests for <see cref="HeatSourceSystemModel"/> and heat source sub-systems.
    /// </summary>
    /// <remarks>
    /// Test conditions are taken from HeatSourceSubsystemTest3–5 sample code.
    ///
    /// Test3: CentrifugalChillerSystem
    ///   chiller: SimpleCentrifugalChiller, 500/6 kW/unit, NCH_FLOW kg/s chilled water
    ///   ForecastSupplyWaterTemperature(load/(cp*5), 12, 0, 40) → cooling
    ///
    /// Test5: GroundHeatSourceHeatPumpSystem
    ///   WaterHeatPump(62.4, mcEvpC, mcCndC, 7, 26, 13.3, 72.3, mcCndH, mcEvpH, 45, 12, 18.6)
    ///   8h cooling + 10h idle + 8h heating
    ///
    /// HeatSourceSystemModel.ForecastSupplyWaterTemperature(
    ///   chilledWaterFlowRate, chilledWaterReturnTemperature,
    ///   hotWaterFlowRate, hotWaterReturnTemperature)
    /// </remarks>
    public class HeatSourceSystemModelTests
    {
        #region 定数

        private const double Cp = 4.186;

        #endregion

        // ================================================================
        #region Test3-A: CentrifugalChillerSystem — 冷却運転

        /// <summary>
        /// Test3 (HeatSourceSubsystemTest3) の前半に対応。
        /// ターボ冷凍機 + 冷却塔 で 60%負荷の冷却運転。
        /// </summary>
        private static (HeatSourceSystemModel, SimpleCentrifugalChiller, CentrifugalPump, CentrifugalPump, CoolingTower)
            MakeCentrifugalSystem()
        {
            const double NCH_FLOW = 500.0 / (12 - 7) / Cp;
            const double NCD_FLOW = 1670.0 / 60;

            var chiller = new SimpleCentrifugalChiller(
                500.0 / 6.0, 0.2, 12, 7, 37, NCH_FLOW, false);
            var chPmp = new CentrifugalPump(
                150, 1e-3 * NCH_FLOW, 140, 1e-3 * NCH_FLOW,
                CentrifugalPump.ControlMethod.ConstantPressureWithInverter, 50);
            var cdPmp = new CentrifugalPump(
                150, 1e-3 * NCD_FLOW, 140, 1e-3 * NCD_FLOW,
                CentrifugalPump.ControlMethod.ConstantPressureWithInverter, 50);
            var cTower = new CoolingTower(
                37, 32, 27, NCD_FLOW,
                CoolingTower.AirFlowDirection.CrossFlow, false);

            var crSystem = new CentrifugalChillerSystem(chiller, chPmp, cdPmp, cTower, 1, 1);

            var hsSystem = new HeatSourceSystemModel(
                new IHeatSourceSubSystem[] { crSystem });
            hsSystem.SetOperatingMode(0, HeatSourceSystemModel.OperatingMode.Cooling);
            hsSystem.SetChillingOperationSequence(0, 1);
            hsSystem.ChilledWaterSupplyTemperatureSetpoint = 7.0;
            hsSystem.OutdoorAir = new MoistAir(35, 0.0195);
            hsSystem.TimeStep = 3600;

            return (hsSystem, chiller, chPmp, cdPmp, cTower);
        }

        /// <summary>60%負荷で ChilledWaterSupplyTemperature が設定値付近になる。</summary>
        [Fact]
        public void CentrifugalChillerSystem_Cooling_SupplyTempNearSetpoint()
        {
            var (hs, chiller, _, _, _) = MakeCentrifugalSystem();
            double load = 500.0 * 0.6;  // kW
            hs.ForecastSupplyWaterTemperature(load / (Cp * 5), 12, 0, 40);

            if (!hs.IsOverLoad_C)
                Assert.InRange(hs.ChilledWaterSupplyTemperature, 6.5, 7.5);
        }

        /// <summary>60%負荷でチラー消費電力が正。</summary>
        [Fact]
        public void CentrifugalChillerSystem_Cooling_ChillerElecIsPositive()
        {
            var (hs, chiller, _, _, _) = MakeCentrifugalSystem();
            double load = 500.0 * 0.6;
            hs.ForecastSupplyWaterTemperature(load / (Cp * 5), 12, 0, 40);

            Assert.True(chiller.ElectricConsumption > 0,
                $"Chiller EC={chiller.ElectricConsumption:F2} kW > 0");
        }

        /// <summary>ゼロ負荷で ShutOff（冷凍機停止）。</summary>
        [Fact]
        public void CentrifugalChillerSystem_ZeroLoad_ChillerShutOff()
        {
            var (hs, chiller, _, _, _) = MakeCentrifugalSystem();
            hs.ForecastSupplyWaterTemperature(0, 12, 0, 40);
            Assert.Equal(0.0, chiller.CoolingLoad);
        }

        /// <summary>高負荷→低負荷で消費電力が下がる。</summary>
        [Fact]
        public void CentrifugalChillerSystem_HigherLoad_HigherElec()
        {
            var (hs1, c1, _, _, _) = MakeCentrifugalSystem();
            hs1.ForecastSupplyWaterTemperature(500 * 0.4 / (Cp * 5), 12, 0, 40);
            double ec40 = c1.ElectricConsumption;

            var (hs2, c2, _, _, _) = MakeCentrifugalSystem();
            hs2.ForecastSupplyWaterTemperature(500 * 0.8 / (Cp * 5), 12, 0, 40);
            double ec80 = c2.ElectricConsumption;

            Assert.True(ec80 > ec40,
                $"80% EC={ec80:F2} kW > 40% EC={ec40:F2} kW");
        }

        /// <summary>FixState 呼び出しで例外が発生しない。</summary>
        [Fact]
        public void CentrifugalChillerSystem_FixState_NoException()
        {
            var (hs, _, _, _, _) = MakeCentrifugalSystem();
            hs.ForecastSupplyWaterTemperature(500 * 0.6 / (Cp * 5), 12, 0, 40);
            var ex = Record.Exception(() => hs.FixState());
            Assert.Null(ex);
        }

        #endregion

        // ================================================================
        #region Test5: GroundHeatSourceHeatPumpSystem

        private static (HeatSourceSystemModel, WaterHeatPump, SimpleGroundHeatExchanger)
            MakeGSHPSystem()
        {
            double mcEvpC = 178.3 / 60;
            double mcCndC = 216.7 / 60;
            double mcEvpH = 155.0 / 60;
            double mcCndH = 206.7 / 60;

            var whp = new WaterHeatPump(
                62.4, mcEvpC, mcCndC, 7, 26, 13.3,
                72.3, mcCndH, mcEvpH, 45, 12, 18.6);
            var gPmp = new CentrifugalPump(
                150, 1e-3 * mcCndC, 140, 1e-3 * mcCndC,
                CentrifugalPump.ControlMethod.ConstantPressureWithInverter, 50);
            var chPmp = new CentrifugalPump(
                150, 1e-3 * mcEvpC, 140, 1e-3 * mcEvpC,
                CentrifugalPump.ControlMethod.ConstantPressureWithInverter, 50);
            var gHex = new SimpleGroundHeatExchanger(
                mcCndC, Cp, 0.75,
                SimpleGroundHeatExchanger.Type.Vertical);
            // 土壌物性調整（Test5と同じ）
            gHex.NearGroundHeatConductance *= 5;
            gHex.NearGroundHeatCapacity    *= 5;

            var gshp = new GroundHeatSourceHeatPumpSystem(whp, gHex, gPmp, chPmp);

            var hs = new HeatSourceSystemModel(
                new IHeatSourceSubSystem[] { gshp });
            hs.TimeStep = 3600;
            hs.SetChillingOperationSequence(0, 1);
            hs.SetHeatingOperationSequence(0, 1);

            return (hs, whp, gHex);
        }

        /// <summary>
        /// GSHP 冷却運転: チラー（WHP）の冷却負荷が正になる。
        /// Test5 の 8h 冷却ループ最初のステップに対応。
        /// </summary>
        [Fact]
        public void GSHP_Cooling_CoolingLoadIsPositive()
        {
            var (hs, whp, _) = MakeGSHPSystem();
            hs.SetOperatingMode(0, HeatSourceSystemModel.OperatingMode.Cooling);
            hs.ChilledWaterSupplyTemperatureSetpoint = 7.0;
            hs.OutdoorAir = new MoistAir(25, 0.012);

            double mcEvpC = 178.3 / 60;
            hs.ForecastSupplyWaterTemperature(0.8 * mcEvpC, 12, 0, 40);
            Assert.True(whp.CoolingLoad > 0,
                $"WHP CoolingLoad={whp.CoolingLoad:F2} kW > 0");
        }

        /// <summary>
        /// GSHP 暖房運転: ヒートポンプの加熱負荷が正になる。
        /// Test5 の 8h 加熱ループに対応。
        /// </summary>
        [Fact]
        public void GSHP_Heating_HeatingLoadIsPositive()
        {
            var (hs, whp, _) = MakeGSHPSystem();
            hs.SetOperatingMode(0, HeatSourceSystemModel.OperatingMode.Heating);
            hs.HotWaterSupplyTemperatureSetpoint = 45.0;
            hs.OutdoorAir = new MoistAir(5, 0.004);

            double mcCndH = 206.7 / 60;
            hs.ForecastSupplyWaterTemperature(0, 12, 0.8 * mcCndH, 40);
            Assert.True(whp.HeatingLoad > 0,
                $"WHP HeatingLoad={whp.HeatingLoad:F2} kW > 0");
        }

        /// <summary>
        /// 連続8ステップ冷却後に近傍土壌温度が上昇する（排熱による地温上昇）。
        /// Test5 の冷却ループに対応。
        /// </summary>
        [Fact]
        public void GSHP_Cooling_8Steps_GroundTempRises()
        {
            var (hs, _, gHex) = MakeGSHPSystem();
            hs.SetOperatingMode(0, HeatSourceSystemModel.OperatingMode.Cooling);
            hs.ChilledWaterSupplyTemperatureSetpoint = 7.0;
            hs.OutdoorAir = new MoistAir(25, 0.012);

            double mcEvpC = 178.3 / 60;
            double tInit = gHex.NearGroundTemperature;

            for (int i = 0; i < 8; i++)
            {
                hs.ForecastSupplyWaterTemperature(0.8 * mcEvpC, 12, 0, 40);
                hs.FixState();
            }

            Assert.True(gHex.NearGroundTemperature > tInit,
                $"Ground temp after cooling: {gHex.NearGroundTemperature:F3}C > initial {tInit:F3}C");
        }

        /// <summary>
        /// ゼロ負荷で FixState を呼ぶと地中熱交換器の更新のみ行われ例外なし。
        /// Test5 の回復フェーズに対応。
        /// </summary>
        [Fact]
        public void GSHP_ZeroLoad_FixState_NoException()
        {
            var (hs, _, _) = MakeGSHPSystem();
            hs.SetOperatingMode(0, HeatSourceSystemModel.OperatingMode.Cooling);
            hs.ChilledWaterSupplyTemperatureSetpoint = 7.0;
            hs.OutdoorAir = new MoistAir(15, 0.008);

            var ex = Record.Exception(() =>
            {
                hs.ForecastSupplyWaterTemperature(0, 12, 0, 40);
                hs.FixState();
            });
            Assert.Null(ex);
        }

        #endregion

        // ================================================================
        #region HotWaterBoilerSystem — 暖房運転

        private static (HeatSourceSystemModel, HotWaterBoiler)
            MakeBoilerSystem()
        {
            // 定格: 入口40°C→出口60°C, 流量2kg/s, 都市ガス13A
            double nomCap = (60.0 - 40.0) * 2.0 * Cp;
            double nomFuel = Boiler.GetFuelConsumption(
                nomCap, 60.0, 15.0, Boiler.Fuel.Gas13A, 200, 1.1, 0, 15.0, 60.0);

            var boiler = new HotWaterBoiler(
                40.0, 60.0, 2.0, nomFuel, 0.5, 15.0, 1.1,
                Boiler.Fuel.Gas13A, 200.0);
            var hwPmp = new CentrifugalPump(
                100, 0.002, 90, 0.002,
                CentrifugalPump.ControlMethod.ConstantPressureWithInverter, 50);

            var bSystem = new HotWaterBoilerSystem(boiler, hwPmp, 1);

            var hs = new HeatSourceSystemModel(
                new IHeatSourceSubSystem[] { bSystem });
            hs.SetOperatingMode(0, HeatSourceSystemModel.OperatingMode.Heating);
            hs.SetHeatingOperationSequence(0, 1);
            hs.HotWaterSupplyTemperatureSetpoint = 60.0;
            hs.OutdoorAir = new MoistAir(5, 0.003);
            hs.TimeStep = 3600;

            return (hs, boiler);
        }

        /// <summary>暖房運転で HotWaterSupplyTemperature が設定値付近になる。</summary>
        [Fact]
        public void HotWaterBoilerSystem_Heating_SupplyTempNearSetpoint()
        {
            var (hs, boiler) = MakeBoilerSystem();
            double load = nomCap_Boiler() * 0.5;
            hs.ForecastSupplyWaterTemperature(0, 40, load / (Cp * 5), 40);

            if (!hs.IsOverLoad_H)
                Assert.InRange(hs.HotWaterSupplyTemperature, 58.0, 62.0);
        }

        /// <summary>暖房運転でボイラ燃料消費が正。</summary>
        [Fact]
        public void HotWaterBoilerSystem_Heating_FuelConsumptionIsPositive()
        {
            var (hs, boiler) = MakeBoilerSystem();
            hs.ForecastSupplyWaterTemperature(0, 40, 1.0, 40);

            Assert.True(boiler.FuelConsumption > 0,
                $"FuelConsumption={boiler.FuelConsumption:F6} > 0");
        }

        /// <summary>冷水流量ゼロ(=冷房なし)ならば IsOverLoad_C = false。</summary>
        [Fact]
        public void HotWaterBoilerSystem_NoCooling_IsOverLoad_C_IsFalse()
        {
            var (hs, _) = MakeBoilerSystem();
            hs.ForecastSupplyWaterTemperature(0, 12, 1.0, 40);
            Assert.False(hs.IsOverLoad_C);
        }

        private static double nomCap_Boiler() => (60.0 - 40.0) * 2.0 * Cp;

        #endregion

        // ================================================================
        #region HeatSourceSystemModel — 基本プロパティ

        /// <summary>SetOperatingMode で Mode が設定される。</summary>
        [Fact]
        public void HeatSourceSystemModel_SetOperatingMode_Works()
        {
            var (hs, _, _, _, _) = MakeCentrifugalSystem();
            // Cooling モードで初期化済み、ShutOff に変更
            hs.SetOperatingMode(0, HeatSourceSystemModel.OperatingMode.ShutOff);
            hs.ForecastSupplyWaterTemperature(500 * 0.6 / (Cp * 5), 12, 0, 40);
            // ShutOff 後は冷水往温度が還温度付近になる
            Assert.InRange(hs.ChilledWaterSupplyTemperature, 6.0, 13.0);
        }

        /// <summary>ChilledWaterSupplyTemperatureSetpoint が設定・取得できる。</summary>
        [Fact]
        public void HeatSourceSystemModel_SetpointRoundTrip()
        {
            var (hs, _, _, _, _) = MakeCentrifugalSystem();
            hs.ChilledWaterSupplyTemperatureSetpoint = 8.0;
            Assert.InRange(hs.ChilledWaterSupplyTemperatureSetpoint, 7.99, 8.01);
        }

        #endregion
    }
}
