/* PCMWallLayer.cs
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
using System.Collections.Generic;

namespace Popolo.Core.Building.Envelope
{
  /// <summary>
  /// Represents a wall layer containing phase-change material (PCM) that
  /// stores and releases latent heat across a solid-liquid transition.
  /// </summary>
  /// <remarks>
  /// <para>
  /// PCM layers exhibit three phases — <see cref="State.Solid"/>,
  /// <see cref="State.Equilibrium"/> (mushy zone), and <see cref="State.Liquid"/> —
  /// with distinct thermal conductivity and volumetric specific heat in each.
  /// The effective volumetric specific heat in the equilibrium zone is inflated
  /// to account for the latent heat absorbed or released during phase change.
  /// </para>
  /// <para>
  /// The F side and B side can be in different phase states simultaneously.
  /// When either side changes phase, the overall thermal conductance and the
  /// per-side heat capacities (<see cref="IReadOnlyWallLayer.HeatCapacity_F"/> /
  /// <see cref="IReadOnlyWallLayer.HeatCapacity_B"/>) are recomputed from the
  /// current phase assignments. Because properties vary with state,
  /// <see cref="IReadOnlyWallLayer.IsVariableProperties"/> is always true.
  /// </para>
  /// <para>
  /// Typical use cases include passive indoor temperature regulation in walls
  /// or floors charged during peak solar hours and discharged overnight.
  /// </para>
  /// </remarks>
  public class PCMWallLayer : WallLayer
  {

    #region 列挙型

    /// <summary>Specifies the phase state of the PCM layer.</summary>
    [Flags]
    public enum State
    {
      /// <summary>Solid phase.</summary>
      Solid = 1,
      /// <summary>Phase equilibrium (mushy zone).</summary>
      Equilibrium = 2,
      /// <summary>Liquid phase.</summary>
      Liquid = 4
    }

    #endregion

    #region インスタンス変数・プロパティ

    /// <summary>Thermophysical properties for each phase.</summary>
    private Dictionary<State, WallLayer> layers = new Dictionary<State, WallLayer>();

    /// <summary>Gets the current phase state on the B side.</summary>
    public State CurrentState_B { get; private set; }

    /// <summary>Gets the current phase state on the F side.</summary>
    public State CurrentState_F { get; private set; }

    /// <summary>Gets the previous phase state on the B side.</summary>
    public State LastState_B { get; private set; }

    /// <summary>Gets the previous phase state on the F side.</summary>
    public State LastState_F { get; private set; }

    /// <summary>Gets the freezing (solidification) temperature [°C].</summary>
    public double FreezingTemperature { get; private set; }

    /// <summary>Gets the melting temperature [°C].</summary>
    public double MeltingTemperature { get; private set; }

    #endregion

    #region インスタンスメソッド

    /// <summary>Initializes a new PCM wall layer with three phase states.</summary>
    /// <param name="name">Layer name.</param>
    /// <param name="freezingTemperature">Freezing (solidification) temperature [°C].</param>
    /// <param name="meltingTemperature">Melting temperature [°C].</param>
    /// <param name="thickness">Layer thickness [m].</param>
    /// <param name="solidLayer">Thermophysical properties for the solid phase.</param>
    /// <param name="equilibriumLayer">Thermophysical properties for the equilibrium (mushy) zone.</param>
    /// <param name="liquidLayer">Thermophysical properties for the liquid phase.</param>
    public PCMWallLayer(string name, double freezingTemperature, double meltingTemperature,
      double thickness, WallLayer solidLayer, WallLayer equilibriumLayer, WallLayer liquidLayer)

    {
      IsVariableProperties = true;
      Name = name;
      FreezingTemperature = freezingTemperature;
      MeltingTemperature = meltingTemperature;
      Thickness = thickness;
      layers.Add(State.Solid, solidLayer);
      layers.Add(State.Equilibrium, equilibriumLayer);
      layers.Add(State.Liquid, liquidLayer);

      CurrentState_F = CurrentState_B = State.Equilibrium;
      UpdateState(freezingTemperature, freezingTemperature);
    }

    /// <summary>Determines the phase state from the given temperature.</summary>
    /// <param name="temp">Temperature [°C].</param>
    /// <returns>The corresponding phase state.</returns>
    private State GetState(double temp)
    {
      if (temp <= FreezingTemperature) return State.Solid;
      if (temp < MeltingTemperature) return State.Equilibrium;
      else return State.Liquid;
    }

    /// <summary>Gets the heat capacity [J/(m²·K)] for the specified phase state.</summary>
    /// <param name="state">Phase state.</param>
    /// <returns>Heat capacity [J/(m²·K)].</returns>
    public double GetHeatCapacity(State state)
    { return 0.5 * layers[state].VolSpecificHeat * Thickness * 1000d; }

    /// <summary>Updates thermophysical properties based on layer-end temperatures and current phase states.</summary>
    /// <param name="temperatureF">Temperature on the F side [°C].</param>
    /// <param name="temperatureB">Temperature on the B side [°C].</param>
    /// <returns>True if the phase state changed; otherwise false.</returns>
    public override bool UpdateState(double temperatureF, double temperatureB)
    {
      LastState_F = CurrentState_F;
      LastState_B = CurrentState_B;
      CurrentState_F = GetState(temperatureF);
      CurrentState_B = GetState(temperatureB);

      bool phaseChanged = false;
      if ((CurrentState_F != LastState_F) || (CurrentState_B != LastState_B))
        phaseChanged = true;

      if (phaseChanged)
      {
        double res =
          Thickness / layers[CurrentState_F].ThermalConductivity +
          Thickness / layers[CurrentState_B].ThermalConductivity;
        HeatConductance = 2d / res;
        double th1000 = Thickness * 1000d;
        HeatCapacity_B = 0.5 * layers[CurrentState_B].VolSpecificHeat * th1000;
        HeatCapacity_F = 0.5 * layers[CurrentState_F].VolSpecificHeat * th1000;

        ThermalConductivity = HeatConductance * Thickness;
        VolSpecificHeat = (HeatCapacity_F + HeatCapacity_B) / th1000;
      }

      return phaseChanged;
    }

    #endregion

  }
}
