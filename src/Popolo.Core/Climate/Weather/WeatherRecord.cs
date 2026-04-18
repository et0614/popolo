/* WeatherRecord.cs
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
  /// A single observation point of weather data at a single instant in time.
  /// </summary>
  /// <remarks>
  /// <para>
  /// Units:
  /// </para>
  /// <list type="bullet">
  ///   <item><description>Temperature: [°C]</description></item>
  ///   <item><description>Humidity ratio (absolute humidity): [g/kg(DA)]</description></item>
  ///   <item><description>Atmospheric pressure: [kPa]</description></item>
  ///   <item><description>Radiation: [W/m²]</description></item>
  ///   <item><description>Wind speed: [m/s]</description></item>
  ///   <item><description>Wind direction: [radian] (south = 0, east = negative, west = positive)</description></item>
  ///   <item><description>Precipitation: [mm/h]</description></item>
  ///   <item><description>Cloud cover: fraction [0, 1]</description></item>
  /// </list>
  /// <para>
  /// Each numeric field is meaningful only when its corresponding
  /// <see cref="WeatherField"/> flag is set in <see cref="AvailableFields"/>.
  /// A field whose flag is not set holds an undefined value and must not be
  /// read by downstream code.
  /// </para>
  /// <para>
  /// <see cref="Time"/> is the logical time used for simulation, and is required
  /// to be monotonically non-decreasing when records are held by
  /// <see cref="WeatherData"/>. <see cref="SourceTime"/> is the original
  /// observation time; for ordinary observed data it equals <see cref="Time"/>,
  /// while for typical meteorological year (TMY) data <see cref="Time"/> and
  /// <see cref="SourceTime"/> differ so that the origin year/month/day of each
  /// synthesized record is preserved.
  /// </para>
  /// </remarks>
  public readonly struct WeatherRecord
  {
    /// <summary>Logical simulation time of this record.</summary>
    public DateTime Time { get; }

    /// <summary>
    /// Original observation time. Equal to <see cref="Time"/> for ordinary
    /// observed data. Differs from <see cref="Time"/> for typical year data
    /// where each month may originate from a different observation year.
    /// </summary>
    public DateTime SourceTime { get; }

    /// <summary>Dry-bulb temperature [°C].</summary>
    public double DryBulbTemperature { get; }

    /// <summary>Absolute humidity ratio [g/kg(DA)].</summary>
    public double HumidityRatio { get; }

    /// <summary>Atmospheric pressure [kPa].</summary>
    public double AtmosphericPressure { get; }

    /// <summary>Global horizontal solar radiation [W/m²].</summary>
    public double GlobalHorizontalRadiation { get; }

    /// <summary>Direct normal solar radiation [W/m²].</summary>
    public double DirectNormalRadiation { get; }

    /// <summary>Diffuse horizontal solar radiation [W/m²].</summary>
    public double DiffuseHorizontalRadiation { get; }

    /// <summary>Downward atmospheric (long-wave) radiation [W/m²].</summary>
    public double AtmosphericRadiation { get; }

    /// <summary>Wind speed [m/s].</summary>
    public double WindSpeed { get; }

    /// <summary>
    /// Wind direction [radian]. South = 0, east = negative, west = positive,
    /// following the convention of <see cref="Incline"/>.
    /// </summary>
    public double WindDirection { get; }

    /// <summary>Precipitation rate [mm/h].</summary>
    public double Precipitation { get; }

    /// <summary>Cloud cover as a fraction in [0, 1].</summary>
    public double CloudCover { get; }

    /// <summary>Bit mask of fields that hold recorded (non-missing) values.</summary>
    public WeatherField AvailableFields { get; }

    /// <summary>
    /// Initializes a new record with all fields specified.
    /// </summary>
    /// <remarks>
    /// This constructor is intended for use by <see cref="WeatherRecordBuilder"/>
    /// and by readers. Application code should use the builder.
    /// </remarks>
    internal WeatherRecord(
        DateTime time,
        DateTime sourceTime,
        double dryBulbTemperature,
        double humidityRatio,
        double atmosphericPressure,
        double globalHorizontalRadiation,
        double directNormalRadiation,
        double diffuseHorizontalRadiation,
        double atmosphericRadiation,
        double windSpeed,
        double windDirection,
        double precipitation,
        double cloudCover,
        WeatherField availableFields)
    {
      Time = time;
      SourceTime = sourceTime;
      DryBulbTemperature = dryBulbTemperature;
      HumidityRatio = humidityRatio;
      AtmosphericPressure = atmosphericPressure;
      GlobalHorizontalRadiation = globalHorizontalRadiation;
      DirectNormalRadiation = directNormalRadiation;
      DiffuseHorizontalRadiation = diffuseHorizontalRadiation;
      AtmosphericRadiation = atmosphericRadiation;
      WindSpeed = windSpeed;
      WindDirection = windDirection;
      Precipitation = precipitation;
      CloudCover = cloudCover;
      AvailableFields = availableFields;
    }

    /// <summary>
    /// Returns true when every flag in <paramref name="fields"/> is set in
    /// <see cref="AvailableFields"/>.
    /// </summary>
    /// <param name="fields">One or more fields to test. Bits are ANDed together.</param>
    /// <returns>True if all requested fields are recorded.</returns>
    public bool Has(WeatherField fields) => (AvailableFields & fields) == fields;
  }
}
