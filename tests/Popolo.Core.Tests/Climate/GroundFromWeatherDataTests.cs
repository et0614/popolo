/* GroundFromWeatherDataTests.cs
 *
 * Copyright (C) 2026 E.Togashi
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 3 of the License, or (at
 * your option) any later version.
 */

using System;
using Xunit;

using Popolo.Core.Climate;
using Popolo.Core.Climate.Weather;
using Popolo.Core.Exceptions;

namespace Popolo.Core.Tests.Climate
{
  /// <summary>Tests for <see cref="Ground.FromWeatherData"/>.</summary>
  public class GroundFromWeatherDataTests
  {
    /// <summary>
    /// Builds a synthetic WeatherData with daily records whose dry-bulb
    /// temperature follows T(n) = avg + (range/2)·cos((n - peak)·2π/365).
    /// </summary>
    private static WeatherData BuildSyntheticYear(
        double avg, double range, int peakDoy, int days, int year = 2026)
    {
      var data = new WeatherData
      {
        Source = WeatherDataSource.Csv,
        NominalInterval = TimeSpan.FromDays(1),
      };
      var start = new DateTime(year, 1, 1);
      for (int i = 0; i < days; i++)
      {
        DateTime t = start.AddDays(i);
        int doy = t.DayOfYear;
        double tdb = avg + 0.5 * range * Math.Cos((doy - peakDoy) * 2.0 * Math.PI / 365.0);
        data.Add(new WeatherRecordBuilder()
            .SetTime(t)
            .SetDryBulbTemperature(tdb)
            .ToRecord());
      }
      return data;
    }

    [Fact]
    public void RecoversMonthlyMeansFromPureSineYear()
    {
      // peak=208 は 7 月末、振幅 25 °C、年平均 15 °C の純正弦波。
      // 月平均は 7 月と 8 月がほぼ同値で最暖、1 月と 2 月がほぼ同値で最冷。
      var data = BuildSyntheticYear(avg: 15.0, range: 25.0, peakDoy: 208, days: 365);

      var g = Ground.FromWeatherData(data);

      // 年平均は月平均の平均で、正弦波の離散月平均からだと真値 15 °C をほぼそのまま回復。
      Assert.Equal(15.0, g.AnnualAverageTemperature, precision: 1);
      // 最暖月平均は真の瞬時 peak (27.5) より少し低い (月平均化でピークが鈍る)。
      // 一方、最冷月平均も真の anti-peak (2.5) より少し高い。月較差は ≈ 23 °C 程度。
      Assert.True(g.MaxMonthlyMeanTemperature > 25.0);     // ピーク付近の値を拾う
      Assert.True(g.MinMonthlyMeanTemperature < 5.0);      // 反ピーク付近
      Assert.InRange(g.AnnualMonthlyMeanRange, 22.0, 25.0);
      // peak=208 は 7 月 27 日。最暖月は 7 月 (peakDoy=196) と 8 月 (peakDoy=227) が拮抗。
      // いずれにせよその 2 つのどちらか。
      Assert.True(g.PeakDayOfYear == 196 || g.PeakDayOfYear == 227);
    }

    [Fact]
    public void RecoveredParametersReproduceInputMonthlyMeans()
    {
      // 各月に「その月の真の平均値」をそのまま毎日の温度として与える。
      // この場合、月平均は厳密にその値に戻り、FromWeatherData は完全な入力を回収する。
      double[] monthlyMeans =
      {
        // Jan  Feb  Mar  Apr  May  Jun  Jul  Aug  Sep  Oct  Nov  Dec
          5.0, 6.0, 9.0,14.0,19.0,23.0,26.0,28.0,24.0,18.0,12.0, 7.0
      };
      var data = new WeatherData
      {
        Source = WeatherDataSource.Csv,
        NominalInterval = TimeSpan.FromDays(1),
      };
      var start = new DateTime(2026, 1, 1);
      for (int i = 0; i < 365; i++)
      {
        DateTime t = start.AddDays(i);
        double tdb = monthlyMeans[t.Month - 1];
        data.Add(new WeatherRecordBuilder()
            .SetTime(t)
            .SetDryBulbTemperature(tdb)
            .ToRecord());
      }

      var g = Ground.FromWeatherData(data);

      Assert.Equal(28.0, g.MaxMonthlyMeanTemperature, precision: 6);   // Aug
      Assert.Equal(5.0, g.MinMonthlyMeanTemperature, precision: 6);    // Jan
      // 年平均は 12 ヶ月の単純平均 (日数重みなし)
      double expectedAvg = 0.0;
      foreach (var v in monthlyMeans) expectedAvg += v;
      expectedAvg /= 12.0;
      Assert.Equal(expectedAvg, g.AnnualAverageTemperature, precision: 6);
      // 最暖月 = 8 月 (DOY=227 on non-leap)
      Assert.Equal(227, g.PeakDayOfYear);
    }

    [Fact]
    public void UsesOnlyFirstYearOfMultiYearData()
    {
      // 1 年目: avg=15, range=20, peak=208
      // 2 年目: avg=25, range=5, peak=100 ← 混ざるなら結果が動く
      var first = BuildSyntheticYear(15.0, 20.0, 208, 365, year: 2026);
      var second = BuildSyntheticYear(25.0, 5.0, 100, 365, year: 2027);
      foreach (var r in second.Records) first.Add(r);

      var g = Ground.FromWeatherData(first);

      // 1 年目のパラメータが返ること (年平均は 12 ヶ月平均に近い)
      Assert.Equal(15.0, g.AnnualAverageTemperature, precision: 1);
      Assert.True(g.PeakDayOfYear == 196 || g.PeakDayOfYear == 227);
    }

