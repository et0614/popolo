/* CsvWeatherRoundTripTests.cs
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
using System.IO;
using Xunit;
using Popolo.Core.Climate.Weather;
using Popolo.IO.Climate.Weather;

namespace Popolo.IO.Tests.Climate.Weather
{
    /// <summary>CsvWeatherReader / CsvWeatherWriter のラウンドトリップテスト</summary>
    public class CsvWeatherRoundTripTests
    {
        private static WeatherData RoundTrip(WeatherData original, out string csvText)
        {
            var writer = new CsvWeatherWriter();
            using var mem = new MemoryStream();
            writer.Write(original, mem);
            csvText = System.Text.Encoding.UTF8.GetString(mem.ToArray());

            mem.Position = 0;
            var reader = new CsvWeatherReader();
            return reader.Read(mem);
        }

        /// <summary>通常データのラウンドトリップでは地点情報とフィールドが保持される</summary>
        [Fact]
        public void RoundTrip_OrdinaryData_PreservesAllFieldsAndStation()
        {
            var original = new WeatherData(
                new WeatherStationInfo("Tokyo", 35.6895, 139.6917, 40.0),
                WeatherDataSource.Csv)
            {
                NominalInterval = TimeSpan.FromHours(1),
            };
            original.Add(new WeatherRecordBuilder()
                .SetTime(new DateTime(2026, 1, 1, 0, 0, 0))
                .SetDryBulbTemperature(5.3).SetHumidityRatio(4.2)
                .SetAtmosphericRadiation(340.1).SetWindSpeed(2.1)
                .SetWindDirection(Math.PI / 2.0).SetCloudCover(0.6)
                .ToRecord());
            original.Add(new WeatherRecordBuilder()
                .SetTime(new DateTime(2026, 1, 1, 1, 0, 0))
                .SetDryBulbTemperature(4.8).SetHumidityRatio(4.1)
                .SetAtmosphericRadiation(338.5)
                // WindSpeed intentionally omitted
                .SetCloudCover(0.7)
                .ToRecord());

            var roundTripped = RoundTrip(original, out _);

            Assert.Equal(original.Count, roundTripped.Count);
            Assert.Equal(original.Station.Name, roundTripped.Station.Name);
            Assert.Equal(original.Station.Latitude, roundTripped.Station.Latitude, precision: 9);
            Assert.Equal(original.Station.Longitude, roundTripped.Station.Longitude, precision: 9);
            Assert.Equal(original.NominalInterval, roundTripped.NominalInterval);

            for (int i = 0; i < original.Count; i++)
            {
                var o = original.Records[i];
                var r = roundTripped.Records[i];
                Assert.Equal(o.Time, r.Time);
                Assert.Equal(o.AvailableFields, r.AvailableFields);
                if (o.Has(WeatherField.DryBulbTemperature))
                    Assert.Equal(o.DryBulbTemperature, r.DryBulbTemperature, precision: 9);
                if (o.Has(WeatherField.HumidityRatio))
                    Assert.Equal(o.HumidityRatio, r.HumidityRatio, precision: 9);
                if (o.Has(WeatherField.WindDirection))
                    Assert.Equal(o.WindDirection, r.WindDirection, precision: 12);
            }
        }

        /// <summary>TMYスタイル: IsTypicalYear と SourceTime が保持される</summary>
        [Fact]
        public void RoundTrip_TypicalYearData_PreservesSourceTime()
        {
            var original = new WeatherData(
                new WeatherStationInfo("Tokyo", 35.6895, 139.6917, 40.0),
                WeatherDataSource.Tmy1)
            {
                IsTypicalYear = true,
                NominalInterval = TimeSpan.FromHours(1),
            };
            original.Add(new WeatherRecordBuilder()
                .SetTime(new DateTime(2026, 1, 1, 0, 0, 0))
                .SetSourceTime(new DateTime(2002, 1, 1, 0, 0, 0))
                .SetDryBulbTemperature(5.0).ToRecord());
            original.Add(new WeatherRecordBuilder()
                .SetTime(new DateTime(2026, 2, 1, 0, 0, 0))
                .SetSourceTime(new DateTime(1999, 2, 1, 0, 0, 0))
                .SetDryBulbTemperature(3.0).ToRecord());

            var roundTripped = RoundTrip(original, out _);

            Assert.True(roundTripped.IsTypicalYear);
            Assert.Equal(2002, roundTripped.Records[0].SourceTime.Year);
            Assert.Equal(1999, roundTripped.Records[1].SourceTime.Year);
            Assert.Equal(2026, roundTripped.Records[0].Time.Year);
            Assert.Equal(2026, roundTripped.Records[1].Time.Year);
        }

        /// <summary>欠測フィールドは空欄として書かれ、読み戻した時にも欠測として復元される</summary>
        [Fact]
        public void RoundTrip_MissingFields_PreservedAsMissing()
        {
            var original = new WeatherData();
            original.Add(new WeatherRecordBuilder()
                .SetTime(new DateTime(2026, 1, 1))
                .SetDryBulbTemperature(5.0)
                // 湿度、放射、風など全て欠測
                .ToRecord());

            var roundTripped = RoundTrip(original, out string csvText);

            var r = roundTripped.Records[0];
            Assert.True(r.Has(WeatherField.DryBulbTemperature));
            Assert.False(r.Has(WeatherField.HumidityRatio));
            Assert.False(r.Has(WeatherField.GlobalHorizontalRadiation));
            Assert.False(r.Has(WeatherField.WindSpeed));

            // CSVには空欄 (",," 連続) が現れる
            Assert.Contains(",,", csvText);
        }

        /// <summary>AlwaysEmitSourceTimeをtrueにすると通常データにもSourceTime列が出力される</summary>
        [Fact]
        public void RoundTrip_AlwaysEmitSourceTime_ColumnAppearsInOutput()
        {
            var original = new WeatherData(
                new WeatherStationInfo("X", 0, 0, 0), WeatherDataSource.Csv);
            original.Add(new WeatherRecordBuilder()
                .SetTime(new DateTime(2026, 1, 1))
                .SetDryBulbTemperature(5.0).ToRecord());

            var writer = new CsvWeatherWriter { AlwaysEmitSourceTime = true };
            using var mem = new MemoryStream();
            writer.Write(original, mem);
            string csvText = System.Text.Encoding.UTF8.GetString(mem.ToArray());

            Assert.Contains("SourceTime", csvText);
        }
    }
}
