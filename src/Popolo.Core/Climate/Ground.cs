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

using Popolo.Core.Climate.Weather;
using Popolo.Core.Exceptions;

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
  /// Four site parameters fully determine the model:
  /// </para>
  /// <list type="bullet">
  ///   <item><description><see cref="PeakDayOfYear"/> — day when the surface
  ///     temperature is warmest; conventionally the middle day of the warmest
  ///     calendar month.</description></item>
  ///   <item><description><see cref="MaxMonthlyMeanTemperature"/> — mean
  ///     outdoor temperature of the warmest calendar month.</description></item>
  ///   <item><description><see cref="MinMonthlyMeanTemperature"/> — mean
  ///     outdoor temperature of the coldest calendar month.</description></item>
  ///   <item><description><see cref="AnnualAverageTemperature"/> — yearly
  ///     mean outdoor temperature.</description></item>
  /// </list>
  /// <para>
  /// Building physics literature (Kusuda, ASHRAE Handbook) parameterises the
  /// driving sinusoid by the range between the warmest and coldest
  /// <em>monthly</em> means — not by instantaneous extremes, and not by a
  /// statistical fit of a single sinusoid. Exposing the two monthly-mean
  /// extremes directly (instead of their difference) removes the long-standing
  /// ambiguity of "annual temperature range" and makes
  /// <see cref="FromWeatherData"/> straightforward to implement.
  /// </para>
  /// <para>
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

    /// <summary>
    /// Gets the mean outdoor temperature [°C] of the warmest calendar month.
    /// </summary>
    public double MaxMonthlyMeanTemperature { get; private set; }

    /// <summary>
    /// Gets the mean outdoor temperature [°C] of the coldest calendar month.
    /// </summary>
    public double MinMonthlyMeanTemperature { get; private set; }

    /// <summary>Gets the annual mean outdoor temperature [°C].</summary>
    public double AnnualAverageTemperature { get; private set; }

    /// <summary>
    /// Gets the annual amplitude [°C] used by the Kusuda formula, defined as
    /// <see cref="MaxMonthlyMeanTemperature"/> − <see cref="MinMonthlyMeanTemperature"/>.
    /// </summary>
    public double AnnualMonthlyMeanRange
        => MaxMonthlyMeanTemperature - MinMonthlyMeanTemperature;

    #endregion

    #region コンストラクタ

    /// <summary>
    /// Initializes a new instance with the specified site temperature statistics.
    /// </summary>
    /// <param name="peakDayOfYear">Day of year when the surface temperature peaks.</param>
    /// <param name="maxMonthlyMeanTemperature">Mean outdoor temperature of the warmest month [°C].</param>
    /// <param name="minMonthlyMeanTemperature">Mean outdoor temperature of the coldest month [°C].</param>
    /// <param name="annualAverageTemperature">Annual mean outdoor temperature [°C].</param>
    public Ground(
        int peakDayOfYear,
        double maxMonthlyMeanTemperature,
        double minMonthlyMeanTemperature,
        double annualAverageTemperature)
    {
      PeakDayOfYear = peakDayOfYear;
      MaxMonthlyMeanTemperature = maxMonthlyMeanTemperature;
      MinMonthlyMeanTemperature = minMonthlyMeanTemperature;
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
          PeakDayOfYear, MaxMonthlyMeanTemperature, MinMonthlyMeanTemperature,
          AnnualAverageTemperature, dayOfYear, depth);
    }

    #endregion

    #region 静的メソッド

    /// <summary>
    /// Gets the ground temperature [°C] at the specified depth and day of year.
    /// </summary>
    /// <param name="peakDayOfYear">Day of year when the surface temperature peaks.</param>
    /// <param name="maxMonthlyMeanTemperature">Mean outdoor temperature of the warmest month [°C].</param>
    /// <param name="minMonthlyMeanTemperature">Mean outdoor temperature of the coldest month [°C].</param>
    /// <param name="annualAverageTemperature">Annual mean outdoor temperature [°C].</param>
    /// <param name="dayOfYear">Day of year for the calculation.</param>
    /// <param name="depth">Depth below the ground surface [m].</param>
    /// <returns>Ground temperature [°C]</returns>
    public static double GetTemperature(
        int peakDayOfYear,
        double maxMonthlyMeanTemperature,
        double minMonthlyMeanTemperature,
        double annualAverageTemperature,
        int dayOfYear,
        double depth)
    {
      double range = maxMonthlyMeanTemperature - minMonthlyMeanTemperature;
      return annualAverageTemperature
          + 0.5 * range
          * Math.Exp(-0.526 * depth)
          * Math.Cos((dayOfYear - peakDayOfYear - 30.556 * depth)
              / 365.0 * 2.0 * Math.PI);
    }

    /// <summary>
    /// Constructs a <see cref="Ground"/> by computing monthly-mean dry-bulb
    /// temperatures from the first year of records in <paramref name="data"/>
    /// and extracting the warmest- and coldest-month statistics.
    /// </summary>
    /// <param name="data">Weather data spanning at least one year.</param>
    /// <returns>A <see cref="Ground"/> parameterised for the site described by
    /// <paramref name="data"/>.</returns>
    /// <exception cref="PopoloArgumentException">
    /// Thrown when <paramref name="data"/> is <c>null</c>, contains no records,
    /// spans less than one year, or has at least one calendar month with no
    /// dry-bulb records in the first-year window.
    /// </exception>
    /// <remarks>
    /// <para>
    /// Uses only the records in the first 365-day window starting at
    /// <c>data.Records[0].Time</c>. Records beyond the window are ignored, so
    /// multi-year data contributes only its first year and no cross-year
    /// averaging is performed. Any 365-day window, regardless of its starting
    /// date, collects 28–31 days for each of the twelve calendar months.
    /// </para>
    /// <para>
    /// For each calendar month that has at least one record in the window,
    /// the mean of the dry-bulb readings is taken. The four site parameters
    /// are then:
    /// </para>
    /// <list type="bullet">
    ///   <item><description><see cref="MaxMonthlyMeanTemperature"/> = maximum
    ///     of the twelve monthly means.</description></item>
    ///   <item><description><see cref="MinMonthlyMeanTemperature"/> = minimum
    ///     of the twelve monthly means.</description></item>
    ///   <item><description><see cref="AnnualAverageTemperature"/> = mean of
    ///     the twelve monthly means.</description></item>
    ///   <item><description><see cref="PeakDayOfYear"/> = day-of-year of the
    ///     15th of the warmest month (e.g., Aug 15 → DOY 227 in a non-leap
    ///     year).</description></item>
    /// </list>
    /// <para>
    /// This is the standard parameterisation used by Kusuda's original work
    /// and by the ASHRAE Handbook. The monthly mean averages out daily
    /// weather variability while preserving the seasonal envelope; no
    /// harmonic fit or iterative solver is used.
    /// </para>
    /// </remarks>
    public static Ground FromWeatherData(IReadOnlyWeatherData data)
    {
      if (data == null)
        throw new PopoloArgumentException("data must not be null.", nameof(data));
      if (data.Count == 0)
        throw new PopoloArgumentException("data contains no records.", nameof(data));

      var records = data.Records;
      TimeSpan span = records[records.Count - 1].Time - records[0].Time;

      // 最初と最終レコードの時間差が (ほぼ) 1 年未満なら拒否。365 日ぴったりの
      // 時系列 (例: 00:00 Jan 1 〜 23:00 Dec 31 の 8760 本) は 364 日 23 時間の
      // スパンになるため、しきい値は 364 日に緩めている。
      if (span.TotalDays < 364.0)
        throw new PopoloArgumentException(
            "data must span at least one year. "
            + $"Actual span: {span.TotalDays:F1} days.",
            nameof(data));

      DateTime windowEnd = records[0].Time + TimeSpan.FromDays(365);

      // 各月 (1..12) の乾球温度合計と度数を累積。
      double[] monthSum = new double[13];
      int[] monthCount = new int[13];

      for (int i = 0; i < records.Count; i++)
      {
        var r = records[i];
        if (r.Time >= windowEnd) break;
        if (!r.Has(WeatherField.DryBulbTemperature)) continue;
        int month = r.Time.Month;                       // [1, 12]
        monthSum[month] += r.DryBulbTemperature;
        monthCount[month]++;
      }

      // 12 ヶ月すべてが揃っていないと月平均ベースの推定が意味を持たない。
      for (int m = 1; m <= 12; m++)
      {
        if (monthCount[m] == 0)
          throw new PopoloArgumentException(
              $"no dry-bulb records for month {m} in the first-year window.",
              nameof(data));
      }

      double[] monthlyMean = new double[13];
      for (int m = 1; m <= 12; m++)
        monthlyMean[m] = monthSum[m] / monthCount[m];

      int warmestMonth = 1;
      int coldestMonth = 1;
      double sum = 0.0;
      for (int m = 1; m <= 12; m++)
      {
        sum += monthlyMean[m];
        if (monthlyMean[m] > monthlyMean[warmestMonth]) warmestMonth = m;
        if (monthlyMean[m] < monthlyMean[coldestMonth]) coldestMonth = m;
      }

      double avg = sum / 12.0;
      double maxMonthly = monthlyMean[warmestMonth];
      double minMonthly = monthlyMean[coldestMonth];

      // 最暖月の 15 日の DOY を peak day として採用 (基準年は非うるう年で固定)。
      int peakDoy = new DateTime(2001, warmestMonth, 15).DayOfYear;

      return new Ground(peakDoy, maxMonthly, minMonthly, avg);
    }

    #endregion

  }
}
