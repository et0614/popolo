/* ODESolver.cs
 *
 * Copyright (C) 2014 E.Togashi
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

using System;
using Popolo.Core.Exceptions;

namespace Popolo.Core.Numerics
{
  /// <summary>Ordinary differential equation (ODE) solvers.</summary>
  public static class ODESolver
  {

    #region Delegate definitions

    /// <summary>Scalar differential equation dy/dt = f(t, y).</summary>
    /// <param name="t">Current time.</param>
    /// <param name="yt">Current value of y.</param>
    /// <returns>Derivative dy/dt.</returns>
    public delegate double DifferentialEquation(double t, double yt);

    /// <summary>System of coupled differential equations.</summary>
    /// <param name="t">Current time.</param>
    /// <param name="yt">Current state vector.</param>
    /// <param name="dyt">Output derivative vector.</param>
    public delegate void DifferentialEquations(double t, double[] yt, ref double[] dyt);

    /// <summary>Early-termination predicate.</summary>
    /// <param name="t">Current time.</param>
    /// <param name="yt">Current value of y.</param>
    /// <returns>True to terminate integration early.</returns>
    public delegate bool TerminateProcess(double t, double yt);

    #endregion

    #region Static fields and constructors

    /// <summary>Coefficients for the Runge-Kutta-Fehlberg (RKF45) method.</summary>
    private static readonly double[][] RKF45;

    /// <summary>Static constructor.</summary>
    static ODESolver()
    {
      RKF45 = new double[6][];
      RKF45[0] = new double[] { 0 };
      RKF45[1] = new double[] { 1 / 4d, 1 / 4d };
      RKF45[2] = new double[] { 3 / 8d, 3 / 32d, 9 / 32d };
      RKF45[3] = new double[] { 12 / 13d, 1932 / 2197d, -7200 / 2197d, 7296 / 2197d };
      RKF45[4] = new double[] { 1, 439 / 216d, -8, 3680 / 513d, -845 / 4104d };
      RKF45[5] = new double[] { 0.5, -8 / 27d, 2, -3544 / 2565d, 1859 / 4104d, -11 / 40d };
    }

    #endregion

    #region Runge-Kutta methods

    /// <summary>Advances the scalar ODE by one step using the classical fourth-order Runge-Kutta method.</summary>
    /// <param name="dEqn">Differential equation.</param>
    /// <param name="dt">Time step.</param>
    /// <param name="t">Current time.</param>
    /// <param name="yt">Current value of y.</param>
    /// <returns>Value of y at time t + dt.</returns>
    public static double SolveRK4(
        DifferentialEquation dEqn, double dt, double t, double yt)
    {
      double dt2 = 0.5 * dt;
      double k1 = dt * dEqn(t, yt);
      double k2 = dt * dEqn(t + dt2, yt + 0.5 * k1);
      double k3 = dt * dEqn(t + dt2, yt + 0.5 * k2);
      double k4 = dt * dEqn(t + dt, yt + k3);
      return yt + (k1 + 2 * (k2 + k3) + k4) / 6.0;
    }

    /// <summary>Advances the ODE system by one step using the Runge-Kutta-Gill method.</summary>
    /// <param name="dEqn">Differential equation system.</param>
    /// <param name="dt">Time step.</param>
    /// <param name="t">Current time.</param>
    /// <param name="yt0">Current state vector.</param>
    /// <param name="yt1">Output: state vector at time t + dt.</param>
    /// <exception cref="PopoloArgumentException">
    /// Thrown when <paramref name="yt0"/> or <paramref name="yt1"/> is null or empty, or when their lengths differ.
    /// </exception>
    public static void SolveRKGill(
        DifferentialEquations dEqn, double dt, double t,
        double[] yt0, ref double[] yt1)
    {
      if (yt0 == null || yt0.Length == 0)
        throw new PopoloArgumentException(
            "yt0 must not be null or empty.", nameof(yt0));
      if (yt1 == null || yt1.Length == 0)
        throw new PopoloArgumentException(
            "yt1 must not be null or empty.", nameof(yt1));
      if (yt0.Length != yt1.Length)
        throw new PopoloArgumentException(
            $"yt0 and yt1 must have the same length. "
            + $"yt0.Length={yt0.Length}, yt1.Length={yt1.Length}.",
            nameof(yt1));

      double sq2 = Math.Sqrt(2);
      double dt2 = 0.5 * dt;
      double[] k1 = new double[yt0.Length];
      double[] k2 = new double[yt0.Length];
      double[] k3 = new double[yt0.Length];
      double[] k4 = new double[yt0.Length];

      dEqn(t, yt0, ref k1);
      for (int i = 0; i < yt0.Length; i++)
      {
        k1[i] *= dt;
        yt1[i] = yt0[i] + 0.5 * k1[i];
      }

      dEqn(t + dt2, yt1, ref k2);
      for (int i = 0; i < yt0.Length; i++)
      {
        k2[i] *= dt;
        yt1[i] = yt0[i] + 0.5 * ((sq2 - 1) * k1[i] + (2 - sq2) * k2[i]);
      }

      dEqn(t + dt2, yt1, ref k3);
      for (int i = 0; i < yt0.Length; i++)
      {
        k3[i] *= dt;
        yt1[i] = yt0[i] + 0.5 * (-sq2 * k2[i] + (2 + sq2) * k3[i]);
      }

      dEqn(t + dt, yt1, ref k4);
      for (int i = 0; i < yt0.Length; i++)
      {
        k4[i] *= dt;
        yt1[i] = yt0[i]
            + (k1[i] + (2 - sq2) * k2[i] + (2 + sq2) * k3[i] + k4[i]) / 6.0;
      }
    }

    /// <summary>Integrates the ODE using the adaptive Runge-Kutta-Fehlberg (RKF45) method.</summary>
    /// <param name="dEqn">Differential equation.</param>
    /// <param name="tFnc">Early-termination predicate.</param>
    /// <param name="dtMax">Maximum time step.</param>
    /// <param name="t">Start time.</param>
    /// <param name="tend">End time.</param>
    /// <param name="yt">Initial value.</param>
    /// <param name="errTol">Error tolerance.</param>
    /// <returns>Value of y at time <paramref name="tend"/> (or at the time the
    /// early-termination predicate returned true). When <paramref name="tend"/> equals
    /// <paramref name="t"/>, <paramref name="yt"/> is returned unchanged.</returns>
    /// <exception cref="PopoloArgumentException">
    /// Thrown when <paramref name="dtMax"/> or <paramref name="errTol"/> is not positive,
    /// or when <paramref name="tend"/> is earlier than <paramref name="t"/>.
    /// </exception>
    /// <exception cref="PopoloNumericalException">
    /// Thrown when the error estimate becomes non-finite (e.g., the derivative returns NaN
    /// or infinity), or when the step size shrinks below the resolvable minimum.
    /// </exception>
    /// <remarks>
    /// Reference: http://slpr.sakura.ne.jp/qp/runge-kutta-ex
    /// </remarks>
    public static double SolveRKF45(
        DifferentialEquation dEqn, TerminateProcess tFnc,
        double dtMax, double t, double tend, double yt, double errTol)
    {
      if (!(0 < dtMax))
        throw new PopoloArgumentException(
            $"dtMax must be positive. dtMax={dtMax}.", nameof(dtMax));
      if (!(0 < errTol))
        throw new PopoloArgumentException(
            $"errTol must be positive. errTol={errTol}.", nameof(errTol));
      if (double.IsNaN(t) || double.IsNaN(tend) || tend < t)
        throw new PopoloArgumentException(
            $"tend must not be earlier than t. t={t}, tend={tend}.", nameof(tend));
      if (tend == t) return yt;

      //Smallest step size considered resolvable; the end time is regarded as reached
      //once the remaining interval falls below it
      double dtMin = Math.Max(
          1e-12 * Math.Min(tend - t, dtMax), 1e-15 * Math.Abs(t));
      double dt = Math.Min(dtMax, tend - t);
      double[] k = new double[6];

      while (true)
      {
        for (int i = 0; i < k.Length; i++)
        {
          double sm = yt;
          for (int j = 1; j < RKF45[i].Length; j++)
            sm += RKF45[i][j] * k[j - 1];
          k[i] = dt * dEqn(t + dt * RKF45[i][0], sm);
        }

        double r = Math.Abs(
            1 / 360d * k[0]
            - 128 / 4275d * k[2]
            - 2197 / 75240d * k[3]
            + 1 / 50d * k[4]
            + 2 / 55d * k[5]) / dt;

        //A non-finite error estimate (NaN or infinite derivative) can never be accepted
        //and would otherwise loop forever
        if (!double.IsFinite(r))
          throw new PopoloNumericalException(
              "SolveRKF45",
              $"Non-finite error estimate at t={t}, dt={dt}, y={yt}. "
              + "The differential equation returned NaN or infinity.");

        if (r < errTol)
        {
          yt += 25 / 216d * k[0]
              + 1408 / 2565d * k[2]
              + 2197 / 4104d * k[3]
              - 1 / 5d * k[4];
          t += dt;
        }

        if (tend - t <= dtMin || tFnc(t, yt)) return yt;

        // Avoid division by zero when r=0 (delta would become Infinity and dt would be capped at 4*dt)
        double delta = r > 0
            ? Math.Pow(errTol / (2 * r), 0.25)
            : 4.0;

        if (delta <= 0.1) dt = 0.1 * dt;
        else if (4 <= delta) dt = 4 * dt;
        else dt = delta * dt;

        dt = Math.Min(dtMax, dt);
        if (tend < t + dt) dt = tend - t;
        if (dt < dtMin)
          throw new PopoloNumericalException(
              "SolveRKF45",
              $"Step size underflow at t={t}: dt={dt} is below the minimum {dtMin}. "
              + "The required accuracy cannot be attained.");
      }
    }

    #endregion

  }
}
