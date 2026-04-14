/* TanabeMultiNodeModelTests.cs
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
using System.Linq;
using Xunit;
using Popolo.Core.ThermalComfort;

namespace Popolo.Core.Tests.ThermalComfort
{
  /// <summary>Integration tests for <see cref="TanabeMultiNodeModel"/>.</summary>
  /// <remarks>
  /// Standard reference body: male, 74.43 kg, 1.72 m, 25 years, 15% fat, standing.
  ///
  /// The constructor runs a 24-hour warm-up at 28.8 °C / 1 met internally.
  /// To keep conditions consistent with the warm-up, RunEnvironment() also uses 1 met.
  ///
  /// For shivering-specific tests, RunEnvironmentBasalOnly() sets met = 0 so that
  ///   MetabolicRate - BasalMetabolicRate = shivering heat production only.
  /// Because the warm-up used 1 met, there is a transient when switching to 0 met,
  /// so a 10% threshold is used instead of 5%.
  ///
  /// Environments:
  ///   Neutral  : 28.8 °C, MRT 28.8 °C, 50 % RH, 0.0 m/s
  ///   Cold     : 10 °C,   MRT 10 °C,   50 % RH, 0.0 m/s
  ///   Hot/humid: 40 °C,   MRT 40 °C,   70 % RH, 0.1 m/s
  /// </remarks>
  public class TanabeMultiNodeModelTests
  {
    #region ヘルパー

    private const double TimeStepSec = 60.0;
    private const int WarmUpSteps = 180; // 3時間

    /// <summary>
    /// 1 met でモデルを指定環境に 3 時間さらして返す。
    /// 皮膚温・顕熱損失・熱収支など代謝量に依存しない物理量のテストに使用。
    /// </summary>
    private static TanabeMultiNodeModel RunEnvironment(
        double dbt, double mrt, double rh, double vel, int steps = WarmUpSteps)
    {
      var model = new TanabeMultiNodeModel(); // 内部で 1 met / 28.8°C / 24h 暖機
      model.SetMetabolicRate(1.0);
      model.UpdateBoundary(vel, mrt, dbt, rh);
      for (int i = 0; i < steps; i++)
        model.Update(TimeStepSec);
      return model;
    }

    /// <summary>
    /// 追加活動代謝をゼロ（安静臥位相当）にしてモデルを指定環境に 3 時間さらして返す。
    /// <see cref="TanabeMultiNodeModel.SetMetabolicRate"/> の引数に 0 を渡すと、
    /// 基礎代謝（<see cref="TanabeMultiNodeModel.BasalMetabolicRate"/>）のみが計上され、
    /// 追加の活動代謝は加算されない。
    /// その結果、MetabolicRate - BasalMetabolicRate が震え熱産生のみを表す。
    /// コンストラクタ内の暖機（1 met）との不整合による過渡応答が若干残るため、
    /// 閾値判定には 10% を使用すること。
    /// </summary>
    private static TanabeMultiNodeModel RunEnvironmentBasalOnly(
        double dbt, double mrt, double rh, double vel, int steps = WarmUpSteps)
    {
      var model = new TanabeMultiNodeModel();
      model.SetMetabolicRate(0.0); // 追加活動代謝ゼロ＝基礎代謝のみ（安静臥位相当）
      model.UpdateBoundary(vel, mrt, dbt, rh);
      for (int i = 0; i < steps; i++)
        model.Update(TimeStepSec);
      return model;
    }

    /// <summary>全部位の潜熱損失合計 [W]。</summary>
    private static double TotalLatentHeatLoss(TanabeMultiNodeModel model)
        => Enum.GetValues(typeof(TanabeMultiNodeModel.Node))
               .Cast<TanabeMultiNodeModel.Node>()
               .Sum(n => model.GetLatentHeatLoss(n));

    /// <summary>全部位の顕熱損失合計 [W]。</summary>
    private static double TotalSensibleHeatLoss(TanabeMultiNodeModel model)
        => Enum.GetValues(typeof(TanabeMultiNodeModel.Node))
               .Cast<TanabeMultiNodeModel.Node>()
               .Sum(n => model.GetSensibleHeatLoss(n));

    #endregion

    #region 基本動作テスト

    /// <summary>デフォルト体格でインスタンスを生成できる。</summary>
    [Fact]
    public void Constructor_Default_Succeeds()
    {
      var model = new TanabeMultiNodeModel();
      Assert.True(model.Weight > 0);
      Assert.True(model.Height > 0);
      Assert.True(model.SurfaceArea > 0);
      Assert.True(model.BasalMetabolicRate > 0);
    }

    /// <summary>ニュートラル環境（1 met）で胸部コア温が生理的範囲内に収まる。</summary>
    [Fact]
    public void NeutralEnvironment_CoreTemperature_InPhysiologicalRange()
    {
      var model = RunEnvironment(28.8, 28.8, 50, 0);
      double core = model.GetTemperature(
          TanabeMultiNodeModel.Node.Chest, TanabeMultiNodeModel.Layer.Core);
      // ヒトの生理的コア温度: 35–38 °C
      Assert.InRange(core, 35.0, 38.0);
    }

    #endregion

    #region 震え熱産生テスト（追加活動代謝ゼロ＝基礎代謝のみ）
    /// <remarks>
    /// 震えの制御式（式27.92）：
    ///   SIGshv = -24.36 × Min(SIGhead, 0) × Clds × RAdu
    /// SIGhead は頭部コア温とセットポイントの差、Clds は寒冷側皮膚信号の合計。
    /// SetMetabolicRate(0) にすると皮膚温が低下して Clds &gt; 0 になりやすいため、
    /// ニュートラル28.8°Cでも若干の震えが生じる場合がある。
    /// したがって「ニュートラルで震えゼロ」の絶対評価は行わず、
    /// 「寒冷時の震えが高温時より大きい」「寒冷時の震えが基礎代謝の一定割合を超える」
    /// という相対比較で検証する。
    /// </remarks>

    /// <summary>
    /// 寒冷環境（10 °C）では基礎代謝を大幅に上回る震え熱産生が生じる。
    /// SetMetabolicRate(0) で追加活動代謝をゼロにしているため、
    /// MetabolicRate - BasalMetabolicRate = 震え熱産生のみ。
    /// </summary>
    [Fact]
    public void ColdEnvironment_ShiveringHeat_ExceedsBasal()
    {
      var model = RunEnvironmentBasalOnly(10, 10, 50, 0);
      double shivering = model.MetabolicRate - model.BasalMetabolicRate;
      // 寒冷時の震え熱産生は基礎代謝の20%以上
      Assert.True(shivering > model.BasalMetabolicRate * 0.20,
          $"Cold shivering={shivering:F2} W should exceed 20% of BasalMR={model.BasalMetabolicRate:F2} W");
    }

    /// <summary>
    /// 高温環境（40 °C）では震えは寒冷時より格段に少ない。
    /// 寒冷時の震えを基準に相対比較する。
    /// </summary>
    [Fact]
    public void HotEnvironment_ShiveringHeat_MuchLessThanCold()
    {
      var cold = RunEnvironmentBasalOnly(10, 10, 50, 0);
      var hot = RunEnvironmentBasalOnly(40, 40, 70, 0.1);
      double coldShivering = cold.MetabolicRate - cold.BasalMetabolicRate;
      double hotShivering = hot.MetabolicRate - hot.BasalMetabolicRate;
      // 高温時の震えは寒冷時の20%未満
      Assert.True(hotShivering < coldShivering * 0.20,
          $"Hot shivering={hotShivering:F2} W should be < 20% of cold shivering={coldShivering:F2} W");
    }

    /// <summary>寒冷時の代謝量（= 基礎代謝 + 震え）は高温時より大きい。</summary>
    [Fact]
    public void Cold_MetabolicRate_GreaterThan_Hot()
    {
      var cold = RunEnvironmentBasalOnly(10, 10, 50, 0);
      var hot = RunEnvironmentBasalOnly(40, 40, 70, 0.1);
      Assert.True(cold.MetabolicRate > hot.MetabolicRate,
          $"Cold MR={cold.MetabolicRate:F2} W should be > Hot MR={hot.MetabolicRate:F2} W");
    }

    #endregion

    #region 発汗テスト（1 met）

    /// <summary>高温多湿環境では潜熱損失がニュートラル時の2倍以上になる（発汗）。</summary>
    [Fact]
    public void HotEnvironment_LatentHeatLoss_Increases_DueToSweating()
    {
      var hot = RunEnvironment(40, 40, 70, 0.1);
      var neutral = RunEnvironment(28.8, 28.8, 50, 0);
      double hotLatent = TotalLatentHeatLoss(hot);
      double neutralLatent = TotalLatentHeatLoss(neutral);
      Assert.True(hotLatent > neutralLatent * 2.0,
          $"Hot latent={hotLatent:F2} W should be > 2x neutral={neutralLatent:F2} W");
    }

    /// <summary>高温時の潜熱損失は寒冷時より大きい。</summary>
    [Fact]
    public void Hot_LatentHeatLoss_GreaterThan_Cold()
    {
      var cold = RunEnvironment(10, 10, 50, 0);
      var hot = RunEnvironment(40, 40, 70, 0.1);
      Assert.True(TotalLatentHeatLoss(hot) > TotalLatentHeatLoss(cold),
          $"Hot latent={TotalLatentHeatLoss(hot):F2} W should be > Cold latent={TotalLatentHeatLoss(cold):F2} W");
    }

    #endregion

    #region 皮膚温・顕熱損失テスト（1 met）

    /// <summary>寒冷環境では平均皮膚温がニュートラルより低い。</summary>
    [Fact]
    public void ColdEnvironment_MeanSkinTemperature_LowerThanNeutral()
    {
      var cold = RunEnvironment(10, 10, 50, 0);
      var neutral = RunEnvironment(28.8, 28.8, 50, 0);
      Assert.True(cold.GetAverageSkinTemperature() < neutral.GetAverageSkinTemperature(),
          $"Cold skin={cold.GetAverageSkinTemperature():F2} < Neutral skin={neutral.GetAverageSkinTemperature():F2}");
    }

    /// <summary>高温環境では平均皮膚温がニュートラルより高い。</summary>
    [Fact]
    public void HotEnvironment_MeanSkinTemperature_HigherThanNeutral()
    {
      var hot = RunEnvironment(40, 40, 70, 0.1);
      var neutral = RunEnvironment(28.8, 28.8, 50, 0);
      Assert.True(hot.GetAverageSkinTemperature() > neutral.GetAverageSkinTemperature(),
          $"Hot skin={hot.GetAverageSkinTemperature():F2} > Neutral skin={neutral.GetAverageSkinTemperature():F2}");
    }

    /// <summary>寒冷・ニュートラル・高温で平均皮膚温の順序が物理的に正しい。</summary>
    [Fact]
    public void SkinTemperature_Ordering_Cold_Neutral_Hot()
    {
      var cold = RunEnvironment(10, 10, 50, 0);
      var neutral = RunEnvironment(28.8, 28.8, 50, 0);
      var hot = RunEnvironment(40, 40, 70, 0.1);
      double tC = cold.GetAverageSkinTemperature();
      double tN = neutral.GetAverageSkinTemperature();
      double tH = hot.GetAverageSkinTemperature();
      Assert.True(tC < tN, $"Cold skin({tC:F2}) < Neutral skin({tN:F2})");
      Assert.True(tN < tH, $"Neutral skin({tN:F2}) < Hot skin({tH:F2})");
    }

    /// <summary>寒冷環境では顕熱損失がニュートラルより大きい。</summary>
    [Fact]
    public void ColdEnvironment_SensibleHeatLoss_GreaterThanNeutral()
    {
      var cold = RunEnvironment(10, 10, 50, 0);
      var neutral = RunEnvironment(28.8, 28.8, 50, 0);
      Assert.True(TotalSensibleHeatLoss(cold) > TotalSensibleHeatLoss(neutral),
          $"Cold sensible={TotalSensibleHeatLoss(cold):F2} W > Neutral={TotalSensibleHeatLoss(neutral):F2} W");
    }

    /// <summary>高温環境では顕熱損失がニュートラルより少ない（温度勾配が小さいため）。</summary>
    [Fact]
    public void HotEnvironment_SensibleHeatLoss_DecreasesOrNegative()
    {
      var hot = RunEnvironment(40, 40, 70, 0.1);
      var neutral = RunEnvironment(28.8, 28.8, 50, 0);
      Assert.True(TotalSensibleHeatLoss(hot) < TotalSensibleHeatLoss(neutral),
          $"Hot sensible={TotalSensibleHeatLoss(hot):F2} W < Neutral={TotalSensibleHeatLoss(neutral):F2} W");
    }

    /// <summary>寒冷環境では中央血液温度がニュートラルより低い。</summary>
    [Fact]
    public void ColdEnvironment_CentralBloodTemperature_Drops()
    {
      var cold = RunEnvironment(10, 10, 50, 0);
      var neutral = RunEnvironment(28.8, 28.8, 50, 0);
      Assert.True(cold.CentralBloodTemperature < neutral.CentralBloodTemperature,
          $"Cold blood={cold.CentralBloodTemperature:F2} < Neutral blood={neutral.CentralBloodTemperature:F2}");
    }

    #endregion

    #region 個別部位テスト

    /// <summary>寒冷環境では四肢末端（右手）の皮膚温が胸部コア温より大幅に低い。</summary>
    [Fact]
    public void ColdEnvironment_DistalSkinTemperature_LowerThanCoreTemperature()
    {
      var model = RunEnvironment(10, 10, 50, 0);
      double handSkin = model.GetTemperature(
          TanabeMultiNodeModel.Node.RightHand, TanabeMultiNodeModel.Layer.Skin);
      double chestCore = model.GetTemperature(
          TanabeMultiNodeModel.Node.Chest, TanabeMultiNodeModel.Layer.Core);
      // 四肢末端皮膚温はコア温より5°C以上低い
      Assert.True(handSkin < chestCore - 5.0,
          $"Hand skin({handSkin:F2}) should be < Chest core({chestCore:F2}) - 5°C");
    }

    /// <summary>
    /// ニュートラル環境（1 met）での熱収支誤差が代謝量の30%未満。
    /// 非定常成分や蓄熱を含むため許容誤差を広めに設定する。
    /// </summary>
    [Fact]
    public void NeutralEnvironment_HeatBalance_Approximately_Satisfied()
    {
      var model = RunEnvironment(28.8, 28.8, 50, 0);
      double totalLoss = TotalSensibleHeatLoss(model)
                       + TotalLatentHeatLoss(model)
                       + model.HeatLossByBreathing;
      double mr = model.MetabolicRate;
      double error = Math.Abs(totalLoss - mr);
      Assert.True(error < mr * 0.30,
          $"Heat balance error={error:F2} W should be < 30% of MetabolicRate={mr:F2} W");
    }

    #endregion
  }
}