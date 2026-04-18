/* EpwWeatherReaderWriterTests.cs
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
using System.Linq;
using Xunit;
using Popolo.Core.Climate.Weather;
using Popolo.IO.Climate.Weather;

namespace Popolo.IO.Tests.Climate.Weather
{
    /// <summary>EpwWeatherReader / EpwWeatherWriter のテスト</summary>
    public class EpwWeatherReaderWriterTests
    {
        private static WeatherData MakeSampleYear(int year = 2023)
        {
            var data = new WeatherData(
                new WeatherStationInfo("TestCity", 35.0, 139.0, 25.0),
                WeatherDataSource.Epw)
            {
                NominalInterval = TimeSpan.FromHours(1),
            };
            var start = new DateTime(year, 1, 1, 0, 0, 0);
            for (int h = 0; h < 8760; h++)
            {
                var b = new WeatherRecordBuilder()
                    .SetTime(start.AddHours(h))
                    .SetDryBulbTemperature(10.0 + 10.0 * Math.Sin(2 * Math.PI * h / 8760))
                    .SetHumidityRatio(5.0 + 3.0 * Math.Sin(2 * Math.PI * h / 8760))
                    .SetAtmosphericPressure(101.325)
                    .SetAtmosphericRadiation(350.0)
                    .SetWindSpeed(2.5)
                    .SetWindDirection(0.0);
                if (h % 24 >= 6 && h % 24 < 18)
                {
                    b.SetGlobalHorizontalRadiation(400);
                    b.SetDirectNormalRadiation(600);
                    b.SetDiffuseHorizontalRadiation(100);
                }
                data.Add(b.ToRecord());
            }
            return data;
        }

        /// <summary>EPW Reader/Writer のラウンドトリップで主要フィールドが保持される</summary>
        [Fact]
        public void RoundTrip_PreservesMainFields()
        {
            var original = MakeSampleYear();

            var writer = new EpwWeatherWriter { CountryCode = "JPN", TimeZone = 9.0 };
            using var ms = new MemoryStream();
            writer.Write(original, ms);

            ms.Position = 0;
            var reader = new EpwWeatherReader();
            var rt = reader.Read(ms);

            Assert.Equal(original.Count, rt.Count);
            Assert.Equal(original.Station.Name, rt.Station.Name);
            Assert.Equal(original.Station.Latitude, rt.Station.Latitude, precision: 2);
            Assert.Equal(original.Station.Longitude, rt.Station.Longitude, precision: 2);

            // 複数時刻をサンプルして値の一致を確認
            foreach (int i in new[] { 0, 100, 4380, 8759 })
            {
                var o = original.Records[i];
                var r = rt.Records[i];
                Assert.Equal(o.Time, r.Time);
                Assert.Equal(o.DryBulbTemperature, r.DryBulbTemperature, precision: 1);
                if (o.Has(WeatherField.GlobalHorizontalRadiation))
                    Assert.Equal(o.GlobalHorizontalRadiation, r.GlobalHorizontalRadiation, precision: 0);
                if (o.Has(WeatherField.AtmosphericRadiation))
                    Assert.Equal(o.AtmosphericRadiation, r.AtmosphericRadiation, precision: 0);
            }
        }

        /// <summary>EPW ヘッダが8行あり、LOCATIONで始まる</summary>
        [Fact]
        public void Writer_ProducesExpectedHeader()
        {
            var data = MakeSampleYear();
            var writer = new EpwWeatherWriter();
            using var ms = new MemoryStream();
            writer.Write(data, ms);

            ms.Position = 0;
            using var sr = new StreamReader(ms);
            string firstLine = sr.ReadLine()!;
            Assert.StartsWith("LOCATION,", firstLine);

            // 残り 7 行のヘッダをスキップ
            for (int i = 0; i < 7; i++) Assert.NotNull(sr.ReadLine());

            // 次の行はデータ行 (カンマ区切り、最初のフィールドは年)
            string firstDataLine = sr.ReadLine()!;
            var fields = firstDataLine.Split(',');
            Assert.True(fields.Length >= 22);
            Assert.Equal("2023", fields[0]);
        }

        /// <summary>欠測フィールドはEPW のセンチネル値で書かれ、再読み込み時も欠測として復元される</summary>
        [Fact]
        public void Writer_MissingFields_UseSentinels()
        {
            var data = new WeatherData(
                new WeatherStationInfo("X", 0, 0, 0), WeatherDataSource.Epw);
            // 全年分データは不要、1 件だけで検査
            var start = new DateTime(2023, 1, 1, 0, 0, 0);
            for (int h = 0; h < 8760; h++)
            {
                var b = new WeatherRecordBuilder()
                    .SetTime(start.AddHours(h))
                    .SetDryBulbTemperature(20.0);
                // Only DryBulb is set; all other fields missing
                data.Add(b.ToRecord());
            }

            var writer = new EpwWeatherWriter();
            using var ms = new MemoryStream();
            writer.Write(data, ms);

            ms.Position = 0;
            var reader = new EpwWeatherReader();
            var rt = reader.Read(ms);

            var r = rt.Records[0];
            Assert.True(r.Has(WeatherField.DryBulbTemperature));
            // Missing fields must round-trip as missing
            Assert.False(r.Has(WeatherField.HumidityRatio));
            Assert.False(r.Has(WeatherField.GlobalHorizontalRadiation));
            Assert.False(r.Has(WeatherField.AtmosphericRadiation));
            Assert.False(r.Has(WeatherField.WindSpeed));
            Assert.False(r.Has(WeatherField.CloudCover));
        }

        /// <summary>ヘッダ不足のEPWは PopoloArgumentException</summary>
        [Fact]
        public void Reader_MissingLocationHeader_Throws()
        {
            string bogus = "NOT A LOCATION LINE\r\n";
            using var ms = new MemoryStream(System.Text.Encoding.ASCII.GetBytes(bogus));
            var reader = new EpwWeatherReader();
            Assert.Throws<Popolo.Core.Exceptions.PopoloArgumentException>(() => reader.Read(ms));
        }

        /// <summary>
        /// TMY (複数年起源) の EPW データを書き出し→再読込したとき、
        /// SourceTime.Year と IsTypicalYear フラグが保持される。
        /// </summary>
        [Fact]
        public void RoundTrip_TmyStyle_PreservesSourceYearAndFlag()
        {
            var data = new WeatherData(
                new WeatherStationInfo("TMYCity", 35.0, 139.0, 25.0),
                WeatherDataSource.Epw)
            {
                IsTypicalYear = true,
            };

            // 8760件、各月を異なる元年度から取得したかのように作る
            var logicalStart = new DateTime(2001, 1, 1, 0, 0, 0);
            int[] sourceYears = { 1958, 1970, 2009, 1988, 1987, 2019,
                                   1975, 2005, 1986, 1980, 1957, 1971 };
            for (int h = 0; h < 8760; h++)
            {
                DateTime lt = logicalStart.AddHours(h);
                int srcYear = sourceYears[lt.Month - 1];
                DateTime st;
                try { st = new DateTime(srcYear, lt.Month, lt.Day, lt.Hour, 0, 0); }
                catch { continue; }  // skip invalid dates (e.g., Feb 29 in non-leap source)
                var b = new WeatherRecordBuilder()
                    .SetTime(lt)
                    .SetSourceTime(st)
                    .SetDryBulbTemperature(10.0)
                    .SetHumidityRatio(5.0)
                    .SetAtmosphericPressure(101.325);
                data.Add(b.ToRecord());
            }

            var writer = new EpwWeatherWriter();
            using var ms = new MemoryStream();
            writer.Write(data, ms);

            ms.Position = 0;
            var rt = new EpwWeatherReader().Read(ms);

            Assert.True(rt.IsTypicalYear, "IsTypicalYear should be preserved");
            // Jan → 1958
            Assert.Equal(1958, rt.Records[0].SourceTime.Year);
            // Feb → 1970
            var febRec = rt.Records.First(x => x.Time.Month == 2);
            Assert.Equal(1970, febRec.SourceTime.Year);
            // Mar → 2009
            var marRec = rt.Records.First(x => x.Time.Month == 3);
            Assert.Equal(2009, marRec.SourceTime.Year);
        }
    }
}
