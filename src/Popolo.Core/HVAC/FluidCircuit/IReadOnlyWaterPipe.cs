/* IReadOnlyWaterPipe.cs
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
      double OutletWaterTemperauture { get; }
    }
}
