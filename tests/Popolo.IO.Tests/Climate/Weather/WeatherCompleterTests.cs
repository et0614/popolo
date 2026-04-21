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
  }
}
