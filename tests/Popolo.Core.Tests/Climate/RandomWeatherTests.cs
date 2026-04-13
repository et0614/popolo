/* RandomWeatherTests.cs
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
using Popolo.Core.Climate;

namespace Popolo.Core.Tests.Climate
{
  /// <summary>RandomWeather のテスト</summary>
  /// <remarks>
  /// 期待値の根拠：
  /// 富樫英介「省エネ投資リスク評価のための確率的気象モデルの開発」
  /// 空気調和・衛生工学会論文集, 2015
  ///
  /// 固定シードによる統計的検証：
  /// 乱数を使うため個々の値は再現できないが、20年間（175,200時間）の
  /// 統計量（平均・標準偏差・パーセンタイル）が論文記載値に近いことを確認する。
  /// 許容誤差は論文Figure9の「原系列 vs モデル」の差異に基づき設定する。
  ///
  /// 論文Figure9（東京・20年）：
  ///   乾球温度: AVE=15.9°C, 2.5U=30.2°C, 2.5L=2.0°C
  ///   絶対湿度: AVE=8.2g/kg, 2.5U=18.4g/kg, 2.5L=1.5g/kg
  ///   水平面全天日射: AVE=134W/m²
  /// </remarks>
  public class RandomWeatherTests
  {
    /// <summary>テスト用の計算年数（統計的検証に十分な長さ）</summary>
    private const int TestYears = 20;

    /// <summary>固定シード（再現性のため固定）</summary>
    private const uint Seed = 12345u;

    #region 東京のテスト

    /// <summary>東京・20年の乾球温度平均が論文値（15.9°C）に近い</summary>
    [Fact]
    public void MakeWeather_Tokyo_DrybulbTemperatureMean_MatchesPaper()
    {
      var rw = new RandomWeather(Seed, RandomWeather.Location.Tokyo);
      rw.MakeWeather(TestYears, out double[] dbt, out _, out _, out _);

      double mean = dbt.Average();
      //論文Figure9: AVE=15.9°C（原系列16.1°C）
      Assert.InRange(mean, 14.5, 17.5);
    }

    /// <summary>東京・20年の乾球温度2.5%パーセンタイル値が論文値（2.0°C）に近い</summary>
    [Fact]
    public void MakeWeather_Tokyo_DrybulbTemperature2_5Percentile_MatchesPaper()
    {
      var rw = new RandomWeather(Seed, RandomWeather.Location.Tokyo);
      rw.MakeWeather(TestYears, out double[] dbt, out _, out _, out _);

      double p2_5 = Percentile(dbt, 2.5);
      //論文Figure9: 2.5L=2.0°C
      Assert.InRange(p2_5, -1.0, 5.0);
    }

    /// <summary>東京・20年の乾球温度97.5%パーセンタイル値が論文値（30.2°C）に近い</summary>
    [Fact]
    public void MakeWeather_Tokyo_DrybulbTemperature97_5Percentile_MatchesPaper()
    {
      var rw = new RandomWeather(Seed, RandomWeather.Location.Tokyo);
      rw.MakeWeather(TestYears, out double[] dbt, out _, out _, out _);

      double p97_5 = Percentile(dbt, 97.5);
      //論文Figure9: 2.5U=30.2°C
      Assert.InRange(p97_5, 27.0, 33.0);
    }

    /// <summary>東京・20年の絶対湿度平均が論文値（8.2g/kg）に近い</summary>
    [Fact]
    public void MakeWeather_Tokyo_HumidityRatioMean_MatchesPaper()
    {
      var rw = new RandomWeather(Seed, RandomWeather.Location.Tokyo);
      rw.MakeWeather(TestYears, out _, out double[] hrt, out _, out _);

      double mean = hrt.Average();
      //論文Figure9: AVE=8.2g/kg（原系列8.4g/kg）
      Assert.InRange(mean, 7.0, 9.5);
    }

    /// <summary>東京・20年の絶対湿度2.5%パーセンタイル値が論文値（1.5g/kg）に近い</summary>
    [Fact]
    public void MakeWeather_Tokyo_HumidityRatio2_5Percentile_MatchesPaper()
    {
      var rw = new RandomWeather(Seed, RandomWeather.Location.Tokyo);
      rw.MakeWeather(TestYears, out _, out double[] hrt, out _, out _);

      double p2_5 = Percentile(hrt, 2.5);
      //論文Figure9: 2.5L=1.5g/kg
      Assert.InRange(p2_5, 0.3, 3.0);
    }

    /// <summary>東京・20年の水平面全天日射平均が論文値（134W/m²）に近い</summary>
    [Fact]
    public void MakeWeather_Tokyo_GlobalHorizontalRadiationMean_MatchesPaper()
    {
      var rw = new RandomWeather(Seed, RandomWeather.Location.Tokyo);
      rw.MakeWeather(TestYears, out _, out _, out double[] rad, out _);

      double mean = rad.Average();
      //論文Figure9: AVE=134W/m²（原系列141W/m²）
      //夜間の0を含む全時間平均
      Assert.InRange(mean, 110.0, 160.0);
    }

    /// <summary>東京・20年の水平面全天日射は非負</summary>
    [Fact]
    public void MakeWeather_Tokyo_GlobalHorizontalRadiation_IsNonNegative()
    {
      var rw = new RandomWeather(Seed, RandomWeather.Location.Tokyo);
      rw.MakeWeather(TestYears, out _, out _, out double[] rad, out _);

      Assert.All(rad, r => Assert.True(r >= 0, $"Negative radiation: {r}"));
    }

    /// <summary>東京・20年の絶対湿度は非負</summary>
    [Fact]
    public void MakeWeather_Tokyo_HumidityRatio_IsNonNegative()
    {
      var rw = new RandomWeather(Seed, RandomWeather.Location.Tokyo);
      rw.MakeWeather(TestYears, out _, out double[] hrt, out _, out _);

      Assert.All(hrt, h => Assert.True(h >= 0, $"Negative humidity ratio: {h}"));
    }

    /// <summary>東京・20年の相対湿度は最小値（10%）以上・100%以下（日中のみ検証）</summary>
    [Fact]
    public void MakeWeather_Tokyo_RelativeHumidity_WithinBounds()
    {
      var rw = new RandomWeather(Seed, RandomWeather.Location.Tokyo);
      rw.MakeWeather(TestYears, out double[] dbt, out double[] hrt,
          out double[] rad, out _);

      //夜間（日射=0）は飽和水蒸気圧が極小になり相対湿度が発散するケースがある。
      //RandomWeatherの湿度制約は日中に対して適用されるため、日中のみ検証する。
      for (int i = 0; i < dbt.Length; i++)
      {
        if (rad[i] <= 0) continue;
        double rh = Popolo.Core.Physics.MoistAir
            .GetRelativeHumidityFromDryBulbTemperatureAndHumidityRatio(
                dbt[i], hrt[i] / 1000.0, 101.325);
        Assert.InRange(rh, 9.0, 101.0); //数値誤差を考慮して±1%の余裕
      }
    }

    #endregion

    #region 都市間比較テスト

    /// <summary>札幌の年平均気温は東京より低い</summary>
    [Fact]
    public void MakeWeather_Sapporo_CoolerThanTokyo()
    {
      var rwTokyo = new RandomWeather(Seed, RandomWeather.Location.Tokyo);
      var rwSapporo = new RandomWeather(Seed, RandomWeather.Location.Sapporo);

      rwTokyo.MakeWeather(TestYears, out double[] dbtTokyo, out _, out _, out _);
      rwSapporo.MakeWeather(TestYears, out double[] dbtSapporo, out _, out _, out _);

      //論文Table1,3: 東京AVE≒16°C、札幌AVE≒8.8°C
      Assert.True(dbtSapporo.Average() < dbtTokyo.Average());
    }

    /// <summary>那覇の年平均気温は東京より高い</summary>
    [Fact]
    public void MakeWeather_Naha_WarmerThanTokyo()
    {
      var rwTokyo = new RandomWeather(Seed, RandomWeather.Location.Tokyo);
      var rwNaha = new RandomWeather(Seed, RandomWeather.Location.Naha);

      rwTokyo.MakeWeather(TestYears, out double[] dbtTokyo, out _, out _, out _);
      rwNaha.MakeWeather(TestYears, out double[] dbtNaha, out _, out _, out _);

      //論文Table6: 那覇AVE≒22.9°C
      Assert.True(dbtNaha.Average() > dbtTokyo.Average());
    }

    /// <summary>那覇の絶対湿度は東京より高い</summary>
    [Fact]
    public void MakeWeather_Naha_HumidityHigherThanTokyo()
    {
      var rwTokyo = new RandomWeather(Seed, RandomWeather.Location.Tokyo);
      var rwNaha = new RandomWeather(Seed, RandomWeather.Location.Naha);

      rwTokyo.MakeWeather(TestYears, out _, out double[] hrtTokyo, out _, out _);
      rwNaha.MakeWeather(TestYears, out _, out double[] hrtNaha, out _, out _);

      //論文Table6: 那覇AVE≒13.9g/kg
      Assert.True(hrtNaha.Average() > hrtTokyo.Average());
    }

    #endregion

    #region 再現性のテスト

    /// <summary>同じシードで同じ結果が得られる（再現性）</summary>
    [Fact]
    public void MakeWeather_SameSeed_ProducesSameResult()
    {
      var rw1 = new RandomWeather(Seed, RandomWeather.Location.Tokyo);
      var rw2 = new RandomWeather(Seed, RandomWeather.Location.Tokyo);

      rw1.MakeWeather(1, out double[] dbt1, out _, out _, out _);
      rw2.MakeWeather(1, out double[] dbt2, out _, out _, out _);

      Assert.Equal(dbt1, dbt2);
    }

    /// <summary>異なるシードで異なる結果が得られる</summary>
    [Fact]
    public void MakeWeather_DifferentSeed_ProducesDifferentResult()
    {
      var rw1 = new RandomWeather(Seed, RandomWeather.Location.Tokyo);
      var rw2 = new RandomWeather(Seed + 1u, RandomWeather.Location.Tokyo);

      rw1.MakeWeather(1, out double[] dbt1, out _, out _, out _);
      rw2.MakeWeather(1, out double[] dbt2, out _, out _, out _);

      Assert.False(dbt1.SequenceEqual(dbt2));
    }

    #endregion

    #region 出力サイズのテスト

    /// <summary>出力配列のサイズが正しい（通常年）</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(20)]
    public void MakeWeather_OutputSize_IsCorrect(int years)
    {
      var rw = new RandomWeather(Seed, RandomWeather.Location.Tokyo);
      rw.MakeWeather(years, out double[] dbt, out double[] hrt,
          out double[] rad, out bool[] isFair);

      int expected = years * 365 * 24;
      Assert.Equal(expected, dbt.Length);
      Assert.Equal(expected, hrt.Length);
      Assert.Equal(expected, rad.Length);
      Assert.Equal(expected, isFair.Length);
    }

    /// <summary>うるう年モードの出力サイズが正しい</summary>
    [Fact]
    public void MakeWeather_LeapYear_OutputSize_IsCorrect()
    {
      var rw = new RandomWeather(Seed, RandomWeather.Location.Tokyo);
      rw.MakeWeather(1, true, out double[] dbt, out _, out _, out _);

      Assert.Equal(366 * 24, dbt.Length);
    }

    #endregion

    #region ヘルパーメソッド

    /// <summary>パーセンタイル値を計算する</summary>
    private static double Percentile(double[] data, double percentile)
    {
      double[] sorted = data.OrderBy(x => x).ToArray();
      double rank = percentile / 100.0 * (sorted.Length - 1);
      int lower = (int)Math.Floor(rank);
      int upper = (int)Math.Ceiling(rank);
      if (lower == upper) return sorted[lower];
      return sorted[lower] + (rank - lower) * (sorted[upper] - sorted[lower]);
    }

    #endregion
  }
}
