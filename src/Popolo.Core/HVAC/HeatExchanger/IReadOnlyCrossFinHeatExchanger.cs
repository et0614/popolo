/* IReadOnlyCrossFinHeatExchanger.cs
 * Copyright (C) 2026 E.Togashi
 * GNU General Public License v3
 */

namespace Popolo.Core.HVAC.HeatExchanger
{
  /// <summary>Read-only view of a cross-fin heat exchanger.</summary>
  public interface IReadOnlyCrossFinHeatExchanger
  {

    /// <summary>Gets the relative humidity at the dry/wet boundary [%].</summary>
    double BorderRelativeHumidity { get; }

    /// <summary>Gets the maximum water flow rate [kg/s].</summary>
    double MaxWaterFlowRate { get; }

    /// <summary>Gets the nominal water flow rate [kg/s].</summary>
    double RatedWaterFlowRate { get; }

    /// <summary>Gets the current water flow rate [kg/s].</summary>
    double WaterFlowRate { get; }

    /// <summary>Gets the current air flow rate [kg/s].</summary>
    double AirFlowRate { get; }

    /// <summary>Gets the nominal air flow rate [kg/s].</summary>
    double RatedAirFlowRate { get; }

    /// <summary>Gets the inlet air dry-bulb temperature [°C].</summary>
    double InletAirTemperature { get; }

    /// <summary>Gets the inlet air humidity ratio [kg/kg].</summary>
    double InletAirHumidityRatio { get; }

    /// <summary>Gets the outlet air dry-bulb temperature [°C].</summary>
    double OutletAirTemperature { get; }

    /// <summary>Gets the outlet air humidity ratio [kg/kg].</summary>
    double OutletAirHumidityRatio { get; }

    /// <summary>Gets the inlet water temperature [°C].</summary>
    double InletWaterTemperature { get; }

    /// <summary>Gets the outlet water temperature [°C].</summary>
    double OutletWaterTemperature { get; }

    /// <summary>Gets the heat transfer surface area [m²].</summary>
    double SurfaceArea { get; }

    /// <summary>Gets the dry coil fraction [-].</summary>
    double DryRate { get; }

    /// <summary>Gets the overall heat transfer coefficient for the dry coil [kW/(m²·K)].</summary>
    double DryHeatTransferCoefficient { get; }

    /// <summary>Gets the overall heat transfer coefficient for the wet coil [kW/(m²·(kJ/kg))].</summary>
    double WetHeatTransferCoefficient { get; }

    /// <summary>Gets the surface area correction factor [-].</summary>
    double CorrectionFactor { get; }

    /// <summary>Gets the heat transfer rate [kW].</summary>
    double HeatTransfer { get; }
  }
}
