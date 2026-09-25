/* RealDataIntegrationTests.cs
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
using Popolo.IO.Climate.Weather;

namespace Popolo.IO.Tests.Climate.Weather
{
    /// <summary>
    /// 実データを用いた統合テスト。環境変数 POPOLO_TEST_DATA_DIR に
    /// tokyo.has / tokyo.epw / DRYCOLD.TMY の置かれたディレクトリを指定すると走る。
    /// 未指定ならスキップ。
    /// </summary>
    public class RealDataIntegrationTests
    {
        private static readonly string? DataDir =
            Environment.GetEnvironmentVariable("POPOLO_TEST_DATA_DIR");

        private static string? PathFor(string fileName)
        {
            if (string.IsNullOrEmpty(DataDir)) return null;
            string p = Path.Combine(DataDir!, fileName);
            return File.Exists(p) ? p : null;
        }

        /// <summary>tokyo.hasを読み込み、8760件、妥当な1月1日深夜の気温・湿度を確認</summary>
        [Fact]
        public void Hasp_Tokyo_ReadsAndRoundTrips()
        {
            string? path = PathFor("tokyo.has");
            if (path == null) return;  // data absent, skip silently

            var reader = new HaspWeatherReader();
            var data = reader.Read(path);
            Assert.Equal(8760, data.Count);
            Assert.Equal(new DateTime(1999, 1, 1, 0, 0, 0), data.Records[0].Time);
            Assert.Equal(new DateTime(1999, 12, 31, 23, 0, 0), data.Records[8759].Time);
            Assert.InRange(data.Records[0].DryBulbTemperature, -10.0, 15.0);
            Assert.InRange(data.Records[0].HumidityRatio, 0.1, 10.0);

            // ラウンドトリップ
            using var ms = new MemoryStream();
            new HaspWeatherWriter().Write(data, ms);
            ms.Position = 0;
            var rt = reader.Read(ms);
            Assert.Equal(data.Count, rt.Count);
            Assert.Equal(data.Records[0].DryBulbTemperature,
                rt.Records[0].DryBulbTemperature, precision: 1);
        }

        /// <summary>DRYCOLD.TMY (BESTEST寒冷地TMY)を読み、TMYとして認識する</summary>
        [Fact]
        public void Tmy1_DryCold_ReadsAsTypicalYear()
        {
            string? path = PathFor("DRYCOLD.TMY");
            if (path == null) return;

            var reader = new Tmy1WeatherReader();
            var data = reader.Read(path);
            Assert.Equal(8760, data.Count);
            Assert.True(data.IsTypicalYear, "DRYCOLD.TMY should be detected as typical-year");
            // Time は synthetic year (2001), SourceTime は実年
            Assert.Equal(2001, data.Records[0].Time.Year);
            // 寒冷地 — 1月は平均して氷点下近辺
            Assert.InRange(data.Records[0].DryBulbTemperature, -30.0, 10.0);
            // Denver 高地 — 気圧が 85 kPa 付近
            Assert.InRange(data.Records[0].AtmosphericPressure, 70.0, 95.0);
        }

        /// <summary>tokyo.epw を読み、8760件が正しい時刻順で得られ、ラウンドトリップも可能</summary>
        [Fact]
        public void Epw_Tokyo_ReadsInMonotonicOrderAndRoundTrips()
        {
            string? path = PathFor("tokyo.epw");
            if (path == null) return;

            var reader = new EpwWeatherReader();
            var data = reader.Read(path);
            Assert.Equal(8760, data.Count);
            Assert.Equal("TOKYO", data.Station.Name);
            Assert.InRange(data.Station.Latitude, 35.0, 36.0);

            // 時刻が単調増加であること
            for (int i = 1; i < data.Count; i++)
                Assert.True(data.Records[i].Time > data.Records[i - 1].Time,
                    $"Time not monotonic at index {i}");

            // ラウンドトリップ
            using var ms = new MemoryStream();
            new EpwWeatherWriter { CountryCode = "JPN", TimeZone = 9.0 }.Write(data, ms);
            ms.Position = 0;
            var rt = reader.Read(ms);
            Assert.Equal(data.Count, rt.Count);
            Assert.Equal(data.Records[0].DryBulbTemperature,
                rt.Records[0].DryBulbTemperature, precision: 1);
        }
    }
}
