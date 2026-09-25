/* CircuitNode.cs
 * 
 * Copyright (C) 2014 E.Togashi
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
  /// <summary>Represents a node (junction) in a fluid circuit network.</summary>
  public class CircuitNode : IReadOnlyCircuitNode
  {

    #region Instance variables and properties

    /// <summary>List of incoming branches.</summary>
    private List<ICircuitBranch> outFlowBrchs = new List<ICircuitBranch>();

    /// <summary>List of outgoing branches.</summary>
    private List<ICircuitBranch> inFlowBrchs = new List<ICircuitBranch>();

    /// <summary>Gets a value indicating whether this node has a fixed pressure.</summary>
    public bool IsPressureFixed { get; set; } = false;
    
    /// <summary>Gets or sets the pressure [kPa].</summary>
    public double Pressure { get; set; }

    /// <summary>Gets or sets the inflow [m³/s].</summary>
    public double Inflow { get; set; }

    #endregion

    #region Internal methods
    
    /// <summary>Adds an incoming branch.</summary>
    /// <param name="branch">Incoming branch.</param>
    internal void addOutFlowBranch(ICircuitBranch branch) { outFlowBrchs.Add(branch); }

    /// <summary>Adds an outgoing branch.</summary>
    /// <param name="branch">Outgoing branch.</param>
    internal void addInFlowBranch(ICircuitBranch branch) { inFlowBrchs.Add(branch); }

    /// <summary>Removes a branch.</summary>
    /// <param name="branch">Branch.</param>
    internal void removeBranch(ICircuitBranch branch)
    {
      outFlowBrchs.Remove(branch);
      inFlowBrchs.Remove(branch);
    }

    #endregion

    #region Public methods

    /// <summary>Accumulates the inflow/outflow balance [m³/s].</summary>
    /// <returns>Inflow/outflow balance [m³/s].</returns>
    /// <remarks>Used for mass conservation validation.</remarks>
    public double IntegrateFlow()
    {
      double sum = Inflow;
      foreach (ICircuitBranch br in outFlowBrchs) sum -= br.VolumetricFlowRate;
      foreach (ICircuitBranch br in inFlowBrchs) sum += br.VolumetricFlowRate;
      return sum;
    }

    /// <summary>Gets the list of outgoing branches.</summary>
    /// <returns>List of outgoing branches.</returns>
    public IReadOnlyCircuitBranch[] GetInFlowBranches() { return inFlowBrchs.ToArray(); }

    /// <summary>Gets the list of incoming branches.</summary>
    /// <returns>List of incoming branches.</returns>
    public IReadOnlyCircuitBranch[] GetOutFlowBranches() { return outFlowBrchs.ToArray(); }

    #endregion

  }

  #region Read-only interface

  #endregion

}
