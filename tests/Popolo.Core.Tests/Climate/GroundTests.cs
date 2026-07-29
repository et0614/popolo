/* GroundTests.cs
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
using Popolo.Core.Climate;

namespace Popolo.Core.Tests.Climate
{
  /// <summary>Ground のテスト (Watanabe 1964 モデル)</summary>
  /// <remarks>
  /// 東京の代表的な気象値を基準に使用 (BESTEST Case 990 流に、日最高の月平均と
  /// 日最低の月平均から年較差を取る):
  /// - 年平均気温：15.4 °C
  /// - 最暖月の日最高の平均：約 32 °C
  /// - 最冷月の日最低の平均：約 0 °C
  /// - 年較差：約 32 °C
  /// - 最高温度日：208 日 (7 月下旬)
  /// </remarks>
  public class GroundTests
  {
    private const int PeakDay = 208;
    private const double WarmMax = 32.0;
    private const double ColdMin = 0.0;
    private const double TempRange = WarmMax - ColdMin;
    private const double MeanTemp = 15.4;

    #region Constructor tests

    /// <summary>コンストラクタで正しくプロパティが設定される</summary>
    [Fact]
    public void Constructor_SetsProperties()
    {
      var ground = new Ground(PeakDay, WarmMax, ColdMin, MeanTemp);
      Assert.Equal(PeakDay, ground.PeakDayOfYear);
      Assert.Equal(WarmMax, ground.WarmestMonthlyMeanDailyMax);
      Assert.Equal(ColdMin, ground.ColdestMonthlyMeanDailyMin);
      Assert.Equal(MeanTemp, ground.AnnualAverageTemperature);
      Assert.Equal(TempRange, ground.AnnualTemperatureRange);
    }

    #endregion

    #region Physical plausibility tests of ground temperature

    /// <summary>年平均気温より振幅は小さい (地中は外気より変動が小さい)</summary>
    [Theory]
    [InlineData(1.0)]
    [InlineData(3.0)]
    [InlineData(5.0)]
    public void GetTemperature_DeepGround_CloserToMean(double depth)
    {
      var ground = new Ground(PeakDay, WarmMax, ColdMin, MeanTemp);
      double max = double.MinValue;
      double min = double.MaxValue;
      for (int d = 1; d <= 365; d++)
      {
        double t = ground.GetTemperature(d, depth);
        if (t > max) max = t;
        if (t < min) min = t;
      }
      double amplitude = (max - min) / 2.0;
      //地中の振幅は外気の振幅 (TempRange/2) より小さい
      Assert.True(amplitude < TempRange / 2.0);
    }

    /// <summary>深くなるほど振幅が小さくなる</summary>
    [Fact]
    public void GetTemperature_DeeperDepth_SmallerAmplitude()
    {
      var ground = new Ground(PeakDay, WarmMax, ColdMin, MeanTemp);

      double GetAmplitude(double depth)
      {
        double max = double.MinValue, min = double.MaxValue;
        for (int d = 1; d <= 365; d++)
        {
          double t = ground.GetTemperature(d, depth);
          if (t > max) max = t;
          if (t < min) min = t;
        }
        return (max - min) / 2.0;
      }

      Assert.True(GetAmplitude(1.0) > GetAmplitude(3.0));
      Assert.True(GetAmplitude(3.0) > GetAmplitude(5.0));
    }

    /// <summary>地表面 (depth=0) の年平均は外気年平均と等しい</summary>
    [Fact]
    public void GetTemperature_SurfaceDepth_AnnualMeanEqualsAirMean()
    {
      var ground = new Ground(PeakDay, WarmMax, ColdMin, MeanTemp);
      double sum = 0;
      for (int d = 1; d <= 365; d++)
        sum += ground.GetTemperature(d, 0);
      Assert.Equal(MeanTemp, sum / 365.0, precision: 1);
    }

    /// <summary>インスタンスメソッドと静的メソッドの結果が一致する</summary>
    [Theory]
    [InlineData(1, 1.0)]
    [InlineData(100, 2.0)]
    [InlineData(208, 0.5)]
    public void GetTemperature_InstanceAndStaticMatch(int dayOfYear, double depth)
    {
      var ground = new Ground(PeakDay, WarmMax, ColdMin, MeanTemp);
      double fromInstance = ground.GetTemperature(dayOfYear, depth);
      double fromStatic = Ground.GetTemperature(
          PeakDay, WarmMax, ColdMin, MeanTemp, dayOfYear, depth);
      Assert.Equal(fromInstance, fromStatic, precision: 10);
    }

    #endregion
  }
}
