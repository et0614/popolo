/* RefrigerantTests.cs
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
  /// <summary>Refrigerant のテスト</summary>
  /// <remarks>
  /// 期待値の根拠：
  /// - Togashi (2014) Table 8: 蒸発温度5°C での冷凍サイクル計算結果
  ///   ①圧縮機入口: P=949kPa, ρ=24.9kg/m³, T=283.2K, h=523.1kJ/kg, s=2.161kJ/(kg·K)
  ///   ②圧縮機出口: P=4000kPa, ρ=85.8kg/m³, T=378.2K, h=582.2kJ/kg, s=2.161kJ/(kg·K)
  ///   ③凝縮器出口: P=4000kPa, ρ=813.0kg/m³, T=328.7K, h=309.5kJ/kg
  ///   ④膨張弁出口: P=949kPa, h=309.5kJ/kg（二相域）
  ///
  /// 近似式の誤差（論文Table9より）：
  /// - 圧縮仕事・冷却量：約3〜4%以内
  /// - COP：約4%以内
  /// テストの許容誤差はこれに基づき設定する。
  /// </remarks>
  public class RefrigerantTests
  {
    #region R32 のテスト

    private readonly Refrigerant _r32 = new Refrigerant(Refrigerant.Fluid.R32);

    /// <summary>臨界温度が正しい値を持つ（R32: 78.105°C = 351.255K）</summary>
    [Fact]
    public void R32_CriticalTemperature_IsCorrect()
    {
      Assert.Equal(351.255, _r32.CriticalTemperature, precision: 2);
    }

    /// <summary>飽和状態計算：蒸発圧力949kPa での飽和温度は約5°C = 278.15K</summary>
    [Fact]
    public void R32_GetSaturatedPropertyFromPressure_AtEvaporatingPoint()
    {
      _r32.GetSaturatedPropertyFromPressure(949,
          out double rhoL, out double rhoV, out double tSat);

      Assert.InRange(tSat, 276.0, 280.0);   // ≒ 278.15K（5°C）
      Assert.True(rhoL > rhoV);              // 液体密度 > 気体密度
      Assert.InRange(rhoV, 20.0, 30.0);      // ≒ 24.9 kg/m³（論文Table8 ①）
    }

    /// <summary>飽和状態計算と逆算の整合性</summary>
    [Theory]
    [InlineData(949)]
    [InlineData(2000)]
    [InlineData(4000)]
    public void R32_GetSaturatedPropertyFromPressure_IsInverseOfFromTemperature(double pressure)
    {
      _r32.GetSaturatedPropertyFromPressure(pressure,
          out _, out _, out double tSat);
      _r32.GetSaturatedPropertyFromTemperature(tSat,
          out _, out _, out double pressureRecovered);

      Assert.Equal(pressure, pressureRecovered, precision: 0);
    }

    /// <summary>圧縮機入口状態（過熱蒸気）：P=949kPa, T=283.2K</summary>
    [Fact]
    public void R32_GetStateFromPressureAndTemperature_SuperheatedVapor()
    {
      //論文Table8 ①: h=523.1 kJ/kg, s=2.161 kJ/(kg·K)
      _r32.GetStateFromPressureAndTemperature(949, 283.2,
          out double s, out double rho, out double h, out double _);

      Assert.InRange(h, 510.0, 536.0);     // 523.1 ± 2.5%
      Assert.InRange(s, 2.10, 2.22);        // 2.161 ± 2.8%
      Assert.InRange(rho, 22.0, 28.0);      // 24.9 ± 12%（密度は収束初期値依存）
    }

    /// <summary>圧縮機出口状態（過熱蒸気）：P=4000kPa, T=378.2K</summary>
    [Fact]
    public void R32_GetStateFromPressureAndTemperature_HighPressureVapor()
    {
      //論文Table8 ②: h=582.2 kJ/kg, s=2.161 kJ/(kg·K)
      _r32.GetStateFromPressureAndTemperature(4000, 378.2,
          out double s, out double rho, out double h, out double _);

      Assert.InRange(h, 567.0, 597.0);     // 582.2 ± 2.6%
      Assert.InRange(s, 2.10, 2.22);        // 2.161 ± 2.8%
    }

    /// <summary>凝縮器出口状態（過冷却液体）：P=4000kPa, T=328.7K</summary>
    [Fact]
    public void R32_GetStateFromPressureAndTemperature_SubcooledLiquid()
    {
      //論文Table8 ③: h=309.5 kJ/kg, ρ=813.0 kg/m³
      _r32.GetStateFromPressureAndTemperature(4000, 328.7,
          out double _, out double rho, out double h, out double _);

      Assert.InRange(h, 301.0, 318.0);     // 309.5 ± 2.7%
      Assert.InRange(rho, 790.0, 836.0);    // 813.0 ± 2.8%
    }

    /// <summary>エンタルピー・圧力から状態計算：圧縮機出口（等エントロピー圧縮後）</summary>
    [Fact]
    public void R32_GetStateFromPressureAndEntropy_IsentropicCompression()
    {
      //①の状態: P=949kPa での比エントロピーを計算
      _r32.GetStateFromPressureAndTemperature(949, 283.2,
          out double s1, out double _, out double _, out double _);

      //②の圧力4000kPa で同じエントロピーから温度を逆算
      _r32.GetStateFromPressureAndEntropy(4000, s1,
          out double t2, out double _, out double h2, out double _);

      //論文Table8 ②: T=378.2K, h=582.2kJ/kg
      Assert.InRange(t2, 368.0, 388.0);    // 378.2 ± 2.7%
      Assert.InRange(h2, 567.0, 597.0);     // 582.2 ± 2.6%
    }

    /// <summary>膨張弁出口（二相域）：h=309.5kJ/kg, P=949kPa</summary>
    [Fact]
    public void R32_GetStateFromPressureAndEnthalpy_TwoPhaseRegion()
    {
      //論文Table8 ④: 二相域
      _r32.GetStateFromPressureAndEnthalpy(949, 309.5,
          out double t, out double rho, out double _, out double _);

      //二相域では温度 = 飽和温度
      _r32.GetSaturatedPropertyFromPressure(949, out _, out _, out double tSat);
      Assert.Equal(tSat, t, precision: 3);
    }

    /// <summary>冷凍サイクルCOPの検証（論文Table9の蒸発温度5°C）</summary>
    [Fact]
    public void R32_RefrigerationCycleCOP_WithinPaperAccuracy()
    {
      //論文と同じ冷凍サイクル計算（蒸発温度5°C, 凝縮温度≒61°C対応4MPa）
      double Pevap = 949;   // kPa（蒸発圧力）
      double Pcond = 4000;  // kPa（凝縮圧力）

      //①圧縮機入口：蒸発温度 + 過熱度5°C
      _r32.GetSaturatedPropertyFromPressure(Pevap, out _, out _, out double tEvap);
      _r32.GetStateFromPressureAndTemperature(Pevap, tEvap + 5,
          out double s1, out _, out double h1, out _);

      //②圧縮機出口：等エントロピー圧縮
      _r32.GetStateFromPressureAndEntropy(Pcond, s1,
          out _, out _, out double h2, out _);

      //③凝縮器出口：過冷却度10°C
      _r32.GetSaturatedPropertyFromPressure(Pcond, out _, out _, out double tCond);
      _r32.GetStateFromPressureAndTemperature(Pcond, tCond - 10,
          out _, out _, out double h3, out _);

      //冷凍サイクル計算
      double compressionWork = h2 - h1;
      double coolingCapacity = h1 - h3;
      double cop = coolingCapacity / compressionWork;

      //論文Table9（蒸発温度5°C）: 圧縮仕事59.08, 冷却量213.62, COP=3.62
      Assert.InRange(compressionWork, 55.0, 63.0);  // 59.08 ± 6%
      Assert.InRange(coolingCapacity, 200.0, 227.0); // 213.62 ± 6%
      Assert.InRange(cop, 3.2, 4.0);                 // 3.62 ± ~10%（COPは誤差が重なる）
    }

    /// <summary>PVT関係式の整合性：圧力→密度→圧力の逆算</summary>
    [Theory]
    [InlineData(949, 283.2)]  // 気相
    [InlineData(4000, 328.7)]  // 液相
    public void R32_PVT_RoundTrip(double pressure, double temperature)
    {
      _r32.GetStateFromPressureAndTemperature(pressure, temperature,
          out _, out double density, out _, out _);
      double pressureRecovered =
          _r32.GetPressureFromTemperatureAndDensity(temperature, density);
      Assert.Equal(pressure, pressureRecovered, precision: 0);
    }

    #endregion

    #region R410A のテスト

    private readonly Refrigerant _r410a = new Refrigerant(Refrigerant.Fluid.R410A);

    /// <summary>臨界温度が正しい値を持つ（R410A: 71.358°C = 344.508K）</summary>
    [Fact]
    public void R410A_CriticalTemperature_IsCorrect()
    {
      Assert.Equal(344.508, _r410a.CriticalTemperature, precision: 2);
    }

    /// <summary>飽和状態計算と逆算の整合性</summary>
    [Theory]
    [InlineData(950)]
    [InlineData(2000)]
    [InlineData(3500)]
    public void R410A_SaturatedProperty_RoundTrip(double pressure)
    {
      _r410a.GetSaturatedPropertyFromPressure(pressure,
          out _, out _, out double tSat);
      _r410a.GetSaturatedPropertyFromTemperature(tSat,
          out _, out _, out double pressureRecovered);
      Assert.Equal(pressure, pressureRecovered, precision: 0);
    }

    /// <summary>過熱蒸気でエンタルピー・エントロピーが物理的に妥当な値</summary>
    [Fact]
    public void R410A_SuperheatedVaporProperties_ArePhysicallyReasonable()
    {
      _r410a.GetStateFromPressureAndTemperature(1000, 290,
          out double s, out double rho, out double h, out _);

      Assert.True(h > 0, "Enthalpy should be positive");
      Assert.True(s > 0, "Entropy should be positive");
      Assert.True(rho > 0, "Density should be positive");
    }

    #endregion

    #region R134a のテスト

    private readonly Refrigerant _r134a = new Refrigerant(Refrigerant.Fluid.R134a);

    /// <summary>臨界温度が正しい値を持つ（R134a: 101.06°C = 374.21K）</summary>
    [Fact]
    public void R134a_CriticalTemperature_IsCorrect()
    {
      Assert.Equal(374.21, _r134a.CriticalTemperature, precision: 1);
    }

    /// <summary>飽和状態計算と逆算の整合性</summary>
    [Theory]
    [InlineData(300)]
    [InlineData(600)]
    [InlineData(1200)]
    public void R134a_SaturatedProperty_RoundTrip(double pressure)
    {
      _r134a.GetSaturatedPropertyFromPressure(pressure,
          out _, out _, out double tSat);
      _r134a.GetSaturatedPropertyFromTemperature(tSat,
          out _, out _, out double pressureRecovered);
      Assert.Equal(pressure, pressureRecovered, precision: 0);
    }

    #endregion

    #region 入力検証のテスト

    /// <summary>範囲外の圧力で PopoloOutOfRangeException が発生する（R32）</summary>
    [Theory]
    [InlineData(500)]   // MinPressure=700 未満
    [InlineData(5000)]  // MaxPressure=4500 超
    public void R32_InvalidPressure_ThrowsPopoloOutOfRangeException(double pressure)
    {
      Assert.Throws<PopoloOutOfRangeException>(
          () => _r32.GetSaturatedPropertyFromPressure(
              pressure, out _, out _, out _));
    }

    /// <summary>範囲外の圧力で PopoloOutOfRangeException が発生する（R410A）</summary>
    [Theory]
    [InlineData(500)]   // MinPressure=700 未満
    [InlineData(4500)]  // MaxPressure=4000 超
    public void R410A_InvalidPressure_ThrowsPopoloOutOfRangeException(double pressure)
    {
      Assert.Throws<PopoloOutOfRangeException>(
          () => _r410a.GetSaturatedPropertyFromPressure(
              pressure, out _, out _, out _));
    }

    /// <summary>0K以下の温度で PopoloOutOfRangeException が発生する</summary>
    [Fact]
    public void R32_ZeroTemperature_ThrowsPopoloOutOfRangeException()
    {
      Assert.Throws<PopoloOutOfRangeException>(
          () => _r32.GetSaturatedPropertyFromTemperature(
              0, out _, out _, out _));
    }

    /// <summary>GetStateFromPressureAndEnthalpy でも範囲外圧力で例外が発生する</summary>
    [Fact]
    public void R32_GetStateFromPressureAndEnthalpy_InvalidPressure_Throws()
    {
      Assert.Throws<PopoloOutOfRangeException>(
          () => _r32.GetStateFromPressureAndEnthalpy(
              100, 500, out _, out _, out _, out _));
    }

    #endregion

    #region 基準状態のテスト

    /// <summary>
    /// 基準状態（0°C飽和液）でエンタルピー=200kJ/kg, エントロピー=1.0kJ/(kg·K)
    /// ASHRAE/IIR基準に準拠しているかを確認
    /// </summary>
    [Fact]
    public void R32_ReferenceState_EnthalpyAndEntropyAreCorrect()
    {
      //0°C = 273.15K での飽和液密度を取得
      _r32.GetSaturatedPropertyFromTemperature(273.15,
          out double rhoL, out _, out _);

      double h = _r32.GetEnthalpyFromTemperatureAndDensity(273.15, rhoL);
      double s = _r32.GetEntropyFromTemperatureAndDensity(273.15, rhoL);

      Assert.Equal(200.0, h, precision: 1);
      Assert.Equal(1.0, s, precision: 2);
    }

    /// <summary>R410Aも基準状態が正しい</summary>
    [Fact]
    public void R410A_ReferenceState_EnthalpyAndEntropyAreCorrect()
    {
      _r410a.GetSaturatedPropertyFromTemperature(273.15,
          out double rhoL, out _, out _);

      double h = _r410a.GetEnthalpyFromTemperatureAndDensity(273.15, rhoL);
      double s = _r410a.GetEntropyFromTemperatureAndDensity(273.15, rhoL);

      Assert.Equal(200.0, h, precision: 1);
      Assert.Equal(1.0, s, precision: 2);
    }

    #endregion
  }
}
