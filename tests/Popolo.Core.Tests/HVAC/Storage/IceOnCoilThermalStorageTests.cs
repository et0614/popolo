/* IceOnCoilThermalStorageTests.cs
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
using Popolo.Core.Physics;

namespace Popolo.Core.Tests.HVAC.Storage
{
  /// <summary>Unit tests for <see cref="IceOnCoilThermalStorage"/>.</summary>
  /// <remarks>
  /// IceOnCoilThermalStorage implements an internal-melt ice-on-coil thermal storage model
  /// based on Aotake, Sagara et al. (SHASEJ Annual Meeting, 2007), extended to allow
  /// sensible heat flow after full solidification.
  ///
  /// Sign convention for HeatTransferToCoil:
  ///   HeatTransferToCoil &gt; 0 → heat rejected to brine (melting mode, brine warms the tank)
  ///   HeatTransferToCoil &lt; 0 → heat extracted from brine (ice making mode)
  ///
  /// Ice state transitions:
  ///   NoIce   → water only, pipe exposed.
  ///   Frozen  → ice solidly attached to pipe (during freezing).
  ///   Melting → water layer has formed between pipe and ice.
  /// </remarks>
  public class IceOnCoilThermalStorageTests
  {
    #region ヘルパー

    // 標準的な内融式氷蓄熱槽のパラメータ
    private const double WaterVolume = 1.0;    // 水槽容量 [m³]
    private const int NumberOfBranches = 50;     // 分岐数 [-]
    private const double BranchLength = 4.0;    // 分岐長 [m]
    private const double PipeInnerDiameter = 0.0125; // 配管内径 [m] (1/2インチ相当)
    private const double PipeOuterDiameter = 0.0159; // 配管外径 [m] (1/2インチ相当)

    private static IceOnCoilThermalStorage MakeStandard(double initialWaterTemp = 10.0)
    {
      var tank = new IceOnCoilThermalStorage(
          WaterVolume, NumberOfBranches, BranchLength,
          PipeInnerDiameter, PipeOuterDiameter);
      tank.Initialize(initialWaterTemp);
      tank.TimeStep = 60; // 1分
      return tank;
    }

    #endregion

    // ================================================================
    #region 物性定数

    /// <summary>氷の密度 [kg/m³] は 917。</summary>
    [Fact]
    public void Constants_IceDensity_Is917()
    {
      Assert.Equal(917d, IceOnCoilThermalStorage.ICE_DENSITY);
    }

    /// <summary>氷の融解潜熱 [kJ/kg] は 334。</summary>
    [Fact]
    public void Constants_IceLatentHeat_Is334()
    {
      Assert.Equal(334d, IceOnCoilThermalStorage.ICE_LATENT_HEAT);
    }

    /// <summary>氷の比熱 [kJ/(kg·K)] は 2.1。</summary>
    [Fact]
    public void Constants_IceSpecificHeat_Is2Point1()
    {
      Assert.Equal(2.1, IceOnCoilThermalStorage.ICE_SPECIFIC_HEAT);
    }

    #endregion

    // ================================================================
    #region 初期化

    /// <summary>コンストラクタに指定した寸法パラメータがプロパティで取得できる。</summary>
    [Fact]
    public void Constructor_DimensionsExposedAsProperties()
    {
      var tank = MakeStandard();
      Assert.Equal(WaterVolume, tank.WaterVolume);
      Assert.Equal(NumberOfBranches, tank.NumberOfBranches);
      Assert.Equal(BranchLength, tank.BranchLength);
      Assert.Equal(PipeInnerDiameter, tank.PipeInnerDiameter);
      Assert.Equal(PipeOuterDiameter, tank.PipeOuterDiameter);
    }

    /// <summary>PipeThickness は (外径 − 内径) の半分。</summary>
    [Fact]
    public void PipeThickness_IsHalfOfOuterMinusInner()
    {
      var tank = MakeStandard();
      double expected = 0.5 * (PipeOuterDiameter - PipeInnerDiameter);
      Assert.InRange(tank.PipeThickness, expected - 1e-12, expected + 1e-12);
    }

    /// <summary>Initialize 後は氷なし状態で、IPF は 0。</summary>
    [Fact]
    public void Initialize_NoIceState_IPFIsZero()
    {
      var tank = MakeStandard(10.0);
      Assert.Equal(0.0, tank.GetIcePackingFactor());
    }

    /// <summary>Initialize に正の水温を指定すると、平均水温がその値になる。</summary>
    [Fact]
    public void Initialize_PositiveTemp_AverageMatches()
    {
      var tank = MakeStandard(10.0);
      Assert.InRange(tank.GetAverageWaterIceTemperature(), 9.999, 10.001);
    }

    /// <summary>Initialize に負の水温を指定しても、0 にクランプされる（氷は無し）。</summary>
    [Fact]
    public void Initialize_NegativeTemp_ClampedToZero()
    {
      var tank = MakeStandard(-5.0);
      Assert.InRange(tank.GetAverageWaterIceTemperature(), -0.001, 0.001);
      Assert.Equal(0.0, tank.GetIcePackingFactor()); // クランプ後は氷なし
    }

    /// <summary>TimeStep プロパティは正値のみ受け付ける。</summary>
    [Fact]
    public void TimeStep_AcceptsPositive_RejectsNonPositive()
    {
      var tank = MakeStandard();
      tank.TimeStep = 120;
      Assert.Equal(120, tank.TimeStep);

      tank.TimeStep = -1;   // 負値は無視
      Assert.Equal(120, tank.TimeStep);

      tank.TimeStep = 0;    // ゼロも無視
      Assert.Equal(120, tank.TimeStep);
    }

    /// <summary>セグメント数は 10。</summary>
    [Fact]
    public void NumberOfSegments_Is10()
    {
      Assert.Equal(10, IceOnCoilThermalStorage.SEGMENTS_COUNT);
    }

    #endregion

    // ================================================================
    #region 製氷運転

    /// <summary>低温ブライン投入で氷が生成される（IPF が増加）。</summary>
    [Fact]
    public void Update_ColdBrine_ProducesIce()
    {
      var tank = MakeStandard(5.0);
      tank.AmbientTemperature = 5.0; // 熱損失の影響を小さく
      double ipfBefore = tank.GetIcePackingFactor();

      // ブラインを-5°Cで十分長く流す(60分×60ステップ=1時間)
      for (int i = 0; i < 60; i++) tank.Update(-5.0, 0.5);

      Assert.True(tank.GetIcePackingFactor() > ipfBefore,
          $"IPF after freezing = {tank.GetIcePackingFactor():F4} should be > {ipfBefore:F4}");
    }

    /// <summary>製氷中は HeatTransferToCoil が負（ブラインから熱を奪う）。</summary>
    [Fact]
    public void Update_ColdBrine_HeatTransferToCoilIsNegative()
    {
      var tank = MakeStandard(5.0);
      tank.AmbientTemperature = 5.0;
      tank.Update(-5.0, 0.5);
      Assert.True(tank.HeatTransferToCoil < 0,
          $"HeatTransferToCoil = {tank.HeatTransferToCoil:F4} kW should be negative (ice making)");
    }

    /// <summary>製氷運転中のブライン出口温度は入口温度より高い（ブラインが加熱される）。</summary>
    [Fact]
    public void Update_ColdBrine_OutletBrineWarmer()
    {
      var tank = MakeStandard(5.0);
      tank.AmbientTemperature = 5.0;
      tank.Update(-5.0, 0.5);
      Assert.True(tank.OutletBrineTemperature > tank.InletBrineTemperature,
          $"Outlet = {tank.OutletBrineTemperature:F3}°C should be > Inlet = {tank.InletBrineTemperature:F3}°C");
    }

    /// <summary>製氷が進むと IceState が Frozen になる。</summary>
    /// <remarks>
    /// 水槽容量 1 m³ を初期温度から 0 °C まで冷却してから氷を作る必要があるので、
    /// 初期水温は 0 °C に近い方が早く Frozen 状態に入る。
    /// </remarks>
    [Fact]
    public void Update_AfterFreezingSomeIce_StateIsFrozen()
    {
      var tank = MakeStandard(2.0);
      tank.AmbientTemperature = 2.0;
      for (int i = 0; i < 60; i++) tank.Update(-5.0, 0.5);
      Assert.Equal(IceOnCoilThermalStorage.IceState.Frozen, tank.CurrentState);
    }

    #endregion

    // ================================================================
    #region 解氷運転

    /// <summary>氷が存在する状態で高温ブラインを流すと氷が融ける（IPF 減少）。</summary>
    [Fact]
    public void Update_WarmBrineWithIce_MeltsIce()
    {
      var tank = MakeStandard(2.0);
      tank.AmbientTemperature = 2.0;
      // まず製氷
      for (int i = 0; i < 60; i++) tank.Update(-5.0, 0.5);
      double ipfMax = tank.GetIcePackingFactor();
      Assert.True(ipfMax > 0, "製氷ステップで氷が作られていること");

      // 次に解氷
      for (int i = 0; i < 60; i++) tank.Update(10.0, 0.5);
      Assert.True(tank.GetIcePackingFactor() < ipfMax,
          $"IPF after melting = {tank.GetIcePackingFactor():F4} < IPF before melting = {ipfMax:F4}");
    }

    /// <summary>解氷中は HeatTransferToCoil が正（ブラインに熱を渡す）。</summary>
    [Fact]
    public void Update_WarmBrineWithIce_HeatTransferToCoilIsPositive()
    {
      var tank = MakeStandard(2.0);
      tank.AmbientTemperature = 2.0;
      for (int i = 0; i < 60; i++) tank.Update(-5.0, 0.5); // 製氷
      tank.Update(10.0, 0.5); // 解氷
      Assert.True(tank.HeatTransferToCoil > 0,
          $"HeatTransferToCoil = {tank.HeatTransferToCoil:F4} kW should be positive (melting)");
    }

    /// <summary>製氷→解氷運転後、氷状態は Melting になる（内融式なので配管と氷の間に水層が生じる）。</summary>
    [Fact]
    public void Update_WarmBrineAfterFreezing_StateIsMelting()
    {
      var tank = MakeStandard(2.0);
      tank.AmbientTemperature = 2.0;
      for (int i = 0; i < 60; i++) tank.Update(-5.0, 0.5); // 製氷
      for (int i = 0; i < 5; i++) tank.Update(10.0, 0.5); // 少し解氷
      Assert.Equal(IceOnCoilThermalStorage.IceState.Melting, tank.CurrentState);
    }

    #endregion

    // ================================================================
    #region 熱流ゼロのケース

    /// <summary>ブラインと水槽が同温度・ambient も同温度なら熱流は 0。</summary>
    [Fact]
    public void Update_ThermalEquilibrium_NoHeatTransfer()
    {
      var tank = MakeStandard(5.0);
      tank.AmbientTemperature = 5.0;
      tank.HeatLossCoefficient = 0; // 熱損失なし
      tank.Update(5.0, 0.5);
      Assert.InRange(tank.HeatTransferToCoil, -1e-6, 1e-6);
      Assert.InRange(tank.HeatLoss, -1e-6, 1e-6);
    }

    /// <summary>流量ゼロでブライン入口温度=出口温度。</summary>
    [Fact]
    public void Update_ZeroBrineFlow_OutletEqualsInlet()
    {
      var tank = MakeStandard(5.0);
      tank.AmbientTemperature = 5.0;
      tank.Update(-5.0, 0.0);
      Assert.Equal(tank.InletBrineTemperature, tank.OutletBrineTemperature);
    }

    #endregion

    // ================================================================
    #region 熱損失

    /// <summary>周囲温度 > 水温のとき、HeatLoss は正（周囲から水槽が吸熱）。</summary>
    [Fact]
    public void HeatLoss_WarmerAmbient_IsPositive()
    {
      var tank = MakeStandard(2.0);
      tank.AmbientTemperature = 30.0;  // 周囲が暖かい
      tank.HeatLossCoefficient = 50.0; // [W/K]
      tank.Update(2.0, 0.5);           // ブライン温度は水温と同じ(熱流を切り離す)
      Assert.True(tank.HeatLoss > 0,
          $"HeatLoss = {tank.HeatLoss:F4} kW should be positive (tank absorbs heat from ambient)");
    }

    /// <summary>周囲温度 &lt; 水温のとき、HeatLoss は負（水槽から周囲へ放熱）。</summary>
    [Fact]
    public void HeatLoss_ColderAmbient_IsNegative()
    {
      var tank = MakeStandard(20.0);
      tank.AmbientTemperature = 0.0;   // 周囲が冷たい
      tank.HeatLossCoefficient = 50.0;
      tank.Update(20.0, 0.5);
      Assert.True(tank.HeatLoss < 0,
          $"HeatLoss = {tank.HeatLoss:F4} kW should be negative (tank loses heat to ambient)");
    }

    /// <summary>HeatLossCoefficient の set/get は可逆（往復しても元の値）。</summary>
    [Fact]
    public void HeatLossCoefficient_SetGet_Roundtrip()
    {
      var tank = MakeStandard();
      tank.HeatLossCoefficient = 123.4;
      Assert.InRange(tank.HeatLossCoefficient, 123.399, 123.401);
    }

    #endregion

    // ================================================================
    #region IPF (Ice Packing Factor)

    /// <summary>IPF は 0 以上で、最大でも 1.0 を大きく超えない（完全凍結で ICE_DENSITY/WATER_DENSITY 近傍）。</summary>
    [Fact]
    public void IPF_WithinPhysicalBounds()
    {
      var tank = MakeStandard(2.0);
      tank.AmbientTemperature = 2.0;
      for (int i = 0; i < 300; i++) tank.Update(-5.0, 0.5);
      double ipf = tank.GetIcePackingFactor();
      Assert.True(ipf >= 0, $"IPF = {ipf:F4} must be non-negative");
      // 水槽が完全に凍ったときのIPF上限は 氷質量/水質量 ≒ 917/1000
      Assert.True(ipf <= IceOnCoilThermalStorage.ICE_DENSITY / PhysicsConstants.NominalWaterDensity + 1e-6,
          $"IPF = {ipf:F4} cannot exceed ICE_DENSITY/WATER_DENSITY = {IceOnCoilThermalStorage.ICE_DENSITY / PhysicsConstants.NominalWaterDensity:F4}");
    }

    /// <summary>十分長く製氷運転すると IPF がほぼ最大値(≒ ICE_DENSITY/WATER_DENSITY)まで到達する。</summary>
    /// <remarks>
    /// 水槽容量 1 m³ を 0 °C まで冷却してから徐々に凍結させるため、最大値に近づくには
    /// 十分な時間(20 時間相当以上)が必要。閾値は最大値の半分と控えめに設定。
    /// </remarks>
    [Fact]
    public void IPF_LongFreezing_ApproachesMaximum()
    {
      var tank = MakeStandard(2.0);
      tank.AmbientTemperature = 2.0;
      // 十分に長く(20時間相当)
      for (int i = 0; i < 1200; i++) tank.Update(-5.0, 0.5);
      double maxIpf = IceOnCoilThermalStorage.ICE_DENSITY / PhysicsConstants.NominalWaterDensity;
      Assert.True(tank.GetIcePackingFactor() > 0.5 * maxIpf,
          $"IPF = {tank.GetIcePackingFactor():F4} should approach {maxIpf:F4} after long freezing");
    }

    #endregion

    // ================================================================
    #region エネルギー収支

    /// <summary>
    /// 熱損失を 0 とし、製氷運転を 1 ステップだけ行った場合、
    /// ブラインが失った熱量 と 氷に蓄えられた潜熱 + 顕熱変化 の大小関係は整合すること。
    /// (厳密な完全一致は数値誤差があるので、符号とオーダーのみ確認。)
    /// </summary>
    [Fact]
    public void EnergyBalance_FreezingStep_BrineHeatLossIsConsistentWithTankEnergyChange()
    {
      var tank = MakeStandard(2.0);
      tank.AmbientTemperature = 2.0;
      tank.HeatLossCoefficient = 0.0;

      double tAvgBefore = tank.GetAverageWaterIceTemperature();
      double ipfBefore = tank.GetIcePackingFactor();

      tank.Update(-5.0, 0.5);

      // ブラインが水槽に渡した熱量 [kJ]（製氷なので負）
      double qBrineToTank = tank.HeatTransferToCoil * tank.TimeStep;
      Assert.True(qBrineToTank < 0, "製氷中の水槽への熱流は負");

      double tAvgAfter = tank.GetAverageWaterIceTemperature();
      double ipfAfter = tank.GetIcePackingFactor();

      // 氷が生じているか、または水温が低下していること
      bool tankCooledDown = (ipfAfter > ipfBefore) || (tAvgAfter < tAvgBefore);
      Assert.True(tankCooledDown,
          $"Expected ice formation or temperature drop. IPF: {ipfBefore:F4} → {ipfAfter:F4}, avgT: {tAvgBefore:F3} → {tAvgAfter:F3}");
    }

    #endregion

    // ================================================================
    #region 例外条件

    /// <summary>タイムステップが大きすぎて 1 ステップで全水量が凍る場合は PopoloNumericalException。</summary>
    [Fact]
    public void Update_TooLargeTimeStep_ThrowsOnMassIceProduction()
    {
      // 小さい水槽 + 過大な熱引き抜き
      var tank = new IceOnCoilThermalStorage(
          waterVolume: 0.01,         // 非常に小さい
          branchCount: 10,
          branchLength: 2.0,
          pipeInnerDiameter: PipeInnerDiameter,
          pipeOuterDiameter: PipeOuterDiameter);
      tank.Initialize(1.0);
      tank.AmbientTemperature = 1.0;
      tank.TimeStep = 24 * 3600; // 1日

      Assert.Throws<PopoloNumericalException>(() =>
      {
        tank.Update(-30.0, 2.0); // 過大な入熱負荷
      });
    }

    #endregion

    // ================================================================
    #region IReadOnly インターフェース

    /// <summary>IReadOnlyIceOnCoilThermalStorage で参照できるプロパティが一致する。</summary>
    [Fact]
    public void IReadOnlyInterface_ExposesSameValues()
    {
      var tank = MakeStandard(5.0);
      tank.AmbientTemperature = 5.0;
      tank.Update(-3.0, 0.3);

      IReadOnlyIceOnCoilThermalStorage ro = tank;

      Assert.Equal(tank.TimeStep, ro.TimeStep);
      Assert.Equal(tank.CurrentState, ro.CurrentState);
      Assert.Equal(tank.WaterVolume, ro.WaterVolume);
      Assert.Equal(tank.NumberOfBranches, ro.NumberOfBranches);
      Assert.Equal(tank.BranchLength, ro.BranchLength);
      Assert.Equal(tank.PipeInnerDiameter, ro.PipeInnerDiameter);
      Assert.Equal(tank.PipeOuterDiameter, ro.PipeOuterDiameter);
      Assert.Equal(tank.PipeThickness, ro.PipeThickness);
      Assert.Equal(tank.IsBubbling, ro.IsBubbling);
      Assert.Equal(tank.InletBrineTemperature, ro.InletBrineTemperature);
      Assert.Equal(tank.OutletBrineTemperature, ro.OutletBrineTemperature);
      Assert.Equal(tank.BrineFlowRate, ro.BrineFlowRate);
      Assert.Equal(tank.BrineSpecificHeat, ro.BrineSpecificHeat);
      Assert.Equal(tank.HeatTransferToCoil, ro.HeatTransferToCoil);
      Assert.Equal(tank.HeatLossCoefficient, ro.HeatLossCoefficient);
      Assert.Equal(tank.AmbientTemperature, ro.AmbientTemperature);
      Assert.Equal(tank.HeatLoss, ro.HeatLoss);
      Assert.Equal(tank.GetIcePackingFactor(), ro.GetIcePackingFactor());
      Assert.Equal(tank.GetAverageWaterIceTemperature(), ro.GetAverageWaterIceTemperature());
    }

    #endregion
  }
}