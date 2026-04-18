/* WeatherReadOptions.cs
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

namespace Popolo.IO.Climate.Weather
{
  /// <summary>
  /// Options that control how a reader derives values for fields that are not
  /// directly recorded in the source format.
  /// </summary>
  /// <remarks>
  /// <para>
  /// All options default to <c>false</c>, in which case the reader reports only
  /// fields that are explicitly recorded by the source format. Enabling an
  /// option instructs the reader to estimate that field from other recorded
  /// quantities; when the estimation is performed, the resulting value is
  /// marked as recorded in the output <c>WeatherRecord</c>.
  /// </para>
  /// <para>
  /// Individual readers declare in their XML documentation which of these
  /// options they act on. Options that are not supported by a given reader are
  /// silently ignored.
  /// </para>
  /// </remarks>
  public class WeatherReadOptions
  {
    /// <summary>
    /// If <c>true</c> and atmospheric pressure is not recorded in the source
    /// format, the reader estimates pressure from the station elevation using
    /// the standard atmosphere model. Default is <c>false</c>.
    /// </summary>
    public bool EstimateAtmosphericPressureFromElevation { get; set; }

    /// <summary>
    /// If <c>true</c> and direct / diffuse horizontal radiation are not
    /// recorded, the reader estimates them from the global horizontal
    /// radiation using solar geometry and a clearness-based split (e.g.
    /// Bouguer). Default is <c>false</c>.
    /// </summary>
    public bool SplitGlobalRadiationIntoDirectAndDiffuse { get; set; }

    /// <summary>
    /// If <c>true</c> and atmospheric (long-wave) radiation is not recorded,
    /// the reader estimates it from dry-bulb temperature, humidity ratio, and
    /// cloud cover using the Berdahl-Fromberg or equivalent correlation.
    /// Default is <c>false</c>.
    /// </summary>
    public bool EstimateAtmosphericRadiation { get; set; }

    /// <summary>A shared instance of the default (all-false) options.</summary>
    public static WeatherReadOptions Default { get; } = new WeatherReadOptions();
  }
}
