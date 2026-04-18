/* AirHeatSourceModularChillersTests.cs
 *
 * Copyright (C) 2026 E.Togashi
 * GNU General Public License v3 — see accompanying LICENSE file.
 */

using System;
using Xunit;
using Popolo.Core.HVAC.HeatSource;

namespace Popolo.Core.Tests.HVAC.HeatSource
{
    /// <summary>Unit tests for <see cref="AirHeatSourceModularChillers"/>.</summary>
    /// <remarks>
    /// Test conditions from AirSourceHeatPumpTest() sample code:
    ///   new AirHeatSourceModularChillers(150, 7, 430/60, 35, 850/60*1.2, 49.8,
    ///                                    150, 45, 430/60, 7, 850/60*1.2, 50.0, 3, 1.9)
    ///   Total water flow: 430*3/60 = 21.5 kg/s
    ///   Nominal cooling COP ~= 3.01, heating COP ~= 3.00
    ///
    /// Cooling: twi = 7 + load / (cp * mw)
    /// Heating: twi = 45 - load / (cp * mw)
    /// </remarks>
    public class AirHeatSourceModularChillersTests
    {
        #region 定格条件

        private const double CoolingCap  = 150.0;
        private const double HeatingCap  = 150.0;
        private const int    Units       = 3;
        private const double ChwOutlet   = 7.0;
        private const double HwOutlet    = 45.0;
        private const double CoolingAirT = 35.0;
        private const double HeatingAirT = 7.0;
        private static readonly double Mw = 430.0 * 3 / 60.0;
        private static readonly double MwPerUnit = 430.0 / 60.0;
        private const double Cp = 4.186;

        #endregion

        #region ヘルパー

        private static AirHeatSourceModularChillers MakeHP()
            => new AirHeatSourceModularChillers(
                CoolingCap, ChwOutlet, MwPerUnit, CoolingAirT, 850.0/60*1.2, 49.8,
                HeatingCap, HwOutlet,  MwPerUnit, HeatingAirT, 850.0/60*1.2, 50.0,
                Units, 1.9);

        private static double CoolingInletTemp(double pl)
            => ChwOutlet + (CoolingCap * pl) / (Cp * Mw);

        private static double HeatingInletTemp(double pl)
            => HwOutlet - (HeatingCap * pl) / (Cp * Mw);

        #endregion

        // ================================================================
        #region コンストラクタ

        [Fact]
        public void Constructor_NumberOfUnits_MatchesInput()
        {
            Assert.Equal(Units, MakeHP().UnitCount);
        }

        [Fact]
        public void Constructor_NominalCoolingCapacity_MatchesInput()
        {
            Assert.InRange(MakeHP().NominalCoolingCapacity, 149.9, 150.1);
        }

        [Fact]
        public void Constructor_NominalHeatingCapacity_MatchesInput()
        {
            Assert.InRange(MakeHP().NominalHeatingCapacity, 149.9, 150.1);
        }

        [Fact]
        public void Constructor_IsHeatPumpModel_IsTrue()
        {
            Assert.True(MakeHP().IsHeatPumpModel);
        }

        [Fact]
        public void Constructor_InitialState_IsShutOff()
        {
            var hp = MakeHP();
            Assert.Equal(0.0, hp.CoolingLoad);
            Assert.Equal(0.0, hp.HeatingLoad);
        }

        #endregion

        // ================================================================
        #region 冷房運転

        /// <summary>100%負荷・外気35C で CoolingLoad が正。</summary>
        [Fact]
        public void Update_Cooling_FullLoad_CoolingLoadIsPositive()
        {
            var hp = MakeHP();
            hp.Mode = AirHeatSourceModularChillers.OperatingMode.Cooling;
            hp.WaterOutletSetpointTemperature = ChwOutlet;
            hp.MaximizeEfficiency = true;
            hp.MinimumPartialLoadRatio = 0.2;
            hp.Update(CoolingInletTemp(1.0), Mw, CoolingAirT);
            Assert.True(hp.CoolingLoad > 0,
                $"CoolingLoad={hp.CoolingLoad:F2} kW > 0");
        }

        /// <summary>冷房時、出口水温が設定値付近になる。</summary>
        [Fact]
        public void Update_Cooling_OutletNearSetpoint()
        {
            var hp = MakeHP();
            hp.Mode = AirHeatSourceModularChillers.OperatingMode.Cooling;
            hp.WaterOutletSetpointTemperature = ChwOutlet;
            hp.Update(CoolingInletTemp(0.7), Mw, CoolingAirT);
            if (!hp.IsOverLoad)
                Assert.InRange(hp.WaterOutletTemperature, ChwOutlet - 0.5, ChwOutlet + 0.5);
        }

        /// <summary>外気温が低いほど COP が高い（tai=25 vs 35）。</summary>
        [Fact]
        public void Update_Cooling_LowerAmbient_HigherCOP()
        {
            var hp35 = MakeHP();
            hp35.Mode = AirHeatSourceModularChillers.OperatingMode.Cooling;
            hp35.WaterOutletSetpointTemperature = ChwOutlet;
            hp35.Update(CoolingInletTemp(0.7), Mw, 35.0);

            var hp25 = MakeHP();
            hp25.Mode = AirHeatSourceModularChillers.OperatingMode.Cooling;
            hp25.WaterOutletSetpointTemperature = ChwOutlet;
            hp25.Update(CoolingInletTemp(0.7), Mw, 25.0);

            Assert.True(hp25.COP > hp35.COP,
                $"Amb=25C COP={hp25.COP:F3} > Amb=35C COP={hp35.COP:F3}");
        }

