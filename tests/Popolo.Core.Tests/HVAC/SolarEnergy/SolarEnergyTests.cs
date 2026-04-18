/* SolarEnergyTests.cs
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
using Popolo.Core.Climate;
using Popolo.Core.HVAC.SolarEnergy;

namespace Popolo.Core.Tests.HVAC.SolarEnergy
{
  /// <summary>Unit tests for <see cref="FlatPlateSolarCollector"/> and <see cref="SimpleSolarCollector"/>.</summary>
  /// <remarks>
  /// FlatPlateSolarCollector parameters (base case from FlatPlateSolarCollectorTest):
  ///   skyTemp=0°C, airTemp=0°C, DNI=500 W/m², diffuse=0,
  ///   waterFlowRate=0.01 kg/s, airGap=0.01 m,
  ///   glassEmissivity=0.1, panelEmissivity=0.1, windSpeed=0 m/s,
  ///   insulatorThickness=0.02 m, insulatorConductivity=0.05 W/(m·K),
  ///   area=2.0 m², tubePitch=0.02 m, innerDiameter=0.013 m, outerDiameter=0.015 m,
  ///   panelThickness=0.001 m, panelConductivity=20 W/(m·K),
  ///   cosTheta=1.0, transmittance=0.90, reflectance=0.08, absorptance=0.93
  ///
  /// SimpleSolarCollector characteristic equation: η = A·(ΔT/G) + B
  ///   FlatPlate:   A = -4.14 W/K, B = 0.80
  ///   VacuumTube:  A = -1.75 W/K, B = 0.60
  /// </remarks>
  public class SolarEnergyTests
  {
    #region 定数・ヘルパー

    /// <summary>
    /// 南向き 30° 傾斜面（サンプルコードの SimpleSolarCollectorTest と同条件）。
    /// </summary>
    private static readonly Incline SouthFacing30 =
        new Incline(Incline.Orientation.S, 30.0 / 180.0 * Math.PI);

    // FlatPlateSolarCollector 基準パラメータ（サンプルコードと同値）
    private const double Sky = 0.0;
    private const double Air = 0.0;
    private const double Dni = 500.0;
    private const double Diff = 0.0;
    private const double Wf = 0.01;
    private const double Gap = 0.01;
    private const double GlEm = 0.1;
    private const double PnEm = 0.1;
    private const double Wind = 0.0;
    private const double InsTh = 0.02;
    private const double InsCo = 0.05;
    private const double Area = 2.0;
    private const double Pitch = 0.02;
    private const double Di = 0.013;
    private const double Do = 0.015;
    private const double PnTh = 0.001;
    private const double PnCo = 20.0;
    private const double CosT = 1.0;
    private const double Trans = 0.90;
    private const double Refl = 0.08;
    private const double Abs = 0.93;

    /// <summary>FlatPlateSolarCollector の GetHeatTransfer をデフォルトパラメータで呼ぶ。</summary>
    private static double CallGetHeatTransfer(
        double inletTemp, double windSpeed,
        out double panelTemp, out double glassTemp,
        out double outletTemp, out double meanTemp,
        out double efficiency)
    {
      return FlatPlateSolarCollector.GetHeatTransfer(
          Sky, Air, Dni, Diff, Wf, inletTemp, Gap, GlEm, PnEm,
          windSpeed, InsTh, InsCo, Area, Pitch, Di, Do, PnTh, PnCo,
          CosT, Trans, Refl, Abs,
          out panelTemp, out glassTemp, out outletTemp, out meanTemp, out efficiency);
    }

    /// <summary>out 引数を破棄して呼ぶ簡略版。</summary>
    private static double CallGetHeatTransfer(double inletTemp, double windSpeed = Wind)
    {
      double pt, gt, ot, mt, eta;
      return CallGetHeatTransfer(inletTemp, windSpeed, out pt, out gt, out ot, out mt, out eta);
    }

    #endregion

    // ================================================================
    #region FlatPlateSolarCollector

    /// <summary>日射あり・低入口水温で集熱量が正になる。</summary>
    [Fact]
    public void FlatPlate_PositiveIrradiance_HeatTransferIsPositive()
    {
      double ht = CallGetHeatTransfer(20);
      Assert.True(ht > 0, $"Heat transfer = {ht:F2} W should be positive");
    }

    /// <summary>日射なしでは集熱量がゼロ以下になる。</summary>
    [Fact]
    public void FlatPlate_ZeroIrradiance_HeatTransferNotPositive()
    {
      double pt, gt, wt, mt, eta;
      double ht = FlatPlateSolarCollector.GetHeatTransfer(
          Sky, Air, 0, 0, Wf, 20, Gap, GlEm, PnEm,
          Wind, InsTh, InsCo, Area, Pitch, Di, Do, PnTh, PnCo,
          CosT, Trans, Refl, Abs,
          out pt, out gt, out wt, out mt, out eta);
      Assert.True(ht <= 0, $"Zero irradiance: heat transfer = {ht:F2} W should be ≤ 0");
    }

    /// <summary>出口水温 > 入口水温（日射あり・低水温）。</summary>
    [Fact]
    public void FlatPlate_Heated_OutletTemperatureHigherThanInlet()
    {
      double inlet = 20;
      double outlet;
      CallGetHeatTransfer(inlet, Wind,
          out _, out _, out outlet, out _, out _);
      Assert.True(outlet > inlet,
          $"Outlet={outlet:F2}°C > Inlet={inlet}°C");
    }

    /// <summary>入口水温が高いほど集熱効率が低下する（熱損失増大）。</summary>
    [Fact]
    public void FlatPlate_HigherInletTemp_LowerEfficiency()
    {
      double eta25, eta70;
      CallGetHeatTransfer(25, Wind, out _, out _, out _, out _, out eta25);
      CallGetHeatTransfer(70, Wind, out _, out _, out _, out _, out eta70);
      Assert.True(eta25 > eta70,
          $"η(25°C)={eta25:F3} > η(70°C)={eta70:F3}");
    }

    /// <summary>風速が高いほど集熱効率が低下する（対流熱損失増大）。</summary>
    [Fact]
    public void FlatPlate_HigherWindSpeed_LowerEfficiency()
    {
      double eta0, eta10;
      CallGetHeatTransfer(40, 0, out _, out _, out _, out _, out eta0);
      CallGetHeatTransfer(40, 10, out _, out _, out _, out _, out eta10);
      Assert.True(eta0 > eta10,
          $"η(wind=0)={eta0:F3} > η(wind=10m/s)={eta10:F3}");
    }

    /// <summary>水量が多いほど出口水温は入口水温に近い（温度上昇が小さい）。</summary>
    [Fact]
    public void FlatPlate_HigherFlowRate_SmallerTemperatureRise()
    {
      double pt, gt, outletLow, outletHigh, mt, eta;
      double inlet = 20;
      // 少流量
      FlatPlateSolarCollector.GetHeatTransfer(
          Sky, Air, Dni, Diff, 0.005, inlet, Gap, GlEm, PnEm,
          Wind, InsTh, InsCo, Area, Pitch, Di, Do, PnTh, PnCo,
          CosT, Trans, Refl, Abs,
          out pt, out gt, out outletLow, out mt, out eta);
      // 多流量
      FlatPlateSolarCollector.GetHeatTransfer(
          Sky, Air, Dni, Diff, 0.05, inlet, Gap, GlEm, PnEm,
          Wind, InsTh, InsCo, Area, Pitch, Di, Do, PnTh, PnCo,
          CosT, Trans, Refl, Abs,
          out pt, out gt, out outletHigh, out mt, out eta);
      double riseHigh = outletHigh - inlet;
      double riseLow = outletLow - inlet;
      Assert.True(riseHigh < riseLow,
          $"High flow rise={riseHigh:F2}K  <  Low flow rise={riseLow:F2}K");
    }

    /// <summary>パネル温度はガラス温度より高い（熱流の方向）。</summary>
    [Fact]
    public void FlatPlate_PanelTemperatureHigherThanGlass()
    {
      double panelT, glassT, outletT, meanT, eta;
      CallGetHeatTransfer(20, Wind,
          out panelT, out glassT, out outletT, out meanT, out eta);
      Assert.True(panelT > glassT,
          $"Panel={panelT:F2}°C > Glass={glassT:F2}°C");
    }

    /// <summary>集熱効率は 0〜1 の範囲にある。</summary>
    [Theory]
    [InlineData(10)]
    [InlineData(30)]
    [InlineData(50)]
    public void FlatPlate_Efficiency_InValidRange(double inletTemp)
    {
      double eta;
      CallGetHeatTransfer(inletTemp, Wind, out _, out _, out _, out _, out eta);
      Assert.InRange(eta, 0.0, 1.0);
    }

    #endregion

    // ================================================================
    #region SimpleSolarCollector — GetWaterFlowRate

    /// <summary>日射あり・低水温では水量が正になる（平板型）。</summary>
    [Fact]
    public void SimpleCollector_FlatPlate_PositiveIrradiance_FlowRateIsPositive()
    {
      var sc = new SimpleSolarCollector(SouthFacing30, 1.0, SimpleSolarCollector.HeatReceiver.FlatPlate);
      double wf = sc.GetWaterFlowRate(500, 25, 3, 20);
      Assert.True(wf > 0, $"Water flow rate = {wf:F5} kg/s should be positive");
    }

    /// <summary>日射ゼロでは水量がゼロになる。</summary>
    [Fact]
    public void SimpleCollector_ZeroIrradiance_FlowRateIsZero()
    {
      var sc = new SimpleSolarCollector(SouthFacing30, 1.0, SimpleSolarCollector.HeatReceiver.FlatPlate);
      double wf = sc.GetWaterFlowRate(0, 25, 3, 20);
      Assert.Equal(0.0, wf);
    }

    /// <summary>温度差がゼロ以下では水量がゼロになる。</summary>
    [Fact]
    public void SimpleCollector_NegativeTemperatureDifference_FlowRateIsZero()
    {
      var sc = new SimpleSolarCollector(SouthFacing30, 1.0, SimpleSolarCollector.HeatReceiver.FlatPlate);
      double wf = sc.GetWaterFlowRate(500, 25, -1, 20);
      Assert.Equal(0.0, wf);
    }

    /// <summary>
    /// 平板型の集熱効率は特性式 η = B + A·(ΔT/G) に一致する。
    /// 入口25°C、外気20°C、G=500 W/m²、温度差3K のとき
    /// η = 0.8 + (-4.14)×(26.5-20)/500 ≈ 0.746。
    /// </summary>
    [Fact]
    public void SimpleCollector_FlatPlate_Efficiency_MatchesCharacteristicEquation()
    {
      var sc = new SimpleSolarCollector(SouthFacing30, 1.0, SimpleSolarCollector.HeatReceiver.FlatPlate);
      double G = 500; double Tin = 25; double dT = 3; double Tamb = 20;
      sc.GetWaterFlowRate(G, Tin, dT, Tamb);
      double Tm = Tin + dT / 2.0;
      double expected = Math.Max(0, -4.14 * ((Tm - Tamb) / G) + 0.8);
      Assert.InRange(sc.Efficiency, expected - 0.001, expected + 0.001);
    }

    /// <summary>高温域（70°C）では真空管型の効率が平板型より高い。</summary>
    [Fact]
    public void SimpleCollector_VacuumTube_HigherEfficiency_AtHighTemperature()
    {
      var flat = new SimpleSolarCollector(SouthFacing30, 1.0, SimpleSolarCollector.HeatReceiver.FlatPlate);
      var vac = new SimpleSolarCollector(SouthFacing30, 1.0, SimpleSolarCollector.HeatReceiver.VacuumTube);
      double G = 500; double Tin = 70; double dT = 3; double Tamb = 20;
      flat.GetWaterFlowRate(G, Tin, dT, Tamb);
      vac.GetWaterFlowRate(G, Tin, dT, Tamb);
      Assert.True(vac.Efficiency > flat.Efficiency,
          $"VacuumTube η={vac.Efficiency:F3} > FlatPlate η={flat.Efficiency:F3} at {Tin}°C");
    }

    /// <summary>入口水温が高いほど効率が低下する（熱損失増大）。</summary>
    [Fact]
    public void SimpleCollector_HigherInletTemp_LowerEfficiency()
    {
      var sc = new SimpleSolarCollector(SouthFacing30, 1.0, SimpleSolarCollector.HeatReceiver.FlatPlate);
      sc.GetWaterFlowRate(500, 25, 3, 20);
      double eta25 = sc.Efficiency;
      sc.GetWaterFlowRate(500, 60, 3, 20);
      double eta60 = sc.Efficiency;
      Assert.True(eta25 > eta60,
          $"η(25°C)={eta25:F3} > η(60°C)={eta60:F3}");
    }

    /// <summary>集熱量 HeatCollection は出入口温度差・水量・比熱の積と一致する。</summary>
    [Fact]
    public void SimpleCollector_HeatCollection_ConsistentWithFlowAndTemperature()
    {
      var sc = new SimpleSolarCollector(SouthFacing30, 1.0, SimpleSolarCollector.HeatReceiver.FlatPlate);
      sc.GetWaterFlowRate(500, 25, 3, 20);
      // HeatCollection = (Tout-Tin) * wf * 0.001 * cp
      // HeatCollection uses PhysicsConstants.NominalWaterIsobaricSpecificHeat = 4182.0 J/(kg·K)
      double expected = (sc.OutletWaterTemperature - sc.InletWaterTemperature)
                        * sc.WaterFlowRate * 0.001 * Popolo.Core.Physics.PhysicsConstants.NominalWaterIsobaricSpecificHeat;
      Assert.InRange(sc.HeatCollection, expected - 1e-6, expected + 1e-6);
    }

    #endregion

    // ================================================================
    #region SimpleSolarCollector — GetOutletTemperature

    /// <summary>日射あり・正流量では出口水温が入口水温より高い。</summary>
    [Fact]
    public void SimpleCollector_GetOutletTemperature_OutletHigherThanInlet()
    {
      var sc = new SimpleSolarCollector(SouthFacing30, 1.0, SimpleSolarCollector.HeatReceiver.FlatPlate);
      double inlet = 25;
      double outlet = sc.GetOutletTemperature(500, inlet, 0.01, 20);
      Assert.True(outlet > inlet,
          $"Outlet={outlet:F2}°C > Inlet={inlet}°C");
    }

    /// <summary>流量ゼロでは出口温度がゼロを返す（停止判定）。</summary>
    [Fact]
    public void SimpleCollector_GetOutletTemperature_ZeroFlowRate_ReturnsZero()
    {
      var sc = new SimpleSolarCollector(SouthFacing30, 1.0, SimpleSolarCollector.HeatReceiver.FlatPlate);
      double outlet = sc.GetOutletTemperature(500, 25, 0, 20);
      Assert.Equal(0.0, outlet);
    }

    /// <summary>流量が多いほど出口水温が入口水温に近い（温度上昇が小さい）。</summary>
    [Fact]
    public void SimpleCollector_GetOutletTemperature_HigherFlowRate_SmallerTemperatureRise()
    {
      var sc = new SimpleSolarCollector(SouthFacing30, 1.0, SimpleSolarCollector.HeatReceiver.FlatPlate);
      double inlet = 25;
      double outlet_low = sc.GetOutletTemperature(500, inlet, 0.005, 20);
      double outlet_high = sc.GetOutletTemperature(500, inlet, 0.05, 20);
      Assert.True(outlet_high - inlet < outlet_low - inlet,
          $"High flow rise={outlet_high - inlet:F3}K < Low flow rise={outlet_low - inlet:F3}K");
    }

    #endregion
  }
}