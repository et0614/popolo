/* OdeSolverDemo.cs
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

using System;

using Popolo.Core.Numerics;

namespace Popolo.Samples.Demos.Core
{
  /// <summary>
  /// Integrates Newton's law of cooling with <see cref="ODESolver"/> and prints
  /// the computed temperature trajectory alongside the analytical solution.
  /// </summary>
  /// <remarks>
  /// <para>
  /// Model: an object at initial temperature T0 cools exponentially toward
  /// the ambient temperature Ta with time constant τ:
  /// <code>dT/dt = -(T - Ta) / τ,  T(0) = T0.</code>
  /// Exact solution: <c>T(t) = Ta + (T0 - Ta) · exp(-t / τ)</c>.
  /// </para>
  /// <para>
  /// Exercises the fixed-step <see cref="ODESolver.SolveRK4"/> by stepping
  /// 1 minute at a time and comparing to the closed-form solution every 5
  /// minutes for the first hour.
  /// </para>
  /// </remarks>
  public sealed class OdeSolverDemo : IDemo
  {
    public string Name => "numerics-ode";
    public string Category => "Core";
    public string Description => "Fixed-step RK4 integration of Newton's law of cooling.";

    public int Run(string[] args)
    {
      const double T0 = 90.0;    // initial temperature [°C] (cup of coffee)
      const double Ta = 22.0;    // ambient temperature [°C]
      const double tau = 900.0;  // time constant [s] (15 min)
      const double dt = 60.0;    // step [s] — 1 minute

      ODESolver.DifferentialEquation dTdt = (t, T) => -(T - Ta) / tau;

      Console.WriteLine($"Newton's law of cooling: T0 = {T0} °C, Ta = {Ta} °C, τ = {tau / 60} min");
      Console.WriteLine();
      Console.WriteLine("   t [min]   RK4 T [°C]   Exact T [°C]   |Δ|");
      Console.WriteLine("  --------  -----------  -------------  -------");

      double T = T0;
      double t = 0.0;
      PrintRow(0, T, AnalyticalT(T0, Ta, tau, 0));

      for (int minute = 1; minute <= 60; minute++)
      {
        T = ODESolver.SolveRK4(dTdt, dt, t, T);
        t += dt;
        if (minute % 5 == 0)
          PrintRow(minute, T, AnalyticalT(T0, Ta, tau, t));
      }

      return 0;
    }

    private static double AnalyticalT(double T0, double Ta, double tau, double t)
      => Ta + (T0 - Ta) * Math.Exp(-t / tau);

    private static void PrintRow(int minute, double numeric, double analytical)
    {
      Console.WriteLine(
        $"   {minute,6}   {numeric,10:F3}   {analytical,12:F3}   {Math.Abs(numeric - analytical),6:F4}");
    }
  }
}
