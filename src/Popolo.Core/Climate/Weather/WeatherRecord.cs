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
  /// For every field present in <see cref="AvailableFields"/>, the source of
  /// the value is one of two mutually-exclusive provenances:
  /// </para>
  /// <list type="bullet">
  ///   <item><description>
  ///     <see cref="RecordedFields"/> — the value was read verbatim from the
  ///     source format (an observation or a value already stored in the file).
  ///   </description></item>
  ///   <item><description>
  ///     <see cref="EstimatedFields"/> — the value was derived by the reader
  ///     from other recorded quantities in response to a
  ///     <c>WeatherReadOptions</c> flag. Estimated values carry additional
  ///     uncertainty and may indicate that a given field was not present in
  ///     the source file.
  ///   </description></item>
  /// </list>
  /// <para>
  /// The invariant <c>RecordedFields &amp; EstimatedFields == 0</c> always
  /// holds: no field can be both recorded and estimated in the same record.
  /// <see cref="AvailableFields"/> is the union of the two.
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

    /// <summary>
    /// Opaque cloud cover as a fraction in [0, 1]. The portion of the sky
    /// that is entirely obscured by clouds (excluding partial / transparent
    /// cloud layers). Always less than or equal to <see cref="CloudCover"/>.
    /// </summary>
    public double OpaqueCloudCover { get; }

    /// <summary>
    /// Ceiling height [m] above ground level (lowest opaque cloud base).
    /// Sources commonly use a sentinel like 22000 m for "unlimited / no clouds".
    /// </summary>
    public double CeilingHeight { get; }

    /// <summary>
    /// Bit mask of fields that were read verbatim from the source format.
    /// </summary>
    public WeatherField RecordedFields { get; }

    /// <summary>
    /// Bit mask of fields that were derived by the reader from other recorded
    /// quantities (e.g. diffuse horizontal radiation computed from GHI and
    /// DNI via the geometric identity). Disjoint with
    /// <see cref="RecordedFields"/>.
    /// </summary>
    public WeatherField EstimatedFields { get; }

    /// <summary>
    /// Bit mask of fields that hold a value, whether recorded or estimated.
    /// Equivalent to <c>RecordedFields | EstimatedFields</c>.
    /// </summary>
    public WeatherField AvailableFields => RecordedFields | EstimatedFields;

    /// <summary>
    /// Initializes a new record with all fields specified.
    /// </summary>
    /// <remarks>
    /// This constructor is intended for use by <see cref="WeatherRecordBuilder"/>
    /// and by readers. Application code should use the builder.
    /// The caller is responsible for maintaining the invariant
    /// <c>recordedFields &amp; estimatedFields == 0</c>.
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
        double opaqueCloudCover,
        double ceilingHeight,
        WeatherField recordedFields,
        WeatherField estimatedFields)
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
      OpaqueCloudCover = opaqueCloudCover;
      CeilingHeight = ceilingHeight;
      RecordedFields = recordedFields;
      EstimatedFields = estimatedFields;
    }

    /// <summary>
    /// Returns true when every flag in <paramref name="fields"/> is set in
    /// <see cref="AvailableFields"/>.
    /// </summary>
    /// <param name="fields">One or more fields to test. Bits are ANDed together.</param>
    /// <returns>True if all requested fields hold a value (recorded or estimated).</returns>
    public bool Has(WeatherField fields) => (AvailableFields & fields) == fields;

    /// <summary>
    /// Returns true when every flag in <paramref name="fields"/> holds a value
    /// that was derived by the reader (i.e. is in <see cref="EstimatedFields"/>).
    /// </summary>
    /// <param name="fields">One or more fields to test. Bits are ANDed together.</param>
    /// <returns>True if every requested field is an estimated value.</returns>
    public bool IsEstimated(WeatherField fields) => (EstimatedFields & fields) == fields;
  }
}
