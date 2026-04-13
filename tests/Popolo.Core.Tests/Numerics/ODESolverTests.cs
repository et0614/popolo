/* ODESolverTests.cs
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
using Xunit;
using Popolo.Core.Numerics;
using Popolo.Core.Exceptions;

namespace Popolo.Core.Tests.Numerics
{
  /// <summary>ODESolver のテスト</summary>
  public class ODESolverTests
  {

    #region SolveRK4 のテスト

    /// <summary>dy/dt = y の解析解 y = exp(t) と一致する（小ステップ）</summary>
    [Fact]
    public void SolveRK4_ExponentialGrowth_MatchesAnalyticalSolution()
    {
      // dy/dt = y → y(t) = exp(t)、y(0)=1
      ODESolver.DifferentialEquation dEqn = (t, yt) => yt;
      double dt = 0.01;
      double y = 1.0;
      double t = 0.0;

      for (int i = 0; i < 100; i++)
      {
        y = ODESolver.SolveRK4(dEqn, dt, t, y);
        t += dt;
      }
      // t=1.0 での解析解 e^1 ≈ 2.71828
      Assert.Equal(Math.E, y, precision: 5);
    }

    /// <summary>dy/dt = -y の解析解 y = exp(-t) と一致する</summary>
    [Fact]
    public void SolveRK4_ExponentialDecay_MatchesAnalyticalSolution()
    {
      // dy/dt = -y → y(t) = exp(-t)、y(0)=1
      ODESolver.DifferentialEquation dEqn = (t, yt) => -yt;
      double dt = 0.01;
      double y = 1.0;
      double t = 0.0;

      for (int i = 0; i < 100; i++)
      {
        y = ODESolver.SolveRK4(dEqn, dt, t, y);
        t += dt;
      }
      // t=1.0 での解析解 e^-1 ≈ 0.36788
      Assert.Equal(Math.Exp(-1.0), y, precision: 5);
    }

    /// <summary>dy/dt = cos(t) の解析解 y = sin(t) と一致する</summary>
    [Fact]
    public void SolveRK4_SinFunction_MatchesAnalyticalSolution()
    {
      // dy/dt = cos(t) → y(t) = sin(t)、y(0)=0
      ODESolver.DifferentialEquation dEqn = (t, yt) => Math.Cos(t);
      double dt = 0.001;
      double y = 0.0;
      double t = 0.0;

      int steps = (int)(Math.PI / 2 / dt);
      for (int i = 0; i < steps; i++)
      {
        y = ODESolver.SolveRK4(dEqn, dt, t, y);
        t += dt;
      }
      // t=π/2 での解析解 sin(π/2) = 1
      Assert.Equal(1.0, y, precision: 6);
    }

    #endregion

    #region SolveRKGill のテスト

    /// <summary>既存テストと同じ連立微分方程式の解が解析解と一致する</summary>
    [Fact]
    public void SolveRKGill_CoupledODE_MatchesAnalyticalSolution()
    {
      // dy0/dt = y0 - 2*y1
      // dy1/dt = y0 + 4*y1
      // 解析解: y0 = -2*exp(2t) + 3*exp(3t), y1 = exp(2t) - 3*exp(3t)
      // 初期値: y0(0)=1, y1(0)=-2
      ODESolver.DifferentialEquations dEqn = (t, yt, ref dyt) =>
      {
        dyt[0] = yt[0] - 2 * yt[1];
        dyt[1] = yt[0] + 4 * yt[1];
      };

      double dt = 0.01;
      double[] yt0 = { 1.0, -2.0 };
      double[] yt1 = new double[2];

      double t = 0.0;
      for (int i = 0; i < 50; i++)
      {
        ODESolver.SolveRKGill(dEqn, dt, t, yt0, ref yt1);
        Array.Copy(yt1, yt0, 2);
        t += dt;
      }
      // t=0.5 での解析解
      double expected0 = -2 * Math.Exp(2 * 0.5) + 3 * Math.Exp(3 * 0.5);
      double expected1 = Math.Exp(2 * 0.5) - 3 * Math.Exp(3 * 0.5);

      Assert.Equal(expected0, yt0[0], precision: 5);
      Assert.Equal(expected1, yt0[1], precision: 5);
    }

    /// <summary>yt0 が null のとき PopoloArgumentException が発生する</summary>
    [Fact]
    public void SolveRKGill_NullYt0_ThrowsPopoloArgumentException()
    {
      ODESolver.DifferentialEquations dEqn = (t, yt, ref dyt) => { };
      double[] yt1 = new double[2];

      var ex = Assert.Throws<PopoloArgumentException>(
          () => ODESolver.SolveRKGill(dEqn, 0.1, 0.0, null!, ref yt1));
      Assert.Equal("yt0", ex.ParamName);
    }

    /// <summary>yt0 と yt1 の長さが異なるとき PopoloArgumentException が発生する</summary>
    [Fact]
    public void SolveRKGill_MismatchedLength_ThrowsPopoloArgumentException()
    {
      ODESolver.DifferentialEquations dEqn = (t, yt, ref dyt) => { };
      double[] yt0 = new double[2];
      double[] yt1 = new double[3];

      var ex = Assert.Throws<PopoloArgumentException>(
          () => ODESolver.SolveRKGill(dEqn, 0.1, 0.0, yt0, ref yt1));
      Assert.Equal("yt1", ex.ParamName);
    }

    #endregion

    #region SolveRKF45 のテスト

    /// <summary>dy/dt = y の解析解 y=exp(t) と一致する</summary>
    [Fact]
    public void SolveRKF45_ExponentialGrowth_MatchesAnalyticalSolution()
    {
      ODESolver.DifferentialEquation dEqn = (t, yt) => yt;
      ODESolver.TerminateProcess tFnc = (t, yt) => false;

      double result = ODESolver.SolveRKF45(
          dEqn, tFnc, dtMax: 0.1, t: 0.0, tend: 1.0, yt: 1.0, errTol: 1e-6);

      Assert.Equal(Math.E, result, precision: 5);
    }

    /// <summary>強制終了関数が機能する</summary>
    [Fact]
    public void SolveRKF45_TerminateProcess_StopsEarly()
    {
      ODESolver.DifferentialEquation dEqn = (t, yt) => yt;
      // y > 2 になったら終了
      ODESolver.TerminateProcess tFnc = (t, yt) => yt > 2.0;

      double result = ODESolver.SolveRKF45(
          dEqn, tFnc, dtMax: 0.1, t: 0.0, tend: 10.0, yt: 1.0, errTol: 1e-6);

      // y=exp(t) > 2 となる t = ln(2) ≈ 0.693 付近で停止
      // 停止後の値は 2 より少し大きい
      Assert.True(result >= 2.0,
          $"Expected result >= 2.0, but got {result}");
    }

    #endregion

  }
}
