/* LithiumBromideTests.cs
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

    #region Saturation temperature, solution temperature, and mass fraction calculation tests

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

    #region Specific enthalpy calculation tests

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

    #region Other physical property tests

    /// <summary>
    /// 密度は実測値と整合する（質量分率0.55, 80°C で約1590 kg/m3）。
    /// 従来の質量分率による単純平均（3460*0.55 + 972*0.45 ≒ 2340 kg/m3）は約45%過大だった。
    /// </summary>
    [Fact]
    public void GetDensity_ReturnsPhysicallyReasonableValue()
    {
      // Królikowska et al. (2021) Table 7: x1=0.206 (w≒0.556) で 348.15K: 1.5930 g/cm3
      // 質量分率0.55, 353.15K ではこれよりわずかに小さい 1580〜1600 kg/m3 程度
      double rho = LithiumBromide.GetDensity(T80C, MF55);
      Assert.InRange(rho, 1560.0, 1620.0);
    }

    /// <summary>
    /// 密度が文献の実測値と 1.5% 以内で一致する。
    /// 参照値：Królikowska M., et al., "Vapor Pressure and Physicochemical Properties of
    /// {LiBr + IL-Based Additive + Water} Mixtures: Experimental Data and COSMO-RS Predictions",
    /// J. Solution Chem. 50 (2021) 473–502, Table 7（振動式密度計、u(ρ)=5e-4 g/cm3, u(x1)=1e-3）。
    /// 表のモル分率 x1 は LiBr 86.845 g/mol、水 18.015 g/mol で質量分率に換算する。
    /// </summary>
    [Theory]
    [InlineData(0.246, 298.15, 1735.1)]
    [InlineData(0.246, 348.15, 1700.4)]
    [InlineData(0.206, 298.15, 1624.1)]
    [InlineData(0.206, 323.15, 1608.7)]
    [InlineData(0.170, 348.15, 1488.4)]
    [InlineData(0.129, 298.15, 1401.8)]
    [InlineData(0.058, 323.15, 1172.7)]
    public void GetDensity_AgreesWithMeasuredData(
        double moleFraction, double temperature, double measuredDensity)
    {
      const double M_LIBR = 86.845;
      const double M_H2O = 18.015;
      double mf = moleFraction * M_LIBR
          / (moleFraction * M_LIBR + (1.0 - moleFraction) * M_H2O);
      double rho = LithiumBromide.GetDensity(temperature, mf);
      Assert.InRange(rho, measuredDensity * 0.985, measuredDensity * 1.015);
    }

    /// <summary>質量分率0で純水の飽和液密度に一致する（25°C: 997.0 kg/m3）</summary>
    [Fact]
    public void GetDensity_ZeroMassFraction_EqualsWaterDensity()
    {
      double rho = LithiumBromide.GetDensity(298.15, 0.0);
      Assert.Equal(997.0, rho, 0.5);
    }

    /// <summary>密度は温度の減少関数である</summary>
    [Fact]
    public void GetDensity_IsDecreasingWithTemperature()
    {
      double rho1 = LithiumBromide.GetDensity(T10C, MF55);
      double rho2 = LithiumBromide.GetDensity(T80C, MF55);
      double rho3 = LithiumBromide.GetDensity(T90C, MF55);
      Assert.True(rho1 > rho2 && rho2 > rho3);
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

    #region Factory method tests

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

    #region Argument check tests

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

    /// <summary>水の臨界温度以上では密度計算で PopoloOutOfRangeException が発生する</summary>
    [Fact]
    public void GetDensity_AboveWaterCriticalTemperature_ThrowsPopoloOutOfRangeException()
    {
      Assert.Throws<PopoloOutOfRangeException>(
          () => LithiumBromide.GetDensity(650.0, 0.55));
    }

    #endregion
  }
}
