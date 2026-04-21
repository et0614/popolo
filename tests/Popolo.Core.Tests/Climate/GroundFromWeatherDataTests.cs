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
  /// <summary>Tests for <see cref="Ground.FromWeatherData"/> (BESTEST procedure).</summary>
  public class GroundFromWeatherDataTests
  {
    /// <summary>
    /// Builds a synthetic WeatherData with hourly records. Each day's
    /// temperature follows daily_mean(doy) + diurnalAmplitude · cos((hour - 14)·2π/24),
    /// so the daily max occurs at 14:00 and daily min at 02:00.
    /// </summary>
    private static WeatherData BuildHourlySyntheticYear(
        Func<int /*doy*/, double> dailyMean,
        double diurnalAmplitude,
        int year = 2026)
    {
      var data = new WeatherData
      {
        Source = WeatherDataSource.Csv,
        NominalInterval = TimeSpan.FromHours(1),
      };
      var start = new DateTime(year, 1, 1);
      int hours = DateTime.IsLeapYear(year) ? 366 * 24 : 365 * 24;
      for (int i = 0; i < hours; i++)
      {
        DateTime t = start.AddHours(i);
        double basin = dailyMean(t.DayOfYear);
        double hourly = basin + diurnalAmplitude * Math.Cos((t.Hour - 14) * 2.0 * Math.PI / 24.0);
        data.Add(new WeatherRecordBuilder()
            .SetTime(t)
            .SetDryBulbTemperature(hourly)
            .ToRecord());
      }
      return data;
    }

    [Fact]
    public void RecoversBestestParametersFromPureSineDailyMean()
    {
      // 日平均 15 + 0.5·20·cos(...), 日較差 ±5 °C (日最高 = 日平均+5, 日最低 = 日平均−5)
      var data = BuildHourlySyntheticYear(
          doy => 15.0 + 0.5 * 20.0 * Math.Cos((doy - 208) * 2.0 * Math.PI / 365.0),
          diurnalAmplitude: 5.0);

      var g = Ground.FromWeatherData(data);

      // 年平均は 15 °C
      Assert.Equal(15.0, g.AnnualAverageTemperature, precision: 1);
      // 最暖月の日最高平均は (月中の日平均 + 5) のその月平均 ≈ 日平均ピーク + 5。
      // 日平均ピーク = 15 + 10 = 25 → 最暖月の日最高平均 ≈ 29 程度 (月中日で鈍る)
      Assert.InRange(g.WarmestMonthlyMeanDailyMax, 28.0, 30.0);
      // 同様に最冷月は (15 - 10) - 5 = 0 近傍
      Assert.InRange(g.ColdestMonthlyMeanDailyMin, -1.0, 1.0);
      // 年較差 ≈ 28-30 °C (BESTEST 方式なので日平均ベースより 10 °C 広い)
      Assert.InRange(g.AnnualTemperatureRange, 28.0, 30.0);
      // peak day は日平均が最大になる day = 208 (正弦のピーク日)
      Assert.Equal(208, g.PeakDayOfYear);
    }

    [Fact]
    public void RecoversExactMonthlyDailyExtremes_WhenDailyMaxMinAreConstantPerMonth()
    {
      // 各月について、日中 (10-16 時) に maxPerMonth[m]、夜間 (22-04 時) に
      // minPerMonth[m] を与え、他の時間帯は中間値を取るようなプロファイル。
      // → 各日の daily max / daily min が厳密に (max[m], min[m]) に一致。
      double[] maxPerMonth = { 10, 11, 15, 20, 25, 28, 30, 32, 28, 22, 16, 12 };
      double[] minPerMonth = {  0,  1,  3,  8, 14, 18, 22, 24, 20, 12,  5,  1 };

      var data = new WeatherData
      {
        Source = WeatherDataSource.Csv,
        NominalInterval = TimeSpan.FromHours(1),
      };
      var start = new DateTime(2026, 1, 1);
      for (int i = 0; i < 365 * 24; i++)
      {
        DateTime t = start.AddHours(i);
        int m = t.Month;
        double mid = 0.5 * (maxPerMonth[m - 1] + minPerMonth[m - 1]);
        double amp = 0.5 * (maxPerMonth[m - 1] - minPerMonth[m - 1]);
        double hourly = mid + amp * Math.Cos((t.Hour - 14) * 2.0 * Math.PI / 24.0);
        data.Add(new WeatherRecordBuilder()
            .SetTime(t)
            .SetDryBulbTemperature(hourly)
            .ToRecord());
      }

      var g = Ground.FromWeatherData(data);

      // 最暖月は 8 月 (32), 最冷月は 1 月 (0)
      Assert.Equal(32.0, g.WarmestMonthlyMeanDailyMax, precision: 6);
      Assert.Equal(0.0, g.ColdestMonthlyMeanDailyMin, precision: 6);
      // 年平均は中点 mid の全時間平均 = (maxPerMonth + minPerMonth)/2 の時間加重平均
      // (各月の日数が異なるため厳密には簡易平均と少し違うが、概ね中央)
      Assert.InRange(g.AnnualAverageTemperature, 13.0, 16.0);
      // peak day は 8 月 (max=32 が通年最大) の中のどこか。日平均は月内で一定 (mid) なので
      // 8 月内の最初の日 (DOY 213) が同率勝ちで採用される。
      Assert.InRange(g.PeakDayOfYear, 213, 243);
    }

    [Fact]
    public void UsesOnlyFirstYearOfMultiYearData()
    {
      // 1 年目: 日平均 15 °C 基調, 2 年目: 日平均 25 °C 基調
      var first = BuildHourlySyntheticYear(
          doy => 15.0 + 0.5 * 20.0 * Math.Cos((doy - 208) * 2.0 * Math.PI / 365.0),
          diurnalAmplitude: 5.0,
          year: 2026);
      var second = BuildHourlySyntheticYear(
          doy => 25.0 + 0.5 * 10.0 * Math.Cos((doy - 100) * 2.0 * Math.PI / 365.0),
          diurnalAmplitude: 3.0,
          year: 2027);
      foreach (var r in second.Records) first.Add(r);

      var g = Ground.FromWeatherData(first);

      // 1 年目のパラメータが返ること
      Assert.Equal(15.0, g.AnnualAverageTemperature, precision: 1);
      Assert.Equal(208, g.PeakDayOfYear);
    }

    [Fact]
    public void ThrowsWhenDataSpansLessThanOneYear()
    {
      var data = new WeatherData { NominalInterval = TimeSpan.FromDays(1) };
      var start = new DateTime(2026, 1, 1);
      for (int i = 0; i < 180; i++)
      {
        data.Add(new WeatherRecordBuilder()
            .SetTime(start.AddDays(i))
            .SetDryBulbTemperature(15.0)
            .ToRecord());
      }

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
    public void ToleratesHourlyNoise()
    {
      // 季節サイクル + 日変動 + 毎時ランダムノイズ (±2 °C) を与えても、
      // 日最高 / 日最低 → 月平均の二段集計でノイズは大きく抑えられる。
      var rng = new Random(42);
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
        double diurnal = 5.0 * Math.Cos((t.Hour - 14) * 2.0 * Math.PI / 24.0);
        double noise = (rng.NextDouble() - 0.5) * 4.0;
        data.Add(new WeatherRecordBuilder()
            .SetTime(t)
            .SetDryBulbTemperature(daily + diurnal + noise)
            .ToRecord());
      }

      var g = Ground.FromWeatherData(data);

      Assert.Equal(15.0, g.AnnualAverageTemperature, precision: 0);    // ≈ 1 °C 以内
      Assert.InRange(g.AnnualTemperatureRange, 25.0, 32.0);            // ノイズ込みでも頑健
      // ノイズで peak day が正規の 208 からずれる可能性があるが、±30 日以内に留まる
      Assert.InRange(g.PeakDayOfYear, 178, 238);
    }

    [Fact]
    public void WorksForMidYearStartDate()
    {
      var data = new WeatherData
      {
        Source = WeatherDataSource.Csv,
        NominalInterval = TimeSpan.FromHours(1),
      };
      var start = new DateTime(2025, 7, 15);
      for (int i = 0; i < 8760; i++)
      {
        DateTime t = start.AddHours(i);
        int doy = t.DayOfYear;
        double daily = 15.0 + 0.5 * 20.0 * Math.Cos((doy - 208) * 2.0 * Math.PI / 365.0);
        double diurnal = 5.0 * Math.Cos((t.Hour - 14) * 2.0 * Math.PI / 24.0);
        data.Add(new WeatherRecordBuilder()
            .SetTime(t)
            .SetDryBulbTemperature(daily + diurnal)
            .ToRecord());
      }

      var g = Ground.FromWeatherData(data);

      Assert.Equal(15.0, g.AnnualAverageTemperature, precision: 1);
      Assert.Equal(208, g.PeakDayOfYear);
    }
  }
}
