/* IReadOnlyHumidifierUnit.cs
 * Copyright (C) 2026 E.Togashi
 * GNU General Public License v3
 */

using Popolo.Core.HVAC.FluidCircuit;

namespace Popolo.Core.HVAC.AirSide
{
  /// <summary>Read-only view of a humidifier unit (fan + humidifier).</summary>
  public interface IReadOnlyHumidifierUnit
  {
    /// <summary>Gets the fan.</summary>
    IReadOnlyFluidMachinery Fan { get; }

    /// <summary>Gets the humidifier.</summary>
    IReadOnlyHumidifier Humidifier { get; }

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

    /// <summary>Gets the number of discrete air flow notches (0 when not configured).</summary>
    int NotchCount { get; }

    /// <summary>Gets the current notch index (-1 when not operating on a notch).</summary>
    int CurrentNotchIndex { get; }

    /// <summary>Gets the current notch name (empty when not operating on a notch).</summary>
    string CurrentNotchName { get; }
  }
}
