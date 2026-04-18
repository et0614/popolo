/* Wea2WeatherReader.cs
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
using System.Text;
using Popolo.Core.Climate.Weather;
using Popolo.Core.Exceptions;

namespace Popolo.IO.Climate.Weather
{
  /// <summary>
  /// Reads weather data in WEA2 format (extended AMeDAS binary).
  /// </summary>
  /// <remarks>
  /// <para>
  /// WEA2 packs 842 stations into a single binary file. For each station, a
  /// dataset of 11 records × 18306 bytes is stored contiguously: one header
  /// record (location metadata including latitude, longitude, elevation,
  /// station name in Shift-JIS) followed by 10 data records — dry-bulb
  /// temperature, absolute humidity, global horizontal radiation, infrared
  /// radiation from sky, wind orientation (16-point compass index), wind
  /// speed, precipitation, sunshine hours, atmospheric pressure, and
  /// relative humidity.
  /// </para>
  /// <para>
  /// This reader extracts one station at a time. The station is selected by
  /// <see cref="LocationIndex"/> (1 ≤ index ≤ 842). The produced
  /// <see cref="WeatherData"/> contains 366 × 24 = 8784 hourly records stamped
  /// with the year encoded in the header record (with the special value
  /// <c>1120</c> mapped to 2021, matching legacy behaviour).
  /// </para>
  /// <para>
  /// Unit conversions and mappings:
  /// </para>
  /// <list type="bullet">
  ///   <item><description>Dry-bulb temperature: raw × 0.1 [°C]</description></item>
  ///   <item><description>Absolute humidity: raw × 0.1 [g/kg(DA)]</description></item>
  ///   <item><description>Global horizontal radiation: raw × 0.01 [MJ/(m²·h)] → ×1e6/3600 [W/m²]</description></item>
  ///   <item><description>
  ///     Infrared radiation from sky (downwelling): raw × 0.01 [MJ/(m²·h)] →
  ///     ×1e6/3600 [W/m²]. This is already the downwelling quantity and
  ///     maps directly to <see cref="WeatherField.AtmosphericRadiation"/>.
  ///   </description></item>
  ///   <item><description>Wind direction: 16-compass-point index (1..16) converted to radian (Popolo south-origin convention). Index 0 (calm) is stored as missing.</description></item>
  ///   <item><description>Wind speed: raw × 0.1 [m/s]</description></item>
  ///   <item><description>Precipitation: raw × 0.1 [mm/h]</description></item>
  ///   <item><description>Atmospheric pressure: raw × 0.1 [hPa] → × 0.1 [kPa] (so raw × 0.01)</description></item>
  /// </list>
  /// <para>
  /// Reading station names requires the Shift-JIS encoding. On .NET Core and
  /// later, this encoding is not available by default; register
  /// <c>CodePagesEncodingProvider.Instance</c> before using this reader if
  /// you need the Japanese station name. Missing Shift-JIS support is
  /// non-fatal — the numeric data is still parsed, and the station name is
  /// left blank.
  /// </para>
  /// <para>
  /// This reader does not act on any <see cref="WeatherReadOptions"/>.
  /// </para>
  /// </remarks>
  public class Wea2WeatherReader : IWeatherDataReader
  {
    /// <summary>Size (bytes) of a single record within a dataset.</summary>
    public const int RecordLength = 18306;

    /// <summary>Size (bytes) of a full per-station dataset (11 records).</summary>
    public const int DatasetLength = RecordLength * 11;

    /// <summary>
    /// Station index to extract (1 ≤ index ≤ 842). Must be set before
    /// calling <see cref="Read(Stream, WeatherReadOptions?)"/>.
    /// </summary>
    public short LocationIndex { get; set; }

    /// <summary>
    /// Initializes a new reader with no location selected. The caller must
    /// set <see cref="LocationIndex"/> before reading.
    /// </summary>
    public Wea2WeatherReader() { }

    /// <summary>
    /// Initializes a new reader for the specified station index.
    /// </summary>
    /// <param name="locationIndex">Station index (1 ≤ index ≤ 842).</param>
    public Wea2WeatherReader(short locationIndex)
    {
      LocationIndex = locationIndex;
    }

    /// <inheritdoc />
    public WeatherData Read(string path, WeatherReadOptions? options = null)
    {
      if (string.IsNullOrEmpty(path))
        throw new PopoloArgumentException("path must not be null or empty.", nameof(path));
      using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
      return Read(stream, options);
    }

    /// <inheritdoc />
    public WeatherData Read(Stream stream, WeatherReadOptions? options = null)
    {
      if (stream == null)
        throw new PopoloArgumentException("stream must not be null.", nameof(stream));
      if (LocationIndex < 1 || LocationIndex > 842)
        throw new PopoloInvalidOperationException(
            "LocationIndex must be set in the range [1, 842] before calling Read.");
      if (!stream.CanSeek)
        throw new PopoloArgumentException(
            "stream must be seekable for WEA2 random access.", nameof(stream));

      // データセット先頭にシーク
      stream.Seek(DatasetLength * (LocationIndex - 1), SeekOrigin.Begin);

      // 先頭レコード = 地点情報
      byte[] buf = new byte[RecordLength];
      ReadExact(stream, buf);

      short locationIndex = BitConverter.ToInt16(buf, 0);
      // [2]: 種別 (未使用)
      short year = BitConverter.ToInt16(buf, 4);
      if (year == 1120) year = 2021;
      double latitude = BitConverter.ToInt16(buf, 14) + 0.001 * BitConverter.ToInt16(buf, 16);
      double longitude = BitConverter.ToInt16(buf, 18) + 0.001 * BitConverter.ToInt16(buf, 20);
      double elevation = BitConverter.ToInt16(buf, 22);

      // 地点名 (Shift-JIS)
      string stationName = TryReadShiftJisName(buf, (3 + 24 * 366) * 2);

      // 10 個のデータレコードを読み、時刻別の値配列に格納
      double[] dbt = new double[366 * 24];
      double[] hr  = new double[366 * 24];
      double[] ghi = new double[366 * 24];
      double[] lwr = new double[366 * 24];  // 大気放射量(downwelling)
      int[]    wd  = new int[366 * 24];
      double[] ws  = new double[366 * 24];
      double[] pr  = new double[366 * 24];
      // sunshine hours は未使用のためスキップ
      double[] atm = new double[366 * 24];  // 気圧(hPa)
      double[] rh  = new double[366 * 24];  // 相対湿度 (未使用)

      double[] cfH = { 0.1, 0.1, 0.01, 0.01, 1, 0.1, 0.1, 0.01, 1, 0.1 };

      for (int rec = 0; rec < 10; rec++)
      {
        long recOffset = DatasetLength * (long)(LocationIndex - 1) + RecordLength * (long)(1 + rec);
        stream.Seek(recOffset, SeekOrigin.Begin);
        ReadExact(stream, buf);

        double scale = cfH[rec];
        for (int j = 0; j < 366 * 24; j++)
        {
          short raw = BitConverter.ToInt16(buf, 2 * j + 6);
          double hourlyValue = scale * (int)(raw / 10);
          switch (rec)
          {
            case 0: dbt[j] = hourlyValue; break;
            case 1: hr[j]  = hourlyValue; break;
            case 2: ghi[j] = hourlyValue; break;
            case 3: lwr[j] = hourlyValue; break;
            case 4: wd[j]  = (int)hourlyValue; break;
            case 5: ws[j]  = hourlyValue; break;
            case 6: pr[j]  = hourlyValue; break;
            case 7: /* sunshine hours discarded */ break;
            case 8: atm[j] = hourlyValue; break;  // hPa
            case 9: rh[j]  = hourlyValue; break;
          }
        }
      }

      // WeatherData 構築
      var data = new WeatherData
      {
        Source = WeatherDataSource.Wea2,
        NominalInterval = TimeSpan.FromHours(1),
        Station = new WeatherStationInfo(stationName, latitude, longitude, elevation),
      };

      var builder = new WeatherRecordBuilder();
      var startDate = new DateTime(year, 1, 1, 0, 0, 0);
      int numHours = IsLeapYear(year) ? 366 * 24 : 365 * 24;

      for (int i = 0; i < numHours; i++)
      {
        builder.Reset();
        builder.SetTime(startDate.AddHours(i));
        builder.SetDryBulbTemperature(dbt[i]);
        builder.SetHumidityRatio(hr[i]);
        builder.SetGlobalHorizontalRadiation(Math.Max(0.0, MJhToWm2(ghi[i])));
        builder.SetAtmosphericRadiation(Math.Max(0.0, MJhToWm2(lwr[i])));
        if (wd[i] >= 1 && wd[i] <= 16)
        {
          double bearing = 22.5 * wd[i];
          if (bearing >= 360.0) bearing -= 360.0;
          builder.SetWindDirection(WindDirectionUtil.FromNorthBearingDegrees(bearing));
        }
        builder.SetWindSpeed(Math.Max(0.0, ws[i]));
        builder.SetPrecipitation(Math.Max(0.0, pr[i]));
        // 気圧: raw は 0.1 hPa 単位 (× cfH[8] = 1 → hPa)、kPa には × 0.1
        if (atm[i] > 0)
          builder.SetAtmosphericPressure(atm[i] * 0.1);
        data.Add(builder.ToRecord());
      }

      return data;
    }

    private static void ReadExact(Stream stream, byte[] buffer)
    {
      int read = 0;
      while (read < buffer.Length)
      {
        int n = stream.Read(buffer, read, buffer.Length - read);
        if (n <= 0)
          throw new PopoloArgumentException(
              $"WEA2 stream ended unexpectedly after {read} of {buffer.Length} bytes.",
              "stream");
        read += n;
      }
    }

    private static string TryReadShiftJisName(byte[] buf, int offset)
    {
      try
      {
        Encoding sjis = Encoding.GetEncoding("Shift_JIS");
        if (offset + 30 > buf.Length) return string.Empty;
        return sjis.GetString(buf, offset, 30).Trim();
      }
      catch (ArgumentException)
      {
        // Shift-JIS が利用不可 (.NET Core 等で CodePagesEncodingProvider 未登録)
        return string.Empty;
      }
      catch (NotSupportedException)
      {
        return string.Empty;
      }
    }

    private static double MJhToWm2(double mjPerM2Hour) => mjPerM2Hour * 1.0e6 / 3600.0;

    private static bool IsLeapYear(int year) =>
        (year % 4 == 0 && year % 100 != 0) || year % 400 == 0;
  }
}
