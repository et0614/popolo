/* GammaRandom.cs
 *
 * Copyright (C) 2015 E.Togashi
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
  /// <summary>ガンマ分布に従う乱数系列を生成するクラス</summary>
  /// <remarks>
  /// 谷崎久志 (2008) 「ガンマ乱数の生成方法について」 『国民経済雑誌』第197巻, 第4号, pp.17-30.
  /// H. Tanizaki (2008) "A Simple Gamma Random Number Generator for Arbitrary Shape Parameters,"
  /// Economics Bulletin, Vol.3, No.7, pp.1-10.
  /// </remarks>
  [Serializable]
  public class GammaRandom
  {

    #region インスタンス変数

    /// <summary>一様乱数生成器</summary>
    private readonly MersenneTwister random;

    /// <summary>内部パラメータ</summary>
    private readonly double n, b1, b2, c1, c2;

    #endregion

    #region プロパティ

    /// <summary>形状パラメータαを取得する</summary>
    public double Alpha { get; private set; }

    /// <summary>尺度パラメータβを取得する</summary>
    public double Beta { get; private set; }

    #endregion

    #region コンストラクタ

    /// <summary>コンストラクタ</summary>
    /// <param name="seed">乱数シード</param>
    /// <param name="alpha">形状パラメータα（0より大きい値）</param>
    /// <param name="beta">尺度パラメータβ（0より大きい値）</param>
    /// <exception cref="PopoloArgumentException">
    /// alpha または beta が0以下の場合。
    /// </exception>
    public GammaRandom(uint seed, double alpha, double beta)
        : this(new MersenneTwister(seed), alpha, beta) { }

    /// <summary>コンストラクタ</summary>
    /// <param name="rnd">一様乱数生成器</param>
    /// <param name="alpha">形状パラメータα（0より大きい値）</param>
    /// <param name="beta">尺度パラメータβ（0より大きい値）</param>
    /// <exception cref="PopoloArgumentException">
    /// alpha または beta が0以下の場合。
    /// </exception>
    public GammaRandom(MersenneTwister rnd, double alpha, double beta)
    {
      if (alpha <= 0)
        throw new PopoloArgumentException(
            $"alpha must be positive. Got: {alpha}", nameof(alpha));
      if (beta <= 0)
        throw new PopoloArgumentException(
            $"beta must be positive. Got: {beta}", nameof(beta));

      this.random = rnd;
      this.Alpha = alpha;
      this.Beta = beta;

      // 内部パラメータ初期化
      if (Alpha <= 0.4) n = 1.0 / Alpha;
      else if (Alpha <= 4) n = 1.0 / Alpha + (Alpha - 0.4) / (3.6 * Alpha);
      else n = 1.0 / Math.Sqrt(Alpha);

      b1 = Alpha - 1 / n;
      b2 = Alpha + 1 / n;

      c1 = (Alpha <= 0.4) ? 0 : b1 * (Math.Log(b1) - 1) / 2;
      c2 = b2 * (Math.Log(b2) - 1) / 2;
    }

    #endregion

    #region メソッド

    /// <summary>ガンマ分布に従う乱数を返す</summary>
    /// <returns>ガンマ分布に従う乱数</returns>
    public double NextDouble()
    {
      double x, y, w1, w2, v1, v2;

      do
      {
        do
        {
          v1 = random.NextDouble();
          v2 = random.NextDouble();
          w1 = c1 + Math.Log(v1);
          w2 = c2 + Math.Log(v2);
          y = n * (b1 * w2 - b2 * w1);
        } while (y < 0);
        x = n * (w2 - w1);
      } while (Math.Log(y) < x);

      return Beta * Math.Exp(x);
    }

    #endregion

  }
}
