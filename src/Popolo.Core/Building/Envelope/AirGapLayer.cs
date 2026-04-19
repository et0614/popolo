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
  /// <summary>
  /// Represents a wall layer consisting of an air gap, modeled with a fixed
  /// thermal resistance.
  /// </summary>
  /// <remarks>
  /// <para>
  /// Air gaps are treated as a lumped thermal resistance rather than as a
  /// fluid domain, which is sufficient for whole-building thermal load
  /// calculations. Two standard resistances are provided as defaults:
  /// <list type="bullet">
  ///   <item><description><c>0.15 m²·K/W</c> for a <b>sealed</b> gap (still air, only conduction and radiation).</description></item>
  ///   <item><description><c>0.07 m²·K/W</c> for a <b>ventilated</b> gap (slight convective exchange with outside).</description></item>
  /// </list>
  /// A constructor overload accepts a user-specified resistance for non-standard
  /// configurations.
  /// </para>
  /// <para>
  /// The air inside the gap is assigned a small thermal mass based on its
  /// volumetric specific heat at 20 °C / 60 % RH and the specified thickness.
  /// Thermal properties do not vary with state, so
  /// <see cref="IReadOnlyWallLayer.IsVariableProperties"/> is always false.
  /// For cases where the convective exchange must be resolved explicitly
  /// (e.g., attic spaces), use <see cref="HorizontalAirChamber"/> instead.
  /// </para>
  /// </remarks>
  public class AirGapLayer : WallLayer
  {

    /// <summary>Gets the discriminator identifying this layer as an air gap.</summary>
    /// <remarks>Returns <c>"airGapLayer"</c>, overriding the base class value.</remarks>
    public override string Kind => "airGapLayer";

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