/* Tmy3WeatherReader.cs
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

namespace Popolo.IO.Climate.Weather
{
  /// <summary>
  /// Reads weather data in TMY3 (NREL Typical Meteorological Year,
  /// 3rd generation) CSV format.
  /// </summary>
  /// <remarks>
  /// <para>
  /// File layout per NREL/TP-581-43156: two header lines followed by 8760
  /// hourly data lines.
  /// </para>
  /// <list type="bullet">
  ///   <item><description>
  ///     Header line 1 (7 fields): USAF station ID, station name (quoted),
  ///     state, time zone (signed integer), latitude (positive north),
  ///     longitude (positive east), elevation [m].
  ///   </description></item>
  ///   <item><description>
  ///     Header line 2: 71 column-name fields (each measurement triple is
  ///     value / source flag / uncertainty code).
  ///   </description></item>
  ///   <item><description>
  ///     Data lines: <c>MM/DD/YYYY,HH:MM,</c> followed by the 69 measurement
  ///     fields. Hour values run 1..24 with 1 = the interval ending at
  ///     01:00 (i.e. the first hour of the day) and 24 = the interval ending
  ///     at midnight; this reader maps them to hour 0..23 of the labelled
  ///     date, matching the convention used by
  ///     <see cref="EpwWeatherReader"/>.
  ///   </description></item>
  /// </list>
  /// <para>
  /// Fields parsed (others — illuminance, AOD, visibility, ceiling height,
  /// snow, etc. — are skipped):
  /// </para>
  /// <list type="bullet">
  ///   <item><description>[4]  GHI [W/m²]</description></item>
  ///   <item><description>[7]  DNI [W/m²]</description></item>
  ///   <item><description>[10] DHI [W/m²]</description></item>
  ///   <item><description>[25] TotCld [tenths] → fraction</description></item>
  ///   <item><description>[28] OpqCld [tenths] → fraction</description></item>
  ///   <item><description>[31] Dry-bulb [°C]</description></item>
  ///   <item><description>[37] RHum [%] (combined with Tdb and P to derive humidity ratio)</description></item>
  ///   <item><description>[40] Pressure [mbar] → [kPa]</description></item>
  ///   <item><description>[43] Wdir [deg from north] → Popolo radian (south=0)</description></item>
  ///   <item><description>[46] Wspd [m/s]</description></item>
  ///   <item><description>[64] Lprecip depth [mm]</description></item>
  /// </list>
  /// <para>
  /// Because TMY3 files mix records from multiple source years (typical-year
  /// hybrid), the reader stores the original year in
  /// <see cref="WeatherRecord.SourceTime"/> and stamps a synthetic
  /// <see cref="SyntheticYear"/> onto <see cref="WeatherRecord.Time"/> so the
  /// logical timeline is monotonic. <see cref="WeatherData.IsTypicalYear"/>
  /// is set to <c>true</c> whenever multiple source years appear.
  /// </para>
  /// </remarks>
  public class Tmy3WeatherReader : IWeatherDataReader
  {
    /// <summary>
    /// Synthetic logical year for typical-year files. Default 2001 (non-leap).
    /// </summary>
    public int SyntheticYear { get; set; } = 2001;

    /// <inheritdoc />
    public WeatherData Read(string path) => Read(path, WeatherReadOptions.Default);

    /// <inheritdoc />
    public WeatherData Read(Stream stream) => Read(stream, WeatherReadOptions.Default);

    /// <inheritdoc />
    public WeatherData Read(string path, WeatherReadOptions options)
    {
      if (string.IsNullOrEmpty(path))
        throw new PopoloArgumentException("path must not be null or empty.", nameof(path));
      if (options == null)
        throw new PopoloArgumentException("options must not be null.", nameof(options));
      using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
      return Read(stream, options);
    }

    /// <inheritdoc />
    public WeatherData Read(Stream stream, WeatherReadOptions options)
    {
      if (stream == null)
        throw new PopoloArgumentException("stream must not be null.", nameof(stream));
      if (options == null)
        throw new PopoloArgumentException("options must not be null.", nameof(options));
      using var reader = new StreamReader(stream, leaveOpen: true);
      var data = ParseCore(reader);
      WeatherCompleter.Apply(data, options);
      return data;
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
      var data = new WeatherData
      {
        Source = WeatherDataSource.Tmy3,
        NominalInterval = TimeSpan.FromHours(1),
      };

      // --- Header line 1: station information ---
      string? header1 = reader.ReadLine();
      if (header1 == null)
        throw new PopoloArgumentException("TMY3 file is empty.", "stream");

      string[] hdr = ParseCsvLine(header1);
      if (hdr.Length < 7)
        throw new PopoloArgumentException(
            "TMY3 header line 1 has too few fields (expected 7).", "stream");

      string name = hdr[1].Trim();
      double latitude = double.Parse(hdr[4].Trim(), ci);
      double longitude = double.Parse(hdr[5].Trim(), ci);
      double elevation = double.Parse(hdr[6].Trim(), ci);
      data.Station = new WeatherStationInfo(name, latitude, longitude, elevation);

      // --- Header line 2: column names (skipped; positions are fixed by the spec, so columns are accessed by index) ---
      if (reader.ReadLine() == null)
        throw new PopoloArgumentException(
            "TMY3 file is missing the column-name header line.", "stream");

      // --- Data lines ---
      var raws = new List<RawRecord>();
      string? line;
      int lineNo = 2;
      DateTime lastSourceTime = DateTime.MinValue;
      bool hasPrev = false;

      while ((line = reader.ReadLine()) != null)
      {
        lineNo++;
        if (line.Length == 0) continue;

        string[] f = line.Split(',');
        // A data line has 71 fields (date + time + 69 measurements).
        // Trailing fields should never be missing, but check only the minimum as a safeguard.
        if (f.Length < 47) continue;

        try
        {
          // [0] MM/DD/YYYY, [1] HH:MM (hour 1..24, end of interval)
          string[] dateParts = f[0].Split('/');
          if (dateParts.Length != 3) continue;
          int month = int.Parse(dateParts[0], ci);
          int day = int.Parse(dateParts[1], ci);
          int year = int.Parse(dateParts[2], ci);

          string[] timeParts = f[1].Split(':');
          if (timeParts.Length < 1) continue;
          int hour1Based = int.Parse(timeParts[0], ci);

          DateTime sourceTime;
          if (hour1Based == 24 && hasPrev)
          {
            sourceTime = lastSourceTime.AddHours(1);
          }
          else
          {
            if (month < 1 || month > 12 || day < 1 || day > 31) continue;
            int daysInMonth = DateTime.DaysInMonth(year, month);
            if (day > daysInMonth) continue;
            int hour0Based = hour1Based == 24 ? 23 : hour1Based - 1;
            if (hour0Based < 0 || hour0Based > 23) continue;
            sourceTime = new DateTime(year, month, day, hour0Based, 0, 0);
          }

          lastSourceTime = sourceTime;
          hasPrev = true;

          var builder = new WeatherRecordBuilder();

          // [4] GHI [W/m²], missing: 9999
          if (TryParseDouble(f[4], ci, out double ghi) && ghi >= 0 && ghi < 9000)
            builder.SetGlobalHorizontalRadiation(ghi);

          // [7] DNI [W/m²]
          if (TryParseDouble(f[7], ci, out double dni) && dni >= 0 && dni < 9000)
            builder.SetDirectNormalRadiation(dni);

          // [10] DHI [W/m²]
          if (TryParseDouble(f[10], ci, out double dhi) && dhi >= 0 && dhi < 9000)
            builder.SetDiffuseHorizontalRadiation(dhi);

          // [25] TotCld [tenths], missing: 99
          if (TryParseDouble(f[25], ci, out double tcc) && tcc >= 0 && tcc <= 10)
            builder.SetCloudCover(tcc / 10.0);

          // [28] OpqCld [tenths], missing: 99
          if (TryParseDouble(f[28], ci, out double occ) && occ >= 0 && occ <= 10)
            builder.SetOpaqueCloudCover(occ / 10.0);

          // [31] Dry-bulb [°C], missing: 99.9
          if (TryParseDouble(f[31], ci, out double dbtRaw) && Math.Abs(dbtRaw) < 99.0)
            builder.SetDryBulbTemperature(dbtRaw);

          // [34] Dew-point [°C], missing: 99.9
          // TMY3 files process RH (col 37) and Tdp (col 34) independently, so they
          // do not fully round-trip through the Magnus formula (typically ±0.5°C, up to several °C in extremes).
          // ANSI/ASHRAE 140-2023 Tsky-Informative uses col 34 directly, so
          // col 34 takes precedence in long-wave atmospheric radiation calculations (see WeatherCompleter).
          if (TryParseDouble(f[34], ci, out double tdpRaw) && Math.Abs(tdpRaw) < 99.0)
            builder.SetDewPointTemperature(tdpRaw);

          // [37] RHum [%], missing: 999
          // TMY3 records RH as an independent column, so store RH as-is and
          // delegate the conversion to HumidityRatio to WeatherCompleter.
          if (TryParseDouble(f[37], ci, out double rhRaw) && rhRaw >= 0 && rhRaw <= 110)
            builder.SetRelativeHumidity(rhRaw);

          // [40] Pressure [mbar] → [kPa], missing: 9999
          if (TryParseDouble(f[40], ci, out double pmbar) && pmbar > 0 && pmbar < 1500)
            builder.SetAtmosphericPressure(pmbar / 10.0);

          // [43] Wdir [deg from north], missing: 999
          if (TryParseDouble(f[43], ci, out double wdir) && wdir >= 0 && wdir <= 360)
            builder.SetWindDirection(WindDirectionUtil.FromNorthBearingDegrees(wdir));

          // [46] Wspd [m/s], missing: 99
          if (TryParseDouble(f[46], ci, out double wsp) && wsp >= 0 && wsp < 99)
            builder.SetWindSpeed(wsp);

          // [52] CeilHgt [m]: 0..77000 are normal values, 77777 = unlimited (TMY3 sentinel:
          // the ceilometer detected no ceiling, i.e. the cloud base could not be measured), 99999 = missing.
          //
          // 77777 is a TMY3-specific sentinel carrying the abstract meaning "ceiling unknown".
          // It is passed to the physical model (Sky.GetInfraredRadiationFromSky) as NaN, and
          // the Sky side applies the Martin-Berdahl convention (ceiling unknown → Γ_opaque = exp(2000/82000)).
          // The reader is responsible for translating TMY3-specific numeric codes into the abstract representation.
          //
          // 99999 (missing) means SetCeilingHeight is not called → upper layers fall back to the
          // cloud-cover-based model.
          if (f.Length > 52 && TryParseDouble(f[52], ci, out double ceil))
          {
            if (ceil >= 0 && ceil < 77000)
              builder.SetCeilingHeight(ceil);
            else if ((int)ceil == 77777)
              builder.SetCeilingHeight(double.NaN);   // abstract representation of "ceiling unknown"
          }

          // [64] Lprecip depth [mm], missing values are negatives such as -9900 or 99 (TMY3 spec).
          // Accept only the normal range.
          if (f.Length > 64 && TryParseDouble(f[64], ci, out double precip)
              && precip >= 0 && precip < 999)
            builder.SetPrecipitation(precip);

          raws.Add(new RawRecord(
              sourceTime.Year, sourceTime.Month, sourceTime.Day, sourceTime.Hour,
              builder));
        }
        catch (Exception ex) when (ex is FormatException || ex is OverflowException)
        {
          throw new PopoloArgumentException(
              $"Malformed TMY3 data at line {lineNo}: {ex.Message}", "stream");
        }
      }

      // --- TMY detection and time labelling ---
      bool isTypicalYear = DetectTypicalYear(raws);
      data.IsTypicalYear = isTypicalYear;

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

    private static bool TryParseDouble(string s, IFormatProvider fp, out double value)
    {
      return double.TryParse(s.Trim(), NumberStyles.Float, fp, out value);
    }

    /// <summary>
    /// Minimal CSV line parser supporting quoted fields with embedded commas
    /// and doubled-quote escapes. Used for the station-info header line.
    /// </summary>
    private static string[] ParseCsvLine(string line)
    {
      var result = new List<string>();
      var current = new StringBuilder(line.Length);
      bool inQuotes = false;

      for (int i = 0; i < line.Length; i++)
      {
        char c = line[i];
        if (inQuotes)
        {
          if (c == '"')
          {
            if (i + 1 < line.Length && line[i + 1] == '"')
            {
              current.Append('"');
              i++;
            }
            else
            {
              inQuotes = false;
            }
          }
          else
          {
            current.Append(c);
          }
        }
        else
        {
          if (c == ',')
          {
            result.Add(current.ToString());
            current.Clear();
          }
          else if (c == '"')
          {
            inQuotes = true;
          }
          else
          {
            current.Append(c);
          }
        }
      }
      result.Add(current.ToString());
      return result.ToArray();
    }
  }
}
