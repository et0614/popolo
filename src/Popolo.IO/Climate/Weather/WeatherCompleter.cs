/* WeatherCompleter.cs
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

using Popolo.Core.Climate;
using Popolo.Core.Climate.Weather;
using Popolo.Core.Physics;

namespace Popolo.IO.Climate.Weather
{
  /// <summary>
  /// Post-processes a <see cref="WeatherData"/> after parsing by deriving
  /// field values that the source format did not record, according to a
  /// <see cref="WeatherReadOptions"/>.
  /// </summary>
  /// <remarks>
  /// <para>
  /// Each derived value is written back through
  /// <see cref="WeatherData.SetRecord(int, WeatherRecord)"/> and flagged via
  /// <see cref="WeatherRecordBuilder.MarkEstimated(WeatherField)"/> so that
  /// downstream consumers can tell observed from estimated values with
  /// <see cref="WeatherRecord.IsEstimated(WeatherField)"/>.
  /// </para>
  /// <para>
  /// Phases are applied in this order, and each phase skips records that
  /// already carry the field:
  /// </para>
  /// <list type="number">
  ///   <item><description>
  ///     <see cref="WeatherReadOptions.EstimateAtmosphericPressureFromElevation"/>
  ///     — station-wide constant from the standard atmosphere model.
  ///   </description></item>
  ///   <item><description>
  ///     <see cref="WeatherReadOptions.CompleteRadiationComponentsByGeometry"/>
  ///     — model-free completion of one missing component among
  ///     {GHI, DNI, DHI} when the other two are present.
  ///   </description></item>
  ///   <item><description>
  ///     <see cref="WeatherReadOptions.SplitGlobalRadiationIntoDirectAndDiffuse"/>
  ///     — Erbs split applied only when DNI and/or DHI are still missing
  ///     after the geometric phase.
  ///   </description></item>
  ///   <item><description>
  ///     <see cref="WeatherReadOptions.EstimateAtmosphericRadiation"/> —
  ///     downwelling longwave from T, w, P, and cloud cover via
  ///     <see cref="Sky.GetInfraredRadiationFromSky"/>.
  ///   </description></item>
  /// </list>
  /// <para>
  /// Solar-geometry phases require the station latitude/longitude.  The
  /// standard-time meridian used by <see cref="Sun"/> is inferred as the
  /// nearest 15° multiple of the station longitude; when
  /// <see cref="WeatherData.Station"/> exposes no location information (the
  /// default-initialised struct), solar phases are skipped.
  /// </para>
  /// </remarks>
  internal static class WeatherCompleter
  {
    /// <summary>Applies every derivation enabled in <paramref name="options"/>.</summary>
    public static void Apply(WeatherData data, WeatherReadOptions options)
    {
      if (data == null || options == null) return;
      if (data.Count == 0) return;

      // If the file has no station and a fallback is set in options,
      // apply it before the completion steps (so solar completion works for HASP / TMY1)
      if (options.Station.HasValue && !HasStationLocation(data.Station))
        data.Station = options.Station.Value;

      if (options.EstimateAtmosphericPressureFromElevation)
        CompletePressureFromElevation(data);

      // Humidity completion always runs. The physics layer reads only
      // HumidityRatio, so data given only RH would break downstream unless w is filled in.
      // The completion is bidirectional, so RH also becomes available for data given only w.
      CompleteHumidity(data);

      if (options.CompleteRadiationComponentsByGeometry
          || options.SplitGlobalRadiationIntoDirectAndDiffuse)
        CompleteRadiation(data, options);

      if (options.EstimateAtmosphericRadiation)
        CompleteAtmosphericRadiation(data);
    }

    #region Atmospheric pressure completion

    private static void CompletePressureFromElevation(WeatherData data)
    {
      // Standard atmosphere model. It returns 101.325 kPa even at elevation 0 m,
      // so it can still be applied to datasets with no station information.
      double pressure = MoistAir.GetAtmosphericPressure(data.Station.Elevation);

      for (int i = 0; i < data.Count; i++)
      {
        var r = data.Records[i];
        if (r.Has(WeatherField.AtmosphericPressure)) continue;

        var updated = RebuildWith(r,
            builder => builder
                .SetAtmosphericPressure(pressure)
                .MarkEstimated(WeatherField.AtmosphericPressure));
        data.SetRecord(i, updated);
      }
    }

    #endregion

    #region Humidity completion (HumidityRatio ↔ RelativeHumidity)

    /// <summary>
    /// For rows where DBT is given, derives the other of HumidityRatio and
    /// RelativeHumidity when only one is recorded. Does nothing when neither
    /// is recorded (including no DBT). Uses <c>r.AtmosphericPressure</c> if
    /// available; otherwise the standard atmospheric pressure (101.325 kPa).
    /// </summary>
    /// <remarks>
    /// The MoistAir API handles humidity in [kg/kg(DA)] while WeatherRecord
    /// stores it in [g/kg(DA)], so a factor-of-1000 conversion is applied on both sides.
    /// </remarks>
    private static void CompleteHumidity(WeatherData data)
    {
      for (int i = 0; i < data.Count; i++)
      {
        var r = data.Records[i];
        if (!r.Has(WeatherField.DryBulbTemperature)) continue;

        bool hasW  = r.Has(WeatherField.HumidityRatio);
        bool hasRh = r.Has(WeatherField.RelativeHumidity);

        if (hasW == hasRh) continue;   // both missing / both present → do nothing

        double pKPa = r.Has(WeatherField.AtmosphericPressure)
            ? r.AtmosphericPressure
            : PhysicsConstants.StandardAtmosphericPressure;

        if (hasRh)
        {
          double wKgKg = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndRelativeHumidity(
              r.DryBulbTemperature, r.RelativeHumidity, pKPa);
          var updated = RebuildWith(r, b => b
              .SetHumidityRatio(wKgKg * 1000.0)
              .MarkEstimated(WeatherField.HumidityRatio));
          data.SetRecord(i, updated);
        }
        else // hasW
        {
          double rh = MoistAir.GetRelativeHumidityFromDryBulbTemperatureAndHumidityRatio(
              r.DryBulbTemperature, r.HumidityRatio / 1000.0, pKPa);
          var updated = RebuildWith(r, b => b
              .SetRelativeHumidity(rh)
              .MarkEstimated(WeatherField.RelativeHumidity));
          data.SetRecord(i, updated);
        }
      }
    }

    #endregion

    #region Solar radiation completion (geometry / Erbs)

    /// <summary>
    /// Below this effective sin(altitude), DNI back-transforms become
    /// noise-dominated (dividing interval-integrated radiation by a small
    /// number). Geometry's DNI reconstruction is skipped; Erbs is allowed
    /// to run (it produces 0 for all three components and is self-consistent).
    /// </summary>
    private const double MinEffectiveSinH = 0.02;     // solar contribution counted above ≈ 1.15°

    private static void CompleteRadiation(WeatherData data, WeatherReadOptions options)
    {
      if (!HasStationLocation(data.Station)) return;

      double latitude = data.Station.Latitude;
      double longitude = data.Station.Longitude;
      double standardLongitude = 15.0 * Math.Round(longitude / 15.0);

      // Decide how to sample the effective sinH from the interval length and label convention
      TimeSpan? interval = data.NominalInterval;
      bool canIntegrate = interval.HasValue
                          && options.TimestampConvention != TimestampConvention.Instant;

      for (int i = 0; i < data.Count; i++)
      {
        var r = data.Records[i];

        double sinHeff = canIntegrate
            ? EffectiveSinH(r.Time, interval!.Value, options.TimestampConvention,
                            latitude, longitude, standardLongitude)
            : Math.Max(0, Math.Sin(
                Sun.GetSunAltitude(latitude, longitude, standardLongitude, r.Time)));

        var updated = r;

        if (options.CompleteRadiationComponentsByGeometry)
          updated = TryCompleteByGeometry(updated, sinHeff);

        if (options.SplitGlobalRadiationIntoDirectAndDiffuse)
          updated = TrySplitByErbs(updated, sinHeff, r.Time.DayOfYear);

        if (!Equals(updated, r))
          data.SetRecord(i, updated);
      }
    }

    /// <summary>
    /// Computes the interval-averaged <c>sin(altitude)</c> by sampling the
    /// sun position at <c>N</c> equally-spaced points within the interval
    /// derived from <paramref name="time"/>, <paramref name="interval"/> and
    /// <paramref name="convention"/>. Samples below the horizon contribute
    /// zero, so intervals that straddle sunrise or sunset yield a value
    /// weighted only by the sunlit portion.
    /// </summary>
    private static double EffectiveSinH(
        DateTime time, TimeSpan interval, TimestampConvention convention,
        double latitude, double longitude, double standardLongitude)
    {
      // Determine the interval (tStart, tEnd)
      DateTime tStart, tEnd;
      switch (convention)
      {
        case TimestampConvention.EndOfInterval:
          tStart = time - interval;
          tEnd = time;
          break;
        case TimestampConvention.StartOfInterval:
          tStart = time;
          tEnd = time + interval;
          break;
        case TimestampConvention.Midpoint:
          tStart = time - TimeSpan.FromTicks(interval.Ticks / 2);
          tEnd = tStart + interval;
          break;
        default:
          // Instant is assumed never to enter this function
          return Math.Max(0, Math.Sin(
              Sun.GetSunAltitude(latitude, longitude, standardLongitude, time)));
      }

      // Sample count: roughly every 10 minutes, at least 2 points
      int n = Math.Max(2, (int)Math.Ceiling(interval.TotalMinutes / 10.0));

      double sum = 0;
      for (int k = 0; k < n; k++)
      {
        double frac = (k + 0.5) / n;
        DateTime t = tStart + TimeSpan.FromTicks((long)(frac * interval.Ticks));
        double alt = Sun.GetSunAltitude(latitude, longitude, standardLongitude, t);
        if (alt > 0) sum += Math.Sin(alt);
      }
      return sum / n;
    }

    /// <summary>
    /// If exactly one of {GHI, DNI, DHI} is missing and the other two are
    /// present, derive the missing one from the interval-integrated identity
    /// <c>⟨GHI⟩ = ⟨DNI⟩·⟨sin h⟩ + ⟨DHI⟩</c>. GHI and DHI are reconstructed
    /// by a multiplication and thus remain numerically well-behaved even when
    /// <paramref name="sinHeff"/> is zero; DNI requires a division and is
    /// skipped below <see cref="MinEffectiveSinH"/>.
    /// </summary>
    private static WeatherRecord TryCompleteByGeometry(WeatherRecord r, double sinHeff)
    {
      bool hasGhi = r.Has(WeatherField.GlobalHorizontalRadiation);
      bool hasDni = r.Has(WeatherField.DirectNormalRadiation);
      bool hasDhi = r.Has(WeatherField.DiffuseHorizontalRadiation);

      int present = (hasGhi ? 1 : 0) + (hasDni ? 1 : 0) + (hasDhi ? 1 : 0);
      if (present != 2) return r;

      if (!hasGhi)
      {
        double ghi = Math.Max(0, r.DirectNormalRadiation * sinHeff + r.DiffuseHorizontalRadiation);
        return RebuildWith(r, b => b
            .SetGlobalHorizontalRadiation(ghi)
            .MarkEstimated(WeatherField.GlobalHorizontalRadiation));
      }

      if (!hasDhi)
      {
        double dhi = Math.Max(0, r.GlobalHorizontalRadiation - r.DirectNormalRadiation * sinHeff);
        return RebuildWith(r, b => b
            .SetDiffuseHorizontalRadiation(dhi)
            .MarkEstimated(WeatherField.DiffuseHorizontalRadiation));
      }

      // !hasDni — skip when the effective sinH is tiny, since (GHI − DHI) / sinH diverges
      if (sinHeff < MinEffectiveSinH) return r;
      double dni = Math.Max(0, (r.GlobalHorizontalRadiation - r.DiffuseHorizontalRadiation) / sinHeff);
      return RebuildWith(r, b => b
          .SetDirectNormalRadiation(dni)
          .MarkEstimated(WeatherField.DirectNormalRadiation));
    }

    /// <summary>
    /// If GHI is present but DNI and/or DHI are missing, apply the Erbs
    /// split using the interval-averaged <c>sin(altitude)</c>. This is a
    /// local reimplementation of the Erbs correlation so that we can feed
    /// it the effective sinH rather than an instantaneous value — the
    /// mathematically correct quantity for hourly (or sub-hourly)
    /// integrated GHI.
    /// </summary>
    private static WeatherRecord TrySplitByErbs(
        WeatherRecord r, double sinHeff, int dayOfYear)
    {
      if (!r.Has(WeatherField.GlobalHorizontalRadiation)) return r;

      bool missingDni = !r.Has(WeatherField.DirectNormalRadiation);
      bool missingDhi = !r.Has(WeatherField.DiffuseHorizontalRadiation);
      if (!missingDni && !missingDhi) return r;

      double ghi = r.GlobalHorizontalRadiation;
      double dni, dhi;

      if (ghi <= 0 || sinHeff <= 0)
      {
        // Night-time, or an interval with GHI=0: both direct and diffuse are zero
        dni = 0;
        dhi = 0;
      }
      else
      {
        double io = Sun.GetExtraterrestrialRadiation(dayOfYear);
        double kt = ghi / (io * sinHeff);

        // Compute the diffuse fraction K_d = DHI/GHI with the Erbs (1982) piecewise polynomial
        double kd;
        if (kt < 0.22)
          kd = 1.0 - 0.09 * kt;
        else if (kt < 0.80)
          kd = 0.9511 + kt * (-0.1604 + kt * (4.388 + kt * (-16.638 + 12.336 * kt)));
        else
          kd = 0.1651;

        kd = Math.Clamp(kd, 0.0, 1.0);
        dhi = ghi * kd;

        // When the effective sinH is too small, the DNI back-transform diverges into
        // noise, so force DNI to 0 and set DHI = GHI (= treat everything as diffuse)
        if (sinHeff < MinEffectiveSinH)
        {
          dhi = ghi;
          dni = 0;
        }
        else
        {
          dni = (ghi - dhi) / sinHeff;
        }
      }

      return RebuildWith(r, b =>
      {
        WeatherField estimated = WeatherField.None;
        if (missingDni)
        {
          b.SetDirectNormalRadiation(dni);
          estimated |= WeatherField.DirectNormalRadiation;
        }
        if (missingDhi)
        {
          b.SetDiffuseHorizontalRadiation(dhi);
          estimated |= WeatherField.DiffuseHorizontalRadiation;
        }
        b.MarkEstimated(estimated);
      });
    }

    #endregion

    #region Atmospheric radiation completion

    private static void CompleteAtmosphericRadiation(WeatherData data)
    {
      double fallbackPressure = MoistAir.GetAtmosphericPressure(data.Station.Elevation);

      for (int i = 0; i < data.Count; i++)
      {
        var r = data.Records[i];
        if (r.Has(WeatherField.AtmosphericRadiation)) continue;
        if (!r.Has(WeatherField.DryBulbTemperature)) continue;
        // Martin-Berdahl requires Tdp; the fallback formula requires HumidityRatio.
        // With neither, there is no way to compute IR, so skip.
        bool hasTdp = r.Has(WeatherField.DewPointTemperature);
        bool hasW = r.Has(WeatherField.HumidityRatio);
        if (!hasTdp && !hasW) continue;

        double pressure = r.Has(WeatherField.AtmosphericPressure)
            ? r.AtmosphericPressure
            : fallbackPressure;

        // Weather record humidity ratio is in [g/kg(DA)]; MoistAir uses [kg/kg(DA)]
        double wKgKg = hasW ? r.HumidityRatio / 1000.0 : 0.0;

        double atmRad;
        // Use Martin-Berdahl 1984 when opaque cloud cover, ceiling height, and cloud cover
        // are all available (the same formula as ANSI/ASHRAE 140-2023 Tsky-Informative).
        // Fall back to the conventional simple formula when only cloud cover is available.
        bool canUseMartinBerdahl =
               r.Has(WeatherField.CloudCover)
            && r.Has(WeatherField.OpaqueCloudCover)
            && r.Has(WeatherField.CeilingHeight);

        if (canUseMartinBerdahl && (hasTdp || hasW))
        {
          // If Tdp is recorded directly in the weather record (e.g. TMY3 col 34), prefer it.
          // Otherwise recompute it via HumidityRatio.
          // Reason: TMY3 and the like process RH and Tdp independently, so some hours do not
          // Magnus round-trip, and Std 140-2023 Tsky-Informative uses the TMY3 Tdp column directly.
          double tdp = hasTdp
              ? r.DewPointTemperature
              : MoistAir.GetDewPointTemperatureFromHumidityRatio(wKgKg, pressure);
          // The Std 140-2023 reference formula requires atmospheric pressure in [mbar = hPa].
          // Popolo's AtmosphericPressure is in kPa, so multiply by 10.
          double pMbar = pressure * 10.0;
          atmRad = Sky.GetInfraredRadiationFromSky(
              r.DryBulbTemperature,
              tdp,
              pMbar,
              r.CloudCover,
              r.OpaqueCloudCover,
              r.CeilingHeight,
              r.Time.Hour);
        }
        else if (hasW)
        {
          double vaporPressure =
              MoistAir.GetWaterVaporPartialPressureFromHumidityRatio(wKgKg, pressure);
          int cloudIndex = r.Has(WeatherField.CloudCover)
              ? (int)Math.Round(Math.Clamp(r.CloudCover, 0.0, 1.0) * 10.0)
              : 0;
          atmRad = Sky.GetInfraredRadiationFromSky(
              r.DryBulbTemperature, cloudIndex, vaporPressure);
        }
        else
        {
          // Tdp is present but the columns Martin-Berdahl needs (cloud/ceiling) are
          // incomplete, and HumidityRatio is also missing → the fallback formula cannot be used, so skip.
          continue;
        }

        var updated = RebuildWith(r, b => b
            .SetAtmosphericRadiation(atmRad)
            .MarkEstimated(WeatherField.AtmosphericRadiation));
        data.SetRecord(i, updated);
      }
    }

    #endregion

    #region Helpers

    private static bool HasStationLocation(WeatherStationInfo station)
    {
      // The default (Name="" and latitude/longitude 0) is treated as location not set.
      if (!string.IsNullOrEmpty(station.Name)) return true;
      return station.Latitude != 0.0 || station.Longitude != 0.0;
    }

    /// <summary>
    /// Copies all present fields of <paramref name="r"/> into a fresh builder,
    /// restores the <see cref="WeatherRecord.EstimatedFields"/> classification,
    /// then lets the caller add or override additional fields.
    /// </summary>
    private static WeatherRecord RebuildWith(
        WeatherRecord r, Action<WeatherRecordBuilder> mutate)
    {
      var b = new WeatherRecordBuilder().SetTime(r.Time);
      if (r.SourceTime != r.Time) b.SetSourceTime(r.SourceTime);

      if (r.Has(WeatherField.DryBulbTemperature))        b.SetDryBulbTemperature(r.DryBulbTemperature);
      if (r.Has(WeatherField.HumidityRatio))             b.SetHumidityRatio(r.HumidityRatio);
      if (r.Has(WeatherField.RelativeHumidity))          b.SetRelativeHumidity(r.RelativeHumidity);
      if (r.Has(WeatherField.AtmosphericPressure))       b.SetAtmosphericPressure(r.AtmosphericPressure);
      if (r.Has(WeatherField.GlobalHorizontalRadiation)) b.SetGlobalHorizontalRadiation(r.GlobalHorizontalRadiation);
      if (r.Has(WeatherField.DirectNormalRadiation))     b.SetDirectNormalRadiation(r.DirectNormalRadiation);
      if (r.Has(WeatherField.DiffuseHorizontalRadiation)) b.SetDiffuseHorizontalRadiation(r.DiffuseHorizontalRadiation);
      if (r.Has(WeatherField.AtmosphericRadiation))      b.SetAtmosphericRadiation(r.AtmosphericRadiation);
      if (r.Has(WeatherField.WindSpeed))                 b.SetWindSpeed(r.WindSpeed);
      if (r.Has(WeatherField.WindDirection))             b.SetWindDirection(r.WindDirection);
      if (r.Has(WeatherField.Precipitation))             b.SetPrecipitation(r.Precipitation);
      if (r.Has(WeatherField.CloudCover))                b.SetCloudCover(r.CloudCover);
      if (r.Has(WeatherField.OpaqueCloudCover))          b.SetOpaqueCloudCover(r.OpaqueCloudCover);
      if (r.Has(WeatherField.CeilingHeight))             b.SetCeilingHeight(r.CeilingHeight);

      // Preserve the existing estimated classification (Set* stores into recorded, so reclassify afterwards)
      b.MarkEstimated(r.EstimatedFields);

      mutate(b);
      return b.ToRecord();
    }

    private static bool Equals(WeatherRecord a, WeatherRecord b)
    {
      return a.RecordedFields == b.RecordedFields
          && a.EstimatedFields == b.EstimatedFields;
    }

    #endregion
  }
}
