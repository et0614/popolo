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
  /// option instructs the reader to derive that field from other recorded
  /// quantities. When a value is derived, the resulting field appears in
  /// <see cref="Popolo.Core.Climate.Weather.WeatherRecord.EstimatedFields"/>
  /// (not <see cref="Popolo.Core.Climate.Weather.WeatherRecord.RecordedFields"/>)
  /// so that downstream consumers can distinguish observed values from derived
  /// ones.
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
    /// Fallback station information used when the source format does not
    /// record the station location (e.g. HASP, TMY1). When non-null and the
    /// reader leaves <see cref="Popolo.Core.Climate.Weather.WeatherData.Station"/>
    /// unset, this value is copied onto the dataset before any derivation
    /// runs. Has no effect when the source format already records station
    /// information (EPW, WEA2, CSV with station header).
    /// </summary>
    /// <remarks>
    /// Solar-dependent derivations
    /// (<see cref="CompleteRadiationComponentsByGeometry"/>,
    /// <see cref="SplitGlobalRadiationIntoDirectAndDiffuse"/>) require a
    /// station location to evaluate the solar altitude. For HASP / TMY1 the
    /// caller must supply this fallback; otherwise those derivations silently
    /// skip.
    /// </remarks>
    public Popolo.Core.Climate.Weather.WeatherStationInfo? Station { get; set; }

    /// <summary>
    /// If <c>true</c> and atmospheric pressure is not recorded in the source
    /// format, the reader estimates pressure from the station elevation using
    /// the standard atmosphere model. Default is <c>false</c>.
    /// </summary>
    public bool EstimateAtmosphericPressureFromElevation { get; set; }

    /// <summary>
    /// If <c>true</c> and exactly one of the three solar-radiation components
    /// {global horizontal, direct normal, diffuse horizontal} is missing while
    /// the other two are recorded, the reader derives the missing component
    /// from the geometric identity
    /// <c>GHI = DNI · cos(θ_z) + DHI</c> where <c>θ_z</c> is the solar zenith
    /// angle. This completion is model-free and deterministic.
    /// Default is <c>false</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// At times when <c>cos(θ_z)</c> is near zero (sun at or below the
    /// horizon), the identity is singular and the reader leaves the missing
    /// component unset.
    /// </para>
    /// <para>
    /// This completion is applied before
    /// <see cref="SplitGlobalRadiationIntoDirectAndDiffuse"/> so that the
    /// cheaper model-free path is preferred when two components are already
    /// available.
    /// </para>
    /// </remarks>
    public bool CompleteRadiationComponentsByGeometry { get; set; }

    /// <summary>
    /// If <c>true</c> and only the global horizontal radiation is recorded,
    /// the reader estimates the direct normal and diffuse horizontal
    /// components using a statistical split model (e.g. Erbs / Reindl).
    /// Default is <c>false</c>.
    /// </summary>
    /// <remarks>
    /// Unlike <see cref="CompleteRadiationComponentsByGeometry"/>, this option
    /// relies on an empirical correlation and therefore introduces an extra
    /// source of uncertainty. Use it only when a single-component source (GHI
    /// only) is the best available data.
    /// </remarks>
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
