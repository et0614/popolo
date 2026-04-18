/* IReadOnlyFluidMachinery.cs
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
using Popolo.Core.Numerics;

namespace Popolo.Core.HVAC.FluidCircuit
{
  /// <summary>Represents a read-only view of fluid machinery.</summary>
    public interface IReadOnlyFluidMachinery
    {
      /// <summary>Gets the flow rate [m³/s].</summary>
      double VolumetricFlowRate { get; }
  
      /// <summary>Gets the rotation speed ratio [-].</summary>
      double RotationRatio { get; }
  
      /// <summary>Gets the minimum rotation speed ratio [-].</summary>
      double MinRotationRatio { get; }
  
      /// <summary>Gets the total pressure or pump head [kPa].</summary>
      double Pressure { get; }
  
      /// <summary>Gets the actual head [kPa].</summary>
      double ActualHead { get; }
  
      /// <summary>Gets a value indicating whether the machine has an inverter.</summary>
      bool HasInverter { get; }
  
      /// <summary>Gets the design flow rate [m³/s].</summary>
      double DesignFlowRate { get; }
  
      /// <summary>Gets the nominal shaft power [kW].</summary>
      double NominalShaftPower { get; }
  
      /// <summary>Gets the motor efficiency [-].</summary>
      double MotorEfficiency { get; }
  
      /// <summary>Gets the fluid machinery efficiency [-].</summary>
      double GetFluidMachineryEfficiency();
  
      /// <summary>Gets the inverter efficiency [-].</summary>
      double GetInverterEfficiency();
  
      /// <summary>Gets the power consumption [kW].</summary>
      double GetElectricConsumption();
  
      /// <summary>Gets the overall efficiency [-].</summary>
      double GetTotalEfficiency();
  
      /// <summary>Gets a value indicating whether the machine is shut off.</summary>
      bool IsShutOff { get; }
    }
}
