/* Ground.cs
 *
 * Copyright (C) 2021 E.Togashi
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

namespace Popolo.Core.Climate
{
  /// <summary>
  /// Provides ground temperature calculations based on annual outdoor temperature statistics.
  /// </summary>
  public class Ground
  {

    #region プロパティ

    /// <summary>Gets the day of year on which the outdoor temperature peaks.</summary>
    public int PeakDayOfYear { get; private set; }

    /// <summary>Gets the annual temperature range (max - min) [°C].</summary>
    public double AnnualTemperatureRange { get; private set; }

    /// <summary>Gets the annual mean outdoor temperature [°C].</summary>
    public double AnnualAverageTemperature { get; private set; }

    #endregion

    #region コンストラクタ

    /// <summary>
    /// Initializes a new instance with the specified annual temperature statistics.
    /// </summary>
    /// <param name="peakDayOfYear">Day of year on which the outdoor temperature peaks.</param>
    /// <param name="annualTemperatureRange">Annual temperature range (max - min) [°C].</param>
    /// <param name="annualAverageTemperature">Annual mean outdoor temperature [°C].</param>
    public Ground(
        int peakDayOfYear,
        double annualTemperatureRange,
        double annualAverageTemperature)
    {
      PeakDayOfYear = peakDayOfYear;
      AnnualTemperatureRange = annualTemperatureRange;
      AnnualAverageTemperature = annualAverageTemperature;
    }

    #endregion

    #region インスタンスメソッド

    /// <summary>
    /// Gets the ground temperature [°C] at the specified depth and day of year.
    /// </summary>
    /// <param name="dayOfYear">Day of year for the calculation.</param>
    /// <param name="depth">Depth below the ground surface [m].</param>
    /// <returns>Ground temperature [°C]</returns>
    public double GetTemperature(int dayOfYear, double depth)
    {
      return GetTemperature(
          PeakDayOfYear, AnnualTemperatureRange, AnnualAverageTemperature,
          dayOfYear, depth);
    }

    #endregion

    #region 静的メソッド

    /// <summary>
    /// Gets the ground temperature [°C] at the specified depth and day of year.
    /// </summary>
    /// <param name="peakDayOfYear">Day of year on which the outdoor temperature peaks.</param>
    /// <param name="annualTemperatureRange">Annual temperature range (max - min) [°C].</param>
    /// <param name="annualAverageTemperature">Annual mean outdoor temperature [°C].</param>
    /// <param name="dayOfYear">Day of year for the calculation.</param>
    /// <param name="depth">Depth below the ground surface [m].</param>
    /// <returns>Ground temperature [°C]</returns>
    public static double GetTemperature(
        int peakDayOfYear,
        double annualTemperatureRange,
        double annualAverageTemperature,
        int dayOfYear,
        double depth)
    {
      return annualAverageTemperature
          + 0.5 * annualTemperatureRange
          * Math.Exp(-0.526 * depth)
          * Math.Cos((dayOfYear - peakDayOfYear - 30.556 * depth)
              / 365.0 * 2.0 * Math.PI);
    }

    #endregion

  }
}
