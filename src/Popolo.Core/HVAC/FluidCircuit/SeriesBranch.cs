/* SeriesBranch.cs
 * 
 * Copyright (C) 2016 E.Togashi
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
using Popolo.Core.Numerics;

namespace Popolo.Core.HVAC.FluidCircuit
{

  /// <summary>Represents flow branches connected in series.</summary>
  public class SeriesBranch : ICircuitBranch
  {

    private CircuitNode ndUP, ndMD, ndDN;

    private ICircuitBranch brUPStrm, brDNStrm;

    /// <summary>Gets or sets the upstream node.</summary>
    public CircuitNode? UpStreamNode { get; set; }

    /// <summary>Gets or sets the downstream node.</summary>
    public CircuitNode? DownStreamNode { get; set; }

    /// <summary>Gets or sets the volumetric flow rate [m³/s].</summary>
    public double VolumetricFlowRate { get; set; }
    
    /// <summary>Initializes a new instance.</summary>
    public SeriesBranch(ICircuitBranch brUPStrm, ICircuitBranch brDNStrm)
    {
      ndUP = new CircuitNode();
      ndMD = new CircuitNode();
      ndDN = new CircuitNode();

      this.brUPStrm = brUPStrm;
      this.brDNStrm = brDNStrm;
      brUPStrm.UpStreamNode = ndUP;
      brUPStrm.DownStreamNode = ndMD;
      brDNStrm.UpStreamNode = ndMD;
      brDNStrm.DownStreamNode = ndDN;
    }

    /// <summary>Computes the volumetric flow rate [m³/s] from the differential pressure.</summary>
    /// <returns>Volumetric flow rate [m³/s].</returns>
    public void UpdateFlowRateFromNodePressureDifference()
    {
      if (UpStreamNode == null || DownStreamNode == null)
        throw new PopoloInvalidOperationException(
            nameof(SeriesBranch),
            nameof(UpStreamNode));

      double dp = UpStreamNode.Pressure - DownStreamNode.Pressure;
      ndUP.Pressure = UpStreamNode.Pressure;
      ndDN.Pressure = DownStreamNode.Pressure;

      Roots.ErrorFunction eFnc = delegate (double p)
      {
        ndMD.Pressure = p;
        brUPStrm.UpdateFlowRateFromNodePressureDifference();
        brDNStrm.UpdateFlowRateFromNodePressureDifference();
        return brUPStrm.VolumetricFlowRate - brDNStrm.VolumetricFlowRate;
      };
      Roots.Bisection(eFnc, UpStreamNode.Pressure, DownStreamNode.Pressure, 1e-7, 1e-7, 20);
      //Roots.Newton(eFnc, 0.5 * (UpStreamNode.Pressure - DownStreamNode.Pressure), 1e-6, 1e-6, 1e-5, 20);
      VolumetricFlowRate = brUPStrm.VolumetricFlowRate;
    }

  }
}
