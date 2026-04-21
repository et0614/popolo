/* WeatherReaderApiTests.cs
 *
 * Copyright (C) 2026 E.Togashi
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 3 of the License, or (at
 * your option) any later version.
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
  }
}
