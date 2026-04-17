/* IReadOnlyVRFUnit.cs
 * Copyright (C) 2026 E.Togashi
 * GNU General Public License v3
 */

namespace Popolo.Core.HVAC.VRF
{
  /// <summary>Read-only view of a VRF indoor/outdoor heat exchanger unit.</summary>
  public interface IReadOnlyVRFUnit
  {
    /// <summary>Gets the selectable operating modes.</summary>
    VRFUnit.Mode SelectableMode { get; }

    /// <summary>Gets the current operating mode.</summary>
    VRFUnit.Mode CurrentMode { get; }

    /// <summary>Gets the nominal cooling capacity [kW] (negative = cooling, positive = heating).</summary>
    double NominalCoolingCapacity { get; }

    /// <summary>Gets the nominal heating capacity [kW] (positive = heating, negative = cooling).</summary>
    double NominalHeatingCapacity { get; }

    /// <summary>Gets the evaporator heat transfer surface area [m²].</summary>
    double SurfaceArea_Evaporator { get; }

    /// <summary>Gets the condenser heat transfer surface area [m²].</summary>
    double SurfaceArea_Condenser { get; }

    /// <summary>Gets the dry heat transfer surface area [m²].</summary>
    double DrySurfaceArea { get; }

    /// <summary>Gets the wet heat transfer surface area [m²].</summary>
    double WetSurfaceArea { get; }

    /// <summary>Gets the frosted heat transfer surface area [m²].</summary>
    double FrostSurfaceArea { get; }

    /// <summary>Gets the nominal air mass flow rate [kg/s].</summary>
    double NominalAirFlowRate { get; }

    /// <summary>Gets the current air mass flow rate [kg/s].</summary>
    double AirFlowRate { get; }

    /// <summary>Gets the refrigerant temperature [°C].</summary>
    double RefrigerantTemperature { get; }

    /// <summary>Gets the relative humidity at the dry/wet boundary [%].</summary>
    double BorderRelativeHumidity { get; }

    /// <summary>Gets the inlet air dry-bulb temperature [°C].</summary>
    double InletAirTemperature { get; }

    /// <summary>Gets the inlet air humidity ratio [kg/kg].</summary>
    double InletAirHumidityRatio { get; }

    /// <summary>Gets the outlet air dry-bulb temperature [°C].</summary>
    double OutletAirTemperature { get; }

    /// <summary>Gets the outlet air humidity ratio [kg/kg].</summary>
    double OutletAirHumidityRatio { get; }

    /// <summary>Gets the heat transfer rate [kW] (positive = heating, negative = cooling).</summary>
    double HeatTransfer { get; }

    /// <summary>Gets the sensible heat transfer rate [kW] (positive = heating, negative = cooling).</summary>
    double SensibleHeatTransfer { get; }
    
    /// <summary>Gets the latent heat transfer rate [kW] (positive = heating, negative = cooling).</summary>
    double LatentHeatTransfer { get; }

    /// <summary>Gets the defrost load [kW].</summary>
    double DefrostLoad { get; }

    /// <summary>Gets or sets a value indicating whether to apply water spray to the condenser.</summary>
    bool UseWaterSpray { get; }

    /// <summary>Gets the water consumption rate from condenser water spray [kg/s].</summary>
    double WaterSupply { get; }

    /// <summary>Gets the temperature reduction effectiveness of the condenser water spray [-].</summary>
    double SprayEffectiveness { get; }

    /// <summary>Gets the nominal fan electric power in cooling mode [kW].</summary>
    double NominalFanElectricity_C { get; }

    /// <summary>Gets the nominal fan electric power in heating mode [kW].</summary>
    double NominalFanElectricity_H { get; }

    /// <summary>Gets the current fan operating rate [-].</summary>
    double FanOperatingRate { get; }

    /// <summary>Gets the thermo-off time ratio [-].</summary>
    double ThermoOffRate { get; }

    /// <summary>Gets the current fan electric power [kW].</summary>
    double FanElectricity { get; }

    /// <summary>Gets the outlet air temperature setpoint [°C].</summary>
    double OutletAirSetpointTemperature { get; }

    /// <summary>Gets the outlet air humidity ratio setpoint [kg/kg].</summary>
    double OutletAirSetpointHumidityRatio { get; }

    /// <summary>Gets a value indicating whether humidification is active (effective in heating mode only).</summary>
    bool UseHumidifier { get; }

    /// <summary>Gets or sets a value indicating whether the fan uses inverter (speed) control.</summary>
    bool IsInverterControlledFan { get; }

  }

}
