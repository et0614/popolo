/* IHeatSourceSubSystem.cs
 * 
 * Copyright (C) 2016 E.Togashi
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using Popolo.Core.Physics;

using Popolo.Core.HVAC.SystemModel;

namespace Popolo.Core.HVAC.SystemModel
{
  /// <summary>Heat source equipment sub-system.</summary>
  public interface IHeatSourceSubSystem
  {

    /// <summary>Gets the selectable operating modes.</summary>
    HeatSourceSystemModel.OperatingMode SelectableMode { get; }

    /// <summary>Gets or sets the current operating mode.</summary>
    HeatSourceSystemModel.OperatingMode Mode { get; set; }

    /// <summary>Gets or sets the outdoor air conditions.</summary>
    IReadOnlyMoistAir OutdoorAir { get; set; }

    /// <summary>Gets or sets the current date and time.</summary>
    DateTime CurrentDateTime { get; set; }

    /// <summary>Gets or sets the simulation time step [s].</summary>
    double TimeStep { get; set; }

    /// <summary>Forecasts the supply water temperatures for the given flow rates.</summary>
    /// <param name="chilledWaterFlowRate">Chilled water flow rate [kg/s].</param>
    /// <param name="hotWaterFlowRate">Hot water flow rate [kg/s].</param>
    void ForecastSupplyWaterTemperature(double chilledWaterFlowRate, double hotWaterFlowRate);

    /// <summary>Fixes (commits) the forecast state as the current state.</summary>
    void FixState();

    /// <summary>Shuts off this heat source sub-system.</summary>
    void ShutOff();

    /// <summary>Gets a value indicating whether the chilled water supply is overloaded.</summary>
    bool IsOverLoad_C { get; }

    /// <summary>Gets a value indicating whether the hot water supply is overloaded.</summary>
    bool IsOverLoad_H { get; }

    /// <summary>Gets or sets the hot water return temperature [°C].</summary>
    double HotWaterReturnTemperature { get; set; }

    /// <summary>Gets or sets the hot water supply temperature setpoint [°C].</summary>
    double HotWaterSupplyTemperatureSetpoint { get; set; }

    /// <summary>Gets the hot water supply temperature [°C].</summary>
    double HotWaterSupplyTemperature { get; }

    /// <summary>Gets the hot water flow rate [kg/s].</summary>
    double HotWaterFlowRate { get; }

    /// <summary>Gets the maximum hot water flow rate [kg/s].</summary>
    double MaxHotWaterFlowRate { get; }

    /// <summary>Gets the minimum hot water flow rate ratio [-].</summary>
    double MinHotWaterFlowRatio { get; }

    /// <summary>Gets or sets the chilled water return temperature [°C].</summary>
    double ChilledWaterReturnTemperature { get; set; }

    /// <summary>Gets or sets the chilled water supply temperature setpoint [°C].</summary>
    double ChilledWaterSupplyTemperatureSetpoint { get; set; }

    /// <summary>Gets the chilled water supply temperature [°C].</summary>
    double ChilledWaterSupplyTemperature { get; }

    /// <summary>Gets the chilled water flow rate [kg/s].</summary>
    double ChilledWaterFlowRate { get; }

    /// <summary>Gets the maximum chilled water flow rate [kg/s].</summary>
    double MaxChilledWaterFlowRate { get; }

    /// <summary>Gets the minimum chilled water flow rate ratio [-].</summary>
    double MinChilledWaterFlowRatio { get; }

  }
}
