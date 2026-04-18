/* WeatherField.cs
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

namespace Popolo.Core.Climate.Weather
{
  /// <summary>
  /// Bit flags identifying which fields of a <see cref="WeatherRecord"/> hold
  /// recorded values as opposed to unknown (missing) values.
  /// </summary>
  /// <remarks>
  /// A field is considered valid only when its corresponding flag is set.
  /// The underlying numeric field of a <see cref="WeatherRecord"/> whose flag
  /// is not set is undefined and must not be consumed by downstream logic.
  /// </remarks>
  [Flags]
  public enum WeatherField
  {
    /// <summary>No recorded fields.</summary>
    None = 0,

    /// <summary>Dry-bulb temperature [°C].</summary>
    DryBulbTemperature = 1 << 0,

    /// <summary>Absolute humidity ratio [g/kg(DA)].</summary>
    HumidityRatio = 1 << 1,

    /// <summary>Atmospheric pressure [kPa].</summary>
    AtmosphericPressure = 1 << 2,

    /// <summary>Global horizontal solar radiation [W/m²].</summary>
    GlobalHorizontalRadiation = 1 << 3,

    /// <summary>Direct normal solar radiation [W/m²].</summary>
    DirectNormalRadiation = 1 << 4,

    /// <summary>Diffuse horizontal solar radiation [W/m²].</summary>
    DiffuseHorizontalRadiation = 1 << 5,

    /// <summary>Atmospheric (long-wave) downward radiation [W/m²].</summary>
    AtmosphericRadiation = 1 << 6,

    /// <summary>Wind speed [m/s].</summary>
    WindSpeed = 1 << 7,

    /// <summary>
    /// Wind direction [radian]. South = 0, east = negative, west = positive,
    /// following the same convention as <see cref="Incline"/>.
    /// </summary>
    WindDirection = 1 << 8,

    /// <summary>Precipitation rate [mm/h].</summary>
    Precipitation = 1 << 9,

    /// <summary>Cloud cover as fraction [0, 1].</summary>
    CloudCover = 1 << 10,
  }
}
