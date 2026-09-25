/* IReadOnlyCircuitNode.cs
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
using System.Collections.Generic;

namespace Popolo.Core.HVAC.FluidCircuit
{
  /// <summary>Represents a read-only view of a circuit node.</summary>
    public interface IReadOnlyCircuitNode
    {
      /// <summary>Gets a value indicating whether this node has a fixed pressure.</summary>
      bool IsPressureFixed { get; }
  
      /// <summary>Gets the pressure [kPa].</summary>
      double Pressure { get; }
  
      /// <summary>Gets the inflow [m³/s].</summary>
      double Inflow { get; }
  
      /// <summary>Accumulates the inflow/outflow balance [m³/s].</summary>
      /// <returns>Inflow/outflow balance [m³/s].</returns>
      /// <remarks>Used for mass conservation validation.</remarks>
      double IntegrateFlow();
  
      /// <summary>Gets the list of outgoing branches.</summary>
      /// <returns>List of outgoing branches.</returns>
      IReadOnlyCircuitBranch[] GetInFlowBranches();
  
      /// <summary>Gets the list of incoming branches.</summary>
      /// <returns>List of incoming branches.</returns>
      IReadOnlyCircuitBranch[] GetOutFlowBranches();
  
    }
}
