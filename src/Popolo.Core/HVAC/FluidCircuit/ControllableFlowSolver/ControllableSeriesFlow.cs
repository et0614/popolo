/* ControllableSingleFlow.cs
 * 
 * Copyright (C) 2018 E.Togashi
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

namespace Popolo.Core.HVAC.FluidCircuit.ControllableFlowSolver
{
  /// <summary>Represents a variable-flow series circuit controlled by resistance adjustment.</summary>
  public class ControllableSeriesFlow : IFlowControllableBranch
  {

    #region インスタンス変数・プロパティ

    /// <summary>Gets or sets the target flow rate [m³/s].</summary>
    public double FlowRateSetpoint { get; set; }

    /// <summary>Gets the minimum resistance coefficient [kPa/(m³/s)²].</summary>
    public double MinResistance { get; private set; }

    /// <summary>Gets the fixed series resistance coefficient [kPa/(m³/s)²].</summary>
    public double FixedResistance { get; set; }

    /// <summary>Gets or sets the resistance coefficient [kPa/(m³/s)²].</summary>
    public double Resistance
    {
      get { return resist; }
      set
      {
        resist = Math.Max(MinResistance, value);
        HasTotalResistanceChanged = true;
      }
    }

    /// <summary>Resistance coefficient [kPa/(m³/s)²].</summary>
    private double resist;

    #endregion

    #region コンストラクタ

    /// <summary>Initializes a new instance.</summary>
    /// <param name="minResistance">Minimum resistance [kPa/(m³/s)²].</param>
    /// <param name="fixedResistance">Fixed series resistance [kPa/(m³/s)²].</param>
    public ControllableSeriesFlow(double minResistance, double fixedResistance)
    {
      MinResistance = Resistance = minResistance;
      FixedResistance = fixedResistance;
    }

    #endregion

    #region IFlowControllableBranch実装

    /// <summary>Gets a value indicating whether the composite resistance has changed.</summary>
    public bool HasTotalResistanceChanged { get; private set; }

    /// <summary>Gets the total circuit flow rate [m³/s].</summary>
    public double TotalFlowRate { get; private set; }

    /// <summary>Gets the total target flow rate of the circuit [m³/s].</summary>
    /// <returns>Total target flow rate of the circuit [m³/s].</returns>
    public double GetTotalFlowSetpoint()
    { return FlowRateSetpoint; }

    /// <summary>Gets the required minimum differential pressure [kPa].</summary>
    /// <returns>Required minimum differential pressure [kPa].</returns>
    public double GetMinPressure()
    { return FlowRateSetpoint * FlowRateSetpoint * (MinResistance + FixedResistance); }

    /// <summary>Gets the composite resistance of the entire circuit [kPa/(m³/s)²].</summary>
    /// <returns>Composite resistance of the entire circuit [kPa/(m³/s)²].</returns>
    public double GetTotalResistance()
    {
      HasTotalResistanceChanged = false;
      return MinResistance + FixedResistance;
    }

    /// <summary>Adjusts the flow rate based on the differential pressure.</summary>
    /// <param name="pressure">Differential pressure [kPa].</param>
    public void ControlFlowRate(double pressure)
    { TotalFlowRate = Math.Sqrt(pressure / GetTotalResistance()); }

    #endregion

  }
}
