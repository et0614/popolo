/* Tmy1WeatherWriter.cs
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
using System.Globalization;
using System.IO;
using System.Text;
using Popolo.Core.Climate.Weather;
using Popolo.Core.Exceptions;
using Popolo.Core.Physics;

namespace Popolo.IO.Climate.Weather
{
  /// <summary>
  /// Writes <see cref="IReadOnlyWeatherData"/> in NOAA TMY1 fixed-width text
  /// format.
  /// </summary>
  /// <remarks>
  /// <para>
  /// Output is one hour per line, 133 columns per line. When the input has
  /// <see cref="WeatherRecord.SourceTime"/> differing from
  /// <see cref="WeatherRecord.Time"/> (typical-year data), the source year
  /// is written in columns 5-6, preserving the original meteorological
  /// provenance. Otherwise the logical year is written.
  /// </para>
  /// <para>
  /// Humidity in TMY1 is encoded as a dew-point temperature, re-derived
  /// from the record's absolute humidity ratio and pressure via
  /// <see cref="MoistAir"/>. Radiation is written in kJ/(m²·h) (W/m² × 3.6).
  /// </para>
  /// </remarks>
  public class Tmy1WeatherWriter : IWeatherDataWriter
  {
    /// <summary>Station WBAN number written in columns 0-4. Default "00000".</summary>
    public string StationNumber { get; set; } = "00000";

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

      using var writer = new StreamWriter(stream, new ASCIIEncoding(), 1024, leaveOpen: true);
      WriteCore(data, writer);
      writer.Flush();
    }

    private void WriteCore(IReadOnlyWeatherData data, TextWriter writer)
    {
      var ci = CultureInfo.InvariantCulture;

      string wban = StationNumber.PadLeft(5, '0');
      if (wban.Length > 5) wban = wban.Substring(0, 5);

      foreach (var r in data.Records)
      {
        DateTime t = r.SourceTime != default ? r.SourceTime : r.Time;
        int year2 = t.Year % 100;
        int hour1Based = t.Hour + 1;  // TMY1 の hour は 1..24

        var sb = new StringBuilder(new string(' ', 133));

        // [0..4] WBAN
        WritePad(sb, 0, wban);
        // [5..6] Year (2-digit)
        WritePad(sb, 5, year2.ToString("D2", ci));
        // [7..8] Month
        WritePad(sb, 7, t.Month.ToString("D2", ci));
        // [9..10] Day
        WritePad(sb, 9, t.Day.ToString("D2", ci));
        // [11..12] Hour (1..24)
        WritePad(sb, 11, hour1Based.ToString("D2", ci));

        // [13..22] 予備 (10 桁分空白のまま)

        // [23] DNI flag, [24..27] DNI value (kJ/m²·h)
        if (r.Has(WeatherField.DirectNormalRadiation))
        {
          sb[23] = '0';
          int dniKJ = (int)Math.Round(r.DirectNormalRadiation * 3.6);
          WritePad(sb, 24, Clamp04(dniKJ, ci));
        }
        else
        {
          sb[23] = '9';
          WritePad(sb, 24, "9999");
        }

        // [28] DHI flag, [29..32] DHI value
        if (r.Has(WeatherField.DiffuseHorizontalRadiation))
        {
          sb[28] = '0';
          int dhiKJ = (int)Math.Round(r.DiffuseHorizontalRadiation * 3.6);
          WritePad(sb, 29, Clamp04(dhiKJ, ci));
        }
        else
        {
          sb[28] = '9';
          WritePad(sb, 29, "9999");
        }

        // [33..52] 予備 (20 桁分)

        // [53] GHI flag, [54..57] GHI value
        if (r.Has(WeatherField.GlobalHorizontalRadiation))
        {
          sb[53] = '0';
          int ghiKJ = (int)Math.Round(r.GlobalHorizontalRadiation * 3.6);
          WritePad(sb, 54, Clamp04(ghiKJ, ci));
        }
        else
        {
          sb[53] = '9';
          WritePad(sb, 54, "9999");
        }

        // [58..97] 予備 (40 桁分)

        // [98..102] Pressure (0.1 mbar), 5 chars
        double pressureKPa = r.Has(WeatherField.AtmosphericPressure)
            ? r.AtmosphericPressure : PhysicsConstants.StandardAtmosphericPressure;
        int pressureRaw = (int)Math.Round(pressureKPa * 100.0);  // kPa × 100 → 0.1 mbar
        WritePad(sb, 98, Clamp05(pressureRaw, ci));

        // [103..106] Dry-bulb temperature (0.1 °C), 4 chars signed
        int dbtRaw = r.Has(WeatherField.DryBulbTemperature)
            ? (int)Math.Round(r.DryBulbTemperature * 10.0) : 0;
        WritePadSigned(sb, 103, 4, dbtRaw, ci);

        // [107..110] Dew-point temperature (0.1 °C)
        int dptRaw = 0;
        if (r.Has(WeatherField.DryBulbTemperature) && r.Has(WeatherField.HumidityRatio))
        {
          double hrKgKg = r.HumidityRatio * 1.0e-3;
          try
          {
            double dp = MoistAir.GetDryBulbTemperatureFromHumidityRatioAndRelativeHumidity(
                hrKgKg, 100.0, pressureKPa);
            dptRaw = (int)Math.Round(dp * 10.0);
          }
          catch { dptRaw = 0; }
        }
        WritePadSigned(sb, 107, 4, dptRaw, ci);

        // [111..113] Wind direction (degrees from north, integer), 3 chars
        int windDirDeg = 0;
        if (r.Has(WeatherField.WindDirection))
        {
          double degFromSouth = r.WindDirection * 180.0 / Math.PI;
          double bearing = degFromSouth + 180.0;
          while (bearing >= 360.0) bearing -= 360.0;
          while (bearing < 0.0) bearing += 360.0;
          windDirDeg = (int)Math.Round(bearing);
          if (windDirDeg == 360) windDirDeg = 0;
        }
        WritePad(sb, 111, windDirDeg.ToString("D3", ci));

        // [114..117] Wind speed (0.1 m/s), 4 chars
        int wsRaw = r.Has(WeatherField.WindSpeed)
            ? (int)Math.Round(r.WindSpeed * 10.0) : 0;
        WritePad(sb, 114, Clamp04(wsRaw, ci));

        // [118..119] Cloud cover (0..10)
        int ccRaw = r.Has(WeatherField.CloudCover)
            ? (int)Math.Round(r.CloudCover * 10.0) : 0;
        if (ccRaw > 10) ccRaw = 10;
        if (ccRaw < 0) ccRaw = 0;
        WritePad(sb, 118, ccRaw.ToString("D2", ci));

        // [120..121] Opaque cloud (未サポート) → "00"
        WritePad(sb, 120, "00");

        // [122] 予備 → '9'
        sb[122] = '9';

        writer.Write(sb.ToString());
        writer.Write("\r\n");
      }
    }

    private static void WritePad(StringBuilder sb, int offset, string value)
    {
      for (int i = 0; i < value.Length && offset + i < sb.Length; i++)
        sb[offset + i] = value[i];
    }

    /// <summary>符号付き固定幅: 例 "-066" or "  25"</summary>
    private static void WritePadSigned(StringBuilder sb, int offset, int width, int value, CultureInfo ci)
    {
      string s = value.ToString(ci);
      if (s.Length > width) s = s.Substring(s.Length - width);
      s = s.PadLeft(width, ' ');
      WritePad(sb, offset, s);
    }

    private static string Clamp04(int v, CultureInfo ci)
    {
      if (v < 0) v = 0;
      if (v > 9999) v = 9999;
      return v.ToString("D4", ci);
    }

    private static string Clamp05(int v, CultureInfo ci)
    {
      if (v < 0) v = 0;
      if (v > 99999) v = 99999;
      return v.ToString("D5", ci);
    }
  }
}
