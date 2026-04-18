/* WeatherStationInfo.cs
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
  /// Geographic information about a weather observation location.
  /// </summary>
  public readonly struct WeatherStationInfo
  {
    /// <summary>Gets the human-readable station name.</summary>
    public string Name { get; }

    /// <summary>Gets the latitude [degree]. North is positive.</summary>
    public double Latitude { get; }

    /// <summary>Gets the longitude [degree]. East is positive.</summary>
    public double Longitude { get; }

    /// <summary>Gets the ground elevation above mean sea level [m].</summary>
    public double Elevation { get; }

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    /// <param name="name">Station name.</param>
    /// <param name="latitude">Latitude [degree]. North is positive.</param>
    /// <param name="longitude">Longitude [degree]. East is positive.</param>
    /// <param name="elevation">Ground elevation above mean sea level [m].</param>
    public WeatherStationInfo(string name, double latitude, double longitude, double elevation)
    {
      Name = name ?? string.Empty;
      Latitude = latitude;
      Longitude = longitude;
      Elevation = elevation;
    }
  }
}
