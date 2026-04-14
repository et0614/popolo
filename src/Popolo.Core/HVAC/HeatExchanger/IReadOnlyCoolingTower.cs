/* IReadOnlyCoolingTower.cs
 *
 * Copyright (C) 2026 E.Togashi
 * GNU General Public License v3 — see accompanying LICENSE file.
 */

namespace Popolo.Core.HVAC.HeatExchanger
{
  /// <summary>Read-only view of a cooling tower.</summary>
  public interface IReadOnlyCoolingTower
  {
    /// <summary>Gets the circulating water flow rate [kg/s].</summary>
    double WaterFlowRate { get; }

    /// <summary>Gets the maximum allowable water flow rate [kg/s].</summary>
    double MaxWaterFlowRate { get; }

    /// <summary>Gets the current air flow rate [kg/s].</summary>
    double AirFlowRate { get; }

    /// <summary>Gets the nominal air flow rate [kg/s].</summary>
    double MaxAirFlowRate { get; }

    /// <summary>Gets the outdoor wet-bulb temperature [°C].</summary>
    double OutdoorWetbulbTemperature { get; }

    /// <summary>Gets the outdoor humidity ratio [kg/kg].</summary>
    double OutdoorHumidityRatio { get; }

    /// <summary>Gets the inlet water temperature [°C].</summary>
    double InletWaterTemperature { get; }

    /// <summary>Gets the outlet water temperature [°C].</summary>
    double OutletWaterTemperature { get; }

    /// <summary>Gets the outlet water temperature setpoint [°C].</summary>
    double OutletWaterSetPointTemperature { get; }

    /// <summary>Gets the heat rejection rate [kW].</summary>
    double HeatRejection { get; }

    /// <summary>Gets the concentration ratio [-].</summary>
    double ConcentrationRatio { get; }

    /// <summary>Gets the drift water rate [-].</summary>
    double DriftWaterRate { get; }

    /// <summary>True if the fan has an inverter drive.</summary>
    bool HasInverter { get; }

    /// <summary>Gets the air flow direction type.</summary>
    CoolingTower.AirFlowDirection AirFlowType { get; }

    /// <summary>Gets the nominal (rated) fan power consumption [kW].</summary>
    double NominalPowerConsumption { get; }

    /// <summary>Gets the fan power consumption [kW].</summary>
    double ElectricConsumption { get; }

    /// <summary>Gets the water consumption rate due to evaporation [kg/s].</summary>
    double EvaporationWater { get; }

    /// <summary>Gets the water consumption rate due to drift [kg/s].</summary>
    double DriftWater { get; }

    /// <summary>Gets the water consumption rate due to blowdown [kg/s].</summary>
    double BlowDownWater { get; }

    /// <summary>Gets the total water consumption rate (evaporation + drift + blowdown) [kg/s].</summary>
    double WaterConsumption { get; }

    /// <summary>Gets the minimum inverter rotation ratio [-].</summary>
    double MinimumRotationRatio { get; }

    /// <summary>Gets the current inverter rotation ratio [-].</summary>
    double RotationRatio { get; }
  }

}
