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
  /// <summary>1変数非線形関数の最小化処理クラス</summary>
  public static class Minimization
  {
    /// <summary>最小化する関数</summary>
    /// <param name="x">入力値</param>
    /// <returns>出力値</returns>
    public delegate double MinimizeFunction(double x);

    /// <summary>黄金分割法で極小値を探索する</summary>
    /// <param name="xMin">入力：xの最小値　出力：極小値をとるx</param>
    /// <param name="xMax">xの最大値</param>
    /// <param name="mFnc">最小化する関数</param>
    /// <returns>極小値</returns>
    /// <exception cref="PopoloNumericalException">
    /// 最大反復回数内に収束しない場合。
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

    /// <summary>黄金分割法で極小値を探索する（範囲外探索オプション付き）</summary>
    /// <param name="x1">入力：探索境界1　出力：極小値をとるx</param>
    /// <param name="x2">探索境界2</param>
    /// <param name="mFnc">最小化する関数</param>
    /// <param name="searchOutside">境界範囲外に探索範囲を広げるか否か</param>
    /// <returns>極小値</returns>
    /// <exception cref="PopoloNumericalException">
    /// 最大反復回数内に収束しない場合。
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
