/* GaussLegendreIntegrator.cs
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
  /// <summary>Gauss-Legendre numerical integrator.</summary>
  /// <remarks>
  /// The nodes are the roots of the Legendre polynomial P_n, located by Newton's method
  /// from Tricomi's asymptotic estimate; the weights follow from P_n' at each node.
  /// References:
  /// Abramowitz, M. and Stegun, I.A. (eds.), Handbook of Mathematical Functions,
  /// NBS Applied Mathematics Series 55, 1964, Eqs. 22.7.10, 22.8.5 and 25.4.29;
  /// Tricomi, F.G., Sugli zeri dei polinomi sferici ed ultrasferici,
  /// Annali di Matematica Pura ed Applicata 31, pp. 93-97, 1950.
  /// </remarks>
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
    /// <param name="x">Output: the non-negative half of the nodes on [-1, 1] in descending
    /// order (the negative nodes are their mirror images). For an odd
    /// <paramref name="number"/> the last element is the center node, exactly 0.</param>
    /// <param name="w">Output: quadrature weights corresponding to <paramref name="x"/>.</param>
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

      const int MAX_ITER = 100;
      const double TOLERANCE = 1e-15;

      // Tricomi's estimate of the k-th largest root:
      // x_k ≈ (1 − 1/(8n²) + 1/(8n³))·cos(π(4k − 1)/(4n + 2))
      double n = number;
      double scale = 1.0 - (1.0 - 1.0 / n) / (8.0 * n * n);

      int half = (number + 1) / 2;
      x = new double[half];
      w = new double[half];
      for (int k = 0; k < half; k++)
      {
        double root;
        if (number % 2 == 1 && k == half - 1) root = 0.0;   // center node of an odd rule
        else
        {
          root = scale * Math.Cos(Math.PI * (4 * k + 3) / (4.0 * n + 2.0));
          for (int iter = 0; iter < MAX_ITER; iter++)
          {
            EvaluateLegendre(number, root, out double pn, out double dpn);
            double step = pn / dpn;
            root -= step;
            if (Math.Abs(step) <= TOLERANCE) break;
          }
        }

        // Weight (A&S 25.4.29): w = 2 / ((1 − x²)·P_n'(x)²)
        EvaluateLegendre(number, root, out _, out double slope);
        x[k] = root;
        w[k] = 2.0 / ((1.0 - root * root) * slope * slope);
      }
    }

    /// <summary>Evaluates the Legendre polynomial P_n and its derivative at t (|t| &lt; 1).</summary>
    /// <param name="degree">Degree n (1 or more).</param>
    /// <param name="t">Evaluation point.</param>
    /// <param name="value">Output: P_n(t).</param>
    /// <param name="derivative">Output: P_n'(t).</param>
    private static void EvaluateLegendre(
        int degree, double t, out double value, out double derivative)
    {
      // Bonnet's recurrence (A&S 22.7.10): (k+1)·P_{k+1} = (2k+1)·t·P_k − k·P_{k−1}
      double lower = 1.0;   // P_0
      double upper = t;     // P_1
      for (int k = 1; k < degree; k++)
      {
        double next = ((2 * k + 1) * t * upper - k * lower) / (k + 1);
        lower = upper;
        upper = next;
      }
      value = upper;
      // A&S 22.8.5: (1 − t²)·P_n' = n·(P_{n−1} − t·P_n)
      derivative = degree * (lower - t * upper) / (1.0 - t * t);
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
