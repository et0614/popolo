/* IReadOnlyWeatherData.cs
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

using System;
using System.Collections.Generic;

namespace Popolo.Core.Climate.Weather
{
  /// <summary>
  /// Read-only view of a collection of weather records together with their
  /// station information and metadata.
  /// </summary>
  public interface IReadOnlyWeatherData
  {
    /// <summary>Gets station (location) information for these records.</summary>
    WeatherStationInfo Station { get; }

    /// <summary>Gets the provenance of this dataset.</summary>
    WeatherDataSource Source { get; }

    /// <summary>
    /// Gets the nominal sampling interval of the dataset, or <c>null</c>
    /// when the data is recorded at non-uniform intervals. This is a hint
    /// for consumers and is not enforced.
    /// </summary>
    TimeSpan? NominalInterval { get; }

    /// <summary>
    /// Gets a value indicating whether this dataset is a synthesised typical
    /// meteorological year (TMY), in which each record's
    /// <see cref="WeatherRecord.SourceTime"/> may originate from a different
    /// year than its logical <see cref="WeatherRecord.Time"/>.
    /// </summary>
    bool IsTypicalYear { get; }

    /// <summary>
    /// Gets the time-ordered list of records.
    /// Implementations guarantee that records are in non-decreasing order of
    /// <see cref="WeatherRecord.Time"/>.
    /// </summary>
    IReadOnlyList<WeatherRecord> Records { get; }

    /// <summary>Gets the number of records in <see cref="Records"/>.</summary>
    int Count { get; }
  }
}
