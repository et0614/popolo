/* MoistAirTests.cs
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
using Popolo.Core.Physics;
using Popolo.Core.Exceptions;

namespace Popolo.Core.Tests.Physics
{
  /// <summary>MoistAir のテスト</summary>
  /// <remarks>
  /// 期待値は以下の文献に基づく：
  /// - ASHRAE Fundamentals Handbook 1997, Psychrometrics
  /// - 宇田川光弘「パソコンによる空気調和計算法」
  /// </remarks>
  public class MoistAirTests
  {
    /// <summary>標準大気圧 [kPa]</summary>
    private const double Atm = PhysicsConstants.StandardAtmosphericPressure;

    #region Constant tests

    /// <summary>乾き空気の定圧比熱が正しい値を持つ</summary>
    [Fact]
    public void DryAirIsobaricSpecificHeat_HasCorrectValue()
    {
      Assert.Equal(1.006, MoistAir.DryAirIsobaricSpecificHeat, precision: 3);
    }

    /// <summary>0°C の蒸発潜熱が正しい値を持つ</summary>
    [Fact]
    public void VaporizationLatentHeat_HasCorrectValue()
    {
      Assert.Equal(2501.0, MoistAir.VaporizationLatentHeat, precision: 1);
    }

    #endregion

    #region Constructor tests

    /// <summary>デフォルトコンストラクタで正しい初期値が設定される</summary>
    [Fact]
    public void DefaultConstructor_SetsReasonableInitialState()
    {
      var air = new MoistAir();
      Assert.Equal(24.0, air.DryBulbTemperature, precision: 6);
      Assert.Equal(0.0093, air.HumidityRatio, precision: 4);
      Assert.InRange(air.RelativeHumidity, 0.0, 100.0);
      Assert.True(air.Enthalpy > 0);
    }

    /// <summary>コピーコンストラクタで全プロパティが正しくコピーされる</summary>
    [Fact]
    public void CopyConstructor_CopiesAllProperties()
    {
      var src = new MoistAir(30.0, 0.012);
      var dst = new MoistAir(src);

      Assert.Equal(src.DryBulbTemperature, dst.DryBulbTemperature);
      Assert.Equal(src.HumidityRatio, dst.HumidityRatio);
      Assert.Equal(src.RelativeHumidity, dst.RelativeHumidity);
      Assert.Equal(src.Enthalpy, dst.Enthalpy);
      Assert.Equal(src.WetBulbTemperature, dst.WetBulbTemperature);
      Assert.Equal(src.SpecificVolume, dst.SpecificVolume);
      Assert.Equal(src.AtmosphericPressure, dst.AtmosphericPressure);
    }

    #endregion

    #region Argument range tests

    [Fact]
    public void GetEnthalpyFromDryBulbTemperatureAndHumidityRatio_BelowAbsoluteZero_Throws()
    {
      Assert.Throws<PopoloOutOfRangeException>(
          () => MoistAir.GetEnthalpyFromDryBulbTemperatureAndHumidityRatio(-274.0, 0.009));
    }

    [Fact]
    public void GetHumidityRatioFromDryBulbTemperatureAndRelativeHumidity_NegativeRH_Throws()
    {
      Assert.Throws<PopoloOutOfRangeException>(
          () => MoistAir.GetHumidityRatioFromDryBulbTemperatureAndRelativeHumidity(24.0, -1.0, 101.325));
    }

    [Fact]
    public void GetHumidityRatioFromDryBulbTemperatureAndRelativeHumidity_Over100RH_Throws()
    {
      Assert.Throws<PopoloOutOfRangeException>(
          () => MoistAir.GetHumidityRatioFromDryBulbTemperatureAndRelativeHumidity(24.0, 101.0, 101.325));
    }

    [Fact]
    public void GetSpecificVolumeFromDryBulbTemperatureAndHumidityRatio_ZeroPressure_Throws()
    {
      Assert.Throws<PopoloOutOfRangeException>(
          () => MoistAir.GetSpecificVolumeFromDryBulbTemperatureAndHumidityRatio(24.0, 0.009, 0.0));
    }

    #endregion

    #region Enthalpy calculation tests

    /// <summary>乾球温度と絶対湿度からエンタルピーを計算できる（ASHRAE式）</summary>
    [Fact]
    public void GetEnthalpyFromDryBulbTemperatureAndHumidityRatio_ReturnsCorrectValue()
    {
      // h = 1.006*t + W*(2501 + 1.805*t)
      // t=24°C, W=0.009 → h ≒ 47.1 kJ/kg
      double h = MoistAir.GetEnthalpyFromDryBulbTemperatureAndHumidityRatio(24.0, 0.009);
      Assert.InRange(h, 46.5, 47.8);
    }

    /// <summary>エンタルピーと絶対湿度から乾球温度が逆算できる</summary>
    [Theory]
    [InlineData(20.0, 0.008)]
    [InlineData(24.0, 0.009)]
    [InlineData(30.0, 0.015)]
    public void GetDryBulbTemperatureFromHumidityRatioAndEnthalpy_IsInverseOfGetEnthalpy(
        double dbt, double w)
    {
      double h = MoistAir.GetEnthalpyFromDryBulbTemperatureAndHumidityRatio(dbt, w);
      double dbtRecovered = MoistAir.GetDryBulbTemperatureFromHumidityRatioAndEnthalpy(w, h);
      Assert.Equal(dbt, dbtRecovered, precision: 6);
    }

    /// <summary>乾球温度とエンタルピーから絶対湿度が逆算できる</summary>
    [Theory]
    [InlineData(20.0, 0.008)]
    [InlineData(24.0, 0.009)]
    [InlineData(30.0, 0.015)]
    public void GetHumidityRatioFromDryBulbTemperatureAndEnthalpy_IsInverseOfGetEnthalpy(
        double dbt, double w)
    {
      double h = MoistAir.GetEnthalpyFromDryBulbTemperatureAndHumidityRatio(dbt, w);
      double wRecovered = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndEnthalpy(dbt, h);
      Assert.Equal(w, wRecovered, precision: 6);
    }

    #endregion

    #region Relative humidity calculation tests

    /// <summary>相対湿度 50%, 24°C での絶対湿度は約 0.009 kg/kg</summary>
    [Fact]
    public void GetHumidityRatioFromDryBulbTemperatureAndRelativeHumidity_At24C50RH()
    {
      double w = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndRelativeHumidity(
          24.0, 50.0, Atm);
      Assert.InRange(w, 0.0088, 0.0095);
    }

    /// <summary>乾球温度と絶対湿度から相対湿度を計算し逆算できる</summary>
    [Theory]
    [InlineData(20.0, 50.0)]
    [InlineData(24.0, 60.0)]
    [InlineData(30.0, 40.0)]
    public void GetRelativeHumidityFromDryBulbTemperatureAndHumidityRatio_IsInverse(
        double dbt, double rh)
    {
      double w = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndRelativeHumidity(
          dbt, rh, Atm);
      double rhRecovered = MoistAir.GetRelativeHumidityFromDryBulbTemperatureAndHumidityRatio(
          dbt, w, Atm);
      Assert.Equal(rh, rhRecovered, precision: 4);
    }

    /// <summary>飽和状態（RH=100%）では絶対湿度が飽和絶対湿度と一致する</summary>
    [Fact]
    public void GetHumidityRatioFromDryBulbTemperatureAndRelativeHumidity_At100RH_ReturnsSaturation()
    {
      double wSat1 = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndRelativeHumidity(
          24.0, 100.0, Atm);
      double wSat2 = MoistAir.GetSaturationHumidityRatioFromDryBulbTemperature(24.0, Atm);
      Assert.Equal(wSat1, wSat2, precision: 6);
    }

    #endregion

    #region Wet-bulb temperature calculation tests

    /// <summary>乾球温度と湿球温度から絶対湿度を計算し逆算できる</summary>
    [Theory]
    [InlineData(24.0, 17.0)]
    [InlineData(30.0, 22.0)]
    [InlineData(20.0, 15.0)]
    public void GetWetBulbTemperature_IsConsistentWithHumidityRatio(double dbt, double wbt)
    {
      double w = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndWetBulbTemperature(
          dbt, wbt, Atm);
      double wbtRecovered = MoistAir.GetWetBulbTemperatureFromDryBulbTemperatureAndHumidityRatio(
          dbt, w, Atm);
      Assert.Equal(wbt, wbtRecovered, precision: 3);
    }

    /// <summary>乾球温度と湿球温度から相対湿度を計算できる（24°C, 17°C → 約50%）</summary>
    [Fact]
    public void GetRelativeHumidityFromDryBulbTemperatureAndWetBulbTemperature_ReturnsCorrectValue()
    {
      double rh = MoistAir.GetRelativeHumidityFromDryBulbTemperatureAndWetBulbTemperature(
          24.0, 17.0, Atm);
      Assert.InRange(rh, 45.0, 55.0);
    }

    /// <summary>乾球温度 = 湿球温度のとき相対湿度は 100%</summary>
    [Fact]
    public void GetRelativeHumidityFromDryBulbTemperatureAndWetBulbTemperature_EqualTemperatures_Returns100()
    {
      double rh = MoistAir.GetRelativeHumidityFromDryBulbTemperatureAndWetBulbTemperature(
          24.0, 24.0, Atm);
      Assert.Equal(100.0, rh, precision: 2);
    }

    #endregion

    #region Specific volume calculation tests

    /// <summary>24°C, W=0.009 での比体積は約 0.854 m³/kg</summary>
    [Fact]
    public void GetSpecificVolumeFromDryBulbTemperatureAndHumidityRatio_ReturnsCorrectValue()
    {
      double v = MoistAir.GetSpecificVolumeFromDryBulbTemperatureAndHumidityRatio(
          24.0, 0.009, Atm);
      Assert.InRange(v, 0.850, 0.860);
    }

    /// <summary>比体積から乾球温度を逆算できる</summary>
    [Theory]
    [InlineData(20.0, 0.008)]
    [InlineData(24.0, 0.009)]
    [InlineData(30.0, 0.012)]
    public void GetDryBulbTemperatureFromSpecificVolumeAndHumidityRatio_IsInverse(
        double dbt, double w)
    {
      double v = MoistAir.GetSpecificVolumeFromDryBulbTemperatureAndHumidityRatio(dbt, w, Atm);
      double dbtRecovered = MoistAir.GetDryBulbTemperatureFromSpecificVolumeAndHumidityRatio(
          v, w, Atm);
      Assert.Equal(dbt, dbtRecovered, precision: 6);
    }

    #endregion

    #region Saturation state calculation tests

    /// <summary>露点温度から飽和絶対湿度を計算できる</summary>
    [Fact]
    public void GetSaturationHumidityRatioFromDryBulbTemperature_ReturnsCorrectValue()
    {
      //24°C での飽和絶対湿度は約 0.0187 kg/kg
      double wSat = MoistAir.GetSaturationHumidityRatioFromDryBulbTemperature(24.0, Atm);
      Assert.InRange(wSat, 0.018, 0.020);
    }

    /// <summary>飽和エンタルピーは乾球温度・飽和絶対湿度から計算したエンタルピーと一致する</summary>
    [Theory]
    [InlineData(15.0)]
    [InlineData(24.0)]
    [InlineData(30.0)]
    public void GetSaturationEnthalpyFromDryBulbTemperature_IsConsistentWithComponents(double dbt)
    {
      double hSat = MoistAir.GetSaturationEnthalpyFromDryBulbTemperature(dbt, Atm);
      double wSat = MoistAir.GetSaturationHumidityRatioFromDryBulbTemperature(dbt, Atm);
      double hExpected = MoistAir.GetEnthalpyFromDryBulbTemperatureAndHumidityRatio(dbt, wSat);
      Assert.Equal(hExpected, hSat, precision: 6);
    }

    /// <summary>露点温度から絶対湿度を逆算できる</summary>
    [Fact]
    public void GetSaturationDryBulbTemperatureFromHumidityRatio_IsConsistent()
    {
      double dbt = 24.0;
      double wSat = MoistAir.GetSaturationHumidityRatioFromDryBulbTemperature(dbt, Atm);
      double dewPoint = MoistAir.GetSaturationDryBulbTemperatureFromHumidityRatio(wSat, Atm);
      Assert.Equal(dbt, dewPoint, precision: 3);
    }

    #endregion

    #region Other physical property tests

    /// <summary>標高 0m での大気圧は標準大気圧に等しい</summary>
    [Fact]
    public void GetAtmosphericPressure_AtSeaLevel_ReturnsStandardPressure()
    {
      double p = MoistAir.GetAtmosphericPressure(0.0);
      Assert.Equal(PhysicsConstants.StandardAtmosphericPressure, p, precision: 3);
    }

    /// <summary>標高が上がると大気圧が下がる</summary>
    [Fact]
    public void GetAtmosphericPressure_IsMonotonicallyDecreasing()
    {
      double p0 = MoistAir.GetAtmosphericPressure(0);
      double p500 = MoistAir.GetAtmosphericPressure(500);
      double p1000 = MoistAir.GetAtmosphericPressure(1000);
      Assert.True(p0 > p500 && p500 > p1000);
    }

    /// <summary>湿り空気の比熱は乾き空気より大きい（水蒸気分がある）</summary>
    [Fact]
    public void GetSpecificHeat_WithHumidity_IsGreaterThanDryAir()
    {
      double cpMoist = MoistAir.GetSpecificHeat(0.009);
      Assert.True(cpMoist > MoistAir.DryAirIsobaricSpecificHeat);
    }

    /// <summary>熱拡散率は α = λ/(ρ·cp) と整合する</summary>
    [Fact]
    public void GetThermalDiffusivity_IsConsistentWithComponents()
    {
      double dbt = 24.0;
      double w = 0.009;
      double lambda = MoistAir.GetThermalConductivity(dbt);
      double cp = MoistAir.GetSpecificHeat(w);
      double v = MoistAir.GetSpecificVolumeFromDryBulbTemperatureAndHumidityRatio(dbt, w, Atm);
      double alphaExpected = lambda / (1000.0 * cp * v);
      double alphaDirect = MoistAir.GetThermalDiffusivity(dbt, w, Atm);
      Assert.Equal(alphaExpected, alphaDirect, precision: 10);
    }

    /// <summary>蒸発潜熱は温度の減少関数である</summary>
    [Fact]
    public void GetLatentHeatOfVaporization_IsDecreasingWithTemperature()
    {
      double L0 = MoistAir.GetLatentHeatOfVaporization(0.0);
      double L20 = MoistAir.GetLatentHeatOfVaporization(20.0);
      double L100 = MoistAir.GetLatentHeatOfVaporization(100.0);
      Assert.True(L0 > L20 && L20 > L100);
    }

    #endregion

    #region BlendAir tests

    /// <summary>等量の空気を混合すると中間の状態になる</summary>
    [Fact]
    public void BlendAir_TwoStreams_ReturnsWeightedAverage()
    {
      var air1 = new MoistAir(20.0, 0.008);
      var air2 = new MoistAir(30.0, 0.012);

      var blended = MoistAir.BlendAir(air1, air2, 1.0, 1.0);

      Assert.Equal(25.0, blended.DryBulbTemperature, precision: 6);
      Assert.Equal(0.010, blended.HumidityRatio, precision: 6);
    }

    /// <summary>一方の体積が 0 のときもう一方の空気状態がそのまま返る</summary>
    [Fact]
    public void BlendAir_ZeroVolume_ReturnsOtherAir()
    {
      var air1 = new MoistAir(20.0, 0.008);
      var air2 = new MoistAir(30.0, 0.012);

      var result = MoistAir.BlendAir(air1, air2, 0.0, 1.0);
      Assert.Equal(air2.DryBulbTemperature, result.DryBulbTemperature, precision: 6);
    }

    /// <summary>配列の長さが一致しない場合に PopoloArgumentException が発生する</summary>
    [Fact]
    public void BlendAir_MismatchedArrayLength_ThrowsPopoloArgumentException()
    {
      var air = new IReadOnlyMoistAir[] { new MoistAir(24.0, 0.009) };
      var volume = new double[] { 1.0, 2.0 };

      var ex = Assert.Throws<PopoloArgumentException>(
          () => MoistAir.BlendAir(air, volume));
      Assert.Equal("volume", ex.ParamName);
    }

    #endregion

    #region CopyTo tests

    /// <summary>CopyTo で全プロパティが正しくコピーされる</summary>
    [Fact]
    public void CopyTo_CopiesAllProperties()
    {
      var src = new MoistAir(28.0, 0.011);
      var dst = new MoistAir();
      src.CopyTo(dst);

      Assert.Equal(src.DryBulbTemperature, dst.DryBulbTemperature);
      Assert.Equal(src.HumidityRatio, dst.HumidityRatio);
      Assert.Equal(src.RelativeHumidity, dst.RelativeHumidity);
      Assert.Equal(src.Enthalpy, dst.Enthalpy);
      Assert.Equal(src.WetBulbTemperature, dst.WetBulbTemperature);
      Assert.Equal(src.SpecificVolume, dst.SpecificVolume);
      Assert.Equal(src.AtmosphericPressure, dst.AtmosphericPressure);
    }

    #endregion

    #region Sub-zero wet-bulb (ice-bulb) tests

    /// <summary>
    /// 湿球温度が0°C未満では氷面の昇華の熱収支（ASHRAE Fundamentals 2017 Ch.1 Eq.(37)）で
    /// 絶対湿度を求める。参照値：PsychroLib (Meyer and Thevenard, JOSS 2019) の試験値
    /// GetHumRatioFromTWetBulb(-1, -5, 95461 Pa) = 0.00120399819933844
    /// （従来の液面の式では 0.0010245 と約15%過小だった）
    /// </summary>
    [Fact]
    public void GetHumidityRatioFromDryBulbAndWetBulb_BelowFreezing_MatchesAshraeIceBulb()
    {
      double w = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndWetBulbTemperature(
          -1.0, -5.0, 95.461);
      Assert.Equal(0.00120399819933844, w, 0.00120399819933844 * 0.005);
    }

    /// <summary>
    /// 氷点下の湿球温度での絶対湿度が ASHRAE Eq.(37)（ASHRAE の定数 2830, 0.24, 1.006,
    /// 1.86, 2.1 と Hyland-Wexler の氷面飽和水蒸気圧、Ws=0.621945·pws/(p−pws)）による値と
    /// 1% 以内で一致する（本クラスは cpv=1.805, 昇華潜熱 2501+333.4 を用いるため僅かに差がある。
    /// 従来の液面の式では例えば (5°C, -2°C) で 0.00038 と約46%過小）
    /// </summary>
    [Theory]
    [InlineData(-10.0, -12.0, 0.0006257657877335384)]
    [InlineData(5.0, -2.0, 0.0007029770300539591)]
    [InlineData(0.0, -3.0, 0.0018660529997003386)]
    [InlineData(-20.0, -21.0, 0.0002211481923194345)]
    public void GetHumidityRatioFromDryBulbAndWetBulb_BelowFreezing_MatchesAshraeEquation(
        double dbt, double wbt, double expected)
    {
      double w = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndWetBulbTemperature(
          dbt, wbt, Atm);
      Assert.Equal(expected, w, expected * 0.01);
    }

    /// <summary>氷点下の湿球温度は絶対湿度からの逆算で元に戻る（PsychroLib の試験条件）</summary>
    [Fact]
    public void GetWetBulbTemperature_BelowFreezing_MatchesPsychroLib()
    {
      double wbt = MoistAir.GetWetBulbTemperatureFromDryBulbTemperatureAndHumidityRatio(
          -1.0, 0.00120399819933844, 95.461);
      Assert.Equal(-5.0, wbt, 0.05);
    }

    /// <summary>氷点下の湿球温度での順算・逆算（湿球温度・乾球温度）が整合する</summary>
    [Theory]
    [InlineData(-10.0, -12.0)]
    [InlineData(5.0, -2.0)]
    [InlineData(0.0, -3.0)]
    [InlineData(-20.0, -21.0)]
    public void WetBulbRelations_BelowFreezing_AreMutuallyConsistent(double dbt, double wbt)
    {
      double w = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndWetBulbTemperature(
          dbt, wbt, Atm);
      double wbtRecovered = MoistAir.GetWetBulbTemperatureFromDryBulbTemperatureAndHumidityRatio(
          dbt, w, Atm);
      double dbtRecovered = MoistAir.GetDryBulbTemperatureFromHumidityRatioAndWetBulbTemperature(
          w, wbt, Atm);
      Assert.Equal(wbt, wbtRecovered, 1e-3);
      Assert.Equal(dbt, dbtRecovered, 1e-6);
    }

    /// <summary>
    /// 0°C を境に液面（湿球）と氷面（氷球）の式が切り替わる。昇華潜熱と融解熱の差のため
    /// 絶対湿度から求めた湿球温度には 0°C で本質的な小さな不連続が生じるが、その幅は 0.6K 未満である
    /// </summary>
    [Theory]
    [InlineData(5.0)]
    [InlineData(15.0)]
    [InlineData(-2.0)]
    public void GetWetBulbTemperature_AcrossFreezingPoint_DiscontinuityIsSmall(double dbt)
    {
      //湿球温度 0°C（液面側）に対応する絶対湿度
      double w0 = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndWetBulbTemperature(
          dbt, 0.0, Atm);
      if (w0 <= 0) return;
      double above = MoistAir.GetWetBulbTemperatureFromDryBulbTemperatureAndHumidityRatio(
          dbt, w0 * (1 + 1e-9), Atm);
      double below = MoistAir.GetWetBulbTemperatureFromDryBulbTemperatureAndHumidityRatio(
          dbt, w0 * (1 - 1e-6), Atm);
      Assert.InRange(above, -1e-4, 1e-3);
      Assert.True(below <= 0.0, $"below={below}");
      Assert.True(above - below < 0.6, $"above={above}, below={below}");
    }

    /// <summary>湿球温度が0°C以上の結果は従来と同一である（期待値は修正前のコードによる値）</summary>
    [Theory]
    [InlineData(24.0, 0.0093, 17.066400694277977)]
    [InlineData(35.0, 0.02, 27.395686677334883)]
    [InlineData(5.0, 0.004, 3.16713502763123)]
    [InlineData(40.0, 0.0065, 20.07313960442337)]
    [InlineData(10.0, 0.0, 0.3654578505261884)]
    public void Constructor_WetBulbAboveFreezing_Unchanged(double dbt, double w, double expectedWbt)
    {
      var air = new MoistAir(dbt, w);
      Assert.Equal(expectedWbt, air.WetBulbTemperature, 1e-12);
    }

    #endregion

  }
}
