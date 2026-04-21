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
  /// <para>
  /// Implementations set <see cref="WeatherData.Source"/> to the appropriate
  /// <see cref="WeatherDataSource"/> value, populate
  /// <see cref="WeatherData.Station"/> when the format carries station
  /// information, and emit records with <see cref="WeatherField"/> flags set
  /// exactly for fields that are present in the source.
  /// </para>
  /// <para>
  /// The simple <see cref="Read(Stream)"/> and <see cref="Read(string)"/>
  /// overloads read the source as-is and are the recommended entry points.
  /// The options-accepting overloads exist for callers who need the reader
  /// to derive fields that are not recorded by the source format; see
  /// <see cref="WeatherReadOptions"/>.
  /// </para>
  /// </remarks>
  public interface IWeatherDataReader
  {
    /// <summary>
    /// Reads weather data from the specified stream.
    /// </summary>
    /// <param name="stream">The stream positioned at the start of the data.</param>
    /// <returns>A fully-populated <see cref="WeatherData"/>.</returns>
    WeatherData Read(Stream stream);

    /// <summary>
    /// Reads weather data from the specified file path.
    /// </summary>
    /// <param name="path">Path to the file to read.</param>
    /// <returns>A fully-populated <see cref="WeatherData"/>.</returns>
    WeatherData Read(string path);

    /// <summary>
    /// Reads weather data from the specified stream with the given options.
    /// </summary>
    /// <param name="stream">The stream positioned at the start of the data.</param>
    /// <param name="options">
    /// Options controlling whether the reader derives values for fields that
    /// are not recorded by the source format. Must not be <c>null</c>; pass
    /// <see cref="WeatherReadOptions.Default"/> to disable all derivations.
    /// </param>
    /// <returns>A fully-populated <see cref="WeatherData"/>.</returns>
    WeatherData Read(Stream stream, WeatherReadOptions options);

    /// <summary>
    /// Reads weather data from the specified file path with the given options.
    /// </summary>
    /// <param name="path">Path to the file to read.</param>
    /// <param name="options">
    /// Options controlling whether the reader derives values for fields that
    /// are not recorded by the source format. Must not be <c>null</c>; pass
    /// <see cref="WeatherReadOptions.Default"/> to disable all derivations.
    /// </param>
    /// <returns>A fully-populated <see cref="WeatherData"/>.</returns>
    WeatherData Read(string path, WeatherReadOptions options);
  }
}
