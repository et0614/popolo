/* IReadOnlyWaterPipe.cs
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
using Popolo.Core.Physics;
using Popolo.Core.Numerics;

namespace Popolo.Core.HVAC.FluidCircuit
{
  /// <summary>Represents a read-only view of a water pipe.</summary>
    public interface IReadOnlyWaterPipe
    {
      /// <summary>Gets the inner diameter [m].</summary>
      double InnerDiameter { get; }
  
      /// <summary>Gets the outer diameter [m].</summary>
      double OuterDiameter { get; }
  
      /// <summary>Gets the inlet water temperature [°C].</summary>
      double InletWaterTemperature { get; }
  
      /// <summary>Gets the ambient dry-bulb temperature [°C].</summary>
      double AmbientTemperature { get; }
  
      /// <summary>Gets the ambient humidity ratio [kg/kg].</summary>
      double AmbientHumidityRatio { get; }
  
      /// <summary>Gets the linear thermal transmittance (excluding convective resistances) [W/(m·K)].</summary>
      double LinearThermalTransmittance { get; }
  
      /// <summary>Gets the pipe length [m].</summary>
      double Length { get; }
  
      /// <summary>Gets the water flow rate [m³/s].</summary>
      double VolumetricFlowRate { get; }
  
      /// <summary>Gets the heat loss [W].</summary>
      double HeatLoss { get; }
  
      /// <summary>Gets the outlet water temperature [°C].</summary>
      [Obsolete("Misspelled name. Use OutletWaterTemperature instead. This member will be removed in a future major version.")]
      double OutletWaterTemperauture { get; }

      /// <summary>Gets the outlet water temperature [°C].</summary>
      /// <remarks>The default implementation forwards to the former (misspelled) member so that
      /// existing implementations keep compiling.</remarks>
#pragma warning disable CS0618
      double OutletWaterTemperature => OutletWaterTemperauture;
#pragma warning restore CS0618
    }
}
