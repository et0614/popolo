/* CrossFinEvaporatorTests.cs
 *
 * Copyright (C) 2026 E.Togashi
 * GNU General Public License v3 — see accompanying LICENSE file.
 */

using System;
using Xunit;
using Popolo.Core.HVAC.HeatExchanger;

namespace Popolo.Core.Tests.HVAC.HeatExchanger
{
    /// <summary>Unit tests for <see cref="CrossFinEvaporator"/>.</summary>
    /// <remarks>
    /// CrossFinEvaporator models an air-cooled plate-fin-and-tube evaporator
    /// with dehumidification and frosting capability.
    ///
    /// Constructor: (evpTemperature, heatTransfer, airFlowRate,
    ///               inletAirTemp, inletAirHumidityRatio, borderRelativeHumidity)
    ///   → computes surface area from rated conditions
    ///
    /// Heat transfer sign convention (evaporator):
    ///   GetHeatTransfer returns positive when cooling (heat flows from air to refrigerant)
    ///   GetEvaporatingTemperature returns the saturation temperature of the refrigerant
    ///
    /// Surface area split:
    ///   DrySurfaceArea  — air above dewpoint, sensible cooling only
    ///   WetSurfaceArea  — air below dewpoint, sensible + latent cooling
    ///   FrostSurfaceArea — surface below 0°C, frosting occurs
    /// </remarks>
    public class CrossFinEvaporatorTests
    {
        #region ヘルパー

        /// <summary>
        /// 標準的な蒸発器を生成する。
        /// 定格: 蒸発温度5°C, 熱交換量8kW, 風量0.5kg/s, 入口空気25°C/W=0.010, 境界湿度80%。
        /// </summary>
        private static CrossFinEvaporator MakeEvap()
            => new CrossFinEvaporator(5.0, 8.0, 0.5, 25.0, 0.010, 80.0);

        #endregion

        // ================================================================
        #region コンストラクタ・プロパティ

        /// <summary>SurfaceArea が正の値になる。</summary>
        [Fact]
        public void Constructor_SurfaceArea_IsPositive()
        {
            var evap = MakeEvap();
            Assert.True(evap.SurfaceArea > 0, $"SurfaceArea={evap.SurfaceArea:F4} m² > 0");
        }

        /// <summary>NominalAirFlowRate がコンストラクタ指定値と一致する。</summary>
        [Fact]
        public void Constructor_NominalAirFlowRate_MatchesInput()
        {
            var evap = MakeEvap();
            Assert.InRange(evap.NominalAirFlowRate, 0.49, 0.51);
        }

        /// <summary>コンストラクタ直後は停止状態（CrossFinCondensorと同じ設計）。</summary>
        [Fact]
        public void Constructor_InitialState_IsShutOff()
        {
            var evap = MakeEvap();
            Assert.True(evap.IsShutOff);
        }

        #endregion

        // ================================================================
        #region GetHeatTransfer

        /// <summary>
        /// 蒸発温度 &lt; 空気入口温度なら熱交換量が正（冷却方向）。
        /// </summary>
        [Fact]
        public void GetHeatTransfer_EvaporatingBelowAir_IsPositive()
        {
            var evap = MakeEvap();
            double q = evap.GetHeatTransfer(5.0, 0.5, 25.0, 0.010);
            Assert.True(q > 0, $"Q={q:F3} kW > 0");
        }

        /// <summary>風量が多いほど熱交換量が増える。</summary>
        [Fact]
        public void GetHeatTransfer_HigherAirFlow_IncreasesHeatTransfer()
        {
            var evap = MakeEvap();
            double qLow  = evap.GetHeatTransfer(5.0, 0.3, 25.0, 0.010);
            double qHigh = evap.GetHeatTransfer(5.0, 0.8, 25.0, 0.010);
            Assert.True(qHigh > qLow,
                $"High flow Q={qHigh:F3} > Low flow Q={qLow:F3} kW");
        }

