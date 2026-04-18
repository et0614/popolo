/* CsvWeatherReader.cs
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
  /// Reads Popolo-native CSV weather files produced by
  /// <see cref="CsvWeatherWriter"/>.
  /// </summary>
  /// <remarks>
  /// <para>
  /// An empty value cell is interpreted as a missing field. Metadata is read
  /// from the <c>#</c>-prefixed comment lines at the top of the file.
  /// </para>
  /// <para>
  /// Numeric values are parsed using
  /// <see cref="CultureInfo.InvariantCulture"/>. Time columns must be in the
  /// ISO 8601 round-trip format produced by the writer.
  /// </para>
  /// <para>
  /// This reader does not act on any <see cref="WeatherReadOptions"/>; all
  /// data is taken verbatim from the file.
  /// </para>
  /// </remarks>
  public class CsvWeatherReader : IWeatherDataReader
  {
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
      return ReadCore(reader);
    }

    private WeatherData ReadCore(TextReader reader)
    {
      var ci = CultureInfo.InvariantCulture;

      var data = new WeatherData { Source = WeatherDataSource.Csv };
      string? name = null;
      double lat = 0, lon = 0, ele = 0;
      bool stationSeen = false;

      // ========== メタデータヘッダ読み込み ==========
      string? line;
      while ((line = reader.ReadLine()) != null)
      {
        if (line.Length == 0) continue;
        if (!line.StartsWith("#", StringComparison.Ordinal)) break;

        string content = line.Substring(1).Trim();
        int colonIdx = content.IndexOf(':');
        if (colonIdx < 0) continue;                  // Popolo Weather Data v3 magic 行はここを通る
        string key = content.Substring(0, colonIdx).Trim();
        string value = content.Substring(colonIdx + 1).Trim();

        switch (key)
        {
          case "Station":
            {
              var parts = value.Split(',');
              if (parts.Length != 4)
                throw new PopoloArgumentException(
                    "Station metadata must have 4 comma-separated fields "
                    + "(name, latitude, longitude, elevation). Got: " + value,
                    "Station");
              name = parts[0].Trim();
              lat = double.Parse(parts[1].Trim(), NumberStyles.Float, ci);
              lon = double.Parse(parts[2].Trim(), NumberStyles.Float, ci);
              ele = double.Parse(parts[3].Trim(), NumberStyles.Float, ci);
              stationSeen = true;
              break;
            }
          case "Source":
            if (Enum.TryParse<WeatherDataSource>(value, ignoreCase: true, out var src))
              data.Source = src;
            break;
          case "IsTypicalYear":
            data.IsTypicalYear = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
            break;
          case "NominalInterval":
            if (TimeSpan.TryParse(value, ci, out var ts))
              data.NominalInterval = ts;
            break;
          // 他のキーは無視 (将来拡張余地)
        }
      }

      if (stationSeen)
        data.Station = new WeatherStationInfo(name ?? "", lat, lon, ele);

      // line には最初のメタデータ以外の行が入っている。これがカラムヘッダのはず。
      if (line == null)
        throw new PopoloArgumentException(
            "CSV contains no column header after metadata.", "stream");

      var columns = ParseCsvLine(line);
      var columnIndex = BuildColumnIndex(columns);

      if (!columnIndex.TryGetValue("Time", out int timeCol))
        throw new PopoloArgumentException(
            "CSV column header does not contain 'Time' column.", "stream");
      columnIndex.TryGetValue("SourceTime", out int sourceTimeCol);
      if (!columnIndex.ContainsKey("SourceTime")) sourceTimeCol = -1;

      // 各 WeatherField とカラム index の対応を作る
      var fieldColumnMap = new List<(WeatherField Field, int Column)>();
      AddFieldIfPresent(columnIndex, fieldColumnMap, WeatherField.DryBulbTemperature,         "DryBulbTemperature[C]");
      AddFieldIfPresent(columnIndex, fieldColumnMap, WeatherField.HumidityRatio,              "HumidityRatio[g/kg]");
      AddFieldIfPresent(columnIndex, fieldColumnMap, WeatherField.AtmosphericPressure,        "AtmosphericPressure[kPa]");
      AddFieldIfPresent(columnIndex, fieldColumnMap, WeatherField.GlobalHorizontalRadiation,  "GlobalHorizontalRadiation[W/m2]");
      AddFieldIfPresent(columnIndex, fieldColumnMap, WeatherField.DirectNormalRadiation,      "DirectNormalRadiation[W/m2]");
      AddFieldIfPresent(columnIndex, fieldColumnMap, WeatherField.DiffuseHorizontalRadiation, "DiffuseHorizontalRadiation[W/m2]");
      AddFieldIfPresent(columnIndex, fieldColumnMap, WeatherField.AtmosphericRadiation,       "AtmosphericRadiation[W/m2]");
      AddFieldIfPresent(columnIndex, fieldColumnMap, WeatherField.WindSpeed,                  "WindSpeed[m/s]");
      AddFieldIfPresent(columnIndex, fieldColumnMap, WeatherField.WindDirection,              "WindDirection[rad]");
      AddFieldIfPresent(columnIndex, fieldColumnMap, WeatherField.Precipitation,              "Precipitation[mm/h]");
      AddFieldIfPresent(columnIndex, fieldColumnMap, WeatherField.CloudCover,                 "CloudCover[0-1]");

      var builder = new WeatherRecordBuilder();

      // ========== データ行読み込み ==========
      while ((line = reader.ReadLine()) != null)
      {
        if (line.Length == 0) continue;
        if (line.StartsWith("#", StringComparison.Ordinal)) continue;

        var cells = ParseCsvLine(line);

        builder.Reset();

        // Time
        if (timeCol >= cells.Length || string.IsNullOrWhiteSpace(cells[timeCol]))
          throw new PopoloArgumentException(
              "CSV data row is missing the Time value.", "stream");
        var t = DateTime.Parse(cells[timeCol], ci, DateTimeStyles.RoundtripKind);
        builder.SetTime(t);

        // SourceTime
        if (sourceTimeCol >= 0 && sourceTimeCol < cells.Length
            && !string.IsNullOrWhiteSpace(cells[sourceTimeCol]))
        {
          var st = DateTime.Parse(cells[sourceTimeCol], ci, DateTimeStyles.RoundtripKind);
          builder.SetSourceTime(st);
        }

        // 観測量カラム
        foreach (var (field, col) in fieldColumnMap)
        {
          if (col >= cells.Length) continue;
          string cell = cells[col];
          if (string.IsNullOrWhiteSpace(cell)) continue;  // 空欄 = 欠測
          double v = double.Parse(cell, NumberStyles.Float, ci);
          SetField(builder, field, v);
        }

        data.Add(builder.ToRecord());
      }

      return data;
    }

    private static void AddFieldIfPresent(
        Dictionary<string, int> columnIndex,
        List<(WeatherField, int)> fieldColumnMap,
        WeatherField field,
        string headerName)
    {
      if (columnIndex.TryGetValue(headerName, out int idx))
        fieldColumnMap.Add((field, idx));
    }

    private static Dictionary<string, int> BuildColumnIndex(string[] columns)
    {
      var dict = new Dictionary<string, int>(columns.Length, StringComparer.Ordinal);
      for (int i = 0; i < columns.Length; i++)
        dict[columns[i].Trim()] = i;
      return dict;
    }

    private static void SetField(WeatherRecordBuilder b, WeatherField field, double v)
    {
      switch (field)
      {
        case WeatherField.DryBulbTemperature:         b.SetDryBulbTemperature(v); break;
        case WeatherField.HumidityRatio:              b.SetHumidityRatio(v); break;
        case WeatherField.AtmosphericPressure:        b.SetAtmosphericPressure(v); break;
        case WeatherField.GlobalHorizontalRadiation:  b.SetGlobalHorizontalRadiation(v); break;
        case WeatherField.DirectNormalRadiation:      b.SetDirectNormalRadiation(v); break;
        case WeatherField.DiffuseHorizontalRadiation: b.SetDiffuseHorizontalRadiation(v); break;
        case WeatherField.AtmosphericRadiation:       b.SetAtmosphericRadiation(v); break;
        case WeatherField.WindSpeed:                  b.SetWindSpeed(v); break;
        case WeatherField.WindDirection:              b.SetWindDirection(v); break;
        case WeatherField.Precipitation:              b.SetPrecipitation(v); break;
        case WeatherField.CloudCover:                 b.SetCloudCover(v); break;
      }
    }

    /// <summary>
    /// Minimal CSV line parser supporting quoted fields with embedded commas
    /// and doubled-quote escapes. Sufficient for Popolo CSV output, which does
    /// not quote numeric or time values but may quote station names.
    /// </summary>
    private static string[] ParseCsvLine(string line)
    {
      var result = new List<string>();
      var current = new System.Text.StringBuilder(line.Length);
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
