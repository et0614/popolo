/* WeatherData.cs
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
using Popolo.Core.Exceptions;

namespace Popolo.Core.Climate.Weather
{
  /// <summary>
  /// A collection of weather observations for a single station.
  /// </summary>
  /// <remarks>
  /// <para>
  /// Records are maintained in non-decreasing order of
  /// <see cref="WeatherRecord.Time"/>. Use <see cref="Add(WeatherRecord)"/> for
  /// the streaming case where records arrive in chronological order, or
  /// <see cref="AddRangeSorted(IEnumerable{WeatherRecord})"/> to ingest an
  /// unordered collection.
  /// </para>
  /// <para>
  /// For typical-year (TMY) data, <see cref="IsTypicalYear"/> should be set to
  /// <c>true</c>. The logical time axis (<see cref="WeatherRecord.Time"/>)
  /// remains monotonic; the underlying observation year is preserved in
  /// <see cref="WeatherRecord.SourceTime"/>.
  /// </para>
  /// </remarks>
  public class WeatherData : IReadOnlyWeatherData
  {

    #region インスタンス変数・プロパティ

    private readonly List<WeatherRecord> _records;

    /// <inheritdoc />
    public WeatherStationInfo Station { get; set; }

    /// <inheritdoc />
    public WeatherDataSource Source { get; set; } = WeatherDataSource.Unknown;

    /// <inheritdoc />
    public TimeSpan? NominalInterval { get; set; }

    /// <inheritdoc />
    public bool IsTypicalYear { get; set; }

    /// <inheritdoc />
    public IReadOnlyList<WeatherRecord> Records => _records;

    /// <inheritdoc />
    public int Count => _records.Count;

    #endregion

    #region コンストラクタ

    /// <summary>Initializes an empty dataset.</summary>
    public WeatherData()
    {
      _records = new List<WeatherRecord>();
    }

    /// <summary>
    /// Initializes an empty dataset with the specified station information and
    /// source.
    /// </summary>
    public WeatherData(WeatherStationInfo station, WeatherDataSource source)
        : this()
    {
      Station = station;
      Source = source;
    }

    #endregion

    #region インスタンスメソッド

    /// <summary>
    /// Appends a record. The record's <see cref="WeatherRecord.Time"/> must be
    /// greater than or equal to the time of the last existing record.
    /// </summary>
    /// <param name="record">The record to append.</param>
    /// <exception cref="PopoloArgumentException">
    /// Thrown when the record's logical time is earlier than the time of the
    /// last record already in the dataset.
    /// </exception>
    public void Add(WeatherRecord record)
    {
      if (_records.Count > 0 && record.Time < _records[_records.Count - 1].Time)
      {
        throw new PopoloArgumentException(
            "Record time must be non-decreasing. "
            + $"Last time = {_records[_records.Count - 1].Time:O}, "
            + $"incoming = {record.Time:O}.",
            nameof(record));
      }
      _records.Add(record);
    }

    /// <summary>
    /// Appends records that are already known to be in non-decreasing order of
    /// <see cref="WeatherRecord.Time"/>.
    /// </summary>
    /// <param name="records">Records in ascending time order.</param>
    /// <exception cref="PopoloArgumentException">
    /// Thrown when <paramref name="records"/> contains an out-of-order record
    /// or a record earlier than the current tail.
    /// </exception>
    public void AddRange(IEnumerable<WeatherRecord> records)
    {
      if (records == null) throw new PopoloArgumentException(
          "records must not be null.", nameof(records));
      foreach (var r in records) Add(r);
    }

    /// <summary>
    /// Appends records in arbitrary order; the internal list is re-sorted by
    /// <see cref="WeatherRecord.Time"/> after insertion using a stable sort.
    /// </summary>
    /// <param name="records">Records in any order.</param>
    public void AddRangeSorted(IEnumerable<WeatherRecord> records)
    {
      if (records == null) throw new PopoloArgumentException(
          "records must not be null.", nameof(records));
      foreach (var r in records) _records.Add(r);
      // 安定ソート (LINQ)
      _records.Sort((a, b) => a.Time.CompareTo(b.Time));
    }

    /// <summary>Removes all records. Station, source, and metadata are preserved.</summary>
    public void Clear() => _records.Clear();

    #endregion

  }
}
