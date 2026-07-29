/* BoundaryInterpolator.cs
 *
 * Copyright (C) 2025 E.Togashi
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

namespace Popolo.Core.Utilities
{
  /// <summary>
  /// Interpolates boundary condition values over time. The interpolation
  /// method is selectable per series: simple linear interpolation, step hold
  /// (previous value), or PCHIP (Piecewise Cubic Hermite Interpolating
  /// Polynomial), which guarantees monotonicity of the interpolated values.
  /// The default is <see cref="InterpolationMethod.Pchip"/>.
  /// </summary>
  public class BoundaryInterpolator
  {

    #region Enumerations

    /// <summary>Interpolation method applied between data nodes.</summary>
    public enum InterpolationMethod
    {
      /// <summary>Simple linear interpolation.</summary>
      Linear,
      /// <summary>Holds the value of the previous node (step function).</summary>
      StepHold,
      /// <summary>Monotonicity-preserving piecewise cubic Hermite interpolation.</summary>
      Pchip,
    }

    #endregion

    #region Instance variables and properties

    /// <summary>Array of date-time values used for interpolation.</summary>
    private readonly DateTime[] _dTimes;

    /// <summary>Per-series arrays of state values.</summary>
    private readonly List<double[]> _seriesValues;

    /// <summary>Per-series interpolation methods.</summary>
    private readonly List<InterpolationMethod> _seriesMethods;

    /// <summary>Gets the number of data nodes.</summary>
    public int NodeCount => _dTimes.Length;

    /// <summary>Gets the number of data series.</summary>
    public int SeriesCount => _seriesValues.Count;

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new instance with the specified date-time array and no series data.
    /// </summary>
    /// <param name="dateTimes">Array of date-time values used for interpolation.</param>
    /// <exception cref="PopoloArgumentException">
    /// Thrown when the date-time array is not sorted in ascending order.
    /// </exception>
    public BoundaryInterpolator(DateTime[] dateTimes)
        : this(dateTimes, new List<double[]>()) { }

    /// <summary>
    /// Initializes a new instance with the specified date-time array and series data.
    /// </summary>
    /// <param name="dateTimes">Array of date-time values used for interpolation.</param>
    /// <param name="boundaryValues">List of value arrays, one per series.</param>
    /// <exception cref="PopoloArgumentException">
    /// Thrown when the date-time array is not sorted, or when any series length
    /// does not match the number of nodes.
    /// </exception>
    public BoundaryInterpolator(DateTime[] dateTimes, List<double[]> boundaryValues)
    {
      _dTimes = dateTimes;

      //Check sort order
      for (int i = 1; i < dateTimes.Length; i++)
        if (dateTimes[i] < dateTimes[i - 1])
          throw new PopoloArgumentException(
              "DateTime array must be sorted in ascending order.", 
              nameof(dateTimes));

      //Check data counts
      for (int i = 0; i < boundaryValues.Count; i++)
        if (boundaryValues[i].Length != NodeCount)
          throw new PopoloArgumentException(
              $"Series[{i}]: length {boundaryValues[i].Length} "
              + $"does not match NodeCount ({NodeCount}).",
              nameof(boundaryValues));

      _seriesValues = boundaryValues;
      _seriesMethods = new List<InterpolationMethod>();
      for (int i = 0; i < boundaryValues.Count; i++)
        _seriesMethods.Add(InterpolationMethod.Pchip);
    }

    #endregion

    #region Instance methods

    /// <summary>
    /// Returns the interpolated value for the specified date-time and series index.
    /// </summary>
    /// <param name="dateTime">The date-time at which to interpolate.</param>
    /// <param name="seriesIndex">The index of the series to interpolate.</param>
    /// <returns>Interpolated value.</returns>
    public double Interpolate(DateTime dateTime, int seriesIndex)
    {
      int iIndex = GetIntervalIndex(dateTime);
      if (iIndex < 0) return _seriesValues[seriesIndex][0];
      if (iIndex >= NodeCount) return _seriesValues[seriesIndex][NodeCount - 1];

      switch (_seriesMethods[seriesIndex])
      {
        case InterpolationMethod.StepHold:
          //GetIntervalIndex maps an exact node hit to the preceding interval;
          //at the node itself the node's own value must be returned
          return dateTime == _dTimes[iIndex + 1]
            ? _seriesValues[seriesIndex][iIndex + 1]
            : _seriesValues[seriesIndex][iIndex];

        case InterpolationMethod.Linear:
          {
            double h = _dTimes[iIndex + 1].Ticks - _dTimes[iIndex].Ticks;
            double t = (dateTime.Ticks - _dTimes[iIndex].Ticks) / h;
            return (1 - t) * _seriesValues[seriesIndex][iIndex]
                 + t * _seriesValues[seriesIndex][iIndex + 1];
          }

        default:
          {
            double[] slps = ComputeSlopes(iIndex, seriesIndex);

            double h = _dTimes[iIndex + 1].Ticks - _dTimes[iIndex].Ticks;
            double t = (dateTime.Ticks - _dTimes[iIndex].Ticks) / h;
            double h00 = (1 + 2 * t) * (1 - t) * (1 - t);
            double h10 = t * (1 - t) * (1 - t);
            double h01 = t * t * (3 - 2 * t);
            double h11 = t * t * (t - 1);

            return h00 * _seriesValues[seriesIndex][iIndex]
                 + h10 * h * slps[0]
                 + h01 * _seriesValues[seriesIndex][iIndex + 1]
                 + h11 * h * slps[1];
          }
      }
    }

    /// <summary>
    /// Returns the interpolated values for all series at the specified date-time.
    /// </summary>
    /// <param name="dateTime">The date-time at which to interpolate.</param>
    /// <returns>Array of interpolated values, one per series.</returns>
    public double[] InterpolateAllSeries(DateTime dateTime)
    {
      var values = new double[SeriesCount];
      for (int i = 0; i < SeriesCount; i++)
        values[i] = Interpolate(dateTime, i);
      return values;
    }

    /// <summary>
    /// Adds a new series of values interpolated with
    /// <see cref="InterpolationMethod.Pchip"/>.
    /// </summary>
    /// <param name="sValues">Array of values with length equal to <see cref="NodeCount"/>.</param>
    /// <exception cref="PopoloArgumentException">
    /// Thrown when the length of <paramref name="sValues"/> does not match
    /// <see cref="NodeCount"/>.
    /// </exception>
    public void AddSeries(double[] sValues)
    {
      AddSeries(sValues, InterpolationMethod.Pchip);
    }

    /// <summary>
    /// Adds a new series of values with the specified interpolation method.
    /// </summary>
    /// <param name="sValues">Array of values with length equal to <see cref="NodeCount"/>.</param>
    /// <param name="method">Interpolation method applied to this series.</param>
    /// <exception cref="PopoloArgumentException">
    /// Thrown when the length of <paramref name="sValues"/> does not match
    /// <see cref="NodeCount"/>.
    /// </exception>
    public void AddSeries(double[] sValues, InterpolationMethod method)
    {
      if (sValues.Length != NodeCount)
        throw new PopoloArgumentException(
            $"Length {sValues.Length} does not match NodeCount ({NodeCount}).",
            nameof(sValues));
      _seriesValues.Add(sValues);
      _seriesMethods.Add(method);
    }

    /// <summary>
    /// Gets the interpolation method of the specified series.
    /// </summary>
    /// <param name="seriesIndex">The index of the series.</param>
    /// <returns>Interpolation method of the series.</returns>
    /// <exception cref="PopoloArgumentException">
    /// Thrown when <paramref name="seriesIndex"/> is out of range.
    /// </exception>
    public InterpolationMethod GetInterpolationMethod(int seriesIndex)
    {
      if (seriesIndex < 0 || SeriesCount <= seriesIndex)
        throw new PopoloArgumentException(
            $"seriesIndex {seriesIndex} is out of range (SeriesCount = {SeriesCount}).",
            nameof(seriesIndex));
      return _seriesMethods[seriesIndex];
    }

    /// <summary>
    /// Sets the interpolation method of the specified series.
    /// </summary>
    /// <param name="seriesIndex">The index of the series.</param>
    /// <param name="method">Interpolation method applied to the series.</param>
    /// <exception cref="PopoloArgumentException">
    /// Thrown when <paramref name="seriesIndex"/> is out of range.
    /// </exception>
    public void SetInterpolationMethod(int seriesIndex, InterpolationMethod method)
    {
      if (seriesIndex < 0 || SeriesCount <= seriesIndex)
        throw new PopoloArgumentException(
            $"seriesIndex {seriesIndex} is out of range (SeriesCount = {SeriesCount}).",
            nameof(seriesIndex));
      _seriesMethods[seriesIndex] = method;
    }

    /// <summary>
    /// Returns the index i such that dTimes[i] &lt;= dateTime &lt; dTimes[i+1].
    /// Returns -1 if before the first node, NodeCount if after the last node.
    /// </summary>
    private int GetIntervalIndex(DateTime dateTime)
    {
      int pos = Array.BinarySearch(_dTimes, dateTime);
      if (0 <= pos) return Math.Max(0, pos - 1);

      int insertIndex = ~pos;
      if (insertIndex == 0) return -1;
      if (insertIndex >= _dTimes.Length) return _dTimes.Length;
      return insertIndex - 1;
    }

    /// <summary>Computes the slopes at both ends of an interval for PCHIP interpolation.</summary>
    private double[] ComputeSlopes(int intervalIndex, int seriesIndex)
    {
      bool isLeftEnd = intervalIndex == 0;
      bool isRightEnd = intervalIndex == NodeCount - 2;

      double[] h = new double[3];
      double[] delta = new double[3];
      for (int i = 0; i < 3; i++)
      {
        int tIndex = intervalIndex - 1 + i;
        if (!((i == 0 && isLeftEnd) || (i == 2 && isRightEnd)))
        {
          h[i] = _dTimes[tIndex + 1].Ticks - _dTimes[tIndex].Ticks;
          delta[i] = (_seriesValues[seriesIndex][tIndex + 1]
                    - _seriesValues[seriesIndex][tIndex]) / h[i];
        }
      }

      double[] d = new double[2];
      for (int i = 0; i < 2; i++)
      {
        if ((i == 0 && isLeftEnd) || (i == 1 && isRightEnd))
          d[i] = delta[1];
        else if (0 < delta[i] * delta[i + 1])
        {
          double w1 = 2 * h[i + 1] + h[i];
          double w2 = h[i + 1] + 2 * h[i];
          d[i] = (w1 + w2) / (w1 / delta[i] + w2 / delta[i + 1]);
        }
        else
          d[i] = 0;
      }
      return d;
    }

    #endregion

  }
}
