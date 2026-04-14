/* IReadOnlyCircuitNode.cs
 *
 * Copyright (C) 2026 E.Togashi
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 3 of the License, or (at
 * your option) any later version.
 *
 * This program is distributed in the hope that it will be useful, but
 * WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
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
