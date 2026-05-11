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
 *
 * References:
 *   Watanabe, Y., "Ground Temperatures for Heating and Cooling Design"
 *     (暖冷房設計用地中温度),
 *     Trans. of SHASE Japan, Vol. 38, No. 2, Feb. 1964, pp. 23-32.
 *     — origin of the damped cosine formula used by GetTemperature and
 *     of the numeric constants 0.526 (depth damping) and 30.556
 *     (depth-phase-lag in days per metre).
 *
 *   ANSI/ASHRAE Standard 140 (BESTEST), Case 990.
 *     — convention used by FromWeatherData for the annual temperature
 *     range: max over calendar months of (monthly mean of daily
 *     maximum) minus min over months of (monthly mean of daily
 *     minimum), and the peak day as the day of the highest daily mean.
 */

using System;

using Popolo.Core.Climate.Weather;
using Popolo.Core.Exceptions;

namespace Popolo.Core.Climate
{
  /// <summary>
  /// Represents the soil body adjacent to a building and provides ground
  /// temperature estimates at arbitrary depth and day of year, following
  /// the damped-cosine model of Watanabe (1964).
  /// </summary>
  /// <remarks>
  /// <para>
  /// The soil is modeled as a semi-infinite homogeneous medium excited at the
  /// surface by the annual sinusoidal swing of the outdoor temperature. The
  /// resulting temperature profile at depth is a damped, phase-lagged cosine:
  /// the amplitude decays exponentially with depth, while the peak day shifts
  /// later because heat needs time to propagate downward. Soil thermal
  /// diffusivity is not an explicit parameter; a representative value
  /// (approximately 8 × 10⁻⁷ m²/s, typical of damp soil) is baked into the
  /// damping coefficient (0.526) and the depth–phase-shift coefficient
  /// (30.556), as originally tabulated in Watanabe (1964).
  /// </para>
  /// <para>
  /// Four site parameters fully determine the model:
  /// </para>
  /// <list type="bullet">
  ///   <item><description><see cref="PeakDayOfYear"/> — day when the driving
  ///     surface temperature is warmest.</description></item>
  ///   <item><description><see cref="WarmestMonthlyMeanDailyMax"/> — for the
  ///     calendar month with the highest mean of daily-maximum dry-bulb
  ///     temperature, that monthly mean.</description></item>
  ///   <item><description><see cref="ColdestMonthlyMeanDailyMin"/> — for the
  ///     calendar month with the lowest mean of daily-minimum dry-bulb
  ///     temperature, that monthly mean.</description></item>
  ///   <item><description><see cref="AnnualAverageTemperature"/> — yearly
  ///     mean outdoor temperature.</description></item>
  /// </list>
  /// <para>
  /// The difference <see cref="WarmestMonthlyMeanDailyMax"/> −
  /// <see cref="ColdestMonthlyMeanDailyMin"/> is the annual temperature
  /// range that drives the cosine in the ground-temperature formula. This
  /// definition follows the BESTEST (ANSI/ASHRAE Standard 140) Case 990
  /// convention: for each day the daily high and daily low are taken, those
  /// are averaged per calendar month, and the warmest / coldest months'
  /// values give the two extremes. It is <em>not</em> simply the max / min
  /// of monthly means of all hourly readings — that narrower definition
  /// under-estimates the seasonal swing experienced at the surface.
  /// </para>
  /// <para>
  /// In the building thermal model, <see cref="Ground"/> supplies the driving
  /// temperature for wall surfaces registered as ground-contact boundaries via
  /// <see cref="Building.Envelope.GroundWallReference"/> — for example,
  /// basement walls or slab-on-grade floors. Day-by-day, the wall's
  /// ground-facing side sees a quasi-steady temperature computed from this
  /// model rather than the outdoor air temperature.
  /// </para>
  /// <para>
  /// <b>Primary reference:</b> Watanabe, Y., "Ground Temperatures for
  /// Heating and Cooling Design" (暖冷房設計用地中温度), Trans. of SHASE
  /// Japan, Vol. 38, No. 2, Feb. 1964, pp. 23-32.
  /// </para>
  /// </remarks>
  public class Ground
  {

    #region プロパティ

    /// <summary>Gets the day of year on which the surface temperature peaks.</summary>
    public int PeakDayOfYear { get; private set; }

    /// <summary>
    /// Gets, for the calendar month with the highest mean of daily-maximum
    /// dry-bulb temperature, that monthly mean [°C]. Forms the upper extreme
    /// of the annual temperature range driving the Watanabe (1964) formula.
    /// </summary>
    public double WarmestMonthlyMeanDailyMax { get; private set; }

