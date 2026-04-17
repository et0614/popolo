/* IReadOnlyAirHandlingUnit.cs
 * Copyright (C) 2026 E.Togashi
 * GNU General Public License v3
 */

using Popolo.Core.HVAC.HeatExchanger;
using Popolo.Core.HVAC.FluidCircuit;

namespace Popolo.Core.HVAC.AirSide
{
  /// <summary>Read-only view of an air handling unit.</summary>
  public interface IReadOnlyAirHandlingUnit
  {
    /// <summary>Gets the outdoor air economiser control mode.</summary>
    AirHandlingUnit.OutdoorAirCoolingControl OutdoorAirCooling { get; }

    /// <summary>Gets the humidifier type.</summary>
    AirHandlingUnit.HumidifierType Humidifier { get; }

    /// <summary>Gets the cooling coil.</summary>
    IReadOnlyCrossFinHeatExchanger CoolingCoil { get; }

    /// <summary>Gets the heating coil.</summary>
    IReadOnlyCrossFinHeatExchanger HeatingCoil { get; }

    /// <summary>Gets the supply air fan.</summary>
    IReadOnlyFluidMachinery SupplyAirFan { get; }

    /// <summary>Gets the return air fan.</summary>
    IReadOnlyFluidMachinery ReturnAirFan { get; }

    /// <summary>Gets the rotary heat recovery wheel.</summary>
    IReadOnlyRotaryRegenerator? Regenerator { get; }

    /// <summary>Gets the duct heat loss rate [-].</summary>
    double DuctHeatLossRate { get; } 

    /// <summary>Gets a value indicating whether the heat recovery wheel is bypassed.</summary>
    bool BypassRegenerator { get; }

    /// <summary>Gets the maximum outdoor air flow rate [kg/s].</summary>
    double MaxOAFlowRate { get; }

    /// <summary>Gets the minimum outdoor air flow rate [kg/s].</summary>
    double MinOAFlowRate { get; }

    /// <summary>Gets the outdoor air intake flow rate [kg/s].</summary>
    double OAFlowRate { get; }

    /// <summary>Gets the return air flow rate [kg/s].</summary>
    double RAFlowRate { get; }

    /// <summary>Gets the supply air flow rate [kg/s].</summary>
    double SAFlowRate { get; }

    /// <summary>Gets the exhaust air flow rate [kg/s].</summary>
    double EAFlowRate { get; }

    /// <summary>Gets the return air dry-bulb temperature [°C].</summary>
    double RATemperature { get; }

    /// <summary>Gets the return air humidity ratio [kg/kg].</summary>
    double RAHumidityRatio { get; }

    /// <summary>Gets the outdoor air dry-bulb temperature [°C].</summary>
    double OATemperature { get; }

    /// <summary>Gets the outdoor air humidity ratio [kg/kg].</summary>
    double OAHumidityRatio { get; }

    /// <summary>Gets the supply air dry-bulb temperature [°C].</summary>
    double SATemperature { get; }

    /// <summary>Gets the supply air humidity ratio [kg/kg].</summary>
    double SAHumidityRatio { get; }

    /// <summary>Gets the chilled water inlet temperature [°C].</summary>
    double ChilledWaterInletTemperature { get; }

    /// <summary>Gets the hot water inlet temperature [°C].</summary>
    double HotWaterInletTemperature { get; }

    /// <summary>Gets the water consumption rate for humidification [kg/s].</summary>
    double WaterConsumption { get; }

    /// <summary>Gets the steam consumption rate for humidification [kg/s].</summary>
    double SteamConsumption { get; }

    /// <summary>Gets the water supply efficiency of the humidifier [-].</summary>
    double WaterSupplyCoefficient { get; }

    /// <summary>Gets the maximum humidifier saturation efficiency [-].</summary>
    double MaxSaturationEfficiency { get; }

    /// <summary>Gets the humidifier saturation efficiency [-].</summary>
    double SaturationEfficiency { get; }

    /// <summary>Gets or sets a value indicating whether to minimise airflow even at the cost of over-heating or over-cooling.</summary>
    bool MinimizeAirFlow { get; }

    /// <summary>Gets the supply air upper temperature limit in cooling mode [°C].</summary>
    double UpperTemperatureLimit_C { get; }

    /// <summary>Gets the supply air lower temperature limit in cooling mode [°C].</summary>
    double LowerTemperatureLimit_C { get; }

    /// <summary>Gets the supply air upper temperature limit in heating mode [°C].</summary>
    double UpperTemperatureLimit_H { get; }

    /// <summary>Gets the supply air lower temperature limit in heating mode [°C].</summary>
    double LowerTemperatureLimit_H { get; }
  }
}
