/* WindDirectionUtil.cs
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

namespace Popolo.IO.Climate.Weather
{
  /// <summary>
  /// Internal helpers that convert wind direction from external format
  /// conventions to Popolo's radian convention (south = 0, east = negative,
  /// west = positive; same as <c>Incline.HorizontalAngle</c>).
  /// </summary>
  /// <remarks>
  /// External meteorological data typically reports the direction from which
  /// the wind blows, with the convention that 0° is north and angles increase
  /// clockwise. Popolo uses a mathematical south-origin angle, so the formula
  /// is <c>radians = (bearingDegrees - 180°) × π/180</c>, normalised to
  /// (−π, π].
  /// </remarks>
  internal static class WindDirectionUtil
  {
    /// <summary>
    /// Converts a meteorological bearing (degrees, 0 = north, clockwise) to
    /// Popolo wind direction [radian].
    /// </summary>
    public static double FromNorthBearingDegrees(double bearingDegrees)
    {
      double degFromSouth = bearingDegrees - 180.0;
      while (degFromSouth > 180.0) degFromSouth -= 360.0;
      while (degFromSouth <= -180.0) degFromSouth += 360.0;
      return degFromSouth * Math.PI / 180.0;
    }
  }
}
