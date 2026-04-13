/* FourierTransform.cs
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
  /// <summary>フーリエ変換を行うクラス</summary>
  public static class FourierTransform
  {
    /// <summary>高速フーリエ変換（FFT）を行う</summary>
    /// <param name="x">実部の入力配列。変換後の実部で上書きされる。</param>
    /// <param name="xi">虚部の入力配列。変換後の虚部で上書きされる。</param>
    /// <remarks>
    /// 入力長が2の冪でない場合は、2の冪に切り上げてゼロパディングして処理する。
    /// 結果は x および xi に書き戻されないことに注意。
    /// Danielson-Lanczos アルゴリズムを使用。
    /// </remarks>
    /// <exception cref="PopoloArgumentException">
    /// x または xi が null もしくは空の場合、あるいは長さが一致しない場合。
    /// </exception>
    public static void FFT(double[] x, double[] xi)
    {
      if (x == null || x.Length == 0)
        throw new PopoloArgumentException(
            "x must not be null or empty.", nameof(x));
      if (xi == null || xi.Length == 0)
        throw new PopoloArgumentException(
            "xi must not be null or empty.", nameof(xi));
      if (x.Length != xi.Length)
        throw new PopoloArgumentException(
            $"x and xi must have the same length. x.Length={x.Length}, xi.Length={xi.Length}.",
            nameof(xi));

      // 配列を2の冪乗に調整
      int len2 = (int)Math.Ceiling(Math.Log(x.Length, 2));
      int len = (int)Math.Pow(2, len2);
      double[] y = new double[len];
      double[] yi = new double[len];
      x.CopyTo(y, 0);
      xi.CopyTo(yi, 0);

      // ビット反転処理
      int jj = 0;
      for (int i = 0; i < len; i++)
      {
        if (i < jj)
        {
          Swap(ref y, i, jj);
          Swap(ref yi, i, jj);
        }
        int m = len >> 1;
        while (1 <= m && m <= jj)
        {
          jj -= m;
          m >>= 1;
        }
        jj += m;
      }

      // Danielson-Lanczos
      int nj = 1;
      double pk = Math.PI;
      for (int i = 0; i < len2; i++)
      {
        double w = 1.0;
        double wi = 0.0;
        double w1 = 0.0;
        double w1i = pk;
        Exp(ref w1, ref w1i);
        for (int j = 0; j < nj; j++)
        {
          for (int k0 = j; k0 < len; k0 += (2 * nj))
          {
            int k1 = k0 + nj;
            double wg = w;
            double wgi = wi;
            Multi(ref wg, ref wgi, y[k1], yi[k1]);
            y[k1] = y[k0] - wg;
            y[k0] = y[k0] + wg;
            yi[k1] = yi[k0] - wgi;
            yi[k0] = yi[k0] + wgi;
          }
          Multi(ref w, ref wi, w1, w1i);
        }
        nj *= 2;
        pk /= 2.0;
      }

      // 結果を元の配列に書き戻す
      for (int i = 0; i < x.Length; i++)
      {
        x[i] = y[i];
        xi[i] = yi[i];
      }
    }

    /// <summary>逆高速フーリエ変換（IFFT）</summary>
    /// <param name="h">変換対象の配列</param>
    /// <exception cref="PopoloNotImplementedException">
    /// このメソッドは未実装です。
    /// </exception>
    public static void INV(double[] h)
    {
      throw new PopoloNotImplementedException("FourierTransform.INV");
    }

    /// <summary>複素指数関数 exp(x + xi*i) を計算する</summary>
    private static void Exp(ref double x, ref double xi)
    {
      double ex = Math.Exp(x);
      x = ex * Math.Cos(xi);
      xi = ex * Math.Sin(xi);
    }

    /// <summary>複素数の乗算 (x + xi*i) * (y + yi*i) を計算する</summary>
    private static void Multi(ref double x, ref double xi, double y, double yi)
    {
      // 元のコードは x を上書き後に yi との積を計算するバグがあった
      // 正しくは一時変数を使って同時に計算する
      double tmpX = x * y - xi * yi;
      xi = x * yi + y * xi;
      x = tmpX;
    }

    /// <summary>配列の要素を入れ替える</summary>
    private static void Swap(ref double[] x, int a, int b)
    {
      double c = x[a];
      x[a] = x[b];
      x[b] = c;
    }
  }
}
