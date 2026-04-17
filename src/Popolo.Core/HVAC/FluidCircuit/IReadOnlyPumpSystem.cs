/* IReadOnlyPumpSystem.cs
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

namespace Popolo.Core.HVAC.FluidCircuit
{
  /// <summary>Represents a read-only view of a pump system.</summary>
    public interface IReadOnlyPumpSystem
    {
      /// <summary>Gets the centrifugal pump.</summary>
      IReadOnlyCentrifugalPump Pump { get; }
  
      /// <summary>Gets or sets the total flow rate [m³/s].</summary>
      double TotalFlowRate { get; }
  
      /// <summary>Gets the bypass flow rate [m³/s].</summary>
      double BypassFlowRate { get; }
  
      /// <summary>Gets the number of operating units [units].</summary>
      int ActivePumpCount { get; }
  
      /// <summary>Gets the number of pumps [units].</summary>
      int PumpCount { get; }
  
      /// <summary>Gets the actual head [kPa].</summary>
      double ActualHead { get; }
  
      /// <summary>Gets the design total pressure or head [kPa].</summary>
      double PressureSetpoint { get; }
  
      /// <summary>Gets the power consumption [kW].</summary>
      double GetElectricConsumption();
    }
}
