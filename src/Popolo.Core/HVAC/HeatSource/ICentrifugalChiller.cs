using System;
using System.Collections.Generic;
using System.Text;

namespace Popolo.Core.HVAC.HeatSource
{
  /// <summary>Centrifugal (turbo) chiller.</summary>
  public interface ICentrifugalChiller: IReadOnlyCentrifugalChiller
  {

    /// <summary>Gets or sets a value indicating whether the chiller is operating.</summary>
    new bool IsOperating { get; set; }

    /// <summary>Gets or sets the chilled water outlet temperature setpoint [°C].</summary>
    new double ChilledWaterOutletSetpointTemperature { get; set; }

    /// <summary>Shuts off the chiller.</summary>
    void ShutOff();

    /// <summary>Updates the chiller state for the given inlet conditions.</summary>
    /// <param name="coolingWaterInletTemperature">Cooling water inlet temperature [°C].</param>
    /// <param name="chilledWaterInletTemperature">Chilled water inlet temperature [°C].</param>
    /// <param name="coolingWaterFlowRate">Cooling water mass flow rate [kg/s].</param>
    /// <param name="chilledWaterFlowRate">Chilled water mass flow rate [kg/s].</param>
    void Update
      (double coolingWaterInletTemperature, double chilledWaterInletTemperature, 
      double coolingWaterFlowRate, double chilledWaterFlowRate);

  }

  /// <summary>Read-only view of a centrifugal chiller.</summary>
  public interface IReadOnlyCentrifugalChiller
  {

    /// <summary>Gets or sets a value indicating whether the chiller is operating.</summary>
    bool IsOperating { get; }

    /// <summary>Gets the chilled water outlet temperature [°C].</summary>
    double ChilledWaterOutletTemperature { get; }

    /// <summary>Gets the chilled water outlet temperature setpoint [°C].</summary>
    double ChilledWaterOutletSetpointTemperature { get; }

    /// <summary>Gets the chilled water inlet temperature [°C].</summary>
    double ChilledWaterInletTemperature { get; }

    /// <summary>Gets the cooling water outlet temperature [°C].</summary>
    double CoolingWaterOutletTemperature { get; }

    /// <summary>Gets the cooling water inlet temperature [°C].</summary>
    double CoolingWaterInletTemperature { get; }

    /// <summary>Gets the chilled water flow rate [kg/s].</summary>
    double ChilledWaterFlowRate { get; }

    /// <summary>Gets the cooling water flow rate [kg/s].</summary>
    double CoolingWaterFlowRate { get; }

    /// <summary>Gets the nominal cooling capacity [kW].</summary>
    double NominalCapacity { get; }

    /// <summary>Gets the nominal power input [kW].</summary>
    double NominalInput { get; }

    /// <summary>Gets the nominal COP [-].</summary>
    double NominalCOP { get; }

    /// <summary>Gets the minimum partial load ratio for capacity control [-].</summary>
    double MinimumPartialLoadRatio { get; }

    /// <summary>Gets the electric power consumption [kW].</summary>
    double ElectricConsumption { get; }

    /// <summary>Gets the cooling load [kW].</summary>
    double CoolingLoad { get; }

    /// <summary>Gets the coefficient of performance [-].</summary>
    double COP { get; }

    /// <summary>Gets the maximum chilled water flow rate [kg/s].</summary>
    double MaxChilledWaterFlowRate { get; }

    /// <summary>Gets the minimum chilled water flow rate ratio [-].</summary>
    double MinChilledWaterFlowRatio { get; }

    /// <summary>Gets a value indicating whether the chiller is overloaded.</summary>
    bool IsOverLoad { get; }

  }

}
