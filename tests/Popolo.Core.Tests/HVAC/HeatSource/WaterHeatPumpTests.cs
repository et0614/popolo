/* WaterHeatPumpTests.cs
 *
 * Copyright (C) 2026 E.Togashi
 * GNU General Public License v3 — see accompanying LICENSE file.
 */

using System;
using Xunit;
using Popolo.Core.HVAC.HeatSource;

namespace Popolo.Core.Tests.HVAC.HeatSource
{
    /// <summary>Unit tests for <see cref="WaterHeatPump"/>.</summary>
    /// <remarks>
    /// WaterHeatPump models a water-source heat pump that can operate in
    /// cooling or heating mode (reference: https://doi.org/10.3130/aije.82.453).
    ///
    /// Constructor:
    ///   (coolingCapacity, chilledWaterFlowRate, coolingWaterFlowRate,
    ///    chilledWaterOutletTemp, coolingWaterInletTemp, coolingEnergyConsumption,
    ///    heatingCapacity, hotWaterFlowRate, heatSourceWaterFlowRate,
    ///    hotWaterOutletTemp, heatSourceWaterInletTemp, heatingEnergyConsumption)
    ///
    /// CoolWater(chilledWaterFlowRate, coolingWaterFlowRate,
    ///           chilledWaterInletTemp, coolingWaterInletTemp):
    ///   Controls ChilledWaterSetPoint; computes CoolingLoad, EnergyConsumption.
    ///
    /// HeatWater(hotWaterFlowRate, heatSourceWaterFlowRate,
    ///           hotWaterInletTemp, heatSourceWaterInletTemp):
    ///   Controls HotWaterSetPoint; computes HeatingLoad, EnergyConsumption.
    /// </remarks>
    public class WaterHeatPumpTests
    {
        #region ヘルパー

        /// <summary>
        /// 標準的な水熱源ヒートポンプを生成する。
        /// 冷却定格: 能力100kW, 冷水12→7°C/3kg/s, 冷却水32°C/5kg/s, 消費電力30kW。
        /// 加熱定格: 能力110kW, 温水40→45°C/3kg/s, 熱源水10°C/5kg/s, 消費電力30kW。
        /// </summary>
        private static WaterHeatPump MakeHP()
        {
            var hp = new WaterHeatPump(
                coolingCapacity:              100.0,
                chilledWaterFlowRate:           3.0,
                coolingWaterFlowRate:           5.0,
                chilledWaterOutletTemperature:  7.0,
                coolingWaterInletTemperature:  32.0,
                coolingEnergyConsumption:      30.0,
                heatingCapacity:              110.0,
                hotWaterFlowRate:               3.0,
                heatSourceWaterFlowRate:        5.0,
                hotWaterOutletTemperature:     45.0,
                heatSourceWaterInletTemperature: 10.0,
                heatingEnergyConsumption:      30.0);
            hp.ChilledWaterSetpoint = 7.0;
            hp.HotWaterSetpoint     = 45.0;
            return hp;
        }

        #endregion

        // ================================================================
        #region コンストラクタ・プロパティ

        /// <summary>NominalCoolingCapacity がコンストラクタ指定値と一致する。</summary>
        [Fact]
        public void Constructor_NominalCoolingCapacity_MatchesInput()
        {
            var hp = MakeHP();
            Assert.InRange(hp.NominalCoolingCapacity, 99.0, 101.0);
        }

        /// <summary>NominalHeatingCapacity がコンストラクタ指定値と一致する。</summary>
        [Fact]
        public void Constructor_NominalHeatingCapacity_MatchesInput()
        {
            var hp = MakeHP();
            Assert.InRange(hp.NominalHeatingCapacity, 109.0, 111.0);
        }

        /// <summary>コンストラクタ直後は ShutOff モード。</summary>
        [Fact]
        public void Constructor_InitialMode_IsShutOff()
        {
            var hp = MakeHP();
            Assert.Equal(WaterHeatPump.OperatingMode.ShutOff, hp.Mode);
        }

        #endregion

        // ================================================================
        #region CoolWater — 冷却運転

        /// <summary>冷却負荷が正。</summary>
        [Fact]
        public void CoolWater_Normal_CoolingLoadIsPositive()
        {
            var hp = MakeHP();
            hp.CoolWater(3.0, 5.0, 12.0, 32.0);
            Assert.True(hp.CoolingLoad > 0,
                $"CoolingLoad={hp.CoolingLoad:F2} kW > 0");
        }

