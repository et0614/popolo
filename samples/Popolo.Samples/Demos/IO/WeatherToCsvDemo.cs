/* WeatherToCsvDemo.cs
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

using Popolo.Core.Climate.Weather;
using Popolo.IO.Climate.Weather;

namespace Popolo.Samples.Demos.IO
{
  /// <summary>
  /// Reads a weather file (EPW / HASP / TMY1) and writes its contents to
  /// the Popolo CSV weather format.
  /// </summary>
  /// <remarks>
  /// <para>
  /// Pipeline:
  /// </para>
  /// <list type="number">
  ///   <item><description>Detect the input format from the file extension.</description></item>
  ///   <item><description>Parse the file with the matching
  ///     <see cref="IWeatherDataReader"/>.</description></item>
  ///   <item><description>Serialize to Popolo CSV via
  ///     <see cref="CsvWeatherWriter"/>.</description></item>
  ///   <item><description>Print a short summary (record count, station, value
  ///     ranges) to the console.</description></item>
  /// </list>
  /// <para>
  /// Supported input extensions:
  /// </para>
  /// <list type="bullet">
  ///   <item><description><c>.epw</c> — EnergyPlus Weather</description></item>
  ///   <item><description><c>.has</c>, <c>.hasp</c> — HASP fixed-width</description></item>
  ///   <item><description><c>.tmy</c>, <c>.tm1</c> — NOAA TMY1</description></item>
  /// </list>
  /// <para>
  /// <b>Usage:</b>
  /// </para>
  /// <code>
  /// dotnet run --project samples/Popolo.Samples -- weather-to-csv &lt;input&gt; [output.csv]
  /// </code>
  /// <para>
  /// When <c>output.csv</c> is omitted, the output is written next to the
  /// input with the <c>.csv</c> extension.
  /// </para>
  /// </remarks>
  public sealed class WeatherToCsvDemo : IDemo
  {

    #region IDemo 実装

    public string Name => "weather-to-csv";
    public string Category => "IO";
    public string Description => "Convert an EPW / HASP / TMY1 weather file to Popolo CSV.";

    public int Run(string[] args)
    {
      if (args.Length < 1)
      {
        Console.Error.WriteLine($"Usage: {Name} <input> [output.csv]");
        Console.Error.WriteLine("  Supported input extensions: .epw, .has, .hasp, .tmy, .tm1");
        return 1;
      }

      string inputPath = args[0];
      if (!File.Exists(inputPath))
      {
        Console.Error.WriteLine($"Input file not found: {inputPath}");
        return 2;
      }

      string outputPath = args.Length >= 2
          ? args[1]
          : Path.ChangeExtension(inputPath, ".csv");

      IWeatherDataReader reader;
      try
      {
        reader = CreateReaderFor(inputPath);
      }
      catch (NotSupportedException ex)
      {
        Console.Error.WriteLine(ex.Message);
        return 3;
      }

      Console.WriteLine($"Reading {inputPath} with {reader.GetType().Name}...");
      WeatherData data = reader.Read(inputPath);

      Console.WriteLine($"  {data.Count} records");
      if (data.Count > 0)
      {
        Console.WriteLine($"  Time range   : {data.Records[0].Time:yyyy-MM-dd HH:mm} "
                          + $"- {data.Records[data.Count - 1].Time:yyyy-MM-dd HH:mm}");
        Console.WriteLine($"  Source       : {data.Source}");
        Console.WriteLine($"  TypicalYear  : {data.IsTypicalYear}");
        if (!string.IsNullOrEmpty(data.Station.Name))
        {
          Console.WriteLine($"  Station      : {data.Station.Name} "
                            + $"({data.Station.Latitude:F3}, {data.Station.Longitude:F3}), "
                            + $"elev {data.Station.Elevation:F1} m");
        }
      }

      var writer = new CsvWeatherWriter { AlwaysEmitSourceTime = data.IsTypicalYear };
      writer.Write(data, outputPath);
      Console.WriteLine($"Wrote {outputPath}");
      return 0;
    }

    #endregion

    #region 拡張子からの Reader 解決

    /// <summary>
    /// Returns a reader instance matching the extension of the given file.
    /// </summary>
    /// <param name="path">Input file path.</param>
    /// <exception cref="NotSupportedException">
    /// Thrown if the extension is not one of the supported weather formats.
    /// </exception>
    private static IWeatherDataReader CreateReaderFor(string path)
    {
      string ext = Path.GetExtension(path).ToLowerInvariant();
      return ext switch
      {
        ".epw" => new EpwWeatherReader(),
        ".has" or ".hasp" => new HaspWeatherReader(),
        ".tmy" or ".tm1" => new Tmy1WeatherReader(),
        _ => throw new NotSupportedException(
            $"Unknown weather file extension '{ext}'. "
            + "Supported: .epw, .has, .hasp, .tmy, .tm1."),
      };
    }

    #endregion
  }
}
