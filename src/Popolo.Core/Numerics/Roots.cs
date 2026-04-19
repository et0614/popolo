/* Roots.cs
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
  /// <summary>Utility for finding the roots of a univariate nonlinear function.</summary>
  public static class Roots
  {

    /// <summary>Machine epsilon.</summary>
    private static readonly double MECH_EPS;

    /// <summary>Residual function.</summary>
    /// <param name="x">Input value.</param>
    /// <returns>Residual value.</returns>
    public delegate double ErrorFunction(double x);

    /// <summary>Static constructor.</summary>
    static Roots()
    {
      MECH_EPS = 1.0;
      while (true)
      {
        if (1.0 + MECH_EPS <= 1.0)
        {
          MECH_EPS *= 2;
          break;
        }
        else MECH_EPS = MECH_EPS * 0.5;
      }
    }

    /// <summary>Finds a root by bisection using precomputed values at the bracket endpoints.</summary>
    /// <param name="eFnc">Residual function.</param>
    /// <param name="a">First bracket endpoint.</param>
    /// <param name="b">Second bracket endpoint.</param>
    /// <param name="fa">Residual value at <paramref name="a"/>.</param>
    /// <param name="fb">Residual value at <paramref name="b"/>.</param>
    /// <param name="errTolerance">Tolerance on the residual.</param>
    /// <param name="collecTolerance">Tolerance on the interval width.</param>
    /// <param name="maxIter">Maximum number of iterations.</param>
    /// <returns>Root of the function.</returns>
    /// <exception cref="PopoloArgumentException">
    /// Thrown when <paramref name="fa"/> and <paramref name="fb"/> have the same sign (the root is not bracketed).
    /// </exception>
    /// <exception cref="PopoloNumericalException">
    /// Thrown when convergence is not reached within the maximum number of iterations.
    /// </exception>
    public static double Bisection(ErrorFunction eFnc, double a, double b,
        double fa, double fb, double errTolerance, double collecTolerance, int maxIter)
    {
      if (0 < fa * fb)
        throw new PopoloArgumentException(
            $"Initial points do not bracket a root. f(a)={fa}, f(b)={fb} must have opposite signs.",
            nameof(a));

      int iterNum = 0;
      while (true)
      {
        double c = 0.5 * (a + b);
        double fc = eFnc(c);
        if (Math.Sign(fc) == Math.Sign(fa))
        {
          fa = fc;
          a = c;
        }
        else
        {
          fb = fc;
          b = c;
        }
        if ((Math.Abs(fc) < errTolerance) || Math.Abs(a - b) < collecTolerance) return c;
        iterNum++;
        if (maxIter < iterNum)
          throw new PopoloNumericalException(
              "Bisection",
              $"Convergence failed after {iterNum} iterations. "
              + $"Current interval: [{a}, {b}].");
      }
    }

    /// <summary>Finds a root by bisection.</summary>
    /// <param name="eFnc">Residual function.</param>
    /// <param name="a">First bracket endpoint.</param>
    /// <param name="b">Second bracket endpoint.</param>
    /// <param name="errorTolerance">Tolerance on the residual.</param>
    /// <param name="collectionTolerance">Tolerance on the interval width.</param>
    /// <param name="maxIteration">Maximum number of iterations.</param>
    /// <returns>Root of the function.</returns>
    /// <exception cref="PopoloArgumentException">
    /// Thrown when <paramref name="a"/> and <paramref name="b"/> do not bracket a root.
    /// </exception>
    /// <exception cref="PopoloNumericalException">
    /// Thrown when convergence is not reached within the maximum number of iterations.
    /// </exception>
    public static double Bisection(ErrorFunction eFnc, double a, double b,
        double errorTolerance, double collectionTolerance, int maxIteration)
    {
      return Bisection(eFnc, a, b, eFnc(a), eFnc(b),
          errorTolerance, collectionTolerance, maxIteration);
    }

    /// <summary>Finds a root using Brent's method.</summary>
    /// <param name="a">First bracket endpoint.</param>
    /// <param name="b">Second bracket endpoint.</param>
    /// <param name="errorTolerance">Tolerance on the residual.</param>
    /// <param name="eFnc">Residual function.</param>
    /// <returns>Root of the function.</returns>
    /// <exception cref="PopoloNumericalException">
    /// Thrown when convergence is not reached within the maximum number of iterations.
    /// </exception>
    public static double Brent(double a, double b, double errorTolerance, ErrorFunction eFnc)
    {
      const int MAX_ITER = 100;

      double e = 0;
      double d = 0;
      double fa = eFnc(a);
      double fb = eFnc(b);
      double c = a;
      double fc = fa;

      int iterNum = 0;
      while (true)
      {
        if ((0 < fb && 0 < fc) || (fb < 0 && fc < 0))
        {
          c = a;
          fc = fa;
          e = d = b - a;
        }

        if (Math.Abs(fc) < Math.Abs(fa))
        {
          a = b; b = c; c = a;
          fa = fb; fb = fc; fc = fa;
        }

        double tol = 2.0 * MECH_EPS * Math.Abs(b) + errorTolerance;
        double mid = 0.5 * (c - b);
        if (Math.Abs(mid) < tol || fb == 0.0) return b;

        if ((Math.Abs(e) < tol) || Math.Abs(fa) <= Math.Abs(fb))
        {
          d = mid;
          e = d;
        }
        else
        {
          double p, q, r;
          double s = fb / fa;
          if (a == c)
          {
            p = 2.0 * mid * s;
            q = 1.0 - s;
          }
          else
          {
            q = fa / fc;
            r = fb / fc;
            p = s * (2.0 * mid * q * (q - r) - (b - a) * (r - 1.0));
            q = (q - 1.0) * (r - 1.0) * (s - 1.0);
          }
          if (0 < p) q = -q;
          p = Math.Abs(p);
          double min1 = 3.0 * mid * mid * q - Math.Abs(tol * q);
          double min2 = Math.Abs(e * q);
          if (2.0 * p < Math.Min(min1, min2))
          {
            e = d;
            d = p / q;
          }
          else
          {
            d = mid;
            e = d;
          }
        }
        a = b;
        fa = fb;
        if (tol < Math.Abs(d)) b += d;
        else b += Math.Sign(mid) * tol;
        fb = eFnc(b);

        iterNum++;
        if (MAX_ITER < iterNum)
          throw new PopoloNumericalException(
              "Brent",
              $"Convergence failed after {iterNum} iterations. "
              + $"Last estimate: b={b}, f(b)={fb}.");
      }
    }

    /// <summary>Finds a root using Newton's method with numerical differentiation.</summary>
    /// <param name="eFnc">Residual function.</param>
    /// <param name="x">Initial guess.</param>
    /// <param name="delta">Step size used for numerical differentiation.</param>
    /// <param name="errorTolerance">Tolerance on the residual.</param>
    /// <param name="collectionTolerance">Tolerance on the correction step.</param>
    /// <param name="maxIteration">Maximum number of iterations.</param>
    /// <returns>Root of the function.</returns>
    /// <exception cref="PopoloNumericalException">
    /// Thrown when convergence is not reached within the maximum number of iterations.
    /// </exception>
    public static double Newton(ErrorFunction eFnc, double x, double delta,
        double errorTolerance, double collectionTolerance, int maxIteration)
    {
      int iNum = 0;
      double err1 = eFnc(x);
      while (errorTolerance < Math.Abs(err1))
      {
        if (maxIteration < iNum)
          throw new PopoloNumericalException(
              "Newton",
              $"Convergence failed after {iNum} iterations. "
              + $"Last estimate: x={x}, f(x)={err1}.");
        double err2 = eFnc(x + delta);
        double dX = (err1 * delta) / (err2 - err1);
        x -= dX;
        if (Math.Abs(dX) < collectionTolerance) break;
        err1 = eFnc(x);
        iNum++;
      }
      return x;
    }

    /// <summary>Finds a root using Newton's method with an analytic derivative.</summary>
    /// <param name="eFnc">Residual function.</param>
    /// <param name="eFncD">Derivative of the residual function.</param>
    /// <param name="x">Initial guess.</param>
    /// <param name="errorTolerance">Tolerance on the residual.</param>
    /// <param name="collectionTolerance">Tolerance on the correction step.</param>
    /// <param name="maxIteration">Maximum number of iterations.</param>
    /// <returns>Root of the function.</returns>
    /// <exception cref="PopoloNumericalException">
    /// Thrown when convergence is not reached within the maximum number of iterations.
    /// </exception>
    public static double Newton(ErrorFunction eFnc, ErrorFunction eFncD,
        double x, double errorTolerance, double collectionTolerance, int maxIteration)
    {
      int iNum = 0;
      double err = eFnc(x);
      while (errorTolerance < Math.Abs(err))
      {
        if (maxIteration < iNum)
          throw new PopoloNumericalException(
              "Newton",
              $"Convergence failed after {iNum} iterations. "
              + $"Last estimate: x={x}, f(x)={err}.");
        double dX = err / eFncD(x);
        x -= dX;
        if (Math.Abs(dX) < collectionTolerance) break;
        err = eFnc(x);
        iNum++;
      }
      return x;
    }

    /// <summary>Finds a root by combining Newton's method with bisection fallback.</summary>
    /// <param name="eFnc">Residual function.</param>
    /// <param name="x">Initial guess.</param>
    /// <param name="delta">Step size used for numerical differentiation.</param>
    /// <param name="errorTolerance">Tolerance on the residual.</param>
    /// <param name="collectionTolerance">Tolerance on the correction step.</param>
    /// <param name="maxIteration">Maximum number of iterations.</param>
    /// <returns>Root of the function.</returns>
    /// <exception cref="PopoloNumericalException">
    /// Thrown when convergence is not reached within the maximum number of iterations.
    /// </exception>
    public static double NewtonBisection(ErrorFunction eFnc, double x, double delta,
        double errorTolerance, double collectionTolerance, int maxIteration)
    {
      int iNum = 0;
      double err1 = eFnc(x);
      while (errorTolerance < Math.Abs(err1))
      {
        if (maxIteration < iNum)
          throw new PopoloNumericalException(
              "NewtonBisection",
              $"Convergence failed after {iNum} iterations. "
              + $"Last estimate: x={x}, f(x)={err1}.");
        double err2 = eFnc(x + delta);
        double dX = (err1 * delta) / (err2 - err1);
        double lastX = x;
        double lastErr = err1;
        x -= dX;
        if (Math.Abs(dX) < collectionTolerance) break;
        err1 = eFnc(x);
        if (lastErr * err1 < 0)
          return Bisection(eFnc, lastX, x, lastErr, err1,
              errorTolerance, collectionTolerance, maxIteration - iNum);
        iNum++;
      }
      return x;
    }

  }
}
