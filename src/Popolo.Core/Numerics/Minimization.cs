/* Minimization.cs
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
  /// <summary>Utility for minimizing a univariate nonlinear function.</summary>
  public static class Minimization
  {
    /// <summary>Function to minimize.</summary>
    /// <param name="x">Input value.</param>
    /// <returns>Output value.</returns>
    public delegate double MinimizeFunction(double x);

    /// <summary>Searches for a local minimum by the golden-section method.</summary>
    /// <param name="xMin">Input: lower bound of x. Output: x at the local minimum.</param>
    /// <param name="xMax">Upper bound of x.</param>
    /// <param name="mFnc">Function to minimize.</param>
    /// <returns>Value of the local minimum.</returns>
    /// <remarks>
    /// The bounds may be given in either order. When a probe value ties with the interior
    /// point, the bracket keeping the interior point is retained (strict comparison), so a
    /// minimum lying between two equal probe values is not discarded. Only when three
    /// successive values are equal (a flat region) does the search advance past the interior
    /// point, toward the edge of the flat region; callers such as
    /// AirHandlingUnit.OptimizeVAV rely on this edge-seeking behavior.
    /// </remarks>
    /// <exception cref="PopoloNumericalException">
    /// Thrown when convergence is not reached within the maximum number of iterations.
    /// </exception>
    public static double GoldenSection(ref double xMin, double xMax, MinimizeFunction mFnc)
    {
      const int MAX_ITER = 100;
      const double ERR_TOL = 0.0001;
      const double G_RATIO = 0.61803399;

      //Normalize reversed bounds (a <= c)
      double a = Math.Min(xMin, xMax);
      double c = Math.Max(xMin, xMax);
      double b = a + (c - a) * G_RATIO;

      double fa = mFnc(a);
      double fb = mFnc(b);
      double fc = mFnc(c);

      int iterNum = 0;
      while (true)
      {
        if (b - a < c - b)
        {
          double x1 = a + (c - a) * G_RATIO;
          double fx1 = mFnc(x1);
          if (fx1 < fb || (fa == fb && fx1 == fb))
          {
            a = b; fa = fb;
            b = x1; fb = fx1;
          }
          else
          {
            c = x1; fc = fx1;
          }
        }
        else
        {
          double x1 = c - (c - a) * G_RATIO;
          double fx1 = mFnc(x1);
          if (fx1 < fb || (fb == fc && fx1 == fb))
          {
            c = b; fc = fb;
            b = x1; fb = fx1;
          }
          else
          {
            a = x1; fa = fx1;
          }
        }

        if (Math.Abs(c - a) < ERR_TOL)
        {
          if (fa < fb && fa < fc) xMin = a;
          else if (fc < fb && fc < fa) xMin = c;
          else xMin = b;
          return Math.Min(Math.Min(fa, fb), fc);
        }

        iterNum++;
        if (MAX_ITER < iterNum)
          throw new PopoloNumericalException(
              "GoldenSection",
              $"Convergence failed after {iterNum} iterations. "
              + $"Current interval: [{a}, {c}], width={Math.Abs(c - a)}.");
      }
    }

    /// <summary>Searches for a local minimum by the golden-section method, optionally extending the search beyond the initial interval.</summary>
    /// <param name="x1">Input: first bound. Output: x at the local minimum.</param>
    /// <param name="x2">Second bound.</param>
    /// <param name="mFnc">Function to minimize.</param>
    /// <param name="searchOutside">Whether to extend the search interval beyond the given bounds.</param>
    /// <returns>Value of the local minimum.</returns>
    /// <exception cref="PopoloNumericalException">
    /// Thrown when convergence is not reached within the maximum number of iterations.
    /// </exception>
    public static double GoldenSection(
        ref double x1, double x2, MinimizeFunction mFnc, bool searchOutside)
    {
      const double G_RATIO = 1.61803399;

      double min = Math.Min(x1, x2);
      double max = Math.Max(x1, x2);
      x1 = min;
      x2 = max;
      double xmax = x2;
      if (searchOutside)
      {
        xmax = x2 + (x2 - x1) * G_RATIO;
        double fa = mFnc(x1);
        double fb = mFnc(x2);
        double fc = mFnc(xmax);
        while (fc < fb)
        {
          x2 = xmax;
          fb = fc;
          xmax = x2 + (x2 - x1) * G_RATIO;
          fc = mFnc(xmax);
        }
      }
      return GoldenSection(ref x1, xmax, mFnc);
    }
  }
}
