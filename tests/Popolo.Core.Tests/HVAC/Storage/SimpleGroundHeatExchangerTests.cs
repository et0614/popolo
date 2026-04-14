/* SimpleGroundHeatExchanger.cs
 * 
 * Copyright (C) 2018 E.Togashi
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

namespace Popolo.Core.HVAC.Storage
{
  /// <summary>Simplified ground heat exchanger model based on two virtual mass points
  /// (Togashi et al., J. Environ. Eng. AIJ, Vol.83, No.747, 2018.
  /// https://doi.org/10.3130/aije.83.491).</summary>
  public class SimpleGroundHeatExchanger : IReadOnlySimpleGroundHeatExchanger
  {

    #region 列挙型

    /// <summary>Ground heat exchanger installation type.</summary>
    public enum Type
    {
      /// <summary>Vertical borehole type.</summary>
      Vertical,
      /// <summary>Horizontal buried pipe type.</summary>
      Horizontal
    }

    #endregion

    #region インスタンス変数・プロパティ

    /// <summary>Near-field soil temperature at the previous time step [°C].</summary>
    private double prevNearTemp;

    /// <summary>Far-field soil temperature at the previous time step [°C].</summary>
    private double prevDistTemp;

    /// <summary>Gets or sets the constant-temperature layer temperature [°C].</summary>
    public double ConstantGroundTemperature { get; set; } = 20;

    /// <summary>Gets the near-field soil temperature (Tcnt) [°C].</summary>
    public double NearGroundTemperature { get; private set; } = 20;

    /// <summary>Gets the far-field soil temperature (Tfar) [°C].</summary>
    public double DistantGroundTemperature { get; private set; } = 20;

    /// <summary>Gets or sets the heat transfer effectiveness of the ground heat exchanger (0–1) [-].</summary>
    public double Effectiveness { get; set; } = 0.7;

    /// <summary>Gets or sets the heat source fluid specific heat [kJ/(kg·K)].</summary>
    public double FluidSpecificHeat { get; set; } = 4.186;

    /// <summary>Gets or sets the heat conductance between near-field and far-field soil (Kcnt) [kW/K].</summary>
    public double NearGroundHeatConductance { get; set; }

    /// <summary>Gets or sets the heat conductance between far-field soil and constant-temperature layer (Kfar) [kW/K].</summary>
    public double DistantGroundHeatConductance { get; set; }

    /// <summary>Gets or sets the thermal capacitance of the near-field soil (Ccnt) [kJ/K].</summary>
    public double NearGroundHeatCapacity { get; set; }

    /// <summary>Gets or sets the thermal capacitance of the far-field soil (Cfar) [kJ/K].</summary>
    public double DistantGroundHeatCapacity { get; set; }

    /// <summary>Gets or sets the calculation time step [s].</summary>
    public double TimeStep { get; set; } = 3600d;

    /// <summary>Gets the heat source fluid inlet temperature (Twi) [°C].</summary>
    public double FluidInletTemperature { get; private set; }

    /// <summary>Gets the heat source fluid outlet temperature (Two) [°C].</summary>
    public double FluidOutletTemperature { get; private set; }

    /// <summary>Gets the heat source fluid mass flow rate [kg/s].</summary>
    public double FluidMassFlowRate { get; private set; }

    /// <summary>Gets the heat exchange rate [kW] (positive = heat extraction, negative = heat rejection).</summary>
    public double HeatExchange { get; private set; }

    #endregion

    #region コンストラクタ

    /// <summary>Initializes a new instance with default parameters from the reference paper.</summary>
    /// <param name="nominalFlowRate">Nominal fluid mass flow rate [kg/s].</param>
    /// <param name="fluidSpecificHeat">Heat source fluid specific heat [kJ/(kg·K)].</param>
    /// <param name="effectiveness">Heat transfer effectiveness [-] (0–1).</param>
    /// <param name="type">Installation type (Vertical or Horizontal).</param>
    public SimpleGroundHeatExchanger(double nominalFlowRate, double fluidSpecificHeat, double effectiveness, Type type)
    {
      InitTemperature(20);

      Effectiveness = effectiveness;
      FluidSpecificHeat = fluidSpecificHeat;
      double emc = effectiveness * nominalFlowRate * fluidSpecificHeat;

      //デフォルトパラメータ設定。詳細は文献参照
      if (type == Type.Horizontal)
      {
        NearGroundHeatConductance = 0.217 * emc;
        DistantGroundHeatConductance = 0.031 * emc;
        NearGroundHeatCapacity = 155.4e3 * emc;
        DistantGroundHeatCapacity = 840.4e3 * emc;
      }
      else
      {
        NearGroundHeatConductance = 0.373 * emc;
        DistantGroundHeatConductance = 0.153 * emc;
        NearGroundHeatCapacity = 5.8e3 * emc;
        DistantGroundHeatCapacity = 921.4e3 * emc;
      }
    }

    /// <summary>Initializes all soil temperatures to the specified value.</summary>
    /// <param name="groundTemperature">Ground temperature to initialize [°C].</param>
    public void InitTemperature(double groundTemperature)
    {
      NearGroundTemperature = DistantGroundTemperature =
        prevNearTemp = prevDistTemp = groundTemperature;
    }

    #endregion

    #region 状態更新処理

    /// <summary>Advances the ground temperature state by one time step (forecast mode).</summary>
    public void ForecastState(double fluidInletTemperature, double fluidMassFlowRate)
    {
      DistantGroundTemperature = prevDistTemp;
      NearGroundTemperature = prevNearTemp;

      double emc = Effectiveness * fluidMassFlowRate * FluidSpecificHeat;
      double aCnt = (emc * fluidInletTemperature + NearGroundHeatConductance * DistantGroundTemperature) / (emc + NearGroundHeatConductance);
      double bCnt = Math.Exp(-(emc + NearGroundHeatConductance) / NearGroundHeatCapacity * TimeStep);
      double aFar = (NearGroundHeatConductance * NearGroundTemperature + DistantGroundHeatConductance * ConstantGroundTemperature) / (NearGroundHeatConductance + DistantGroundHeatConductance);
      double bFar = Math.Exp(-(NearGroundHeatConductance + DistantGroundHeatConductance) / DistantGroundHeatCapacity * TimeStep);

      NearGroundTemperature = aCnt + (NearGroundTemperature - aCnt) * bCnt;
      DistantGroundTemperature = aFar + (DistantGroundTemperature - aFar) * bFar;

      FluidInletTemperature = fluidInletTemperature;
      FluidMassFlowRate = fluidMassFlowRate;
      if (FluidMassFlowRate <= 0)
      {
        HeatExchange = 0;
        FluidOutletTemperature = FluidInletTemperature;
      }
      else
      {
        HeatExchange = emc * (NearGroundTemperature - FluidInletTemperature);
        FluidOutletTemperature = FluidInletTemperature + HeatExchange / (FluidMassFlowRate * FluidSpecificHeat);
      }
    }

    /// <summary>Commits the forecasted state as the current state.</summary>
    public void FixState()
    {
      prevDistTemp = DistantGroundTemperature;
      prevNearTemp = NearGroundTemperature;
    }

    /// <summary>Advances the ground temperature state and commits it in one call.</summary>
    /// <param name="fluidInletTemperature"></param>
    /// <param name="fluidMassFlowRate"></param>
    public void Update(double fluidInletTemperature, double fluidMassFlowRate)
    {
      ForecastState(fluidInletTemperature, fluidMassFlowRate);
      FixState();
    }

    #endregion

  }
}