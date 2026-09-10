/* PowerConverterTests.cs
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

using Xunit;
using Popolo.Core.Energy;

namespace Popolo.Core.Tests.Energy
{
  /// <summary>PowerConverter のテスト</summary>
  /// <remarks>
  /// 期待値の根拠（正規化2次損失モデル: η(p) = p / (p + a·p² + b·p + c)）：
  /// デフォルト係数 a=0.01, b=0.01, c=0.03 のとき、
  /// - η(1.0) = 1/(1+0.01+0.01+0.03) = 1/1.05 = 0.952381
  /// - η(0.2) = 0.2/(0.2+0.0004+0.002+0.03) = 0.2/0.2324 = 0.860585
  /// - η(0.05) = 0.05/(0.05+0.000025+0.0005+0.03) = 0.05/0.080525 = 0.620925
  /// 低負荷では無負荷損係数cが支配的になり効率が急落する。
  /// </remarks>
  public class PowerConverterTests
  {

    #region Constructor tests

    /// <summary>デフォルトコンストラクタで既定の損失係数が設定される</summary>
    [Fact]
    public void Constructor_Default_SetsDefaultCoefficients()
    {
      var conv = new PowerConverter(3000);

      Assert.Equal(3000, conv.RatedPower, precision: 6);
      Assert.Equal(PowerConverter.DefaultCoefficientA, conv.CoefficientA, precision: 6);
      Assert.Equal(PowerConverter.DefaultCoefficientB, conv.CoefficientB, precision: 6);
      Assert.Equal(PowerConverter.DefaultCoefficientC, conv.CoefficientC, precision: 6);
    }

    /// <summary>係数指定コンストラクタで係数が設定される</summary>
    [Fact]
    public void Constructor_WithCoefficients_SetsCoefficients()
    {
      var conv = new PowerConverter(5000, 0.02, 0.005, 0.015);

      Assert.Equal(0.02, conv.CoefficientA, precision: 6);
      Assert.Equal(0.005, conv.CoefficientB, precision: 6);
      Assert.Equal(0.015, conv.CoefficientC, precision: 6);
    }

    /// <summary>負の係数は0にクランプされる</summary>
    [Fact]
    public void Coefficients_Negative_ClampedToZero()
    {
      var conv = new PowerConverter(3000, -0.1, -0.1, -0.1);

      Assert.Equal(0, conv.CoefficientA, precision: 6);
      Assert.Equal(0, conv.CoefficientB, precision: 6);
      Assert.Equal(0, conv.CoefficientC, precision: 6);
    }

    #endregion

    #region Efficiency tests

    /// <summary>デフォルト係数での効率の手計算値と一致する</summary>
    [Theory]
    [InlineData(1.00, 0.9523810)]
    [InlineData(0.20, 0.8605852)]
    [InlineData(0.05, 0.6209252)]
    public void GetEfficiencyAtLoadRatio_DefaultCoefficients_MatchesHandCalculation(
        double loadRatio, double expected)
    {
      var conv = new PowerConverter(3000);
      Assert.Equal(expected, conv.GetEfficiencyAtLoadRatio(loadRatio), precision: 6);
    }

    /// <summary>電力指定の効率は負荷率指定の効率と一致する</summary>
    [Fact]
    public void GetEfficiency_MatchesLoadRatioBasis()
    {
      var conv = new PowerConverter(1000);
      Assert.Equal(
          conv.GetEfficiencyAtLoadRatio(0.2),
          conv.GetEfficiency(200), precision: 10);
    }

    /// <summary>負荷率が1を超える場合は1にクランプされる</summary>
    [Fact]
    public void GetEfficiencyAtLoadRatio_OverUnity_ClampedToRated()
    {
      var conv = new PowerConverter(3000);
      Assert.Equal(
          conv.GetEfficiencyAtLoadRatio(1.0),
          conv.GetEfficiencyAtLoadRatio(1.5), precision: 10);
    }

    /// <summary>電力ゼロ以下では効率0を返す</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public void GetEfficiency_ZeroOrNegative_ReturnsZero(double power)
    {
      var conv = new PowerConverter(3000);
      Assert.Equal(0, conv.GetEfficiency(power), precision: 10);
    }

    /// <summary>低負荷になるほど効率が低下する（無負荷損の影響）</summary>
    [Fact]
    public void GetEfficiencyAtLoadRatio_LowerLoad_LowerEfficiency()
    {
      var conv = new PowerConverter(3000);
      double e50 = conv.GetEfficiencyAtLoadRatio(0.5);
      double e20 = conv.GetEfficiencyAtLoadRatio(0.2);
      double e05 = conv.GetEfficiencyAtLoadRatio(0.05);

      Assert.True(e50 > e20 && e20 > e05,
          $"Expected η(0.5)={e50:F4} > η(0.2)={e20:F4} > η(0.05)={e05:F4}");
    }

    #endregion

    #region Coefficient estimation tests

    /// <summary>3点の運転点から損失係数を復元できる</summary>
    [Fact]
    public void EstimateCoefficients_RecoverKnownCoefficients()
    {
      //既知の係数からη値を生成し、その3点から係数を逆推定する
      var original = new PowerConverter(1000, 0.02, 0.005, 0.01);
      double e1 = original.GetEfficiencyAtLoadRatio(0.25);
      double e2 = original.GetEfficiencyAtLoadRatio(0.50);
      double e3 = original.GetEfficiencyAtLoadRatio(1.00);

      var (a, b, c) = PowerConverter.EstimateCoefficients(
          0.25, e1, 0.50, e2, 1.00, e3);

      Assert.Equal(0.02, a, precision: 8);
      Assert.Equal(0.005, b, precision: 8);
      Assert.Equal(0.01, c, precision: 8);
    }

    #endregion

    #region IReadOnlyPowerConverter tests

    /// <summary>IReadOnlyPowerConverterとして参照できる</summary>
    [Fact]
    public void Converter_ImplementsIReadOnlyPowerConverter()
    {
      var conv = new PowerConverter(3000);

      IReadOnlyPowerConverter readOnly = conv;
      Assert.Equal(3000, readOnly.RatedPower, precision: 6);
      Assert.Equal(conv.GetEfficiency(1500), readOnly.GetEfficiency(1500), precision: 10);
    }

    #endregion

  }
}
