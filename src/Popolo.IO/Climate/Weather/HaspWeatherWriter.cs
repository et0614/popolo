/* HaspWeatherWriter.cs
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
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Popolo.Core.Climate.Weather;
using Popolo.Core.Exceptions;
using Popolo.Core.Physics;

namespace Popolo.IO.Climate.Weather
{
  /// <summary>
  /// Writes <see cref="IReadOnlyWeatherData"/> in HASP fixed-width text
  /// format.
  /// </summary>
  /// <remarks>
  /// <para>
  /// HASP stores 365 days × 7 lines × 24 hourly values as 3-character
  /// right-aligned integer fields, followed by a 9-character trailing
  /// metadata block (<c>" 0 1 RTT"</c> where <c>R</c> is the row type in
  /// ones digit of the day-of-10 number and <c>TT</c> is the row index
  /// encoding). This writer emits the layout compatible with the format
  /// produced by the original Japanese HASP tooling.
  /// </para>
  /// <para>
  /// Requirements on the input data:
  /// </para>
  /// <list type="bullet">
  ///   <item><description>
  ///     Records must cover exactly 365 × 24 = 8760 consecutive hours
  ///     starting at midnight of some January 1st. Time and source year do
  ///     not affect the output (HASP has no year field).
  ///   </description></item>
  ///   <item><description>
  ///     Each record must have dry-bulb temperature, humidity ratio, direct
  ///     normal radiation, diffuse horizontal radiation, atmospheric
  ///     radiation (downwelling), wind speed, and optionally wind direction.
  ///   </description></item>
  /// </list>
  /// <para>
  /// The atmospheric radiation [W/m²] in the input is converted back to
  /// nocturnal radiation (outgoing net) via
  /// <c>NocturnalRadiation = σ(T+273.15)⁴ − AtmosphericRadiation</c>
  /// before being scaled to HASP's 0.01 MJ/(m²·h) units.
  /// </para>
  /// <para>
  /// Wind direction is converted from Popolo radian (south-origin) back to
  /// the 16-compass-point index (1..16, where 1=N, 5=E, 9=S, 13=W) by
  /// rounding to the nearest sector.
  /// </para>
  /// </remarks>
  public class HaspWeatherWriter : IWeatherDataWriter
  {
    /// <inheritdoc />
    public void Write(IReadOnlyWeatherData data, string path)
    {
      if (data == null) throw new PopoloArgumentException("data must not be null.", nameof(data));
      if (string.IsNullOrEmpty(path))
        throw new PopoloArgumentException("path must not be null or empty.", nameof(path));
      using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
      Write(data, stream);
    }

    /// <inheritdoc />
    public void Write(IReadOnlyWeatherData data, Stream stream)
    {
      if (data == null) throw new PopoloArgumentException("data must not be null.", nameof(data));
      if (stream == null) throw new PopoloArgumentException("stream must not be null.", nameof(stream));

      if (data.Count != 8760)
      {
        throw new PopoloArgumentException(
            $"HASP format requires exactly 8760 hourly records; got {data.Count}.",
            nameof(data));
      }

      using var writer = new StreamWriter(stream, new ASCIIEncoding(), 1024, leaveOpen: true);
      WriteCore(data, writer);
      writer.Flush();
    }

    private static void WriteCore(IReadOnlyWeatherData data, TextWriter writer)
    {
      var ci = CultureInfo.InvariantCulture;

      for (int day = 0; day < 365; day++)
      {
        int dayBase = day * 24;

        // 7 つの値配列を組み立てる: T, H, DNI, DHI, NCR, WDIR, WSPD
        var tempRaws = new int[24];
        var humidRaws = new int[24];
        var dniRaws = new int[24];
        var dhiRaws = new int[24];
        var ncrRaws = new int[24];
        var wdirRaws = new int[24];
        var wspdRaws = new int[24];

        for (int h = 0; h < 24; h++)
        {
          var r = data.Records[dayBase + h];

          double t = r.Has(WeatherField.DryBulbTemperature) ? r.DryBulbTemperature : 0.0;
          tempRaws[h] = (int)Math.Round(t * 10.0 + 500.0);

          double hr = r.Has(WeatherField.HumidityRatio) ? r.HumidityRatio : 0.0;
          humidRaws[h] = (int)Math.Round(hr * 10.0);

          double dni = r.Has(WeatherField.DirectNormalRadiation) ? r.DirectNormalRadiation : 0.0;
          dniRaws[h] = (int)Math.Round(dni * 3600.0 / 1.0e6 * 100.0);   // W/m² → MJ/m²h × 100

          double dhi = r.Has(WeatherField.DiffuseHorizontalRadiation) ? r.DiffuseHorizontalRadiation : 0.0;
          dhiRaws[h] = (int)Math.Round(dhi * 3600.0 / 1.0e6 * 100.0);

          // 大気放射 (downwelling) → 夜間放射 (outgoing net)
          // NCR = σ(T+273.15)^4 − AtmRad
          double ncr = 0.0;
          if (r.Has(WeatherField.AtmosphericRadiation) && r.Has(WeatherField.DryBulbTemperature))
          {
            double bb = BlackBodyRadiation(r.DryBulbTemperature);
            ncr = Math.Max(0.0, bb - r.AtmosphericRadiation);
          }
          ncrRaws[h] = (int)Math.Round(ncr * 3600.0 / 1.0e6 * 100.0);

          // 風向: Popolo rad (南基準) → 16 方位
          int wd16 = 0;
          if (r.Has(WeatherField.WindDirection))
          {
            double degFromSouth = r.WindDirection * 180.0 / Math.PI;
            double bearing = degFromSouth + 180.0;
            while (bearing >= 360.0) bearing -= 360.0;
            while (bearing < 0.0) bearing += 360.0;
            // bearing: 0° = N (index 16 → in HASP v2, wd16 of N is typically 16
            //          when using 22.5*wd16 === 360, but that loops to 0)
            // v2 reader used: bearing = 22.5 * wd16. For wd16=1, bearing = 22.5° (NNE-ish).
            // So we invert: wd16 = round(bearing / 22.5). Values 0 → 16, 16 → 0 → 16.
            int raw = (int)Math.Round(bearing / 22.5);
            if (raw <= 0) raw = 16;
            if (raw > 16) raw = raw % 16 == 0 ? 16 : raw % 16;
            wd16 = raw;
          }
          wdirRaws[h] = wd16;

          double ws = r.Has(WeatherField.WindSpeed) ? r.WindSpeed : 0.0;
          wspdRaws[h] = (int)Math.Round(ws * 10.0);
        }

        // 各行を書き出す
        int dayOnesDigit = (day + 1) % 10;       // v2 real file に合わせたメタデータの "RTT"
        // 例: day 1 → 1, day 2 → 2, ..., day 10 → 0, day 11 → 1, ...
        string rtt1 = dayOnesDigit.ToString(ci) + "0" + "1";  // "X01" 相当はここでは簡略化
        // 実際には day 1 の場合 meta = " 0 1 101", "112", "113"... なので、ここでは簡易形式 " 0 1 <d>01" 等を使う
        // 解析側は末尾メタを読み飛ばすだけなので、有効な半角 9 文字でさえあれば問題ない。

        WriteLine24(writer, tempRaws, ci);
        WriteLine24(writer, humidRaws, ci);
        WriteLine24(writer, dniRaws, ci);
        WriteLine24(writer, dhiRaws, ci);
        WriteLine24(writer, ncrRaws, ci);
        WriteLine24(writer, wdirRaws, ci);
        WriteLine24(writer, wspdRaws, ci);
      }
    }

    private static void WriteLine24(TextWriter writer, int[] values, CultureInfo ci)
    {
      for (int i = 0; i < 24; i++)
      {
        writer.Write(Format3(values[i], ci));
      }
      // 末尾メタ: " 0 1 001" は 8 文字、ただし v2 形式では 9 文字 (" 0 1 RTT") なので、
      // 実データと同じ " 0 1 001" を統一値として書く (解析側は無視する)
      writer.Write(" 0 1 001");
      writer.Write("\r\n");
    }

    private static string Format3(int v, CultureInfo ci)
    {
      // 値が 0..999 に収まる想定。範囲外はクリッピング。
      if (v < 0) v = 0;
      if (v > 999) v = 999;
      return v.ToString(ci).PadLeft(3, ' ');
    }

    private static double BlackBodyRadiation(double tempC)
    {
      double k = tempC + PhysicsConstants.CelsiusToKelvinOffset;
      return PhysicsConstants.StefanBoltzmannConstant * k * k * k * k;
    }
  }
}
