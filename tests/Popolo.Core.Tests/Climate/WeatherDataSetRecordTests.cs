/* WeatherDataSetRecordTests.cs
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

using Popolo.Core.Climate.Weather;
using Popolo.Core.Exceptions;

namespace Popolo.Core.Tests.Climate
{
  /// <summary>Tests for <see cref="WeatherData.SetRecord(int, WeatherRecord)"/>.</summary>
  public class WeatherDataSetRecordTests
  {
    private static WeatherRecord Make(DateTime t, double tdb)
        => new WeatherRecordBuilder().SetTime(t).SetDryBulbTemperature(tdb).ToRecord();

    [Fact]
    public void SetRecord_ReplacesExistingEntryInPlace()
    {
      var data = new WeatherData();
      var t = new DateTime(2026, 1, 1);
      data.Add(Make(t, 5.0));

      var replacement = new WeatherRecordBuilder()
          .SetTime(t)
          .SetDryBulbTemperature(7.5)
          .SetHumidityRatio(3.0)
          .MarkEstimated(WeatherField.HumidityRatio)
          .ToRecord();

      data.SetRecord(0, replacement);

      Assert.Equal(7.5, data.Records[0].DryBulbTemperature);
      Assert.True(data.Records[0].Has(WeatherField.HumidityRatio));
      Assert.True(data.Records[0].IsEstimated(WeatherField.HumidityRatio));
    }

    [Fact]
    public void SetRecord_TimeMismatch_Throws()
    {
      var data = new WeatherData();
      data.Add(Make(new DateTime(2026, 1, 1), 5.0));

      var different = Make(new DateTime(2026, 1, 2), 10.0);

      Assert.Throws<PopoloArgumentException>(() => data.SetRecord(0, different));
    }

    [Fact]
    public void SetRecord_IndexOutOfRange_Throws()
    {
      var data = new WeatherData();
      data.Add(Make(new DateTime(2026, 1, 1), 5.0));

      var r = Make(new DateTime(2026, 1, 1), 5.0);

      Assert.Throws<PopoloArgumentException>(() => data.SetRecord(-1, r));
      Assert.Throws<PopoloArgumentException>(() => data.SetRecord(1, r));
    }
  }
}
