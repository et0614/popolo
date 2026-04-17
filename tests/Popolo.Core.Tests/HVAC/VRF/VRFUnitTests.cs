/* VRFUnitTests.cs
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
    /// <summary>Unit tests for <see cref="VRFUnit"/>.</summary>
    /// <remarks>
    /// Test conditions from CrossFinEvaporatorTest() and CrossFinCondensorTest():
    ///
    /// Evaporator (cooling):
    ///   VRFUnit(167/60*1.2 kg/s, 0, evpTemp=2C, evpHeat=-13 kW, inlet=7C/RH85%, borderRH=95%)
    ///   UpdateWithRefrigerantTemperature(refrigerantTemp, airFlow, inletDbt, inletHr, deductDefrost)
    ///   HeatTransfer less than 0 (cooling)
    ///
    /// Condenser (heating):
    ///   VRFUnit(167/60*1.2 kg/s, 0, cndTemp=45C, cndHeat=+25 kW, inlet=35C/RH55%)
    ///   HeatTransfer greater than 0 (heating)
    /// </remarks>
    public class VRFUnitTests
    {
        private static readonly double AirFlow = 167.0 / 60 * 1.2;

        private static double GetHumidityRatio(double dbt, double rh)
            => MoistAir.GetHumidityRatioFromDryBulbTemperatureAndRelativeHumidity(
                dbt, rh, PhysicsConstants.StandardAtmosphericPressure);

        private static VRFUnit MakeEvaporator()
        {
            double hr = GetHumidityRatio(7.0, 85.0);
            var evp = new VRFUnit(AirFlow, 0.0, 2.0, -13.0, 7.0, hr, 95.0);
            evp.CurrentMode = VRFUnit.Mode.Cooling;
            return evp;
        }

        private static VRFUnit MakeCondenser()
        {
            double hr = GetHumidityRatio(35.0, 55.0);
            var cnd = new VRFUnit(AirFlow, 0.0, 45.0, 25.0, 35.0, hr);
            cnd.CurrentMode = VRFUnit.Mode.Heating;
            return cnd;
        }

        // ================================================================
        #region コンストラクタ

        [Fact]
        public void Constructor_Evaporator_SelectableModeIncludesCooling()
        {
            var evp = MakeEvaporator();
            Assert.True((evp.SelectableMode & VRFUnit.Mode.Cooling) != 0);
        }

        [Fact]
        public void Constructor_Evaporator_NominalCoolingCapacityIsNegative()
        {
            var evp = MakeEvaporator();
            Assert.True(evp.NominalCoolingCapacity < 0,
                $"NominalCoolingCapacity={evp.NominalCoolingCapacity:F2} kW < 0");
        }

        [Fact]
        public void Constructor_Evaporator_SurfaceAreaIsPositive()
        {
            var evp = MakeEvaporator();
            Assert.True(evp.SurfaceArea_Evaporator > 0,
                $"SurfaceArea_Evaporator={evp.SurfaceArea_Evaporator:F4} m2 > 0");
        }

        [Fact]
        public void Constructor_Condenser_SelectableModeIncludesHeating()
        {
            var cnd = MakeCondenser();
            Assert.True((cnd.SelectableMode & VRFUnit.Mode.Heating) != 0);
        }

        [Fact]
        public void Constructor_Condenser_NominalHeatingCapacityIsPositive()
        {
            var cnd = MakeCondenser();
            Assert.True(cnd.NominalHeatingCapacity > 0,
                $"NominalHeatingCapacity={cnd.NominalHeatingCapacity:F2} kW > 0");
        }

        [Fact]
        public void Constructor_Condenser_SurfaceAreaIsPositive()
        {
            var cnd = MakeCondenser();
            Assert.True(cnd.SurfaceArea_Condenser > 0,
                $"SurfaceArea_Condenser={cnd.SurfaceArea_Condenser:F4} m2 > 0");
        }

        #endregion

        // ================================================================
        #region 蒸発器 — CrossFinEvaporatorTest

        /// <summary>定格条件（入口7C、蒸発2C）で HeatTransfer が負値（冷却）。</summary>
        [Fact]
        public void Evaporator_RatedCondition_HeatTransferIsNegative()
        {
            var evp = MakeEvaporator();
            double hr = GetHumidityRatio(7.0, 85.0);
            evp.UpdateWithRefrigerantTemperature(2.0, AirFlow, 7.0, hr, false);
            Assert.True(evp.HeatTransfer < 0,
                $"HeatTransfer={evp.HeatTransfer:F2} kW < 0 (cooling)");
        }

        /// <summary>出口空気温度が入口より低い（冷却）。</summary>
        [Fact]
        public void Evaporator_RatedCondition_OutletCoolerThanInlet()
        {
            var evp = MakeEvaporator();
            double hr = GetHumidityRatio(7.0, 85.0);
            evp.UpdateWithRefrigerantTemperature(2.0, AirFlow, 7.0, hr, false);
            Assert.True(evp.OutletAirTemperature < evp.InletAirTemperature,
                $"Outlet={evp.OutletAirTemperature:F2}C < Inlet={evp.InletAirTemperature:F2}C");
        }

        /// <summary>入口温度が高いほど冷却能力が大きい（|HeatTransfer| 増加）。</summary>
        [Fact]
        public void Evaporator_HigherInletTemp_LargerCooling()
        {
            double hr0 = GetHumidityRatio(0.0, 85.0);
            var evp0 = MakeEvaporator();
            evp0.UpdateWithRefrigerantTemperature(2.0, AirFlow, 0.0, hr0, false);

            double hr10 = GetHumidityRatio(10.0, 85.0);
            var evp10 = MakeEvaporator();
            evp10.UpdateWithRefrigerantTemperature(2.0, AirFlow, 10.0, hr10, false);

            Assert.True(Math.Abs(evp10.HeatTransfer) > Math.Abs(evp0.HeatTransfer),
                $"Inlet=10C |Q|={Math.Abs(evp10.HeatTransfer):F2} > Inlet=0C |Q|={Math.Abs(evp0.HeatTransfer):F2}");
        }

        /// <summary>蒸発温度が低いほど冷却能力が大きい（te=-8 vs te=6）。</summary>
        [Fact]
        public void Evaporator_LowerEvpTemp_LargerCooling()
        {
            double hr = GetHumidityRatio(7.0, 85.0);

            var evpHigh = MakeEvaporator();
            evpHigh.UpdateWithRefrigerantTemperature(6.0, AirFlow, 7.0, hr, false);

            var evpLow = MakeEvaporator();
            evpLow.UpdateWithRefrigerantTemperature(-8.0, AirFlow, 7.0, hr, false);

            Assert.True(Math.Abs(evpLow.HeatTransfer) > Math.Abs(evpHigh.HeatTransfer),
                $"te=-8: |Q|={Math.Abs(evpLow.HeatTransfer):F2} > te=6: |Q|={Math.Abs(evpHigh.HeatTransfer):F2}");
        }

        /// <summary>氷点下の蒸発温度では DefrostLoad が 0 以上。</summary>
        [Fact]
        public void Evaporator_BelowFreezing_DefrostLoadIsNonNegative()
        {
            var evp = MakeEvaporator();
            double hr = GetHumidityRatio(-5.0, 85.0);
            evp.UpdateWithRefrigerantTemperature(-8.0, AirFlow, -5.0, hr, false);
            Assert.True(evp.DefrostLoad >= 0,
                $"DefrostLoad={evp.DefrostLoad:F3} kW >= 0");
        }

        #endregion

        // ================================================================
        #region 凝縮器 — CrossFinCondensorTest

        /// <summary>定格条件（入口35C/RH55%、凝縮45C）で HeatTransfer が正値（加熱）。</summary>
        [Fact]
        public void Condenser_RatedCondition_HeatTransferIsPositive()
        {
            var cnd = MakeCondenser();
            double hr = GetHumidityRatio(35.0, 55.0);
            cnd.UpdateWithRefrigerantTemperature(45.0, AirFlow, 35.0, hr, false);
            Assert.True(cnd.HeatTransfer > 0,
                $"HeatTransfer={cnd.HeatTransfer:F2} kW > 0 (heating)");
        }

        /// <summary>出口空気温度が入口より高い（加熱）。</summary>
        [Fact]
        public void Condenser_RatedCondition_OutletWarmerThanInlet()
        {
            var cnd = MakeCondenser();
            double hr = GetHumidityRatio(35.0, 55.0);
            cnd.UpdateWithRefrigerantTemperature(45.0, AirFlow, 35.0, hr, false);
            Assert.True(cnd.OutletAirTemperature > cnd.InletAirTemperature,
                $"Outlet={cnd.OutletAirTemperature:F2}C > Inlet={cnd.InletAirTemperature:F2}C");
        }

        /// <summary>入口温度が低いほど放熱量が大きい（乾球20C vs 35C）。</summary>
        [Fact]
        public void Condenser_LowerInletTemp_LargerHeatTransfer()
        {
            double hr20 = GetHumidityRatio(20.0, 55.0);
            var cnd20 = MakeCondenser();
            cnd20.UpdateWithRefrigerantTemperature(45.0, AirFlow, 20.0, hr20, false);

            double hr35 = GetHumidityRatio(35.0, 55.0);
            var cnd35 = MakeCondenser();
            cnd35.UpdateWithRefrigerantTemperature(45.0, AirFlow, 35.0, hr35, false);

            Assert.True(cnd20.HeatTransfer > cnd35.HeatTransfer,
                $"Inlet=20C Q={cnd20.HeatTransfer:F2} > Inlet=35C Q={cnd35.HeatTransfer:F2}");
        }

        /// <summary>
        /// 水噴霧（SprayEffectiveness=0.6）あり → 放熱量が噴霧なし以上。
        /// CrossFinCondensorTest の SprayEffectiveness 比較に対応。
        /// </summary>
        [Fact]
        public void Condenser_WaterSpray_IncreasesOrMaintainsHeatTransfer()
        {
            double hr = GetHumidityRatio(35.0, 55.0);

            var cndNo = MakeCondenser();
            cndNo.UseWaterSpray = true;
            cndNo.SprayEffectiveness = 0.0;
            cndNo.UpdateWithRefrigerantTemperature(45.0, AirFlow, 35.0, hr, false);

            var cndYes = MakeCondenser();
            cndYes.UseWaterSpray = true;
            cndYes.SprayEffectiveness = 0.6;
            cndYes.UpdateWithRefrigerantTemperature(45.0, AirFlow, 35.0, hr, false);

            Assert.True(cndYes.HeatTransfer >= cndNo.HeatTransfer,
                $"Spray Q={cndYes.HeatTransfer:F2} >= No-spray Q={cndNo.HeatTransfer:F2}");
        }

        /// <summary>水噴霧使用時は WaterSupply が正。</summary>
        [Fact]
        public void Condenser_WaterSpray_WaterSupplyIsPositive()
        {
            var cnd = MakeCondenser();
            cnd.UseWaterSpray = true;
            cnd.SprayEffectiveness = 0.6;
            double hr = GetHumidityRatio(35.0, 55.0);
            cnd.UpdateWithRefrigerantTemperature(45.0, AirFlow, 35.0, hr, false);
            Assert.True(cnd.WaterSupply > 0,
                $"WaterSupply={cnd.WaterSupply * 3600:F4} kg/h > 0");
        }

        #endregion

        // ================================================================
        #region ShutOff

        [Fact]
        public void Mode_ShutOff_ZeroHeatTransfer()
        {
            var evp = MakeEvaporator();
            evp.CurrentMode = VRFUnit.Mode.ShutOff;
            double hr = GetHumidityRatio(7.0, 85.0);
            evp.UpdateWithRefrigerantTemperature(2.0, AirFlow, 7.0, hr, false);
            Assert.Equal(0.0, evp.HeatTransfer);
        }

        #endregion
    }
}
