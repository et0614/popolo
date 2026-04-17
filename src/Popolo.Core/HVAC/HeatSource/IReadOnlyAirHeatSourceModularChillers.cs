/* IReadOnlyAirHeatSourceModularChillers.cs
 * Copyright (C) 2026 E.Togashi
 * GNU General Public License v3
 */

namespace Popolo.Core.HVAC.HeatSource
{
  /// <summary>Read-only view of an air-heat-source modular chiller/heat-pump.</summary>
  public interface IReadOnlyAirHeatSourceModularChillers
  {
    /// <summary>Gets the current operating mode.</summary>
    AirHeatSourceModularChillers.OperatingMode Mode { get; }

    /// <summary>Gets or sets a value indicating whether to maximise operating efficiency by adjusting the number of active units.</summary>
    bool MaximizeEfficiency { get; }

    /// <summary>Gets the total number of modules.</summary>
    int UnitCount { get; }

    /// <summary>Gets the number of currently operating units.</summary>
    int ActiveUnitCount { get; }

    /// <summary>Gets a value indicating whether the unit is a heat-pump model (supports both heating and cooling).</summary>
    bool IsHeatPumpModel { get; }

    /// <summary>Gets the nominal cooling capacity per unit [kW].</summary>
    double NominalCoolingCapacity { get; }

    /// <summary>Gets the nominal cooling COP [-].</summary>
    double NominalCoolingCOP { get; }

    /// <summary>Gets the nominal heating capacity per unit [kW].</summary>
    double NominalHeatingCapacity { get; }

    /// <summary>Gets the nominal heating COP [-].</summary>
    double NominalHeatingCOP { get; }

    /// <summary>Gets the water outlet temperature [°C].</summary>
    double WaterOutletTemperature { get; }

    /// <summary>Gets the water outlet temperature setpoint [°C].</summary>
    double WaterOutletSetpointTemperature { get; }

    /// <summary>Gets the water inlet temperature [°C].</summary>
    double WaterInletTemperature { get; }

    /// <summary>Gets the water flow rate per unit [kg/s].</summary>
    double WaterFlowRate { get; }

    /// <summary>Gets the air mass flow rate per unit [kg/s].</summary>
    double AirFlowRate { get; }

    /// <summary>Gets the electric power consumption per unit [kW].</summary>
    double ElectricConsumption { get; }

    /// <summary>Gets the auxiliary electric power consumption per unit [kW].</summary>
    double AuxiliaryElectricConsumption { get; }

    /// <summary>Gets the ambient air dry-bulb temperature [°C].</summary>
    double AmbientTemperature { get; }

    /// <summary>Gets the coefficient of performance [-].</summary>
    double COP { get; }

    /// <summary>Gets the total cooling output [kW].</summary>
    double CoolingLoad { get; }

    /// <summary>Gets the total heating output [kW].</summary>
    double HeatingLoad { get; }

    /// <summary>Gets the maximum chilled water volume flow rate [kg/s].</summary>
    double MaxChilledWaterFlowRate { get; }

    /// <summary>Gets the maximum hot water volume flow rate [kg/s].</summary>
    double MaxHotWaterFlowRate { get; }

    /// <summary>Gets a value indicating whether the unit is overloaded.</summary>
    bool IsOverLoad { get; }
  }

}
