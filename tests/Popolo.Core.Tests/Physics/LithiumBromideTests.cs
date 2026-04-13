/* LithiumBromideTests.cs
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
  /// <summary>LithiumBromide のテスト</summary>
  /// <remarks>
  /// 温度引数は全て [K]。質量分率は 0〜1 [-]。
  /// </remarks>
  public class LithiumBromideTests
  {
    //典型的な吸収冷凍機の運転条件
    //高温再生器：液温 90°C = 363.15K、質量分率 0.60
    //低温蒸発器：蒸気温度 10°C = 283.15K、質量分率 0.55
    private const double T90C = 363.15;   // K
    private const double T80C = 353.15;   // K
    private const double T10C = 283.15;   // K
    private const double MF60 = 0.60;
    private const double MF55 = 0.55;

    #region 飽和温度・溶液温度・質量分率の計算テスト

    /// <summary>溶液温度と質量分率から飽和温度を計算し逆算できる</summary>
    [Theory]
    [InlineData(363.15, 0.60)]
    [InlineData(353.15, 0.55)]
    [InlineData(333.15, 0.50)]
    public void GetVaporTemperature_IsInverseOfGetLiquidTemperature(
        double liquidTemp, double mf)
    {
      double vaporTemp = LithiumBromide.GetVaporTemperatureFromLiquidTemperatureAndMassFraction(
          liquidTemp, mf);
      double liquidTempRecovered =
          LithiumBromide.GetLiquidTemperatureFromVaporTemperatureAndMassFraction(
          vaporTemp, mf);
      Assert.Equal(liquidTemp, liquidTempRecovered, precision: 4);
    }

    /// <summary>溶液温度と飽和温度から質量分率を逆算できる</summary>
    [Fact]
    public void GetMassFractionFromLiquidTemperatureAndVaporTemperature_IsConsistent()
    {
      double vaporTemp = LithiumBromide.GetVaporTemperatureFromLiquidTemperatureAndMassFraction(
          T90C, MF60);
      double mfRecovered =
          LithiumBromide.GetMassFractionFromLiquidTemperatureAndVaporTemperature(
          T90C, vaporTemp);
      Assert.Equal(MF60, mfRecovered, precision: 4);
    }

    /// <summary>飽和温度は溶液温度より低い（濃度上昇効果）</summary>
    [Fact]
    public void VaporTemperature_IsLessThanLiquidTemperature()
    {
      double vaporTemp = LithiumBromide.GetVaporTemperatureFromLiquidTemperatureAndMassFraction(
          T90C, MF60);
      Assert.True(vaporTemp < T90C,
          $"Vapor temperature ({vaporTemp} K) should be less than liquid temperature ({T90C} K).");
    }

    #endregion

    #region 比エンタルピーの計算テスト

    /// <summary>溶液温度と質量分率から比エンタルピーを計算し逆算できる</summary>
    [Theory]
    [InlineData(363.15, 0.60)]
    [InlineData(353.15, 0.55)]
    [InlineData(333.15, 0.50)]
    public void GetEnthalpy_IsInverseOfGetLiquidTemperature(
        double liquidTemp, double mf)
    {
      double h = LithiumBromide.GetEnthalpyFromLiquidTemperatureAndMassFraction(
          liquidTemp, mf);
      double liquidTempRecovered =
          LithiumBromide.GetLiquidTemperatureFromEnthalpyAndMassFraction(h, mf);
      Assert.Equal(liquidTemp, liquidTempRecovered, precision: 3);
    }

    /// <summary>飽和温度経由のエンタルピーが直接計算と一致する</summary>
    [Fact]
    public void GetEnthalpyFromVaporTemperature_IsConsistentWithDirectCalculation()
    {
      double hDirect = LithiumBromide.GetEnthalpyFromLiquidTemperatureAndMassFraction(
          T90C, MF60);
      double vaporTemp = LithiumBromide.GetVaporTemperatureFromLiquidTemperatureAndMassFraction(
          T90C, MF60);
      double hViaVapor = LithiumBromide.GetEnthalpyFromLiquidTemperatureAndVaporTemperature(
          T90C, vaporTemp);
      Assert.Equal(hDirect, hViaVapor, precision: 4);
    }

    /// <summary>エンタルピーは温度の増加関数である</summary>
    [Fact]
    public void GetEnthalpy_IsIncreasingWithTemperature()
    {
      double h1 = LithiumBromide.GetEnthalpyFromLiquidTemperatureAndMassFraction(T10C, MF55);
      double h2 = LithiumBromide.GetEnthalpyFromLiquidTemperatureAndMassFraction(T80C, MF55);
      double h3 = LithiumBromide.GetEnthalpyFromLiquidTemperatureAndMassFraction(T90C, MF55);
      Assert.True(h1 < h2 && h2 < h3);
    }

    #endregion

    #region その他の物性値テスト

    /// <summary>密度は水の密度と臭化リチウム密度の混合として妥当な範囲にある</summary>
    [Fact]
    public void GetDensity_ReturnsPhysicallyReasonableValue()
    {
      // 水の密度≒1000 kg/m3、LiBr密度=3460 kg/m3
      // 質量分率0.55のとき：3460*0.55 + 972*0.45 ≒ 2340 kg/m3
      double rho = LithiumBromide.GetDensity(T80C, MF55);
      Assert.InRange(rho, 2200.0, 2500.0);
    }

    /// <summary>密度は質量分率の増加関数である</summary>
    [Fact]
    public void GetDensity_IsIncreasingWithMassFraction()
    {
      double rho1 = LithiumBromide.GetDensity(T80C, 0.40);
      double rho2 = LithiumBromide.GetDensity(T80C, 0.55);
      double rho3 = LithiumBromide.GetDensity(T80C, 0.65);
      Assert.True(rho1 < rho2 && rho2 < rho3);
    }

    /// <summary>比熱は温度・質量分率から計算できる</summary>
    [Fact]
    public void GetSpecificHeat_ReturnsPhysicallyReasonableValue()
    {
      // LiBr水溶液の比熱は水(4.186)より小さく、おおむね 1.5〜3.5 kJ/(kg·K) の範囲
      double cp = LithiumBromide.GetSpecificHeat(T80C, MF55);
      Assert.InRange(cp, 1.5, 3.5);
    }

    /// <summary>比熱は質量分率の減少関数である（水の割合が多いほど大きい）</summary>
    [Fact]
    public void GetSpecificHeat_IsDecreasingWithMassFraction()
    {
      double cp1 = LithiumBromide.GetSpecificHeat(T80C, 0.40);
      double cp2 = LithiumBromide.GetSpecificHeat(T80C, 0.55);
      double cp3 = LithiumBromide.GetSpecificHeat(T80C, 0.65);
      Assert.True(cp1 > cp2 && cp2 > cp3);
    }

    #endregion

    #region ファクトリメソッドのテスト

    /// <summary>MakeFromLiquidTemperatureAndMassFraction で全プロパティが設定される</summary>
    [Fact]
    public void MakeFromLiquidTemperatureAndMassFraction_SetsAllProperties()
    {
      var lb = LithiumBromide.MakeFromLiquidTemperatureAndMassFraction(T90C, MF60);

      Assert.Equal(T90C, lb.LiquidTemperature, precision: 6);
      Assert.Equal(MF60, lb.MassFraction, precision: 6);
      Assert.True(lb.VaporTemperature > 0);
      Assert.True(lb.SpecificHeat > 0);
    }

    /// <summary>MakeFromVaporTemperatureAndMassFraction と MakeFromLiquidTemperatureAndMassFraction が整合する</summary>
    [Fact]
    public void MakeFromVaporTemperature_IsConsistentWithMakeFromLiquidTemperature()
    {
      var lb1 = LithiumBromide.MakeFromLiquidTemperatureAndMassFraction(T90C, MF60);
      var lb2 = LithiumBromide.MakeFromVaporTemperatureAndMassFraction(
          lb1.VaporTemperature, MF60);

      Assert.Equal(lb1.LiquidTemperature, lb2.LiquidTemperature, precision: 3);
      Assert.Equal(lb1.Enthalpy, lb2.Enthalpy, precision: 3);
    }

    /// <summary>MakeFromLiquidTemperatureAndVaporTemperature が整合する</summary>
    [Fact]
    public void MakeFromLiquidTemperatureAndVaporTemperature_IsConsistent()
    {
      var lb1 = LithiumBromide.MakeFromLiquidTemperatureAndMassFraction(T90C, MF60);
      var lb2 = LithiumBromide.MakeFromLiquidTemperatureAndVaporTemperature(
          T90C, lb1.VaporTemperature);

      Assert.Equal(MF60, lb2.MassFraction, precision: 3);
      Assert.Equal(lb1.Enthalpy, lb2.Enthalpy, precision: 3);
    }

    #endregion

    #region 引数チェックのテスト

    /// <summary>負の温度（[K]）で PopoloOutOfRangeException が発生する</summary>
    [Fact]
    public void GetVaporTemperature_NegativeTemperatureK_ThrowsPopoloOutOfRangeException()
    {
      Assert.Throws<PopoloOutOfRangeException>(
          () => LithiumBromide.GetVaporTemperatureFromLiquidTemperatureAndMassFraction(
              -1.0, 0.55));
    }

    /// <summary>質量分率が範囲外で PopoloOutOfRangeException が発生する</summary>
    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void GetVaporTemperature_InvalidMassFraction_ThrowsPopoloOutOfRangeException(
        double massFraction)
    {
      Assert.Throws<PopoloOutOfRangeException>(
          () => LithiumBromide.GetVaporTemperatureFromLiquidTemperatureAndMassFraction(
              T90C, massFraction));
    }

    #endregion
  }
}
