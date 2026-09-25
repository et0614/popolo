/* FluidCircuitTests.cs
 *
 * Copyright (C) 2026 E.Togashi
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using Xunit;
using Popolo.Core.Exceptions;
using Popolo.Core.HVAC.FluidCircuit;
using Popolo.Core.HVAC.FluidCircuit.ControllableFlowSolver;

namespace Popolo.Core.Tests.HVAC.FluidCircuit
{
  /// <summary>Unit tests for <see cref="Popolo.Core.HVAC.FluidCircuit"/>.</summary>
  /// <remarks>
  /// Test network topology (same as CircuitTester.makePumpNetwork1):
  ///
  ///   Supply side (9 nodes: 01,02,05,08,11,03,06,09,12):
  ///     Node[0](pump outlet +0.09 m³/s)
  ///       ├─ pipe01_02 ─ Node[1] ─ pipe02_05 ─ Node[2]
  ///       │              Node[1] ─ AHU1 ───────  Node[5] (-0.0225)
  ///       │                         Node[2] ─ AHU2 ─ Node[6] (-0.0225)
  ///       └─ pipe01_08 ─ Node[3] ─ pipe08_11 ─ Node[4]
  ///                      Node[3] ─ AHU3 ───────  Node[7] (-0.0225)
  ///                                 Node[4] ─ AHU4 ─ Node[8] (-0.0225)
  ///
  ///   Return side (5 nodes: 00,04,07,10,13):
  ///     Node[0](pump inlet -0.09) ← pipe04_00 ← Node[1](+0.0225)
  ///                               ← pipe10_00 ← Node[3](+0.0225)
  ///     Node[1] ← pipe07_04 ← Node[2](+0.0225)
  ///     Node[3] ← pipe13_10 ← Node[4](+0.0225)
  /// </remarks>
  public class FluidCircuitTests
  {
    #region Constants and helpers

    private const double Lcf = 1.5;    // 局部抵抗係数（サンプルコードと同値）
    private const double DesignQ = 0.0225; // AHU 1台あたりの設計流量 [m³/s]
    private const double TotalQ = DesignQ * 4;

    /// <summary>
    /// CircuitTester.makePumpNetwork1 と同等の往路・復路の回路を構築する。
    /// </summary>
    private static (CircuitNetwork supply, CircuitNode[] sn,
                     CircuitNetwork return_, CircuitNode[] rn) BuildPumpNetwork()
    {
      // ── 往路回路 ────────────────────────────────────────────────
      var supply = new CircuitNetwork();
      var pipe01_02 = new WaterPipe(30 * Lcf, 0.150, WaterPipe.Material.CarbonSteel);
      var pipe02_05 = new WaterPipe(5 * Lcf, 0.100, WaterPipe.Material.CarbonSteel);
      var pipe01_08 = new WaterPipe(70 * Lcf, 0.150, WaterPipe.Material.CarbonSteel);
      var pipe08_11 = new WaterPipe(5 * Lcf, 0.100, WaterPipe.Material.CarbonSteel);
      var ahu1 = new SimpleCircuitBranch(DesignQ, 150);
      var ahu2 = new SimpleCircuitBranch(DesignQ, 150);
      var ahu3 = new SimpleCircuitBranch(DesignQ, 150);
      var ahu4 = new SimpleCircuitBranch(DesignQ, 150);

      var sn = new CircuitNode[9]; // 01,02,05,08,11,03,06,09,12 の順
      for (int i = 0; i < sn.Length; i++) sn[i] = supply.AddNode();
      supply.ConnectNode(pipe01_02, sn[0], sn[1]);
      supply.ConnectNode(pipe02_05, sn[1], sn[2]);
      supply.ConnectNode(pipe01_08, sn[0], sn[3]);
      supply.ConnectNode(pipe08_11, sn[3], sn[4]);
      supply.ConnectNode(ahu1, sn[1], sn[5]);
      supply.ConnectNode(ahu2, sn[2], sn[6]);
      supply.ConnectNode(ahu3, sn[3], sn[7]);
      supply.ConnectNode(ahu4, sn[4], sn[8]);
      sn[0].Inflow = TotalQ;
      for (int i = 5; i < 9; i++) sn[i].Inflow = -DesignQ;

      // ── 復路回路 ────────────────────────────────────────────────
      var return_ = new CircuitNetwork();
      var pipe04_00 = new WaterPipe(30 * Lcf, 0.150, WaterPipe.Material.CarbonSteel);
      var pipe07_04 = new WaterPipe(5 * Lcf, 0.100, WaterPipe.Material.CarbonSteel);
      var pipe10_00 = new WaterPipe(70 * Lcf, 0.150, WaterPipe.Material.CarbonSteel);
      var pipe13_10 = new WaterPipe(5 * Lcf, 0.100, WaterPipe.Material.CarbonSteel);

      var rn = new CircuitNode[5]; // 00,04,07,10,13 の順
      for (int i = 0; i < rn.Length; i++) rn[i] = return_.AddNode();
      return_.ConnectNode(pipe04_00, rn[1], rn[0]);
      return_.ConnectNode(pipe07_04, rn[2], rn[1]);
      return_.ConnectNode(pipe10_00, rn[3], rn[0]);
      return_.ConnectNode(pipe13_10, rn[4], rn[3]);
      for (int i = 1; i < 5; i++) rn[i].Inflow = DesignQ;
      rn[0].Inflow = -TotalQ;

      return (supply, sn, return_, rn);
    }

    #endregion

    // ================================================================
    #region Conduit static methods

    /// <summary>
    /// 層流（Re=1000）では Darcy-Weisbach 摩擦係数 f = 64/Re = 0.064。
    /// </summary>
    [Fact]
    public void Conduit_FrictionFactor_Laminar_Is64OverRe()
    {
      double ff = Conduit.GetFrictionFactor(1000, 0);
      Assert.InRange(ff, 0.063, 0.065);
    }

    /// <summary>
    /// 乱流粗管（Re=1e6, ε/D=1e-3）の摩擦係数は Moody 線図より約 0.020。
    /// </summary>
    [Fact]
    public void Conduit_FrictionFactor_TurbulentRough_MatchesMoody()
    {
      double ff = Conduit.GetFrictionFactor(1e6, 1e-3);
      Assert.InRange(ff, 0.019, 0.022);
    }

    /// <summary>Re が高いほど摩擦係数は減少する（同一相対粗度）。</summary>
    [Fact]
    public void Conduit_FrictionFactor_DecreasesWithHigherReynolds()
    {
      double ffLow = Conduit.GetFrictionFactor(1e4, 1e-4);
      double ffHigh = Conduit.GetFrictionFactor(1e6, 1e-4);
      Assert.True(ffLow > ffHigh,
          $"Re=1e4: f={ffLow:F4}  >  Re=1e6: f={ffHigh:F4}");
    }

    /// <summary>正方形ダクト（a=b=0.4 m）の等価直径は辺長に等しい。</summary>
    /// <remarks>
    /// GetEquivalentDiameterOfRectangularDuct takes arguments in mm and returns m.
    /// A 400 mm × 400 mm square duct has an equivalent diameter of 0.4 m.
    /// </remarks>
    /// <remarks>
    /// Huebscher (1948): De = 1.30 × (a×b)^0.625 / (a+b)^0.25 [m].
    /// A square duct does NOT have De = side length; De is slightly larger.
    /// 0.4 m × 0.4 m → De ≈ 0.437 m.
    /// Wider ducts give smaller De relative to the arithmetic mean of sides.
    /// </remarks>
    [Fact]
    public void Conduit_EquivalentDiameter_SquareDuct_MatchesHuebscher()
    {
      // 0.4m × 0.4m square duct: De = 1.30*(0.16)^0.625/(0.8)^0.25 ≈ 0.437 m
      double deq = Conduit.GetEquivalentDiameterOfRectangularDuct(0.4, 0.4);
      Assert.InRange(deq, 0.430, 0.445);
    }

    /// <summary>圧力損失は流量の2乗に比例する（Darcy-Weisbach）。</summary>
    [Fact]
    public void Conduit_PressureDrop_ScalesWithFlowRateSquared()
    {
      double ff = 0.02; double rho = 1000;
      double length = 10; double diameter = 0.05;
      double dp1 = Conduit.GetPressureDrop(ff, rho, length, diameter, 0.01);
      double dp2 = Conduit.GetPressureDrop(ff, rho, length, diameter, 0.02);
      // 流量 2倍 → 流速 2倍 → ΔP ≈ 4倍
      Assert.InRange(dp2 / dp1, 3.8, 4.2);
    }

    #endregion

    // ================================================================
    #region WaterPipe

    /// <summary>管長が長いほど圧力損失が大きい。</summary>
    [Fact]
    public void WaterPipe_PressureDrop_IncreasesWithLength()
    {
      var pipeShort = new WaterPipe(5.0, 0.05, WaterPipe.Material.CarbonSteel);
      var pipeLong = new WaterPipe(20.0, 0.05, WaterPipe.Material.CarbonSteel);
      Assert.True(pipeLong.GetPressureDrop(0.01) > pipeShort.GetPressureDrop(0.01));
    }

    /// <summary>
    /// 断熱材を追加すると配管の線熱通過率が低下する。
    /// LinearThermalTransmittance プロパティは UpdateHeatFlow() が呼ばれるまで 0 のため、
    /// GetPipeLinearThermalTransmittance() 静的メソッドで直接比較する。
    /// </summary>
    [Fact]
    public void WaterPipe_Insulator_ReducesLinearThermalTransmittance()
    {
      // 炭素鋼管 (k≈50 W/(mK)), 内径50mm, 管厚3mm
      double inner = 0.05; double pipeThick = 0.003; double kPipe = 50.0;

      // 断熱なし
      double uBare = WaterPipe.GetPipeLinearThermalTransmittance(
          inner, 1.0, 0.0, kPipe, pipeThick);

      // グラスウール 50mm (k≈0.043 W/(mK))
      double uInsulated = WaterPipe.GetPipeLinearThermalTransmittance(
          inner, 0.043, 0.05, kPipe, pipeThick);

      Assert.True(uInsulated < uBare,
          $"With insulation: U={uInsulated:F4}  <  Bare: U={uBare:F4} W/(m·K)");
    }

    /// <summary>
    /// InsulatorThickness は SetInsulator で更新され、RemoveInsulator で 0 に戻る。
    /// </summary>
    [Fact]
    public void WaterPipe_RemoveInsulator_ClearsInsulatorThickness()
    {
      var pipe = new WaterPipe(10.0, 0.05, WaterPipe.Material.CarbonSteel);
      pipe.SetInsulator(0.05, WaterPipe.Insulator.GlassWool);
      Assert.Equal(0.05, pipe.InsulatorThickness);
      pipe.RemoveInsulator();
      Assert.Equal(0.0, pipe.InsulatorThickness);
    }

    #endregion

    // ================================================================
    #region SimpleCircuitBranch

    /// <summary>設計点の抵抗係数 R = ΔP / Q²。</summary>
    [Fact]
    public void SimpleCircuitBranch_Resistance_EqualsDesignPressureOverFlowSquared()
    {
      double q = 0.02; double dp = 100;
      var branch = new SimpleCircuitBranch(q, dp);
      double expected = dp / (q * q);
      Assert.InRange(branch.Resistance, expected * 0.99, expected * 1.01);
    }

    /// <summary>圧力損失は流量の2乗に比例する。</summary>
    [Fact]
    public void SimpleCircuitBranch_PressureDrop_ScalesWithFlowRateSquared()
    {
      var branch = new SimpleCircuitBranch(0.02, 100);
      double dp1 = branch.GetPressureDrop(0.01);
      double dp2 = branch.GetPressureDrop(0.02);
      Assert.InRange(dp2 / dp1, 3.8, 4.2);
    }

    #endregion

    // ================================================================
    #region CircuitNetwork

    /// <summary>BasePressureNode 未設定で Solve() すると PopoloInvalidOperationException。</summary>
    [Fact]
    public void CircuitNetwork_Solve_WithoutBasePressureNode_Throws()
    {
      // AddNode() sets BasePressureNode automatically for the first node.
      // RemoveNode() clears it. Build a 3-node chain and remove the
      // base-pressure node after wiring, then reconnect via a new branch.
      var net = new CircuitNetwork();
      var n0 = net.AddNode(); // BasePressureNode = n0 (auto)
      var n1 = net.AddNode();
      var n2 = net.AddNode();
      var br01 = new SimpleCircuitBranch(0.01, 50);
      var br12 = new SimpleCircuitBranch(0.01, 50);
      net.ConnectNode(br01, n0, n1);
      net.ConnectNode(br12, n1, n2);
      n0.Inflow = 0.01;
      n2.Inflow = -0.01;
      // Remove the base-pressure node's branch, then the node itself
      // so that BasePressureNode becomes null.
      net.RemoveBranch(br01);
      net.RemoveNode(n0);
      // Reconnect n1→n2 only; BasePressureNode is now null.
      n1.Inflow = 0.01;
      Assert.Throws<PopoloInvalidOperationException>(() => net.Solve());
    }

    /// <summary>接続のないノードは RemoveNode で削除できる。</summary>
    [Fact]
    public void CircuitNetwork_RemoveNode_Unconnected_ReturnsTrue()
    {
      var net = new CircuitNetwork();
      Assert.True(net.RemoveNode(net.AddNode()));
    }

    /// <summary>接続中のノードは RemoveNode できない。</summary>
    [Fact]
    public void CircuitNetwork_RemoveNode_Connected_ReturnsFalse()
    {
      var net = new CircuitNetwork();
      var n0 = net.AddNode(); var n1 = net.AddNode();
      net.ConnectNode(new SimpleCircuitBranch(0.01, 50), n0, n1);
      Assert.False(net.RemoveNode(n0));
    }

    /// <summary>
    /// 往路回路（吐出圧一定 250 kPa）を Solve した後、
    /// 基準ノードの圧力が 250 kPa に収束する。
    /// </summary>
    [Fact]
    public void CircuitNetwork_Solve_SupplySide_BasePressureIsSet()
    {
      var (supply, sn, _, _) = BuildPumpNetwork();
      supply.SetBasePressure(sn[0], 250);
      Assert.True(supply.Solve(), "Supply network should converge");
      Assert.InRange(sn[0].Pressure, 249.0, 251.0);
    }

    /// <summary>AHU 末端ノードの圧力はポンプ吐出ノードより低い（圧力降下）。</summary>
    [Fact]
    public void CircuitNetwork_Solve_SupplySide_PressureDropsTowardAHU()
    {
      var (supply, sn, _, _) = BuildPumpNetwork();
      supply.SetBasePressure(sn[0], 250);
      supply.Solve();
      // AHU 末端ノード: sn[5]〜sn[8]
      for (int i = 5; i < 9; i++)
        Assert.True(sn[i].Pressure < sn[0].Pressure,
            $"AHU node[{i}].P={sn[i].Pressure:F2} kPa < pump outlet {sn[0].Pressure:F2} kPa");
    }

    /// <summary>往路・復路ともに解いた後、往路最高圧 > 復路最高圧。</summary>
    [Fact]
    public void CircuitNetwork_Solve_BothSides_SupplyPressureHigherThanReturn()
    {
      var (supply, sn, return_, rn) = BuildPumpNetwork();
      supply.SetBasePressure(sn[0], 250);
      return_.SetBasePressure(rn[0], 0);
      supply.Solve(); return_.Solve();
      Assert.True(sn[0].Pressure > rn[1].Pressure,
          $"Supply[0]={sn[0].Pressure:F2} kPa  >  Return[1]={rn[1].Pressure:F2} kPa");
    }

    /// <summary>
    /// 末端差圧一定制御：往路末端 sn[4] と復路末端 rn[4] の差圧が概ね 160 kPa。
    /// （CircuitTester.CircuitNetworkTest1 の末端差圧一定制御と同等）
    /// </summary>
    [Fact]
    public void CircuitNetwork_Solve_EndDifferentialPressure_Around160kPa()
    {
      var (supply, sn, return_, rn) = BuildPumpNetwork();
      return_.SetBasePressure(rn[0], 0);
      return_.Solve();
      supply.SetBasePressure(sn[4], rn[4].Pressure + 160);
      supply.Solve();
      Assert.InRange(sn[4].Pressure - rn[4].Pressure, 155.0, 165.0);
    }

    /// <summary>収束後、全ノードで質量保存が成立する（IntegrateFlow ≒ 0）。</summary>
    [Fact]
    public void CircuitNetwork_Solve_MassConservation_AtAllNodes()
    {
      var (supply, sn, _, _) = BuildPumpNetwork();
      supply.SetBasePressure(sn[0], 250);
      supply.Solve();
      foreach (var node in supply.Nodes)
        Assert.InRange(node.IntegrateFlow(), -1e-5, 1e-5);
    }

    #endregion

    // ================================================================
    #region CentrifugalPump / PumpSystem

    /// <summary>設計流量で UpdateState すると消費電力が正。</summary>
    [Fact]
    public void CentrifugalPump_DesignPoint_ElectricConsumptionIsPositive()
    {
      var pump = new CentrifugalPump(
          260, 0.03, 250, 0.03,
          CentrifugalPump.ControlMethod.ConstantPressureWithInverter, 40);
      pump.UpdateState(0.03);
      Assert.True(pump.GetElectricConsumption() > 0);
    }

    /// <summary>ShutOff 後は消費電力がゼロ。</summary>
    [Fact]
    public void CentrifugalPump_ShutOff_ElectricConsumptionIsZero()
    {
      var pump = new CentrifugalPump(
          260, 0.03, 250, 0.03,
          CentrifugalPump.ControlMethod.ConstantPressureWithInverter, 40);
      pump.ShutOff();
      Assert.Equal(0.0, pump.GetElectricConsumption());
    }

    /// <summary>
    /// PumpSystem（INV）：流量を下げると消費電力が低下する。
    /// （CircuitTester.CentrifugalPumpTest の電力カーブ検証）
    /// </summary>
    [Fact]
    public void PumpSystem_Inverter_PowerDecreasesWithFlowRate()
    {
      var pump = new CentrifugalPump(
          260, 0.03, 250, 0.03,
          CentrifugalPump.ControlMethod.ConstantPressureWithInverter, 40);
      var ps = new PumpSystem(pump, 250, 0.09, 40, 3);

      ps.TotalFlowRate = 0.09; ps.UpdateState();
      double powerFull = ps.GetElectricConsumption();
      ps.TotalFlowRate = 0.045; ps.UpdateState();
      double powerHalf = ps.GetElectricConsumption();

      Assert.True(powerFull > powerHalf,
          $"Full={powerFull:F3} kW  >  50%={powerHalf:F3} kW");
    }

    /// <summary>
    /// 部分負荷（50%）時に INV 制御はバイパス制御より消費電力が小さい。
    /// （CircuitTester.CentrifugalPumpTest の制御方式比較）
    /// </summary>
    [Fact]
    public void PumpSystem_Inverter_LessPowerThanBypass_AtPartialLoad()
    {
      var pumpBy = new CentrifugalPump(
          260, 0.03, 250, 0.03,
          CentrifugalPump.ControlMethod.ConstantPressureWithBypass, 40);
      var pumpInv = new CentrifugalPump(
          260, 0.03, 250, 0.03,
          CentrifugalPump.ControlMethod.ConstantPressureWithInverter, 40);
      var psBy = new PumpSystem(pumpBy, 250, 0.09, 40, 3);
      var psInv = new PumpSystem(pumpInv, 250, 0.09, 40, 3);

      psBy.TotalFlowRate = psInv.TotalFlowRate = 0.045;
      psBy.UpdateState(); psInv.UpdateState();

      Assert.True(psInv.GetElectricConsumption() < psBy.GetElectricConsumption(),
          $"INV={psInv.GetElectricConsumption():F3} kW  <  Bypass={psBy.GetElectricConsumption():F3} kW");
    }

    #endregion

    // ================================================================
    #region CentrifugalFan

    /// <summary>設計流量で UpdateState すると消費電力が正。</summary>
    [Fact]
    public void CentrifugalFan_DesignPoint_ElectricConsumptionIsPositive()
    {
      double nf = 13500.0 / 3600; double df = 13000.0 / 3600;
      var fan = new CentrifugalFan(0.55, nf, 0.53, df, 4, false);
      fan.UpdateState(df);
      Assert.True(fan.GetElectricConsumption() > 0);
    }

    /// <summary>
    /// 部分負荷（60%）時に INV 制御は非 INV より消費電力が小さい。
    /// （CircuitTester.CentrifugalFanTest の比較）
    /// </summary>
    [Fact]
    public void CentrifugalFan_Inverter_LessPowerAtPartialLoad()
    {
      double nf = 13500.0 / 3600; double df = 13000.0 / 3600;
      var fanFixed = new CentrifugalFan(0.55, nf, 0.53, df, 4, false);
      var fanInv = new CentrifugalFan(0.55, nf, 0.53, df, 4, true);
      double q60 = df * 0.6;
      fanFixed.UpdateState(q60); fanInv.UpdateState(q60);
      Assert.True(fanInv.GetElectricConsumption() < fanFixed.GetElectricConsumption(),
          $"INV={fanInv.GetElectricConsumption():F3} kW  <  Fixed={fanFixed.GetElectricConsumption():F3} kW");
    }

    /// <summary>INV 制御ありのファンで流量を下げると消費電力が低下する。</summary>
    [Fact]
    public void CentrifugalFan_Inverter_PowerDecreasesWithFlowRate()
    {
      double nf = 13500.0 / 3600; double df = 13000.0 / 3600;
      var fan = new CentrifugalFan(0.55, nf, 0.53, df, 4, true);
      fan.UpdateState(df); double powerFull = fan.GetElectricConsumption();
      fan.UpdateState(df * 0.6); double power60 = fan.GetElectricConsumption();
      Assert.True(powerFull > power60,
          $"Full={powerFull:F3} kW  >  60%={power60:F3} kW");
    }

    #endregion

    // ================================================================
    #region Regulator

    /// <summary>
    /// 設計差圧・設計流量で UpdateLift を呼ぶと Lift が 1.0 になる。
    /// UpdateLift(pressure) は「この差圧で VolumetricFlowRateSetPoint の流量を
    /// 流すために必要な開度」を逆算する。
    /// 差圧が設計点以下の場合（必要抵抗 &lt; 最小抵抗）は全開（Lift=1.0）になる。
    /// </summary>
    [Fact]
    public void Regulator_DesignPressure_LiftIsOne()
    {
      var reg = new Regulator(0.03, 100, 0.5, 0.5);
      reg.VolumetricFlowRateSetpoint = 0.03;
      reg.UpdateLift(100); // 設計差圧 = 設計点 → 全開
      Assert.InRange(reg.Lift, 0.99, 1.01);
    }

    /// <summary>
    /// 開度（Lift）を小さくすると GetResistance() が変化する。
    /// Lift=1（全開）が設計点（minResistance）。
    /// Lift の変化によって抵抗が変わり、同一差圧下で流量が変化する。
    /// </summary>
    [Fact]
    public void Regulator_Lift_AffectsResistance()
    {
      var reg = new Regulator(0.03, 100, 0.5, 0.5);
      reg.Lift = 1.0;
      double r1 = reg.GetResistance();
      reg.Lift = 0.5;
      double r2 = reg.GetResistance();
      // Lift が変わると抵抗が変わる（値は実装依存だが変化すること自体を確認）
      Assert.NotEqual(r1, r2);
    }

    /// <summary>
    /// 差圧が設計点を下回る場合は常に全開（Lift=1.0）になる。
    /// </summary>
    [Fact]
    public void Regulator_PressureBelowDesign_LiftIsOne()
    {
      var reg = new Regulator(0.03, 100, 0.5, 0.5);
      reg.VolumetricFlowRateSetpoint = 0.03;
      reg.UpdateLift(50); // 設計差圧の半分 → 必要抵抗 < minResistance
      Assert.InRange(reg.Lift, 0.99, 1.01);
    }

    #endregion

    // ================================================================
    #region ControllableFlowSolver

    /// <summary>
    /// 設計流量より低い流量設定では必要最低差圧が下がる。
    /// （CircuitTester.testControllableFlow の最小差圧カーブ検証）
    /// </summary>
    [Fact]
    public void ControllableParallelFlow_MinimumPressure_DecreasesAtPartialLoad()
    {
      var ahu00 = new ControllableSeriesFlow(1.121e6, 2.354e7);
      var ahu01 = new ControllableSeriesFlow(1.455e6, 3.055e7);
      var ahu10 = new ControllableSeriesFlow(1.121e6, 2.130e7);
      var ahu11 = new ControllableSeriesFlow(1.455e6, 2.764e7);

      var floor0 = new ControllableParallelFlow(
          new IFlowControllableBranch[] { ahu00, ahu01 });
      var floor1 = new ControllableParallelFlow(
          new IFlowControllableBranch[] { ahu10, ahu11 });
      var system = new ControllableParallelFlow(
          new IFlowControllableBranch[] { floor0, floor1 });

      // 設計流量
      ahu00.FlowRateSetpoint = ahu10.FlowRateSetpoint = 2.11e-3;
      ahu01.FlowRateSetpoint = ahu11.FlowRateSetpoint = 1.85e-3;
      double pFull = system.GetMinPressure();

      // 50% に低減
      ahu00.FlowRateSetpoint = ahu10.FlowRateSetpoint = 2.11e-3 * 0.5;
      ahu01.FlowRateSetpoint = ahu11.FlowRateSetpoint = 1.85e-3 * 0.5;
      double pHalf = system.GetMinPressure();

      Assert.True(pFull > pHalf,
          $"Full={pFull:F1} Pa  >  50%={pHalf:F1} Pa");
    }

    /// <summary>最小必要差圧は常に正。</summary>
    [Fact]
    public void ControllableParallelFlow_MinimumPressure_IsPositive()
    {
      var ahu0 = new ControllableSeriesFlow(1.121e6, 2.354e7);
      var ahu1 = new ControllableSeriesFlow(1.455e6, 3.055e7);
      var system = new ControllableParallelFlow(
          new IFlowControllableBranch[] { ahu0, ahu1 });
      ahu0.FlowRateSetpoint = 2.11e-3;
      ahu1.FlowRateSetpoint = 1.85e-3;
      Assert.True(system.GetMinPressure() > 0);
    }

    /// <summary>ControllableSeriesFlow：設計流量でのシステム全抵抗は正。</summary>
    [Fact]
    public void ControllableSeriesFlow_TotalResistance_IsPositive()
    {
      var ahu = new ControllableSeriesFlow(1.121e6, 2.354e7);
      ahu.FlowRateSetpoint = 2.11e-3;
      Assert.True(ahu.GetTotalResistance() > 0);
    }

    #endregion
 
    #region Flow notches

    private static CentrifugalFan MakeNotchedFan()
    {
      var fan = new CentrifugalFan(0.15, 0.14, 0.15, 0.14, 3, false);
      fan.SetFlowNotches(("微弱", 0.06), ("弱", 0.084), ("強", 0.108), ("特強", 0.111));
      return fan;
    }

    /// <summary>ノッチ設定と番号・名前による切替が機能する</summary>
    [Fact]
    public void FlowNotch_SetAndSelect()
    {
      var fan = MakeNotchedFan();
      Assert.Equal(4, fan.NotchCount);
      Assert.Equal(-1, fan.CurrentNotchIndex); //設定直後は未選択
      Assert.Equal("", fan.CurrentNotchName);

      fan.SetNotch(2);
      Assert.Equal(2, fan.CurrentNotchIndex);
      Assert.Equal("強", fan.CurrentNotchName);
      Assert.Equal(0.108, fan.VolumetricFlowRate, precision: 12);
      Assert.False(fan.IsShutOff);

      fan.SetNotch("微弱");
      Assert.Equal(0, fan.CurrentNotchIndex);
      Assert.Equal(0.06, fan.VolumetricFlowRate, precision: 12);

      Assert.Equal("特強", fan.GetNotchName(3));
      Assert.Equal(0.111, fan.GetNotchFlowRate(3), precision: 12);
    }

    /// <summary>RaiseNotch/LowerNotchの階段動作（未選択からのRaiseは最小ノッチ、最小からのLowerは変化なし）</summary>
    [Fact]
    public void FlowNotch_RaiseAndLower()
    {
      var fan = MakeNotchedFan();

      Assert.True(fan.RaiseNotch());   //未選択 → 最小ノッチ
      Assert.Equal(0, fan.CurrentNotchIndex);
      Assert.True(fan.RaiseNotch());
      Assert.True(fan.RaiseNotch());
      Assert.True(fan.RaiseNotch());
      Assert.False(fan.RaiseNotch());  //既に最大
      Assert.Equal(3, fan.CurrentNotchIndex);

      Assert.True(fan.LowerNotch());
      Assert.True(fan.LowerNotch());
      Assert.True(fan.LowerNotch());
      Assert.False(fan.LowerNotch()); //既に最小（停止はしない）
      Assert.Equal(0, fan.CurrentNotchIndex);
      Assert.Equal(0.06, fan.VolumetricFlowRate, precision: 12);
    }

    /// <summary>ShutOffでノッチ運転から外れる</summary>
    [Fact]
    public void FlowNotch_InvalidatedByShutOff()
    {
      var fan = MakeNotchedFan();
      fan.SetNotch(1);
      fan.ShutOff();
      Assert.Equal(-1, fan.CurrentNotchIndex);
      Assert.Equal(0.0, fan.VolumetricFlowRate);
    }

    /// <summary>不正なノッチ設定・指定で例外が発生する</summary>
    [Fact]
    public void FlowNotch_InvalidArguments_Throw()
    {
      var fan = new CentrifugalFan(0.15, 0.14, 0.15, 0.14, 3, false);
      //未設定での操作
      Assert.Throws<PopoloArgumentException>(() => fan.SetNotch(0));
      Assert.Throws<PopoloArgumentException>(() => fan.RaiseNotch());
      //昇順でない・非正・重複名・空名
      Assert.Throws<PopoloArgumentException>(
        () => fan.SetFlowNotches(("強", 0.12), ("弱", 0.06)));
      Assert.Throws<PopoloArgumentException>(
        () => fan.SetFlowNotches(("弱", 0.0), ("強", 0.12)));
      Assert.Throws<PopoloArgumentException>(
        () => fan.SetFlowNotches(("弱", 0.06), ("弱", 0.12)));
      Assert.Throws<PopoloArgumentException>(
        () => fan.SetFlowNotches(("", 0.06)));

      //範囲外・未知の名前
      fan.SetFlowNotches(("弱", 0.06), ("強", 0.12));
      Assert.Throws<PopoloOutOfRangeException>(() => fan.SetNotch(2));
      Assert.Throws<PopoloArgumentException>(() => fan.SetNotch("中"));
    }

    #endregion

    // ================================================================
    #region Regression tests (rotation ratio scaling of the PQ characteristic)

    /// <summary>
    /// 試験用の PQ 特性係数 f(x) = -x² + x + 0.75（高次から順）。
    /// 頂点 x = 0.5（f = 1.0）、締切（f = 0）は x = 1.5。
    /// </summary>
    private static readonly double[] TestPressureCoef = { -1.0, 1.0, 0.75 };

    /// <summary>
    /// 回転数比 r &lt; 1 でも GetFlowRate は相似則 P = r²·f(Q/r) の解を返す。
    /// 解は [r·頂点流量, r·締切流量] にあり、未スケールの区間では挟めず例外になっていた。
    /// </summary>
    [Fact]
    public void FluidMachinery_GetFlowRate_ReducedRotationRatio_SatisfiesAffinityLaw()
    {
      double r = 0.4;
      double p = 0.1; // r²·f(頂点) = 0.16 未満、r²·f(x=1.25) = 0.07 超
      double q = FluidMachinery.GetFlowRate(r, TestPressureCoef, p, 0.5);

      // 解析解: r²(-x² + x + 0.75) = p, x = Q/r（下降側の根）
      double x = (1 + Math.Sqrt(1 + 4 * (0.75 - p / (r * r)))) / 2;
      Assert.InRange(q, r * x - 1e-3, r * x + 1e-3);
      double pCheck = FluidMachinery.GetPressure(q, r, TestPressureCoef);
      Assert.InRange(pCheck, p - 1e-3, p + 1e-3);
    }

    /// <summary>圧力ゼロ以下では締切流量（相似則により r 倍）を返す。</summary>
    [Fact]
    public void FluidMachinery_GetFlowRate_ZeroPressure_ReturnsScaledZeroHeadFlow()
    {
      double r = 0.4;
      double q = FluidMachinery.GetFlowRate(r, TestPressureCoef, 0.0, 0.5);
      Assert.InRange(q, r * 1.5 - 1e-9, r * 1.5 + 1e-9);
      // r = 1 は従来通り
      Assert.InRange(FluidMachinery.GetFlowRate(1.0, TestPressureCoef, 0.0, 0.5), 1.5 - 1e-9, 1.5 + 1e-9);
    }

    /// <summary>
    /// 回転数比 0.7 のポンプを含む回路網：解いたポンプ差圧と流量は
    /// GetPressure（相似則 P = r²·f(Q/r)）と整合する。
    /// </summary>
    [Fact]
    public void FluidMachinery_NetworkWithReducedRotationRatio_ConsistentWithGetPressure()
    {
      var pump = new CentrifugalPump(
          260, 0.03, 250, 0.03,
          CentrifugalPump.ControlMethod.ConstantPressureWithInverter, 40);
      pump.SetPressureCoefficient(300, 0, -1.0e5); // P = 300 - 1e5·Q² [kPa]
      pump.RotationRatio = 0.7;
      var load = new SimpleCircuitBranch(0.02, 100);

      var net = new CircuitNetwork();
      var n0 = net.AddNode();
      var n1 = net.AddNode();
      net.ConnectNode(pump, n0, n1);
      net.ConnectNode(load, n1, n0);
      net.SetBasePressure(n0, 0);
      Assert.True(net.Solve(), "Network should converge");

      double dp = n1.Pressure - n0.Pressure;
      double q = pump.VolumetricFlowRate;
      Assert.InRange(q - load.VolumetricFlowRate, -1e-6, 1e-6);
      // P = r²·300 - 1e5·Q² （相似則）と抵抗 P = 2.5e5·Q² の交点
      double qExpected = Math.Sqrt(0.49 * 300 / (1.0e5 + 2.5e5));
      Assert.InRange(q, qExpected * 0.999, qExpected * 1.001);
      double pCheck = FluidMachinery.GetPressure(q, 0.7, new double[] { -1.0e5, 0, 300 });
      Assert.InRange(dp, pCheck - 0.01, pCheck + 0.01);
    }

    /// <summary>回転数比 1.0 の場合の結果は従来式（r²f(Q) = dp）と変わらない。</summary>
    [Fact]
    public void FluidMachinery_NetworkAtFullSpeed_MatchesCharacteristic()
    {
      var pump = new CentrifugalPump(
          260, 0.03, 250, 0.03,
          CentrifugalPump.ControlMethod.ConstantPressureWithInverter, 40);
      pump.SetPressureCoefficient(300, 0, -1.0e5);
      pump.RotationRatio = 1.0;
      var load = new SimpleCircuitBranch(0.02, 100);

      var net = new CircuitNetwork();
      var n0 = net.AddNode();
      var n1 = net.AddNode();
      net.ConnectNode(pump, n0, n1);
      net.ConnectNode(load, n1, n0);
      net.SetBasePressure(n0, 0);
      Assert.True(net.Solve(), "Network should converge");

      double qExpected = Math.Sqrt(300 / (1.0e5 + 2.5e5));
      Assert.InRange(pump.VolumetricFlowRate, qExpected * 0.999, qExpected * 1.001);
    }

    #endregion

    // ================================================================
    #region Regression tests (SeriesBranch)

    /// <summary>受動抵抗 2 本の直列：合成抵抗 R1 + R2 による流量になる。</summary>
    [Fact]
    public void SeriesBranch_TwoPassiveResistances_FlowMatchesCombinedResistance()
    {
      var r1 = new SimpleCircuitBranch(0.02, 100); // R = 2.5e5
      var r2 = new SimpleCircuitBranch(0.02, 100);
      var sb = new SeriesBranch(r1, r2);
      sb.UpStreamNode = new CircuitNode { Pressure = 100 };
      sb.DownStreamNode = new CircuitNode { Pressure = 0 };
      sb.UpdateFlowRateFromNodePressureDifference();

      double expected = Math.Sqrt(100 / 5.0e5);
      Assert.InRange(sb.VolumetricFlowRate, expected * (1 - 1e-6), expected * (1 + 1e-6));
      Assert.InRange(r1.VolumetricFlowRate - r2.VolumetricFlowRate, -1e-9, 1e-9);
    }

    /// <summary>
    /// ポンプと抵抗の直列（両端同圧）：中間圧は両端の区間外にあるため、
    /// 区間を拡張して解き、ポンプ揚程 = 抵抗の圧力損失となる流量を返す。
    /// </summary>
    [Fact]
    public void SeriesBranch_PumpAndResistance_ExpandsBracketAndSolves()
    {
      var pump = new CentrifugalPump(
          260, 0.03, 250, 0.03,
          CentrifugalPump.ControlMethod.ConstantPressureWithInverter, 40);
      pump.SetPressureCoefficient(300, 0, -1.0e5);
      pump.RotationRatio = 1.0;
      var load = new SimpleCircuitBranch(0.02, 100); // R = 2.5e5
      var sb = new SeriesBranch(pump, load);
      sb.UpStreamNode = new CircuitNode { Pressure = 0 };
      sb.DownStreamNode = new CircuitNode { Pressure = 0 };
      sb.UpdateFlowRateFromNodePressureDifference();

      double expected = Math.Sqrt(300 / (1.0e5 + 2.5e5));
      Assert.InRange(sb.VolumetricFlowRate, expected * (1 - 1e-6), expected * (1 + 1e-6));
      Assert.InRange(pump.VolumetricFlowRate - load.VolumetricFlowRate, -1e-9, 1e-9);
    }

    #endregion

    // ================================================================
    #region Regression tests (WaterPipe heat flow)

    /// <summary>
    /// 配管熱損失のエネルギー収支：HeatLoss = ρ·V·cp·(出口 − 入口)。
    /// 体積流量 [m³/s] は密度を乗じて質量流量 [kg/s] に換算する。
    /// </summary>
    [Fact]
    public void WaterPipe_UpdateHeatFlow_EnergyBalance()
    {
      var pipe = new WaterPipe(50, 0.05, WaterPipe.Material.CarbonSteel);
      pipe.SetPipeThermalConductivity(50); // 炭素鋼（既定値は 0 のため明示設定）
      double tIn = 7.0;
      double vf = 0.002; // [m³/s]
      pipe.UpdateHeatFlow(tIn, vf, 30.0, 0.015);

      double mw = vf * Popolo.Core.Physics.Water.GetLiquidDensity(tIn);
      double cp = Popolo.Core.Physics.Water.GetLiquidIsobaricSpecificHeat(tIn);
      double q = mw * cp * (pipe.OutletWaterTemperature - tIn);
      Assert.True(pipe.HeatLoss > 0, $"HeatLoss = {pipe.HeatLoss} kW should be positive (heat gain)");
      Assert.InRange(q, pipe.HeatLoss * (1 - 1e-9), pipe.HeatLoss * (1 + 1e-9));
      Assert.InRange(pipe.OutletWaterTemperature - tIn, 1e-4, 1.0); // 50 m の裸管で温度上昇は小さい
    }

    /// <summary>
    /// 配管の流量計算は逆流（下流側が高圧）でも順流と対称な流量を返す。
    /// 旧実装は負の流速で Reynolds 数が負になり摩擦係数が NaN となって、
    /// 誤った流量を黙って返していた。
    /// </summary>
    [Theory]
    [InlineData(0.5)]
    [InlineData(5.0)]
    [InlineData(50.0)]
    public void WaterPipe_ReverseFlow_IsSymmetricToForwardFlow(double dpKPa)
    {
      var pipe = new WaterPipe(30, 0.1, WaterPipe.Material.CarbonSteel);
      pipe.UpStreamNode = new CircuitNode { Pressure = dpKPa };
      pipe.DownStreamNode = new CircuitNode { Pressure = 0 };
      pipe.UpdateFlowRateFromNodePressureDifference();
      double forward = pipe.VolumetricFlowRate;

      pipe.UpStreamNode.Pressure = 0;
      pipe.DownStreamNode.Pressure = dpKPa;
      pipe.UpdateFlowRateFromNodePressureDifference();
      double reverse = pipe.VolumetricFlowRate;

      Assert.True(forward > 0, $"forward={forward}");
      Assert.InRange(reverse, -forward * (1 + 1e-6), -forward * (1 - 1e-6));
      // 圧力損失は流量と整合する
      Assert.InRange(pipe.GetPressureDrop(forward), dpKPa * 0.999, dpKPa * 1.001);
    }

    /// <summary>圧力損失は流量ゼロで 0、逆流では符号が反転する（旧実装は流量ゼロで NaN）。</summary>
    [Fact]
    public void WaterPipe_GetPressureDrop_ZeroAndReverseFlow()
    {
      var pipe = new WaterPipe(30, 0.1, WaterPipe.Material.CarbonSteel);
      Assert.Equal(0.0, pipe.GetPressureDrop(0.0));
      double dp = pipe.GetPressureDrop(0.01);
      Assert.True(dp > 0);
      Assert.Equal(-dp, pipe.GetPressureDrop(-0.01), 12);
    }

    /// <summary>生成直後（初期化計算後）の体積流量は流速 2 m/s 相当 [m³/s] である。</summary>
    [Fact]
    public void WaterPipe_Initialize_VolumetricFlowRateIsTwoMetersPerSecond()
    {
      var pipe = new WaterPipe(10, 0.05, WaterPipe.Material.CarbonSteel);
      double expected = 0.05 * 0.05 / 4 * Math.PI * 2;
      Assert.InRange(pipe.VolumetricFlowRate, expected * (1 - 1e-9), expected * (1 + 1e-9));
    }

    #endregion

    // ================================================================
    #region Regression tests (Regulator.UpdateLift)

    /// <summary>流量設定値 0 では UpdateLift(pressure) は全閉（Lift = 0）とし NaN にならない。</summary>
    [Fact]
    public void Regulator_UpdateLift_ZeroSetpoint_LiftIsZero()
    {
      var reg = new Regulator(0.03, 100, 50, 0.5);
      reg.VolumetricFlowRateSetpoint = 0;
      reg.UpdateLift(100);
      Assert.Equal(0.0, reg.Lift);
      reg.UpdateLift(0);
      Assert.Equal(0.0, reg.Lift);
      reg.VolumetricFlowRateSetpoint = -0.01;
      reg.UpdateLift(100);
      Assert.Equal(0.0, reg.Lift);
    }

    /// <summary>流量設定値 0 では UpdateLift()（ノード差圧版）も全閉。</summary>
    [Fact]
    public void Regulator_UpdateLiftFromNodes_ZeroSetpoint_LiftIsZero()
    {
      var reg = new Regulator(0.03, 100, 50, 0.5);
      reg.UpStreamNode = new CircuitNode { Pressure = 100 };
      reg.DownStreamNode = new CircuitNode { Pressure = 0 };
      reg.VolumetricFlowRateSetpoint = 0;
      reg.UpdateLift();
      Assert.Equal(0.0, reg.Lift);
    }

    /// <summary>
    /// 全閉時の抵抗でも流量設定値まで絞れない低設定値では Lift = 0（最小開度）。
    /// </summary>
    [Fact]
    public void Regulator_UpdateLift_VeryLowSetpoint_LiftIsZero()
    {
      var reg = new Regulator(0.03, 100, 50, 0.5);
      reg.VolumetricFlowRateSetpoint = 0.03 / 1000;
      reg.UpdateLift(100);
      Assert.Equal(0.0, reg.Lift);
    }

    /// <summary>
    /// 中間の流量設定値では、求めた開度の抵抗で設定流量が流れる（開度は 0〜1）。
    /// </summary>
    [Theory]
    [InlineData(0.02)]
    [InlineData(0.01)]
    [InlineData(0.003)]
    [InlineData(0.001)]
    public void Regulator_UpdateLift_IntermediateSetpoint_AchievesSetpoint(double setpoint)
    {
      var reg = new Regulator(0.03, 100, 50, 0.5);
      reg.VolumetricFlowRateSetpoint = setpoint;
      reg.UpdateLift(100);
      Assert.InRange(reg.Lift, 0.0, 1.0);
      double q = Math.Sqrt(100 / reg.GetResistance());
      Assert.InRange(q, setpoint * (1 - 1e-4), setpoint * (1 + 1e-4));
    }

    #endregion
  }
}