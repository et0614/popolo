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
    private WeatherField _mask;

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
      _mask |= WeatherField.DryBulbTemperature;
      return this;
    }

    /// <summary>Sets the absolute humidity ratio [g/kg(DA)] and marks it as recorded.</summary>
    public WeatherRecordBuilder SetHumidityRatio(double value)
    {
      _humidityRatio = value;
      _mask |= WeatherField.HumidityRatio;
      return this;
    }

    /// <summary>Sets the atmospheric pressure [kPa] and marks it as recorded.</summary>
    public WeatherRecordBuilder SetAtmosphericPressure(double value)
    {
      _atmosphericPressure = value;
      _mask |= WeatherField.AtmosphericPressure;
      return this;
    }

    /// <summary>Sets the global horizontal radiation [W/m²] and marks it as recorded.</summary>
    public WeatherRecordBuilder SetGlobalHorizontalRadiation(double value)
    {
      _globalHorizontalRadiation = value;
      _mask |= WeatherField.GlobalHorizontalRadiation;
      return this;
    }

    /// <summary>Sets the direct normal radiation [W/m²] and marks it as recorded.</summary>
    public WeatherRecordBuilder SetDirectNormalRadiation(double value)
    {
      _directNormalRadiation = value;
      _mask |= WeatherField.DirectNormalRadiation;
      return this;
    }

    /// <summary>Sets the diffuse horizontal radiation [W/m²] and marks it as recorded.</summary>
    public WeatherRecordBuilder SetDiffuseHorizontalRadiation(double value)
    {
      _diffuseHorizontalRadiation = value;
      _mask |= WeatherField.DiffuseHorizontalRadiation;
      return this;
    }

    /// <summary>Sets the atmospheric (long-wave) radiation [W/m²] and marks it as recorded.</summary>
    public WeatherRecordBuilder SetAtmosphericRadiation(double value)
    {
      _atmosphericRadiation = value;
      _mask |= WeatherField.AtmosphericRadiation;
      return this;
    }

    /// <summary>Sets the wind speed [m/s] and marks it as recorded.</summary>
    public WeatherRecordBuilder SetWindSpeed(double value)
    {
      _windSpeed = value;
      _mask |= WeatherField.WindSpeed;
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
      _mask |= WeatherField.WindDirection;
      return this;
    }

    /// <summary>Sets the precipitation rate [mm/h] and marks it as recorded.</summary>
    public WeatherRecordBuilder SetPrecipitation(double value)
    {
      _precipitation = value;
      _mask |= WeatherField.Precipitation;
      return this;
    }

    /// <summary>Sets the cloud cover fraction [0, 1] and marks it as recorded.</summary>
    public WeatherRecordBuilder SetCloudCover(double value)
    {
      _cloudCover = value;
      _mask |= WeatherField.CloudCover;
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
          _precipitation, _cloudCover, _mask);
    }

    /// <summary>
    /// Clears all fields and flags so that the instance can be reused to build
    /// another record without reallocation.
    /// </summary>
    public void Reset()
    {
      _mask = WeatherField.None;
      _sourceTimeSet = false;
    }
  }
}
