/* BuriedPipe.cs
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
  /// Represents a radiant heating or cooling pipe system embedded inside a
  /// <see cref="Wall"/> layer (typically a floor slab).
  /// </summary>
  /// <remarks>
  /// <para>
  /// A buried pipe exchanges heat with the surrounding wall node by circulating
  /// water through one or more parallel tube branches laid out at a given pitch.
  /// Each branch contributes a convective resistance inside the tube (from a
  /// Nusselt-number correlation for turbulent pipe flow), a conductive
  /// resistance across the tube wall, and fin resistances to the adjacent
  /// layer nodes above and below.
  /// </para>
  /// <para>
  /// Heat transfer is modeled by the ε-NTU method: given the water flow rate
  /// and inlet temperature, the pipe reports an effectiveness that the
  /// wall solver uses to determine the heat gain or loss at the embedded node
  /// and the outlet water temperature. Heat transfer is set to zero when the
  /// flow rate is zero.
  /// </para>
  /// <para>
  /// Pipes are attached to a specific node in the layer stack of a
  /// <see cref="Wall"/>, not to the wall surface; this allows the response
  /// factor model to capture the delayed and distributed nature of radiant
  /// floor/wall heating correctly.
  /// </para>
  /// </remarks>
  public class BuriedPipe : IReadOnlyBuriedPipe
  {

    #region プロパティ

    /// <summary>Gets the inlet water temperature [°C].</summary>
    /// <remarks>Used for the internal convective heat transfer coefficient calculation.</remarks>
    public double InletWaterTemperature { get; private set; } = 25;

    /// <summary>Gets the water mass flow rate [kg/s].</summary>
    public double WaterFlowRate { get; private set; }

    /// <summary>Gets the number of parallel pipe branches.</summary>
    public int BranchCount { get; private set; }

    /// <summary>Gets the pipe laying pitch [m].</summary>
    public double Pitch { get; private set; }

    /// <summary>Gets the total pipe length [m].</summary>
    public double Length { get; private set; }

    /// <summary>Gets the inner diameter of the pipe [m].</summary>
    public double InnerDiameter { get; private set; }

    /// <summary>Gets the outer diameter of the pipe [m].</summary>
    public double OuterDiameter { get; private set; }

    /// <summary>Gets the thermal conductivity of the pipe material [W/(m·K)].</summary>
    public double ThermalConductivityOfTube { get; private set; }

    /// <summary>Gets the fin efficiency of the upper fin [-].</summary>
    public double UpperFinEfficiency { get; private set; }

    /// <summary>Gets the fin efficiency of the lower fin [-].</summary>
    public double LowerFinEfficiency { get; private set; }

    /// <summary>Gets the heat transfer effectiveness (NTU method) [-].</summary>
    public double Effectiveness { get; private set; }

    #endregion

    #region インスタンスメソッド

    /// <summary>Initializes a new instance with pipe and fin geometry.</summary>
    /// <param name="pitch">Pipe laying pitch [m].</param>
    /// <param name="length">Total pipe length [m].</param>
    /// <param name="branchCount">Total number of parallel branches.</param>
    /// <param name="iDiameter">Inner diameter [m].</param>
    /// <param name="oDiameter">Outer diameter [m].</param>
    /// <param name="tubeConductivity">Pipe thermal conductivity [W/(m·K)].</param>
    /// <param name="upperFinConductivity">Upper fin thermal conductivity [W/(m·K)].</param>
    /// <param name="lowerFinConductivity">Lower fin thermal conductivity [W/(m·K)].</param>
    /// <param name="upperFinResistance">Upper fin thermal resistance [(m²·K)/W].</param>
    /// <param name="lowerFinResistance">Lower fin thermal resistance [(m²·K)/W].</param>
    /// <param name="upperFinThickness">Upper fin thickness [m].</param>
    /// <param name="lowerFinThickness">Lower fin thickness [m].</param>
    public BuriedPipe(double pitch, double length, int branchCount, double iDiameter, double oDiameter,
      double tubeConductivity, double upperFinConductivity, double lowerFinConductivity,
      double upperFinResistance, double lowerFinResistance, double upperFinThickness, double lowerFinThickness)
    {
      Pitch = pitch;
      Length = length;
      BranchCount = branchCount;
      InnerDiameter = iDiameter;
      OuterDiameter = oDiameter;
      ThermalConductivityOfTube = tubeConductivity;

      double bf1 = (pitch - oDiameter) / 2;
      double bf2 = oDiameter + (pitch - oDiameter);
      double ubL = bf1 * Math.Sqrt(1 / (upperFinResistance * upperFinConductivity * upperFinThickness));
      double ubR = bf1 * Math.Sqrt(1 / (lowerFinResistance * lowerFinConductivity * lowerFinThickness));
      UpperFinEfficiency = 1 / pitch * (bf2 * Math.Tanh(ubL) / ubL);
      LowerFinEfficiency = 1 / pitch * (bf2 * Math.Tanh(ubR) / ubR);
    }

    /// <summary>Sets the volumetric flow rate and updates the heat transfer effectiveness.</summary>
    /// <param name="flowRate">Volumetric flow rate [m³/s].</param>
    public void SetFlowRate(double flowRate)
    {
      WaterFlowRate = flowRate;
      UpdateEffectiveness();
    }

    /// <summary>Sets the inlet water temperature and updates the heat transfer effectiveness.</summary>
    /// <param name="temperature">Inlet water temperature [°C].</param>
    public void SetWaterTemperature(double temperature)
    {
      InletWaterTemperature = temperature;
      UpdateEffectiveness();
    }

    /// <summary>Updates the heat transfer effectiveness from the current flow rate and water temperature.</summary>
    private void UpdateEffectiveness()
    {
      if (WaterFlowRate <= 0)
      {
        Effectiveness = 0;
        return;
      }

      //対流熱伝達率[W/m2K]の計算//////////////////////////
      //動粘性係数[m2/s]・熱拡散率[m2/s]・熱伝導率[W/(m·K)]を計算
      double v = Water.GetLiquidDynamicViscosity(InletWaterTemperature);
      double a = Water.GetLiquidThermalDiffusivity(InletWaterTemperature);
      double lambda = Water.GetLiquidThermalConductivity(InletWaterTemperature);

      //配管内流速[m/s]を計算
      double vFlow = WaterFlowRate / (PhysicsConstants.NominalWaterDensity * BranchCount);
      double u = vFlow / (Math.Pow(InnerDiameter / 2, 2) * Math.PI);

      //ヌセルト数を計算
      double reNumber = u * InnerDiameter / v;
      double prNumber = v / a;
      double nuNumber = 0.023 * Math.Pow(reNumber, 0.8) * Math.Pow(prNumber, 0.4);

      //ヌセルト数から対流熱伝達率を計算
      double hi = nuNumber * lambda / InnerDiameter;

      //伝熱係数KA, 移動単位数NTU, 熱通過率εの計算/////////
      double ka = 1 / (InnerDiameter * hi)
        + 1 / (2 * ThermalConductivityOfTube) * Math.Log(OuterDiameter / InnerDiameter);
      ka = (Math.PI * Length) / ka;
      double ntu = ka / (WaterFlowRate * PhysicsConstants.NominalWaterIsobaricSpecificHeat);
      Effectiveness = 1 - Math.Exp(-ntu);
    }

    #endregion

  }

  /// <summary>
  /// Represents a read-only view of a buried radiant pipe embedded in a wall
  /// or floor layer.
  /// </summary>
  /// <remarks>
  /// Exposes pipe geometry (pitch, length, branch count, diameters), the
  /// thermal conductivity of the pipe material, the current inlet water
  /// temperature and mass flow rate, and derived quantities used by the
  /// wall solver (fin efficiencies and ε-NTU effectiveness). See
  /// <see cref="BuriedPipe"/> for the mutable implementation.
  /// </remarks>
  public interface IReadOnlyBuriedPipe
  {
    /// <summary>Gets the inlet water temperature [°C].</summary>
    /// <remarks>Used for the internal convective heat transfer coefficient calculation.</remarks>
    double InletWaterTemperature { get; }

    /// <summary>Gets the water mass flow rate [kg/s].</summary>
    double WaterFlowRate { get; }

    /// <summary>Gets the number of parallel pipe branches.</summary>
    int BranchCount { get; }

    /// <summary>Gets the pipe laying pitch [m].</summary>
    double Pitch { get; }

    /// <summary>Gets the total pipe length [m].</summary>
    double Length { get; }

    /// <summary>Gets the inner diameter of the pipe [m].</summary>
    double InnerDiameter { get; }

    /// <summary>Gets the outer diameter of the pipe [m].</summary>
    double OuterDiameter { get; }

    /// <summary>Gets the thermal conductivity of the pipe material [W/(m·K)].</summary>
    double ThermalConductivityOfTube { get; }

    /// <summary>Gets the fin efficiency of the upper fin [-].</summary>
    double UpperFinEfficiency { get; }

    /// <summary>Gets the fin efficiency of the lower fin [-].</summary>
    double LowerFinEfficiency { get; }

    /// <summary>Gets the heat transfer effectiveness (NTU method) [-].</summary>
    double Effectiveness { get; }
  }

}
