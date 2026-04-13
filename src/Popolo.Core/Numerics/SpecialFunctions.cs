/* SpecialFunctions.cs
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
  /// <summary>特殊関数を扱うクラス</summary>
  /// <remarks>ニューメリカルレシピ</remarks>
  public static class SpecialFunctions
  {

    #region 不完全ガンマ関数

    /// <summary>不完全ガンマ関数P(a,x)を計算する</summary>
    /// <param name="a">形状パラメータ（正の値）</param>
    /// <param name="x">積分上限（0以上）</param>
    /// <returns>不完全ガンマ関数P(a,x)の値</returns>
    /// <exception cref="PopoloArgumentException">
    /// a が0以下、またはx が負の場合。
    /// </exception>
    /// <exception cref="PopoloNumericalException">
    /// 反復計算が収束しない場合。
    /// </exception>
    public static double GammaP(double a, double x)
    {
      if (a <= 0.0)
        throw new PopoloArgumentException(
            $"a must be positive. Got: {a}",
            nameof(a));
      if (x < 0.0)
        throw new PopoloArgumentException(
            $"x must be non-negative. Got: {x}",
            nameof(x));

      if (x < (a + 1.0))
        return gser(a, x);
      else
        return 1.0 - gcf(a, x);
    }

    /// <summary>不完全ガンマ関数Q(a,x)を計算する</summary>
    /// <param name="a">形状パラメータ（正の値）</param>
    /// <param name="x">積分上限（0以上）</param>
    /// <returns>不完全ガンマ関数Q(a,x)の値</returns>
    /// <exception cref="PopoloArgumentException">
    /// a が0以下、またはx が負の場合。
    /// </exception>
    /// <exception cref="PopoloNumericalException">
    /// 反復計算が収束しない場合。
    /// </exception>
    public static double GammaQ(double a, double x)
    {
      if (a <= 0.0)
        throw new PopoloArgumentException(
            $"a must be positive. Got: {a}",
            nameof(a));
      if (x < 0.0)
        throw new PopoloArgumentException(
            $"x must be non-negative. Got: {x}",
            nameof(x));

      if (x < (a + 1.0))
        return 1.0 - gser(a, x);
      else
        return gcf(a, x);
    }

    /// <summary>相補誤差関数erfc()を計算する</summary>
    /// <param name="x">入力値</param>
    /// <returns>相補誤差関数の値</returns>
    public static double ComplementaryErrorFunction(double x)
    {
      return x < 0.0 ? 1.0 + GammaP(0.5, x * x) : GammaQ(0.5, x * x);
    }

    private static double gammln(double xx)
    {
      double[] cof = new double[]
      {
                76.18009172947146,
                -86.50532032941677,
                24.01409824083091,
                -1.231739572450155,
                0.1208650973866179e-2,
                -0.5395239384953e-5
      };

      double y, x;
      y = x = xx;
      double tmp = x + 5.5;
      tmp -= (x + 0.5) * Math.Log(tmp);
      double ser = 1.000000000190015;
      for (int j = 0; j <= 5; j++) ser += cof[j] / ++y;
      return -tmp + Math.Log(2.5066282746310005 * ser / x);
    }

    private static double gser(double a, double x)
    {
      const int ITMAX = 100;
      const double EPS = 3.0e-7;

      if (x <= 0.0) return 0.0;

      double ap = a;
      double del = 1.0 / a;
      double sum = del;
      for (int n = 1; n <= ITMAX; n++)
      {
        ++ap;
        del *= x / ap;
        sum += del;
        if (Math.Abs(del) < Math.Abs(sum) * EPS)
          return sum * Math.Exp(-x + a * Math.Log(x) - gammln(a));
      }
      throw new PopoloNumericalException(
          "gser",
          $"Convergence failed. a={a}, x={x}. Try reducing a or increasing ITMAX.");
    }

    private static double gcf(double a, double x)
    {
      const int ITMAX = 100;
      const double EPS = 3.0e-7;

      double a0 = 1.0;
      double a1 = x;
      double b0 = 0.0;
      double b1 = 1.0;
      double fac = 1.0;
      double gold = 0.0;
      for (int n = 1; n <= ITMAX; n++)
      {
        double an = n;
        double ana = an - a;
        a0 = (a1 + a0 * ana) * fac;
        b0 = (b1 + b0 * ana) * fac;
        double anf = an * fac;
        a1 = x * a0 + anf * a1;
        b1 = x * b0 + anf * b1;
        if (a1 != 0)
        {
          fac = 1.0 / a1;
          double g = b1 * fac;
          if (Math.Abs((g - gold) / g) < EPS)
            return Math.Exp(-x + a * Math.Log(x) - gammln(a)) * g;
          gold = g;
        }
      }
      throw new PopoloNumericalException(
          "gcf",
          $"Convergence failed. a={a}, x={x}. Try reducing a or increasing ITMAX.");
    }

    #endregion

  }
}