    /// <summary>
    /// Gets, for the calendar month with the lowest mean of daily-minimum
    /// dry-bulb temperature, that monthly mean [°C]. Forms the lower extreme
    /// of the annual temperature range driving the Watanabe (1964) formula.
    /// </summary>
    public double ColdestMonthlyMeanDailyMin { get; private set; }

    /// <summary>Gets the annual mean outdoor temperature [°C].</summary>
    public double AnnualAverageTemperature { get; private set; }

    /// <summary>
    /// Gets the annual temperature range [°C] used by the Watanabe formula,
    /// defined as <see cref="WarmestMonthlyMeanDailyMax"/> −
    /// <see cref="ColdestMonthlyMeanDailyMin"/>.
    /// </summary>
    public double AnnualTemperatureRange
        => WarmestMonthlyMeanDailyMax - ColdestMonthlyMeanDailyMin;

    #endregion

    #region コンストラクタ

    /// <summary>
    /// Initializes a new instance with the specified site temperature statistics.
    /// </summary>
    /// <param name="peakDayOfYear">Day of year when the surface temperature peaks.</param>
    /// <param name="warmestMonthlyMeanDailyMax">
    /// Monthly mean of daily-maximum dry-bulb temperature for the warmest
    /// month [°C]; see <see cref="WarmestMonthlyMeanDailyMax"/>.
    /// </param>
    /// <param name="coldestMonthlyMeanDailyMin">
    /// Monthly mean of daily-minimum dry-bulb temperature for the coldest
    /// month [°C]; see <see cref="ColdestMonthlyMeanDailyMin"/>.
    /// </param>
    /// <param name="annualAverageTemperature">Annual mean outdoor temperature [°C].</param>
    public Ground(
        int peakDayOfYear,
        double warmestMonthlyMeanDailyMax,
        double coldestMonthlyMeanDailyMin,
        double annualAverageTemperature)
    {
      PeakDayOfYear = peakDayOfYear;
      WarmestMonthlyMeanDailyMax = warmestMonthlyMeanDailyMax;
      ColdestMonthlyMeanDailyMin = coldestMonthlyMeanDailyMin;
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
          PeakDayOfYear, WarmestMonthlyMeanDailyMax, ColdestMonthlyMeanDailyMin,
          AnnualAverageTemperature, dayOfYear, depth);
    }

    #endregion

    #region 静的メソッド

    /// <summary>
    /// Gets the ground temperature [°C] at the specified depth and day of
    /// year using the Watanabe (1964) damped-cosine formula.
    /// </summary>
    /// <param name="peakDayOfYear">Day of year when the surface temperature peaks.</param>
    /// <param name="warmestMonthlyMeanDailyMax">
    /// Monthly mean of daily-maximum dry-bulb temperature for the warmest
    /// month [°C].
    /// </param>
    /// <param name="coldestMonthlyMeanDailyMin">
    /// Monthly mean of daily-minimum dry-bulb temperature for the coldest
    /// month [°C].
    /// </param>
    /// <param name="annualAverageTemperature">Annual mean outdoor temperature [°C].</param>
    /// <param name="dayOfYear">Day of year for the calculation.</param>
    /// <param name="depth">Depth below the ground surface [m].</param>
    /// <returns>Ground temperature [°C]</returns>
    public static double GetTemperature(
        int peakDayOfYear,
        double warmestMonthlyMeanDailyMax,
        double coldestMonthlyMeanDailyMin,
        double annualAverageTemperature,
        int dayOfYear,
        double depth)
    {
      double range = warmestMonthlyMeanDailyMax - coldestMonthlyMeanDailyMin;
      return annualAverageTemperature
          + 0.5 * range
          * Math.Exp(-0.526 * depth)
          * Math.Cos((dayOfYear - peakDayOfYear - 30.556 * depth)
              / 365.0 * 2.0 * Math.PI);
    }

