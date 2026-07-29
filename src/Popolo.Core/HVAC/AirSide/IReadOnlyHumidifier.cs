/* IReadOnlyHumidifier.cs
 * Copyright (C) 2026 E.Togashi
 * GNU General Public License v3
 */

namespace Popolo.Core.HVAC.AirSide
{
  /// <summary>Read-only view of a humidifier.</summary>
  public interface IReadOnlyHumidifier
  {
    /// <summary>Gets the humidification method.</summary>
    Humidifier.HumidifierType Type { get; }

    /// <summary>
    /// Gets a value indicating whether the humidification process is adiabatic
    /// (water humidification along a constant-enthalpy line). <c>false</c> for
    /// steam humidification (constant dry-bulb temperature).
    /// </summary>
    bool IsAdiabatic { get; }

    /// <summary>Gets the maximum saturation efficiency [-].</summary>
    double MaxSaturationEfficiency { get; }

    /// <summary>Gets the current saturation efficiency [-].</summary>
    double SaturationEfficiency { get; }

    /// <summary>Gets the effective water use ratio of the supplied water [-].</summary>
    double WaterSupplyCoefficient { get; }

    /// <summary>Gets the inlet air dry-bulb temperature [°C].</summary>
    double InletAirTemperature { get; }

    /// <summary>Gets the inlet air humidity ratio [kg/kg].</summary>
    double InletAirHumidityRatio { get; }

    /// <summary>Gets the outlet air dry-bulb temperature [°C].</summary>
    double OutletAirTemperature { get; }

    /// <summary>Gets the outlet air humidity ratio [kg/kg].</summary>
    double OutletAirHumidityRatio { get; }

    /// <summary>Gets the air mass flow rate [kg/s].</summary>
    double AirFlowRate { get; }

    /// <summary>Gets the feed water consumption rate [kg/s] (water humidification).</summary>
    double WaterConsumption { get; }

    /// <summary>Gets the steam consumption rate [kg/s] (steam humidification).</summary>
    double SteamConsumption { get; }
  }
}
