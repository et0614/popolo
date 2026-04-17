/* VRFSystemTests.cs
 *
 * Copyright (C) 2026 E.Togashi
 * GNU General Public License v3 — see accompanying LICENSE file.
 */

using System;
using Xunit;
using Popolo.Core.HVAC.VRF;
using Popolo.Core.Physics;

namespace Popolo.Core.Tests.HVAC.VRF
{
    /// <summary>Unit tests for <see cref="VRFSystem"/>.</summary>
    /// <remarks>
    /// Test conditions from testVRF1_C(), testVRF1_H(), TestNoControl1(),
    /// and testVRF2_C() / testVRF2_H() sample code.
    ///
    /// Common rated machine (cooling-only, testVRF1_C):
    ///   Refrigerant: R410A
    ///   OA: 187*1.2/60 kg/s, fanElec=0
    ///   Cooling: rated=-28 kW/8.93 kW, mid1=-12.6/2.35, mid2=-13.2/1.94
    ///   PipeLength: NOM=7.5m, LONG=100m, FC=0.89
    ///   2 indoor units: 34.5*1.2/60 kg/s, 0, -14.0 kW each
    ///
    /// Common rated machine (heat-pump, testVRF1_H):
    ///   Same outdoor + heating: rated=31.5/8.68, mid=14.2/2.54
    ///   PipeLength: NOM=7.5m, LONG_C=100m, FC_C=0.89, LONG_H=80m, FC_H=0.91
    ///
    /// JIS cooling rating:
    ///   OA: 35°C DB / 24°C WB; IA: 27°C DB / 19°C WB
    ///
    /// JIS heating rating:
    ///   OA: 7°C DB / 6°C WB; IA: 20°C DB / 15°C WB
    /// </remarks>
    public class VRFSystemTests
    {
        #region 定数

        private static readonly double NOM_IHEX_AFLOW = 34.5 * 1.2 / 60.0;
        private static readonly double NOM_OHEX_AFLOW_S = 187.0 * 1.2 / 60.0;
        private const double IHEX_CAP_C = 14.0;
        private const double IHEX_CAP_H = 16.0;
        private const double NOM_PIPE = 7.5;
        private const double LONG_PIPE_C = 100.0;
        private const double FC_C = 0.89;
        private const double LONG_PIPE_H = 80.0;
        private const double FC_H = 0.91;
        private const double ATM = 101.325;

        #endregion

        #region ヘルパー

        private static double HR_from_DBT_WBT(double dbt, double wbt)
            => MoistAir.GetHumidityRatioFromDryBulbTemperatureAndWetBulbTemperature(dbt, wbt, ATM);

        /// <summary>
        /// testVRF1_C() と同じ冷房専用システムを生成する。
        /// </summary>
        private static (VRFSystem vrf, VRFUnit[] iHexes) MakeCoolingOnlySystem()
        {
            var r410a = new Refrigerant(Refrigerant.Fluid.R410A);
            var iHex = VRFSystem.MakeIndoorUnit_Cooling(NOM_IHEX_AFLOW, 0, -IHEX_CAP_C);

            var vrf = new VRFSystem(
                r410a, NOM_OHEX_AFLOW_S, 0,
                -28.0, 8.93, -12.6, 2.35, -13.2, 1.94,
                NOM_PIPE, LONG_PIPE_C, FC_C, iHex);
            vrf.CurrentMode = VRFSystem.Mode.Cooling;

            var iHexes = new VRFUnit[]
            {
                VRFSystem.MakeIndoorUnit_Cooling(NOM_IHEX_AFLOW, 0, -IHEX_CAP_C),
                VRFSystem.MakeIndoorUnit_Cooling(NOM_IHEX_AFLOW, 0, -IHEX_CAP_C)
            };
            vrf.AddIndoorUnit(iHexes);
            vrf.MaxEvaporatingTemperature = 18;
            return (vrf, iHexes);
        }

