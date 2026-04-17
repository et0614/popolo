/* IReadOnlyWaterHeatPump.cs
 * Copyright (C) 2026 E.Togashi
 * GNU General Public License v3
 */

namespace Popolo.Core.HVAC.HeatSource
{
  /// <summary>Read-only view of a water-source heat pump.</summary>
  public interface IReadOnlyWaterHeatPump
  {
    /// <summary>Gets the current operating mode.</summary>
    WaterHeatPump.OperatingMode Mode { get; }

    /// <summary>Gets a value indicating whether the unit is overloaded.</summary>
    bool IsOverLoad { get; }

    /// <summary>Gets the nominal cooling capacity [kW].</summary>
    double NominalCoolingCapacity { get; }

    /// <summary>Gets the nominal chilled water mass flow rate [kg/s].</summary>
    double NominalChilledWaterFlowRate { get; }

    /// <summary>Gets the nominal cooling water mass flow rate [kg/s].</summary>
    double NominalCoolingWaterFlowRate { get; }

    /// <summary>Gets the nominal power consumption in cooling mode [kW].</summary>
    double NominalCoolingEnergyConsumption { get; }

    /// <summary>Gets the nominal heating capacity [kW].</summary>
    double NominalHeatingCapacity { get; }

    /// <summary>Gets the nominal hot water mass flow rate [kg/s].</summary>
    double NominalHotWaterFlowRate { get; }

    /// <summary>Gets the nominal heat-source water mass flow rate [kg/s].</summary>
    double NominalHeatSourceWaterFlowRate { get; }

    /// <summary>Gets the nominal power consumption in heating mode [kW].</summary>
    double NominalHeatingEnergyConsumption { get; }

    /// <summary>Gets the maximum cooling capacity [kW] at current conditions.</summary>
    double MaxCoolingCapacity { get; }

    /// <summary>Gets the cooling load [kW].</summary>
    double CoolingLoad { get; }

    /// <summary>Gets the chilled water mass flow rate [kg/s].</summary>
    double ChilledWaterFlowRate { get; }

    /// <summary>Gets the cooling water mass flow rate [kg/s].</summary>
    double CoolingWaterFlowRate { get; }

    /// <summary>Gets the chilled water outlet temperature setpoint [°C].</summary>
    double ChilledWaterSetpoint { get; }

    /// <summary>Gets the chilled water inlet temperature [°C].</summary>
    double ChilledWaterInletTemperature { get; }

    /// <summary>Gets the chilled water outlet temperature [°C].</summary>
    double ChilledWaterOutletTemperature { get; }

    /// <summary>Gets the cooling water inlet temperature [°C].</summary>
    double CoolingWaterInletTemperature { get; }

    /// <summary>Gets the cooling water outlet temperature [°C].</summary>
    double CoolingWaterOutletTemperature { get; }

    /// <summary>Gets the maximum heating capacity [kW] at current conditions.</summary>
    double MaxHeatingCapacity { get; }

    /// <summary>Gets the heating load [kW].</summary>
    double HeatingLoad { get; }

    /// <summary>Gets the hot water mass flow rate [kg/s].</summary>
    double HotWaterFlowRate { get; }

    /// <summary>Gets the heat-source water mass flow rate [kg/s].</summary>
    double HeatSourceWaterFlowRate { get; }

    /// <summary>Gets the hot water outlet temperature setpoint [°C].</summary>
    double HotWaterSetpoint { get; }

    /// <summary>Gets the hot water inlet temperature [°C].</summary>
    double HotWaterInletTemperature { get; }

    /// <summary>Gets the hot water outlet temperature [°C].</summary>
    double HotWaterOutletTemperature { get; }

    /// <summary>Gets the heat-source water inlet temperature [°C].</summary>
    double HeatSourceWaterInletTemperature { get; }

    /// <summary>Gets the heat-source water outlet temperature [°C].</summary>
    double HeatSourceWaterOutletTemperature { get; }

    /// <summary>Gets the current electric power consumption [kW].</summary>
    double EnergyConsumption { get; }

    /// <summary>Gets the coefficient of performance [-].</summary>
    double COP { get; }

  }
}
