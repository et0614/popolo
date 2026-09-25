/* IWeatherDataReader.cs
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