        /// <summary>
        /// testVRF1_H() と同じ冷暖切替システムを生成する。
        /// </summary>
        private static (VRFSystem vrf, VRFUnit[] iHexes) MakeHeatPumpSystem()
        {
            var r410a = new Refrigerant(Refrigerant.Fluid.R410A);
            var iHex = VRFSystem.MakeIndoorUnit(NOM_IHEX_AFLOW, 0, -IHEX_CAP_C, 0, IHEX_CAP_H);
            iHex.CurrentMode = VRFUnit.Mode.Heating;

            var vrf = new VRFSystem(
                r410a,
                NOM_OHEX_AFLOW_S, 0, -28.0, 8.93, -12.6, 2.35, -13.2, 1.94,
                NOM_OHEX_AFLOW_S, 0, 31.5, 8.68, 14.2, 2.54,
                NOM_PIPE, LONG_PIPE_C, FC_C, LONG_PIPE_H, FC_H, iHex);
            vrf.CurrentMode = VRFSystem.Mode.Heating;

            var iHexes = new VRFUnit[]
            {
                VRFSystem.MakeIndoorUnit(NOM_IHEX_AFLOW, 0, -IHEX_CAP_C, 0, IHEX_CAP_H),
                VRFSystem.MakeIndoorUnit(NOM_IHEX_AFLOW, 0, -IHEX_CAP_C, 0, IHEX_CAP_H)
            };
            vrf.AddIndoorUnit(iHexes);
            for (int i = 0; i < vrf.IndoorUnitCount; i++)
                vrf.SetIndoorUnitMode(i, VRFUnit.Mode.Heating);
            return (vrf, iHexes);
        }

        #endregion

        // ================================================================
        #region コンストラクタ・プロパティ

        /// <summary>冷房専用機の NominalCoolingCapacity が負値。</summary>
        [Fact]
        public void Constructor_CoolingOnly_NominalCoolingCapacityIsNegative()
        {
            var (vrf, _) = MakeCoolingOnlySystem();
            Assert.True(vrf.NominalCoolingCapacity < 0,
                $"NominalCoolingCapacity={vrf.NominalCoolingCapacity:F2} kW < 0");
        }

        /// <summary>ヒートポンプ機の NominalHeatingCapacity が正値。</summary>
        [Fact]
        public void Constructor_HeatPump_NominalHeatingCapacityIsPositive()
        {
            var (vrf, _) = MakeHeatPumpSystem();
            Assert.True(vrf.NominalHeatingCapacity > 0,
                $"NominalHeatingCapacity={vrf.NominalHeatingCapacity:F2} kW > 0");
        }

        /// <summary>AddIndoorUnit 後に IndoorUnitNumber = 2。</summary>
        [Fact]
        public void AddIndoorUnit_IndoorUnitNumberIsCorrect()
        {
            var (vrf, _) = MakeCoolingOnlySystem();
            Assert.Equal(2, vrf.IndoorUnitCount);
        }

        #endregion

        // ================================================================
        #region 冷房定格条件（testVRF1_C の定格条件テストより）

        /// <summary>
        /// JIS冷房定格条件（OA=35/24°C, IA=27/19°C）で UpdateState → CompressorElectricity > 0。
        /// testVRF1_C の「定格条件テスト」に対応。
        /// </summary>
        [Fact]
        public void UpdateState_CoolingRated_CompressorElecIsPositive()
        {
            var (vrf, iHexes) = MakeCoolingOnlySystem();
            double oHmd = HR_from_DBT_WBT(35, 24);
            vrf.OutdoorAirDryBulbTemperature = 35;
            vrf.OutdoorAirHumidityRatio = oHmd;

            double iHmd = HR_from_DBT_WBT(27, 19);
            // 定格負荷を SolveHeatLoad で設定
            iHexes[0].SolveHeatLoad(-IHEX_CAP_C, NOM_IHEX_AFLOW, 27, iHmd, false);
            for (int i = 0; i < vrf.IndoorUnitCount; i++)
            {
                vrf.SetIndoorUnitMode(i, VRFUnit.Mode.Cooling);
                vrf.SetIndoorUnitSetpointTemperature(i, iHexes[0].OutletAirTemperature);
            }
            vrf.UpdateState();

            Assert.True(vrf.CompressorElectricity > 0,
                $"CompressorElectricity={vrf.CompressorElectricity:F2} kW > 0");
        }

