/* IReadOnlyPumpSystem.cs
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

using System;

namespace Popolo.Core.HVAC.FluidCircuit
{
  /// <summary>Represents a read-only view of a pump system.</summary>
    public interface IReadOnlyPumpSystem
    {
      /// <summary>Gets the centrifugal pump.</summary>
      IReadOnlyCentrifugalPump Pump { get; }
  
      /// <summary>Gets or sets the total flow rate [m³/s].</summary>
      double TotalFlowRate { get; }
  
      /// <summary>Gets the bypass flow rate [m³/s].</summary>
      double BypassFlowRate { get; }
  
      /// <summary>Gets the number of operating units [units].</summary>
      int ActivePumpCount { get; }
  
      /// <summary>Gets the number of pumps [units].</summary>
      int PumpCount { get; }
  
      /// <summary>Gets the actual head [kPa].</summary>
      double ActualHead { get; }
  
      /// <summary>Gets the design total pressure or head [kPa].</summary>
      double PressureSetpoint { get; }
  
      /// <summary>Gets the power consumption [kW].</summary>
      double GetElectricConsumption();
    }
}
