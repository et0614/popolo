/* WeatherSummaryDemo.cs
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
  /// Reads a weather file (EPW / HASP / TMY1) and prints a monthly summary of
  /// dry-bulb temperature, humidity ratio, and global horizontal radiation.
  /// </summary>
  /// <remarks>
  /// <para>
  /// Demonstrates end-to-end use of the <see cref="IWeatherDataReader"/>
  /// hierarchy: the reader is picked by file extension, records are loaded
  /// into a <see cref="WeatherData"/>, and per-month averages are aggregated
  /// in memory. No output file is written.
  /// </para>
  /// <para>
  /// <b>Usage:</b>
  /// </para>
  /// <code>
  /// dotnet run --project samples/Popolo.Samples -- io-weather-summary [input]
  /// </code>
  /// <para>
  /// When <c>input</c> is omitted, the bundled
  /// <c>SampleData/tokyo.epw</c> is used.
  /// </para>
  /// </remarks>
  public sealed class WeatherSummaryDemo : IDemo
  {
    public string Name => "io-weather-summary";
    public string Category => "IO";
    public string Description => "Monthly summary of a weather file (Tdb, w, GHI).";

    public int Run(string[] args)
    {
      string inputPath = args.Length >= 1
          ? args[0]
          : Path.Combine(
              AppContext.BaseDirectory, "SampleData", "tokyo.epw");

      if (!File.Exists(inputPath))
      {
        Console.Error.WriteLine($"Input file not found: {inputPath}");
        return 1;
      }

      IWeatherDataReader reader;
      try { reader = CreateReaderFor(inputPath); }
      catch (NotSupportedException ex)
      {
        Console.Error.WriteLine(ex.Message);
        return 2;
      }

      Console.WriteLine($"Reading {Path.GetFileName(inputPath)} with {reader.GetType().Name}...");
      WeatherData data = reader.Read(inputPath);
      Console.WriteLine($"  {data.Count} records, source = {data.Source}, typical year = {data.IsTypicalYear}");
      if (!string.IsNullOrEmpty(data.Station.Name))
      {
        Console.WriteLine($"  Station: {data.Station.Name} "
                          + $"({data.Station.Latitude:F3}, {data.Station.Longitude:F3}), "
                          + $"elev {data.Station.Elevation:F1} m");
      }
      Console.WriteLine();

      // Monthly accumulators (indexed 1..12).
      var sumT  = new double[13];
      var sumW  = new double[13];
      var sumGh = new double[13];
      var n     = new int[13];

      foreach (var r in data.Records)
      {
        int m = r.Time.Month;
        sumT[m]  += r.DryBulbTemperature;
        sumW[m]  += r.HumidityRatio;
        sumGh[m] += r.GlobalHorizontalRadiation;
        n[m]++;
      }

      Console.WriteLine("Monthly averages");
      Console.WriteLine("  Month   Tdb [°C]   w [g/kg]   GHI [W/m²]   samples");
      Console.WriteLine("  ------  ---------  ---------  -----------  -------");
      for (int m = 1; m <= 12; m++)
      {
        if (n[m] == 0)
        {
          Console.WriteLine($"  {MonthName(m),-6}   (no data)");
          continue;
        }
        Console.WriteLine(
          $"  {MonthName(m),-6}  {sumT[m] / n[m],8:F2}   {sumW[m] / n[m],8:F2}   {sumGh[m] / n[m],10:F1}   {n[m],7}");
      }
      return 0;
    }

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

    private static string MonthName(int m) => m switch
    {
      1 => "Jan", 2 => "Feb", 3 => "Mar", 4 => "Apr",
      5 => "May", 6 => "Jun", 7 => "Jul", 8 => "Aug",
      9 => "Sep", 10 => "Oct", 11 => "Nov", 12 => "Dec",
      _ => "???",
    };
  }
}
