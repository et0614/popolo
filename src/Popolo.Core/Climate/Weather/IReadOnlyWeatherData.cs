/* IReadOnlyWeatherData.cs
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
