/* CO2ModelZone.cs
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
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301, USA.
 */

using Popolo.Core.Exceptions;

namespace Popolo.Core.Building.AirQuality
{
  /// <summary>
  /// A well-mixed zone in a <see cref="MultiZoneCO2Model"/>.
  /// </summary>
  /// <remarks>
  /// <para>
  /// Two ventilation paths supply air to the zone:
  /// </para>
  /// <para>
  /// (A) When the zone is bound to a thermal model zone
  /// (<see cref="BoundZone"/>), the outdoor air ventilation rate is read from
  /// <see cref="IReadOnlyZone.VentilationRate"/> at every update, and the
  /// inflow air carries the model-wide outdoor concentration
  /// (<see cref="MultiZoneCO2Model.OutdoorCO2Level"/>).
  /// </para>
  /// <para>
  /// (B) The auxiliary ventilation
  /// (<see cref="AuxiliaryVentilationRate"/> /
  /// <see cref="AuxiliaryVentilationCO2Level"/>) models any additional
  /// inflow with an arbitrary concentration: make-up air from corridors,
  /// inflow from unmodeled adjacent spaces, or — for an unbound zone — plain
  /// outdoor air. Multiple inflow sources can be aggregated into this single
  /// path without loss because the balance is linear: use the total flow and
  /// the flow-weighted mean concentration.
  /// </para>
  /// </remarks>
  public class CO2ModelZone : IReadOnlyCO2ModelZone
  {

    #region プロパティ

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public double Volume { get; }

    /// <inheritdoc />
    public IReadOnlyZone? BoundZone { get; }

    /// <summary>Gets or sets the CO2 concentration [m³/m³].</summary>
    public double CO2Level { get; set; } = 400e-6;

    /// <summary>Gets or sets the CO2 concentration [ppm].</summary>
    public double CO2Level_PPM
    {
      get { return CO2Level * 1e6; }
      set { CO2Level = value * 1e-6; }
    }

    /// <summary>Gets or sets the CO2 generation rate in the zone [m³/s].</summary>
    public double CO2Generation { get; set; }

    /// <summary>Gets or sets the auxiliary ventilation rate [m³/s].</summary>
    public double AuxiliaryVentilationRate { get; set; }

    /// <summary>Gets or sets the CO2 concentration of the auxiliary ventilation inflow air [m³/m³].</summary>
    public double AuxiliaryVentilationCO2Level { get; set; } = 400e-6;

    /// <summary>Gets or sets the CO2 concentration of the auxiliary ventilation inflow air [ppm].</summary>
    public double AuxiliaryVentilationCO2Level_PPM
    {
      get { return AuxiliaryVentilationCO2Level * 1e6; }
      set { AuxiliaryVentilationCO2Level = value * 1e-6; }
    }

    #endregion

    #region コンストラクタ

    /// <summary>Initializes a new instance without a thermal model binding.</summary>
    /// <param name="name">Zone name.</param>
    /// <param name="volume">Zone air volume [m³].</param>
    public CO2ModelZone(string name, double volume) : this(name, volume, null) { }

    /// <summary>Initializes a new instance.</summary>
    /// <param name="name">Zone name.</param>
    /// <param name="volume">Zone air volume [m³].</param>
    /// <param name="boundZone">
    /// Thermal model zone to bind, or null. When bound, the outdoor air
    /// ventilation rate and the inter-zone air flows are read from the
    /// thermal model at every update.
    /// </param>
    public CO2ModelZone(string name, double volume, IReadOnlyZone? boundZone)
    {
      if (volume <= 0)
        throw new PopoloArgumentException("volume must be positive.", nameof(volume));

      Name = name ?? string.Empty;
      Volume = volume;
      BoundZone = boundZone;
    }

    #endregion

  }
}
