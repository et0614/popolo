/* IReadOnlyCO2ModelZone.cs
 * Copyright (C) 2026 E.Togashi
 * GNU General Public License v3
 */

namespace Popolo.Core.Building.AirQuality
{
  /// <summary>Read-only view of a zone in a <see cref="MultiZoneCO2Model"/>.</summary>
  public interface IReadOnlyCO2ModelZone
  {
    /// <summary>Gets the zone name.</summary>
    string Name { get; }

    /// <summary>Gets the zone air volume [m³].</summary>
    double Volume { get; }

    /// <summary>
    /// Gets the thermal model zone bound to this CO2 zone. Null when the
    /// zone is not bound; inter-zone air flows and the ventilation rate are
    /// then not read from the thermal model.
    /// </summary>
    IReadOnlyZone? BoundZone { get; }

    /// <summary>Gets the CO2 concentration [m³/m³].</summary>
    double CO2Level { get; }

    /// <summary>Gets the CO2 concentration [ppm].</summary>
    double CO2Level_PPM { get; }

    /// <summary>Gets the CO2 generation rate in the zone [m³/s].</summary>
    double CO2Generation { get; }

    /// <summary>Gets the auxiliary ventilation rate [m³/s].</summary>
    double AuxiliaryVentilationRate { get; }

    /// <summary>Gets the CO2 concentration of the auxiliary ventilation inflow air [m³/m³].</summary>
    double AuxiliaryVentilationCO2Level { get; }

    /// <summary>Gets the CO2 concentration of the auxiliary ventilation inflow air [ppm].</summary>
    double AuxiliaryVentilationCO2Level_PPM { get; }
  }
}
