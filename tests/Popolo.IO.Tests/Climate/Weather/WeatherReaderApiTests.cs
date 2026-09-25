/* WeatherReaderApiTests.cs
 *
 * Copyright (C) 2026 E.Togashi
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.IO;
using Xunit;

using Popolo.Core.Climate.Weather;
using Popolo.Core.Exceptions;
using Popolo.IO.Climate.Weather;

namespace Popolo.IO.Tests.Climate.Weather
{
  /// <summary>
  /// Tests for the no-arg / options-accepting overloads on
  /// <see cref="IWeatherDataReader"/>.
  /// </summary>
  /// <remarks>
  /// Uses <see cref="CsvWeatherReader"/> because it allows a minimal in-memory
  /// round trip without real data files. The behaviour under test is the
  /// interface contract (presence of the four overloads, null-argument
  /// validation), not anything CSV-specific.
  /// </remarks>
  public class WeatherReaderApiTests
  {
    private static MemoryStream BuildMinimalCsv()
    {
      var data = new WeatherData(
          new WeatherStationInfo("X", 0, 0, 0), WeatherDataSource.Csv);
      data.Add(new WeatherRecordBuilder()
          .SetTime(new DateTime(2026, 1, 1))
          .SetDryBulbTemperature(5.0)
          .ToRecord());

      var mem = new MemoryStream();
      new CsvWeatherWriter().Write(data, mem);
      mem.Position = 0;
      return mem;
    }

    [Fact]
    public void Read_Stream_NoArg_Works()
    {
      using var mem = BuildMinimalCsv();
      IWeatherDataReader reader = new CsvWeatherReader();

      var result = reader.Read(mem);

      Assert.Equal(1, result.Count);
    }

    [Fact]
    public void Read_Stream_WithDefaultOptions_Works()
    {
      using var mem = BuildMinimalCsv();
      IWeatherDataReader reader = new CsvWeatherReader();

      var result = reader.Read(mem, WeatherReadOptions.Default);

      Assert.Equal(1, result.Count);
    }

    [Fact]
    public void Read_Stream_NullOptions_Throws()
    {
      using var mem = BuildMinimalCsv();
      IWeatherDataReader reader = new CsvWeatherReader();

      Assert.Throws<PopoloArgumentException>(
          () => reader.Read(mem, null!));
    }

    [Fact]
    public void Read_Path_NullOptions_Throws()
    {
      IWeatherDataReader reader = new CsvWeatherReader();

      Assert.Throws<PopoloArgumentException>(
          () => reader.Read("some.csv", null!));
    }

    /// <summary>
    /// CsvWeatherReader を介して、options 経由で WeatherCompleter が
    /// 呼び出されること (大気圧補完) を検証する。
    /// </summary>
    [Fact]
    public void Read_WithOptions_InvokesCompleter()
    {
      // 気圧なしの CSV を作り、elevation 補完を有効にして読み戻す
      var original = new WeatherData(
          new WeatherStationInfo("Tokyo", 35.68, 139.77, 40.0),
          WeatherDataSource.Csv);
      original.Add(new WeatherRecordBuilder()
          .SetTime(new DateTime(2026, 6, 21, 12, 0, 0))
          .SetDryBulbTemperature(25.0)
          .ToRecord());

      using var mem = new MemoryStream();
      new CsvWeatherWriter().Write(original, mem);
      mem.Position = 0;

      var reader = new CsvWeatherReader();
      var data = reader.Read(mem, new WeatherReadOptions
      {
        EstimateAtmosphericPressureFromElevation = true,
      });

      Assert.True(data.Records[0].Has(WeatherField.AtmosphericPressure));
      Assert.True(data.Records[0].IsEstimated(WeatherField.AtmosphericPressure));
    }
  }
}
