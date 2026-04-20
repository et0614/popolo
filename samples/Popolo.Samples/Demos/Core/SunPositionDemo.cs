/* SunPositionDemo.cs
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

using Popolo.Core.Climate;

namespace Popolo.Samples.Demos.Core
{
  /// <summary>
  /// Prints solar altitude / azimuth throughout a representative summer and
  /// winter day at a major city, plus the computed sunrise and sunset times.
  /// </summary>
  /// <remarks>
  /// Exercises <see cref="Sun"/> constructed from the built-in
  /// <see cref="Sun.City"/> lookup. Solar azimuth follows the Popolo convention:
  /// south = 0, east = negative, west = positive, in degrees.
  /// </remarks>
  public sealed class SunPositionDemo : IDemo
  {
    public string Name => "climate-sun";
    public string Category => "Core";
    public string Description => "Solar position at Tokyo for summer/winter solstice.";

    public int Run(string[] args)
    {
      var sun = new Sun(Sun.City.Tokyo);

      // Solstices for the current year context
      PrintDay(sun, new DateTime(2026, 6, 21), "Summer solstice (6/21)");
      Console.WriteLine();
      PrintDay(sun, new DateTime(2026, 12, 22), "Winter solstice (12/22)");

      return 0;
    }

    private static void PrintDay(Sun sun, DateTime date, string label)
    {
      // Update at noon to get sunrise/sunset consistent with the same day
      sun.Update(date.AddHours(12));
      DateTime sunrise = sun.GetSunRiseTime();
      DateTime sunset = sun.GetSunSetTime();

      Console.WriteLine($"Tokyo — {label}");
      Console.WriteLine($"  Sunrise  : {sunrise:HH:mm:ss}");
      Console.WriteLine($"  Sunset   : {sunset:HH:mm:ss}");
      Console.WriteLine();
      Console.WriteLine("   Hour   Altitude [°]   Azimuth [° from S, east–]");
      Console.WriteLine("  -----  -------------  --------------------------");

      const double rad2deg = 180.0 / Math.PI;
      for (int hour = 6; hour <= 18; hour++)
      {
        sun.Update(date.AddHours(hour));
        double alt = sun.Altitude * rad2deg;
        double azi = sun.Azimuth * rad2deg;
        Console.WriteLine($"   {hour,2}:00   {alt,11:F2}   {azi,22:F2}");
      }
    }
  }
}
