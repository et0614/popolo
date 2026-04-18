/* WeatherDataSource.cs
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

namespace Popolo.Core.Climate.Weather
{
  /// <summary>
  /// Identifies the original provenance of a <see cref="WeatherData"/>.
  /// This is an informational tag and does not affect numerical behaviour.
  /// </summary>
  public enum WeatherDataSource
  {
    /// <summary>Source is not specified.</summary>
    Unknown = 0,

    /// <summary>HASP format (Japanese legacy format).</summary>
    Hasp,

    /// <summary>WEA2 format (extended AMeDAS data).</summary>
    Wea2,

    /// <summary>TMY1 format (NOAA Typical Meteorological Year, 1st generation).</summary>
    Tmy1,

    /// <summary>EXA format (extended AMeDAS data CSV).</summary>
    Exa,

    /// <summary>EPW format (EnergyPlus Weather).</summary>
    Epw,

    /// <summary>Popolo native CSV format.</summary>
    Csv,

    /// <summary>Popolo native JSON format.</summary>
    Json,

    /// <summary>Stochastically generated data.</summary>
    Generated,
  }
}
