/* IReadOnlyRotaryRegenerator.cs
 * Copyright (C) 2026 E.Togashi
 * GNU General Public License v3
 */

namespace Popolo.Core.HVAC.HeatExchanger
{
  /// <summary>Read-only view of a rotary heat regenerator.</summary>
  public interface IReadOnlyRotaryRegenerator
  {

    /// <summary>Gets a value indicating whether the detailed geometric model is used.</summary>
    bool IsDetailedModel { get; }

    /// <summary>Gets a value indicating whether this is a total (enthalpy) heat exchanger.</summary>
    bool IsDesiccantWheel { get; }

    /// <summary>Gets the rotor diameter [m].</summary>
    double Diameter { get; }

    /// <summary>Gets the rotor depth [m].</summary>
    double Depth { get; }

    /// <summary>Gets the supply air volumetric flow rate [m³/h].</summary>
    double SupplyAirFlowVolume { get; }

    /// <summary>Gets the exhaust air volumetric flow rate [m³/h].</summary>
    double ExhaustAirFlowVolume { get; }

    /// <summary>Gets the supply air inlet dry-bulb temperature [°C].</summary>
    double SupplyAirInletDrybulbTemperature { get; }

    /// <summary>Gets the exhaust air inlet dry-bulb temperature [°C].</summary>
    double ExhaustAirInletDrybulbTemperature { get; }

    /// <summary>Gets the supply air outlet dry-bulb temperature [°C].</summary>
    double SupplyAirOutletDrybulbTemperature { get; }

    /// <summary>Gets the exhaust air outlet dry-bulb temperature [°C].</summary>
    double ExhaustAirOutletDrybulbTemperature { get; }

    /// <summary>Gets the supply air inlet humidity ratio [kg/kg].</summary>
    double SupplyAirInletHumidityRatio { get; }

    /// <summary>Gets the exhaust air inlet humidity ratio [kg/kg].</summary>
    double ExhaustAirInletHumidityRatio { get; }

    /// <summary>Gets the supply air outlet humidity ratio [kg/kg].</summary>
    double SupplyAirOutletHumidityRatio { get; }

    /// <summary>Gets the exhaust air outlet humidity ratio [kg/kg].</summary>
    double ExhaustAirOutletHumidityRatio { get; }

    /// <summary>Gets the supply-air-side heat exchange efficiency εSA [-].</summary>
    double Efficiency { get; }

    /// <summary>Gets the nominal power consumption [kW].</summary>
    double NominalElectricity { get; }

    /// <summary>Gets the current power consumption [kW].</summary>
    double Electricity { get; }

    /// <summary>Computes the heat recovery rate [kW].</summary>
    /// <returns>Heat recovery rate [kW] (positive = supply air heated).</returns>
    double GetHeatRecovery();
  }
}
