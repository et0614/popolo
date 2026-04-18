/* ControllableParallelFlow.cs
 * 
 * Copyright (C) 2018 E.Togashi
 * 
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 3 of the License, or (at
 * your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful, but
 * WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301, USA.
 */

using System;

namespace Popolo.Core.HVAC.FluidCircuit.ControllableFlowSolver
{
  /// <summary>Represents a variable-flow parallel circuit controlled by resistance adjustment.</summary>
  public class ControllableParallelFlow : IFlowControllableBranch
  {

    #region インスタンス変数・プロパティ

    /// <summary>List of variable-flow branches controllable by resistance adjustment.</summary>
    private IFlowControllableBranch[] branches;

    /// <summary>Gets or sets the list of forward-flow resistances.</summary>
    public double[] SupplyResistances { get; set; }

    /// <summary>Gets or sets the list of return-flow resistances.</summary>
    public double[] ReturnResistances { get; set; }

    /// <summary>Composite resistance coefficient of the entire circuit.</summary>
    private double ttlResist = 0;

    #endregion

    #region コンストラクタ

    /// <summary>Initializes a new instance.</summary>
    /// <param name="branches">List of variable-flow branches controllable by resistance adjustment.</param>
    public ControllableParallelFlow(IFlowControllableBranch[] branches)
    {
      this.branches = branches;
      SupplyResistances = new double[branches.Length];
      ReturnResistances = new double[branches.Length];
    }

    #endregion

    #region IFlowControllableBranch実装

    /// <summary>Gets a value indicating whether the composite resistance has changed.</summary>
    public bool HasTotalResistanceChanged
    {
      get
      {
        foreach (IFlowControllableBranch brch in branches)
          if (brch.HasTotalResistanceChanged) return true;
        return false;
      }
    }

    /// <summary>Gets the total circuit flow rate [m³/s].</summary>
    public double TotalFlowRate { get; private set; }

    /// <summary>Gets the total target flow rate of the circuit [m³/s].</summary>
    /// <returns>Total target flow rate of the circuit [m³/s].</returns>
    public double GetTotalFlowSetpoint()
    {
      double ttlFlw = 0;
      for (int i = 0; i < branches.Length; i++)
        ttlFlw += branches[i].GetTotalFlowSetpoint();
      return ttlFlw;
    }

    /// <summary>Gets the required minimum differential pressure [kPa].</summary>
    /// <returns>Required minimum differential pressure [kPa].</returns>
    public double GetMinPressure()
    {
      double ttlFlw = GetTotalFlowSetpoint();
      double prlP = 0;
      double minP = 0;
      for (int i = 0; i < branches.Length; i++)
      {
        prlP += ttlFlw * ttlFlw
          * (SupplyResistances[i] + ReturnResistances[i]);
        minP = Math.Max(prlP + branches[i].GetMinPressure(), minP);
        ttlFlw -= branches[i].GetTotalFlowSetpoint();
      }
      return minP;
    }

    /// <summary>Gets the composite resistance of the entire circuit [kPa/(m³/s)²].</summary>
    /// <returns>Composite resistance of the entire circuit [kPa/(m³/s)²].</returns>
    public double GetTotalResistance()
    {
      //抵抗係数に変更がなければ前回の計算結果を使用
      if (!HasTotalResistanceChanged) return ttlResist;

      //最遠方から順に合成する
      int indx = branches.Length - 1;
      ttlResist = branches[indx].GetTotalResistance() + SupplyResistances[indx] + ReturnResistances[indx];
      for (int i = branches.Length - 2; 0 <= i; i--)
      {
        ttlResist = 1 / Math.Pow(Math.Sqrt(1 / ttlResist) + Math.Sqrt(1 / branches[i].GetTotalResistance()), 2);
        ttlResist += SupplyResistances[i] + ReturnResistances[i];
      }
      return ttlResist;
    }

    /// <summary>Adjusts the flow rate based on the differential pressure.</summary>
    /// <param name="pressure">Differential pressure [kPa].</param>
    public void ControlFlowRate(double pressure)
    {
      TotalFlowRate = Math.Sqrt(pressure / GetTotalResistance());
      double ttlFlw = TotalFlowRate;
      for (int i = 0; i < branches.Length; i++)
      {
        if (ttlFlw <= 0) pressure = 0;
        else pressure -= ttlFlw * ttlFlw
            * (SupplyResistances[i] + ReturnResistances[i]);
        branches[i].ControlFlowRate(pressure);
        ttlFlw -= branches[i].TotalFlowRate;
      }
    }

    #endregion

  }
}
