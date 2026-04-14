/* IReadOnlyCrossFinEvaporator.cs
 * Copyright (C) 2026 E.Togashi
 * GNU General Public License v3
 */

namespace Popolo.Core.HVAC.HeatExchanger
{
  /// <summary>Read-only view of a cross-fin evaporator.</summary>
  public interface IReadOnlyCrossFinEvaporator
  {
    /// <summary>Gets the total heat transfer surface area [m²].</summary>
    double SurfaceArea { get; }

    /// <summary>Gets the dry surface area [m²].</summary>
    double DrySurfaceArea { get; }

    /// <summary>Gets the wet surface area [m²].</summary>
    double WetSurfaceArea { get; }

    /// <summary>Gets the frosted surface area [m²].</summary>
    double FrostSurfaceArea { get; }

    /// <summary>Gets the nominal air flow rate [kg/s].</summary>
    double NominalAirFlowRate { get; }

    /// <summary>Gets the current air flow rate [kg/s].</summary>
    double AirFlowRate { get; }

    /// <summary>Gets the evaporating temperature [°C].</summary>
    double EvaporatingTemperature { get; }

    /// <summary>Gets the dry/wet boundary relative humidity [%].</summary>
    double BorderRelativeHumidity { get; }

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

    /// <summary>Gets the defrost load [kW].</summary>
    double DefrostLoad { get; }

  }  
}