    [Fact]
    public void ThrowsWhenDataSpansLessThanOneYear()
    {
      var data = BuildSyntheticYear(15.0, 20.0, 208, days: 180);

      Assert.Throws<PopoloArgumentException>(() => Ground.FromWeatherData(data));
    }

    [Fact]
    public void ThrowsWhenDataIsNull()
    {
      Assert.Throws<PopoloArgumentException>(() => Ground.FromWeatherData(null!));
    }

    [Fact]
    public void ThrowsWhenDataHasNoRecords()
    {
      var data = new WeatherData();
      Assert.Throws<PopoloArgumentException>(() => Ground.FromWeatherData(data));
    }

    [Fact]
    public void ThrowsWhenAnyMonthHasNoDryBulb()
    {
      // 1 年間のレコードはあるが、すべて DryBulbTemperature を持たない。
      // → 全ての月で monthCount が 0 → 最初の月 (1 月) で例外
      var data = new WeatherData { NominalInterval = TimeSpan.FromDays(1) };
      var start = new DateTime(2026, 1, 1);
      for (int i = 0; i < 365; i++)
      {
        data.Add(new WeatherRecordBuilder()
            .SetTime(start.AddDays(i))
            .SetHumidityRatio(5.0)            // 他のフィールドのみ
            .ToRecord());
      }

      Assert.Throws<PopoloArgumentException>(() => Ground.FromWeatherData(data));
    }

    [Fact]
    public void ToleratesNoisyDailySeries()
    {
      // 日次ノイズ (±3 °C) が月平均で均されて、月平均ベースのパラメータは安定。
      var rng = new Random(42);
      var data = new WeatherData
      {
        Source = WeatherDataSource.Csv,
        NominalInterval = TimeSpan.FromDays(1),
      };
      var start = new DateTime(2026, 1, 1);
      for (int i = 0; i < 365; i++)
      {
        DateTime t = start.AddDays(i);
        int doy = t.DayOfYear;
        double baseT = 15.0 + 0.5 * 20.0 * Math.Cos((doy - 208) * 2.0 * Math.PI / 365.0);
        double noisy = baseT + (rng.NextDouble() - 0.5) * 6.0;   // ±3 °C
        data.Add(new WeatherRecordBuilder()
            .SetTime(t)
            .SetDryBulbTemperature(noisy)
            .ToRecord());
      }

      var g = Ground.FromWeatherData(data);

      Assert.Equal(15.0, g.AnnualAverageTemperature, precision: 0);  // ≈ 1 °C 以内
      // 月平均の max/min は正弦波の月平均 ≈ ±9.6 °C 近辺 + 月 30 サンプル平均ノイズ
      Assert.InRange(g.AnnualMonthlyMeanRange, 17.0, 22.0);
    }

    [Fact]
    public void WorksForMidYearStartDate()
    {
      // 2025-07-15 から 365 日。月を跨ぐウィンドウでも 12 ヶ月分は揃う。
      var data = new WeatherData
      {
        Source = WeatherDataSource.Csv,
        NominalInterval = TimeSpan.FromDays(1),
      };
      var start = new DateTime(2025, 7, 15);
      for (int i = 0; i < 365; i++)
      {
        DateTime t = start.AddDays(i);
        int doy = t.DayOfYear;
        double tdb = 15.0 + 0.5 * 20.0 * Math.Cos((doy - 208) * 2.0 * Math.PI / 365.0);
        data.Add(new WeatherRecordBuilder()
            .SetTime(t)
            .SetDryBulbTemperature(tdb)
            .ToRecord());
      }

      var g = Ground.FromWeatherData(data);

      Assert.Equal(15.0, g.AnnualAverageTemperature, precision: 1);
      Assert.True(g.PeakDayOfYear == 196 || g.PeakDayOfYear == 227);
    }

    [Fact]
    public void AcceptsHourlyYearOfRecords()
    {
      // EPW 相当: 00:00 Jan 1 〜 23:00 Dec 31 の 8760 本。日変動を入れる。
      var data = new WeatherData
      {
        Source = WeatherDataSource.Csv,
        NominalInterval = TimeSpan.FromHours(1),
      };
      var start = new DateTime(2026, 1, 1);
      for (int i = 0; i < 8760; i++)
      {
        DateTime t = start.AddHours(i);
        int doy = t.DayOfYear;
        double daily = 15.0 + 0.5 * 20.0 * Math.Cos((doy - 208) * 2.0 * Math.PI / 365.0);
        double hourly = daily + 3.0 * Math.Cos((t.Hour - 14) * 2.0 * Math.PI / 24.0);
        data.Add(new WeatherRecordBuilder()
            .SetTime(t)
            .SetDryBulbTemperature(hourly)
            .ToRecord());
      }

      var g = Ground.FromWeatherData(data);

      // 日変動 (24 h 周期) は月平均化で完全に相殺され、季節成分のみ残る
      Assert.Equal(15.0, g.AnnualAverageTemperature, precision: 1);
      Assert.True(g.PeakDayOfYear == 196 || g.PeakDayOfYear == 227);
    }
  }
}
