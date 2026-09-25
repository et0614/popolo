/* SunPositionDemo.cs
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
