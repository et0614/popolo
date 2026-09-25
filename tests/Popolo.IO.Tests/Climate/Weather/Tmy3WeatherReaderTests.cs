/* Tmy3WeatherReaderTests.cs
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

using System.Globalization;
using System.IO;
using System.Text;
using Xunit;

using Popolo.Core.Climate;
using Popolo.Core.Climate.Weather;
using Popolo.Core.Physics;
using Popolo.IO.Climate.Weather;

namespace Popolo.IO.Tests.Climate.Weather
{
  /// <summary>
  /// <see cref="Tmy3WeatherReader"/> の単体テスト（合成フィクスチャ使用）。
  /// </summary>
  public class Tmy3WeatherReaderTests
  {
    /// <summary>1 行分の TMY3 データ（71 フィールド）を生成する。</summary>
    private static string MakeDataLine(
        string date, int hour1Based, double tdb, double tdp, double rh, double pMbar,
        double totCldTenths, double opqCldTenths, double ceilM)
    {
      var ci = CultureInfo.InvariantCulture;
      var f = new string[71];
      for (int i = 0; i < f.Length; i++) f[i] = "0";
      f[0] = date;
      f[1] = hour1Based.ToString("00", ci) + ":00";
      f[4] = "0";    // GHI
      f[7] = "0";    // DNI
      f[10] = "0";   // DHI
      f[25] = totCldTenths.ToString(ci);
      f[28] = opqCldTenths.ToString(ci);
      f[31] = tdb.ToString(ci);
      f[34] = tdp.ToString(ci);
      f[37] = rh.ToString(ci);
      f[40] = pMbar.ToString(ci);
      f[43] = "180";
      f[46] = "3.0";
      f[52] = ceilM.ToString(ci);
      f[64] = "0";
      return string.Join(",", f);
    }

    private static MemoryStream MakeFile(params string[] dataLines)
    {
      var sb = new StringBuilder();
      sb.AppendLine("722780,\"PHOENIX SKY HARBOR INTL AP\",AZ,-7.0,33.450,-111.983,337");
      sb.AppendLine(string.Join(",", new string[71]));   // 列名行（読み飛ばされる）
      foreach (var l in dataLines) sb.AppendLine(l);
      return new MemoryStream(Encoding.UTF8.GetBytes(sb.ToString()));
    }

    /// <summary>
    /// 列 34 の露点温度が読み込み後も保持され、RH から逆算した値ではなく
    /// 記録値そのものが大気放射（Martin-Berdahl）の計算に使われることを確認する。
    /// 湿度補完で WeatherRecord が再構築されても Tdp が失われてはならない
    /// （ANSI/ASHRAE 140-2023 Tsky-Informative は TMY3 の Tdp 列を直接使用する）。
    /// </summary>
    [Fact]
    public void Read_PreservesRecordedDewPoint_AndUsesItForAtmosphericRadiation()
    {
      // 20°C, RH 50% → Magnus 逆算の Tdp は約 9.3°C。記録 Tdp は意図的に 5.0°C とずらす。
      const double tdb = 20.0, tdp = 5.0, rh = 50.0, pMbar = 1000.0;
      const double tcc = 5, occ = 3, ceil = 1500;
      using var stream = MakeFile(
          MakeDataLine("01/01/1990", 1, tdb, tdp, rh, pMbar, tcc, occ, ceil));

      var data = new Tmy3WeatherReader().Read(stream, new WeatherReadOptions
      {
        EstimateAtmosphericRadiation = true,
      });

      var r = data.Records[0];
      // Tdp は recorded のまま残る
      Assert.True(r.Has(WeatherField.DewPointTemperature));
      Assert.False(r.IsEstimated(WeatherField.DewPointTemperature));
      Assert.Equal(tdp, r.DewPointTemperature);
      // RH→w 補完も行われている（=再構築を経由している）
      Assert.True(r.IsEstimated(WeatherField.HumidityRatio));

      // 大気放射は記録 Tdp を使った Martin-Berdahl 式と一致する
      double expected = Sky.GetInfraredRadiationFromSky(
          tdb, tdp, pMbar, tcc / 10.0, occ / 10.0, ceil, r.Time.Hour);
      Assert.True(r.IsEstimated(WeatherField.AtmosphericRadiation));
      Assert.Equal(expected, r.AtmosphericRadiation, precision: 9);

      // 参考: RH 由来の Tdp を使った場合とは明確に異なる
      double tdpFromW = MoistAir.GetDewPointTemperatureFromHumidityRatio(
          r.HumidityRatio / 1000.0, pMbar / 10.0);
      double fromW = Sky.GetInfraredRadiationFromSky(
          tdb, tdpFromW, pMbar, tcc / 10.0, occ / 10.0, ceil, r.Time.Hour);
      Assert.True(System.Math.Abs(fromW - expected) > 1.0);
    }
  }
}
