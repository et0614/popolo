/* CubicSpline.cs
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
using Popolo.Core.Numerics.LinearAlgebra;

namespace Popolo.Core.Numerics
{
  /// <summary>Cubic spline interpolation utilities.</summary>
  public static class CubicSpline
  {
    /// <summary>Computes the spline coefficients for cubic spline interpolation.</summary>
    /// <param name="x">Array of X coordinates (ascending order).</param>
    /// <param name="y">Array of Y coordinates.</param>
    /// <returns>Array of cubic spline coefficients.</returns>
    /// <exception cref="PopoloArgumentException">
    /// Thrown when x or y is null, contains fewer than two elements, or when their lengths differ.
    /// </exception>
    public static double[] GetParameters(double[] x, double[] y)
    {
      if (x == null || x.Length < 3)
        throw new PopoloArgumentException(
            "x must have at least 3 elements.", nameof(x));
      if (y == null || y.Length < 3)
        throw new PopoloArgumentException(
            "y must have at least 3 elements.", nameof(y));
      if (x.Length != y.Length)
        throw new PopoloArgumentException(
            $"x and y must have the same length. x.Length={x.Length}, y.Length={y.Length}.",
            nameof(y));

      IVector h = new Vector(y.Length - 1);
      IVector a = new Vector(y.Length - 1);
      IMatrix hm = new Matrix(3, y.Length - 2);
      for (int i = 0; i < y.Length - 1; i++) h[i] = x[i + 1] - x[i];
      for (int i = 0; i < y.Length - 2; i++)
      {
        if (i != 0) hm[0, i] = h[i];
        hm[1, i] = 2 * (h[i] + h[i + 1]);
        if (i != y.Length - 3) hm[2, i] = h[i + 1];
        a[i] = 3 * ((y[i + 2] - y[i + 1]) / h[i + 1] - (y[i + 1] - y[i]) / h[i]);
      }
      LinearAlgebraOperations.SolveTridiagonalMatrix(hm, a);
      double[] c = new double[y.Length];
      for (int i = 1; i < c.Length; i++) c[i] = a[i - 1];
      c[0] = c[c.Length - 1] = 0;
      return c;
    }

    /// <summary>Interpolates at multiple evaluation points.</summary>
    /// <param name="x">Array of X coordinates (ascending order).</param>
    /// <param name="y">Array of Y coordinates.</param>
    /// <param name="c">Coefficient array (return value of <see cref="GetParameters"/>).</param>
    /// <param name="x2">Array of evaluation positions (expected in ascending order).</param>
    /// <returns>Array of interpolated values.</returns>
    /// <exception cref="PopoloArgumentException">
    /// Thrown when any value in <paramref name="x2"/> lies outside the range of <paramref name="x"/>.
    /// </exception>
    public static double[] Interpolate(double[] x, double[] y, double[] c, double[] x2)
    {
      double[] y2 = new double[x2.Length];
      int num = 0;
      for (int i = 0; i < x2.Length; i++)
      {
        if (x2[i] < x[0] || x[x.Length - 1] < x2[i])
          throw new PopoloArgumentException(
              $"x2[{i}]={x2[i]} is out of range [{x[0]}, {x[x.Length - 1]}].",
              nameof(x2));
        while (x[num + 1] < x2[i]) num++;
        y2[i] = InterPolate(x, y, c, x2[i], num);
      }
      return y2;
    }

    /// <summary>Interpolates at a single evaluation point.</summary>
    /// <param name="x">Array of X coordinates (ascending order).</param>
    /// <param name="y">Array of Y coordinates.</param>
    /// <param name="c">Coefficient array (return value of <see cref="GetParameters"/>).</param>
    /// <param name="x2">Evaluation position.</param>
    /// <returns>Interpolated value.</returns>
    public static double Interpolate(double[] x, double[] y, double[] c, double x2)
    {
      int low = 0;
      int high = x.Length - 1;
      while (1 < high - low)
      {
        int mid = (low + high) >> 1;
        if (x2 < x[mid]) high = mid;
        else low = mid;
      }
      return InterPolate(x, y, c, x2, low);
    }

    /// <summary>Computes the interpolated value at position <paramref name="x2"/>.</summary>
    private static double InterPolate(
        double[] x, double[] y, double[] cf, double x2, int num)
    {
      double dx = x2 - x[num];
      double h = x[num + 1] - x[num];
      double a = y[num];
      double b = (y[num + 1] - y[num]) / h - h * (cf[num + 1] + 2 * cf[num]) / 3.0;
      double c = cf[num];
      double d = (cf[num + 1] - cf[num]) / (3.0 * h);
      return a + dx * (b + dx * (c + dx * d));
    }
  }
}
