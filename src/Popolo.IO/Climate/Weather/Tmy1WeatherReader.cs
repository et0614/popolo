/* Tmy1WeatherReader.cs
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
using Popolo.Core.Climate.Weather;
using Popolo.Core.Exceptions;
using Popolo.Core.Physics;

namespace Popolo.IO.Climate.Weather
{
  /// <summary>
  /// Reads weather data in TMY1 (NOAA Typical Meteorological Year, 1st
  /// generation) fixed-width text format.
  /// </summary>
  /// <remarks>
  /// <para>
  /// TMY1 encodes one hour per line. The WBAN (station) number occupies
  /// positions 0-4, year (2-digit) at 5-6, month at 7-8, day at 9-10, and
  /// hour (1-24) at 11-12. A TMY file typically contains observations from
  /// different calendar years for different months (e.g. January from 1975,
  /// February from 1971, etc.), so this reader:
  /// </para>
  /// <list type="bullet">
  ///   <item><description>
  ///     Stores the original year/month/day/hour in
  ///     <see cref="WeatherRecord.SourceTime"/>.
  ///   </description></item>
  ///   <item><description>
  ///     Stamps a synthetic year (<see cref="SyntheticYear"/>, default
  ///     2001) onto the logical <see cref="WeatherRecord.Time"/> so records
  ///     form a monotonic sequence.
  ///   </description></item>
  ///   <item><description>
  ///     Sets <see cref="WeatherData.IsTypicalYear"/> to <c>true</c>
  ///     whenever the file contains more than one distinct source year;
  ///     otherwise the file is treated as ordinary observation data.
  ///   </description></item>
  /// </list>
  /// <para>
  /// Field layout (0-indexed, matches NOAA TMY1):
  /// </para>
  /// <list type="bullet">
  ///   <item><description>[23] DNI flag ('9' = missing), [24..27] DNI [kJ/(m²·h)]</description></item>
  ///   <item><description>[28] DHI flag, [29..32] DHI [kJ/(m²·h)]</description></item>
  ///   <item><description>[53] GHI flag, [54..57] GHI [kJ/(m²·h)]</description></item>
  ///   <item><description>[98..102] Pressure [0.1 mbar]</description></item>
  ///   <item><description>[103..106] Dry-bulb temperature [0.1 °C]</description></item>
  ///   <item><description>[107..110] Dew-point temperature [0.1 °C]</description></item>
  ///   <item><description>[111..113] Wind direction [degree, north-origin]</description></item>
  ///   <item><description>[114..117] Wind speed [0.1 m/s]</description></item>
  ///   <item><description>[118..119] Total cloud cover [0..10]</description></item>
  /// </list>
  /// <para>
  /// Radiation is converted kJ/(m²·h) → W/m² (÷3.6). Humidity is converted
  /// dew-point → absolute ratio via
  /// <c>MoistAir.GetHumidityRatioFromDryBulbTemperatureAndRelativeHumidity</c>
  /// at the recorded pressure.
  /// </para>
  /// <para>
  /// This reader fixes a bug in v2: the wind-speed offset was erroneously
  /// the same as the wind-direction offset. The corrected offset is used
  /// here.
  /// </para>
  /// </remarks>
  public class Tmy1WeatherReader : IWeatherDataReader
  {
    /// <summary>
    /// Synthetic logical year for typical-year files. Default 2001
    /// (non-leap).
    /// </summary>
    public int SyntheticYear { get; set; } = 2001;

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
      return ParseCore(reader);
    }

    private readonly struct RawRecord
    {
      public RawRecord(int year, int month, int day, int hour0,
          WeatherRecordBuilder builder)
      {
        SourceYear = year; Month = month; Day = day; Hour0 = hour0;
        Builder = builder;
      }
      public int SourceYear { get; }
      public int Month { get; }
      public int Day { get; }
      public int Hour0 { get; }
      public WeatherRecordBuilder Builder { get; }
    }

    private WeatherData ParseCore(TextReader reader)
    {
      var ci = CultureInfo.InvariantCulture;
      var raws = new List<RawRecord>();
      string? line;
      int lineNo = 0;

      while ((line = reader.ReadLine()) != null)
      {
        lineNo++;
        if (line.Length < 122) continue;

        try
        {
          int yy = int.Parse(line.Substring(5, 2), ci);
          int year = yy < 20 ? 2000 + yy : 1900 + yy;
          int month = int.Parse(line.Substring(7, 2), ci);
          int day = int.Parse(line.Substring(9, 2), ci);
          int hour1Based = int.Parse(line.Substring(11, 2), ci);
          int hour0Based = hour1Based == 24 ? 23 : hour1Based - 1;

          var builder = new WeatherRecordBuilder();

          if (line[23] != '9')
          {
            double dni = double.Parse(line.Substring(24, 4), ci) / 3.6;
            builder.SetDirectNormalRadiation(Math.Max(0.0, dni));
          }
          if (line[28] != '9')
          {
            double dhi = double.Parse(line.Substring(29, 4), ci) / 3.6;
            builder.SetDiffuseHorizontalRadiation(Math.Max(0.0, dhi));
          }
          if (line[53] != '9')
          {
            double ghi = double.Parse(line.Substring(54, 4), ci) / 3.6;
            builder.SetGlobalHorizontalRadiation(Math.Max(0.0, ghi));
          }

          double pressureKPa = double.Parse(line.Substring(98, 5), ci) / 100.0;
          if (pressureKPa > 0)
            builder.SetAtmosphericPressure(pressureKPa);

          double dryBulbC = double.Parse(line.Substring(103, 4), ci) / 10.0;
          builder.SetDryBulbTemperature(dryBulbC);

          double dewPointC = double.Parse(line.Substring(107, 4), ci) / 10.0;
          double pForHumidity = pressureKPa > 0
              ? pressureKPa : PhysicsConstants.StandardAtmosphericPressure;
          double hrKgKg = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndRelativeHumidity(
              dewPointC, 100.0, pForHumidity);
          builder.SetHumidityRatio(hrKgKg * 1000.0);

          double windDirDeg = double.Parse(line.Substring(111, 3), ci);
          if (windDirDeg > 0 && windDirDeg <= 360)
            builder.SetWindDirection(WindDirectionUtil.FromNorthBearingDegrees(windDirDeg));

          double windSpeed = double.Parse(line.Substring(114, 4), ci) / 10.0;
          if (windSpeed >= 0 && windSpeed < 999)
            builder.SetWindSpeed(windSpeed);

          string ccStr = line.Substring(118, 2).Trim();
          if (ccStr.Length > 0
              && int.TryParse(ccStr, NumberStyles.Integer, ci, out int ccRaw)
              && ccRaw >= 0 && ccRaw <= 10)
          {
            builder.SetCloudCover(ccRaw / 10.0);
          }

          raws.Add(new RawRecord(year, month, day, hour0Based, builder));
        }
        catch (Exception ex) when (ex is FormatException || ex is OverflowException)
        {
          throw new PopoloArgumentException(
              $"Malformed TMY1 data at line {lineNo}: {ex.Message}", "stream");
        }
      }

      bool isTypicalYear = DetectTypicalYear(raws);

      var data = new WeatherData
      {
        Source = WeatherDataSource.Tmy1,
        NominalInterval = TimeSpan.FromHours(1),
        IsTypicalYear = isTypicalYear,
      };

      int logicalYear = isTypicalYear
          ? SyntheticYear
          : (raws.Count > 0 ? raws[0].SourceYear : SyntheticYear);

      foreach (var raw in raws)
      {
        DateTime sourceTime;
        DateTime logicalTime;
        try
        {
          sourceTime = new DateTime(raw.SourceYear, raw.Month, raw.Day, raw.Hour0, 0, 0);
          logicalTime = new DateTime(logicalYear, raw.Month, raw.Day, raw.Hour0, 0, 0);
        }
        catch (ArgumentOutOfRangeException)
        {
          // うるう年不一致 (logicalYear が非うるうで 2/29 が来た等) はスキップ
          continue;
        }

        raw.Builder.SetTime(logicalTime);
        raw.Builder.SetSourceTime(sourceTime);
        data.Add(raw.Builder.ToRecord());
      }

      return data;
    }

    private static bool DetectTypicalYear(List<RawRecord> raws)
    {
      if (raws.Count == 0) return false;
      int firstYear = raws[0].SourceYear;
      for (int i = 1; i < raws.Count; i++)
        if (raws[i].SourceYear != firstYear) return true;
      return false;
    }
  }
}
