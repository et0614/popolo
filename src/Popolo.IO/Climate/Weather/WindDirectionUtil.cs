/* WindDirectionUtil.cs
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
