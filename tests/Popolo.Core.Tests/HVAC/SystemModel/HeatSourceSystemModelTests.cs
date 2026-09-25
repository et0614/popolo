/* HeatSourceSystemModelTests.cs
 *
 * Copyright (C) 2026 E.Togashi
 * Licensed under the Apache License, Version 2.0 - see the accompanying LICENSE file.
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
        #region Constants

        private const double Cp = 4.186;

        #endregion

        // ================================================================
        #region Test3-A: CentrifugalChillerSystem — cooling operation

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
        /// GSHP の地中熱交換器ループの熱収支が閉じる（地中熱交換器出口温度 = ヒートポンプの冷却水入口温度）。
        /// 探索区間の下限に予測後の地温を使っていた旧実装では、区間が根を挟まず約 8 K の不整合が残っていた。
        /// </summary>
        [Fact]
        public void GSHP_Cooling_GroundLoopIsClosed()
        {
            var (hs, whp, gHex) = MakeGSHPSystem();
            hs.SetOperatingMode(0, HeatSourceSystemModel.OperatingMode.Cooling);
            hs.ChilledWaterSupplyTemperatureSetpoint = 7.0;
            hs.OutdoorAir = new MoistAir(25, 0.012);

            double mcEvpC = 178.3 / 60;
            hs.ForecastSupplyWaterTemperature(0.8 * mcEvpC, 12, 0, 40);
            Assert.True(Math.Abs(gHex.FluidOutletTemperature - whp.CoolingWaterInletTemperature) < 0.05,
                $"Ground outlet {gHex.FluidOutletTemperature:F3} °C vs HP cooling water inlet {whp.CoolingWaterInletTemperature:F3} °C");
        }

        /// <summary>GSHP 暖房運転でも地中熱交換器ループの熱収支が閉じる。</summary>
        [Fact]
        public void GSHP_Heating_GroundLoopIsClosed()
        {
            var (hs, whp, gHex) = MakeGSHPSystem();
            hs.SetOperatingMode(0, HeatSourceSystemModel.OperatingMode.Heating);
            hs.HotWaterSupplyTemperatureSetpoint = 45.0;
            hs.OutdoorAir = new MoistAir(5, 0.004);

            double mcCndH = 206.7 / 60;
            hs.ForecastSupplyWaterTemperature(0, 12, 0.8 * mcCndH, 40);
            Assert.True(Math.Abs(gHex.FluidOutletTemperature - whp.HeatSourceWaterInletTemperature) < 0.05,
                $"Ground outlet {gHex.FluidOutletTemperature:F3} °C vs HP source water inlet {whp.HeatSourceWaterInletTemperature:F3} °C");
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

        /// <summary>
        /// 冷却運転で地温を上げた後、停止条件（ゼロ負荷または停止モード）で予測を n 回行ってから
        /// 確定したときの近傍・遠方土壌温度を返す。
        /// </summary>
        private static (double near, double distant) GSHP_SoilAfterIdleForecasts(
            int forecastCount, HeatSourceSystemModel.OperatingMode idleMode)
        {
            var (hs, _, gHex) = MakeGSHPSystem();
            hs.SetOperatingMode(0, HeatSourceSystemModel.OperatingMode.Cooling);
            hs.ChilledWaterSupplyTemperatureSetpoint = 7.0;
            hs.OutdoorAir = new MoistAir(25, 0.012);
            double mcEvpC = 178.3 / 60;
            for (int i = 0; i < 3; i++)
            {
                hs.ForecastSupplyWaterTemperature(0.8 * mcEvpC, 12, 0, 40);
                hs.FixState();
            }

            hs.SetOperatingMode(0, idleMode);
            for (int i = 0; i < forecastCount; i++)
                hs.ForecastSupplyWaterTemperature(0, 12, 0, 40);
            hs.FixState();
            return (gHex.NearGroundTemperature, gHex.DistantGroundTemperature);
        }

        /// <summary>
        /// 停止中の予測（ForecastSupplyWaterTemperature）を何回呼んでも、確定（FixState）後の
        /// 地温は 1 回呼んだ場合と同じ（予測は状態を確定しない）。
        /// 旧実装は予測のたびに gHex.Update で地温を 1 ステップ進めて確定していた。
        /// </summary>
        [Theory]
        [InlineData(HeatSourceSystemModel.OperatingMode.Cooling)]
        [InlineData(HeatSourceSystemModel.OperatingMode.Heating)]
        [InlineData(HeatSourceSystemModel.OperatingMode.ShutOff)]
        public void GSHP_IdleForecast_DoesNotCommitGroundState(HeatSourceSystemModel.OperatingMode idleMode)
        {
            var once = GSHP_SoilAfterIdleForecasts(1, idleMode);
            var many = GSHP_SoilAfterIdleForecasts(5, idleMode);
            Assert.Equal(once.near, many.near, 12);
            Assert.Equal(once.distant, many.distant, 12);
        }

        /// <summary>停止中も確定のたびに地温は 1 ステップずつ回復（進行）する。</summary>
        [Fact]
        public void GSHP_IdleSteps_GroundRecoversEachStep()
        {
            var (hs, _, gHex) = MakeGSHPSystem();
            hs.SetOperatingMode(0, HeatSourceSystemModel.OperatingMode.Cooling);
            hs.ChilledWaterSupplyTemperatureSetpoint = 7.0;
            hs.OutdoorAir = new MoistAir(25, 0.012);
            double mcEvpC = 178.3 / 60;
            for (int i = 0; i < 3; i++)
            {
                hs.ForecastSupplyWaterTemperature(0.8 * mcEvpC, 12, 0, 40);
                hs.FixState();
            }
            double prev = gHex.NearGroundTemperature;
            for (int i = 0; i < 3; i++)
            {
                hs.ForecastSupplyWaterTemperature(0, 12, 0, 40);
                hs.FixState();
                Assert.True(gHex.NearGroundTemperature < prev,
                    $"step {i}: {gHex.NearGroundTemperature:F4} < {prev:F4}");
                prev = gHex.NearGroundTemperature;
            }
        }

        #endregion

        // ================================================================
        #region HotWaterBoilerSystem — heating operation

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

        /// <summary>
        /// 停止（ゼロ負荷）を経ても次の暖房要求で再びボイラが運転される。
        /// ShutOff がボイラ台数（BoilerCount）を 0 にしていた旧実装では永久に停止していた。
        /// </summary>
        [Fact]
        public void HotWaterBoilerSystem_AfterShutOff_HeatsAgain()
        {
            var (hs, boiler) = MakeBoilerSystem();
            hs.ForecastSupplyWaterTemperature(0, 12, 0, 40);   // 暖房負荷なし → サブシステム停止
            hs.ForecastSupplyWaterTemperature(0, 12, 1.0, 40);
            Assert.True(boiler.FuelConsumption > 0,
                $"FuelConsumption={boiler.FuelConsumption:F6} > 0");
            Assert.InRange(hs.HotWaterSupplyTemperature, 58.0, 62.0);
        }

        /// <summary>
        /// サブシステムを直接 ShutOff した後も、台数と最大流量が保持され、次の予測で運転できる。
        /// </summary>
        [Fact]
        public void HotWaterBoilerSystem_ShutOff_KeepsUnitCount()
        {
            double nomFuel = Boiler.GetFuelConsumption(
                nomCap_Boiler(), 60.0, 15.0, Boiler.Fuel.Gas13A, 200, 1.1, 0, 15.0, 60.0);
            var boiler = new HotWaterBoiler(
                40.0, 60.0, 2.0, nomFuel, 0.5, 15.0, 1.1, Boiler.Fuel.Gas13A, 200.0);
            var hwPmp = new CentrifugalPump(
                100, 0.002, 90, 0.002,
                CentrifugalPump.ControlMethod.ConstantPressureWithInverter, 50);
            var bSystem = new HotWaterBoilerSystem(boiler, hwPmp, 2);
            bSystem.Mode = HeatSourceSystemModel.OperatingMode.Heating;
            bSystem.HotWaterSupplyTemperatureSetpoint = 60.0;

            bSystem.ShutOff();
            Assert.Equal(2, bSystem.BoilerCount);
            Assert.Equal(0, bSystem.ActiveUnitCount);
            Assert.Equal(4.0, bSystem.MaxHotWaterFlowRate, 10);

            bSystem.ForecastSupplyWaterTemperature(0, 1.0);
            Assert.Equal(1, bSystem.ActiveUnitCount);
            Assert.True(boiler.FuelConsumption > 0);
        }

        /// <summary>
        /// 過負荷時、系全体の往温度は各サブシステムの温水流量で重み付け平均される。
        /// ボイラ系が HotWaterFlowRate を設定しなかった旧実装では 0/0 で NaN になっていた。
        /// </summary>
        [Fact]
        public void HotWaterBoilerSystem_Overload_SupplyTemperatureIsFinite()
        {
            var (hs, boiler) = MakeBoilerSystem();
            // 還温度20 °C・1.5 kg/s → 60 °C まで約 251 kW（定格約 167 kW を超過）
            hs.ForecastSupplyWaterTemperature(0, 12, 1.5, 20);
            Assert.True(hs.IsOverLoad_H);
            Assert.True(double.IsFinite(hs.HotWaterSupplyTemperature),
                $"HotWaterSupplyTemperature={hs.HotWaterSupplyTemperature}");
            Assert.InRange(hs.HotWaterSupplyTemperature, 15.0, 60.0);
            Assert.Equal(boiler.OutletWaterTemperature, hs.HotWaterSupplyTemperature, 6);
        }

        private static double nomCap_Boiler() => (60.0 - 40.0) * 2.0 * Cp;

        #endregion

        // ================================================================
        #region SimpleModularAirSourceHeatPumpSystem — cooling-only unit

        /// <summary>
        /// 冷房専用のモジュール型空冷チラーでも、熱源システムの冷房運転で冷水が製造され、
        /// 流量比が有限値となる。旧実装では最大冷水流量が 0 のため運転されず、流量比は NaN だった。
        /// </summary>
        [Fact]
        public void ModularCoolingOnly_Cooling_ProducesChilledWater()
        {
            double mwPerUnit = 430.0 / 60.0;
            var chiller = new SimpleModularAirSourceHeatPump(
                150, 7, mwPerUnit, 35, 850.0 / 60 * 1.2, 49.8, 3, 1.9);
            var chPmp = new CentrifugalPump(150, 3e-3 * mwPerUnit, 140, 3e-3 * mwPerUnit,
                CentrifugalPump.ControlMethod.ConstantPressureWithInverter, 50);
            var hwPmp = new CentrifugalPump(150, 3e-3 * mwPerUnit, 140, 3e-3 * mwPerUnit,
                CentrifugalPump.ControlMethod.ConstantPressureWithInverter, 50);
            var mSystem = new SimpleModularAirSourceHeatPumpSystem(chiller, chPmp, hwPmp, 1);
            Assert.True(double.IsFinite(mSystem.MinChilledWaterFlowRatio));
            Assert.True(double.IsFinite(mSystem.MinHotWaterFlowRatio));

            var hs = new HeatSourceSystemModel(new IHeatSourceSubSystem[] { mSystem });
            hs.SetOperatingMode(0, HeatSourceSystemModel.OperatingMode.Cooling);
            hs.SetChillingOperationSequence(0, 1);
            hs.ChilledWaterSupplyTemperatureSetpoint = 7.0;
            hs.OutdoorAir = new MoistAir(35, 0.0195);

            double flow = 0.6 * 3 * mwPerUnit;
            hs.ForecastSupplyWaterTemperature(flow, 12, 0, 40);
            Assert.True(chiller.CoolingLoad > 0, $"CoolingLoad={chiller.CoolingLoad:F2} kW > 0");
            Assert.True(double.IsFinite(hs.ChilledWaterSupplyTemperature));
            Assert.InRange(hs.ChilledWaterSupplyTemperature, 6.5, 7.5);
        }

        #endregion

        // ================================================================
        #region MultipleStratifiedWaterTankSystem — thermal storage

        private const double TankChwFlow = 500.0 / (12 - 7) / Cp;   // 冷凍機定格冷水流量 [kg/s]
        private const double TankCdwFlow = 1670.0 / 60;              // 冷却水定格流量 [kg/s]

        /// <summary>
        /// 温度成層型蓄熱槽システム（ターボ冷凍機1台＋冷却塔1基＋放熱用プレート熱交換器）。
        /// </summary>
        private static (MultipleStratifiedWaterTankSystem, SimpleCentrifugalChiller) MakeTankSystem()
        {
            var chiller = new SimpleCentrifugalChiller(500.0 / 6.0, 0.2, 12, 7, 37, TankChwFlow, false);
            var chwPmp = new CentrifugalPump(150, 0.03, 140, 0.03,
                CentrifugalPump.ControlMethod.ConstantPressureWithInverter, 50);
            var cdwPmp = new CentrifugalPump(150, 1e-3 * TankCdwFlow, 140, 1e-3 * TankCdwFlow,
                CentrifugalPump.ControlMethod.ConstantPressureWithInverter, 50);
            var chgPmp = new CentrifugalPump(150, 1e-3 * TankChwFlow, 140, 1e-3 * TankChwFlow,
                CentrifugalPump.ControlMethod.ConstantPressureWithInverter, 50);
            var disPmp = new CentrifugalPump(150, 0.03, 140, 0.03,
                CentrifugalPump.ControlMethod.ConstantPressureWithInverter, 50);
            var cTower = new CoolingTower(37, 32, 27, TankCdwFlow,
                CoolingTower.AirFlowDirection.CrossFlow, false);
            var pHex = new PlateHeatExchanger(200.0, 30.0, 30.0);
            var tank = new MultipleStratifiedWaterTank(4.0, 25.0, 0.2, 3.8, 20);
            tank.InitializeTemperature(6.0);

            var sys = new MultipleStratifiedWaterTankSystem(
                tank, pHex, chiller, chwPmp, cdwPmp, chgPmp, disPmp, cTower, 1, 1);
            sys.Mode = HeatSourceSystemModel.OperatingMode.Cooling;
            sys.TimeStep = 3600;
            sys.StorageTemperature = 5.0;
            sys.ChilledWaterSupplyTemperatureSetpoint = 7.0;
            sys.ChilledWaterReturnTemperature = 12.0;
            sys.OutdoorAir = new MoistAir(30, 0.015);
            return (sys, chiller);
        }

        /// <summary>
        /// 冷却水流量設定値が既定値（0＝未設定）のとき、冷却水は設計流量で流れ、蓄熱運転で冷凍機が
        /// 冷却能力を発揮する。旧実装は設定値 0 をそのまま使い、冷却水流量 0 で冷凍機が停止していた。
        /// </summary>
        [Fact]
        public void TankSystem_DefaultCoolingWaterSetpoint_UsesDesignFlow()
        {
            var (sys, chiller) = MakeTankSystem();
            sys.Charging = true;
            sys.ForecastSupplyWaterTemperature(0, 0);
            Assert.True(chiller.CoolingLoad > 0, $"CoolingLoad={chiller.CoolingLoad:F2} kW > 0");
            Assert.Equal(TankCdwFlow, chiller.CoolingWaterFlowRate, 6);
            Assert.Equal(TankCdwFlow, sys.CoolingTower.WaterFlowRate, 6);
        }

        /// <summary>冷却水流量設定値を与えた場合、冷凍機と冷却塔の冷却水流量がともに設定値に従う。</summary>
        [Fact]
        public void TankSystem_CoolingWaterSetpoint_AppliedToChillerAndTower()
        {
            var (sys, chiller) = MakeTankSystem();
            sys.Charging = true;
            sys.CoolingWaterFlowSetpoint = 0.8 * TankCdwFlow;
            sys.ForecastSupplyWaterTemperature(0, 0);
            Assert.Equal(0.8 * TankCdwFlow, chiller.CoolingWaterFlowRate, 6);
            Assert.Equal(0.8 * TankCdwFlow, sys.CoolingTower.WaterFlowRate, 6);
        }

        /// <summary>
        /// 追いかけ運転（蓄熱運転中の二次側負荷）で、放熱ポンプには熱交換器の熱源側流量が
        /// 体積流量 [m³/s] で与えられる。旧実装は質量流量 [kg/s] をそのまま渡していた（1000倍）。
        /// </summary>
        [Fact]
        public void TankSystem_ChasingOperation_DischargePumpFlowInCubicMetres()
        {
            var (sys, _) = MakeTankSystem();
            sys.Charging = true;
            sys.CoolingWaterFlowSetpoint = TankCdwFlow;
            sys.ForecastSupplyWaterTemperature(20.0, 0);
            double hexFlow = sys.PlateHeatExchanger.HeatSourceFlowRate;
            Assert.True(0 < hexFlow, $"HEX heat source flow={hexFlow} kg/s > 0");
            Assert.Equal(0.001 * hexFlow, sys.DischargePump.VolumetricFlowRate, 9);
        }

        #endregion

        // ================================================================
        #region HeatSourceSystemModel — basic properties

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
