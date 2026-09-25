/* CsvWeatherWriter.cs
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

using System;
using System.Globalization;
using System.IO;
using Popolo.Core.Climate.Weather;
using Popolo.Core.Exceptions;

namespace Popolo.IO.Climate.Weather
{
  /// <summary>
  /// Writes <see cref="IReadOnlyWeatherData"/> in the native Popolo CSV format.
  /// </summary>
  /// <remarks>
  /// <para>
  /// The output is round-trip compatible with <see cref="CsvWeatherReader"/>.
  /// An empty cell indicates a missing (un-recorded) field, distinguishing
  /// it from a recorded zero value. Metadata (station, source, nominal
  /// sampling interval, typical-year flag) is emitted as comment lines
  /// prefixed with <c>#</c> at the top of the file.
  /// </para>
  /// <para>
  /// All numeric values are written using
  /// <see cref="CultureInfo.InvariantCulture"/> regardless of the host culture,
  /// so the file is portable across locales. Time columns use the ISO 8601
  /// round-trip format (<c>"o"</c>).
  /// </para>
  /// </remarks>
  public class CsvWeatherWriter : IWeatherDataWriter
  {
    /// <summary>Comment/header marker prefixed to metadata lines.</summary>
    public const string CommentPrefix = "# ";

    /// <summary>Magic header line written at the top of each file.</summary>
    public const string FormatHeader = "Popolo Weather Data v3";

    /// <summary>
    /// If <c>true</c>, the <c>SourceTime</c> column is written to every row
    /// even when <c>SourceTime == Time</c>. When <c>false</c> (default),
    /// <c>SourceTime</c> is written only for typical-year data.
    /// </summary>
    public bool AlwaysEmitSourceTime { get; set; }

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

      // UTF-8, leaveOpen=true so the caller retains ownership of the stream
      using var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(false), 1024, leaveOpen: true);
      WriteCore(data, writer);
      writer.Flush();
    }

    private void WriteCore(IReadOnlyWeatherData data, TextWriter writer)
    {
      var ci = CultureInfo.InvariantCulture;
      bool emitSourceTime = AlwaysEmitSourceTime || data.IsTypicalYear;

      // --- Metadata header ---
      writer.WriteLine(CommentPrefix + FormatHeader);
      writer.WriteLine(CommentPrefix + "Station: " + FormatStation(data.Station));
      writer.WriteLine(CommentPrefix + "Source: " + data.Source.ToString());
      writer.WriteLine(CommentPrefix + "IsTypicalYear: "
          + (data.IsTypicalYear ? "true" : "false"));
      if (data.NominalInterval.HasValue)
        writer.WriteLine(CommentPrefix + "NominalInterval: "
            + data.NominalInterval.Value.ToString("c", ci));

      // --- Column header ---
      writer.Write("Time");
      if (emitSourceTime) writer.Write(",SourceTime");
      writer.Write(",DryBulbTemperature[C]");
      writer.Write(",HumidityRatio[g/kg]");
      writer.Write(",RelativeHumidity[%]");
      writer.Write(",AtmosphericPressure[kPa]");
      writer.Write(",GlobalHorizontalRadiation[W/m2]");
      writer.Write(",DirectNormalRadiation[W/m2]");
      writer.Write(",DiffuseHorizontalRadiation[W/m2]");
      writer.Write(",AtmosphericRadiation[W/m2]");
      writer.Write(",WindSpeed[m/s]");
      writer.Write(",WindDirection[rad]");
      writer.Write(",Precipitation[mm/h]");
      writer.Write(",CloudCover[0-1]");
      writer.WriteLine();

      // --- Data rows ---
      foreach (var r in data.Records)
      {
        writer.Write(r.Time.ToString("o", ci));
        if (emitSourceTime)
        {
          writer.Write(",");
          // Leave blank when SourceTime equals Time to reduce redundancy
          if (r.SourceTime != r.Time)
            writer.Write(r.SourceTime.ToString("o", ci));
        }
        WriteValue(writer, r, WeatherField.DryBulbTemperature,         r.DryBulbTemperature, ci);
        WriteValue(writer, r, WeatherField.HumidityRatio,              r.HumidityRatio, ci);
        WriteValue(writer, r, WeatherField.RelativeHumidity,           r.RelativeHumidity, ci);
        WriteValue(writer, r, WeatherField.AtmosphericPressure,        r.AtmosphericPressure, ci);
        WriteValue(writer, r, WeatherField.GlobalHorizontalRadiation,  r.GlobalHorizontalRadiation, ci);
        WriteValue(writer, r, WeatherField.DirectNormalRadiation,      r.DirectNormalRadiation, ci);
        WriteValue(writer, r, WeatherField.DiffuseHorizontalRadiation, r.DiffuseHorizontalRadiation, ci);
        WriteValue(writer, r, WeatherField.AtmosphericRadiation,       r.AtmosphericRadiation, ci);
        WriteValue(writer, r, WeatherField.WindSpeed,                  r.WindSpeed, ci);
        WriteValue(writer, r, WeatherField.WindDirection,              r.WindDirection, ci);
        WriteValue(writer, r, WeatherField.Precipitation,              r.Precipitation, ci);
        WriteValue(writer, r, WeatherField.CloudCover,                 r.CloudCover, ci);
        writer.WriteLine();
      }
    }

    private static void WriteValue(
        TextWriter writer, WeatherRecord record, WeatherField field, double value, CultureInfo ci)
    {
      writer.Write(",");
      if (record.Has(field)) writer.Write(value.ToString("R", ci));
      // else leave empty -> missing
    }

    private static string FormatStation(WeatherStationInfo station)
    {
      var ci = CultureInfo.InvariantCulture;
      string name = string.IsNullOrEmpty(station.Name) ? "" : station.Name.Replace(',', ' ');
      return string.Format(ci, "{0}, {1}, {2}, {3}",
          name,
          station.Latitude.ToString("R", ci),
          station.Longitude.ToString("R", ci),
          station.Elevation.ToString("R", ci));
    }
  }
}
