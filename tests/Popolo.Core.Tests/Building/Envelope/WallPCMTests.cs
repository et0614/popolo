/* WallPCMTests.cs
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
using Popolo.Core.Building.Envelope;

namespace Popolo.Core.Tests.Building.Envelope
{
  /// <summary>PCM 層を含む Wall の統合テスト（相変化時のエネルギー保存・水分計算モード）。</summary>
  /// <remarks>
  /// 相変化時の温度補正の考え方：
  ///   節点 i の熱容量は capS[i] = layers[i-1].HeatCapacity_B + layers[i].HeatCapacity_F。
  ///   PCM 半層の熱容量が cap1（旧相）→ cap2（新相）に変わる節点で、隣接半層の熱容量を cap3、
  ///   相変化温度を Tt とすると、旧熱容量で解いた温度 T からエンタルピーを保存する補正後温度は
  ///     (cap1 + cap3)(T − T0) = cap1(Tt − T0) + cap2(T' − Tt) + cap3(T' − T0)
  ///     ⇒ T' = ((cap1 + cap3)·T + (cap2 − cap1)·Tt) / (cap2 + cap3)
  ///   ここで cap3 は「同じ節点を共有する隣接層側の半層熱容量」であり、
  ///   F 側（節点 lnum）では layers[lnum-1].HeatCapacity_B、
  ///   B 側（節点 lnum+1）では layers[lnum+1].HeatCapacity_F（最終層なら 0）。
  /// </remarks>
  public class WallPCMTests
  {
    #region Test constants and helpers

    private const double FreezingTemp = 18.0; // 凝固点[°C]
    private const double MeltingTemp = 20.0;  // 融点[°C]
    private const double PcmThickness = 0.02; // [m]
    private const double CpSolid = 1500.0;    // 固体容積比熱[kJ/(m³·K)]
    private const double CpEquil = 20000.0;   // 平衡（潜熱込み）容積比熱[kJ/(m³·K)]
    private const double CpLiquid = 1200.0;   // 液体容積比熱[kJ/(m³·K)]

    private const double CpConc = 1000.0;     // 通常層の容積比熱[kJ/(m³·K)]
    private const double ConcThickness = 0.05;

    /// <summary>典型的な PCM 層を生成する。</summary>
    private static PCMWallLayer MakePCM()
    {
      var solid = new WallLayer("PCM-S", 0.5, CpSolid, PcmThickness);
      var equil = new WallLayer("PCM-E", 0.4, CpEquil, PcmThickness);
      var liquid = new WallLayer("PCM-L", 0.3, CpLiquid, PcmThickness);
      return new PCMWallLayer("PCM", FreezingTemp, MeltingTemp, PcmThickness, solid, equil, liquid);
    }

    /// <summary>通常層（顕熱のみ）を生成する。</summary>
    private static WallLayer MakeConcrete() => new WallLayer("Conc", 1.0, CpConc, ConcThickness);

    /// <summary>PCM 半層の単位面積あたりエンタルピー [J/m²]（凝固点基準、区分線形・連続）。</summary>
    private static double PcmHalfEnthalpy(double t)
    {
      double f = 0.5 * PcmThickness * 1000.0;
      if (t <= FreezingTemp) return f * CpSolid * (t - FreezingTemp);
      if (t < MeltingTemp) return f * CpEquil * (t - FreezingTemp);
      return f * (CpEquil * (MeltingTemp - FreezingTemp) + CpLiquid * (t - MeltingTemp));
    }

    /// <summary>壁全体の単位面積あたりエンタルピー [J/m²]。</summary>
    private static double WallEnthalpy(Wall wall)
    {
      var layers = wall.Layers;
      var t = wall.Temperatures;
      double e = 0;
      for (int i = 0; i < layers.Length; i++)
      {
        if (layers[i] is PCMWallLayer)
          e += PcmHalfEnthalpy(t[i]) + PcmHalfEnthalpy(t[i + 1]);
        else
          e += layers[i].HeatCapacity_F * t[i] + layers[i].HeatCapacity_B * t[i + 1];
      }
      return e;
    }

    /// <summary>
    /// 片側から加熱（反対側はほぼ断熱）して PCM を固相→液相へ相変化させ、
    /// 表面からの流入熱量と壁のエンタルピー変化が一致することを確認する。
    /// </summary>
    /// <remarks>
    /// 流入熱量は更新後の表面温度から評価するため、相変化補正で表面節点の温度が
    /// 書き換わる側（PCM が表面層となる側）は断熱側にする。
    /// </remarks>
    private static void AssertEnergyConservedThroughPhaseChange(Wall wall, int pcmIndex, bool heatFromF = true)
    {
      const double dt = 60.0;
      wall.TimeStep = dt;
      // 0 だと resS=Inf になるので微小値
      if (heatFromF) { wall.ConvectiveCoefficientB = 1e-6; wall.RadiativeCoefficientB = 0.0; }
      else { wall.ConvectiveCoefficientF = 1e-6; wall.RadiativeCoefficientF = 0.0; }
      wall.Initialize(10.0);

      var pcm = (PCMWallLayer)wall.Layers[pcmIndex];
      Assert.Equal(PCMWallLayer.State.Solid, pcm.CurrentState_F);
      Assert.Equal(PCMWallLayer.State.Solid, pcm.CurrentState_B);

      double e0 = WallEnthalpy(wall);
      double qIn = 0;
      wall.SolAirTemperatureF = heatFromF ? 30.0 : 10.0;
      wall.SolAirTemperatureB = heatFromF ? 10.0 : 30.0;
      int phaseChanges = 0;
      for (int step = 0; step < 6000; step++)
      {
        wall.Update();
        if (pcm.CurrentState_F != pcm.LastState_F) phaseChanges++;
        if (pcm.CurrentState_B != pcm.LastState_B) phaseChanges++;
        // 後退差分：流入熱流は更新後の表面温度で評価される
        qIn += dt * wall.FilmCoefficientF * (wall.SolAirTemperatureF - wall.Temperatures[0]);
        qIn += dt * wall.FilmCoefficientB * (wall.SolAirTemperatureB - wall.Temperatures[wall.NodeCount - 1]);
      }

      // 両端とも固相→平衡→液相の 2 回ずつ相変化した
      Assert.Equal(PCMWallLayer.State.Liquid, pcm.CurrentState_F);
      Assert.Equal(PCMWallLayer.State.Liquid, pcm.CurrentState_B);
      Assert.True(phaseChanges >= 4, $"phase changes = {phaseChanges}");

      double dE = WallEnthalpy(wall) - e0;
      Assert.True(Math.Abs(dE - qIn) <= 1e-7 * Math.Abs(qIn),
          $"energy imbalance: ΔE={dE:F3} J/m², Q_in={qIn:F3} J/m², diff={dE - qIn:E3}");
    }

    #endregion

    #region Energy conservation across phase change

    /// <summary>中間層の PCM が相変化してもエネルギーが保存される。</summary>
    /// <remarks>
    /// かつて B 側補正で隣接層 layers[lnum+1].HeatCapacity_F ではなく PCM 自身の
    /// HeatCapacity_B（新相の値）を使っていたため、相変化のたびにエネルギーが増減していた（回帰テスト）。
    /// </remarks>
    [Fact]
    public void Update_InteriorPCM_ConservesEnergyThroughPhaseChange()
    {
      var wall = new Wall(1.0, new[] { MakeConcrete(), MakePCM(), MakeConcrete() });
      AssertEnergyConservedThroughPhaseChange(wall, 1);
    }

    /// <summary>最終層（B 表面側）の PCM が相変化してもエネルギーが保存される。</summary>
    /// <remarks>
    /// かつて最終層の B 側補正が B 表面節点（layers.Length）ではなく 1 つ手前の節点
    /// （layers.Length-1）に適用されていた（回帰テスト）。
    /// </remarks>
    [Fact]
    public void Update_LastLayerPCM_ConservesEnergyThroughPhaseChange()
    {
      var wall = new Wall(1.0, new WallLayer[] { MakeConcrete(), MakePCM() });
      AssertEnergyConservedThroughPhaseChange(wall, 1);
    }

    /// <summary>F 表面側（第 0 層）の PCM が相変化してもエネルギーが保存される。</summary>
    [Fact]
    public void Update_FirstLayerPCM_ConservesEnergyThroughPhaseChange()
    {
      var wall = new Wall(1.0, new WallLayer[] { MakePCM(), MakeConcrete() });
      AssertEnergyConservedThroughPhaseChange(wall, 0, heatFromF: false);
    }

    /// <summary>PCM 層が 2 枚連続する（共有節点で両側とも相変化する）場合もエネルギーが保存される。</summary>
    /// <remarks>
    /// F 側補正の隣接熱容量は、共有節点側である layers[lnum-1].HeatCapacity_B でなければならない。
    /// 隣接層も PCM で F/B の相が異なると HeatCapacity_F ≠ HeatCapacity_B となり差が出る。
    /// B 側から加熱すると、共有節点で前段 PCM の B 側が先に相変化した時点で前段 PCM の
    /// F 側はまだ旧相のため、この差が現れる（F 側補正の回帰テスト）。
    /// </remarks>
    [Fact]
    public void Update_AdjacentPCMLayers_ConservesEnergyThroughPhaseChange()
    {
      var wall = new Wall(1.0, new WallLayer[] { MakeConcrete(), MakePCM(), MakePCM(), MakeConcrete() });
      AssertEnergyConservedThroughPhaseChange(wall, 2, heatFromF: false);
    }

    #endregion

    #region Moisture transfer mode

    /// <summary>
    /// 水分移動計算モードでも PCM 層の相変化が評価され、湿気容量・吸放湿のない条件では
    /// 顕熱のみの計算と同一の温度履歴になる。
    /// </summary>
    /// <remarks>
    /// かつて水分モードでは 4 引数の UpdateState が呼ばれ、基底 WallLayer の実装（常に false）が
    /// 使われていたため、PCMWallLayer / HorizontalAirChamber の物性が一切更新されなかった（回帰テスト）。
    /// κ=ν=0 の層では温度行と湿度行が分離するため、温度解は顕熱のみの解と一致する。
    /// </remarks>
    [Fact]
    public void Update_MoistureMode_PCMStateIsUpdated_SameAsHeatOnly()
    {
      WallLayer MakeMoistLayer() => new WallLayer("Board", 1.0, CpConc, 1e-6, 0.5, 0.0, 0.0, ConcThickness);
      var wallM = new Wall(1.0, new WallLayer[] { MakeMoistLayer(), MakePCM() }, computeMoistureTransfer: true);
      var wallH = new Wall(1.0, new WallLayer[] { MakeConcrete(), MakePCM() });

      foreach (var w in new[] { wallM, wallH })
      {
        w.TimeStep = 60.0;
        w.ConvectiveCoefficientB = 1e-6;
        w.RadiativeCoefficientB = 0.0;
      }
      wallM.Initialize(10.0, 0.008);
      wallH.Initialize(10.0);
      wallM.HumidityRatioF = wallM.HumidityRatioB = 0.008;
      wallM.SolAirTemperatureF = wallH.SolAirTemperatureF = 30.0;
      wallM.SolAirTemperatureB = wallH.SolAirTemperatureB = 10.0;

      // 過渡中（相変化中）の温度履歴全体で比較する
      double maxDiff = 0;
      for (int step = 0; step < 6000; step++)
      {
        wallM.Update();
        wallH.Update();
        for (int n = 0; n < wallM.NodeCount; n++)
          maxDiff = Math.Max(maxDiff, Math.Abs(wallH.Temperatures[n] - wallM.Temperatures[n]));
      }

      var pcmM = (PCMWallLayer)wallM.Layers[1];
      Assert.Equal(PCMWallLayer.State.Liquid, pcmM.CurrentState_F);
      Assert.Equal(PCMWallLayer.State.Liquid, pcmM.CurrentState_B);
      Assert.True(maxDiff < 1e-6, $"max |T_moist − T_heatOnly| = {maxDiff:E3} °C");
    }

    #endregion
  }
}
