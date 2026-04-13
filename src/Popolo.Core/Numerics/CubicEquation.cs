/* CubicEquation.cs
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
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301, USA.
 */

using System;

namespace Popolo.Core.Numerics
{
  /// <summary>3次方程式ソルバ</summary>
  public static class CubicEquation
  {
    /// <summary>機械イプシロン</summary>
    private static readonly double MECH_EPS;

    /// <summary>コンストラクタ</summary>
    static CubicEquation()
    {
      //機械イプシロン初期化
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

    /// <summary>3次方程式の実数根を求める</summary>
    /// <param name="a">係数: a[0]*x^3+a[1]*x^2+a[2]*x+a[3]</param>
    /// <param name="x0">出力: 実数根1</param>
    /// <param name="x1">出力: 実数根2</param>
    /// <param name="x2">出力: 実数根3</param>
    /// <param name="hasMultiSolution">出力: 複数解があるか否か</param>
    public static void Solve(double[] a, out double x0, out double x1, out double x2, out bool hasMultiSolution)
    {
      //p,qを計算
      double bf = a[1] / (3 * a[0]);
      double p = - bf * bf + a[2] / (3 * a[0]);
      double q = bf * (2 * bf * bf - a[2] / a[0]) + a[3] / a[0];

      if (Math.Abs(p) < MECH_EPS && Math.Abs(q) < MECH_EPS)
      {
        hasMultiSolution = false;
        x0 = x1 = x2 = -bf;
        return;
      }

      //根の数の判定
      double pq = q * q + 4 * p * p * p;
      //実数根3つ
      if (pq < 0)
      {
        hasMultiSolution = true;
        double theta = Math.Atan2(Math.Sqrt(-pq) ,-q);
        double sqp2 = 2 * Math.Sqrt(-p);
        x0 = sqp2 * Math.Cos(theta / 3d) - bf;
        x1 = sqp2 * Math.Cos((2 * Math.PI + theta) / 3d) - bf;
        x2 = sqp2 * Math.Cos((4 * Math.PI + theta) / 3d) - bf;
      }
      //実数根1つ
      else
      {
        hasMultiSolution = false;
        double sqpq = Math.Sqrt(pq);
        double r1 = 0.5 * (-q + sqpq);
        double r2 = 0.5 * (-q - sqpq);
        x0 = Math.Sign(r1) * Math.Pow(Math.Abs(r1), 1d / 3) + Math.Sign(r2) * Math.Pow(Math.Abs(r2), 1d / 3) - bf;
        x1 = x2 = 0;
      }
    }

  }
}
