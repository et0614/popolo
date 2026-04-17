/* IFlowControllableBranch.cs
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

namespace Popolo.Core.HVAC.FluidCircuit.ControllableFlowSolver
{
  /// <summary>Represents a variable-flow branch controlled by resistance adjustment.</summary>
  public interface IFlowControllableBranch
  {
    /// <summary>Gets a value indicating whether the composite resistance has changed.</summary>
    bool HasTotalResistanceChanged { get; }

    /// <summary>Gets the total circuit flow rate [m³/s].</summary>
    double TotalFlowRate { get; }

    /// <summary>Gets the total target flow rate of the circuit [m³/s].</summary>
    /// <returns>Total target flow rate of the circuit [m³/s].</returns>
    double GetTotalFlowSetpoint();

    /// <summary>Gets the required minimum differential pressure [kPa].</summary>
    /// <returns>Required minimum differential pressure [kPa].</returns>
    double GetMinimumPressure();

    /// <summary>Gets the composite resistance of the entire circuit [kPa/(m³/s)²].</summary>
    /// <returns>Composite resistance of the entire circuit [kPa/(m³/s)²].</returns>
    double GetTotalResistance();

    /// <summary>Adjusts the flow rate based on the differential pressure.</summary>
    /// <param name="pressure">Differential pressure [kPa].</param>
    void ControlFlowRate(double pressure);
  }
}