        /// <summary>冷房定格条件で EvaporatingTemperature が MaxEvaporatingTemperature 以下。</summary>
        [Fact]
        public void UpdateState_CoolingRated_EvpTempWithinRange()
        {
            var (vrf, iHexes) = MakeCoolingOnlySystem();
            double oHmd = HR_from_DBT_WBT(35, 24);
            vrf.OutdoorAirDryBulbTemperature = 35;
            vrf.OutdoorAirHumidityRatio = oHmd;
            double iHmd = HR_from_DBT_WBT(27, 19);
            iHexes[0].SolveHeatLoad(-IHEX_CAP_C, NOM_IHEX_AFLOW, 27, iHmd, false);
            for (int i = 0; i < vrf.IndoorUnitCount; i++)
            {
                vrf.SetIndoorUnitMode(i, VRFUnit.Mode.Cooling);
                vrf.SetIndoorUnitSetpointTemperature(i, iHexes[0].OutletAirTemperature);
            }
            vrf.UpdateState();

            Assert.True(vrf.EvaporatingTemperature <= vrf.MaxEvaporatingTemperature,
                $"EvpTemp={vrf.EvaporatingTemperature:F1}°C <= Max={vrf.MaxEvaporatingTemperature:F1}°C");
        }

        /// <summary>
        /// 外気温が高いほど消費電力が大きい（OA=23C vs 35C）。
        /// testVRF2_C の外気条件変化ループに対応。
        /// </summary>
        [Fact]
        public void UpdateState_Cooling_HigherOATemp_HigherElectricity()
        {
            double iHmd = HR_from_DBT_WBT(27, 19);

            var (vrf23, iH23) = MakeCoolingOnlySystem();
            double oHmd23 = HR_from_DBT_WBT(23, 14);
            vrf23.OutdoorAirDryBulbTemperature = 23;
            vrf23.OutdoorAirHumidityRatio = oHmd23;
            iH23[0].SolveHeatLoad(-IHEX_CAP_C * 0.7, NOM_IHEX_AFLOW, 27, iHmd, false);
            for (int i = 0; i < vrf23.IndoorUnitCount; i++)
            {
                vrf23.SetIndoorUnitMode(i, VRFUnit.Mode.Cooling);
                vrf23.SetIndoorUnitSetpointTemperature(i, iH23[0].OutletAirTemperature);
            }
            vrf23.UpdateState();
            double ec23 = vrf23.CompressorElectricity;

            var (vrf35, iH35) = MakeCoolingOnlySystem();
            double oHmd35 = HR_from_DBT_WBT(35, 24);
            vrf35.OutdoorAirDryBulbTemperature = 35;
            vrf35.OutdoorAirHumidityRatio = oHmd35;
            iH35[0].SolveHeatLoad(-IHEX_CAP_C * 0.7, NOM_IHEX_AFLOW, 27, iHmd, false);
            for (int i = 0; i < vrf35.IndoorUnitCount; i++)
            {
                vrf35.SetIndoorUnitMode(i, VRFUnit.Mode.Cooling);
                vrf35.SetIndoorUnitSetpointTemperature(i, iH35[0].OutletAirTemperature);
            }
            vrf35.UpdateState();
            double ec35 = vrf35.CompressorElectricity;

            Assert.True(ec35 > ec23,
                $"OA=35C EC={ec35:F3} kW > OA=23C EC={ec23:F3} kW");
        }

        /// <summary>GetHeatLoad が冷房時に負値（室内機から熱を除去）。</summary>
        [Fact]
        public void UpdateState_Cooling_HeatLoadIsNegative()
        {
            var (vrf, iHexes) = MakeCoolingOnlySystem();
            double oHmd = HR_from_DBT_WBT(35, 24);
            vrf.OutdoorAirDryBulbTemperature = 35;
            vrf.OutdoorAirHumidityRatio = oHmd;
            double iHmd = HR_from_DBT_WBT(27, 19);
            iHexes[0].SolveHeatLoad(-IHEX_CAP_C, NOM_IHEX_AFLOW, 27, iHmd, false);
            for (int i = 0; i < vrf.IndoorUnitCount; i++)
            {
                vrf.SetIndoorUnitMode(i, VRFUnit.Mode.Cooling);
                vrf.SetIndoorUnitSetpointTemperature(i, iHexes[0].OutletAirTemperature);
            }
            vrf.UpdateState();
            Assert.True(vrf.GetHeatLoad() < 0,
                $"HeatLoad={vrf.GetHeatLoad():F2} kW < 0 (cooling removes heat)");
        }

