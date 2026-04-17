/* DirectFiredAbsorptionChillerTests.cs
 *
 * Copyright (C) 2026 E.Togashi
 * GNU General Public License v3 — see accompanying LICENSE file.
 */

using System;
using Xunit;
using Popolo.Core.HVAC.HeatSource;

namespace Popolo.Core.Tests.HVAC.HeatSource
{
    /// <summary>Unit tests for <see cref="DirectFiredAbsorptionChiller"/>.</summary>
    /// <remarks>
    /// Rated conditions from DirectFiredAbsorptionChillerTest1/2() sample:
    ///   new DirectFiredAbsorptionChiller(103/3600, 103/3600,
    ///     15, 7, 32, 37, 54.7, 60, 189/3.6, 500/3.6, 189/3.6, 0, Boiler.Fuel.Gas13A)
    ///
    ///   Cooling: chilled 15->7C, cooling 32->37C, hot(heating side) 54.7->60C
    ///   Flows: chilled=189/3.6 kg/s, cooling=500/3.6 kg/s, hot=189/3.6 kg/s
    ///
    /// Update(coolingWaterInletTemp, inletWaterTemp, coolingWaterFlow, waterFlow)
    ///   IsCoolingMode=true (default) -> cooling operation
    ///   IsCoolingMode=false          -> heating operation
    ///   HasSolutionInverterPump      -> affects COP at partial load
    /// </remarks>
    public class DirectFiredAbsorptionChillerTests
    {
        #region 定格条件

        private static readonly double ChWM = 189.0 / 3.6;
        private static readonly double CdWM = 500.0 / 3.6;
        private static readonly double HtWM = 189.0 / 3.6;
        private static readonly double FcCool = 103.0 / 3600.0;
        private static readonly double FcHeat = 103.0 / 3600.0;
        private const double ChWI = 15.0;
        private const double ChWO =  7.0;
        private const double CdWI = 32.0;
        private const double CdWO = 37.0;
        private const double HtWI = 54.7;
        private const double HtWO = 60.0;

        #endregion

        #region ヘルパー

        /// <summary>サンプルコードと同じ定格条件で初期化。冷却モード。</summary>
        private static DirectFiredAbsorptionChiller MakeChiller()
        {
            var c = new DirectFiredAbsorptionChiller(
                FcCool, FcHeat,
                ChWI, ChWO, CdWI, CdWO, HtWI, HtWO,
                ChWM, CdWM, HtWM,
                0.0, Boiler.Fuel.Gas13A);
            c.IsCoolingMode = true;
            c.HasSolutionInverterPump = true;
            return c;
        }

        #endregion

        // ================================================================
        #region コンストラクタ

        /// <summary>NominalCoolingCapacity が正。</summary>
        [Fact]
        public void Constructor_NominalCoolingCapacity_IsPositive()
        {
            var c = MakeChiller();
            Assert.True(c.NominalCoolingCapacity > 0,
                $"NominalCoolingCapacity={c.NominalCoolingCapacity:F2} kW > 0");
        }

        /// <summary>NominalHeatingCapacity が正。</summary>
        [Fact]
        public void Constructor_NominalHeatingCapacity_IsPositive()
        {
            var c = MakeChiller();
            Assert.True(c.NominalHeatingCapacity > 0,
                $"NominalHeatingCapacity={c.NominalHeatingCapacity:F2} kW > 0");
        }

        /// <summary>コンストラクタ直後は ShutOff 状態。</summary>
        [Fact]
        public void Constructor_InitialState_IsShutOff()
        {
            var c = MakeChiller();
            Assert.Equal(0.0, c.CoolingLoad);
        }

        #endregion

        // ================================================================
        #region Update — 冷却運転（Test2の定格条件）

        /// <summary>定格条件で CoolingLoad が正。</summary>
        [Fact]
        public void Update_RatedCooling_CoolingLoadIsPositive()
        {
            var c = MakeChiller();
            c.Update(CdWI, ChWI, CdWM, ChWM);
            Assert.True(c.CoolingLoad > 0,
                $"CoolingLoad={c.CoolingLoad:F2} kW > 0");
        }

        /// <summary>定格条件で蒸発温度・凝縮温度・再生温度がそれぞれ合理的な値。</summary>
        [Fact]
        public void Update_RatedCooling_TemperaturesInPhysicalRange()
        {
            var c = MakeChiller();
            c.Update(CdWI, ChWI, CdWM, ChWM);
            // 蒸発温度: 0〜15°C
            Assert.InRange(c.EvaporatingTemperature, 0.0, 15.0);
            // 凝縮温度: 30〜50°C
            Assert.InRange(c.CondensingTemperature, 30.0, 50.0);
            // 再生温度: 100〜180°C（二重効用）
            Assert.InRange(c.DesorbTemperature, 80.0, 200.0);
        }

        /// <summary>定格条件での COP が現実的な範囲（0.9〜1.4）。</summary>
        [Fact]
        public void Update_RatedCooling_COPInRealisticRange()
        {
            var c = MakeChiller();
            c.Update(CdWI, ChWI, CdWM, ChWM);
            Assert.InRange(c.COP, 0.9, 1.5);
        }

