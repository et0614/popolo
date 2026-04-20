/* RegressionDemo.cs
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

using Popolo.Core.Numerics;
using Popolo.Core.Numerics.LinearAlgebra;

namespace Popolo.Samples.Demos.Core
{
  /// <summary>
  /// Fits a simple line and a two-variable linear model against noisy synthetic
  /// data using <see cref="LinearAlgebraOperations.FitAxPlusB"/> and
  /// <see cref="LinearAlgebraOperations.LeastSquareFit(double[],double[,])"/>.
  /// </summary>
  /// <remarks>
  /// <para>
  /// Generates synthetic observations from a known linear model plus Gaussian
  /// noise, then recovers the coefficients. Printing the recovered vs. true
  /// coefficients side-by-side makes the fit quality obvious.
  /// </para>
  /// </remarks>
  public sealed class RegressionDemo : IDemo
  {
    public string Name => "numerics-regression";
    public string Category => "Core";
    public string Description => "Least-squares regression on synthetic data.";

    public int Run(string[] args)
    {
      // ----- Part 1: simple linear fit y = a·x + b -----
      Console.WriteLine("Part 1 — simple line fit y = a·x + b");
      const double trueA = 2.5;
      const double trueB = -1.0;
      const int N = 50;

      var rng = new NormalRandom(seed: 42u, mean: 0.0, standardDeviation: 0.15);
      double[] xs1 = new double[N];
      double[] ys1 = new double[N];
      for (int i = 0; i < N; i++)
      {
        xs1[i] = i * 0.2;
        ys1[i] = trueA * xs1[i] + trueB + rng.NextDouble();
      }

      LinearAlgebraOperations.FitAxPlusB(xs1, ys1, out double a, out double b);
      Console.WriteLine($"  True        : a = {trueA:F4}, b = {trueB:F4}");
      Console.WriteLine($"  Recovered   : a = {a:F4}, b = {b:F4}");
      Console.WriteLine($"  Samples: {N}, noise σ = 0.15");

      // ----- Part 2: multi-variable fit y = c0 + c1·x1 + c2·x2 -----
      Console.WriteLine();
      Console.WriteLine("Part 2 — two-feature linear regression y = c0 + c1·x1 + c2·x2");
      double[] trueC = { 3.0, 0.8, -1.3 };
      double[] y2 = new double[N];
      double[,] X2 = new double[N, 3];   // columns: 1 (intercept), x1, x2

      var rng2 = new NormalRandom(seed: 7u, mean: 0.0, standardDeviation: 0.25);
      for (int i = 0; i < N; i++)
      {
        double x1 = i * 0.1;
        double x2 = Math.Sin(i * 0.3);
        X2[i, 0] = 1.0;
        X2[i, 1] = x1;
        X2[i, 2] = x2;
        y2[i] = trueC[0] + trueC[1] * x1 + trueC[2] * x2 + rng2.NextDouble();
      }

      double[] coef = LinearAlgebraOperations.LeastSquareFit(y2, X2, out double sigma2, out double aic);
      Console.WriteLine($"  True         : c0 = {trueC[0]:F3}, c1 = {trueC[1]:F3}, c2 = {trueC[2]:F3}");
      Console.WriteLine($"  Recovered    : c0 = {coef[0]:F3}, c1 = {coef[1]:F3}, c2 = {coef[2]:F3}");
      Console.WriteLine($"  Residual σ²  : {sigma2:F5}");
      Console.WriteLine($"  AIC          : {aic:F2}");
      Console.WriteLine($"  Samples: {N}, noise σ = 0.25");

      return 0;
    }
  }
}
