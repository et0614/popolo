/* ExaWeatherReader.cs
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
using Popolo.Core.Climate.Weather;
using Popolo.Core.Exceptions;

namespace Popolo.IO.Climate.Weather
{
  /// <summary>
  /// Reads weather data in extended AMeDAS (EXA) text format.
  /// </summary>
  /// <remarks>
  /// <para>
  /// EXA is a Japanese format used for expanded AMeDAS weather data. It
  /// contains 1 header line followed by 365 days × 8 lines per day of hourly
  /// values (dry-bulb temperature, humidity ratio, global horizontal
  /// radiation, atmospheric radiation, wind direction, wind speed,
  /// precipitation, and sunshine hours), each line carrying 24 hourly values
  /// as 5-character fixed-width fields.
  /// </para>
  /// <para>
  /// The header line encodes location information: a 3-digit location number
  /// at the start, and latitude/longitude in degrees+arc-minutes encoded in
  /// the trailing fixed-width block. This reader parses them into
  /// <see cref="WeatherStationInfo"/>.
  /// </para>
  /// <para>
  /// Unit conversions applied by this reader:
  /// </para>
  /// <list type="bullet">
  ///   <item><description>Temperature: raw × 0.1 [°C]</description></item>
  ///   <item><description>Humidity ratio: raw × 0.1 [g/kg(DA)]</description></item>
  ///   <item><description>Radiation: raw × 0.01 [MJ/(m²·h)] → ×1e6/3600 [W/m²]</description></item>
  ///   <item><description>Wind direction: raw is already degrees (north-origin, clockwise); converted to Popolo radian convention.</description></item>
  ///   <item><description>Wind speed: raw × 0.1 [m/s]</description></item>
  ///   <item><description>Precipitation: raw as-is [mm/h]</description></item>
  /// </list>
  /// <para>
  /// The atmospheric radiation field in EXA is the downwelling longwave from
  /// the sky (not the outgoing nocturnal loss), so it maps directly to
  /// <see cref="WeatherField.AtmosphericRadiation"/> without conversion.
  /// </para>
  /// <para>
  /// The synthetic year for records is 1999, matching the legacy
  /// <c>WeatherConverter.EXAtoCSV</c> behaviour, and can be overridden via
  /// <see cref="SyntheticYear"/>.
  /// </para>
  /// </remarks>
  public class ExaWeatherReader : IWeatherDataReader
  {
    /// <summary>Synthetic year to stamp onto the records. Default 1999.</summary>
    public int SyntheticYear { get; set; } = 1999;

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

      using var reader = new StreamReader(stream, leaveOpen: true);
      string content = reader.ReadToEnd();
      return ParseCore(content);
    }

    private WeatherData ParseCore(string content)
    {
      var ci = CultureInfo.InvariantCulture;
      string[] lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

      if (lines.Length < 1 + 365 * 8)
      {
        throw new PopoloArgumentException(
            $"EXA data has {lines.Length} lines; expected at least {1 + 365 * 8} "
            + "(1 header + 8 lines × 365 days).",
            "stream");
      }

      // --- ヘッダから地点情報 ---
      string header = lines[0];
      int sL = header.Length;
      if (sL < 23)
      {
        throw new PopoloArgumentException(
            "EXA header is too short to contain location information.", "stream");
      }
      int locationNumber = int.Parse(header.Substring(0, 3), ci);
      double latitude = double.Parse(header.Substring(sL - 23, 2), ci)
          + double.Parse(header.Substring(sL - 21, 3), ci) / 600.0;
      double longitude = double.Parse(header.Substring(sL - 17, 3), ci)
          + double.Parse(header.Substring(sL - 14, 3), ci) / 600.0;

      var data = new WeatherData
      {
        Source = WeatherDataSource.Exa,
        NominalInterval = TimeSpan.FromHours(1),
        Station = new WeatherStationInfo(
            locationNumber.ToString("D3", ci),
            latitude, longitude, elevation: 0.0),
      };

      var builder = new WeatherRecordBuilder();
      var startDate = new DateTime(SyntheticYear, 1, 1, 0, 0, 0);

      for (int day = 0; day < 365; day++)
      {
        string dbtLine = lines[day * 8 + 1];
        string hrtLine = lines[day * 8 + 2];
        string radLine = lines[day * 8 + 3];
        string atmLine = lines[day * 8 + 4];
        string wdrLine = lines[day * 8 + 5];
        string wspLine = lines[day * 8 + 6];
        string rinLine = lines[day * 8 + 7];
        // sunshine hours (lines[day*8+8]) — 記録していない

        for (int hour = 0; hour < 24; hour++)
        {
          int off = hour * 5;

          builder.Reset();
          builder.SetTime(startDate.AddHours(day * 24 + hour));

          if (TryParseField(dbtLine, off, 4, ci, out double dbt))
            builder.SetDryBulbTemperature(dbt * 0.1);
          if (TryParseField(hrtLine, off, 4, ci, out double hrt))
            builder.SetHumidityRatio(hrt * 0.1);
          if (TryParseField(radLine, off, 4, ci, out double ghi))
            builder.SetGlobalHorizontalRadiation(Math.Max(0.0, ghi * 0.01 * 1.0e6 / 3600.0));
          if (TryParseField(atmLine, off, 4, ci, out double atm))
            builder.SetAtmosphericRadiation(Math.Max(0.0, atm * 0.01 * 1.0e6 / 3600.0));
          if (TryParseField(wdrLine, off, 4, ci, out double wdir))
          {
            if (wdir > 0)  // 0 は calm/不明扱い
              builder.SetWindDirection(WindDirectionUtil.FromNorthBearingDegrees(wdir));
          }
          if (TryParseField(wspLine, off, 4, ci, out double wsp))
            builder.SetWindSpeed(Math.Max(0.0, wsp * 0.1));
          if (TryParseField(rinLine, off, 4, ci, out double rin))
            builder.SetPrecipitation(Math.Max(0.0, rin));

          data.Add(builder.ToRecord());
        }
      }

      return data;
    }

    private static bool TryParseField(string line, int offset, int length,
        CultureInfo ci, out double value)
    {
      value = 0;
      if (offset + length > line.Length) return false;
      string s = line.Substring(offset, length).Trim();
      if (s.Length == 0) return false;
      return double.TryParse(s, NumberStyles.Float, ci, out value);
    }
  }
}
