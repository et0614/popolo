/* SimpleCircuitBranch.cs
 * 
 * Copyright (C) 2015 E.Togashi
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

using Popolo.Core.Exceptions;
using System;

namespace Popolo.Core.HVAC.FluidCircuit
{

  /// <summary>Represents a simple flow branch where pressure drop is proportional to the square of volumetric flow rate.</summary>
  public class SimpleCircuitBranch : ICircuitBranch
  {
    
    /// <summary>Gets or sets the upstream node.</summary>
    public CircuitNode? UpStreamNode { get; set; }

    /// <summary>Gets or sets the downstream node.</summary>
    public CircuitNode? DownStreamNode { get; set; }

    /// <summary>Gets or sets the volumetric flow rate [m³/s].</summary>
    public double VolumetricFlowRate { get; set; }

    /// <summary>Gets or sets the flow resistance coefficient [kPa/(m/s)²].</summary>
    public double Resistance { get; set; }

    /// <summary>Initializes a new instance.</summary>
    /// <param name="flowRate">Volumetric flow rate [m³/s].</param>
    /// <param name="pressure">Pressure loss [kPa].</param>
    public SimpleCircuitBranch(double flowRate, double pressure)
    { Resistance = pressure / (flowRate * flowRate); }

    /// <summary>Computes the pressure loss [kPa].</summary>
    /// <param name="flowRate">Volumetric flow rate [m³/s].</param>
    /// <returns>Pressure loss [kPa].</returns>
    public double GetPressureDrop(double flowRate)
    {
      VolumetricFlowRate = flowRate;
      return flowRate * flowRate * Resistance;
    }

    /// <summary>Computes the volumetric flow rate [m³/s] from the differential pressure.</summary>
    /// <returns>Volumetric flow rate [m³/s].</returns>
    public void UpdateFlowRateFromNodePressureDifference()
    {
      if (UpStreamNode == null || DownStreamNode == null)
        throw new PopoloInvalidOperationException(
            nameof(SimpleCircuitBranch),
            nameof(UpStreamNode));

      double dp = UpStreamNode.Pressure - DownStreamNode.Pressure;
      VolumetricFlowRate = Math.Sign(dp) * Math.Sqrt((Math.Abs(dp) / Resistance));
    }

  }
}
