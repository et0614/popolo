/* GaggeModelTests.cs
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
using Popolo.Core.ThermalComfort;

namespace Popolo.Core.Tests.ThermalComfort
{
  /// <summary>Unit tests for <see cref="GaggeModel"/> (2-Node Model).</summary>
  /// <remarks>
  /// The 2-Node Model represents the human body as two compartments:
  ///   Core: deep body tissues, thermoregulatory setpoint Tcr = 36.8 °C
  ///   Skin: shell, setpoint Tsk = 33.7 °C
  ///
  /// Standard Effective Temperature (SET*) is the equivalent temperature at which,
  /// in a standard environment (50 % RH, 0 m/s, standard clothing for the activity),
  /// the same skin temperature and skin wettedness would occur as in the actual environment.
  ///
  /// API notes:
  ///   Static methods (GetSteadyState, GetSETStarFromAmbientCondition):
  ///     basalMetabolism [W/m²]  — heat flux per unit body surface area
  ///     externalWork    [W/m²]  — mechanical work per unit body surface area
  ///     No atmospheric pressure argument.
  ///     Seated quiet ≈ 58 W/m² (= 1.0 met × 58.15 W/m²)
  ///
  ///   Instance method (UpdateState):
  ///     metabolicRate [-]       — ratio relative to basal metabolism
  ///                               (internally: met = BasalMetabolism × ratio / 0.7)
  ///     atmosphericPressure [kPa] — required argument
  /// </remarks>
  public class GaggeModelTests
  {
    #region 定数

    // 代謝量 [W/m²]（静的APIで使用）
    private const double Met_SeatedQuiet = 58.2;  // ≈ 1.0 met
    private const double Met_LightActivity = 80.0; // ≈ 1.4 met（軽作業）

    private const double Tatm = 101.325; // 大気圧 [kPa]（インスタンスAPIで使用）

    #endregion

    #region ヘルパー

    /// <summary>
    /// GetSteadyState（標準体格）を呼び出す。
    /// basalMetabolism: W/m²、externalWork: W/m²、大気圧引数なし。
    /// </summary>
    private static void SteadyState(
        double dbt, double mrt, double rh, double vel, double clo, double basalMet,
        out double tSkin, out double tCore, out double tBody, out double tCloth,
        out double sensibleSkin, out double latentSkin,
        out double sensibleResp, out double latentResp, out double wettedness)
    {
      GaggeModel.GetSteadyState(
          dbt, mrt, rh, vel, clo, basalMet, 0.0,
          out tSkin, out tCore, out tBody, out tCloth,
          out sensibleSkin, out latentSkin,
          out sensibleResp, out latentResp, out wettedness);
    }

    #endregion

    #region 定常状態テスト

    /// <summary>ニュートラル条件でコア温度が生理的範囲にある。</summary>
    [Fact]
    public void SteadyState_Neutral_CoreTemperature_InPhysiologicalRange()
    {
      SteadyState(25, 25, 50, 0.1, 1.0, Met_SeatedQuiet,
          out _, out double tCore, out _, out _, out _, out _, out _, out _, out _);
      // ヒトの正常コア体温域: 35–38 °C
      Assert.InRange(tCore, 35.0, 38.0);
    }

    /// <summary>ニュートラル条件で皮膚温が生理的範囲にある。</summary>
    [Fact]
    public void SteadyState_Neutral_SkinTemperature_InPhysiologicalRange()
    {
      SteadyState(25, 25, 50, 0.1, 1.0, Met_SeatedQuiet,
          out double tSkin, out _, out _, out _, out _, out _, out _, out _, out _);
      // 快適環境の平均皮膚温: 31–36 °C
      Assert.InRange(tSkin, 30.0, 37.0);
    }

    /// <summary>高温環境ではニュートラルより皮膚温が高い。</summary>
    [Fact]
    public void SteadyState_Hot_SkinTemperature_HigherThanNeutral()
    {
      SteadyState(25, 25, 50, 0.1, 1.0, Met_SeatedQuiet,
          out double tSkinNeutral, out _, out _, out _, out _, out _, out _, out _, out _);
      SteadyState(35, 35, 60, 0.1, 0.5, Met_SeatedQuiet,
          out double tSkinHot, out _, out _, out _, out _, out _, out _, out _, out _);
      Assert.True(tSkinHot > tSkinNeutral,
          $"Hot skin({tSkinHot:F2}) > Neutral skin({tSkinNeutral:F2})");
    }

    /// <summary>寒い環境ではニュートラルより皮膚温が低い。</summary>
    [Fact]
    public void SteadyState_Cold_SkinTemperature_LowerThanNeutral()
    {
      SteadyState(25, 25, 50, 0.1, 1.0, Met_SeatedQuiet,
          out double tSkinNeutral, out _, out _, out _, out _, out _, out _, out _, out _);
      SteadyState(15, 15, 30, 0.1, 1.0, Met_SeatedQuiet,
          out double tSkinCold, out _, out _, out _, out _, out _, out _, out _, out _);
      Assert.True(tSkinCold < tSkinNeutral,
          $"Cold skin({tSkinCold:F2}) < Neutral skin({tSkinNeutral:F2})");
    }

    /// <summary>高温多湿環境では皮膚濡れ率がニュートラルより高い（発汗）。</summary>
    [Fact]
    public void SteadyState_Hot_Wettedness_HigherThanNeutral()
    {
      SteadyState(25, 25, 50, 0.1, 1.0, Met_SeatedQuiet,
          out _, out _, out _, out _, out _, out _, out _, out _, out double wNeutral);
      SteadyState(35, 35, 70, 0.1, 0.5, Met_SeatedQuiet,
          out _, out _, out _, out _, out _, out _, out _, out _, out double wHot);
      Assert.True(wHot > wNeutral,
          $"Hot wettedness({wHot:F3}) > Neutral wettedness({wNeutral:F3})");
    }

    /// <summary>皮膚濡れ率は 0〜1 の範囲に収まる。</summary>
    [Theory]
    [InlineData(15, 15, 30, 0.1, 1.0)] // 寒冷
    [InlineData(25, 25, 50, 0.1, 1.0)] // ニュートラル
    [InlineData(35, 35, 70, 0.1, 0.5)] // 高温
    public void SteadyState_Wettedness_BetweenZeroAndOne(
        double dbt, double mrt, double rh, double vel, double clo)
    {
      SteadyState(dbt, mrt, rh, vel, clo, Met_SeatedQuiet,
          out _, out _, out _, out _, out _, out _, out _, out _, out double w);
      Assert.InRange(w, 0.0, 1.0);
    }

    /// <summary>高温環境では顕熱損失より潜熱損失が大きい（発汗優位）。</summary>
    [Fact]
    public void SteadyState_Hot_LatentLoss_ExceedsSensibleLoss()
    {
      SteadyState(35, 35, 70, 0.1, 0.5, Met_SeatedQuiet,
          out _, out _, out _, out _,
          out double sensible, out double latent,
          out _, out _, out _);
      Assert.True(latent > sensible,
          $"Hot: latent({latent:F2}) > sensible({sensible:F2}) W/m²");
    }

    /// <summary>寒い環境では潜熱損失より顕熱損失が大きい（放熱優位）。</summary>
    [Fact]
    public void SteadyState_Cold_SensibleLoss_ExceedsLatentLoss()
    {
      SteadyState(15, 15, 30, 0.1, 1.0, Met_SeatedQuiet,
          out _, out _, out _, out _,
          out double sensible, out double latent,
          out _, out _, out _);
      Assert.True(sensible > latent,
          $"Cold: sensible({sensible:F2}) > latent({latent:F2}) W/m²");
    }

    #endregion

    #region SET* テスト

    /// <summary>ニュートラル条件の SET* が概ね作用温度に近い。</summary>
    [Fact]
    public void SET_Neutral_CloseToOperativeTemperature()
    {
      // 標準条件（50% RH, 0 m/s）なら SET* は作用温度とほぼ一致する
      // basalMetabolism [W/m²]、大気圧引数なし
      double set = GaggeModel.GetSETStarFromAmbientCondition(
          25, 25, 50, 0.0, 1.0, Met_SeatedQuiet, 0.0);
      Assert.InRange(set, 22.0, 28.0);
    }

    /// <summary>SET* は環境温度の単調増加関数。</summary>
    [Fact]
    public void SET_IncreasesMonotonically_WithTemperature()
    {
      double set20 = GaggeModel.GetSETStarFromAmbientCondition(20, 20, 50, 0.1, 1.0, Met_SeatedQuiet, 0.0);
      double set25 = GaggeModel.GetSETStarFromAmbientCondition(25, 25, 50, 0.1, 1.0, Met_SeatedQuiet, 0.0);
      double set30 = GaggeModel.GetSETStarFromAmbientCondition(30, 30, 50, 0.1, 1.0, Met_SeatedQuiet, 0.0);
      Assert.True(set20 < set25 && set25 < set30,
          $"SET*(20)={set20:F2} < SET*(25)={set25:F2} < SET*(30)={set30:F2}");
    }

    /// <summary>気流速度が高い方が SET* は低くなる（体感温度の低下）。</summary>
    [Fact]
    public void SET_DecreasesWithHigherAirVelocity()
    {
      double setLow = GaggeModel.GetSETStarFromAmbientCondition(30, 30, 50, 0.1, 0.5, Met_SeatedQuiet, 0.0);
      double setHigh = GaggeModel.GetSETStarFromAmbientCondition(30, 30, 50, 0.8, 0.5, Met_SeatedQuiet, 0.0);
      Assert.True(setLow > setHigh,
          $"Low vel SET*({setLow:F2}) > High vel SET*({setHigh:F2})");
    }

    /// <summary>高湿環境では SET* が上昇する（蒸発放熱の阻害）。</summary>
    [Fact]
    public void SET_IncreasesWithHigherHumidity_AtHighTemp()
    {
      double setLowRH = GaggeModel.GetSETStarFromAmbientCondition(30, 30, 30, 0.1, 0.5, Met_SeatedQuiet, 0.0);
      double setHighRH = GaggeModel.GetSETStarFromAmbientCondition(30, 30, 80, 0.1, 0.5, Met_SeatedQuiet, 0.0);
      Assert.True(setHighRH > setLowRH,
          $"High RH SET*({setHighRH:F2}) > Low RH SET*({setLowRH:F2})");
    }

    #endregion

    #region インスタンスAPI（UpdateState）テスト

    /// <summary>
    /// UpdateState の metabolicRate は BasalMetabolism に対する比率 [-]。
    /// 内部実装：metab = BasalMetabolism × metabolicRate / 0.7
    /// 1.0 を渡すと BasalMetabolism / 0.7 ≈ 軽活動レベルになる。
    /// </summary>
    [Fact]
    public void UpdateState_RepeatedCalls_DoNotThrow()
    {
      // 標準体格: 25歳, 男性, 170 cm, 70 kg
      var model = new GaggeModel(25, true, 1.70, 70.0);
      for (int i = 0; i < 60; i++)
        model.UpdateState(60, 25, 25, 50, 0.1, 1.0, 1.0, 0.0, Tatm);
    }

    /// <summary>定常状態で皮膚温がコア温より低い。</summary>
    [Fact]
    public void UpdateState_SteadyState_SkinCoolerThanCore()
    {
      var model = new GaggeModel(25, true, 1.70, 70.0);
      for (int i = 0; i < 60; i++)
        model.UpdateState(60, 25, 25, 50, 0.1, 1.0, 1.0, 0.0, Tatm);
      Assert.True(model.SkinTemperature < model.CoreTemperature,
          $"Skin({model.SkinTemperature:F2}) < Core({model.CoreTemperature:F2})");
    }

    /// <summary>高温環境ではニュートラルより皮膚温が上昇する。</summary>
    [Fact]
    public void UpdateState_Hot_SkinTemperature_HigherThanNeutral()
    {
      var neutral = new GaggeModel(25, true, 1.70, 70.0);
      var hot = new GaggeModel(25, true, 1.70, 70.0);
      for (int i = 0; i < 60; i++)
      {
        neutral.UpdateState(60, 25, 25, 50, 0.1, 1.0, 1.0, 0.0, Tatm);
        hot.UpdateState(60, 35, 35, 70, 0.1, 0.5, 1.0, 0.0, Tatm);
      }
      Assert.True(hot.SkinTemperature > neutral.SkinTemperature,
          $"Hot skin({hot.SkinTemperature:F2}) > Neutral skin({neutral.SkinTemperature:F2})");
    }

    /// <summary>
    /// 高温乾燥環境（40 °C, 50 % RH）では寒冷環境より濡れ率が高い（発汗）。
    /// 高湿条件（70 % RH）では蒸発余力（emax）が小さく短時間では差が出にくいため、
    /// 低湿で emax を確保した条件を使用する。
    /// </summary>
    [Fact]
    public void UpdateState_Hot_Wettedness_HigherThanCold()
    {
      var cold = new GaggeModel(25, true, 1.70, 70.0);
      var hot = new GaggeModel(25, true, 1.70, 70.0);
      for (int i = 0; i < 120; i++) // 2時間で十分定常化
      {
        cold.UpdateState(60, 15, 15, 30, 0.1, 1.0, 1.0, 0.0, Tatm);
        hot.UpdateState(60, 40, 40, 50, 0.1, 0.5, 1.0, 0.0, Tatm);
      }
      Assert.True(hot.Wettedness > cold.Wettedness,
          $"Hot wettedness({hot.Wettedness:F3}) should be > Cold wettedness({cold.Wettedness:F3})");
    }

    #endregion
  }
}