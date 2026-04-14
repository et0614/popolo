/* IReadOnlyAirToAirFlatPlateHeatExchanger.cs
 *
 * Copyright (C) 2026 E.Togashi
 *
 * GNU General Public License v3 — see accompanying LICENSE file.
 */

namespace Popolo.Core.HVAC.HeatExchanger
{

  /// <summary>Read-only view of an air-to-air fixed-plate heat exchanger.</summary>
  public interface IReadOnlyAirToAirFlatPlateHeatExchanger
  {
    /// <summary>Gets a value indicating whether this is a total heat exchanger (sensible + latent).</summary>
    bool IsTotalHeatExchanger { get; }

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

    /// <summary>Gets the sensible heat exchange efficiency [-].</summary>
    double SensibleEfficiency { get; }

    /// <summary>Gets the latent heat exchange efficiency [-].</summary>
    double LatentEfficiency { get; }

    /// <summary>Gets the air flow arrangement type.</summary>
    AirToAirFlatPlateHeatExchanger.AirFlow Flow { get; }

    /// <summary>Computes the total heat exchange efficiency [-] from sensible and latent effectivenesses.</summary>
    /// <returns>Total heat exchange efficiency [-].</returns>
    double GetTotalEfficiency();
  }

}
