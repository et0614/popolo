/* MoistAirTests.cs
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

    #region 定数のテスト

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

    #region コンストラクタのテスト

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

    #region 引数範囲のテスト

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

    #region エンタルピーの計算テスト

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

    #region 相対湿度の計算テスト

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

    #region 湿球温度の計算テスト

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

    #region 比体積の計算テスト

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

    #region 飽和状態の計算テスト

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

    #region その他の物性値テスト

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

    #region BlendAir のテスト

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

    #region CopyTo のテスト

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

  }
}
