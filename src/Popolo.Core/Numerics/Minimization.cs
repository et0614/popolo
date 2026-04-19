/* Minimization.cs
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
    /// <exception cref="PopoloNumericalException">
    /// Thrown when convergence is not reached within the maximum number of iterations.
    /// </exception>
    public static double GoldenSection(ref double xMin, double xMax, MinimizeFunction mFnc)
    {
      const int MAX_ITER = 100;
      const double ERR_TOL = 0.0001;
      const double G_RATIO = 0.61803399;

      double a = xMin;
      double b = xMin + (xMax - xMin) * G_RATIO;
      double c = xMax;

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
          if (fx1 < fb || fa == fb)
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
          if (fx1 < fb || fb == fc)
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
