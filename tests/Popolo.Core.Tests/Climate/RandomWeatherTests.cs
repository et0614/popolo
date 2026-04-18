/* RandomWeatherGenerateTests.cs
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
using Popolo.Core.Climate.Weather;
using Popolo.Core.Exceptions;

namespace Popolo.Core.Tests.Climate
{
  /// <summary>RandomWeather.Generate (WeatherData出力API) のテスト</summary>
  public class RandomWeatherGenerateTests
  {
    /// <summary>1年分生成すると8760件のレコードが得られる</summary>
    [Fact]
    public void Generate_OneYear_Produces8760Records()
    {
      var rw = new RandomWeather(seed: 12345, location: RandomWeather.Location.Tokyo);
      var data = rw.Generate(new DateTime(2026, 1, 1), years: 1, isLeapYear: false);

      Assert.Equal(8760, data.Count);
    }

    /// <summary>うるう年指定で8784件になる</summary>
    [Fact]
    public void Generate_OneLeapYear_Produces8784Records()
    {
      var rw = new RandomWeather(seed: 12345, location: RandomWeather.Location.Tokyo);
      var data = rw.Generate(new DateTime(2024, 1, 1), years: 1, isLeapYear: true);

      Assert.Equal(8784, data.Count);
    }

    /// <summary>Sourceは Generated, NominalIntervalは 1時間</summary>
    [Fact]
    public void Generate_MetadataSet()
    {
      var rw = new RandomWeather(seed: 12345, location: RandomWeather.Location.Tokyo);
      var data = rw.Generate(new DateTime(2026, 1, 1), years: 1);

      Assert.Equal(WeatherDataSource.Generated, data.Source);
      Assert.Equal(TimeSpan.FromHours(1), data.NominalInterval);
    }

    /// <summary>生成されたレコードは温度・湿度・全天日射を持ち、それ以外は欠測</summary>
    [Fact]
    public void Generate_FieldsAvailability()
    {
      var rw = new RandomWeather(seed: 12345, location: RandomWeather.Location.Tokyo);
      var data = rw.Generate(new DateTime(2026, 1, 1), years: 1);

      var r = data.Records[0];
      Assert.True(r.Has(WeatherField.DryBulbTemperature));
      Assert.True(r.Has(WeatherField.HumidityRatio));
      Assert.True(r.Has(WeatherField.GlobalHorizontalRadiation));
      Assert.False(r.Has(WeatherField.AtmosphericPressure));
      Assert.False(r.Has(WeatherField.WindSpeed));
      Assert.False(r.Has(WeatherField.AtmosphericRadiation));
    }

    /// <summary>時刻の昇順と範囲が正しい</summary>
    [Fact]
    public void Generate_TimesAreMonotonicAndCorrectRange()
    {
      var rw = new RandomWeather(seed: 12345, location: RandomWeather.Location.Tokyo);
      var data = rw.Generate(new DateTime(2026, 1, 1), years: 1);

      Assert.Equal(new DateTime(2026, 1, 1, 0, 0, 0), data.Records[0].Time);
      Assert.Equal(new DateTime(2026, 12, 31, 23, 0, 0), data.Records[8759].Time);

      for (int i = 1; i < data.Count; i++)
        Assert.True(data.Records[i].Time >= data.Records[i - 1].Time);
    }

    /// <summary>同じシードで2回呼び出すと決定的に同じ結果になる</summary>
    [Fact]
    public void Generate_SameSeed_Deterministic()
    {
      var rw1 = new RandomWeather(seed: 42, location: RandomWeather.Location.Tokyo);
      var d1 = rw1.Generate(new DateTime(2026, 1, 1), years: 1);

      var rw2 = new RandomWeather(seed: 42, location: RandomWeather.Location.Tokyo);
      var d2 = rw2.Generate(new DateTime(2026, 1, 1), years: 1);

      Assert.Equal(d1.Count, d2.Count);
      for (int i = 0; i < d1.Count; i++)
      {
        Assert.Equal(d1.Records[i].DryBulbTemperature,
                     d2.Records[i].DryBulbTemperature, precision: 9);
        Assert.Equal(d1.Records[i].HumidityRatio,
                     d2.Records[i].HumidityRatio, precision: 9);
      }
    }

    /// <summary>years=0以下はPopoloArgumentExceptionを投げる</summary>
    [Fact]
    public void Generate_ZeroOrNegativeYears_Throws()
    {
      var rw = new RandomWeather(seed: 1, location: RandomWeather.Location.Tokyo);

      Assert.Throws<PopoloArgumentException>(
          () => rw.Generate(new DateTime(2026, 1, 1), years: 0));
      Assert.Throws<PopoloArgumentException>(
          () => rw.Generate(new DateTime(2026, 1, 1), years: -1));
    }
  }
}
