/* SeriesBranch.cs
 * 
 * Copyright (C) 2016 E.Togashi
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
