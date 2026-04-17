/* ParallelFluidMachineries.cs
 * 
 * Copyright (C) 2015 E.Togashi
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
using System.Collections.Generic;

using Popolo.Core.Exceptions;
using Popolo.Core.Numerics;

namespace Popolo.Core.HVAC.FluidCircuit
{
  /// <summary>Represents a group of fluid machines connected in parallel.</summary>
  public class ParallelFluidMachineries: ICircuitBranch
  {

    #region インスタンス変数・プロパティ

    /// <summary>List of fluid machines.</summary>
    private List<FluidMachinery> allFlds = new List<FluidMachinery>();

    /// <summary>List of fluid machines for each stage.</summary>
    private List<FluidMachinery>[] flds;

    /// <summary>Gets the list of fluid machines.</summary>
    public IReadOnlyFluidMachinery[] FluidMachineries
    { get { return allFlds.ToArray(); } }
    
    /// <summary>Gets or sets the rotation speed ratio [-].</summary>
    public double RotationRatio { get; set; }

    /// <summary>Gets the minimum rotation speed ratio [-].</summary>
    public double MinimumRotationRatio
    {
      get
      {
        double min = 0.001;
        foreach (FluidMachinery fm in flds[Stage - 1])
          if (min < fm.MinimumRotationRatio) min = fm.MinimumRotationRatio;
        return min;
      }
    }

    /// <summary>Gets the bypass flow rate [m³/s].</summary>
    public double BypassFlowRate { get; private set; }

    /// <summary>Gets the design pressure [kPa].</summary>
    public double DesignPressure { get; private set; }

    /// <summary>Gets the maximum number of stages.</summary>
    public int MaxStageCount { get; private set; }

    /// <summary>Gets or sets the minimum differential pressure [kPa].</summary>
    /// <remarks>Differential pressure of the bypass circuit under full bypass conditions.</remarks>
    public double MinimumPressure { get; set; }

    /// <summary>Gets or sets the number of operating stages.</summary>
    public int Stage { get; set; }

    #endregion

    #region コンストラクタ

    /// <summary>Static constructor.</summary>
    /// <param name="maxStageCount">Number of control stages.</param>
    /// <param name="designPressure">Design pressure [kPa].</param>
    public ParallelFluidMachineries(int maxStageCount, double designPressure)
    {
      if (maxStageCount <= 0) throw new PopoloOutOfRangeException(
        nameof(maxStageCount), maxStageCount, 1, null);
      MaxStageCount = maxStageCount;
      RotationRatio = 1.0;
      DesignPressure = designPressure;
      MinimumPressure = 20;

      flds = new List<FluidMachinery>[maxStageCount];
      for (int i = 0; i < flds.Length; i++) flds[i] = new List<FluidMachinery>();
    }

    #endregion

    #region インスタンスメソッド

    /// <summary>Passes fluid through the branch.</summary>
    /// <param name="flowRate">Flow rate [m³/s].</param>
    /// <param name="pressure">Differential pressure [kPa].</param>
    /// <returns>True if fluid can flow through the branch.</returns>
    public bool TryToRunFluid(double flowRate, double pressure)
    {
      //一旦、全台停止
      foreach (FluidMachinery fm in allFlds) fm.ShutOff();
      pressure = Math.Max(MinimumPressure, pressure);

      for (Stage = 1; Stage <= MaxStageCount; Stage++)
      {
        VolumetricFlowRate = 0;
        foreach (FluidMachinery fm in flds[Stage - 1])
        {
          fm.updateWithRotationRatioAndPressure(1.0, pressure);
          VolumetricFlowRate += fm.VolumetricFlowRate;
        }
        if (flowRate < VolumetricFlowRate)
        {
          Roots.ErrorFunction eFnc = delegate (double rr)
          {
            VolumetricFlowRate = 0;
            foreach (FluidMachinery fm in flds[Stage - 1])
            {
              fm.updateWithRotationRatioAndPressure(rr, pressure);
              VolumetricFlowRate += fm.VolumetricFlowRate;
            }
            return VolumetricFlowRate - flowRate;
          };
          //収束計算//0.0001の加算は解がある側へシフトさせる保険
          //PQ特性切片付近の極微小流量で差圧不足となる場合があるため
          RotationRatio = Roots.NewtonBisection(eFnc, 1.0, 0.0001, 0.0001, 0.0001, 20);
          RotationRatio = Math.Max(0.0001 + RotationRatio, MinimumRotationRatio);
          BypassFlowRate = eFnc(RotationRatio);
          return true;
        }
      }
      Stage = MaxStageCount;
      return false;
    }

    /// <summary>Registers a fluid machine.</summary>
    /// <param name="stage">Stage number to register (1-based).</param>
    /// <param name="fm">Fluid machine to register.</param>
    public void AddFluidMachinery(int stage, FluidMachinery fm)
    {
      if (!allFlds.Contains(fm)) allFlds.Add(fm);
      if (!flds[stage - 1].Contains(fm)) flds[stage - 1].Add(fm);
    }

    /// <summary>Unregisters a fluid machine.</summary>
    /// <param name="stage">Stage number to unregister (1-based).</param>
    /// <param name="fm">Fluid machine to unregister.</param>
    public void RemoveFluidMachinery(int stage, FluidMachinery fm)
    {
      if (flds[stage - 1].Contains(fm)) flds[stage - 1].Remove(fm);
      for (int i = 0; i < flds.Length; i++)
        if (flds[i].Contains(fm)) return;
      allFlds.Remove(fm);
    }

    /// <summary>Gets the power consumption [kW].</summary>
    public double GetElectricConsumption()
    {
      double se = 0;
      foreach (FluidMachinery fm in allFlds) se += fm.GetElectricConsumption();
      return se;
    }

    #endregion

    #region ICircuitBranch実装

    /// <summary>Gets or sets the flow rate [m³/s].</summary>
    public double VolumetricFlowRate { get; set; }

    /// <summary>Gets or sets the upstream node.</summary>
    public CircuitNode? UpStreamNode { get; set; }

    /// <summary>Gets or sets the downstream node.</summary>
    public CircuitNode? DownStreamNode { get; set; }

    /// <summary>Computes the volumetric flow rate [m³/s] from the differential pressure.</summary>
    /// <returns>Volumetric flow rate [m³/s].</returns>
    public void UpdateFlowRateFromNodePressureDifference()
    {
      VolumetricFlowRate = 0;
      if (Stage == 0) return;
      foreach (FluidMachinery fm in flds[Stage - 1])
      {
        fm.UpStreamNode = UpStreamNode;
        fm.DownStreamNode = DownStreamNode;
        fm.UpdateFlowRateFromNodePressureDifference();
        VolumetricFlowRate += fm.VolumetricFlowRate;
      }
    }

    #endregion

  }
}