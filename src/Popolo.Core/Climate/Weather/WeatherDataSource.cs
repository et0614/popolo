/* WeatherDataSource.cs
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

    /// <summary>TMY3 format (NREL Typical Meteorological Year, 3rd generation).</summary>
    Tmy3,

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
