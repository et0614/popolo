/* SimpleGroundHeatExchangerTests.cs
 *
 * Copyright (C) 2026 E.Togashi
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 3 of the License, or (at
 * your option) any later version.
 *
 * This program is distributed in the hope that it will be useful, but
 * WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
 * General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301, USA.
 */

using System;
using Xunit;
using Popolo.Core.HVAC.Storage;

namespace Popolo.Core.Tests.HVAC.Storage
{
    /// <summary>Unit tests for <see cref="SimpleGroundHeatExchanger"/>.</summary>
    /// <remarks>
    /// SimpleGroundHeatExchanger implements the 2-mass-point ground heat exchanger
    /// model from: Togashi et al., J. Environ. Eng. AIJ, Vol.83, No.747, 2018.
    ///   https://doi.org/10.3130/aije.83.491
    ///
    /// Model structure (Fig.6 in the paper):
    ///   Twi (fluid inlet) → ε·mc → Tcnt (near-field) → Kcnt → Tfar (far-field) → Kfar → TGC (constant layer)
    ///
    /// Sign convention:
    ///   HeatExchange &gt; 0 → heat extracted from ground to fluid (heating mode)
    ///   HeatExchange &lt; 0 → heat rejected from fluid to ground (cooling mode)
    ///
    /// Default parameters (paper Table 4, normalized by ε·mc):
    ///   Horizontal: Kcnt/εmc=0.217, Kfar/εmc=0.031, Ccnt/εmc=155.4e3, Cfar/εmc=840.4e3
    ///   Vertical:   Kcnt/εmc=0.373, Kfar/εmc=0.153, Ccnt/εmc=5.8e3,   Cfar/εmc=921.4e3
    /// </remarks>
    public class SimpleGroundHeatExchangerTests
    {
        #region ヘルパー

        private const double FlowRate = 0.02;   // 定格流量 [kg/s]
        private const double Cp       = 4.186;  // 比熱 [kJ/(kg·K)]
        private const double Eps      = 0.7;    // 有効度 [-]

        private static SimpleGroundHeatExchanger MakeVertical(double initTemp = 15.0)
        {
            var m = new SimpleGroundHeatExchanger(
                FlowRate, Cp, Eps, SimpleGroundHeatExchanger.Type.Vertical);
            m.InitTemperature(initTemp);
            m.TimeStep = 3600;
            return m;
        }

        private static SimpleGroundHeatExchanger MakeHorizontal(double initTemp = 15.0)
        {
            var m = new SimpleGroundHeatExchanger(
                FlowRate, Cp, Eps, SimpleGroundHeatExchanger.Type.Horizontal);
            m.InitTemperature(initTemp);
            m.TimeStep = 3600;
            return m;
        }

        #endregion

        // ================================================================
        #region 初期化

        /// <summary>InitTemperature で Tcnt・Tfar が指定温度になる。</summary>
        [Fact]
        public void InitTemperature_AllGroundTemperatures_SetToSpecifiedValue()
        {
            var m = MakeVertical(20.0);
            Assert.InRange(m.NearGroundTemperature,     19.99, 20.01);
            Assert.InRange(m.DistantGroundTemperature,  19.99, 20.01);
        }

        /// <summary>
        /// コンストラクタで設定されるデフォルトパラメータが論文 Table 4 と一致する。
        /// Vertical: Kcnt/εmc = 0.373。
        /// </summary>
        [Fact]
        public void Constructor_Vertical_ParametersMatchPaperTable4()
        {
            var m = MakeVertical();
            double emc = Eps * FlowRate * Cp;
            Assert.InRange(m.NearGroundHeatConductance / emc, 0.370, 0.376); // 0.373
            Assert.InRange(m.DistantGroundHeatConductance / emc, 0.150, 0.156); // 0.153
        }

        /// <summary>
        /// Horizontal のデフォルトパラメータが論文 Table 4 と一致する。
        /// Horizontal: Kcnt/εmc = 0.217。
        /// </summary>
        [Fact]
        public void Constructor_Horizontal_ParametersMatchPaperTable4()
        {
            var m = MakeHorizontal();
            double emc = Eps * FlowRate * Cp;
            Assert.InRange(m.NearGroundHeatConductance / emc, 0.214, 0.220); // 0.217
            Assert.InRange(m.DistantGroundHeatConductance / emc, 0.028, 0.034); // 0.031
        }

