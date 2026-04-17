/* IReadOnlyDirectFiredAbsorptionChiller.cs
 * Copyright (C) 2026 E.Togashi
 * GNU General Public License v3
 */

namespace Popolo.Core.HVAC.HeatSource
{
  /// <summary>Read-only view of a direct-fired double-effect absorption chiller/heater.</summary>
  public interface IReadOnlyDirectFiredAbsorptionChiller
  {
    /// <summary>Gets a value indicating whether the unit is in cooling mode.</summary>
    bool IsCoolingMode { get; }

    /// <summary>Gets the fuel type.</summary>
    Boiler.Fuel Fuel { get; }

    /// <summary>Gets the outlet water temperature [°C].</summary>
    double OutletWaterTemperature { get; }

    /// <summary>Gets the outlet water temperature setpoint [°C].</summary>
    double OutletWaterSetpointTemperature { get; }

    /// <summary>Gets the inlet water temperature [°C].</summary>
    double InletWaterTemperature { get; }

    /// <summary>Gets the cooling water outlet temperature [°C].</summary>
    double CoolingWaterOutletTemperature { get; }

    /// <summary>Gets the cooling water inlet temperature [°C].</summary>
    double CoolingWaterInletTemperature { get; }

    /// <summary>Gets the chilled/hot water flow rate [kg/s].</summary>
    double WaterFlowRate { get; }

    /// <summary>Gets the cooling water flow rate [kg/s].</summary>
    double CoolingWaterFlowRate { get; }

    /// <summary>Gets the maximum chilled water volume flow rate [kg/s].</summary>
    double MaxChilledWaterFlowRate { get; }

    /// <summary>Gets the maximum hot water volume flow rate [kg/s].</summary>
    double MaxHotWaterFlowRate { get; }

    /// <summary>Gets the minimum chilled/hot water flow rate [kg/s].</summary>
    double MinChilledWaterFlowRate { get; }

    /// <summary>Gets the maximum cooling water volume flow rate [kg/s].</summary>
    double MaxCoolingWaterFlowRate { get; }

    /// <summary>Gets the minimum cooling water volume flow rate [kg/s].</summary>
    double MinCoolingWaterFlowRate { get; }

    /// <summary>Gets the nominal cooling capacity [kW].</summary>
    double NominalCoolingCapacity { get; }

    /// <summary>Gets the nominal heating capacity [kW].</summary>
    double NominalHeatingCapacity { get; }

    /// <summary>Gets the nominal cooling fuel consumption rate [Nm³/s or kg/s].</summary>
    double NominalCoolingFuelConsumption { get; }

    /// <summary>Gets the nominal heating fuel consumption rate [Nm³/s or kg/s].</summary>
    double NominalHeatingFuelConsumption { get; }

    /// <summary>Gets the minimum partial load ratio for capacity control [-].</summary>
    double MinimumPartialLoadRatio { get; }

    /// <summary>Gets the nominal electric power consumption [kW].</summary>
    double NominalElectricConsumption { get; }

    /// <summary>Gets the electric power consumption [kW].</summary>
    double ElectricConsumption { get; }

    /// <summary>Gets the fuel consumption rate [Nm³/s or kg/s].</summary>
    double FuelConsumption { get; }

    /// <summary>Gets the heating load [kW].</summary>
    double HeatingLoad { get; }

    /// <summary>Gets the cooling load [kW].</summary>
    double CoolingLoad { get; }

    /// <summary>Gets the coefficient of performance [-].</summary>
    double COP { get; }

    /// <summary>Gets or sets a value indicating whether the solution pump has inverter control.</summary>
    bool HasSolutionInverterPump { get; }

    /// <summary>Gets the concentrated (thick) solution mass fraction [-].</summary>
    double ThickSolutionMassFraction { get; }

    /// <summary>Gets the dilute (thin) solution mass fraction [-].</summary>
    double ThinSolutionMassFraction { get; }

    /// <summary>Gets the evaporating temperature [°C].</summary>
    double EvaporatingTemperature { get; }

    /// <summary>Gets the condensing temperature [°C].</summary>
    double CondensingTemperature { get; }

    /// <summary>Gets the desorption temperature [°C].</summary>
    double DesorbTemperature { get; }

    /// <summary>Gets a value indicating whether the unit is overloaded.</summary>
    bool IsOverLoad { get; }
  }
}
