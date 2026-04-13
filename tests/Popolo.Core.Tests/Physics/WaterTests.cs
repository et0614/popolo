/* WaterTests.cs
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
  /// <summary>Water のテスト</summary>
  /// <remarks>
  /// 期待値は以下の文献に基づく：
  /// - IAPWS-IF97 実用国際状態式
  /// - Irvine &amp; Liley, "Steam and Gas Tables with Computer Equations," 1984.
  /// - 日本機械学会 蒸気表
  /// </remarks>
  public class WaterTests
  {

    #region 公開定数のテスト

    /// <summary>臨界温度が正しい値を持つ</summary>
    [Fact]
    public void CriticalTemperature_HasCorrectValue()
    {
      Assert.Equal(647.096, Water.CriticalTemperature, precision: 3);
    }

    /// <summary>三重点蒸発潜熱が正しい値を持つ</summary>
    [Fact]
    public void VaporizationHeatAtTriplePoint_HasCorrectValue()
    {
      Assert.Equal(2500.9, Water.VaporizationHeatAtTriplePoint, precision: 1);
    }

    #endregion

    #region GetSaturationPressure のテスト

    /// <summary>100°C での飽和水蒸気圧は大気圧（101.325 kPa）に近い</summary>
    [Fact]
    public void GetSaturationPressure_At100C_ReturnsAtmosphericPressure()
    {
      double p = Water.GetSaturationPressure(100.0);
      Assert.InRange(p, 100.9, 101.9);
    }

    /// <summary>0°C での飽和水蒸気圧は約 0.6113 kPa（三重点）</summary>
    [Fact]
    public void GetSaturationPressure_At0C_ReturnsTriplePointPressure()
    {
      double p = Water.GetSaturationPressure(0.0);
      Assert.Equal(0.6113, p, precision: 3);
    }

    /// <summary>-10°C での飽和水蒸気圧（氷点下・Wexler-Hyland式）</summary>
    [Fact]
    public void GetSaturationPressure_AtMinus10C_ReturnsCorrectValue()
    {
      //文献値: -10°C -> 0.2599 kPa
      double p = Water.GetSaturationPressure(-10.0);
      Assert.Equal(0.2599, p, precision: 3);
    }

    /// <summary>飽和水蒸気圧は温度の単調増加関数である</summary>
    [Fact]
    public void GetSaturationPressure_IsMonotonicallyIncreasing()
    {
      double p1 = Water.GetSaturationPressure(20.0);
      double p2 = Water.GetSaturationPressure(50.0);
      double p3 = Water.GetSaturationPressure(80.0);
      Assert.True(p1 < p2 && p2 < p3);
    }

    #endregion

    #region GetSaturationTemperature のテスト

    /// <summary>101.325 kPa（大気圧）での飽和温度は 100°C</summary>
    [Fact]
    public void GetSaturationTemperature_AtAtmosphericPressure_Returns100C()
    {
      double t = Water.GetSaturationTemperature(101.325);
      Assert.Equal(100.0, t, precision: 1);
    }

    /// <summary>GetSaturationPressure と GetSaturationTemperature が互いに逆関数になる</summary>
    [Theory]
    [InlineData(20.0)]
    [InlineData(50.0)]
    [InlineData(100.0)]
    [InlineData(150.0)]
    public void GetSaturationTemperature_IsInverseOfGetSaturationPressure(double temperature)
    {
      double p = Water.GetSaturationPressure(temperature);
      double t = Water.GetSaturationTemperature(p);
      Assert.Equal(temperature, t, precision: 3);
    }

    #endregion

    #region GetVaporizationLatentHeat のテスト

    /// <summary>100°C での蒸発潜熱は約 2256 kJ/kg</summary>
    [Fact]
    public void GetVaporizationLatentHeat_At100C_ReturnsCorrectValue()
    {
      double L = Water.GetVaporizationLatentHeat(100.0);
      Assert.Equal(2256.4, L, precision: 0);
    }

    /// <summary>0°C での蒸発潜熱は三重点の値（2500.9 kJ/kg）に近い</summary>
    [Fact]
    public void GetVaporizationLatentHeat_At0C_ReturnsTriplePointValue()
    {
      double L = Water.GetVaporizationLatentHeat(0.0);
      Assert.Equal(Water.VaporizationHeatAtTriplePoint, L, precision: 0);
    }

    /// <summary>蒸発潜熱は温度の単調減少関数である</summary>
    [Fact]
    public void GetVaporizationLatentHeat_IsMonotonicallyDecreasing()
    {
      double L1 = Water.GetVaporizationLatentHeat(20.0);
      double L2 = Water.GetVaporizationLatentHeat(60.0);
      double L3 = Water.GetVaporizationLatentHeat(100.0);
      Assert.True(L1 > L2 && L2 > L3);
    }

    #endregion

    #region 飽和液の物性値のテスト

    /// <summary>100°C での飽和液エンタルピーは約 419 kJ/kg</summary>
    [Fact]
    public void GetSaturatedLiquidEnthalpy_At100C_ReturnsCorrectValue()
    {
      double h = Water.GetSaturatedLiquidEnthalpy(100.0);
      Assert.InRange(h, 417.0, 422.0);
    }

    /// <summary>飽和液エンタルピーはエントロピー・蒸発潜熱と熱力学的に整合する</summary>
    /// <remarks>クラウジウス-クラペイロン関係: L = T * (sv - sl) の近似確認</remarks>
    [Fact]
    public void GetSaturatedLiquidEntropy_At100C_ReturnsCorrectValue()
    {
      //文献値: 100°C -> sl = 1.307 kJ/(kg·K)
      double s = Water.GetSaturatedLiquidEntropy(100.0);
      Assert.Equal(1.307, s, precision: 2);
    }

    #endregion

    #region 飽和蒸気の物性値のテスト

    /// <summary>100°C での飽和蒸気エンタルピーは約 2676 kJ/kg</summary>
    [Fact]
    public void GetSaturatedVaporEnthalpy_At100C_ReturnsCorrectValue()
    {
      double h = Water.GetSaturatedVaporEnthalpy(100.0);
      Assert.Equal(2676.0, h, precision: 0);
    }

    /// <summary>100°C での飽和蒸気エントロピーは約 7.355 kJ/(kg·K)</summary>
    [Fact]
    public void GetSaturatedVaporEntropy_At100C_ReturnsCorrectValue()
    {
      double s = Water.GetSaturatedVaporEntropy(100.0);
      Assert.InRange(s, 7.30, 7.41);
    }

    /// <summary>蒸発エンタルピー = 飽和蒸気エンタルピー - 飽和液エンタルピー ≒ 蒸発潜熱</summary>
    [Theory]
    [InlineData(50.0)]
    [InlineData(100.0)]
    [InlineData(150.0)]
    public void SteamTableConsistency_VaporizationEnthalpy(double temperature)
    {
      double hv = Water.GetSaturatedVaporEnthalpy(temperature);
      double hl = Water.GetSaturatedLiquidEnthalpy(temperature);
      double L = Water.GetVaporizationLatentHeat(temperature);
      // hv - hl ≒ L（蒸発潜熱）
      Assert.Equal(L, hv - hl, precision: 0);
    }

    #endregion

    #region 液体水の物性値のテスト

    /// <summary>4°C での液体水密度は約 999.8 kg/m³（最大密度付近）</summary>
    [Fact]
    public void GetLiquidDensity_At4C_ReturnsMaximumDensity()
    {
      double rho = Water.GetLiquidDensity(4.0);
      Assert.InRange(rho, 999.0, 1000.5);
    }

    /// <summary>20°C での液体水密度は約 998.2 kg/m³</summary>
    [Fact]
    public void GetLiquidDensity_At20C_ReturnsCorrectValue()
    {
      double rho = Water.GetLiquidDensity(20.0);
      Assert.Equal(998.2, rho, precision: 0);
    }

    /// <summary>液体水の熱拡散率はα = λ/(ρ·cp) と整合する</summary>
    [Fact]
    public void GetLiquidThermalDiffusivity_At20C_IsConsistentWithComponents()
    {
      double lambda = Water.GetLiquidThermalConductivity(20.0);
      double cp = Water.GetLiquidIsobaricSpecificHeat(20.0);
      double rho = Water.GetLiquidDensity(20.0);
      double alpha = lambda / (1000.0 * cp * rho);
      double alphaDirect = Water.GetLiquidThermalDiffusivity(20.0);
      Assert.Equal(alpha, alphaDirect, precision: 10);
    }

    /// <summary>液体水の動粘性係数はν = μ/ρ と整合する</summary>
    [Fact]
    public void GetLiquidDynamicViscosity_At20C_IsConsistentWithComponents()
    {
      double mu = Water.GetLiquidViscosity(20.0);
      double rho = Water.GetLiquidDensity(20.0);
      double nu = mu / rho;
      double nuDirect = Water.GetLiquidDynamicViscosity(20.0);
      Assert.Equal(nu, nuDirect, precision: 10);
    }

    #endregion

    #region 過熱蒸気の物性値のテスト

    /// <summary>100°C, 101.325 kPa での過熱蒸気エンタルピーは飽和蒸気エンタルピーに近い</summary>
    [Fact]
    public void GetSuperheatedVaporEnthalpy_AtSaturationPoint_NearSaturatedValue()
    {
      double hSuperheated = Water.GetSuperheatedVaporEnthalpy(101.325, 100.0);
      double hSaturated = Water.GetSaturatedVaporEnthalpy(100.0);
      //飽和点での過熱蒸気≒飽和蒸気（誤差±5 kJ/kg以内）
      Assert.InRange(hSuperheated, hSaturated - 5, hSaturated + 5);
    }

    /// <summary>200°C, 101.325 kPa での過熱蒸気エンタルピーは約 2875 kJ/kg</summary>
    [Fact]
    public void GetSuperheatedVaporEnthalpy_At200C_ReturnsCorrectValue()
    {
      double h = Water.GetSuperheatedVaporEnthalpy(101.325, 200.0);
      Assert.InRange(h, 2872.0, 2879.0);
    }

    /// <summary>過熱蒸気比体積は理想気体近似と整合する（低圧時）</summary>
    [Fact]
    public void GetSuperheatedVaporSpecificVolume_LowPressure_NearIdealGas()
    {
      //理想気体: v = RT/P, R_water = 0.4615 kJ/(kg·K)
      double T = 100.0 + 273.15;  // K
      double P = 101.325;         // kPa
      double vIdeal = 0.4615 * T / P;
      double vActual = Water.GetSuperheatedVaporSpecificVolume(P, 100.0);
      //低圧では理想気体に近い（5%以内）
      Assert.InRange(vActual, vIdeal * 0.95, vIdeal * 1.05);
    }

    /// <summary>
    /// GetSuperheatedVaporTemperature と GetSuperheatedVaporEntropy が互いに逆関数になる
    /// </summary>
    [Fact]
    public void GetSuperheatedVaporTemperature_IsInverseOfGetSuperheatedVaporEntropy()
    {
      double pressure = 500.0;    // kPa
      double temperature = 200.0; // °C
      double entropy = Water.GetSuperheatedVaporEntropy(pressure, temperature);
      double tRecovered = Water.GetSuperheatedVaporTemperature(pressure, entropy);
      Assert.Equal(temperature, tRecovered, precision: 1);
    }

    /// <summary>有効範囲外の温度で PopoloArgumentException が発生する</summary>
    [Theory]
    [InlineData(26.0)]    // 299 K（下限300K未満）
    [InlineData(3228.0)]  // 3501 K（上限3500K超）
    public void GetSuperheatedVaporIsobaricSpecificHeat_OutOfRange_ThrowsPopoloArgumentException(
        double temperature)
    {
      var ex = Assert.Throws<PopoloArgumentException>(
          () => Water.GetSuperheatedVaporIsobaricSpecificHeat(temperature));
      Assert.Equal("temperature", ex.ParamName);
    }

    /// <summary>有効範囲内の温度で正しく計算できる</summary>
    [Fact]
    public void GetSuperheatedVaporIsobaricSpecificHeat_At100C_ReturnsReasonableValue()
    {
      //水蒸気の比熱は約 1.87-2.1 kJ/(kg·K) の範囲
      double cp = Water.GetSuperheatedVaporIsobaricSpecificHeat(100.0);
      Assert.InRange(cp, 1.8, 2.2);
    }

    #endregion

  }
}
