/* IReadOnlyIceOnCoilThermalStorage.cs
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

namespace Popolo.Core.HVAC.Storage
{
  /// <summary>Represents a read-only view of an internal-melt ice-on-coil thermal storage tank.</summary>
  public interface IReadOnlyIceOnCoilThermalStorage
  {
    /// <summary>Gets the time step [s].</summary>
    double TimeStep { get; }

    /// <summary>Gets the current ice state of the tank.</summary>
    IceOnCoilThermalStorage.IceState CurrentState { get; }

    /// <summary>Gets the total water volume of the tank [m³].</summary>
    double WaterVolume { get; }

    /// <summary>Gets the number of parallel coil branches.</summary>
    double NumberOfBranches { get; }

    /// <summary>Gets the length of a single coil branch [m].</summary>
    double BranchLength { get; }

    /// <summary>Gets the pipe inner diameter [m].</summary>
    double PipeInnerDiameter { get; }

    /// <summary>Gets the pipe wall thickness [m].</summary>
    double PipeThickness { get; }

    /// <summary>Gets the pipe outer diameter [m].</summary>
    double PipeOuterDiameter { get; }

    /// <summary>Gets a value indicating whether air bubbling (forced convection) is active.</summary>
    bool IsBubbling { get; }

    /// <summary>Gets the brine inlet temperature [°C].</summary>
    double InletBrineTemperature { get; }

    /// <summary>Gets the brine outlet temperature [°C].</summary>
    double OutletBrineTemperature { get; }

    /// <summary>Gets the brine mass flow rate [kg/s].</summary>
    double BrineFlowRate { get; }

    /// <summary>Gets the specific heat of brine [kJ/(kg·K)].</summary>
    double BrineSpecificHeat { get; }

    /// <summary>Gets the heat transfer to the coil [kW].
    /// Positive value: heat rejected to brine (melting); negative value: heat extracted from brine (ice making).</summary>
    double HeatTransferToCoil { get; }

    /// <summary>Gets the overall heat loss coefficient of the tank [W/K].</summary>
    double HeatLossCoefficient { get; }

    /// <summary>Gets the ambient temperature surrounding the tank [°C].</summary>
    double AmbientTemperature { get; }

    /// <summary>Gets the heat loss from the tank to the ambient [kW].</summary>
    double HeatLoss { get; }

    /// <summary>Gets the current ice packing factor (IPF) [-],
    /// defined as the ratio of total ice mass to total tank water mass.</summary>
    /// <returns>Ice packing factor [-].</returns>
    double GetIcePackingFactor();

    /// <summary>Gets the spatially averaged water/ice temperature across all coil segments [°C].</summary>
    /// <returns>Average water/ice temperature [°C].</returns>
    double GetAverageWaterIceTemperature();
  }
}
