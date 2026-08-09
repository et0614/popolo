/* IReadOnlyEnergyRecoveryVentilator.cs
 * Copyright (C) 2026 E.Togashi
 * GNU General Public License v3
 */

using Popolo.Core.HVAC.FluidCircuit;
using Popolo.Core.HVAC.HeatExchanger;

namespace Popolo.Core.HVAC.AirSide
{
  /// <summary>Read-only view of an energy recovery ventilator (ERV).</summary>
  public interface IReadOnlyEnergyRecoveryVentilator
  {
    /// <summary>Gets the air-to-air fixed-plate heat exchanger.</summary>
    IReadOnlyAirToAirFlatPlateHeatExchanger HeatExchanger { get; }

    /// <summary>Gets the supply air fan.</summary>
    IReadOnlyFluidMachinery SupplyAirFan { get; }

    /// <summary>Gets the exhaust air fan.</summary>
    IReadOnlyFluidMachinery ExhaustAirFan { get; }

    /// <summary>Gets a value indicating whether the heat exchanger is bypassed.</summary>
    bool BypassHeatExchanger { get; }

    /// <summary>Gets the outdoor air dry-bulb temperature [°C].</summary>
    double OATemperature { get; }

    /// <summary>Gets the outdoor air humidity ratio [kg/kg].</summary>
    double OAHumidityRatio { get; }

    /// <summary>Gets the return air dry-bulb temperature [°C].</summary>
    double RATemperature { get; }

    /// <summary>Gets the return air humidity ratio [kg/kg].</summary>
    double RAHumidityRatio { get; }

    /// <summary>Gets the supply air flow rate [kg/s].</summary>
    double SAFlowRate { get; }

    /// <summary>Gets the exhaust air flow rate [kg/s].</summary>
    double EAFlowRate { get; }

    /// <summary>Gets the supply air dry-bulb temperature [°C].</summary>
    double SATemperature { get; }

    /// <summary>Gets the supply air humidity ratio [kg/kg].</summary>
    double SAHumidityRatio { get; }

    /// <summary>Gets the exhaust air outlet dry-bulb temperature [°C].</summary>
    double EATemperature { get; }

    /// <summary>Gets the exhaust air outlet humidity ratio [kg/kg].</summary>
    double EAHumidityRatio { get; }

    /// <summary>Gets the number of discrete air flow notches (0 when not configured).</summary>
    int NotchCount { get; }

    /// <summary>Gets the current notch index (-1 when not operating on a notch).</summary>
    int CurrentNotchIndex { get; }

    /// <summary>Gets the current notch name (empty when not operating on a notch).</summary>
    string CurrentNotchName { get; }
  }
}
