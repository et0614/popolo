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

      // ファイルが station を持たず、options にフォールバックが設定されていれば
      // 補完処理に先立って反映する (HASP / TMY1 で solar 補完を動かすため)
      if (options.Station.HasValue && !HasStationLocation(data.Station))
        data.Station = options.Station.Value;

      if (options.EstimateAtmosphericPressureFromElevation)
        CompletePressureFromElevation(data);

      if (options.CompleteRadiationComponentsByGeometry
          || options.SplitGlobalRadiationIntoDirectAndDiffuse)
        CompleteRadiation(data, options);

      if (options.EstimateAtmosphericRadiation)
        CompleteAtmosphericRadiation(data);
    }

    #region 気圧の補完

    private static void CompletePressureFromElevation(WeatherData data)
    {
      // 標準大気モデル。標高 0 m でも 101.325 kPa を返すので
      // 駅情報が未設定のデータセットにも一応は適用できる。
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

    #region 日射の補完 (geometry / Erbs)

    private static void CompleteRadiation(WeatherData data, WeatherReadOptions options)
    {
      if (!HasStationLocation(data.Station)) return;

      double latitude = data.Station.Latitude;
      double longitude = data.Station.Longitude;
      double standardLongitude = 15.0 * Math.Round(longitude / 15.0);

      for (int i = 0; i < data.Count; i++)
      {
        var r = data.Records[i];
        double sinH = Math.Sin(Sun.GetSunAltitude(latitude, longitude, standardLongitude, r.Time));
        var updated = r;

        if (options.CompleteRadiationComponentsByGeometry)
          updated = TryCompleteByGeometry(updated, sinH);

        if (options.SplitGlobalRadiationIntoDirectAndDiffuse)
          updated = TrySplitByErbs(updated, latitude, longitude, standardLongitude);

        if (!Equals(updated, r))
          data.SetRecord(i, updated);
      }
    }

    /// <summary>
    /// If exactly one of {GHI, DNI, DHI} is missing and the other two are
    /// present, derive the missing one from <c>GHI = DNI·sin(h) + DHI</c>.
    /// </summary>
    private static WeatherRecord TryCompleteByGeometry(WeatherRecord r, double sinH)
    {
      bool hasGhi = r.Has(WeatherField.GlobalHorizontalRadiation);
      bool hasDni = r.Has(WeatherField.DirectNormalRadiation);
      bool hasDhi = r.Has(WeatherField.DiffuseHorizontalRadiation);

      int present = (hasGhi ? 1 : 0) + (hasDni ? 1 : 0) + (hasDhi ? 1 : 0);
      if (present != 2) return r;

      // 地平線付近は GHI = DNI·sinH + DHI が事実上 GHI ≈ DHI に退化するため、
      // DNI を復元するには不適切。閾値未満は補完しない。
      const double MinSinH = 0.05;          // ≈ 2.87°
      if (sinH < MinSinH)
      {
        // DNI だけが未知で sin(h) が小さい場合は算出できないが、
        // GHI または DHI が未知のケースは低太陽高度でも一意に決まるため許容する。
        if (!hasDni) return r;
      }

      if (!hasGhi)
      {
        double ghi = Math.Max(0, r.DirectNormalRadiation * sinH + r.DiffuseHorizontalRadiation);
        return RebuildWith(r, b => b
            .SetGlobalHorizontalRadiation(ghi)
            .MarkEstimated(WeatherField.GlobalHorizontalRadiation));
      }

      if (!hasDhi)
      {
        double dhi = Math.Max(0, r.GlobalHorizontalRadiation - r.DirectNormalRadiation * sinH);
        return RebuildWith(r, b => b
            .SetDiffuseHorizontalRadiation(dhi)
            .MarkEstimated(WeatherField.DiffuseHorizontalRadiation));
      }

      // !hasDni
      double dni = Math.Max(0, (r.GlobalHorizontalRadiation - r.DiffuseHorizontalRadiation) / sinH);
      return RebuildWith(r, b => b
          .SetDirectNormalRadiation(dni)
          .MarkEstimated(WeatherField.DirectNormalRadiation));
    }

    /// <summary>
    /// If GHI is present but DNI and/or DHI are missing, apply the Erbs
    /// split. The existing <see cref="Sun.SeparateGlobalHorizontalRadiation"/>
    /// returns zeros when the sun is below the horizon or GHI is zero, which
    /// we propagate faithfully.
    /// </summary>
    private static WeatherRecord TrySplitByErbs(
        WeatherRecord r, double latitude, double longitude, double standardLongitude)
    {
      if (!r.Has(WeatherField.GlobalHorizontalRadiation)) return r;

      bool missingDni = !r.Has(WeatherField.DirectNormalRadiation);
      bool missingDhi = !r.Has(WeatherField.DiffuseHorizontalRadiation);
      if (!missingDni && !missingDhi) return r;

      Sun.SeparateGlobalHorizontalRadiation(
          r.GlobalHorizontalRadiation,
          latitude, longitude, standardLongitude, r.Time,
          Sun.SeparationMethod.Erbs,
          out double dni, out double dhi);

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

    #region 大気放射の補完

    private static void CompleteAtmosphericRadiation(WeatherData data)
    {
      double fallbackPressure = MoistAir.GetAtmosphericPressure(data.Station.Elevation);

      for (int i = 0; i < data.Count; i++)
      {
        var r = data.Records[i];
        if (r.Has(WeatherField.AtmosphericRadiation)) continue;
        if (!r.Has(WeatherField.DryBulbTemperature)) continue;
        if (!r.Has(WeatherField.HumidityRatio)) continue;

        double pressure = r.Has(WeatherField.AtmosphericPressure)
            ? r.AtmosphericPressure
            : fallbackPressure;

        // 気象レコードの絶対湿度は [g/kg(DA)]、MoistAir は [kg/kg(DA)]
        double wKgKg = r.HumidityRatio / 1000.0;
        double vaporPressure =
            MoistAir.GetWaterVaporPartialPressureFromHumidityRatio(wKgKg, pressure);

        int cloudIndex = r.Has(WeatherField.CloudCover)
            ? (int)Math.Round(Math.Clamp(r.CloudCover, 0.0, 1.0) * 10.0)
            : 0;

        double atmRad = Sky.GetInfraredRadiationFromSky(
            r.DryBulbTemperature, cloudIndex, vaporPressure);

        var updated = RebuildWith(r, b => b
            .SetAtmosphericRadiation(atmRad)
            .MarkEstimated(WeatherField.AtmosphericRadiation));
        data.SetRecord(i, updated);
      }
    }

    #endregion

    #region ヘルパー

    private static bool HasStationLocation(WeatherStationInfo station)
    {
      // 既定値 (Name="" かつ 緯度経度 0) は location 未設定と見なす。
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
      if (r.Has(WeatherField.AtmosphericPressure))       b.SetAtmosphericPressure(r.AtmosphericPressure);
      if (r.Has(WeatherField.GlobalHorizontalRadiation)) b.SetGlobalHorizontalRadiation(r.GlobalHorizontalRadiation);
      if (r.Has(WeatherField.DirectNormalRadiation))     b.SetDirectNormalRadiation(r.DirectNormalRadiation);
      if (r.Has(WeatherField.DiffuseHorizontalRadiation)) b.SetDiffuseHorizontalRadiation(r.DiffuseHorizontalRadiation);
      if (r.Has(WeatherField.AtmosphericRadiation))      b.SetAtmosphericRadiation(r.AtmosphericRadiation);
      if (r.Has(WeatherField.WindSpeed))                 b.SetWindSpeed(r.WindSpeed);
      if (r.Has(WeatherField.WindDirection))             b.SetWindDirection(r.WindDirection);
      if (r.Has(WeatherField.Precipitation))             b.SetPrecipitation(r.Precipitation);
      if (r.Has(WeatherField.CloudCover))                b.SetCloudCover(r.CloudCover);

      // 既存の estimated 分類を維持 (Set*は recorded に積むので、後から reclassify する)
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
