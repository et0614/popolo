/* MultiConnectedWaterTankTests.cs
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
using Popolo.Core.Exceptions;
using Popolo.Core.HVAC.Storage;

namespace Popolo.Core.Tests.HVAC.Storage
{
  /// <summary>Unit tests for <see cref="MultiConnectedWaterTank"/>.</summary>
  /// <remarks>
  /// MultiConnectedWaterTank models n fully-mixed tanks connected in series.
  /// The governing equation per tank i (forward flow):
  ///   T_i(t+dt) = [T_i(t) + s·T_(i-1) + r·Tamb] / (1 + s + r)
  ///   where s = wf·dt/V,  r = kL/V·dt/(cp·rho)
  ///
  /// ForecastState advances the state and allows RestoreState to roll back.
  /// FixState commits the forecast permanently.
  /// </remarks>
  public class MultiConnectedWaterTankTests
  {
    #region ヘルパー

    /// <summary>
    /// 均一容量の n タンクを 20°C で初期化して返す。
    /// </summary>
    private static MultiConnectedWaterTank MakeTank(int n, double volumePerTank = 1.0)
    {
      double[] vols = new double[n];
      for (int i = 0; i < n; i++) vols[i] = volumePerTank;
      var tank = new MultiConnectedWaterTank(vols);
      tank.InitializeTemperature(20.0);
      return tank;
    }

    #endregion

    // ================================================================
    #region 初期化

    /// <summary>構築直後のタンク数が指定値と一致する。</summary>
    [Fact]
    public void Constructor_TankNumber_MatchesInput()
    {
      var tank = MakeTank(4);
      Assert.Equal(4, tank.TankCount);
    }

    /// <summary>InitializeTemperature(double) で全タンクが指定温度になる。</summary>
    [Fact]
    public void InitializeTemperature_AllTanks_SetToSpecifiedValue()
    {
      var tank = MakeTank(3);
      tank.InitializeTemperature(15.0);
      for (int i = 0; i < tank.TankCount; i++)
        Assert.InRange(tank.GetTemperature(i), 14.99, 15.01);
    }

    /// <summary>InitializeTemperature(int, double) で指定タンクのみ変化する。</summary>
    [Fact]
    public void InitializeTemperature_SingleTank_OnlyThatTankChanges()
    {
      var tank = MakeTank(3);
      tank.InitializeTemperature(1, 5.0);
      Assert.InRange(tank.GetTemperature(0), 19.99, 20.01);
      Assert.InRange(tank.GetTemperature(1), 4.99, 5.01);
      Assert.InRange(tank.GetTemperature(2), 19.99, 20.01);
    }

    /// <summary>FirstTankTemperature と LastTankTemperature が初期値と一致する。</summary>
    [Fact]
    public void FirstAndLastTankTemperature_AfterInit_MatchInitialValue()
    {
      var tank = MakeTank(3);
      Assert.InRange(tank.FirstTankTemperature, 19.99, 20.01);
      Assert.InRange(tank.LastTankTemperature, 19.99, 20.01);
    }

    #endregion

    // ================================================================
    #region ForecastState（順流）

    /// <summary>
    /// 冷水を流入させると順流で最初のタンクが最も冷える。
    /// </summary>
    [Fact]
    public void ForecastState_ForwardFlow_FirstTankCoolestWhenColdInlet()
    {
      var tank = MakeTank(3);
      tank.TimeStep = 60;
      tank.ForecastState(5.0, 0.01, isForwardFlow: true);
      Assert.True(tank.GetTemperature(0) < tank.GetTemperature(1),
          $"T[0]={tank.GetTemperature(0):F3} < T[1]={tank.GetTemperature(1):F3}");
      Assert.True(tank.GetTemperature(1) < tank.GetTemperature(2),
          $"T[1]={tank.GetTemperature(1):F3} < T[2]={tank.GetTemperature(2):F3}");
    }

    /// <summary>
    /// 順流では WaterOutletTemperature = 末端タンク温度。
    /// </summary>
    [Fact]
    public void ForecastState_ForwardFlow_OutletIsLastTank()
    {
      var tank = MakeTank(3);
      tank.TimeStep = 60;
      tank.ForecastState(5.0, 0.01, isForwardFlow: true);
      Assert.Equal(tank.LastTankTemperature, tank.WaterOutletTemperarture);
    }

    /// <summary>
    /// 逆流では WaterOutletTemperature = 先頭タンク温度。
    /// </summary>
    [Fact]
    public void ForecastState_ReverseFlow_OutletIsFirstTank()
    {
      var tank = MakeTank(3);
      tank.TimeStep = 60;
      tank.ForecastState(5.0, 0.01, isForwardFlow: false);
      Assert.Equal(tank.FirstTankTemperature, tank.WaterOutletTemperarture);
    }

    /// <summary>
    /// 流入水温が均衡温度（初期値と同じ）なら全タンク温度が変化しない。
    /// </summary>
    [Fact]
    public void ForecastState_InletEqualToTankTemp_TemperatureUnchanged()
    {
      var tank = MakeTank(3);
      tank.TimeStep = 60;
      tank.ForecastState(20.0, 0.01, isForwardFlow: true);
      for (int i = 0; i < tank.TankCount; i++)
        Assert.InRange(tank.GetTemperature(i), 19.99, 20.01);
    }

    /// <summary>
    /// 流量が多いほど WaterOutletTemperature が流入水温に近づく。
    /// </summary>
    [Fact]
    public void ForecastState_HigherFlowRate_OutletCloserToInlet()
    {
      double Tin = 5.0;

      var tankLow = MakeTank(3);
      tankLow.TimeStep = 60;
      tankLow.ForecastState(Tin, 0.001, true);
      double outLow = tankLow.WaterOutletTemperarture;

      var tankHigh = MakeTank(3);
      tankHigh.TimeStep = 60;
      tankHigh.ForecastState(Tin, 0.05, true);
      double outHigh = tankHigh.WaterOutletTemperarture;

      // 流量が多いほど出口水温は流入水温（5°C）に近い
      Assert.True(Math.Abs(outHigh - Tin) < Math.Abs(outLow - Tin),
          $"High flow outlet={outHigh:F3}°C closer to {Tin}°C than low flow outlet={outLow:F3}°C");
    }

    #endregion

    // ================================================================
    #region RestoreState / FixState

    /// <summary>RestoreState で ForecastState 前の温度に戻る。</summary>
    [Fact]
    public void RestoreState_AfterForecast_RestoredToPriorTemperature()
    {
      var tank = MakeTank(3);
      tank.TimeStep = 60;
      // 初期温度を記録
      double t0_before = tank.GetTemperature(0);

      tank.ForecastState(5.0, 0.01, true);
      Assert.True(tank.GetTemperature(0) < t0_before); // 変化確認

      tank.RestoreState();
      Assert.InRange(tank.GetTemperature(0), t0_before - 0.01, t0_before + 0.01);
    }

    /// <summary>FixState 後は RestoreState しても温度が戻らない。</summary>
    [Fact]
    public void FixState_AfterForecast_RestoreHasNoEffect()
    {
      var tank = MakeTank(3);
      tank.TimeStep = 60;
      tank.ForecastState(5.0, 0.01, true);
      double tAfterForecast = tank.GetTemperature(0);

      tank.FixState();
      tank.RestoreState(); // FixState後はnop

      Assert.InRange(tank.GetTemperature(0),
          tAfterForecast - 0.01, tAfterForecast + 0.01);
    }

    /// <summary>ForecastState を複数回呼ぶと最新の予測に上書きされる。</summary>
    [Fact]
    public void ForecastState_CalledTwice_UsesLatestForecast()
    {
      var tank = MakeTank(3);
      tank.TimeStep = 60;
      tank.ForecastState(5.0, 0.01, true);
      tank.ForecastState(40.0, 0.01, true); // 温水に変更
                                            // 温水流入 → タンク温度は初期値(20°C)より上昇
      Assert.True(tank.GetTemperature(0) > 20.0,
          $"After hot forecast T[0]={tank.GetTemperature(0):F3} > 20°C");
    }

    #endregion

    // ================================================================
    #region 蓄熱量・蓄熱流

    /// <summary>
    /// 基準温度と同じ温度のタンクでは蓄熱量がゼロ。
    /// </summary>
    [Fact]
    public void GetHeatStorage_TankAtReferenceTemp_IsZero()
    {
      var tank = MakeTank(2);
      double q = tank.GetHeatStorage(20.0);
      Assert.InRange(q, -0.01, 0.01);
    }

    /// <summary>
    /// タンク温度 > 基準温度 → 蓄熱量が正（温水蓄熱）。
    /// </summary>
    [Fact]
    public void GetHeatStorage_HotTank_IsPositive()
    {
      var tank = MakeTank(2);
      tank.InitializeTemperature(30.0);
      Assert.True(tank.GetHeatStorage(20.0) > 0);
    }

    /// <summary>
    /// タンク温度 < 基準温度 → 蓄熱量が負（冷水蓄熱）。
    /// </summary>
    [Fact]
    public void GetHeatStorage_ColdTank_IsNegative()
    {
      var tank = MakeTank(2);
      tank.InitializeTemperature(10.0);
      Assert.True(tank.GetHeatStorage(20.0) < 0);
    }

    /// <summary>
    /// GetHeatStorageFlow：冷水流入（Tin &lt; Tout）では蓄熱流が負（放熱）。
    /// </summary>
    [Fact]
    public void GetHeatStorageFlow_ColdInlet_IsNegative()
    {
      var tank = MakeTank(3);
      tank.TimeStep = 60;
      tank.ForecastState(5.0, 0.01, true);
      // 流入温度(5°C) < 出口温度(≈20°C) → 蓄熱流は負
      Assert.True(tank.GetHeatStorageFlow() < 0,
          $"Cold inlet: storage flow={tank.GetHeatStorageFlow():F3} kW should be negative");
    }

    /// <summary>
    /// GetHeatStorageFlow：温水流入（Tin &gt; Tout）では蓄熱流が正（蓄熱）。
    /// </summary>
    [Fact]
    public void GetHeatStorageFlow_HotInlet_IsPositive()
    {
      var tank = MakeTank(3);
      tank.TimeStep = 60;
      tank.ForecastState(60.0, 0.01, true);
      Assert.True(tank.GetHeatStorageFlow() > 0,
          $"Hot inlet: storage flow={tank.GetHeatStorageFlow():F3} kW should be positive");
    }

    #endregion

    // ================================================================
    #region 熱損失係数

    /// <summary>熱損失係数を設定・取得できる。</summary>
    [Fact]
    public void SetGetHeatLossCoefficient_RoundTrip()
    {
      var tank = MakeTank(3);
      tank.SetHeatLossCoefficient(1, 0.5);
      Assert.InRange(tank.GetHeatLossCoefficient(1), 0.499, 0.501);
    }

    /// <summary>
    /// タンク数が1の場合は PopoloArgumentException が発生する。
    /// 三重対角行列ソルバーは2タンク以上を必要とする。
    /// </summary>
    [Fact]
    public void Constructor_SingleTank_ThrowsArgumentException()
    {
      Assert.Throws<PopoloArgumentException>(() =>
          new MultiConnectedWaterTank(new double[] { 1.0 }));
    }

    /// <summary>
    /// 熱損失あり（kL>0）では周囲温度に向かって収束する。
    /// 初期=30°C, Tamb=20°C, 流量ゼロ → ForecastState 後にタンク温度が下がる。
    /// 最小構成の2タンクで検証する。
    /// </summary>
    [Fact]
    public void HeatLoss_WithPositiveCoefficient_TankCoolsTowardAmbient()
    {
      var tank = MakeTank(2);
      tank.InitializeTemperature(30.0);
      tank.AmbientTemperature = 20.0;
      tank.SetHeatLossCoefficient(0, 0.1);
      tank.SetHeatLossCoefficient(1, 0.1);
      tank.TimeStep = 3600; // 1時間
      tank.ForecastState(30.0, 0.0, true); // 流量ゼロ（熱損失のみ）
      Assert.True(tank.GetTemperature(0) < 30.0,
          $"Tank should cool: T={tank.GetTemperature(0):F3}°C < 30°C");
      Assert.True(tank.GetTemperature(0) > 20.0,
          $"Tank should not reach ambient: T={tank.GetTemperature(0):F3}°C > 20°C");
    }

    #endregion

    // ================================================================
    #region GetTemperatures

    /// <summary>GetTemperatures で全タンク温度を配列にコピーできる。</summary>
    [Fact]
    public void GetTemperatures_CopiesAllValues()
    {
      var tank = MakeTank(3);
      tank.InitializeTemperature(0, 10.0);
      tank.InitializeTemperature(1, 20.0);
      tank.InitializeTemperature(2, 30.0);
      double[] arr = new double[3];
      tank.GetTemperatures(ref arr);
      Assert.InRange(arr[0], 9.99, 10.01);
      Assert.InRange(arr[1], 19.99, 20.01);
      Assert.InRange(arr[2], 29.99, 30.01);
    }

    #endregion
  }
}