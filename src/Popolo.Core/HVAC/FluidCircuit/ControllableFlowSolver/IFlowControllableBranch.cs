/* IFlowControllableBranch.cs
 * 
 * Copyright (C) 2018 E.Togashi
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

namespace Popolo.Core.HVAC.FluidCircuit.ControllableFlowSolver
{
  /// <summary>Represents a variable-flow branch controlled by resistance adjustment.</summary>
  public interface IFlowControllableBranch
  {
    /// <summary>Gets a value indicating whether the composite resistance has changed.</summary>
    bool HasTotalResistanceChanged { get; }

    /// <summary>Gets the total circuit flow rate [m³/s].</summary>
    double TotalFlowRate { get; }

    /// <summary>Gets the total target flow rate of the circuit [m³/s].</summary>
    /// <returns>Total target flow rate of the circuit [m³/s].</returns>
    double GetTotalFlowSetpoint();

    /// <summary>Gets the required minimum differential pressure [kPa].</summary>
    /// <returns>Required minimum differential pressure [kPa].</returns>
    double GetMinPressure();

    /// <summary>Gets the composite resistance of the entire circuit [kPa/(m³/s)²].</summary>
    /// <returns>Composite resistance of the entire circuit [kPa/(m³/s)²].</returns>
    double GetTotalResistance();

    /// <summary>Adjusts the flow rate based on the differential pressure.</summary>
    /// <param name="pressure">Differential pressure [kPa].</param>
    void ControlFlowRate(double pressure);
  }
}
