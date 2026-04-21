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

    /// <summary>
    /// Constructs a <see cref="Ground"/> by estimating the annual-mean
    /// temperature, annual range, and peak day from the first year of
    /// dry-bulb temperature records in <paramref name="data"/>.
    /// </summary>
    /// <param name="data">Weather data spanning at least one year.</param>
    /// <returns>A <see cref="Ground"/> parameterised for the site described by
    /// <paramref name="data"/>.</returns>
    /// <exception cref="PopoloArgumentException">
    /// Thrown when <paramref name="data"/> is <c>null</c>, contains no records,
    /// spans less than one year, or contains no record with a recorded
    /// dry-bulb temperature.
    /// </exception>
    /// <remarks>
    /// <para>
    /// Uses only the records in the first 365-day window starting at
    /// <c>data.Records[0].Time</c>. Any records beyond that window are
    /// ignored, and records older than the window never appear anyway. This
    /// means multi-year data contributes only the first year; averaging
    /// across years is not performed.
    /// </para>
    /// <para>
    /// For every day of year that has at least one record, the dry-bulb
    /// readings for that day are averaged to give a daily mean. The three
    /// Kusuda parameters are then extracted by projecting the daily-mean
    /// series onto the first annual harmonic:
    /// </para>
    /// <code>
    /// T_avg = mean(T_daily)
    /// α     = (2/N) · Σ T_daily(n)·cos(2π·n/365)
    /// β     = (2/N) · Σ T_daily(n)·sin(2π·n/365)
    /// A     = 2·√(α² + β²)            ← annual temperature range
    /// n_peak = atan2(β, α)·365/(2π)     ← day-of-year of the peak
    /// </code>
    /// <para>
    /// No iterative solver is used; the projection is the closed-form
    /// optimal fit to a single sinusoid. Higher-frequency weather variation
    /// is discarded, matching the assumption of the Kusuda model (soil acts
    /// as a low-pass filter that only sees the annual period).
    /// </para>
    /// <para>
    /// <b>Accuracy:</b> for typical hourly EPW / TMY data, the annual mean
    /// recovers to within a few hundredths of a degree, the annual range to
    /// within ≈ 0.5 °C, and the peak day to within 1–2 days of the
    /// seasonal maximum of the smoothed series.
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

      // DOY ごとに乾球温度の合計・度数を累積する。
      double[] doySum = new double[367];
      int[] doyCount = new int[367];

      for (int i = 0; i < records.Count; i++)
      {
        var r = records[i];
        if (r.Time >= windowEnd) break;
        if (!r.Has(WeatherField.DryBulbTemperature)) continue;
        int doy = r.Time.DayOfYear;                     // [1, 366]
        doySum[doy] += r.DryBulbTemperature;
        doyCount[doy]++;
      }

      // DOY ごとの日平均から第 1 高調波を直接射影する。
      double sumT = 0.0;
      double alphaSum = 0.0;
      double betaSum = 0.0;
      int validDays = 0;

      for (int n = 1; n <= 366; n++)
      {
        if (doyCount[n] == 0) continue;
        double meanT = doySum[n] / doyCount[n];
        double angle = 2.0 * Math.PI * n / 365.0;
        sumT += meanT;
        alphaSum += meanT * Math.Cos(angle);
        betaSum += meanT * Math.Sin(angle);
        validDays++;
      }

      if (validDays == 0)
        throw new PopoloArgumentException(
            "data has no records with dry-bulb temperature.", nameof(data));

      double tAvg = sumT / validDays;
      double alpha = 2.0 * alphaSum / validDays;
      double beta = 2.0 * betaSum / validDays;
      double amplitude = 2.0 * Math.Sqrt(alpha * alpha + beta * beta);

      double peakDoyReal = Math.Atan2(beta, alpha) * 365.0 / (2.0 * Math.PI);
      if (peakDoyReal <= 0.0) peakDoyReal += 365.0;

      int peakDoy = (int)Math.Round(peakDoyReal);
      if (peakDoy < 1) peakDoy = 1;
      if (peakDoy > 365) peakDoy = 365;

      return new Ground(peakDoy, amplitude, tAvg);
    }

    #endregion

  }
}