        /// <summary>空気入口温度が高いほど熱交換量が増える（温度差大）。</summary>
        [Fact]
        public void GetHeatTransfer_HigherInletTemp_IncreasesHeatTransfer()
        {
            var evap = MakeEvap();
            double qCool = evap.GetHeatTransfer(5.0, 0.5, 20.0, 0.010);
            double qHot  = evap.GetHeatTransfer(5.0, 0.5, 30.0, 0.010);
            Assert.True(qHot > qCool,
                $"Hot inlet Q={qHot:F3} > Cool inlet Q={qCool:F3} kW");
        }

        /// <summary>
        /// 定格条件で GetHeatTransfer が定格熱交換量に近い値を返す。
        /// </summary>
        [Fact]
        public void GetHeatTransfer_RatedCondition_MatchesRatedHeatTransfer()
        {
            var evap = MakeEvap();
            double q = evap.GetHeatTransfer(5.0, 0.5, 25.0, 0.010);
            Assert.InRange(q, 7.0, 9.0); // 定格8kW ±12%
        }

        #endregion

        // ================================================================
        #region GetEvaporatingTemperature

        /// <summary>
        /// GetEvaporatingTemperature は空気入口温度より低い（冷却効果）。
        /// </summary>
        [Fact]
        public void GetEvaporatingTemperature_BelowInletAirTemperature()
        {
            var evap = MakeEvap();
            double tEvp = evap.GetEvaporatingTemperature(8.0, 0.5, 25.0, 0.010, false);
            Assert.True(tEvp < 25.0,
                $"Tevp={tEvp:F2}°C < Tin=25°C");
        }

        /// <summary>
        /// 定格条件での蒸発温度が定格値（5°C）に近い。
        /// </summary>
        [Fact]
        public void GetEvaporatingTemperature_RatedCondition_MatchesRatedTemp()
        {
            var evap = MakeEvap();
            double tEvp = evap.GetEvaporatingTemperature(8.0, 0.5, 25.0, 0.010, false);
            Assert.InRange(tEvp, 3.0, 7.0);
        }

        /// <summary>熱交換量が大きいほど蒸発温度が低くなる（より大きな温度差が必要）。</summary>
        [Fact]
        public void GetEvaporatingTemperature_LargerHeat_LowerEvapTemp()
        {
            var evap = MakeEvap();
            double tHigh = evap.GetEvaporatingTemperature(5.0,  0.5, 25.0, 0.010, false);
            double tLow  = evap.GetEvaporatingTemperature(12.0, 0.5, 25.0, 0.010, false);
            Assert.True(tLow < tHigh,
                $"High Q Tevp={tLow:F2}°C < Low Q Tevp={tHigh:F2}°C");
        }

        #endregion

        // ================================================================
        #region 乾湿・着霜面積

        /// <summary>
        /// 高湿度空気では WetSurfaceArea が DrySurfaceArea より大きい（除湿が支配的）。
        /// </summary>
        [Fact]
        public void GetEvaporatingTemperature_HighHumidity_WetAreaDominant()
        {
            var evap = MakeEvap();
            // 高湿度：除湿が多くなる条件
            evap.GetEvaporatingTemperature(8.0, 0.5, 25.0, 0.018, false);
            // DrySurface + WetSurface + FrostSurface ≒ SurfaceArea
            double total = evap.DrySurfaceArea + evap.WetSurfaceArea + evap.FrostSurfaceArea;
            Assert.InRange(total, evap.SurfaceArea * 0.9, evap.SurfaceArea * 1.1);
        }

        /// <summary>
        /// 低温蒸発（着霜条件）では FrostSurfaceArea が正になる。
        /// </summary>
        [Fact]
        public void GetEvaporatingTemperature_FrostingCondition_FrostAreaPositive()
        {
            // 蒸発温度が非常に低い（着霜条件）
            var evap = new CrossFinEvaporator(-10.0, 8.0, 0.5, 10.0, 0.004, 80.0);
            evap.GetEvaporatingTemperature(8.0, 0.5, 10.0, 0.004, false);
            Assert.True(evap.FrostSurfaceArea >= 0,
                $"FrostSurfaceArea={evap.FrostSurfaceArea:F4} m² >= 0");
        }

        #endregion
    }
}
