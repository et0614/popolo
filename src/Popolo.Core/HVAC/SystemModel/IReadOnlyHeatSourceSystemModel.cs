/* IReadOnlyHeatSourceSystemModel.cs
 * Copyright (C) 2026 E.Togashi
 * GNU General Public License v3
 */

using System;

using Popolo.Core.Physics;
using Popolo.Core.HVAC.FluidCircuit;

namespace Popolo.Core.HVAC.SystemModel
{
  /// <summary>Read-only view of the primary heat source system.</summary>
  public interface IReadOnlyHeatSourceSystemModel
  {
    /// <summary>Gets the chilled water supply temperature setpoint [°C].</summary>
    double ChilledWaterSupplyTemperatureSetpoint { get; }

    /// <summary>Gets the chilled water supply temperature [°C].</summary>
    double ChilledWaterSupplyTemperature { get; }

    /// <summary>Gets the chilled water return temperature [°C].</summary>
    double ChilledWaterReturnTemperature { get; }

    /// <summary>Gets the chilled water flow rate [kg/s].</summary>
    double ChilledWaterFlowRate { get; }

    /// <summary>Gets the chilled water bypass flow rate [kg/s].</summary>
    double ChilledWaterBypassFlowRate { get; }

    /// <summary>Gets a value indicating whether the chilled water supply is overloaded.</summary>
    bool IsOverLoad_C { get; }

    /// <summary>Gets or sets the hot water supply temperature setpoint [°C].</summary>
    double HotWaterSupplyTemperatureSetpoint { get; }

    /// <summary>Gets the hot water supply temperature [°C].</summary>
    double HotWaterSupplyTemperature { get; }

    /// <summary>Gets the hot water return temperature [°C].</summary>
    double HotWaterReturnTemperature { get; }

    /// <summary>Gets the hot water flow rate [kg/s].</summary>
    double HotWaterFlowRate { get; }

    /// <summary>Gets the hot water bypass flow rate [kg/s].</summary>
    double HotWaterBypassFlowRate { get; }

    /// <summary>Gets a value indicating whether the hot water supply is overloaded.</summary>
    bool IsOverLoad_H { get; }

    /// <summary>True if a secondary pump system is used.</summary>
    bool IsSecondaryPumpSystem { get; }

    /// <summary>Gets the chilled water secondary pump system.</summary>
    IReadOnlyPumpSystem? ChilledWaterPumpSystem { get; }

    /// <summary>Gets the hot water secondary pump system.</summary>
    IReadOnlyPumpSystem? HotWaterPumpSystem { get; }

    /// <summary>Gets or sets the outdoor air conditions.</summary>
    IReadOnlyMoistAir OutdoorAir { get; }

    /// <summary>Gets or sets the current date and time.</summary>
    DateTime CurrentDateTime { get; }

    /// <summary>Gets or sets the simulation time step [s].</summary>
    double TimeStep { get; }

    /// <summary>Gets or sets the piping heat loss rate [-].</summary>
    double PipeHeatLossRate { get; }
  }

}
