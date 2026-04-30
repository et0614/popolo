/* WeatherRecordBuilder.cs
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

namespace Popolo.Core.Climate.Weather
{
  /// <summary>
  /// Fluent builder for <see cref="WeatherRecord"/>.
  /// </summary>
  /// <remarks>
  /// <para>
  /// Fields that are not explicitly set remain missing, i.e. the corresponding
  /// flag is not set in <see cref="WeatherRecord.AvailableFields"/>.
  /// </para>
  /// <para>
  /// Each <c>Set...</c> method marks its field as <b>recorded</b>. Values
  /// that the caller derives (rather than reads verbatim from a source
  /// format) should additionally be passed through
  /// <see cref="MarkEstimated(WeatherField)"/> to reclassify them, so that
  /// downstream consumers can distinguish observed from derived values via
  /// <see cref="WeatherRecord.IsEstimated(WeatherField)"/>.
  /// </para>
  /// <para>
  /// <see cref="SetSourceTime(DateTime)"/> is optional; when it is not called,
  /// <see cref="WeatherRecord.SourceTime"/> is set equal to
  /// <see cref="WeatherRecord.Time"/>.
  /// </para>
  /// <para>
  /// A builder instance can be reused across many records by calling
  /// <see cref="Reset"/> between builds, which is useful in reader loops to
  /// avoid per-record allocations.
  /// </para>
  /// </remarks>
  public sealed class WeatherRecordBuilder
  {
    private DateTime _time;
    private DateTime _sourceTime;
    private bool _sourceTimeSet;
    private double _dryBulbTemperature;
    private double _humidityRatio;
    private double _atmosphericPressure;
    private double _globalHorizontalRadiation;
    private double _directNormalRadiation;
    private double _diffuseHorizontalRadiation;
    private double _atmosphericRadiation;
    private double _windSpeed;
    private double _windDirection;
    private double _precipitation;
    private double _cloudCover;
    private double _opaqueCloudCover;
    private WeatherField _recordedMask;
    private WeatherField _estimatedMask;

    /// <summary>Sets the logical time.</summary>
    public WeatherRecordBuilder SetTime(DateTime time)
    {
      _time = time;
      return this;
    }

    /// <summary>
    /// Sets the original observation time. When omitted, <see cref="WeatherRecord.SourceTime"/>
    /// defaults to the value of <see cref="WeatherRecord.Time"/>.
    /// </summary>
    public WeatherRecordBuilder SetSourceTime(DateTime sourceTime)
    {
      _sourceTime = sourceTime;
      _sourceTimeSet = true;
      return this;
    }

    /// <summary>Sets the dry-bulb temperature [°C] and marks it as recorded.</summary>
    public WeatherRecordBuilder SetDryBulbTemperature(double value)
    {
      _dryBulbTemperature = value;
      _recordedMask |= WeatherField.DryBulbTemperature;
      return this;
    }

    /// <summary>Sets the absolute humidity ratio [g/kg(DA)] and marks it as recorded.</summary>
    public WeatherRecordBuilder SetHumidityRatio(double value)
    {
      _humidityRatio = value;
      _recordedMask |= WeatherField.HumidityRatio;
      return this;
    }

    /// <summary>Sets the atmospheric pressure [kPa] and marks it as recorded.</summary>
    public WeatherRecordBuilder SetAtmosphericPressure(double value)
    {
      _atmosphericPressure = value;
      _recordedMask |= WeatherField.AtmosphericPressure;
      return this;
    }

    /// <summary>Sets the global horizontal radiation [W/m²] and marks it as recorded.</summary>
    public WeatherRecordBuilder SetGlobalHorizontalRadiation(double value)
    {
      _globalHorizontalRadiation = value;
      _recordedMask |= WeatherField.GlobalHorizontalRadiation;
      return this;
    }

    /// <summary>Sets the direct normal radiation [W/m²] and marks it as recorded.</summary>
    public WeatherRecordBuilder SetDirectNormalRadiation(double value)
    {
      _directNormalRadiation = value;
      _recordedMask |= WeatherField.DirectNormalRadiation;
      return this;
    }

    /// <summary>Sets the diffuse horizontal radiation [W/m²] and marks it as recorded.</summary>
    public WeatherRecordBuilder SetDiffuseHorizontalRadiation(double value)
    {
      _diffuseHorizontalRadiation = value;
      _recordedMask |= WeatherField.DiffuseHorizontalRadiation;
      return this;
    }

    /// <summary>Sets the atmospheric (long-wave) radiation [W/m²] and marks it as recorded.</summary>
    public WeatherRecordBuilder SetAtmosphericRadiation(double value)
    {
      _atmosphericRadiation = value;
      _recordedMask |= WeatherField.AtmosphericRadiation;
      return this;
    }

    /// <summary>Sets the wind speed [m/s] and marks it as recorded.</summary>
    public WeatherRecordBuilder SetWindSpeed(double value)
    {
      _windSpeed = value;
      _recordedMask |= WeatherField.WindSpeed;
      return this;
    }

    /// <summary>
    /// Sets the wind direction [radian] and marks it as recorded.
    /// South = 0, east = negative, west = positive
    /// (same convention as <see cref="Incline"/>).
    /// </summary>
    public WeatherRecordBuilder SetWindDirection(double value)
    {
      _windDirection = value;
      _recordedMask |= WeatherField.WindDirection;
      return this;
    }

    /// <summary>Sets the precipitation rate [mm/h] and marks it as recorded.</summary>
    public WeatherRecordBuilder SetPrecipitation(double value)
    {
      _precipitation = value;
      _recordedMask |= WeatherField.Precipitation;
      return this;
    }

    /// <summary>Sets the cloud cover fraction [0, 1] and marks it as recorded.</summary>
    public WeatherRecordBuilder SetCloudCover(double value)
    {
      _cloudCover = value;
      _recordedMask |= WeatherField.CloudCover;
      return this;
    }

    /// <summary>Sets the opaque cloud cover fraction [0, 1] and marks it as recorded.</summary>
    public WeatherRecordBuilder SetOpaqueCloudCover(double value)
    {
      _opaqueCloudCover = value;
      _recordedMask |= WeatherField.OpaqueCloudCover;
      return this;
    }

    /// <summary>
    /// Reclassifies the given fields from recorded to estimated.
    /// </summary>
    /// <param name="fields">
    /// One or more fields that were derived by the caller rather than read
    /// verbatim from a source. The bits are cleared from the recorded mask
    /// and set in the estimated mask, preserving the invariant that a field
    /// is never simultaneously recorded and estimated.
    /// </param>
    /// <returns>This builder, for chaining.</returns>
    /// <remarks>
    /// Typical usage is to call a <c>Set...</c> method with a derived value
    /// and then immediately call <see cref="MarkEstimated(WeatherField)"/>
    /// with the corresponding flag. Calling <see cref="MarkEstimated(WeatherField)"/>
    /// for a field that was never set results in the estimated bit being
    /// claimed without a meaningful value; this is a caller error that the
    /// builder does not diagnose.
    /// </remarks>
    public WeatherRecordBuilder MarkEstimated(WeatherField fields)
    {
      _recordedMask &= ~fields;
      _estimatedMask |= fields;
      return this;
    }

    /// <summary>
    /// Builds the record. All unset fields remain missing.
    /// </summary>
    public WeatherRecord ToRecord()
    {
      DateTime sourceTime = _sourceTimeSet ? _sourceTime : _time;
      return new WeatherRecord(
          _time, sourceTime,
          _dryBulbTemperature, _humidityRatio, _atmosphericPressure,
          _globalHorizontalRadiation, _directNormalRadiation, _diffuseHorizontalRadiation,
          _atmosphericRadiation, _windSpeed, _windDirection,
          _precipitation, _cloudCover, _opaqueCloudCover,
          _recordedMask, _estimatedMask);
    }

    /// <summary>
    /// Clears all fields and flags so that the instance can be reused to build
    /// another record without reallocation.
    /// </summary>
    public void Reset()
    {
      _recordedMask = WeatherField.None;
      _estimatedMask = WeatherField.None;
      _sourceTimeSet = false;
    }
  }
}