        #endregion

        // ================================================================
        #region 暖房定格条件（testVRF1_H の定格条件テストより）

        /// <summary>
        /// JIS暖房定格条件（OA=7/6°C, IA=20/15°C）で UpdateState → CompressorElectricity > 0。
        /// testVRF1_H の「定格条件テスト」に対応。
        /// </summary>
        [Fact]
        public void UpdateState_HeatingRated_CompressorElecIsPositive()
        {
            var (vrf, iHexes) = MakeHeatPumpSystem();
            double oHmd = HR_from_DBT_WBT(7, 6);
            vrf.OutdoorAirDryBulbTemperature = 7;
            vrf.OutdoorAirHumidityRatio = oHmd;

            double iHmd = HR_from_DBT_WBT(20, 15);
            iHexes[0].SolveHeatLoad(IHEX_CAP_H / 2.0, NOM_IHEX_AFLOW, 20, iHmd, false);
            double tSP = iHexes[0].OutletAirTemperature;
            for (int i = 0; i < vrf.IndoorUnitCount; i++)
            {
                vrf.SetIndoorUnitMode(i, VRFUnit.Mode.Heating);
                vrf.SetIndoorUnitSetpointTemperature(i, tSP);
            }
            vrf.UpdateState();

            Assert.True(vrf.CompressorElectricity > 0,
                $"CompressorElectricity={vrf.CompressorElectricity:F2} kW > 0");
        }

        /// <summary>GetHeatLoad が暖房時に正値（室内機に熱を供給）。</summary>
        [Fact]
        public void UpdateState_Heating_HeatLoadIsPositive()
        {
            var (vrf, iHexes) = MakeHeatPumpSystem();
            double oHmd = HR_from_DBT_WBT(7, 6);
            vrf.OutdoorAirDryBulbTemperature = 7;
            vrf.OutdoorAirHumidityRatio = oHmd;
            double iHmd = HR_from_DBT_WBT(20, 15);
            iHexes[0].SolveHeatLoad(IHEX_CAP_H / 2.0, NOM_IHEX_AFLOW, 20, iHmd, false);
            double tSP = iHexes[0].OutletAirTemperature;
            for (int i = 0; i < vrf.IndoorUnitCount; i++)
            {
                vrf.SetIndoorUnitMode(i, VRFUnit.Mode.Heating);
                vrf.SetIndoorUnitSetpointTemperature(i, tSP);
            }
            vrf.UpdateState();
            Assert.True(vrf.GetHeatLoad() > 0,
                $"HeatLoad={vrf.GetHeatLoad():F2} kW > 0 (heating)");
        }

