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
  /// Geographic and instrumentation metadata for a weather observation
  /// location.
  /// </summary>
  /// <remarks>
  /// <see cref="AnemometerHeight"/> and <see cref="StationTerrain"/> are
  /// optional (nullable). When both are populated they enable the wind
  /// boundary-layer height correction in
  /// <see cref="Building.MultiRoom.UpdateOutdoorCondition(System.DateTime, IReadOnlySun, WeatherRecord)"/>;
  /// when either is <c>null</c> the recorded wind speed is used as-is.
  /// </remarks>
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
    /// Anemometer height above ground at the station [m]. <c>null</c> when
    /// unknown — the wind height correction is then skipped.
    /// </summary>
    /// <remarks>
    /// The conventional World Meteorological Organization standard is 10 m,
    /// but many sources deviate; only the value actually documented for the
    /// data should be supplied here.
    /// </remarks>
    public double? AnemometerHeight { get; }

    /// <summary>
    /// Terrain category of the station's surroundings. <c>null</c> when
    /// unknown — the wind height correction is then skipped.
    /// </summary>
    public TerrainCategory? StationTerrain { get; }

    /// <summary>
    /// Initializes a new instance with geographic data only.
    /// <see cref="AnemometerHeight"/> and <see cref="StationTerrain"/> remain
    /// <c>null</c>.
    /// </summary>
    public WeatherStationInfo(string name, double latitude, double longitude, double elevation)
        : this(name, latitude, longitude, elevation, null, null) { }

    /// <summary>
    /// Initializes a new instance with full geographic and instrumentation
    /// metadata.
    /// </summary>
    /// <param name="name">Station name.</param>
    /// <param name="latitude">Latitude [degree]. North is positive.</param>
    /// <param name="longitude">Longitude [degree]. East is positive.</param>
    /// <param name="elevation">Ground elevation above mean sea level [m].</param>
    /// <param name="anemometerHeight">Anemometer height above ground [m], or <c>null</c> if unknown.</param>
    /// <param name="stationTerrain">Terrain category of the station surroundings, or <c>null</c> if unknown.</param>
    public WeatherStationInfo(string name, double latitude, double longitude, double elevation,
        double? anemometerHeight, TerrainCategory? stationTerrain)
    {
      Name = name ?? string.Empty;
      Latitude = latitude;
      Longitude = longitude;
      Elevation = elevation;
      AnemometerHeight = anemometerHeight;
      StationTerrain = stationTerrain;
    }

    /// <summary>
    /// Returns a copy of this <see cref="WeatherStationInfo"/> with
    /// <see cref="AnemometerHeight"/> and <see cref="StationTerrain"/>
    /// replaced. Useful when a reader supplies only geographic data and the
    /// instrumentation metadata is provided separately by the caller.
    /// </summary>
    public WeatherStationInfo WithAnemometer(double anemometerHeight, TerrainCategory stationTerrain)
        => new WeatherStationInfo(Name, Latitude, Longitude, Elevation, anemometerHeight, stationTerrain);
  }
}
