/* GaussLegendreIntegrator.cs
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
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
 * General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301, USA.
 */

using System;
using Popolo.Core.Exceptions;

namespace Popolo.Core.Numerics
{
  /// <summary>Gauss-Legendre numerical integrator.</summary>
  /// <remarks>Adapted from "Numerical Recipes".</remarks>
  [Serializable]
  public class GaussLegendreIntegrator
  {
    /// <summary>Integrand function.</summary>
    /// <param name="x">Input value.</param>
    /// <returns>Output value.</returns>
    public delegate double IntegrateFunction(double x);

    /// <summary>Quadrature nodes.</summary>
    private double[] x;

    /// <summary>Quadrature weights.</summary>
    private double[] w;

    /// <summary>Integrand function.</summary>
    private readonly IntegrateFunction iFnc;

    /// <summary>Initializes a new instance.</summary>
    /// <param name="iFnc">Integrand function.</param>
    /// <param name="nodeCount">Number of quadrature nodes (1 or more).</param>
    /// <exception cref="PopoloArgumentException">
    /// Thrown when <paramref name="nodeCount"/> is less than 1.
    /// </exception>
    public GaussLegendreIntegrator(IntegrateFunction iFnc, int nodeCount)
    {
      if (nodeCount < 1)
        throw new PopoloArgumentException(
            $"nodeCount must be at least 1. Got: {nodeCount}",
            nameof(nodeCount));

      this.iFnc = iFnc;
      ComputeNodesAndWeights(nodeCount, out x, out w);
    }

    /// <summary>Evaluates the definite integral over the interval [a, b].</summary>
    /// <param name="a">Lower bound.</param>
    /// <param name="b">Upper bound.</param>
    /// <returns>Value of the integral.</returns>
    public double Integrate(double a, double b)
    {
      return Integrate(iFnc, a, b, x, w);
    }

    /// <summary>Updates the number of quadrature nodes.</summary>
    /// <param name="nodeCount">Number of quadrature nodes (1 or more).</param>
    /// <exception cref="PopoloArgumentException">
    /// Thrown when <paramref name="nodeCount"/> is less than 1.
    /// </exception>
    public void UpdateNodeCount(int nodeCount)
    {
      if (nodeCount < 1)
        throw new PopoloArgumentException(
            $"nodeCount must be at least 1. Got: {nodeCount}",
            nameof(nodeCount));

      ComputeNodesAndWeights(nodeCount, out x, out w);
    }

    /// <summary>Computes the nodes and weights for Gauss-Legendre quadrature.</summary>
    /// <param name="number">Number of quadrature nodes (1 or more).</param>
    /// <param name="x">Output: quadrature nodes.</param>
    /// <param name="w">Output: quadrature weights.</param>
    /// <exception cref="PopoloArgumentException">
    /// Thrown when <paramref name="number"/> is less than 1.
    /// </exception>
    public static void ComputeNodesAndWeights(
        int number, out double[] x, out double[] w)
    {
      if (number < 1)
        throw new PopoloArgumentException(
            $"number must be at least 1. Got: {number}",
            nameof(number));

      int m = (number + 1) / 2;
      x = new double[m];
      w = new double[m];
      for (int i = 1; i <= m; i++)
      {
        double z = Math.Cos(Math.PI * (i - 0.25) / (number + 0.5));
        double pp = 0;
        while (true)
        {
          double p1 = 1.0;
          double p2 = 0.0;
          for (int j = 1; j <= number; j++)
          {
            double p3 = p2;
            p2 = p1;
            p1 = ((2.0 * j - 1.0) * z * p2 - (j - 1.0) * p3) / j;
          }
          pp = number * (z * p1 - p2) / (z * z - 1.0);
          double prevz = z;
          z = z - p1 / pp;
          if (Math.Abs(z - prevz) < 1e-10) break;
        }
        if (number % 2 == 1 && i == m) x[i - 1] = 0;
        else x[i - 1] = z;
        w[i - 1] = 2.0 / ((1.0 - z * z) * pp * pp);
      }
    }

    /// <summary>Evaluates the definite integral over [a, b] using the given nodes and weights.</summary>
    /// <param name="iFnc">Integrand function.</param>
    /// <param name="a">Lower bound.</param>
    /// <param name="b">Upper bound.</param>
    /// <param name="x">Quadrature nodes.</param>
    /// <param name="w">Quadrature weights.</param>
    /// <returns>Value of the integral.</returns>
    public static double Integrate(
        IntegrateFunction iFnc, double a, double b, double[] x, double[] w)
    {
      double xm = 0.5 * (a + b);
      double xl = 0.5 * (b - a);
      double sum = 0;
      int number = x.Length;
      if (x[number - 1] == 0.0)
      {
        number--;
        sum = w[number] * iFnc(xm);
      }
      for (int i = 0; i < number; i++)
      {
        double dx = xl * x[i];
        sum += w[i] * (iFnc(xm + dx) + iFnc(xm - dx));
      }
      return sum * xl;
    }
  }
}
