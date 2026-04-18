/* HaspWeatherReaderTests.cs
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
using System.Text;
using Xunit;
using Popolo.Core.Climate.Weather;
using Popolo.Core.Exceptions;
using Popolo.IO.Climate.Weather;

namespace Popolo.IO.Tests.Climate.Weather
{
    /// <summary>HaspWeatherReader のテスト</summary>
    public class HaspWeatherReaderTests
    {
        /// <summary>
        /// 合成HASPテキストを生成する。365日×7行×24×3文字。
        /// 全日時で一定値を使う:
        ///   T=20°C → raw 700
        ///   H=10 g/kg → raw 100
        ///   DNI=1.0 MJ/m²h → raw 100
        ///   DHI=0.5 MJ/m²h → raw 50
        ///   NCR=0.1 MJ/m²h → raw 10
        ///   Wind direction=9 (S)
        ///   Wind speed=3.5 m/s → raw 35
        /// </summary>
        private static string GenerateSyntheticHasp()
        {
            string Line(int v) => string.Concat(Enumerable.Repeat(v.ToString("D3"), 24));
            var sb = new StringBuilder();
            for (int d = 0; d < 365; d++)
            {
                sb.Append(Line(700)).Append("\r\n");
                sb.Append(Line(100)).Append("\r\n");
                sb.Append(Line(100)).Append("\r\n");
                sb.Append(Line(50)).Append("\r\n");
                sb.Append(Line(10)).Append("\r\n");
                sb.Append(Line(9)).Append("\r\n");
                sb.Append(Line(35)).Append("\r\n");
            }
            return sb.ToString();
        }

        private static WeatherData ReadSyntheticHasp()
        {
            var reader = new HaspWeatherReader();
            using var ms = new MemoryStream(Encoding.ASCII.GetBytes(GenerateSyntheticHasp()));
            return reader.Read(ms);
        }

        /// <summary>8760件のレコードが得られる</summary>
        [Fact]
        public void Read_ProducesEightSevenSixtyRecords()
        {
            var data = ReadSyntheticHasp();
            Assert.Equal(8760, data.Count);
        }

        /// <summary>Source, NominalInterval が正しく設定される</summary>
        [Fact]
        public void Read_SetsMetadata()
        {
            var data = ReadSyntheticHasp();
            Assert.Equal(WeatherDataSource.Hasp, data.Source);
            Assert.Equal(TimeSpan.FromHours(1), data.NominalInterval);
        }

        /// <summary>既定ではSyntheticYear=1999で時刻範囲が1月1日0時〜12月31日23時</summary>
        [Fact]
        public void Read_DefaultYear_TimeRangeCorrect()
        {
            var data = ReadSyntheticHasp();
            Assert.Equal(new DateTime(1999, 1, 1, 0, 0, 0), data.Records[0].Time);
            Assert.Equal(new DateTime(1999, 12, 31, 23, 0, 0), data.Records[8759].Time);
        }

        /// <summary>SyntheticYearを変えると時刻もそれに追従する</summary>
        [Fact]
        public void Read_CustomSyntheticYear_TimesFollowYear()
        {
            var reader = new HaspWeatherReader { SyntheticYear = 2020 };
            using var ms = new MemoryStream(Encoding.ASCII.GetBytes(GenerateSyntheticHasp()));
            var data = reader.Read(ms);

            Assert.Equal(new DateTime(2020, 1, 1, 0, 0, 0), data.Records[0].Time);
        }

        /// <summary>温度が正しくデコードされる(raw 700 → 20.0 °C)</summary>
        [Fact]
        public void Read_TemperatureDecoded()
        {
            var data = ReadSyntheticHasp();
            Assert.Equal(20.0, data.Records[0].DryBulbTemperature, precision: 9);
        }

        /// <summary>絶対湿度がg/kg単位でデコードされる(raw 100 → 10.0 g/kg)</summary>
        [Fact]
        public void Read_HumidityRatioDecoded()
        {
            var data = ReadSyntheticHasp();
            Assert.Equal(10.0, data.Records[0].HumidityRatio, precision: 9);
        }

        /// <summary>直達日射がW/m²に正しく変換される(1.0 MJ/m²h = 1e6/3600 W/m²)</summary>
        [Fact]
        public void Read_DirectNormalRadiation_ConvertedToWm2()
        {
            var data = ReadSyntheticHasp();
            double expected = 1.0e6 / 3600.0;
            Assert.Equal(expected, data.Records[0].DirectNormalRadiation, precision: 4);
        }

        /// <summary>全てのレコードで必須フィールドが立っている</summary>
        [Fact]
        public void Read_AllRecordsHaveRequiredFields()
        {
            var data = ReadSyntheticHasp();
            foreach (var r in data.Records)
            {
                Assert.True(r.Has(WeatherField.DryBulbTemperature));
                Assert.True(r.Has(WeatherField.HumidityRatio));
                Assert.True(r.Has(WeatherField.DirectNormalRadiation));
                Assert.True(r.Has(WeatherField.DiffuseHorizontalRadiation));
                Assert.True(r.Has(WeatherField.AtmosphericRadiation));
                Assert.True(r.Has(WeatherField.WindSpeed));
                Assert.True(r.Has(WeatherField.WindDirection));
            }
        }

        /// <summary>大気放射は downwelling に変換されている(BB - 夜間放射)ため、夜間放射そのものより大きな値になる</summary>
        [Fact]
        public void Read_AtmosphericRadiation_DownwellingConverted()
        {
            var data = ReadSyntheticHasp();
            // T=20°C で σ(T+273.15)^4 ≈ 418.7 W/m², 夜間放射 0.1 MJ/m²h ≈ 27.8 W/m²
            // ゆえに AtmosphericRadiation ≈ 418.7 - 27.8 ≈ 390.9 W/m²
            double atm = data.Records[0].AtmosphericRadiation;
            Assert.InRange(atm, 380.0, 400.0);
        }

        /// <summary>行数が不足している場合は PopoloArgumentException</summary>
        [Fact]
        public void Read_TruncatedData_Throws()
        {
            var reader = new HaspWeatherReader();
            string truncated = "001002003\r\n";  // たった1行
            using var ms = new MemoryStream(Encoding.ASCII.GetBytes(truncated));

            Assert.Throws<PopoloArgumentException>(() => reader.Read(ms));
        }
    }
}
