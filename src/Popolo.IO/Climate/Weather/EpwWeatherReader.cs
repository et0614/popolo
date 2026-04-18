/* EpwWeatherReader.cs
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

namespace Popolo.IO.Climate.Weather
{
  /// <summary>
  /// Reads EnergyPlus Weather (EPW) files.
  /// </summary>
  /// <remarks>
  /// <para>
  /// EPW files consist of 8 header lines followed by hourly data lines (one
  /// per hour, comma-separated). This reader parses:
  /// </para>
  /// <list type="bullet">
  ///   <item><description>
  ///     The <c>LOCATION</c> header line for station metadata (name,
  ///     latitude, longitude, elevation).
  ///   </description></item>
  ///   <item><description>
  ///     Each data line's year, month, day, hour (1..24), dry-bulb
  ///     temperature [°C], dew-point temperature [°C], relative humidity
  ///     [%], atmospheric pressure [Pa], horizontal infrared radiation from
  ///     sky [W/m²], global horizontal / direct normal / diffuse horizontal
  ///     radiation [W/m²], wind direction [degrees from north, clockwise],
  ///     wind speed [m/s], total sky cover [0..10], and liquid
  ///     precipitation depth [mm].
  ///   </description></item>
  /// </list>
  /// <para>
  /// Conversions applied:
  /// </para>
  /// <list type="bullet">
  ///   <item><description>Pressure [Pa] → [kPa] (÷ 1000).</description></item>
  ///   <item><description>
  ///     Relative humidity + dry-bulb + pressure → absolute humidity ratio
  ///     [g/kg(DA)] via psychrometric formulas from <c>MoistAir</c>.
  ///   </description></item>
  ///   <item><description>
  ///     Cloud cover [0..10] → fraction [0..1] (÷ 10).
  ///   </description></item>
  ///   <item><description>
  ///     Wind direction [deg, N-origin] → [rad, Popolo S-origin convention].
  ///   </description></item>
  /// </list>
  /// <para>
  /// EPW missing-value markers (e.g. <c>9999</c> for radiation, <c>999</c>
  /// for temperature, <c>999999</c> for illuminance) are detected field by
  /// field and stored as missing rather than as literal numeric values.
  /// </para>
  /// <para>
  /// When the EPW file mixes records from different source years (typical
  /// AMY/TMY hybrid), <see cref="WeatherData.IsTypicalYear"/> is set to
  /// <c>true</c> and each record's <see cref="WeatherRecord.SourceTime"/>
  /// preserves the original year. The logical <see cref="WeatherRecord.Time"/>
  /// uses <see cref="SyntheticYear"/> (default 2001) to keep records
  /// monotonically ordered.
  /// </para>
  /// </remarks>
  public class EpwWeatherReader : IWeatherDataReader
  {
    /// <summary>
    /// Synthetic logical year stamped onto records when the file contains
    /// multiple source years. Ignored when all records share one year.
    /// Default is 2001 (non-leap).
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
      public RawRecord(int sourceYear, int month, int day, int hour0,
          WeatherRecordBuilder builder)
      {
        SourceYear = sourceYear; Month = month; Day = day; Hour0 = hour0;
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
        Source = WeatherDataSource.Epw,
        NominalInterval = TimeSpan.FromHours(1),
      };

      // --- 8 行のヘッダ ---
      string? line = reader.ReadLine();
      if (line == null)
        throw new PopoloArgumentException("EPW file is empty.", "stream");

      // LOCATION,<name>,<region>,<country>,<source>,<WMO>,<lat>,<lon>,<tz>,<elev>
      var locParts = line.Split(',');
      if (locParts.Length < 10 || !locParts[0].Equals("LOCATION", StringComparison.OrdinalIgnoreCase))
        throw new PopoloArgumentException(
            "EPW file does not start with a LOCATION header line.", "stream");

      string name = locParts[1].Trim();
      double latitude = double.Parse(locParts[6].Trim(), ci);
      double longitude = double.Parse(locParts[7].Trim(), ci);
      double elevation = double.Parse(locParts[9].Trim(), ci);
      data.Station = new WeatherStationInfo(name, latitude, longitude, elevation);

      // 残り 7 行のヘッダをスキップ (DESIGN CONDITIONS ... DATA PERIODS)
      for (int i = 0; i < 7; i++)
      {
        if (reader.ReadLine() == null)
          throw new PopoloArgumentException(
              "EPW file header is truncated (expected 8 header lines).", "stream");
      }

      // --- データ行 ---
      var raws = new List<RawRecord>();
      int lineNo = 8;
      int skippedBadDate = 0;
      // EPW convention: hour=24 は「その時点まで」の 1 時間分を表す。
      // 一部の生成系では hour=24 の行の日付フィールドが前日でなく翌月先頭近辺の
      // 誤った日付になっていることがある。そのため hour=24 行は直前の行から
      // 時刻を前進させる方式でパースする。
      DateTime lastSourceTime = DateTime.MinValue;
      bool hasPrev = false;

      while ((line = reader.ReadLine()) != null)
      {
        lineNo++;
        if (line.Length == 0) continue;

        string[] f = line.Split(',');
        if (f.Length < 22) continue;  // データ行として短すぎる

        try
        {
          int year = int.Parse(f[0].Trim(), ci);
          int month = int.Parse(f[1].Trim(), ci);
          int day = int.Parse(f[2].Trim(), ci);
          int hour1Based = int.Parse(f[3].Trim(), ci);

          DateTime sourceTime;
          if (hour1Based == 24 && hasPrev)
          {
            // 前行の次の時刻とみなす (EPW 仕様に沿った解釈、不正な日付ラベルを回避)
            sourceTime = lastSourceTime.AddHours(1);
          }
          else
          {
            // 日付フィールドの妥当性チェック
            if (month < 1 || month > 12 || day < 1 || day > 31)
            {
              skippedBadDate++;
              continue;
            }
            int daysInMonth = DateTime.DaysInMonth(year, month);
            if (day > daysInMonth)
            {
              skippedBadDate++;
              continue;
            }
            int hour0Based = hour1Based == 24 ? 23 : hour1Based - 1;
            if (hour0Based < 0 || hour0Based > 23)
            {
              skippedBadDate++;
              continue;
            }
            sourceTime = new DateTime(year, month, day, hour0Based, 0, 0);
          }

          lastSourceTime = sourceTime;
          hasPrev = true;

          var builder = new WeatherRecordBuilder();
          bool dbtSet = false;

          // [6] 乾球温度 [°C], 欠測: 99.9
          double dbt = 0;
          if (TryParseDouble(f[6], ci, out dbt) && dbt < 99.0)
          {
            builder.SetDryBulbTemperature(dbt);
            dbtSet = true;
          }

          // [8] 相対湿度 [%], 欠測: 999
          double relHumidity = double.NaN;
          if (TryParseDouble(f[8], ci, out double rh) && rh >= 0.0 && rh <= 110.0)
            relHumidity = rh;

          // [9] 大気圧 [Pa], 欠測: 999999
          double? pressureKPa = null;
          if (TryParseDouble(f[9], ci, out double patm) && patm > 0 && patm < 200000)
          {
            pressureKPa = patm / 1000.0;
            builder.SetAtmosphericPressure(pressureKPa.Value);
          }

          // 乾球 + 相対湿度 + 気圧 → 絶対湿度 [g/kg]
          if (dbtSet && !double.IsNaN(relHumidity))
          {
            double pForHr = pressureKPa ?? Popolo.Core.Physics.PhysicsConstants.StandardAtmosphericPressure;
            double hrKgKg = Popolo.Core.Physics.MoistAir
                .GetHumidityRatioFromDryBulbTemperatureAndRelativeHumidity(
                    dbt, relHumidity, pForHr);
            builder.SetHumidityRatio(hrKgKg * 1000.0);
          }

          // [12] 大気(下向き)放射量 [W/m²], 欠測: 9999
          if (TryParseDouble(f[12], ci, out double atm) && atm > 0 && atm < 9000)
            builder.SetAtmosphericRadiation(atm);

          // [13] 全天日射 [W/m²] (単位は W/m² であり Wh/m² という表記は誤導的)
          if (TryParseDouble(f[13], ci, out double ghi) && ghi >= 0 && ghi < 9000)
            builder.SetGlobalHorizontalRadiation(ghi);

          // [14] 直達日射 [W/m²]
          if (TryParseDouble(f[14], ci, out double dni) && dni >= 0 && dni < 9000)
            builder.SetDirectNormalRadiation(dni);

          // [15] 天空日射 [W/m²]
          if (TryParseDouble(f[15], ci, out double dhi) && dhi >= 0 && dhi < 9000)
            builder.SetDiffuseHorizontalRadiation(dhi);

          // [20] 風向 [deg], 欠測: 999
          if (TryParseDouble(f[20], ci, out double windDir) && windDir >= 0 && windDir <= 360)
            builder.SetWindDirection(WindDirectionUtil.FromNorthBearingDegrees(windDir));

          // [21] 風速 [m/s], 欠測: 999
          if (TryParseDouble(f[21], ci, out double windSpeed) && windSpeed >= 0 && windSpeed < 99)
            builder.SetWindSpeed(windSpeed);

          // [22] 全天雲量 [0..10], 欠測: 99
          if (f.Length > 22 && TryParseDouble(f[22], ci, out double tcc)
              && tcc >= 0 && tcc <= 10)
            builder.SetCloudCover(tcc / 10.0);

          // [33] 降水量 [mm], 欠測: 999
          if (f.Length > 33 && TryParseDouble(f[33], ci, out double precip)
              && precip >= 0 && precip < 999)
            builder.SetPrecipitation(precip);

          raws.Add(new RawRecord(
              sourceTime.Year, sourceTime.Month, sourceTime.Day, sourceTime.Hour,
              builder));
        }
        catch (Exception ex) when (ex is FormatException || ex is OverflowException)
        {
          throw new PopoloArgumentException(
              $"Malformed EPW data at line {lineNo}: {ex.Message}", "stream");
        }
      }

      // --- TMY 判定と時刻ラベル付け ---
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
  }
}
