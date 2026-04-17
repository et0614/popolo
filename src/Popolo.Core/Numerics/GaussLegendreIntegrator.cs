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
  /// <summary>ガウス・ルジャンドル積分クラス</summary>
  /// <remarks>ニューメリカルレシピより</remarks>
  [Serializable]
  public class GaussLegendreIntegrator
  {
    /// <summary>積分する関数</summary>
    /// <param name="x">入力値</param>
    /// <returns>出力値</returns>
    public delegate double IntegrateFunction(double x);

    /// <summary>分点</summary>
    private double[] x;

    /// <summary>重み</summary>
    private double[] w;

    /// <summary>被積分関数</summary>
    private readonly IntegrateFunction iFnc;

    /// <summary>コンストラクタ</summary>
    /// <param name="iFnc">被積分関数</param>
    /// <param name="nodeCount">分点の数（1以上）</param>
    /// <exception cref="PopoloArgumentException">
    /// nodeCount が1未満の場合。
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

    /// <summary>区間abで定積分する</summary>
    /// <param name="a">下限値</param>
    /// <param name="b">上限値</param>
    /// <returns>積分値</returns>
    public double Integrate(double a, double b)
    {
      return Integrate(iFnc, a, b, x, w);
    }

    /// <summary>分点の数を更新する</summary>
    /// <param name="nodeCount">分点の数（1以上）</param>
    /// <exception cref="PopoloArgumentException">
    /// nodeCount が1未満の場合。
    /// </exception>
    public void UpdateNodeCount(int nodeCount)
    {
      if (nodeCount < 1)
        throw new PopoloArgumentException(
            $"nodeCount must be at least 1. Got: {nodeCount}",
            nameof(nodeCount));

      ComputeNodesAndWeights(nodeCount, out x, out w);
    }

    /// <summary>分点と重みを計算する</summary>
    /// <param name="number">分点の数（1以上）</param>
    /// <param name="x">出力：分点</param>
    /// <param name="w">出力：重み</param>
    /// <exception cref="PopoloArgumentException">
    /// number が1未満の場合。
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

    /// <summary>区間abで定積分する</summary>
    /// <param name="iFnc">被積分関数</param>
    /// <param name="a">下限</param>
    /// <param name="b">上限</param>
    /// <param name="x">分点</param>
    /// <param name="w">重み</param>
    /// <returns>積分値</returns>
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
