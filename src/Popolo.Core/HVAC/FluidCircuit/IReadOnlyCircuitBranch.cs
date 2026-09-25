/* IReadOnlyCircuitBranch.cs
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

namespace Popolo.Core.HVAC.FluidCircuit
{
  /// <summary>Represents a branch (conduit) in a fluid circuit network.</summary>
    public interface IReadOnlyCircuitBranch
    {
      /// <summary>Gets the upstream node.</summary>
      CircuitNode? UpStreamNode { get; }
  
      /// <summary>Gets the downstream node.</summary>
      CircuitNode? DownStreamNode { get; }
  
      /// <summary>Gets the flow rate [m³/s].</summary>
      double VolumetricFlowRate { get; }
    }
}
