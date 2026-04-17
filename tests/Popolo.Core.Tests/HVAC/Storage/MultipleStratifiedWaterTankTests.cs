/* MultipleStratifiedWaterTankTests.cs
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
  /// <summary>Unit tests for <see cref="MultipleStratifiedWaterTank"/>.</summary>
  /// <remarks>
  /// MultipleStratifiedWaterTank models a thermally stratified tank divided into
  /// horizontal layers. Buoyancy-driven mixing (inversion correction) prevents
  /// density inversions: hot water stays at the top, cold at the bottom.
  ///
  /// Constructor:
  ///   (waterDepth [m], sectionalArea [m²], pipeDiameter [m],
  ///    pipeInstallationHeight [m] (upper port), layerNumber)
  ///
  /// ForecastState(inletTemp, flowRate, isDownFlow):
  ///   isDownFlow=true  → water enters from the top, exits from the bottom
  ///   isDownFlow=false → water enters from the bottom, exits from the top
  ///
  /// Layers are indexed 0 (bottom) to LayerNumber-1 (top).
  /// UpperOutletTemperarture = temperature of the topmost layer.
  /// LowerOutletTemperarture = temperature of the bottommost layer.
  /// </remarks>
  public class MultipleStratifiedWaterTankTests
  {
    #region ヘルパー

    /// <summary>
    /// 標準的な成層型蓄熱槽を生成する。
    ///   水深 2 m, 断面積 1 m², 管径 0.05 m, 上部口設置高さ 1.8 m, 10 層
    /// </summary>
    private static MultipleStratifiedWaterTank MakeTank(double initTemp = 20.0)
    {
      var tank = new MultipleStratifiedWaterTank(2.0, 1.0, 0.05, 1.8, 10);
      tank.InitializeTemperature(initTemp);
      return tank;
    }

    #endregion

    // ================================================================
    #region 初期化・プロパティ

    /// <summary>LayerNumber がコンストラクタ指定値と一致する。</summary>
    [Fact]
    public void Constructor_LayerNumber_MatchesInput()
    {
      var tank = MakeTank();
      Assert.Equal(10, tank.LayerCount);
    }

    /// <summary>WaterVolume = 水深 × 断面積。</summary>
    [Fact]
    public void Constructor_WaterVolume_EqualsDepthTimesSectionalArea()
    {
      var tank = MakeTank();
      Assert.InRange(tank.WaterVolume, 1.99, 2.01); // 2.0 m × 1.0 m² = 2.0 m³
    }

    /// <summary>InitializeTemperature で全層が指定温度になる。</summary>
    [Fact]
    public void InitializeTemperature_AllLayers_SetToSpecifiedValue()
    {
      var tank = MakeTank();
      tank.InitializeTemperature(15.0);
      for (int i = 0; i < tank.LayerCount; i++)
        Assert.InRange(tank.GetTemperature(i), 14.99, 15.01);
    }

    /// <summary>PipeInstallationLayer が水深に対応した層番号になっている。</summary>
    [Fact]
    public void Constructor_PipeInstallationLayer_WithinValidRange()
    {
      var tank = MakeTank();
      Assert.InRange(tank.PipeInstallationLayer, 0, tank.LayerCount - 1);
    }

    #endregion

    // ================================================================
    #region ForecastState — 流入温度の影響

    /// <summary>
    /// 下向き流（isDownFlow=true）で冷水流入すると上層から冷却される。
    /// 上部出口温度 &lt; 初期温度。
    /// </summary>
    [Fact]
    public void ForecastState_DownFlow_ColdInlet_CoolsUpperLayers()
    {
      var tank = MakeTank(40.0); // 初期40°C
      tank.TimeStep = 3600;
      tank.ForecastState(5.0, 0.001, isDownFlow: true);
      // 上部から冷水が入るため上部出口は初期より低温
      Assert.True(tank.UpperOutletTemperarture < 40.0,
          $"Upper outlet={tank.UpperOutletTemperarture:F2}°C should be < 40°C");
    }

    /// <summary>
    /// 上向き流（isDownFlow=false）で温水流入すると下層から加熱される。
    /// 下部出口温度 &gt; 初期温度。
    /// </summary>
    [Fact]
    public void ForecastState_UpFlow_HotInlet_HeatsLowerLayers()
    {
      var tank = MakeTank(20.0); // 初期20°C
      tank.TimeStep = 3600;
      tank.ForecastState(60.0, 0.001, isDownFlow: false);
      Assert.True(tank.LowerOutletTemperarture > 20.0,
          $"Lower outlet={tank.LowerOutletTemperarture:F2}°C should be > 20°C");
    }

    /// <summary>
    /// 流入水温と同じ初期温度では全層温度がほぼ変化しない。
    /// </summary>
    [Fact]
    public void ForecastState_InletEqualToInitialTemp_NoSignificantChange()
    {
      var tank = MakeTank(20.0);
      tank.TimeStep = 3600;
      tank.ForecastState(20.0, 0.01, isDownFlow: true);
      for (int i = 0; i < tank.LayerCount; i++)
        Assert.InRange(tank.GetTemperature(i), 19.5, 20.5);
    }

    /// <summary>
    /// 流量が多いほど出口温度が流入水温に近づく。
    /// </summary>
    [Fact]
    public void ForecastState_HigherFlowRate_OutletCloserToInlet()
    {
      double Tin = 5.0;

      var tankLow = MakeTank(40.0);
      tankLow.TimeStep = 3600;
      tankLow.ForecastState(Tin, 0.0005, isDownFlow: true);
      double outLow = tankLow.UpperOutletTemperarture;

      var tankHigh = MakeTank(40.0);
      tankHigh.TimeStep = 3600;
      tankHigh.ForecastState(Tin, 0.005, isDownFlow: true);
      double outHigh = tankHigh.UpperOutletTemperarture;

      Assert.True(Math.Abs(outHigh - Tin) < Math.Abs(outLow - Tin),
          $"High flow outlet={outHigh:F2}°C closer to {Tin}°C than low={outLow:F2}°C");
    }

    #endregion

    // ================================================================
    #region 温度成層性

    /// <summary>
    /// 上部が高温・下部が冷水で初期化した後、ForecastState を行わなければ
    /// 成層が維持される（浮力補正が発動しない）。
    /// </summary>
    [Fact]
    public void Stratification_HotTopColdBottom_MaintainedWithoutFlow()
    {
      var tank = new MultipleStratifiedWaterTank(2.0, 1.0, 0.05, 1.8, 4);
      // 下から順に 10, 15, 25, 40°C
      double[] temps = { 10, 15, 25, 40 };
      for (int i = 0; i < 4; i++) tank.InitializeTemperature(i, temps[i]);

      // 各層温度が設定値と一致することを確認（浮力補正は呼ばれていない）
      for (int i = 0; i < 4; i++)
        Assert.InRange(tank.GetTemperature(i), temps[i] - 0.01, temps[i] + 0.01);
    }

    /// <summary>
    /// 浮力補正は流入水温と槽内温度の逆転条件で発動する。
    /// 上向き流（isDownFlow=false）で高温水が流入し、かつ
    /// 上部（最終層）温度 ≤ 流入水温 の条件を満たすとき混合が起こる。
    /// 混合後は流入口付近の層温度が流入水温に近づく。
    /// </summary>
    [Fact]
    public void Stratification_UpFlow_HotInletAboveTopTemp_TriggersMixing()
    {
      // 初期：全層10°C（冷タンク）
      var tank = new MultipleStratifiedWaterTank(2.0, 1.0, 0.05, 1.8, 4);
      tank.InitializeTemperature(10.0);
      tank.TimeStep = 3600;

      // 上向き流で60°C流入 → 上部(最終層)10°C ≤ 60°C → 逆転補正発動
      tank.ForecastState(60.0, 0.001, isDownFlow: false);

      // 補正後：底層（流入口側）が加熱されている
      Assert.True(tank.LowerOutletTemperarture > 10.0,
          $"Lower outlet={tank.LowerOutletTemperarture:F2}°C > 10°C (heated by hot inlet)");
    }

    #endregion

    // ================================================================
    #region 蓄熱量・蓄熱流

    /// <summary>基準温度と同じ均一温度では蓄熱量がゼロ。</summary>
    [Fact]
    public void GetHeatStorage_UniformAtReferenceTemp_IsZero()
    {
      var tank = MakeTank(20.0);
      Assert.InRange(tank.GetHeatStorage(20.0), -0.01, 0.01);
    }

    /// <summary>全層が基準温度より高いと蓄熱量が正（温水蓄熱）。</summary>
    [Fact]
    public void GetHeatStorage_HotTank_IsPositive()
    {
      var tank = MakeTank(40.0);
      Assert.True(tank.GetHeatStorage(20.0) > 0);
    }

    /// <summary>全層が基準温度より低いと蓄熱量が負（冷水蓄熱）。</summary>
    [Fact]
    public void GetHeatStorage_ColdTank_IsNegative()
    {
      var tank = MakeTank(5.0);
      Assert.True(tank.GetHeatStorage(20.0) < 0);
    }

    /// <summary>冷水流入（下向き）では蓄熱流が負（放熱）。</summary>
    [Fact]
    public void GetHeatStorageFlow_ColdDownFlowInlet_IsNegative()
    {
      var tank = MakeTank(40.0);
      tank.TimeStep = 3600;
      tank.ForecastState(5.0, 0.001, isDownFlow: true);
      Assert.True(tank.GetHeatStorageFlow() < 0,
          $"Cold inlet: storage flow={tank.GetHeatStorageFlow():F3} kW should be negative");
    }

    /// <summary>温水流入（上向き）では蓄熱流が正（蓄熱）。</summary>
    [Fact]
    public void GetHeatStorageFlow_HotUpFlowInlet_IsPositive()
    {
      var tank = MakeTank(10.0);
      tank.TimeStep = 3600;
      tank.ForecastState(60.0, 0.001, isDownFlow: false);
      Assert.True(tank.GetHeatStorageFlow() > 0,
          $"Hot inlet: storage flow={tank.GetHeatStorageFlow():F3} kW should be positive");
    }

    #endregion

    // ================================================================
    #region GetTemperatures

    /// <summary>GetTemperatures で全層温度を配列にコピーできる。</summary>
    [Fact]
    public void GetTemperatures_CopiesAllValues()
    {
      var tank = new MultipleStratifiedWaterTank(2.0, 1.0, 0.05, 1.8, 4);
      double[] init = { 10, 15, 25, 40 };
      for (int i = 0; i < 4; i++) tank.InitializeTemperature(i, init[i]);

      double[] arr = new double[4];
      tank.GetTemperatures(ref arr);
      for (int i = 0; i < 4; i++)
        Assert.InRange(arr[i], init[i] - 0.01, init[i] + 0.01);
    }

    #endregion

    // ================================================================
    #region 熱損失

    /// <summary>熱損失係数を正に設定すると周囲温度に向かって収束する。</summary>
    [Fact]
    public void HeatLoss_WithPositiveCoefficient_TankConvergesToAmbient()
    {
      var tank = MakeTank(40.0);
      tank.AmbientTemperature = 20.0;
      tank.HeatLossCoefficient = 0.5;
      tank.TimeStep = 3600;
      tank.ForecastState(40.0, 0.0, isDownFlow: false); // 流量ゼロ
                                                        // 熱損失で温度は下がるが外気温度以下にはならない
      for (int i = 0; i < tank.LayerCount; i++)
      {
        Assert.True(tank.GetTemperature(i) < 40.0,
            $"Layer {i}: T={tank.GetTemperature(i):F2}°C should be < 40°C");
        Assert.True(tank.GetTemperature(i) >= 20.0,
            $"Layer {i}: T={tank.GetTemperature(i):F2}°C should be >= 20°C");
      }
    }

    #endregion
  }
}