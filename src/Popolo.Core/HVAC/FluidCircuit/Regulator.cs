/* Regulator.cs
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
using Popolo.Core.Numerics;
using System;

namespace Popolo.Core.HVAC.FluidCircuit
{
  /// <summary>Represents a flow control valve or damper.</summary>
  public class Regulator: ICircuitBranch
  {

    #region インスタンス変数・プロパティ

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
    public double LinearCharactaristicWeight
    {
      get { return lWeight; }
      set { lWeight = Math.Max(0, Math.Min(1, value)); }
    }

    /// <summary>Gets or sets the rangeability [-].</summary>
    public double RangeAbility
    {
      get { return rangeAbility; }
      set { if (0 < value) rangeAbility = value; }
    }
    
    /// <summary>Gets or sets the target flow rate [m³/s].</summary>
    public double VolumetricFlowRateSetPoint { get; set; }

    /// <summary>Gets or sets a value indicating whether the valve can be fully closed.</summary>
    public bool IsTotallyClosable { get; set; } = false;

    #endregion

    #region コンストラクタ

    /// <summary>Initializes a new instance.</summary>
    /// <param name="flowRate">Flow rate at fully open position [m³/s].</param>
    /// <param name="pressureDrop">Pressure drop at fully open position [kPa].</param>
    /// <param name="rangeAbility">Rangeability [-].</param>
    /// <param name="linearWeight">Linear characteristic weighting factor [-].</param>
    public Regulator(double flowRate, double pressureDrop, double rangeAbility, double linearWeight)
    {
      DesignFlowRate = flowRate;
      VolumetricFlowRateSetPoint = flowRate;
      minResistance = pressureDrop / (flowRate * flowRate);
      RangeAbility = rangeAbility;
      LinearCharactaristicWeight = linearWeight;
    }

    /// <summary>Initializes a new instance.</summary>
    /// <param name="cvValue">CV value [US gal/min].</param>
    /// <param name="rangeAbility">Rangeability [-].</param>
    /// <param name="linearWeight">Linear characteristic weighting factor [-].</param>
    public Regulator(double cvValue, double rangeAbility, double linearWeight)
    {
      DesignFlowRate = cvValue * 6.31e-5;
      VolumetricFlowRateSetPoint = DesignFlowRate;
      minResistance = 6.89 / (DesignFlowRate * DesignFlowRate);
      RangeAbility = rangeAbility;
      LinearCharactaristicWeight = linearWeight;
    }

    #endregion

    #region ICircuitBranch実装

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
      double wf = LinearCharactaristicWeight;
      double lam = 1d / RangeAbility;
      return wf* minResistance / Math.Pow((1 - lam) * Lift + lam, 2)
       + (1d - wf) * minResistance * Math.Pow(lam, 2 * Lift - 2);
    }

    #endregion

    #region 開度計算処理

    /// <summary>Adjusts the valve opening based on the current differential pressure [kPa].</summary>
    public void UpdateLift()
    {
      if (VolumetricFlowRateSetPoint == 0) Lift = 0;

      if (UpStreamNode == null || DownStreamNode == null)
        throw new PopoloInvalidOperationException(
            nameof(Regulator),
            nameof(UpStreamNode));

      double dp = Math.Abs(UpStreamNode.Pressure - DownStreamNode.Pressure);
      UpdateLift(dp);
    }

    /// <summary>Adjusts the valve opening based on the upstream-to-downstream differential pressure [kPa].</summary>
    /// <param name="pressure">Differential pressure [kPa].</param>
    public void UpdateLift(double pressure)
    {
      double res = pressure / (VolumetricFlowRateSetPoint * VolumetricFlowRateSetPoint);
      if (res < minResistance) Lift = 1.0;
      else
      {
        double wf = LinearCharactaristicWeight;
        double lam = 1d / RangeAbility;
        Roots.ErrorFunction eFnc = delegate (double c)
        {
          Lift = c;
          return wf * minResistance / Math.Pow((1 - lam) * Lift + lam, 2)
          + (1d - wf) * minResistance * Math.Pow(lam, 2 * Lift - 2) - res;
        };
        Lift = Roots.Newton(eFnc, 0.5, 1e-4, 1e-4, 1e-4, 20);
      }
    }

    #endregion

  }
}
