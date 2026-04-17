/* AirGapLayer.cs
 * 
 * Copyright (C) 2015 E.Togashi
 * 
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 2 of the License, or (at
 * your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful, but
 * WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 675 Mass Ave, Cambridge, MA 02139, USA.
 */

using System;
using Popolo.Core.Physics;

namespace Popolo.Core.Building.Envelope
{
  /// <summary>Represents a wall layer consisting of an air gap, with fixed thermal resistance.</summary>
  public class AirGapLayer : WallLayer
  {

    /// <summary>Gets a value indicating whether the air gap is sealed (still air).</summary>
    public bool IsSealed { get; private set; }

    /// <summary>Initializes a new instance using the standard thermal resistance for a sealed or ventilated air gap.</summary>
    /// <param name="name">Layer name.</param>
    /// <param name="isSealed">True for a sealed (still) air gap; false for a ventilated gap.</param>
    /// <param name="thickness">Layer thickness [m].</param>
    public AirGapLayer(string name, bool isSealed, double thickness)
    {
      //年平均として常温20C/60%程度の値を採用
      ThermalConductivity = MoistAir.GetThermalConductivity(20);
      VolSpecificHeat = MoistAir.GetSpecificHeat(0.01) * PhysicsConstants.NominalMoistAirDensity;

      IsVariableProperties = false;
      Name = name;
      Thickness = Math.Max(0.001, thickness);
      IsSealed = isSealed;

      if (isSealed) HeatConductance = 1 / 0.15;
      else HeatConductance = 1 / 0.07;
      HeatCapacity_B = HeatCapacity_F = 0.5 * VolSpecificHeat * Thickness * 1000;
    }

    /// <summary>Initializes a new instance with a specified thermal resistance.</summary>
    /// <param name="name">Layer name.</param>
    /// <param name="resistance">Thermal resistance [m²·K/W].</param>
    /// <param name="thickness">Layer thickness [m].</param>
    public AirGapLayer(string name, double resistance, double thickness)
    {
      IsVariableProperties = false;
      Name = name;
      Thickness = Math.Max(0.001, thickness);

      IsSealed = true;
      HeatConductance = 1d / resistance;
      HeatCapacity_B = HeatCapacity_F = 0.5 * VolSpecificHeat * Thickness * 1000;
    }

    /// <summary>Initializes a new instance with a default name based on the sealed flag.</summary>
    /// <param name="isSealed">True for a sealed air gap; false for a ventilated gap.</param>
    /// <param name="thickness">Layer thickness [m].</param>
    public AirGapLayer(bool isSealed, double thickness) :
      this(isSealed ? "Sealed Air Gap Layer" : "Air Gap Layer", isSealed, thickness)
    { }

  }
}