        /// <summary>
        /// 外気温が低いほど消費電力が大きい（OA=19C vs 7C）。
        /// testVRF2_H の外気条件変化ループに対応。
        /// </summary>
        [Fact]
        public void UpdateState_Heating_LowerOATemp_HigherElectricity()
        {
            double iHmd = HR_from_DBT_WBT(20, 15);

            var (vrf19, iH19) = MakeHeatPumpSystem();
            vrf19.OutdoorAirDryBulbTemperature = 19;
            vrf19.OutdoorAirHumidityRatio = HR_from_DBT_WBT(19, 16);
            iH19[0].SolveHeatLoad(IHEX_CAP_H * 0.5, NOM_IHEX_AFLOW, 20, iHmd, false);
            for (int i = 0; i < vrf19.IndoorUnitCount; i++)
            {
                vrf19.SetIndoorUnitMode(i, VRFUnit.Mode.Heating);
                vrf19.SetIndoorUnitSetpointTemperature(i, iH19[0].OutletAirTemperature);
            }
            vrf19.UpdateState();
            double ec19 = vrf19.CompressorElectricity;

            var (vrf7, iH7) = MakeHeatPumpSystem();
            vrf7.OutdoorAirDryBulbTemperature = 7;
            vrf7.OutdoorAirHumidityRatio = HR_from_DBT_WBT(7, 6);
            iH7[0].SolveHeatLoad(IHEX_CAP_H * 0.5, NOM_IHEX_AFLOW, 20, iHmd, false);
            for (int i = 0; i < vrf7.IndoorUnitCount; i++)
            {
                vrf7.SetIndoorUnitMode(i, VRFUnit.Mode.Heating);
                vrf7.SetIndoorUnitSetpointTemperature(i, iH7[0].OutletAirTemperature);
            }
            vrf7.UpdateState();
            double ec7 = vrf7.CompressorElectricity;

            Assert.True(ec7 > ec19,
                $"OA=7C EC={ec7:F3} kW > OA=19C EC={ec19:F3} kW");
        }

        #endregion

        // ================================================================
        #region ShutOff / 成り行き計算

        /// <summary>
        /// ShutOff モードでは CompressorElectricity = 0。
        /// TestNoControl1 の停止状態に対応。
        /// </summary>
        [Fact]
        public void UpdateState_ShutOff_ZeroCompressorElectricity()
        {
            var (vrf, iHexes) = MakeCoolingOnlySystem();
            vrf.CurrentMode = VRFSystem.Mode.ShutOff;
            vrf.OutdoorAirDryBulbTemperature = 35;
            vrf.OutdoorAirHumidityRatio = HR_from_DBT_WBT(35, 24);
            vrf.UpdateState(false);
            Assert.Equal(0.0, vrf.CompressorElectricity);
        }

        /// <summary>
        /// 成り行き計算（controlHeatload=false）で UpdateState → PartialLoadRate が [0,1] 内。
        /// TestNoControl1 のループに対応。
        /// </summary>
        [Fact]
        public void UpdateState_FreeRunning_PartialLoadRateInRange()
        {
            var (vrf, iHexes) = MakeCoolingOnlySystem();
            vrf.MinEvaporatingTemperature = 5;
            vrf.MaxEvaporatingTemperature = 30;
            vrf.TargetEvaporatingTemperature = 12;
            vrf.OutdoorAirDryBulbTemperature = 35;
            vrf.OutdoorAirHumidityRatio = HR_from_DBT_WBT(35, 24);
            for (int i = 0; i < vrf.IndoorUnitCount; i++)
            {
                vrf.SetIndoorUnitMode(i, VRFUnit.Mode.Cooling);
                vrf.SetIndoorUnitInletAirState(i, 25, 0.010);
            }
            vrf.UpdateState(false); // 成り行き計算
            Assert.InRange(vrf.PartialLoadRate, 0.0, 1.0);
        }

        #endregion

        // ================================================================
        #region COP

        /// <summary>冷房 COP が現実的な範囲（1〜8）。</summary>
        [Fact]
        public void UpdateState_Cooling_COPInRealisticRange()
        {
            var (vrf, iHexes) = MakeCoolingOnlySystem();
            double oHmd = HR_from_DBT_WBT(35, 24);
            vrf.OutdoorAirDryBulbTemperature = 35;
            vrf.OutdoorAirHumidityRatio = oHmd;
            double iHmd = HR_from_DBT_WBT(27, 19);
            iHexes[0].SolveHeatLoad(-IHEX_CAP_C, NOM_IHEX_AFLOW, 27, iHmd, false);
            for (int i = 0; i < vrf.IndoorUnitCount; i++)
            {
                vrf.SetIndoorUnitMode(i, VRFUnit.Mode.Cooling);
                vrf.SetIndoorUnitSetpointTemperature(i, iHexes[0].OutletAirTemperature);
            }
            vrf.UpdateState();
            Assert.InRange(vrf.GetCOP(), 1.0, 8.0);
        }

        #endregion
    }
}
