/* ControllableSingleFlow.cs
 * 
 * Copyright (C) 2018 E.Togashi
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

namespace Popolo.Core.HVAC.FluidCircuit.ControllableFlowSolver
{
  /// <summary>Represents a variable-flow series circuit controlled by resistance adjustment.</summary>
  public class ControllableSeriesFlow : IFlowControllableBranch
  {

    #region Instance variables and properties

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

    #region Constructors

    /// <summary>Initializes a new instance.</summary>
    /// <param name="minResistance">Minimum resistance [kPa/(m³/s)²].</param>
    /// <param name="fixedResistance">Fixed series resistance [kPa/(m³/s)²].</param>
    public ControllableSeriesFlow(double minResistance, double fixedResistance)
    {
      MinResistance = Resistance = minResistance;
      FixedResistance = fixedResistance;
    }

    #endregion

    #region IFlowControllableBranch implementation

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
