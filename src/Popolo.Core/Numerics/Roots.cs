/* Roots.cs
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
    /// <param name="errorTolerance">Absolute tolerance on the root location (the search stops
    /// when the bracket half-width falls below errorTolerance + 2ε|x|).</param>
    /// <param name="eFnc">Residual function.</param>
    /// <returns>Root of the function.</returns>
    /// <exception cref="PopoloArgumentException">
    /// Thrown when f(a) and f(b) have the same sign (the root is not bracketed).
    /// </exception>
    /// <exception cref="PopoloNumericalException">
    /// Thrown when convergence is not reached within the maximum number of iterations.
    /// </exception>
    public static double Brent(double a, double b, double errorTolerance, ErrorFunction eFnc)
    {
      return Brent(eFnc, a, b, eFnc(a), eFnc(b), errorTolerance);
    }

    /// <summary>Finds a root using Brent's method with precomputed values at the bracket endpoints.</summary>
    /// <param name="eFnc">Residual function.</param>
    /// <param name="a">First bracket endpoint.</param>
    /// <param name="b">Second bracket endpoint.</param>
    /// <param name="fa">Residual value at <paramref name="a"/>.</param>
    /// <param name="fb">Residual value at <paramref name="b"/>.</param>
    /// <param name="errorTolerance">Absolute tolerance on the root location (the search stops
    /// when the bracket half-width falls below errorTolerance + 2ε|x|).</param>
    /// <returns>Root of the function.</returns>
    /// <exception cref="PopoloArgumentException">
    /// Thrown when <paramref name="fa"/> and <paramref name="fb"/> have the same sign (the root is not bracketed).
    /// </exception>
    /// <exception cref="PopoloNumericalException">
    /// Thrown when convergence is not reached within the maximum number of iterations.
    /// </exception>
    public static double Brent(ErrorFunction eFnc, double a, double b,
        double fa, double fb, double errorTolerance)
    {
      //With the stall safeguard in BrentQ the bracket at least halves every 3 iterations,
      //so 200 iterations cover a width-to-tolerance ratio of up to 2^66.
      const int MAX_ITER = 200;

      if (fa == 0.0) return a;
      if (fb == 0.0) return b;
      if ((fa < 0.0) == (fb < 0.0))
        throw new PopoloArgumentException(
            $"Initial points do not bracket a root. f(a)={fa} at a={a}, f(b)={fb} at b={b} "
            + "must have opposite signs.",
            nameof(a));

      return BrentQ(eFnc, a, fa, b, fb, 2.0 * errorTolerance, 4.0 * MECH_EPS, MAX_ITER);
    }

    /// <summary>Brent's method on a bracketing interval [xa, xb] with known end values.</summary>
    /// <remarks>
    /// C# port of brentq() in SciPy (scipy/optimize/Zeros/brentq.c, written by Charles Harris).
    /// Copyright (c) 2001-2002 Enthought, Inc. 2003, SciPy Developers. All rights reserved.
    /// Distributed under the BSD 3-Clause License; the full license text is reproduced in
    /// THIRD-PARTY-NOTICES.md at the repository root. Changes from the original: the end
    /// values are passed in rather than evaluated, non-convergence throws, and a stall
    /// safeguard forces bisection when the bracket has not halved during the last
    /// STALL_LIMIT iterations (the original can creep toward a multiple root from one side
    /// with slowly shrinking interpolation steps while the bracket stays wide).
    /// </remarks>
    private static double BrentQ(ErrorFunction f,
        double xa, double fa, double xb, double fb, double xtol, double rtol, int maxIter)
    {
      const int STALL_LIMIT = 2;

      double xpre = xa, xcur = xb, fpre = fa, fcur = fb;
      double xblk = 0.0, fblk = 0.0, spre = 0.0, scur = 0.0;
      if (fpre == 0.0) return xpre;
      if (fcur == 0.0) return xcur;

      double refWidth = double.PositiveInfinity;   //bracket width at the last halving
      int stalled = 0;                             //iterations since the last halving
      for (int i = 0; i < maxIter; i++)
      {
        if (fpre != 0.0 && fcur != 0.0 && (fpre < 0.0) != (fcur < 0.0))
        {
          xblk = xpre;
          fblk = fpre;
          spre = scur = xcur - xpre;
        }
        if (Math.Abs(fblk) < Math.Abs(fcur))
        {
          xpre = xcur;
          xcur = xblk;
          xblk = xpre;

          fpre = fcur;
          fcur = fblk;
          fblk = fpre;
        }

        //The tolerance is 2*delta
        double delta = (xtol + rtol * Math.Abs(xcur)) / 2.0;
        double sbis = (xblk - xcur) / 2.0;
        if (fcur == 0.0 || Math.Abs(sbis) < delta) return xcur;

        //Stall safeguard (not in the original)
        double width = 2.0 * Math.Abs(sbis);
        if (width <= 0.5 * refWidth) { refWidth = width; stalled = 0; }
        else stalled++;

        if (stalled < STALL_LIMIT && Math.Abs(spre) > delta && Math.Abs(fcur) < Math.Abs(fpre))
        {
          double stry;
          if (xpre == xblk)
          {
            //Interpolate
            stry = -fcur * (xcur - xpre) / (fcur - fpre);
          }
          else
          {
            //Extrapolate
            double dpre = (fpre - fcur) / (xpre - xcur);
            double dblk = (fblk - fcur) / (xblk - xcur);
            stry = -fcur * (fblk * dblk - fpre * dpre)
                / (dblk * dpre * (fblk - fpre));
          }
          if (2.0 * Math.Abs(stry) < Math.Min(Math.Abs(spre), 3.0 * Math.Abs(sbis) - delta))
          {
            //Good short step
            spre = scur;
            scur = stry;
          }
          else
          {
            //Bisect
            spre = sbis;
            scur = sbis;
          }
        }
        else
        {
          //Bisect
          spre = sbis;
          scur = sbis;
        }

        xpre = xcur; fpre = fcur;
        if (Math.Abs(scur) > delta) xcur += scur;
        else xcur += (sbis > 0.0 ? delta : -delta);

        fcur = f(xcur);
      }
      throw new PopoloNumericalException(
          "Brent",
          $"Convergence failed after {maxIter} iterations. "
          + $"Last estimate: x={xcur}, f(x)={fcur}.");
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

    /// <summary>
    /// Finds a root using Newton's method with an analytic derivative and
    /// a bisection fallback when consecutive Newton steps straddle the root.
    /// </summary>
    /// <remarks>
    /// Equivalent to <see cref="Newton(ErrorFunction, ErrorFunction, double, double, double, int)"/>
    /// in the convergent regime, but falls back to
    /// <see cref="Bisection(ErrorFunction, double, double, double, double, double, double, int)"/>
    /// as soon as two consecutive iterates produce residuals of opposite sign — i.e.,
    /// once a root is bracketed. Useful for problems where the analytic derivative is
    /// trustworthy near the root but the function has stiff regions (e.g.,
    /// PVT polynomials in the quasi-2-phase loop) where pure Newton can overshoot.
    /// </remarks>
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
    public static double NewtonBisection(ErrorFunction eFnc, ErrorFunction eFncD,
        double x, double errorTolerance, double collectionTolerance, int maxIteration)
    {
      int iNum = 0;
      double err = eFnc(x);
      while (errorTolerance < Math.Abs(err))
      {
        if (maxIteration < iNum)
          throw new PopoloNumericalException(
              "NewtonBisection (analytical)",
              $"Convergence failed after {iNum} iterations. "
              + $"Last estimate: x={x}, f(x)={err}.");
        double dfx = eFncD(x);
        if (dfx == 0.0)
          throw new PopoloNumericalException(
              "NewtonBisection (analytical)",
              $"Zero derivative at iteration {iNum}, x={x}.");
        double dX = err / dfx;
        double lastX = x;
        double lastErr = err;
        x -= dX;
        if (Math.Abs(dX) < collectionTolerance) break;
        err = eFnc(x);
        if (lastErr * err < 0)
          return Bisection(eFnc, lastX, x, lastErr, err,
              errorTolerance, collectionTolerance, maxIteration - iNum);
        iNum++;
      }
      return x;
    }

  }
}
