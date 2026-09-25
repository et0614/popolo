/* IReadOnlyFluidMachinery.cs
 *
 * Copyright (C) 2026 E.Togashi
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

      /// <summary>Gets the number of discrete flow notches (0 when not configured).</summary>
      int NotchCount { get; }

      /// <summary>Gets the current notch index (-1 when not operating on a notch).</summary>
      int CurrentNotchIndex { get; }

      /// <summary>Gets the current notch name (empty when not operating on a notch).</summary>
      string CurrentNotchName { get; }

      /// <summary>Gets the name of the specified notch.</summary>
      /// <param name="notchIndex">Notch index (ascending flow order).</param>
      /// <returns>Name of the notch.</returns>
      string GetNotchName(int notchIndex);

      /// <summary>Gets the volumetric flow rate [m³/s] of the specified notch.</summary>
      /// <param name="notchIndex">Notch index (ascending flow order).</param>
      /// <returns>Volumetric flow rate of the notch [m³/s].</returns>
      double GetNotchFlowRate(int notchIndex);
    }
}