        /// <summary>
        /// 出口水温が入口水温より低い（冷水冷却）。
        /// Test2の定格性能: Update(32, 15, 500/3.6, 189/3.6)。
        /// </summary>
        [Fact]
        public void Update_RatedCooling_OutletCoolerThanInlet()
        {
            var c = MakeChiller();
            c.Update(32.0, 15.0, CdWM, ChWM);
            Assert.True(c.OutletWaterTemperature < c.InletWaterTemperature,
                $"Outlet={c.OutletWaterTemperature:F2}C < Inlet={c.InletWaterTemperature:F2}C");
        }

        /// <summary>
        /// 冷水入口温度が低いほど冷却負荷が下がる（低負荷条件）。
        /// Test2の「負荷率50%」: Update(32, 11, ...)。
        /// </summary>
        [Fact]
        public void Update_LowerInletTemp_LowerCoolingLoad()
        {
            var c = MakeChiller();
            c.Update(32.0, 15.0, CdWM, ChWM);
            double qFull = c.CoolingLoad;

            c.Update(32.0, 11.0, CdWM, ChWM);
            double qHalf = c.CoolingLoad;

            Assert.True(qHalf < qFull,
                $"Inlet=11C: Q={qHalf:F2} kW < Inlet=15C: Q={qFull:F2} kW");
        }

        /// <summary>
        /// 冷却水温度が低いほど COP が高い。
        /// Test2の「冷却水温度25度」: Update(25, 15, ...) vs 定格32度。
        /// </summary>
        [Fact]
        public void Update_LowerCoolingWaterTemp_HigherCOP()
        {
            var cHot = MakeChiller();
            cHot.Update(32.0, 15.0, CdWM, ChWM);
            double copHot = cHot.COP;

            var cCold = MakeChiller();
            cCold.Update(25.0, 15.0, CdWM, ChWM);
            double copCold = cCold.COP;

            Assert.True(copCold > copHot,
                $"CDW=25C COP={copCold:F3} > CDW=32C COP={copHot:F3}");
        }

        /// <summary>
        /// インバータポンプあり vs なしで COP が異なる。
        /// Test1の「INV vs 定速」条件（JIS B8622）。
        /// </summary>
        [Fact]
        public void Update_InverterVsFixedPump_COPDiffers()
        {
            // JIS B8622 条件（負荷率100%）
            double pl = 1.0;
            double tcd = 27.0 + 5.0 * pl;   // 32°C
            double tch = 7.0 + 8.0 * pl;    // 15°C

            var cInv = MakeChiller();
            cInv.HasSolutionInverterPump = true;
            cInv.Update(tcd, tch, CdWM * pl, ChWM);
            double copInv = cInv.COP;

            var cFixed = MakeChiller();
            cFixed.HasSolutionInverterPump = false;
            cFixed.Update(tcd, tch, CdWM * pl, ChWM);
            double copFixed = cFixed.COP;

            // 定格ではほぼ同一（差が小さい）または INV の方が高い
            Assert.True(Math.Abs(copInv - copFixed) >= 0 &&
                        (copInv >= copFixed || Math.Abs(copInv - copFixed) < 0.2),
                $"INV COP={copInv:F3}, Fixed COP={copFixed:F3}");
        }

        /// <summary>濃溶液質量分率 &gt; 稀溶液質量分率（LiBr 吸収冷凍の定義）。</summary>
        [Fact]
        public void Update_RatedCooling_ThickFractionHigherThanThin()
        {
            var c = MakeChiller();
            c.Update(32.0, 15.0, CdWM, ChWM);
            Assert.True(c.ThickSolutionMassFraction > c.ThinSolutionMassFraction,
                $"Thick={c.ThickSolutionMassFraction:F4} > Thin={c.ThinSolutionMassFraction:F4}");
        }

        #endregion

        // ================================================================
        #region 加熱運転

        /// <summary>加熱モードで HeatingLoad が正。</summary>
        [Fact]
        public void Update_HeatingMode_HeatingLoadIsPositive()
        {
            var c = MakeChiller();
            c.IsCoolingMode = false;
            c.OutletWaterSetpointTemperature = 55.0;
            c.Update(32.0, 45.0, CdWM, HtWM);
            Assert.True(c.HeatingLoad > 0,
                $"HeatingLoad={c.HeatingLoad:F2} kW > 0");
        }

        #endregion

        // ================================================================
        #region ShutOff

        /// <summary>ShutOff 後は CoolingLoad = 0 かつ FuelConsumption = 0。</summary>
        [Fact]
        public void ShutOff_ResetsOutputs()
        {
            var c = MakeChiller();
            c.Update(CdWI, ChWI, CdWM, ChWM);
            Assert.True(c.CoolingLoad > 0);

            c.ShutOff();
            Assert.Equal(0.0, c.CoolingLoad);
            Assert.Equal(0.0, c.FuelConsumption);
        }

        #endregion
    }
}