        /// <summary>冷房 COP が現実的な範囲（1〜8）。</summary>
        [Fact]
        public void Update_Cooling_COPInRealisticRange()
        {
            var hp = MakeHP();
            hp.Mode = AirHeatSourceModularChillers.OperatingMode.Cooling;
            hp.WaterOutletSetpointTemperature = ChwOutlet;
            hp.Update(CoolingInletTemp(1.0), Mw, CoolingAirT);
            Assert.InRange(hp.COP, 1.0, 8.0);
        }

        /// <summary>消費電力が正。</summary>
        [Fact]
        public void Update_Cooling_ElectricConsumptionIsPositive()
        {
            var hp = MakeHP();
            hp.Mode = AirHeatSourceModularChillers.OperatingMode.Cooling;
            hp.WaterOutletSetpointTemperature = ChwOutlet;
            hp.Update(CoolingInletTemp(1.0), Mw, CoolingAirT);
            Assert.True(hp.ElectricConsumption > 0);
        }

        #endregion

        // ================================================================
        #region 暖房運転

        /// <summary>100%負荷・外気7C で HeatingLoad が正。</summary>
        [Fact]
        public void Update_Heating_FullLoad_HeatingLoadIsPositive()
        {
            var hp = MakeHP();
            hp.Mode = AirHeatSourceModularChillers.OperatingMode.Heating;
            hp.WaterOutletSetpointTemperature = HwOutlet;
            hp.MaximizeEfficiency = true;
            hp.MinimumPartialLoadRatio = 0.2;
            hp.Update(HeatingInletTemp(1.0), Mw, HeatingAirT);
            Assert.True(hp.HeatingLoad > 0,
                $"HeatingLoad={hp.HeatingLoad:F2} kW > 0");
        }

        /// <summary>暖房時、出口水温が設定値付近になる。</summary>
        [Fact]
        public void Update_Heating_OutletNearSetpoint()
        {
            var hp = MakeHP();
            hp.Mode = AirHeatSourceModularChillers.OperatingMode.Heating;
            hp.WaterOutletSetpointTemperature = HwOutlet;
            hp.Update(HeatingInletTemp(0.7), Mw, HeatingAirT);
            if (!hp.IsOverLoad)
                Assert.InRange(hp.WaterOutletTemperature, HwOutlet - 0.5, HwOutlet + 0.5);
        }

        /// <summary>外気温が高いほど COP が高い（tai=20 vs 7）。</summary>
        [Fact]
        public void Update_Heating_HigherAmbient_HigherCOP()
        {
            var hp7 = MakeHP();
            hp7.Mode = AirHeatSourceModularChillers.OperatingMode.Heating;
            hp7.WaterOutletSetpointTemperature = HwOutlet;
            hp7.Update(HeatingInletTemp(0.7), Mw, 7.0);

            var hp20 = MakeHP();
            hp20.Mode = AirHeatSourceModularChillers.OperatingMode.Heating;
            hp20.WaterOutletSetpointTemperature = HwOutlet;
            hp20.Update(HeatingInletTemp(0.7), Mw, 20.0);

            Assert.True(hp20.COP > hp7.COP,
                $"Amb=20C COP={hp20.COP:F3} > Amb=7C COP={hp7.COP:F3}");
        }

        /// <summary>暖房 COP が現実的な範囲（1〜8）。</summary>
        [Fact]
        public void Update_Heating_COPInRealisticRange()
        {
            var hp = MakeHP();
            hp.Mode = AirHeatSourceModularChillers.OperatingMode.Heating;
            hp.WaterOutletSetpointTemperature = HwOutlet;
            hp.Update(HeatingInletTemp(1.0), Mw, HeatingAirT);
            Assert.InRange(hp.COP, 1.0, 8.0);
        }

        #endregion

        // ================================================================
        #region ShutOff

        [Fact]
        public void ShutOff_ZeroLoads()
        {
            var hp = MakeHP();
            hp.Mode = AirHeatSourceModularChillers.OperatingMode.Cooling;
            hp.WaterOutletSetpointTemperature = ChwOutlet;
            hp.Update(CoolingInletTemp(1.0), Mw, CoolingAirT);
            Assert.True(hp.CoolingLoad > 0);
            hp.ShutOff();
            Assert.Equal(0.0, hp.CoolingLoad);
            Assert.Equal(0.0, hp.HeatingLoad);
        }

        [Fact]
        public void Mode_SetToShutOff_ZeroLoad()
        {
            var hp = MakeHP();
            hp.Mode = AirHeatSourceModularChillers.OperatingMode.Cooling;
            hp.WaterOutletSetpointTemperature = ChwOutlet;
            hp.Update(CoolingInletTemp(1.0), Mw, CoolingAirT);
            hp.Mode = AirHeatSourceModularChillers.OperatingMode.ShutOff;
            hp.Update(CoolingInletTemp(1.0), Mw, CoolingAirT);
            Assert.Equal(0.0, hp.CoolingLoad);
        }

        #endregion
    }
}