    /// <summary>
    /// Constructs a <see cref="Ground"/> from the first year of records in
    /// <paramref name="data"/>, following the BESTEST (ANSI/ASHRAE Standard
    /// 140) Case 990 procedure for extracting the Watanabe (1964) ground
    /// temperature model's parameters.
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
    /// The algorithm, matching BESTEST Case 990:
    /// </para>
    /// <list type="number">
    ///   <item><description>For each calendar day, take the daily-maximum
    ///     and daily-minimum dry-bulb from the hourly records of that day.
    ///   </description></item>
    ///   <item><description>For each month, average the daily maxima and
    ///     daily minima separately: <c>MonthlyMeanDailyMax[m]</c> and
    ///     <c>MonthlyMeanDailyMin[m]</c>.
    ///   </description></item>
    ///   <item><description>
    ///     <see cref="WarmestMonthlyMeanDailyMax"/> = max over months of
    ///     <c>MonthlyMeanDailyMax[m]</c>.
    ///   </description></item>
    ///   <item><description>
    ///     <see cref="ColdestMonthlyMeanDailyMin"/> = min over months of
    ///     <c>MonthlyMeanDailyMin[m]</c>.
    ///   </description></item>
    ///   <item><description>
    ///     <see cref="AnnualAverageTemperature"/> = mean of every dry-bulb
    ///     reading in the window.
    ///   </description></item>
    ///   <item><description>
    ///     <see cref="PeakDayOfYear"/> = day of year whose daily-mean
    ///     dry-bulb temperature is highest.
    ///   </description></item>
    /// </list>
    /// <para>
    /// Taking daily extremes before monthly averaging (rather than averaging
    /// all hourly readings first) preserves the diurnal swing, so the
    /// resulting range reflects the seasonal envelope rather than a
    /// symmetric mean. See the reference usage in
    /// <c>tests/BESTEST_2023/Section7Runner.cs</c> (Case C990 ground-coupling).
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

      // レコードは時系列順なので (WeatherData の不変条件)、単純に 1 パスで
      // 「日」単位の max/min/sum を畳み込み、日が変わるたびに月累積へ flush する。
      double hourlySum = 0.0;
      int hourlyCount = 0;

      double[] monthDailyMaxSum = new double[13];
      double[] monthDailyMinSum = new double[13];
      int[] monthDayCount = new int[13];
      int peakDayOfYear = 1;
      double peakDailyMean = double.MinValue;

      DateTime currentDay = DateTime.MinValue;
      double dayMax = double.MinValue;
      double dayMin = double.MaxValue;
      double daySum = 0.0;
      int dayCount = 0;

      for (int i = 0; i < records.Count; i++)
      {
        var r = records[i];
        if (r.Time >= windowEnd) break;
        if (!r.Has(WeatherField.DryBulbTemperature)) continue;

        double t = r.DryBulbTemperature;
        DateTime day = r.Time.Date;

        if (day != currentDay && dayCount > 0)
        {
          FinalizeDay(currentDay, dayMax, dayMin, daySum, dayCount,
              monthDailyMaxSum, monthDailyMinSum, monthDayCount,
              ref peakDayOfYear, ref peakDailyMean);
          dayMax = double.MinValue;
          dayMin = double.MaxValue;
          daySum = 0.0;
          dayCount = 0;
        }
        currentDay = day;

        if (t > dayMax) dayMax = t;
        if (t < dayMin) dayMin = t;
        daySum += t;
        dayCount++;

        hourlySum += t;
        hourlyCount++;
      }

      if (dayCount > 0)
      {
        FinalizeDay(currentDay, dayMax, dayMin, daySum, dayCount,
            monthDailyMaxSum, monthDailyMinSum, monthDayCount,
            ref peakDayOfYear, ref peakDailyMean);
      }

      if (hourlyCount == 0)
        throw new PopoloArgumentException(
            "data has no records with dry-bulb temperature.", nameof(data));

      // 12 ヶ月すべてが揃っていないと月平均ベースの推定が意味を持たない。
      for (int m = 1; m <= 12; m++)
      {
        if (monthDayCount[m] == 0)
          throw new PopoloArgumentException(
              $"no dry-bulb records for month {m} in the first-year window.",
              nameof(data));
      }

      double warmestValue = double.MinValue;
      double coldestValue = double.MaxValue;
      for (int m = 1; m <= 12; m++)
      {
        double meanOfDailyMax = monthDailyMaxSum[m] / monthDayCount[m];
        double meanOfDailyMin = monthDailyMinSum[m] / monthDayCount[m];
        if (meanOfDailyMax > warmestValue) warmestValue = meanOfDailyMax;
        if (meanOfDailyMin < coldestValue) coldestValue = meanOfDailyMin;
      }

      double annualAverage = hourlySum / hourlyCount;

      return new Ground(peakDayOfYear, warmestValue, coldestValue, annualAverage);
    }

    private static void FinalizeDay(
        DateTime day, double max, double min, double sum, int count,
        double[] monthDailyMaxSum, double[] monthDailyMinSum, int[] monthDayCount,
        ref int peakDayOfYear, ref double peakDailyMean)
    {
      int m = day.Month;
      monthDailyMaxSum[m] += max;
      monthDailyMinSum[m] += min;
      monthDayCount[m]++;

      double dailyMean = sum / count;
      if (dailyMean > peakDailyMean)
      {
        peakDailyMean = dailyMean;
        peakDayOfYear = day.DayOfYear;
      }
    }

    #endregion

  }
}