        #endregion

        // ================================================================
        #region Update — 採熱運転

        /// <summary>
        /// 流入水温 &lt; 地中温度のとき HeatExchange が正（採熱）。
        /// 出口水温が流入水温より高い。
        /// </summary>
        [Fact]
        public void Update_ColdFluidInlet_HeatExtractionIsPositive()
        {
            var m = MakeVertical(15.0);
            m.ConstantGroundTemperature = 15.0;
            m.Update(5.0, FlowRate); // 流入5°C, 地中15°C
            Assert.True(m.HeatExchange > 0,
                $"HeatExchange={m.HeatExchange:F4} kW should be positive (extraction)");
            Assert.True(m.FluidOutletTemperature > m.FluidInletTemperature,
                $"Outlet={m.FluidOutletTemperature:F3}°C > Inlet={m.FluidInletTemperature:F3}°C");
        }

        /// <summary>
        /// 流入水温 &gt; 地中温度のとき HeatExchange が負（放熱）。
        /// 出口水温が流入水温より低い。
        /// </summary>
        [Fact]
        public void Update_HotFluidInlet_HeatRejectionIsNegative()
        {
            var m = MakeVertical(15.0);
            m.ConstantGroundTemperature = 15.0;
            m.Update(30.0, FlowRate); // 流入30°C, 地中15°C
            Assert.True(m.HeatExchange < 0,
                $"HeatExchange={m.HeatExchange:F4} kW should be negative (rejection)");
            Assert.True(m.FluidOutletTemperature < m.FluidInletTemperature,
                $"Outlet={m.FluidOutletTemperature:F3}°C < Inlet={m.FluidInletTemperature:F3}°C");
        }

        /// <summary>
        /// 流入水温 = 地中温度のとき HeatExchange がゼロ（熱平衡）。
        /// </summary>
        [Fact]
        public void Update_InletEqualsGroundTemp_NoHeatExchange()
        {
            var m = MakeVertical(15.0);
            m.ConstantGroundTemperature = 15.0;
            m.Update(15.0, FlowRate);
            Assert.InRange(m.HeatExchange, -0.001, 0.001);
        }

        /// <summary>
        /// 流量ゼロのとき HeatExchange = 0 で出口水温 = 入口水温。
        /// </summary>
        [Fact]
        public void Update_ZeroFlowRate_NoHeatExchange()
        {
            var m = MakeVertical(15.0);
            m.Update(5.0, 0.0);
            Assert.Equal(0.0, m.HeatExchange);
            Assert.Equal(m.FluidInletTemperature, m.FluidOutletTemperature);
        }

        #endregion

        // ================================================================
        #region 連続運転による地中温度変化

        /// <summary>
        /// 採熱運転を連続すると地中温度（Tcnt）が低下する。
        /// </summary>
        [Fact]
        public void Update_ContinuousExtraction_CoolsNearGroundTemperature()
        {
            var m = MakeVertical(15.0);
            m.ConstantGroundTemperature = 15.0;
            double tBefore = m.NearGroundTemperature;
            for (int i = 0; i < 24; i++) m.Update(5.0, FlowRate);
            Assert.True(m.NearGroundTemperature < tBefore,
                $"Tcnt after={m.NearGroundTemperature:F3}°C < initial={tBefore:F3}°C");
        }

        /// <summary>
        /// 放熱運転を連続すると地中温度（Tcnt）が上昇する。
        /// </summary>
        [Fact]
        public void Update_ContinuousRejection_WarmsNearGroundTemperature()
        {
            var m = MakeVertical(15.0);
            m.ConstantGroundTemperature = 15.0;
            double tBefore = m.NearGroundTemperature;
            for (int i = 0; i < 24; i++) m.Update(30.0, FlowRate);
            Assert.True(m.NearGroundTemperature > tBefore,
                $"Tcnt after={m.NearGroundTemperature:F3}°C > initial={tBefore:F3}°C");
        }

