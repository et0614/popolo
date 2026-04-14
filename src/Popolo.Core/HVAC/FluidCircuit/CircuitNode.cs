/* CircuitNode.cs
 * 
 * Copyright (C) 2014 E.Togashi
 * 
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 3 of the License, or (at
 * your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful, but
 * WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301, USA.
 */

using System;
using System.Collections.Generic;

namespace Popolo.Core.HVAC.FluidCircuit
{
  /// <summary>Represents a node (junction) in a fluid circuit network.</summary>
  public class CircuitNode : IReadOnlyCircuitNode
  {

    #region インスタンス変数・プロパティ

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

    #region internalメソッド
    
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

    #region publicメソッド

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

  #region 読み取り専用インターフェース

  #endregion

}
