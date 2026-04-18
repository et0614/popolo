/* IWeatherDataReader.cs
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

using System.IO;
using Popolo.Core.Climate.Weather;

namespace Popolo.IO.Climate.Weather
{
  /// <summary>
  /// Reads <see cref="WeatherData"/> from a specific on-disk format.
  /// </summary>
  /// <remarks>
  /// Implementations set <see cref="WeatherData.Source"/> to the appropriate
  /// <see cref="WeatherDataSource"/> value, populate
  /// <see cref="WeatherData.Station"/> when the format carries station
  /// information, and emit records with <see cref="WeatherField"/> flags set
  /// exactly for fields that are present in the source.
  /// </remarks>
  public interface IWeatherDataReader
  {
    /// <summary>
    /// Reads weather data from the specified stream.
    /// </summary>
    /// <param name="stream">The stream positioned at the start of the data.</param>
    /// <param name="options">
    /// Reader options. When <c>null</c>, <see cref="WeatherReadOptions.Default"/>
    /// is used.
    /// </param>
    /// <returns>A fully-populated <see cref="WeatherData"/>.</returns>
    WeatherData Read(Stream stream, WeatherReadOptions? options = null);

    /// <summary>
    /// Reads weather data from the specified file path.
    /// </summary>
    /// <param name="path">Path to the file to read.</param>
    /// <param name="options">
    /// Reader options. When <c>null</c>, <see cref="WeatherReadOptions.Default"/>
    /// is used.
    /// </param>
    /// <returns>A fully-populated <see cref="WeatherData"/>.</returns>
    WeatherData Read(string path, WeatherReadOptions? options = null);
  }
}
