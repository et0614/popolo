/* HaspTmyWriterRoundTripTests.cs
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
    /// <summary>HaspWeatherWriter / Tmy1WeatherWriter のラウンドトリップテスト</summary>
    public class HaspTmyWriterRoundTripTests
    {
        private static WeatherData MakeHaspCompatibleYear()
        {
            var data = new WeatherData
            {
                Source = WeatherDataSource.Hasp,
                NominalInterval = TimeSpan.FromHours(1),
            };
            var start = new DateTime(1999, 1, 1, 0, 0, 0);
            for (int h = 0; h < 8760; h++)
            {
                double t = 15.0 + 10.0 * Math.Sin(2 * Math.PI * h / 8760);
                double hr = 8.0 + 3.0 * Math.Sin(2 * Math.PI * h / 8760);
                double dni = 0, dhi = 0;
                int hourOfDay = h % 24;
                if (hourOfDay >= 6 && hourOfDay < 18)
                {
                    dni = 500;
                    dhi = 150;
                }
                var b = new WeatherRecordBuilder()
                    .SetTime(start.AddHours(h))
                    .SetDryBulbTemperature(t)
                    .SetHumidityRatio(hr)
                    .SetDirectNormalRadiation(dni)
                    .SetDiffuseHorizontalRadiation(dhi)
                    .SetAtmosphericRadiation(350.0)
                    .SetWindSpeed(2.5)
                    .SetWindDirection(0.0);  // south
                data.Add(b.ToRecord());
            }
            return data;
        }

        /// <summary>HASP Writer が 365 x 7 = 2555 行を出力する</summary>
        [Fact]
        public void HaspWriter_ProducesExpectedLineCount()
        {
            var data = MakeHaspCompatibleYear();
            var writer = new HaspWeatherWriter();
            using var ms = new MemoryStream();
            writer.Write(data, ms);

            ms.Position = 0;
            using var sr = new StreamReader(ms);
            int count = 0;
            while (sr.ReadLine() != null) count++;
            Assert.Equal(365 * 7, count);
        }

        /// <summary>8760件以外を渡すとPopoloArgumentException</summary>
        [Fact]
        public void HaspWriter_WrongRecordCount_Throws()
        {
            var data = new WeatherData();
            data.Add(new WeatherRecordBuilder()
                .SetTime(new DateTime(1999, 1, 1))
                .SetDryBulbTemperature(10).ToRecord());

            var writer = new HaspWeatherWriter();
            using var ms = new MemoryStream();
            Assert.Throws<Popolo.Core.Exceptions.PopoloArgumentException>(
                () => writer.Write(data, ms));
        }

        /// <summary>HASP Writer→Reader のラウンドトリップで主要フィールドが保持される</summary>
        [Fact]
        public void HaspRoundTrip_PreservesFields()
        {
            var original = MakeHaspCompatibleYear();

            using var ms = new MemoryStream();
            new HaspWeatherWriter().Write(original, ms);
            ms.Position = 0;
            var rt = new HaspWeatherReader().Read(ms);

            Assert.Equal(original.Count, rt.Count);

            foreach (int i in new[] { 0, 500, 4380, 8759 })
            {
                var o = original.Records[i];
                var r = rt.Records[i];
                Assert.Equal(o.Time, r.Time);
                Assert.Equal(o.DryBulbTemperature, r.DryBulbTemperature, precision: 1);
                Assert.Equal(o.HumidityRatio, r.HumidityRatio, precision: 1);
                // 大気放射はBB減算→再構成で数W/m²の誤差がある
                Assert.Equal(o.AtmosphericRadiation, r.AtmosphericRadiation, tolerance: 5.0);
            }
        }

        /// <summary>TMY1 Writer→Reader のラウンドトリップで主要フィールドが保持される</summary>
        [Fact]
        public void Tmy1RoundTrip_PreservesFields()
        {
            var original = new WeatherData { Source = WeatherDataSource.Tmy1 };
            var start = new DateTime(1985, 1, 1, 0, 0, 0);
            for (int h = 0; h < 48; h++)
            {
                var b = new WeatherRecordBuilder()
                    .SetTime(start.AddHours(h))
                    .SetDryBulbTemperature(5.0 + 0.1 * h)
                    .SetHumidityRatio(3.5)
                    .SetAtmosphericPressure(95.0)
                    .SetWindSpeed(3.5)
                    .SetWindDirection(Math.PI);  // north (from-bearing 0)
                if (h % 24 >= 6 && h % 24 < 18)
                {
                    b.SetGlobalHorizontalRadiation(300);
                    b.SetDirectNormalRadiation(500);
                    b.SetDiffuseHorizontalRadiation(100);
                }
                original.Add(b.ToRecord());
            }

            using var ms = new MemoryStream();
            new Tmy1WeatherWriter { StationNumber = "23062" }.Write(original, ms);
            ms.Position = 0;
            var rt = new Tmy1WeatherReader().Read(ms);

            Assert.Equal(original.Count, rt.Count);
            foreach (int i in new[] { 0, 12, 24, 47 })
            {
                var o = original.Records[i];
                var r = rt.Records[i];
                Assert.Equal(o.DryBulbTemperature, r.DryBulbTemperature, precision: 1);
                Assert.Equal(o.AtmosphericPressure, r.AtmosphericPressure, precision: 1);
                if (o.Has(WeatherField.GlobalHorizontalRadiation))
                    Assert.Equal(o.GlobalHorizontalRadiation, r.GlobalHorizontalRadiation, tolerance: 3.0);
            }
        }
    }
}
