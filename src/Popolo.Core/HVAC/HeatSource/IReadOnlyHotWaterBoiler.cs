/* IReadOnlyHotWaterBoiler.cs
 * Copyright (C) 2026 E.Togashi
 * GNU General Public License v3
 */

namespace Popolo.Core.HVAC.HeatSource
{
  /// <summary>Read-only view of a Hot-water boiler.</summary>
  public interface IReadOnlyHotWaterBoiler
  {
    
    /// <summary>Gets the fuel type.</summary>
    Boiler.Fuel Fuel { get; }

    /// <summary>Gets the outlet water temperature [°C].</summary>
    double OutletWaterTemperature { get; }

    /// <summary>Gets the outlet water temperature setpoint [°C].</summary>
    double OutletWaterSetpointTemperature { get; }

    /// <summary>Gets the inlet water temperature [°C].</summary>
    double InletWaterTemperature { get; }

    /// <summary>Gets the current water flow rate [kg/s].</summary>
    double WaterFlowRate { get; }

    /// <summary>Gets the maximum allowable water flow rate [kg/s].</summary>
    double MaxWaterFlowRate { get; }

    /// <summary>Gets the minimum water flow rate ratio [-].</summary>
    double MinWaterFlowRatio { get; }

    /// <summary>Gets the nominal heating capacity [kW].</summary>
    double NominalCapacity { get; }

    /// <summary>Gets the nominal fuel consumption rate [kg/s or Nm³/s].</summary>
    double NominalFuelConsumption { get; }

    /// <summary>Gets the electric power consumption [kW].</summary>
    double ElectricConsumption { get; }

    /// <summary>Gets the current fuel consumption rate [kg/s or Nm³/s].</summary>
    double FuelConsumption { get; }

    /// <summary>Gets the current heat output [kW].</summary>
    double HeatLoad { get; }

    /// <summary>Gets the ambient temperature [°C].</summary>
    double AmbientTemperature { get; }

    /// <summary>Gets the excess air ratio [-].</summary>
    double AirRatio { get; }

    /// <summary>Gets the coefficient of performance (primary energy basis) [-].</summary>
    double COP { get; }

    /// <summary>Gets a value indicating whether the boiler is overloaded.</summary>
    bool IsOverLoad { get; }
  }

}
