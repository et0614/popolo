/* PhysicsConstants.cs
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

namespace Popolo.Core.Physics
{
  /// <summary>
  /// Provides physical constants and unit conversion utilities.
  /// </summary>
  public static class PhysicsConstants
  {
    /// <summary>
    /// Offset for converting between Celsius and Kelvin [K].
    /// </summary>
    public const double CelsiusToKelvinOffset = 273.15;

    /// <summary>
    /// Standard atmospheric pressure at sea level [kPa].
    /// </summary>
    public const double StandardAtmosphericPressure = 101.325;

    /// <summary>
    /// Converts a temperature from Celsius [°C] to Kelvin [K].
    /// </summary>
    /// <param name="celsius">Temperature in Celsius [°C]</param>
    /// <returns>Temperature in Kelvin [K]</returns>
    public static double ToKelvin(double celsius) => celsius + CelsiusToKelvinOffset;

    /// <summary>
    /// Converts a temperature from Kelvin [K] to Celsius [°C].
    /// </summary>
    /// <param name="kelvin">Temperature in Kelvin [K]</param>
    /// <returns>Temperature in Celsius [°C]</returns>
    public static double ToCelsius(double kelvin) => kelvin - CelsiusToKelvinOffset;
  }
}