        /// <summary>冷水出口温度が入口温度より低い。</summary>
        [Fact]
        public void CoolWater_Normal_OutletCoolerThanInlet()
        {
            var hp = MakeHP();
            hp.CoolWater(3.0, 5.0, 12.0, 32.0);
            Assert.True(hp.ChilledWaterOutletTemperature < hp.ChilledWaterInletTemperature,
                $"Outlet={hp.ChilledWaterOutletTemperature:F2}°C < Inlet={hp.ChilledWaterInletTemperature:F2}°C");
        }

        /// <summary>消費電力が正。</summary>
        [Fact]
        public void CoolWater_Normal_EnergyConsumptionIsPositive()
        {
            var hp = MakeHP();
            hp.CoolWater(3.0, 5.0, 12.0, 32.0);
            Assert.True(hp.EnergyConsumption > 0,
                $"EnergyConsumption={hp.EnergyConsumption:F2} kW > 0");
        }

        /// <summary>冷却水出口温度が入口温度より高い（冷却水が熱を受け取る）。</summary>
        [Fact]
        public void CoolWater_Normal_CoolingWaterOutletHigherThanInlet()
        {
            var hp = MakeHP();
            hp.CoolWater(3.0, 5.0, 12.0, 32.0);
            Assert.True(hp.CoolingWaterOutletTemperature > hp.CoolingWaterInletTemperature,
                $"CW outlet={hp.CoolingWaterOutletTemperature:F2}°C > inlet={hp.CoolingWaterInletTemperature:F2}°C");
        }

        /// <summary>冷却水入口温度が低いほど COP が高い。</summary>
        [Fact]
        public void CoolWater_LowerCoolingWaterTemp_HigherCOP()
        {
            var hpHot = MakeHP();
            hpHot.CoolWater(3.0, 5.0, 12.0, 35.0);
            double copHot = hpHot.COP;

            var hpCold = MakeHP();
            hpCold.CoolWater(3.0, 5.0, 12.0, 25.0);
            double copCold = hpCold.COP;

            Assert.True(copCold > copHot,
                $"Cold CW COP={copCold:F3} > Hot CW COP={copHot:F3}");
        }

        #endregion

        // ================================================================
        #region HeatWater — 加熱運転

        /// <summary>加熱負荷が正。</summary>
        [Fact]
        public void HeatWater_Normal_HeatingLoadIsPositive()
        {
            var hp = MakeHP();
            hp.HeatWater(3.0, 5.0, 40.0, 10.0);
            Assert.True(hp.HeatingLoad > 0,
                $"HeatingLoad={hp.HeatingLoad:F2} kW > 0");
        }

        /// <summary>温水出口温度が入口温度より高い（温水を加熱）。</summary>
        [Fact]
        public void HeatWater_Normal_OutletWarmerThanInlet()
        {
            var hp = MakeHP();
            hp.HeatWater(3.0, 5.0, 40.0, 10.0);
            Assert.True(hp.HotWaterOutletTemperature > hp.HotWaterInletTemperature,
                $"Outlet={hp.HotWaterOutletTemperature:F2}°C > Inlet={hp.HotWaterInletTemperature:F2}°C");
        }

        /// <summary>熱源水出口温度が入口温度より低い（熱源水から採熱）。</summary>
        [Fact]
        public void HeatWater_Normal_HeatSourceOutletCoolerThanInlet()
        {
            var hp = MakeHP();
            hp.HeatWater(3.0, 5.0, 40.0, 10.0);
            Assert.True(hp.HeatSourceWaterOutletTemperature < hp.HeatSourceWaterInletTemperature,
                $"HS outlet={hp.HeatSourceWaterOutletTemperature:F2}°C < inlet={hp.HeatSourceWaterInletTemperature:F2}°C");
        }

        #endregion

        // ================================================================
        #region ShutOff

        /// <summary>ShutOff 後は CoolingLoad = HeatingLoad = 0。</summary>
        [Fact]
        public void ShutOff_ResetsLoads()
        {
            var hp = MakeHP();
            hp.CoolWater(3.0, 5.0, 12.0, 32.0);
            Assert.True(hp.CoolingLoad > 0);

            hp.ShutOff();
            Assert.Equal(0.0, hp.CoolingLoad);
            Assert.Equal(0.0, hp.HeatingLoad);
            Assert.Equal(0.0, hp.EnergyConsumption);
        }

        #endregion
    }
}
