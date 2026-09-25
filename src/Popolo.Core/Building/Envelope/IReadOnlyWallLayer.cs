/* IReadOnlyWallLayer.cs
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

namespace Popolo.Core.Building.Envelope
{
  /// <summary>
  /// Represents a read-only view of a single layer in a multi-layer wall
  /// or floor assembly.
  /// </summary>
  /// <remarks>
  /// <para>
  /// A wall layer exposes the thermophysical properties required by the one-dimensional
  /// heat-conduction model that underlies <see cref="IReadOnlyWall"/>:
  /// thermal conductivity, volumetric specific heat, thickness, and the derived
  /// thermal conductance and heat capacity. Moisture transfer requires three
  /// additional properties: moisture conductivity, moisture capacity, and the
  /// absorption/release coefficients (<see cref="KappaC"/> and <see cref="NuC"/>).
  /// </para>
  /// <para>
  /// The heat capacity is reported <b>per side</b>
  /// (<see cref="HeatCapacity_F"/> and <see cref="HeatCapacity_B"/>). In the
  /// underlying finite-difference discretization, each layer contributes
  /// half of its thermal mass to the node on the F side and half to the node
  /// on the B side; the two values therefore typically satisfy
  /// <c>HeatCapacity_F == HeatCapacity_B == 0.5 · VolSpecificHeat · Thickness · 1000</c>.
  /// Subtypes such as <c>PCMWallLayer</c> may report asymmetric values
  /// when the two sides are in different phase states.
  /// </para>
  /// <para>
  /// <see cref="IsVariableProperties"/> is true when the layer's thermophysical
  /// properties depend on current state (temperature, humidity, phase);
  /// such layers recompute their properties during the solution loop.
  /// The <see cref="Kind"/> discriminator identifies the concrete subtype and
  /// allows reflection-free serialization (see the value list below).
  /// </para>
  /// </remarks>
  public interface IReadOnlyWallLayer
  {

    #region Properties

    /// <summary>Gets the discriminator identifying the concrete layer type.
    /// Used by serializers to distinguish subtypes without reflection.</summary>
    /// <remarks>
    /// Expected values:
    /// <list type="bullet">
    ///   <item><description><c>"wallLayer"</c> — solid layer with thermal/moisture properties.</description></item>
    ///   <item><description><c>"airGapLayer"</c> — air gap with fixed thermal resistance.</description></item>
    /// </list>
    /// Subtypes should override this property to return their own discriminator.
    /// </remarks>
    string Kind { get; }

    /// <summary>Gets or sets the name of the layer.</summary>
    string Name { get; }

    /// <summary>Gets a value indicating whether thermophysical properties can change with temperature.</summary>
    bool IsVariableProperties { get; }

    /// <summary>Gets the thermal conductivity [W/(m·K)].</summary>
    double ThermalConductivity { get; }

    /// <summary>Gets the moisture conductivity [(kg/s)/((kg/kg)·m)].</summary>
    double MoistureConductivity { get; }

    /// <summary>Gets the volumetric specific heat [kJ/(m³·K)].</summary>
    double VolSpecificHeat { get; }

    /// <summary>Gets the thermal conductance [W/(m²·K)].</summary>
    double HeatConductance { get; }

    /// <summary>Gets the sensible heat capacity on the F side [J/(m²·K)].</summary>
    double HeatCapacity_F { get; }

    /// <summary>Gets the sensible heat capacity on the B side [J/(m²·K)].</summary>
    double HeatCapacity_B { get; }

    /// <summary>Gets the moisture capacity [kg/m²].</summary>
    double WaterCapacity { get; }

    /// <summary>Gets the moisture absorption coefficient per unit humidity difference [kg/m²].</summary>
    double KappaC { get; }

    /// <summary>Gets the moisture release coefficient per unit temperature difference [kg/(m²·K)].</summary>
    double NuC { get; }

    /// <summary>Gets the layer thickness [m].</summary>
    double Thickness { get; }

    #endregion

  }
}