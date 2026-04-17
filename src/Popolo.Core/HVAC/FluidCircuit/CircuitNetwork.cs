/* CircuitNetwork.cs
 * 
 * Copyright (C) 2014 E.Togashi
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

using Popolo.Core.Exceptions;
using Popolo.Core.Numerics;
using Popolo.Core.Numerics.LinearAlgebra;
using System;
using System.Collections.Generic;

namespace Popolo.Core.HVAC.FluidCircuit
{
  /// <summary>Represents a fluid circuit network solved by linear algebra.</summary>
  public class CircuitNetwork
  {

    #region インスタンス変数・プロパティ

    /// <summary>List of circuit branches.</summary>
    private List<ICircuitBranch> branches = new List<ICircuitBranch>();

    /// <summary>List of circuit nodes.</summary>
    private List<CircuitNode> nodes = new List<CircuitNode>();

    /// <summary>Gets a node from the network.</summary>
    public CircuitNode[] Nodes { get { return nodes.ToArray(); } }

    /// <summary>Gets the pressure-reference node, or <see langword="null"/> if not yet set.</summary>
    /// <remarks>
    /// Set by <see cref="SetBasePressure"/>. Must be non-null before calling <see cref="Solve()"/>.
    /// </remarks>
    public CircuitNode? BasePressureNode { get; private set; }

    /// <summary>Gets the reference pressure [kPa].</summary>
    public double BasePressure { get; private set; }

    /// <summary>Gets the number of iterations performed.</summary>
    public int Iteration { get; private set; }

    /// <summary>Gets the residual error.</summary>
    public double Error { get; private set; }

    /// <summary>Gets a branch.</summary>
    public IReadOnlyCircuitBranch[] Branches { get { return branches.ToArray(); } }

    /// <summary>Gets or sets the convergence tolerance.</summary>
    public double ErrorTolerance { get; set; } = 2.5e-4;

    /// <summary>Gets or sets the convergence criterion for the input change rate.</summary>
    public double CollectionTolerance { get; set; } = 2.5e-4;

    #endregion

    #region 回路網構築処理

    /// <summary>Adds a node to the network.</summary>
    /// <returns>Node index.</returns>
    public CircuitNode AddNode()
    {
      CircuitNode nd = new CircuitNode();
      nodes.Add(nd);
      if (nodes.Count == 1) SetBasePressure(nd, 0);
      return nd;
    }

    /// <summary>Removes a node from the network.</summary>
    /// <param name="node"></param>
    /// <returns>True if removal succeeded.</returns>
    public bool RemoveNode(CircuitNode node)
    {
      if (nodes.Contains(node))
      {
        //接続中の節点は削除不可
        foreach (ICircuitBranch br in branches)
          if (br.UpStreamNode == node || br.DownStreamNode == node) return false;
        nodes.Remove(node);
        BasePressureNode = null;
        return true;
      }
      else return false;
    }

    /// <summary>Connects nodes via a branch.</summary>
    /// <param name="branch">Branch.</param>
    /// <param name="ndFrom">Source node.</param>
    /// <param name="ndTo">Destination node.</param>
    public void ConnectNode(ICircuitBranch branch, CircuitNode ndFrom, CircuitNode ndTo)
    {
      if (!nodes.Contains(ndFrom) || !nodes.Contains(ndTo)) throw new PopoloArgumentException(
        "Both nodes must be registered to this network before connecting.",
        !nodes.Contains(ndFrom) ? nameof(ndFrom) : nameof(ndTo));
      if (!branches.Contains(branch)) branches.Add(branch);
      //接続処理
      branch.UpStreamNode = ndFrom;
      branch.DownStreamNode = ndTo;
      ndFrom.addOutFlowBranch(branch);
      ndTo.addInFlowBranch(branch);
    }

    /// <summary>Removes a branch.</summary>
    /// <param name="branch">Branch.</param>
    public void RemoveBranch(ICircuitBranch branch)
    {
      branch.UpStreamNode!.removeBranch(branch);
      branch.DownStreamNode!.removeBranch(branch);
      branches.Remove(branch);
    }

    /// <summary>Sets the reference pressure.</summary>
    /// <param name="node">Circuit node.</param>
    /// <param name="pressure">Reference pressure [kPa].</param>
    public void SetBasePressure(CircuitNode node, double pressure)
    {
      BasePressureNode = node;
      BasePressure = pressure;
    }

    #endregion

    #region 収束計算処理

    /// <summary>Solves the circuit network.</summary>
    /// <returns>True if convergence succeeded.</returns>
    public bool Solve()
    {
      if (BasePressureNode == null)
        throw new PopoloInvalidOperationException(nameof(CircuitNetwork), nameof(BasePressureNode));

      //入力値ベクトルを作成する
      List<double> inp = new List<double>();
      for (int i = 0; i < nodes.Count; i++)
      {
        if (nodes[i].IsPressureFixed) inp.Add(nodes[i].Inflow);
        else inp.Add(nodes[i].Pressure);
      }
      IVector inputs = new Vector(inp.Count);
      for (int i = 0; i < inp.Count; i++) inputs[i] = inp[i];

      //ニュートン法で解く
      int iter;
      double err;
      bool success = MultiRoots.Newton(ErrorFNC, ref inputs, ErrorTolerance, CollectionTolerance, 100, out iter, out err); //2016.01.05:E.Togashi 精度変更。1CMH相当。十分のはず。
      Iteration = iter;
      Error = err;

      //基準点に合わせて絶対圧力を調整する
      double dp = BasePressure - BasePressureNode!.Pressure;
      foreach (CircuitNode nd in nodes) nd.Pressure += dp;

      return success;
    }

    /// <summary>Solves the circuit network.</summary>]
    /// <param name="antiVibrationC">Anti-vibration coefficient [-] (0.0–1.0).</param>
    /// <remarks>For models including pumps or fans, an anti-vibration coefficient of approximately 0.5 is empirically effective for convergence.</remarks>
    /// <returns>True if convergence succeeded.</returns>
    public bool Solve(double antiVibrationC)
    {
      if (BasePressureNode == null)
        throw new PopoloInvalidOperationException(nameof(CircuitNetwork), nameof(BasePressureNode));
      //入力値ベクトルを作成する
      List<double> inp = new List<double>();
      for (int i = 0; i < nodes.Count; i++)
      {
        if (nodes[i].IsPressureFixed) inp.Add(nodes[i].Inflow);
        else inp.Add(nodes[i].Pressure);
      }
      IVector inputs = new Vector(inp.Count);
      for (int i = 0; i < inp.Count; i++) inputs[i] = inp[i];

      //ニュートン法で解く
      int iter;
      double err;
      bool success = MultiRoots.Newton(ErrorFNC, ref inputs, ErrorTolerance, CollectionTolerance, 100, antiVibrationC, out iter, out err);
      Iteration = iter;
      Error = err;

      //基準点に合わせて絶対圧力を調整する
      double dp = BasePressure - BasePressureNode!.Pressure;
      foreach (CircuitNode nd in nodes) nd.Pressure += dp;

      return success;
    }

    /// <summary>Error function for iterative convergence.</summary>
    /// <param name="inputs">Input values.</param>
    /// <param name="outputs">Output values.</param>
    private void ErrorFNC(IVector inputs, ref IVector outputs)
    {
      //節点圧力および固定流出入量を設定
      int indx = 0;
      for (int i = 0; i < nodes.Count; i++)
      {
        if (nodes[i].IsPressureFixed)
        {
          nodes[i].Inflow = inputs[indx];
          indx++;
        }
        else
        {
          nodes[i].Pressure = inputs[indx];
          indx++;
        }
      }

      //流路流量を更新
      for (int i = 0; i < branches.Count; i++) branches[i].UpdateFlowRateFromNodePressureDifference();

      //節点の質量保存誤差を計算 
      indx = 0;
      for (int i = 0; i < nodes.Count; i++)
        outputs[i] = nodes[i].IntegrateFlow();
    }

    /// <summary>Resets flow rate and pressure of all branches to zero.</summary>
    public void ShutOff()
    {
      for (int i = 0; i < branches.Count; i++)
        branches[i].VolumetricFlowRate = 0;
      for (int i = 0; i < nodes.Count; i++)
        nodes[i].Pressure = 0;
    }

    #endregion

  }

}