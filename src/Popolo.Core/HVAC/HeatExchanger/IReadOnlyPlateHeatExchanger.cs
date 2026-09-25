/* IReadOnlyPlateHeatExchanger.cs
 *
 * Copyright (C) 2026 E.Togashi
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

namespace Popolo.Core.HVAC.HeatExchanger
{
  /// <summary>Represents a read-only view of a plate heat exchanger.</summary>
  public interface IReadOnlyPlateHeatExchanger
  {
    /// <summary>Gets a value indicating whether the heat exchanger is overloaded.</summary>
    bool IsOverLoad { get; }

    /// <summary>Gets the overall heat transfer coefficient UA [kW/K].</summary>
    double HeatTransferCoefficient { get; }

    /// <summary>Gets the maximum allowable heat source flow rate [kg/s].</summary>
    double MaxHeatSourceFlowRate { get; }

    /// <summary>Gets the maximum allowable supply flow rate [kg/s].</summary>
    double MaxSupplyFlowRate { get; }

    /// <summary>Gets the current heat source flow rate [kg/s].</summary>
    double HeatSourceFlowRate { get; }

    /// <summary>Gets the current supply flow rate [kg/s].</summary>
    double SupplyFlowRate { get; }

    /// <summary>Gets the heat source inlet temperature [°C].</summary>
    double HeatSourceInletTemperature { get; }

    /// <summary>Gets the heat source outlet temperature [°C].</summary>
    double HeatSourceOutletTemperature { get; }

    /// <summary>Gets the supply temperature [°C].</summary>
    double SupplyTemperature { get; }

    /// <summary>Gets the return temperature [°C].</summary>
    double ReturnTemperature { get; }

    /// <summary>Gets the supply temperature setpoint [°C].</summary>
    double SupplyTemperatureSetpoint { get; }

    /// <summary>Gets the heat transfer rate [kW].</summary>
    double HeatTransfer { get; }
  }
}
