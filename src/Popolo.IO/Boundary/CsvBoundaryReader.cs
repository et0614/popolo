/* CsvBoundaryReader.cs
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
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301, USA.
 */

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

using Popolo.Core.Exceptions;
using Popolo.Core.Utilities;

namespace Popolo.IO.Boundary
{
  /// <summary>
  /// Reads time series boundary condition CSV files (occupancy, internal
  /// heat gains, setpoints, etc.) into a <see cref="BoundaryInterpolator"/>.
  /// </summary>
  /// <remarks>
  /// <para>
  /// File format: lines starting with <c>#</c> are comments and blank lines
  /// are ignored. The first non-comment line is the header; its first column
  /// names the time column (the name itself is not interpreted) and the
  /// remaining columns name the data series. Each data row consists of a
  /// date-time followed by one numeric value per series. Date-times must be
  /// in ascending order.
  /// </para>
  /// <para>
  /// Values are parsed with <see cref="CultureInfo.InvariantCulture"/>.
  /// Date-times accept ISO 8601 and other formats recognized by
  /// <see cref="DateTime.Parse(string, IFormatProvider)"/> with the
  /// invariant culture (e.g. <c>2026-01-01T00:00:00</c> or
  /// <c>2026-01-01 00:00:00</c>).
  /// </para>
  /// <para>
  /// Unless specified otherwise, every series is interpolated with
  /// <see cref="BoundaryInterpolator.InterpolationMethod.Linear"/>: boundary
  /// schedules are typically hand-edited data for which simple linear
  /// interpolation is the safest default. Use
  /// <see cref="BoundaryInterpolator.SetInterpolationMethod"/> to switch
  /// individual series (e.g. to
  /// <see cref="BoundaryInterpolator.InterpolationMethod.StepHold"/> for
  /// stepwise schedules such as occupant counts).
  /// </para>
  /// </remarks>
  public class CsvBoundaryReader
  {

    #region Static methods

    /// <summary>
    /// Returns a sample boundary condition CSV as a string.
    /// </summary>
    /// <returns>Sample CSV text readable by <see cref="Read(Stream, out string[])"/>.</returns>
    /// <remarks>
    /// The sample describes a typical office day (hourly occupant count,
    /// lighting and equipment heat gains) and starts with comment lines that
    /// document the file format. Write it to a file to obtain an editable
    /// template:
    /// <code>File.WriteAllText("boundary.csv", CsvBoundaryReader.GetSampleCsv());</code>
    /// </remarks>
    public static string GetSampleCsv()
    {
      var sb = new System.Text.StringBuilder();
      sb.AppendLine("# Boundary condition schedule sample (edit values as needed).");
      sb.AppendLine("# Lines starting with '#' are comments and blank lines are ignored.");
      sb.AppendLine("# The first non-comment line is the header: the first column is the");
      sb.AppendLine("# date-time and the remaining columns are named data series.");
      sb.AppendLine("# Date-times must be in ascending order (ISO 8601 recommended).");
      sb.AppendLine("# Values use '.' as the decimal separator.");
      sb.AppendLine("time,occupants,lighting,equipment");

      //Hourly office-day profile: occupants [-], lighting [kW], equipment [kW]
      double[,] rows =
      {
        {  0, 0.0,  0.5,  2.0 }, {  1, 0.0,  0.5,  2.0 }, {  2, 0.0,  0.5,  2.0 },
        {  3, 0.0,  0.5,  2.0 }, {  4, 0.0,  0.5,  2.0 }, {  5, 0.0,  0.5,  2.0 },
        {  6, 0.0,  0.5,  2.0 }, {  7, 5.0,  2.0,  3.0 }, {  8, 30.0, 8.0,  8.0 },
        {  9, 50.0, 10.0, 12.0 }, { 10, 50.0, 10.0, 12.0 }, { 11, 50.0, 10.0, 12.0 },
        { 12, 25.0, 6.0,  8.0 }, { 13, 50.0, 10.0, 12.0 }, { 14, 50.0, 10.0, 12.0 },
        { 15, 50.0, 10.0, 12.0 }, { 16, 50.0, 10.0, 12.0 }, { 17, 50.0, 10.0, 12.0 },
        { 18, 30.0, 8.0,  8.0 }, { 19, 15.0, 5.0,  5.0 }, { 20, 5.0,  2.0,  3.0 },
        { 21, 0.0,  1.0,  2.0 }, { 22, 0.0,  0.5,  2.0 }, { 23, 0.0,  0.5,  2.0 },
        { 24, 0.0,  0.5,  2.0 },
      };
      var start = new DateTime(2026, 1, 1, 0, 0, 0);
      for (int i = 0; i < rows.GetLength(0); i++)
      {
        DateTime dt = start.AddHours(rows[i, 0]);
        sb.Append(dt.ToString("s", CultureInfo.InvariantCulture));
        for (int j = 1; j < 4; j++)
          sb.Append(',').Append(rows[i, j].ToString(CultureInfo.InvariantCulture));
        sb.AppendLine();
      }
      return sb.ToString();
    }

    #endregion

    #region Instance methods