        /// <summary>
        /// 採熱・放熱を交互に繰り返すと、遠方質点（Tfar）が長期的に安定する。
        /// NearGroundTemperature (Tcnt) は熱容量が小さく直近の流入水温に強く
        /// 引かれるため、最後の運転モードに応じた値になる。
        /// 一方 DistantGroundTemperature (Tfar) は熱容量が大きく長期変動を
        /// 表すため、不易層温度付近に留まる。
        /// </summary>
        [Fact]
        public void Update_AlternatingOperation_FarFieldTemperatureStabilizes()
        {
            var m = MakeVertical(15.0);
            m.ConstantGroundTemperature = 15.0;

            // 採熱12h + 放熱12h を10サイクル（対称なので Tfar は不易層温度付近に安定）
            for (int cycle = 0; cycle < 10; cycle++)
            {
                for (int i = 0; i < 12; i++) m.Update(5.0, FlowRate);
                for (int i = 0; i < 12; i++) m.Update(25.0, FlowRate);
            }
            // Tfar は長期変動を表す質点 → 不易層温度（15°C）付近に安定
            Assert.InRange(m.DistantGroundTemperature, 13.0, 17.0);
        }

        #endregion

        // ================================================================
        #region ForecastState / FixState

        /// <summary>
        /// ForecastState 後に FixState を呼ばないと、
        /// 再度 ForecastState を呼んでも直前の確定値から計算される。
        /// </summary>
        [Fact]
        public void ForecastState_WithoutFix_UsesCommittedState()
        {
            var m = MakeVertical(15.0);
            m.ConstantGroundTemperature = 15.0;

            m.ForecastState(5.0, FlowRate);
            double tcnt1 = m.NearGroundTemperature;

            // FixState を呼ばずに再度 ForecastState → 同じ初期値から計算
            m.ForecastState(5.0, FlowRate);
            double tcnt2 = m.NearGroundTemperature;

            Assert.InRange(tcnt2 - tcnt1, -0.001, 0.001);
        }

        /// <summary>
        /// ForecastState → FixState → ForecastState の順に呼ぶと
        /// 2回目は1回目の結果を初期値として計算される（地中温度が連続して変化）。
        /// </summary>
        [Fact]
        public void ForecastState_AfterFix_UsesUpdatedState()
        {
            var m = MakeVertical(15.0);
            m.ConstantGroundTemperature = 15.0;

            m.ForecastState(5.0, FlowRate);
            double tcnt1 = m.NearGroundTemperature;
            m.FixState();

            m.ForecastState(5.0, FlowRate);
            double tcnt2 = m.NearGroundTemperature;

            // 2ステップ目の方が採熱が進んで地中温度がさらに低い
            Assert.True(tcnt2 < tcnt1,
                $"Step2 Tcnt={tcnt2:F4}°C < Step1 Tcnt={tcnt1:F4}°C");
        }

        #endregion

        // ================================================================
        #region 垂直型 vs 水平型

        /// <summary>
        /// 垂直型は Kcnt が大きく Ccnt が小さいため、
        /// 採熱時の地中温度低下が水平型より速い（短時間変動に敏感）。
        /// </summary>
        [Fact]
        public void VerticalVsHorizontal_VerticalCoolsFasterWithExtraction()
        {
            var vert = MakeVertical(15.0);
            var horiz = MakeHorizontal(15.0);
            vert.ConstantGroundTemperature  = 15.0;
            horiz.ConstantGroundTemperature = 15.0;

            // 1ステップの採熱でTcntの低下量を比較
            vert.Update(5.0, FlowRate);
            horiz.Update(5.0, FlowRate);

            // 垂直型は Ccnt が小さいので Tcnt の低下が大きい
            double dropVert  = 15.0 - vert.NearGroundTemperature;
            double dropHoriz = 15.0 - horiz.NearGroundTemperature;
            Assert.True(dropVert > dropHoriz,
                $"Vertical drop={dropVert:F4}K > Horizontal drop={dropHoriz:F4}K");
        }

        #endregion
    }
}
