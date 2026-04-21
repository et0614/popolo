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
    public void RecoversKusudaParametersFromPerfectSyntheticYear()
    {
      var data = BuildSyntheticYear(avg: 15.4, range: 25.0, peakDoy: 208, days: 365);

      var g = Ground.FromWeatherData(data);

      Assert.Equal(15.4, g.AnnualAverageTemperature, precision: 2);
      Assert.Equal(25.0, g.AnnualTemperatureRange, precision: 2);
      // ピーク日は DOY 離散化と 1 次フーリエ射影の ±1 日程度の丸めを許容
      Assert.InRange(g.PeakDayOfYear, 207, 209);
    }

    [Fact]
    public void UsesOnlyFirstYearOfMultiYearData()
    {
      // 1 年目: (avg=15, range=20, peak=208)
      // 2 年目: (avg=25, range=5, peak=100)   ← 2 年目が混ざるなら結果が動くはず
      var first = BuildSyntheticYear(15.0, 20.0, 208, 365, year: 2026);
      var second = BuildSyntheticYear(25.0, 5.0, 100, 365, year: 2027);
      foreach (var r in second.Records) first.Add(r);

      var g = Ground.FromWeatherData(first);

      // 1 年目のパラメータが返ること
      Assert.Equal(15.0, g.AnnualAverageTemperature, precision: 2);
      Assert.Equal(20.0, g.AnnualTemperatureRange, precision: 2);
      Assert.InRange(g.PeakDayOfYear, 207, 209);
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
    public void ThrowsWhenNoRecordsHaveDryBulb()
    {
      // 1 年間のレコードはあるが、すべて DryBulbTemperature を持たない
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
    public void ToleratesNoisySeriesAndRecoversMacroParameters()
    {
      // 正弦波 + 日次のランダムノイズ。第 1 高調波射影なので振幅・ピーク日は頑健。
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
        double noisy = baseT + (rng.NextDouble() - 0.5) * 4.0;   // ±2 °C
        data.Add(new WeatherRecordBuilder()
            .SetTime(t)
            .SetDryBulbTemperature(noisy)
            .ToRecord());
      }

      var g = Ground.FromWeatherData(data);

      Assert.Equal(15.0, g.AnnualAverageTemperature, precision: 1);   // 0.1 °C 以内
      Assert.InRange(g.AnnualTemperatureRange, 18.5, 21.5);           // ≈ 1.5 °C ロバスト
      Assert.InRange(g.PeakDayOfYear, 200, 216);                     // ≈ ±8 日
    }

    [Fact]
    public void WorksForMidYearStartDate()
    {
      // 2025-07-15 から 365 日。DOY は後半→翌年前半へ周回。
      // 真パラメータを回収できること。
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

      Assert.Equal(15.0, g.AnnualAverageTemperature, precision: 2);
      Assert.Equal(20.0, g.AnnualTemperatureRange, precision: 2);
      Assert.InRange(g.PeakDayOfYear, 207, 209);
    }

    [Fact]
    public void AcceptsHourlyYearOfRecords()
    {
      // EPW 相当: 00:00 Jan 1 〜 23:00 Dec 31 の 8760 本。スパンは 364 日 23 時間。
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
        // 日変動も少し入れる
        double hourly = daily + 3.0 * Math.Cos((t.Hour - 14) * 2.0 * Math.PI / 24.0);
        data.Add(new WeatherRecordBuilder()
            .SetTime(t)
            .SetDryBulbTemperature(hourly)
            .ToRecord());
      }

      var g = Ground.FromWeatherData(data);

      // 日変動成分 (24h 周期) は DOY 平均で相殺され、年周期のみ残る
      Assert.Equal(15.0, g.AnnualAverageTemperature, precision: 2);
      Assert.Equal(20.0, g.AnnualTemperatureRange, precision: 2);
      Assert.InRange(g.PeakDayOfYear, 207, 209);
    }
  }
}
