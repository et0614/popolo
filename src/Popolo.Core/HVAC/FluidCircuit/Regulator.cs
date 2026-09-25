/* Regulator.cs
 * 
 * Copyright (C) 2015 E.Togashi
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
  /// <summary>Represents a flow control valve or damper.</summary>
  public class Regulator: ICircuitBranch
  {

    #region Instance variables and properties

    /// <summary>Resistance coefficient at fully open position [kPa/(m³/s)²].</summary>
    private double minResistance;

    /// <summary>Valve opening ratio [-].</summary>
    private double lift = 1;

    /// <summary>Linear characteristic weighting factor [-].</summary>
    private double lWeight;

    /// <summary>Rangeability [-].</summary>
    private double rangeAbility = 100;

    /// <summary>Gets or sets the valve opening ratio [-].</summary>
    public double Lift
    {
      get { return lift; }
      set { lift = Math.Max(0, Math.Min(1, value)); }
    }

    /// <summary>Gets or sets the linear characteristic weighting factor [-].</summary>
    public double LinearCharacteristicWeight
    {
      get { return lWeight; }
      set { lWeight = Math.Max(0, Math.Min(1, value)); }
    }

    /// <summary>Gets or sets the linear characteristic weighting factor [-].</summary>
    [Obsolete("Misspelled name. Use LinearCharacteristicWeight instead. This member will be removed in a future major version.")]
    public double LinearCharactaristicWeight
    {
      get { return LinearCharacteristicWeight; }
      set { LinearCharacteristicWeight = value; }
    }

    /// <summary>Gets or sets the rangeability [-].</summary>
    public double RangeAbility
    {
      get { return rangeAbility; }
      set { if (0 < value) rangeAbility = value; }
    }
    
    /// <summary>Gets or sets the target flow rate [m³/s].</summary>
    public double VolumetricFlowRateSetpoint { get; set; }

    /// <summary>Gets or sets a value indicating whether the valve can be fully closed.</summary>
    public bool IsTotallyClosable { get; set; } = false;

    #endregion

    #region Constructors

    /// <summary>Initializes a new instance.</summary>
    /// <param name="flowRate">Flow rate at fully open position [m³/s].</param>
    /// <param name="pressureDrop">Pressure drop at fully open position [kPa].</param>
    /// <param name="rangeAbility">Rangeability [-].</param>
    /// <param name="linearWeight">Linear characteristic weighting factor [-].</param>
    public Regulator(double flowRate, double pressureDrop, double rangeAbility, double linearWeight)
    {
      DesignFlowRate = flowRate;
      VolumetricFlowRateSetpoint = flowRate;
      minResistance = pressureDrop / (flowRate * flowRate);
      RangeAbility = rangeAbility;
      LinearCharacteristicWeight = linearWeight;
    }

    /// <summary>Initializes a new instance.</summary>
    /// <param name="cvValue">CV value [US gal/min].</param>
    /// <param name="rangeAbility">Rangeability [-].</param>
    /// <param name="linearWeight">Linear characteristic weighting factor [-].</param>
    public Regulator(double cvValue, double rangeAbility, double linearWeight)
    {
      DesignFlowRate = cvValue * 6.31e-5;
      VolumetricFlowRateSetpoint = DesignFlowRate;
      minResistance = 6.89 / (DesignFlowRate * DesignFlowRate);
      RangeAbility = rangeAbility;
      LinearCharacteristicWeight = linearWeight;
    }

    #endregion

    #region ICircuitBranch implementation

    /// <summary>Gets or sets the flow rate [m³/s].</summary>
    public double VolumetricFlowRate { get; set; }

    /// <summary>Gets the design flow rate [m³/s].</summary>
    public double DesignFlowRate { get; private set; }

    /// <summary>Gets or sets the upstream node.</summary>
    public CircuitNode? UpStreamNode { get; set; }

    /// <summary>Gets or sets the downstream node.</summary>
    public CircuitNode? DownStreamNode { get; set; }

    /// <summary>Computes the volumetric flow rate [m³/s] from the differential pressure.</summary>
    /// <returns>Volumetric flow rate [m³/s].</returns>
    public void UpdateFlowRateFromNodePressureDifference()
    {
      if (IsTotallyClosable && Lift == 0) VolumetricFlowRate = 0;
      else
      {
        if (UpStreamNode == null || DownStreamNode == null)
          throw new PopoloInvalidOperationException(
              nameof(Regulator),
              nameof(UpStreamNode));

        double dp = UpStreamNode.Pressure - DownStreamNode.Pressure;
        VolumetricFlowRate = Math.Sign(dp) * Math.Sqrt(Math.Abs(dp) / GetResistance());
      }
    }

    /// <summary>Gets the flow resistance coefficient [kPa/(m³/s)²].</summary>
    /// <returns>Flow resistance coefficient [kPa/(m³/s)²].</returns>
    public double GetResistance()
    {
      if (IsTotallyClosable && Lift == 0) return double.PositiveInfinity;
      double wf = LinearCharacteristicWeight;
      double lam = 1d / RangeAbility;
      return wf* minResistance / Math.Pow((1 - lam) * Lift + lam, 2)
       + (1d - wf) * minResistance * Math.Pow(lam, 2 * Lift - 2);
    }

    #endregion

    #region Opening ratio calculation methods

    /// <summary>Adjusts the valve opening based on the current differential pressure [kPa].</summary>
    public void UpdateLift()
    {
      if (VolumetricFlowRateSetpoint <= 0)
      {
        Lift = 0;
        return;
      }

      if (UpStreamNode == null || DownStreamNode == null)
        throw new PopoloInvalidOperationException(
            nameof(Regulator),
            nameof(UpStreamNode));

      double dp = Math.Abs(UpStreamNode.Pressure - DownStreamNode.Pressure);
      UpdateLift(dp);
    }

    /// <summary>Adjusts the valve opening based on the upstream-to-downstream differential pressure [kPa].</summary>
    /// <param name="pressure">Differential pressure [kPa].</param>
    /// <remarks>
    /// A setpoint of zero or less closes the valve (Lift = 0). When the required resistance is
    /// below the fully-open resistance the valve is fully opened (Lift = 1); when it is at or above
    /// the resistance at Lift = 0 (the rangeability limit) the valve is set to Lift = 0.
    /// In between, the resistance characteristic is monotonically decreasing in the lift,
    /// and the lift is solved by Brent's method on [0, 1].
    /// </remarks>
    public void UpdateLift(double pressure)
    {
      if (VolumetricFlowRateSetpoint <= 0)
      {
        Lift = 0;
        return;
      }

      double res = pressure / (VolumetricFlowRateSetpoint * VolumetricFlowRateSetpoint);
      if (res < minResistance) Lift = 1.0;
      else
      {
        double wf = LinearCharacteristicWeight;
        double lam = 1d / RangeAbility;
        Roots.ErrorFunction eFnc = delegate (double c)
        {
          return wf * minResistance / Math.Pow((1 - lam) * c + lam, 2)
          + (1d - wf) * minResistance * Math.Pow(lam, 2 * c - 2) - res;
        };
        double f0 = eFnc(0); //Resistance at Lift = 0 minus the required resistance
        double f1 = minResistance - res; //Resistance at Lift = 1 minus the required resistance (≤ 0)
        //Rangeability ≤ 1: the lift cannot increase the resistance above the fully-open value
        if (f0 <= f1) Lift = 1.0;
        //The required resistance cannot be reached even at the minimum lift
        else if (f0 <= 0) Lift = 0.0;
        else Lift = Roots.Brent(eFnc, 0, 1, f0, f1, 1e-8);
      }
    }

    #endregion

  }
}
