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
  /// Represents the soil body adjacent to a building and provides ground
  /// temperature estimates at arbitrary depth and day of year.
  /// </summary>
  /// <remarks>
  /// <para>
  /// The soil is modeled as a semi-infinite homogeneous medium excited at the
  /// surface by the annual sinusoidal swing of the outdoor temperature. The
  /// resulting temperature profile at depth is a damped, phase-lagged cosine:
  /// the amplitude decays exponentially with depth, while the peak day shifts
  /// later because heat needs time to propagate downward.
  /// </para>
  /// <para>
  /// Three site parameters fully determine the model:
  /// <list type="bullet">
  ///   <item><description><see cref="PeakDayOfYear"/> — the day when the outdoor temperature is highest (typically mid-summer in the Northern Hemisphere).</description></item>
  ///   <item><description><see cref="AnnualTemperatureRange"/> — the swing between the warmest and coldest daily means.</description></item>
  ///   <item><description><see cref="AnnualAverageTemperature"/> — the yearly mean of the outdoor temperature.</description></item>
  /// </list>
  /// Soil thermal diffusivity is not an explicit parameter; a representative
  /// value (approximately 8 × 10⁻⁷ m²/s, typical of damp soil) is baked into
  /// the damping coefficient and the depth–phase-shift coefficient.
  /// </para>
  /// <para>
  /// In the building thermal model, <see cref="Ground"/> supplies the driving
  /// temperature for wall surfaces registered as ground-contact boundaries via
  /// <see cref="Building.Envelope.GroundWallReference"/> — for example,
  /// basement walls or slab-on-grade floors. Day-by-day, the wall's
  /// ground-facing side sees a quasi-steady temperature computed from this
  /// model rather than the outdoor air temperature.
  /// </para>
  /// </remarks>
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
