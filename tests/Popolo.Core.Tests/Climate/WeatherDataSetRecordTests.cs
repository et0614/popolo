/* WeatherDataSetRecordTests.cs
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
