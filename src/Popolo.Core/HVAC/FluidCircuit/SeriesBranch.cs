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
using System;

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

    /// <summary>Maximum number of bracket expansions for the intermediate node pressure.</summary>
    private const int MAX_BRACKET_EXPANSION = 60;

    /// <summary>Minimum initial bracket expansion step for the intermediate node pressure [kPa].</summary>
    private const double MIN_EXPANSION_STEP = 1.0;

    /// <summary>Absolute tolerance on the intermediate node pressure [kPa].</summary>
    private const double PRESSURE_TOLERANCE = 1e-9;

    /// <summary>Computes the volumetric flow rate [m³/s] from the differential pressure.</summary>
    /// <returns>Volumetric flow rate [m³/s].</returns>
    /// <remarks>
    /// The intermediate node pressure p is solved so that both branches carry the same flow.
    /// The flow of each branch must be monotone in its own pressure difference (as for pipes,
    /// valves, fixed resistances, pumps and fans), which makes the flow imbalance
    /// (upstream flow − downstream flow) non-increasing in p. For passive branches the root
    /// lies between the end-node pressures; when a pump or fan is included it lies outside
    /// them, so the interval is expanded (bounded number of doublings) until it brackets the root.
    /// </remarks>
    /// <exception cref="PopoloNumericalException">
    /// Thrown when no intermediate pressure balancing the two branch flows can be bracketed.
    /// </exception>
    public void UpdateFlowRateFromNodePressureDifference()
    {
      if (UpStreamNode == null || DownStreamNode == null)
        throw new PopoloInvalidOperationException(
            nameof(SeriesBranch),
            nameof(UpStreamNode));

      ndUP.Pressure = UpStreamNode.Pressure;
      ndDN.Pressure = DownStreamNode.Pressure;

      Roots.ErrorFunction eFnc = delegate (double p)
      {
        ndMD.Pressure = p;
        brUPStrm.UpdateFlowRateFromNodePressureDifference();
        brDNStrm.UpdateFlowRateFromNodePressureDifference();
        return brUPStrm.VolumetricFlowRate - brDNStrm.VolumetricFlowRate;
      };

      //Initial bracket: the end-node pressures (sufficient for passive branches)
      double lo = Math.Min(UpStreamNode.Pressure, DownStreamNode.Pressure);
      double hi = Math.Max(UpStreamNode.Pressure, DownStreamNode.Pressure);
      double fLo = eFnc(lo);
      double fHi = eFnc(hi);

      //Expand the bracket when it does not contain a sign change
      //(the imbalance is non-increasing in p: positive at both ends → root above, negative → root below)
      double step = Math.Max(hi - lo, MIN_EXPANSION_STEP);
      int expansion = 0;
      while (0 < fLo * fHi)
      {
        if (MAX_BRACKET_EXPANSION < ++expansion)
          throw new PopoloNumericalException(nameof(SeriesBranch),
            "Could not bracket the intermediate node pressure that balances the flows of the two "
            + $"series branches (last interval [{lo}, {hi}] kPa, imbalance {fLo}, {fHi} m³/s). "
            + "SeriesBranch requires branches whose flow is monotone in their own pressure difference.");
        if (0 < fHi)
        {
          lo = hi; fLo = fHi;
          hi += step; fHi = eFnc(hi);
        }
        else
        {
          hi = lo; fHi = fLo;
          lo -= step; fLo = eFnc(lo);
        }
        step *= 2;
      }

      double pMid = Roots.Brent(eFnc, lo, hi, fLo, fHi, PRESSURE_TOLERANCE);
      eFnc(pMid); //Update the branch states at the solution
      VolumetricFlowRate = brUPStrm.VolumetricFlowRate;
    }

  }
}
