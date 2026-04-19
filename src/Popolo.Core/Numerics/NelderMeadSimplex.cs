/* NelderMeadSimplex.cs
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
  /// <summary>Downhill simplex method (Nelder-Mead) for multivariate optimization.</summary>
  public static class NelderMeadSimplex
  {

    #region 定数

    private const int MAX_ITERATION = 5000;
    private const double ERR_TOLERANCE = 1e-5;

    #endregion

    #region パラメータ

    private static double alpha = 1.0;
    private static double beta = 0.5;
    private static double gamma = 2.0;
    private static double rho = 1.0;
    private static double pow = 2.0;

    /// <summary>Gets or sets the reflection coefficient α (must be positive).</summary>
    public static double Alpha
    {
      get { return alpha; }
      set { if (0 < value) alpha = value; }
    }

    /// <summary>Gets or sets the contraction coefficient β (must be in (0, 1)).</summary>
    public static double Beta
    {
      get { return beta; }
      set { if (0 < value && value < 1) beta = value; }
    }

    /// <summary>Gets or sets the expansion coefficient γ (must be greater than 1).</summary>
    public static double Gamma
    {
      get { return gamma; }
      set { if (1 < value) gamma = value; }
    }

    /// <summary>Gets or sets the penalty parameter ρ (must be positive).</summary>
    public static double Rho
    {
      get { return rho; }
      set { if (0 < value) rho = value; }
    }

    /// <summary>Gets or sets the penalty exponent (must be positive).</summary>
    public static double Pow
    {
      get { return pow; }
      set { if (0 < value) pow = value; }
    }

    #endregion

    #region デリゲート定義

    /// <summary>Objective function to optimize.</summary>
    /// <param name="x">Input vector.</param>
    /// <returns>Objective value.</returns>
    public delegate double OptimizeFunction(double[] x);

    /// <summary>Internal objective function that also receives the current iteration count.</summary>
    private delegate double InternalOptimizeFunction(double[] x, int iteration);

    #endregion

    #region 公開メソッド

    /// <summary>Finds the input vector that minimizes the given function.</summary>
    /// <param name="mFnc">Function to minimize.</param>
    /// <param name="minX">Lower bounds of the search range.</param>
    /// <param name="maxX">Upper bounds of the search range.</param>
    /// <param name="success">Output: whether the search converged successfully.</param>
    /// <returns>Input vector at the minimum of the function.</returns>
    /// <exception cref="PopoloArgumentException">
    /// Thrown when <paramref name="minX"/> or <paramref name="maxX"/> is null or empty, or when their lengths differ.
    /// </exception>
    public static double[] GetSolution(
        OptimizeFunction mFnc, double[] minX, double[] maxX, out bool success)
    {
      ValidateSearchRange(minX, maxX);

      InternalOptimizeFunction fnc = (x, iteration) => mFnc(x);
      double[][] points = MakeInitialPoints(minX, maxX);
      return Solve(fnc, points, false, out success);
    }

    /// <summary>Finds the input vector that minimizes the given function subject to an equality constraint.</summary>
    /// <param name="mFnc">Function to minimize.</param>
    /// <param name="cFnc">Constraint function, enforced as f(x) = 0.</param>
    /// <param name="minX">Lower bounds of the search range.</param>
    /// <param name="maxX">Upper bounds of the search range.</param>
    /// <param name="success">Output: whether the search converged successfully.</param>
    /// <returns>Input vector at the constrained minimum of the function.</returns>
    /// <exception cref="PopoloArgumentException">
    /// Thrown when <paramref name="minX"/> or <paramref name="maxX"/> is null or empty, or when their lengths differ.
    /// </exception>
    public static double[] GetSolution(
        OptimizeFunction mFnc, OptimizeFunction cFnc,
        double[] minX, double[] maxX, out bool success)
    {
      ValidateSearchRange(minX, maxX);

      InternalOptimizeFunction fnc = (x, iteration) =>
          mFnc(x) + (Rho * (iteration + 5)) * Math.Pow(Math.Abs(cFnc(x)), pow);
      double[][] points = MakeInitialPoints(minX, maxX);
      return Solve(fnc, points, true, out success);
    }

    #endregion

    #region 非公開メソッド

    /// <summary>Validates the search-range arguments.</summary>
    private static void ValidateSearchRange(double[] minX, double[] maxX)
    {
      if (minX == null || minX.Length == 0)
        throw new PopoloArgumentException(
            "minX must not be null or empty.", nameof(minX));
      if (maxX == null || maxX.Length == 0)
        throw new PopoloArgumentException(
            "maxX must not be null or empty.", nameof(maxX));
      if (minX.Length != maxX.Length)
        throw new PopoloArgumentException(
            $"minX and maxX must have the same length. "
            + $"minX.Length={minX.Length}, maxX.Length={maxX.Length}.",
            nameof(maxX));
    }

    /// <summary>Solves the minimization problem using the downhill simplex method.</summary>
    private static double[] Solve(
        InternalOptimizeFunction fnc, double[][] points,
        bool hasConstraint, out bool success)
    {
      int num = points[0].Length;
      int iMin = 0;
      double[] newPt1 = new double[num];
      double[] newPt2 = new double[num];
      double[] ypi = new double[num + 1];
      double[] sum = new double[num];
      SummatePoints(points, ref sum);

      int iterNum = 0;
      success = true;
      while (true)
      {
        if (iterNum == 0 || hasConstraint)
          for (int i = 0; i <= num; i++) ypi[i] = fnc(points[i], iterNum);

        double ave = 0;
        int iMax, iSec;
        if (ypi[0] < ypi[1]) { iMax = 1; iSec = 0; }
        else { iMax = 0; iSec = 1; }
        for (int i = 0; i <= num; i++)
        {
          ave += ypi[i];
          if (ypi[i] < ypi[iMin]) iMin = i;
          else if (ypi[iMax] < ypi[i]) { iSec = iMax; iMax = i; }
          else if (ypi[iSec] < ypi[i] && i != iMax) iSec = i;
        }

        ave /= (num + 1);
        double err = 0;
        for (int i = 0; i <= num; i++) err += Math.Abs(ypi[i] - ave);
        if (err / (num + 1) < ERR_TOLERANCE) break;

        double yt1 = TryPoint(fnc, iterNum, points[iMax], sum, -Alpha, ref newPt1);
        if (yt1 < ypi[iMin])
        {
          double yt2 = TryPoint(fnc, iterNum, points[iMax], sum, Gamma, ref newPt2);
          if (yt2 < ypi[iMin])
          {
            SwitchPoint(ref points, ref sum, newPt2, iMax);
            ypi[iMax] = yt2;
          }
          else
          {
            SwitchPoint(ref points, ref sum, newPt1, iMax);
            ypi[iMax] = yt1;
          }
        }
        else
        {
          bool ltNxt = true;
          for (int i = 0; i <= num; i++)
          {
            if (i != iMax && yt1 <= ypi[i]) { ltNxt = false; break; }
          }
          if (ltNxt)
          {
            if (yt1 <= ypi[iMax])
            {
              SwitchPoint(ref points, ref sum, newPt1, iMax);
              ypi[iMax] = yt1;
            }
            yt1 = TryPoint(fnc, iterNum, points[iMax], sum, Beta, ref newPt1);
            if (ypi[iMax] < yt1)
            {
              for (int i = 0; i <= num; i++)
              {
                if (i != iMin)
                {
                  for (int j = 0; j < num; j++)
                    points[i][j] = 0.5 * (points[i][j] + points[iMin][j]);
                }
                if (!hasConstraint) ypi[i] = fnc(points[i], iterNum);
              }
              SummatePoints(points, ref sum);
            }
            else
            {
              SwitchPoint(ref points, ref sum, newPt1, iMax);
              ypi[iMax] = yt1;
            }
          }
          else
          {
            SwitchPoint(ref points, ref sum, newPt1, iMax);
            ypi[iMax] = yt1;
          }
        }

        iterNum++;
        if (MAX_ITERATION < iterNum) { success = false; break; }
      }
      return points[iMin];
    }

    /// <summary>Generates initial simplex vertices using random sampling within the search range.</summary>
    private static double[][] MakeInitialPoints(double[] minX, double[] maxX)
    {
      double[][] pnts = new double[minX.Length + 1][];
      for (int i = 0; i < pnts.Length; i++) pnts[i] = new double[minX.Length];

      // ミリ秒単位のシードで同一秒内の重複を低減
      var mt = new MersenneTwister((uint)DateTime.Now.Ticks);
      for (int i = 0; i < pnts.Length; i++)
        for (int j = 0; j < minX.Length; j++)
          pnts[i][j] = minX[j] + (maxX[j] - minX[j]) * mt.NextDouble();
      return pnts;
    }

    /// <summary>Replaces one simplex vertex and updates the coordinate sum.</summary>
    private static void SwitchPoint(
        ref double[][] points, ref double[] sum, double[] newPt, int iMax)
    {
      for (int i = 0; i < newPt.Length; i++)
      {
        sum[i] += newPt[i] - points[iMax][i];
        points[iMax][i] = newPt[i];
      }
    }

    /// <summary>Evaluates the objective at a new candidate point generated from the current simplex.</summary>
    private static double TryPoint(
        InternalOptimizeFunction fnc, int iteration,
        double[] pt, double[] sum, double cf, ref double[] newPt)
    {
      double cf1 = (1.0 - cf) / pt.Length;
      double cf2 = cf - cf1;
      for (int i = 0; i < pt.Length; i++) newPt[i] = sum[i] * cf1 + pt[i] * cf2;
      return fnc(newPt, iteration);
    }

    /// <summary>Computes the component-wise sum of all simplex vertices.</summary>
    private static void SummatePoints(double[][] points, ref double[] sum)
    {
      int num = points[0].Length;
      for (int i = 0; i < num; i++) sum[i] = 0;
      for (int i = 0; i < points.Length; i++)
        for (int j = 0; j < num; j++) sum[j] += points[i][j];
    }

    #endregion

  }
}
