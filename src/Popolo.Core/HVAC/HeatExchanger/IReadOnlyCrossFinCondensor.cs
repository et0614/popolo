/* IReadOnlyCrossFinCondensor.cs
 * Copyright (C) 2026 E.Togashi
 * GNU General Public License v3
 */

namespace Popolo.Core.HVAC.HeatExchanger
{
  /// <summary>Read-only view of a cross-fin air-cooled condenser.</summary>
  public interface IReadOnlyCrossFinCondensor
  {
    /// <summary>Gets the heat transfer surface area [m²].</summary>
    double SurfaceArea { get; }

    /// <summary>Gets the nominal air flow rate [kg/s].</summary>
    double NominalAirFlowRate { get; }

    /// <summary>Gets the current air flow rate [kg/s].</summary>
    double AirFlowRate { get; }

    /// <summary>Gets the condensing temperature [°C].</summary>
    double CondensingTemperature { get; }

    /// <summary>Gets the inlet air dry-bulb temperature [°C].</summary>
    double InletAirTemperature { get; }

    /// <summary>Gets the inlet air humidity ratio [kg/kg].</summary>
    double InletAirHumidityRatio { get; }

    /// <summary>Gets the outlet air dry-bulb temperature [°C].</summary>
    double OutletAirTemperature { get; }

    /// <summary>Gets the outlet air humidity ratio [kg/kg].</summary>
    double OutletAirHumidityRatio { get; }

    /// <summary>Gets the heat transfer rate [kW].</summary>
    double HeatTransfer { get; }

    /// <summary>Gets the water spray temperature reduction effectiveness [-].</summary>
    double SprayEffectiveness { get; }

    /// <summary>Gets the water consumption rate due to spray [kg/s].</summary>
    double WaterSupply { get; }

    /// <summary>Gets a value indicating whether the condenser is shut off.</summary>
    bool IsShutOff { get; }

    /// <summary>Gets a value indicating whether water spray is active.</summary>
    bool UseWaterSpray { get; }
  }

}