    /// <summary>
    /// Reads a boundary condition CSV file with linear interpolation.
    /// </summary>
    /// <param name="path">Path of the CSV file.</param>
    /// <param name="seriesNames">Names of the data series taken from the header.</param>
    /// <returns>Interpolator loaded with the file contents.</returns>
    public BoundaryInterpolator Read(string path, out string[] seriesNames)
    {
      return Read(path, BoundaryInterpolator.InterpolationMethod.Linear, out seriesNames);
    }

    /// <summary>
    /// Reads a boundary condition CSV file with the specified default
    /// interpolation method.
    /// </summary>
    /// <param name="path">Path of the CSV file.</param>
    /// <param name="defaultMethod">Interpolation method applied to every series.</param>
    /// <param name="seriesNames">Names of the data series taken from the header.</param>
    /// <returns>Interpolator loaded with the file contents.</returns>
    /// <exception cref="PopoloArgumentException">
    /// Thrown when <paramref name="path"/> is null or empty.
    /// </exception>
    public BoundaryInterpolator Read(
      string path, BoundaryInterpolator.InterpolationMethod defaultMethod, out string[] seriesNames)
    {
      if (string.IsNullOrEmpty(path))
        throw new PopoloArgumentException("path must not be null or empty.", nameof(path));

      using (FileStream fs = File.OpenRead(path))
        return Read(fs, defaultMethod, out seriesNames);
    }

    /// <summary>
    /// Reads a boundary condition CSV stream with linear interpolation.
    /// </summary>
    /// <param name="stream">Stream of the CSV data.</param>
    /// <param name="seriesNames">Names of the data series taken from the header.</param>
    /// <returns>Interpolator loaded with the stream contents.</returns>
    public BoundaryInterpolator Read(Stream stream, out string[] seriesNames)
    {
      return Read(stream, BoundaryInterpolator.InterpolationMethod.Linear, out seriesNames);
    }

    /// <summary>
    /// Reads a boundary condition CSV stream with the specified default
    /// interpolation method.
    /// </summary>
    /// <param name="stream">Stream of the CSV data.</param>
    /// <param name="defaultMethod">Interpolation method applied to every series.</param>
    /// <param name="seriesNames">Names of the data series taken from the header.</param>
    /// <returns>Interpolator loaded with the stream contents.</returns>
    /// <exception cref="PopoloArgumentException">
    /// Thrown when <paramref name="stream"/> is null, when the header or data
    /// rows are missing, when a row has an unexpected number of columns, or
    /// when a date-time or numeric value cannot be parsed.
    /// </exception>
    public BoundaryInterpolator Read(
      Stream stream, BoundaryInterpolator.InterpolationMethod defaultMethod, out string[] seriesNames)
    {
      if (stream == null)
        throw new PopoloArgumentException("stream must not be null.", nameof(stream));

      string[]? header = null;
      var dTimes = new List<DateTime>();
      var rows = new List<double[]>();

      using (var sr = new StreamReader(stream))
      {
        string? line;
        int lineNumber = 0;
        while ((line = sr.ReadLine()) != null)
        {
          lineNumber++;
          if (line.Length == 0 || string.IsNullOrWhiteSpace(line)) continue;
          if (line.StartsWith("#", StringComparison.Ordinal)) continue;

          string[] cells = line.Split(',');
          if (header == null)
          {
            if (cells.Length < 2)
              throw new PopoloArgumentException(
                $"Line {lineNumber}: the header must contain a time column and at least one series column.",
                nameof(stream));
            header = cells;
            continue;
          }

          if (cells.Length != header.Length)
            throw new PopoloArgumentException(
              $"Line {lineNumber}: expected {header.Length} columns but found {cells.Length}.",
              nameof(stream));

          if (!DateTime.TryParse(cells[0].Trim(), CultureInfo.InvariantCulture,
            DateTimeStyles.None, out DateTime dTime))
            throw new PopoloArgumentException(
              $"Line {lineNumber}: cannot parse date-time '{cells[0]}'.", nameof(stream));

          var values = new double[header.Length - 1];
          for (int i = 0; i < values.Length; i++)
          {
            if (!double.TryParse(cells[i + 1].Trim(), NumberStyles.Float,
              CultureInfo.InvariantCulture, out values[i]))
              throw new PopoloArgumentException(
                $"Line {lineNumber}: cannot parse value '{cells[i + 1]}' of series '{header[i + 1].Trim()}'.",
                nameof(stream));
          }

          dTimes.Add(dTime);
          rows.Add(values);
        }
      }

      if (header == null)
        throw new PopoloArgumentException("The CSV contains no header line.", nameof(stream));
      if (dTimes.Count == 0)
        throw new PopoloArgumentException("The CSV contains no data rows.", nameof(stream));

      seriesNames = new string[header.Length - 1];
      for (int i = 0; i < seriesNames.Length; i++)
        seriesNames[i] = header[i + 1].Trim();

      //Transpose rows into per-series arrays
      var interpolator = new BoundaryInterpolator(dTimes.ToArray());
      for (int s = 0; s < seriesNames.Length; s++)
      {
        var sValues = new double[dTimes.Count];
        for (int r = 0; r < rows.Count; r++) sValues[r] = rows[r][s];
        interpolator.AddSeries(sValues, defaultMethod);
      }
      return interpolator;
    }

    #endregion

  }
}
