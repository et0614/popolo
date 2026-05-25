/* WeatherCompleterTests.cs
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
using Popolo.Core.Physics;
using Popolo.IO.Climate.Weather;

namespace Popolo.IO.Tests.Climate.Weather
{
  /// <summary>
  /// Unit tests for the four completion phases implemented by
  /// <see cref="WeatherCompleter"/>.
  /// </summary>
  public class WeatherCompleterTests
  {
    private static readonly WeatherStationInfo TokyoStation =
        new WeatherStationInfo("Tokyo", 35.6895, 139.6917, 40.0);

    private static WeatherData MakeData(params WeatherRecord[] records)
    {
      var data = new WeatherData(TokyoStation, WeatherDataSource.Csv);
      foreach (var r in records) data.Add(r);
      return data;
    }

    private static WeatherRecord Build(DateTime time, Action<WeatherRecordBuilder> configure)
    {
      var b = new WeatherRecordBuilder().SetTime(time);
      configure(b);
      return b.ToRecord();
    }

    // ================================================================
    #region EstimateAtmosphericPressureFromElevation

    [Fact]
    public void PressureFromElevation_FillsMissingPressureAndMarksEstimated()
    {
      var data = MakeData(Build(new DateTime(2026, 6, 21, 12, 0, 0),
          b => b.SetDryBulbTemperature(25.0)));

      WeatherCompleter.Apply(data, new WeatherReadOptions
      {
        EstimateAtmosphericPressureFromElevation = true,
      });

      var r = data.Records[0];
      Assert.True(r.Has(WeatherField.AtmosphericPressure));
      Assert.True(r.IsEstimated(WeatherField.AtmosphericPressure));
      Assert.Equal(MoistAir.GetAtmosphericPressure(40.0), r.AtmosphericPressure, precision: 6);
      // 既存の recorded フィールドは recorded のまま
      Assert.False(r.IsEstimated(WeatherField.DryBulbTemperature));
    }

    [Fact]
    public void PressureFromElevation_DoesNotOverwriteExistingPressure()
    {
      var data = MakeData(Build(new DateTime(2026, 6, 21, 12, 0, 0),
          b => b.SetAtmosphericPressure(95.0)));

      WeatherCompleter.Apply(data, new WeatherReadOptions
      {
        EstimateAtmosphericPressureFromElevation = true,
      });

      var r = data.Records[0];
      Assert.Equal(95.0, r.AtmosphericPressure);
      Assert.False(r.IsEstimated(WeatherField.AtmosphericPressure));
    }

    #endregion

    // ================================================================
    #region 湿度の双方向補完 (HumidityRatio ↔ RelativeHumidity)
    // この補完フェーズは無条件で動作する (WeatherReadOptions のフラグなし)。
    // 物理計算層は HumidityRatio しか読まないので RH のみのデータを救済する必要があり、
    // 逆方向 (w→RH) も対称性のために同時に埋める。

    [Fact]
    public void Humidity_FillsHumidityRatioFromRelativeHumidity()
    {
      var data = MakeData(Build(new DateTime(2026, 6, 21, 12, 0, 0),
          b => b.SetDryBulbTemperature(25.0)
                .SetRelativeHumidity(60.0)
                .SetAtmosphericPressure(101.325)));

      WeatherCompleter.Apply(data, new WeatherReadOptions());

      var r = data.Records[0];
      Assert.True(r.Has(WeatherField.HumidityRatio));
      Assert.True(r.IsEstimated(WeatherField.HumidityRatio));

      // 期待値: 25°C, 60% RH, 101.325 kPa → 約 11.9 g/kg
      double expectedWKgKg = MoistAir
          .GetHumidityRatioFromDryBulbTemperatureAndRelativeHumidity(25.0, 60.0, 101.325);
      Assert.Equal(expectedWKgKg * 1000.0, r.HumidityRatio, precision: 6);

      // RH 側は Recorded のまま
      Assert.False(r.IsEstimated(WeatherField.RelativeHumidity));
    }

    [Fact]
    public void Humidity_FillsRelativeHumidityFromHumidityRatio()
    {
      // 25°C, 101.325 kPa, w = 11.9 g/kg ≈ RH 60%
      double wKgKg = MoistAir
          .GetHumidityRatioFromDryBulbTemperatureAndRelativeHumidity(25.0, 60.0, 101.325);
      var data = MakeData(Build(new DateTime(2026, 6, 21, 12, 0, 0),
          b => b.SetDryBulbTemperature(25.0)
                .SetHumidityRatio(wKgKg * 1000.0)
                .SetAtmosphericPressure(101.325)));

      WeatherCompleter.Apply(data, new WeatherReadOptions());

      var r = data.Records[0];
      Assert.True(r.Has(WeatherField.RelativeHumidity));
      Assert.True(r.IsEstimated(WeatherField.RelativeHumidity));
      Assert.Equal(60.0, r.RelativeHumidity, precision: 6);
      Assert.False(r.IsEstimated(WeatherField.HumidityRatio));
    }

    [Fact]
    public void Humidity_UsesStandardPressureWhenMissing()
    {
      // 圧力なしでも 101.325 kPa フォールバックで動作することを確認
      var data = MakeData(Build(new DateTime(2026, 6, 21, 12, 0, 0),
          b => b.SetDryBulbTemperature(25.0).SetRelativeHumidity(60.0)));

      WeatherCompleter.Apply(data, new WeatherReadOptions());

      var r = data.Records[0];
      Assert.True(r.Has(WeatherField.HumidityRatio));
      double expectedWKgKg = MoistAir
          .GetHumidityRatioFromDryBulbTemperatureAndRelativeHumidity(
              25.0, 60.0, PhysicsConstants.StandardAtmosphericPressure);
      Assert.Equal(expectedWKgKg * 1000.0, r.HumidityRatio, precision: 6);
    }

    [Fact]
    public void Humidity_SkipsWhenBothPresent()
    {
      // w と RH が両方与えられているレコードは触らない (恣意的に不整合な値でも維持)
      var data = MakeData(Build(new DateTime(2026, 6, 21, 12, 0, 0),
          b => b.SetDryBulbTemperature(25.0)
                .SetHumidityRatio(10.0)
                .SetRelativeHumidity(40.0)));

      WeatherCompleter.Apply(data, new WeatherReadOptions());

      var r = data.Records[0];
      Assert.Equal(10.0, r.HumidityRatio);
      Assert.Equal(40.0, r.RelativeHumidity);
      Assert.False(r.IsEstimated(WeatherField.HumidityRatio));
      Assert.False(r.IsEstimated(WeatherField.RelativeHumidity));
    }

    [Fact]
    public void Humidity_SkipsWhenDryBulbAbsent()
    {
      var data = MakeData(Build(new DateTime(2026, 6, 21, 12, 0, 0),
          b => b.SetRelativeHumidity(60.0)));

      WeatherCompleter.Apply(data, new WeatherReadOptions());

      var r = data.Records[0];
      Assert.False(r.Has(WeatherField.HumidityRatio));
    }

    [Fact]
    public void Humidity_UsesElevationPressureWhenAvailableAfterFlagEnabled()
    {
      // 圧力が elevation 推定されてから湿度補完が走るので、海抜 40 m での圧力が
      // RH→w 変換に使われることを確認する。フェーズ順 (圧力 → 湿度) の検証。
      var data = MakeData(Build(new DateTime(2026, 6, 21, 12, 0, 0),
          b => b.SetDryBulbTemperature(25.0).SetRelativeHumidity(60.0)));

      WeatherCompleter.Apply(data, new WeatherReadOptions
      {
        EstimateAtmosphericPressureFromElevation = true,
      });

      var r = data.Records[0];
      double pAt40m = MoistAir.GetAtmosphericPressure(40.0);
      double expectedWKgKg = MoistAir
          .GetHumidityRatioFromDryBulbTemperatureAndRelativeHumidity(25.0, 60.0, pAt40m);
      Assert.Equal(expectedWKgKg * 1000.0, r.HumidityRatio, precision: 6);
    }

    #endregion

    // ================================================================
    #region CompleteRadiationComponentsByGeometry

    [Fact]
    public void Geometry_FillsDiffuseFromGhiAndDni()
    {
      // Tokyo 夏至 12:00 の辺り (太陽高度が十分高い)
      var t = new DateTime(2026, 6, 21, 12, 0, 0);
      double sinH = Math.Sin(Sun.GetSunAltitude(
          TokyoStation.Latitude, TokyoStation.Longitude, 135.0, t));
      double dni = 800.0, dhi = 100.0;
      double ghi = dni * sinH + dhi;  // 真値

      var data = MakeData(Build(t, b => b
          .SetGlobalHorizontalRadiation(ghi)
          .SetDirectNormalRadiation(dni)));

      WeatherCompleter.Apply(data, new WeatherReadOptions
      {
        CompleteRadiationComponentsByGeometry = true,
      });

      var r = data.Records[0];
      Assert.True(r.Has(WeatherField.DiffuseHorizontalRadiation));
      Assert.True(r.IsEstimated(WeatherField.DiffuseHorizontalRadiation));
      Assert.Equal(dhi, r.DiffuseHorizontalRadiation, precision: 3);
      // GHI と DNI は recorded のまま
      Assert.False(r.IsEstimated(WeatherField.GlobalHorizontalRadiation));
      Assert.False(r.IsEstimated(WeatherField.DirectNormalRadiation));
    }

    [Fact]
    public void Geometry_FillsDniFromGhiAndDhi()
    {
      var t = new DateTime(2026, 6, 21, 12, 0, 0);
      double sinH = Math.Sin(Sun.GetSunAltitude(
          TokyoStation.Latitude, TokyoStation.Longitude, 135.0, t));
      double dni = 700.0, dhi = 120.0;
      double ghi = dni * sinH + dhi;

      var data = MakeData(Build(t, b => b
          .SetGlobalHorizontalRadiation(ghi)
          .SetDiffuseHorizontalRadiation(dhi)));

      WeatherCompleter.Apply(data, new WeatherReadOptions
      {
        CompleteRadiationComponentsByGeometry = true,
      });

      var r = data.Records[0];
      Assert.True(r.IsEstimated(WeatherField.DirectNormalRadiation));
      Assert.Equal(dni, r.DirectNormalRadiation, precision: 3);
    }

    [Fact]
    public void Geometry_FillsGhiFromDniAndDhi()
    {
      var t = new DateTime(2026, 6, 21, 12, 0, 0);
      double sinH = Math.Sin(Sun.GetSunAltitude(
          TokyoStation.Latitude, TokyoStation.Longitude, 135.0, t));
      double dni = 700.0, dhi = 120.0;

      var data = MakeData(Build(t, b => b
          .SetDirectNormalRadiation(dni)
          .SetDiffuseHorizontalRadiation(dhi)));

      WeatherCompleter.Apply(data, new WeatherReadOptions
      {
        CompleteRadiationComponentsByGeometry = true,
      });

      var r = data.Records[0];
      Assert.True(r.IsEstimated(WeatherField.GlobalHorizontalRadiation));
      Assert.Equal(dni * sinH + dhi, r.GlobalHorizontalRadiation, precision: 3);
    }

    [Fact]
    public void Geometry_AllThreePresent_Untouched()
    {
      var data = MakeData(Build(new DateTime(2026, 6, 21, 12, 0, 0), b => b
          .SetGlobalHorizontalRadiation(900)
          .SetDirectNormalRadiation(800)
          .SetDiffuseHorizontalRadiation(100)));

      WeatherCompleter.Apply(data, new WeatherReadOptions
      {
        CompleteRadiationComponentsByGeometry = true,
      });

      var r = data.Records[0];
      Assert.Equal(WeatherField.None, r.EstimatedFields);
    }

    [Fact]
    public void Geometry_OnlyGhi_Untouched()
    {
      // 1 成分のみ → geometry では補完できない (Split の領域)
      var data = MakeData(Build(new DateTime(2026, 6, 21, 12, 0, 0),
          b => b.SetGlobalHorizontalRadiation(500)));

      WeatherCompleter.Apply(data, new WeatherReadOptions
      {
        CompleteRadiationComponentsByGeometry = true,
      });

      var r = data.Records[0];
      Assert.False(r.Has(WeatherField.DirectNormalRadiation));
      Assert.False(r.Has(WeatherField.DiffuseHorizontalRadiation));
    }

    [Fact]
    public void Geometry_LowSun_SkipsDniCompletion()
    {
      // 夜明け前: 太陽高度が低すぎて DNI 復元は不適切
      var t = new DateTime(2026, 6, 21, 2, 0, 0);   // JST 夜間
      var data = MakeData(Build(t, b => b
          .SetGlobalHorizontalRadiation(0)
          .SetDiffuseHorizontalRadiation(0)));

      WeatherCompleter.Apply(data, new WeatherReadOptions
      {
        CompleteRadiationComponentsByGeometry = true,
      });

      var r = data.Records[0];
      Assert.False(r.Has(WeatherField.DirectNormalRadiation));
    }

    #endregion

    // ================================================================
    #region SplitGlobalRadiationIntoDirectAndDiffuse

    [Fact]
    public void Split_ProducesDniAndDhiFromGhiOnly()
    {
      var t = new DateTime(2026, 6, 21, 12, 0, 0);
      var data = MakeData(Build(t, b => b.SetGlobalHorizontalRadiation(700)));

      WeatherCompleter.Apply(data, new WeatherReadOptions
      {
        SplitGlobalRadiationIntoDirectAndDiffuse = true,
      });

      var r = data.Records[0];
      Assert.True(r.IsEstimated(WeatherField.DirectNormalRadiation));
      Assert.True(r.IsEstimated(WeatherField.DiffuseHorizontalRadiation));

      // 太陽高度が高いので直達 ≥ 散乱 と予想 (Erbs のほぼ常識的結果)
      Assert.True(r.DirectNormalRadiation > 0);
      Assert.True(r.DiffuseHorizontalRadiation > 0);
    }

    [Fact]
    public void Split_SkipsRecordsWithAllThreeComponents()
    {
      var data = MakeData(Build(new DateTime(2026, 6, 21, 12, 0, 0), b => b
          .SetGlobalHorizontalRadiation(900)
          .SetDirectNormalRadiation(800)
          .SetDiffuseHorizontalRadiation(100)));

      WeatherCompleter.Apply(data, new WeatherReadOptions
      {
        SplitGlobalRadiationIntoDirectAndDiffuse = true,
      });

      Assert.Equal(WeatherField.None, data.Records[0].EstimatedFields);
    }

    [Fact]
    public void GeometryThenSplit_GeometryWinsWhenTwoComponentsAvailable()
    {
      // GHI と DNI を与えて両フラグを有効にすると、Geometry が先に DHI を埋める
      // ため、Split (Erbs) は何もせず終わる。
      var t = new DateTime(2026, 6, 21, 12, 0, 0);
      double sinH = Math.Sin(Sun.GetSunAltitude(
          TokyoStation.Latitude, TokyoStation.Longitude, 135.0, t));
      double dni = 800.0, dhi = 100.0;
      double ghi = dni * sinH + dhi;

      var data = MakeData(Build(t, b => b
          .SetGlobalHorizontalRadiation(ghi)
          .SetDirectNormalRadiation(dni)));

      WeatherCompleter.Apply(data, new WeatherReadOptions
      {
        CompleteRadiationComponentsByGeometry = true,
        SplitGlobalRadiationIntoDirectAndDiffuse = true,
      });

      var r = data.Records[0];
      // Geometry で埋められた DHI が恒等式通りで、Erbs 統計値でないこと
      Assert.Equal(dhi, r.DiffuseHorizontalRadiation, precision: 3);
    }

    #endregion

    // ================================================================
    #region EstimateAtmosphericRadiation

    [Fact]
    public void AtmosphericRadiation_DerivesFromTwPCloud()
    {
      var data = MakeData(Build(new DateTime(2026, 6, 21, 0, 0, 0), b => b
          .SetDryBulbTemperature(20.0)
          .SetHumidityRatio(10.0)       // g/kg(DA)
          .SetAtmosphericPressure(101.0)
          .SetCloudCover(0.5)));

      WeatherCompleter.Apply(data, new WeatherReadOptions
      {
        EstimateAtmosphericRadiation = true,
      });

      var r = data.Records[0];
      Assert.True(r.IsEstimated(WeatherField.AtmosphericRadiation));
      Assert.True(r.AtmosphericRadiation > 200);   // 20°C 近傍では 300 W/m² 前後が妥当
      Assert.True(r.AtmosphericRadiation < 500);
    }

    [Fact]
    public void AtmosphericRadiation_SkipsRecordsWithoutRequiredFields()
    {
      var data = MakeData(Build(new DateTime(2026, 6, 21, 0, 0, 0), b => b
          .SetDryBulbTemperature(20.0)
          // humidity missing
          ));

      WeatherCompleter.Apply(data, new WeatherReadOptions
      {
        EstimateAtmosphericRadiation = true,
      });

      Assert.False(data.Records[0].Has(WeatherField.AtmosphericRadiation));
    }

    [Fact]
    public void AtmosphericRadiation_FallsBackToElevationPressure()
    {
      // 気圧が無くても Station.Elevation から推定した気圧で蒸気分圧を計算し続行
      var data = MakeData(Build(new DateTime(2026, 6, 21, 0, 0, 0), b => b
          .SetDryBulbTemperature(20.0)
          .SetHumidityRatio(10.0)));

      WeatherCompleter.Apply(data, new WeatherReadOptions
      {
        EstimateAtmosphericRadiation = true,
      });

      Assert.True(data.Records[0].IsEstimated(WeatherField.AtmosphericRadiation));
    }

    #endregion

    // ================================================================
    #region 駅情報未設定時 / フォールバック

    [Fact]
    public void NoStationLocation_SkipsSolarCompletions()
    {
      var data = new WeatherData();     // Station 未設定
      data.Add(Build(new DateTime(2026, 6, 21, 12, 0, 0),
          b => b.SetGlobalHorizontalRadiation(700)));

      WeatherCompleter.Apply(data, new WeatherReadOptions
      {
        CompleteRadiationComponentsByGeometry = true,
        SplitGlobalRadiationIntoDirectAndDiffuse = true,
      });

      var r = data.Records[0];
      Assert.False(r.Has(WeatherField.DirectNormalRadiation));
      Assert.False(r.Has(WeatherField.DiffuseHorizontalRadiation));
    }

    [Fact]
    public void FallbackStation_EnablesSolarCompletionsWhenFileHasNoStation()
    {
      // TMY1 / HASP 相当: station 情報がないデータセット
      var data = new WeatherData();
      data.Add(Build(new DateTime(2026, 6, 21, 12, 0, 0),
          b => b.SetGlobalHorizontalRadiation(700)));

      WeatherCompleter.Apply(data, new WeatherReadOptions
      {
        Station = TokyoStation,
        SplitGlobalRadiationIntoDirectAndDiffuse = true,
      });

      var r = data.Records[0];
      Assert.True(r.IsEstimated(WeatherField.DirectNormalRadiation));
      Assert.True(r.IsEstimated(WeatherField.DiffuseHorizontalRadiation));
      // 補完に先立って Station が設定されていること
      Assert.Equal("Tokyo", data.Station.Name);
    }

    [Fact]
    public void FallbackStation_DoesNotOverrideFileProvidedStation()
    {
      // ファイル由来の Station が既にあれば options.Station は無視される
      var data = MakeData(Build(new DateTime(2026, 6, 21, 12, 0, 0),
          b => b.SetGlobalHorizontalRadiation(700)));

      var other = new WeatherStationInfo("Osaka", 34.7, 135.5, 10);
      WeatherCompleter.Apply(data, new WeatherReadOptions
      {
        Station = other,
        SplitGlobalRadiationIntoDirectAndDiffuse = true,
      });

      Assert.Equal("Tokyo", data.Station.Name);   // 元の Tokyo のまま
    }

    #endregion

    // ================================================================
    #region デフォルト (no-op)

    [Fact]
    public void DefaultOptions_PerformsNoDerivation()
    {
      var data = MakeData(Build(new DateTime(2026, 6, 21, 12, 0, 0),
          b => b.SetGlobalHorizontalRadiation(700)));

      WeatherCompleter.Apply(data, WeatherReadOptions.Default);

      var r = data.Records[0];
      Assert.Equal(WeatherField.None, r.EstimatedFields);
      Assert.False(r.Has(WeatherField.DirectNormalRadiation));
    }

    #endregion

    // ================================================================
    #region 区間積分された sin(altitude) の使用

    /// <summary>
    /// 規約を既定 (StartOfInterval) のままにして、13:00 の record が
    /// [13:00, 14:00) を表す構成を組むと、区間平均 sin(h) は概ね 13:30
    /// 時点のそれに近い。ただし sinH は時間について厳密な 1 次関数ではない
    /// ので、完了器の 6 点サンプリング平均と「中点 1 点」の値は数 W/m²
    /// 程度ずれる。ここでは ±3 W/m² の帯で吸収する。
    /// </summary>
    [Fact]
    public void DefaultConvention_IsStartOfInterval_ForBuiltInReaders()
    {
      // DNI, DHI を既知で与え、区間 [13:00, 14:00) の中点 13:30 で
      // sinH を評価した恒等式から GHI を組む。完了器は StartOfInterval 既定
      // で区間 [13:00, 14:00) を見るので、復元される DHI は元の値に近い。
      var t = new DateTime(2026, 6, 21, 13, 0, 0);
      double sinAtMid = Math.Sin(Sun.GetSunAltitude(
          TokyoStation.Latitude, TokyoStation.Longitude, 135.0,
          t.AddMinutes(30)));
      double dni = 800.0, dhiTrue = 100.0;
      double ghi = dni * sinAtMid + dhiTrue;

      var data = new WeatherData(TokyoStation, WeatherDataSource.Csv)
      {
        NominalInterval = TimeSpan.FromHours(1),
      };
      data.Add(Build(t, b => b
          .SetGlobalHorizontalRadiation(ghi)
          .SetDirectNormalRadiation(dni)));

      // TimestampConvention を指定しない (既定値を検証する)
      WeatherCompleter.Apply(data, new WeatherReadOptions
      {
        CompleteRadiationComponentsByGeometry = true,
      });

      var r = data.Records[0];
      Assert.True(r.IsEstimated(WeatherField.DiffuseHorizontalRadiation));
      // sinH は時間について凸/凹なので 6 点離散平均 ≠ sinH(30 min) に完全一致
      // はしない。Tokyo 夏至 13:30 近傍では 1-2 W/m² 程度の残差が出る。
      Assert.InRange(r.DiffuseHorizontalRadiation, dhiTrue - 3.0, dhiTrue + 3.0);
    }

    /// <summary>
    /// EndOfInterval 規約と 1 時間の NominalInterval を設定すると、r.Time の
    /// 瞬時値でなく区間中点相当の太陽位置に基づいて DHI が補完される。
    /// Tokyo の夏至 13:00 (太陽時の正午は 11:40 JST 付近) では、
    /// 区間 (12:00, 13:00] の中点 12:30 は r.Time 13:00 より太陽時の正午に
    /// 近く、sinH(12:30) &gt; sinH(13:00)。従って
    /// 区間平均 sinHeff &gt; sinH(r.Time) となり、
    /// DHI_recovered = GHI − DNI·sinHeff &lt; dhiTrue に回る。
    /// </summary>
    [Fact]
    public void EndOfInterval_UsesIntervalAveragedSinH_NotInstantAtRecordTime()
    {
      var t = new DateTime(2026, 6, 21, 13, 0, 0);
      double sinAtRecordTime = Math.Sin(Sun.GetSunAltitude(
          TokyoStation.Latitude, TokyoStation.Longitude, 135.0, t));
      double dni = 800.0, dhiTrue = 100.0;
      double ghi = dni * sinAtRecordTime + dhiTrue;  // 13:00 瞬時の恒等式

      var data = new WeatherData(TokyoStation, WeatherDataSource.Csv)
      {
        NominalInterval = TimeSpan.FromHours(1),
      };
      data.Add(Build(t, b => b
          .SetGlobalHorizontalRadiation(ghi)
          .SetDirectNormalRadiation(dni)));

      WeatherCompleter.Apply(data, new WeatherReadOptions
      {
        CompleteRadiationComponentsByGeometry = true,
        TimestampConvention = TimestampConvention.EndOfInterval,
      });

      var r = data.Records[0];
      Assert.True(r.IsEstimated(WeatherField.DiffuseHorizontalRadiation));
      // 区間 (12:00, 13:00] 中点の sinH は 13:00 瞬時値より大きいので、
      // 恒等式 GHI − DNI·sinHeff で復元した DHI は dhiTrue を下回る。
      Assert.True(r.DiffuseHorizontalRadiation < dhiTrue,
          $"expected DHI < {dhiTrue}, got {r.DiffuseHorizontalRadiation}");
    }

    /// <summary>
    /// Instant 規約では区間積分を行わず、r.Time の瞬時 sin(h) をそのまま使う。
    /// </summary>
    [Fact]
    public void InstantConvention_DoesNotAverageAcrossInterval()
    {
      var t = new DateTime(2026, 6, 21, 13, 0, 0);
      double sinH = Math.Sin(Sun.GetSunAltitude(
          TokyoStation.Latitude, TokyoStation.Longitude, 135.0, t));
      double dni = 800.0, dhiTrue = 100.0;
      double ghi = dni * sinH + dhiTrue;

      var data = new WeatherData(TokyoStation, WeatherDataSource.Csv)
      {
        NominalInterval = TimeSpan.FromHours(1),   // あっても Instant なら無視
      };
      data.Add(Build(t, b => b
          .SetGlobalHorizontalRadiation(ghi)
          .SetDirectNormalRadiation(dni)));

      WeatherCompleter.Apply(data, new WeatherReadOptions
      {
        CompleteRadiationComponentsByGeometry = true,
        TimestampConvention = TimestampConvention.Instant,
      });

      Assert.Equal(dhiTrue, data.Records[0].DiffuseHorizontalRadiation, precision: 3);
    }

    /// <summary>
    /// NominalInterval が未設定なら、TimestampConvention にかかわらず
    /// 瞬時評価にフォールバックする。
    /// </summary>
    [Fact]
    public void NoNominalInterval_FallsBackToInstantEvaluation()
    {
      var t = new DateTime(2026, 6, 21, 13, 0, 0);
      double sinH = Math.Sin(Sun.GetSunAltitude(
          TokyoStation.Latitude, TokyoStation.Longitude, 135.0, t));
      double dni = 800.0, dhiTrue = 100.0;
      double ghi = dni * sinH + dhiTrue;

      // NominalInterval を設定しない
      var data = new WeatherData(TokyoStation, WeatherDataSource.Csv);
      data.Add(Build(t, b => b
          .SetGlobalHorizontalRadiation(ghi)
          .SetDirectNormalRadiation(dni)));

      WeatherCompleter.Apply(data, new WeatherReadOptions
      {
        CompleteRadiationComponentsByGeometry = true,
        TimestampConvention = TimestampConvention.EndOfInterval,
      });

      Assert.Equal(dhiTrue, data.Records[0].DiffuseHorizontalRadiation, precision: 3);
    }

    /// <summary>
    /// 夜間全体の区間 (r.Time が夜明け前で、前 1 時間も全て夜) は
    /// sinHeff = 0 となり、GHI 補完は DNI 項が消えて DHI そのもの、
    /// DHI 補完は GHI そのものになる (物理的に正しい: 夜間は DNI=0)。
    /// </summary>
    [Fact]
    public void EndOfInterval_NightInterval_ZeroSinH_StillCompletesSafely()
    {
      var t = new DateTime(2026, 6, 21, 2, 0, 0);    // JST 2 時、夜
      var data = new WeatherData(TokyoStation, WeatherDataSource.Csv)
      {
        NominalInterval = TimeSpan.FromHours(1),
      };
      // GHI=0, DNI=0 の夜間データ → DHI=0 が期待値
      data.Add(Build(t, b => b
          .SetGlobalHorizontalRadiation(0)
          .SetDirectNormalRadiation(0)));

      WeatherCompleter.Apply(data, new WeatherReadOptions
      {
        CompleteRadiationComponentsByGeometry = true,
        TimestampConvention = TimestampConvention.EndOfInterval,
      });

      var r = data.Records[0];
      Assert.True(r.IsEstimated(WeatherField.DiffuseHorizontalRadiation));
      Assert.Equal(0.0, r.DiffuseHorizontalRadiation);
    }

    /// <summary>
    /// 夜間区間の Erbs split は両成分 0 を返し、estimated にマークする。
    /// </summary>
    [Fact]
    public void EndOfInterval_NightInterval_Erbs_ReturnsZeroForBothComponents()
    {
      var t = new DateTime(2026, 6, 21, 2, 0, 0);
      var data = new WeatherData(TokyoStation, WeatherDataSource.Csv)
      {
        NominalInterval = TimeSpan.FromHours(1),
      };
      data.Add(Build(t, b => b.SetGlobalHorizontalRadiation(0)));

      WeatherCompleter.Apply(data, new WeatherReadOptions
      {
        SplitGlobalRadiationIntoDirectAndDiffuse = true,
        TimestampConvention = TimestampConvention.EndOfInterval,
      });

      var r = data.Records[0];
      Assert.True(r.IsEstimated(WeatherField.DirectNormalRadiation));
      Assert.True(r.IsEstimated(WeatherField.DiffuseHorizontalRadiation));
      Assert.Equal(0.0, r.DirectNormalRadiation);
      Assert.Equal(0.0, r.DiffuseHorizontalRadiation);
    }

    /// <summary>
    /// 日の出を跨ぐ区間 (前 30 分は夜、後 30 分は日出後) では、区間平均の
    /// sinHeff は小さいが 0 ではない。この場合 Erbs は非ゼロの DNI/DHI を
    /// 返し、DNI = 0 への強制 fallback (sinHeff &lt; MinEffectiveSinH) は
    /// ほぼ確実に発動しない程度の値を出す。
    /// </summary>
    [Fact]
    public void EndOfInterval_SunriseCrossingInterval_ProducesNonzeroErbsSplit()
    {
      // Tokyo 夏至の日の出は約 04:25 JST。区間 (04:00, 05:00] は後半が日出後。
      var t = new DateTime(2026, 6, 21, 5, 0, 0);
      var data = new WeatherData(TokyoStation, WeatherDataSource.Csv)
      {
        NominalInterval = TimeSpan.FromHours(1),
      };
      data.Add(Build(t, b => b.SetGlobalHorizontalRadiation(50)));

      WeatherCompleter.Apply(data, new WeatherReadOptions
      {
        SplitGlobalRadiationIntoDirectAndDiffuse = true,
        TimestampConvention = TimestampConvention.EndOfInterval,
      });

      var r = data.Records[0];
      Assert.True(r.DirectNormalRadiation >= 0);
      Assert.True(r.DiffuseHorizontalRadiation > 0);
      // エネルギー保存: DNI·sinHeff + DHI = GHI (Erbs はこの恒等式を満たす)
      // ただし sinHeff を外部で再現するのは難しいので DHI ≤ GHI の緩い上限のみ検証
      Assert.True(r.DiffuseHorizontalRadiation <= 50 + 1e-6);
    }

    #endregion
  }
}
