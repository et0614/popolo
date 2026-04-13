/* LogNormalRandom.cs
 *
 * Copyright (C) 2018 E.Togashi
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
  /// <summary>対数正規分布に従う乱数系列を生成するクラス</summary>
  public class LogNormalRandom
  {

    #region インスタンス変数・プロパティ

    /// <summary>正規乱数生成器</summary>
    private readonly NormalRandom nRnd;

    /// <summary>平均μを取得する</summary>
    public double Mean { get; private set; }

    /// <summary>標準偏差σを取得する</summary>
    public double StandardDeviation { get; private set; }

    #endregion

    #region コンストラクタ

    /// <summary>インスタンスを初期化する</summary>
    /// <param name="seed">乱数シード</param>
    /// <param name="mean">平均（0より大きい値）</param>
    /// <param name="standardDeviation">標準偏差（0より大きい値）</param>
    /// <exception cref="PopoloArgumentException">
    /// mean または standardDeviation が0以下の場合。
    /// </exception>
    public LogNormalRandom(uint seed, double mean = 1, double standardDeviation = 1)
        : this(new MersenneTwister(seed), mean, standardDeviation) { }

    /// <summary>インスタンスを初期化する</summary>
    /// <param name="rnd">一様乱数生成器</param>
    /// <param name="mean">平均（0より大きい値）</param>
    /// <param name="standardDeviation">標準偏差（0より大きい値）</param>
    /// <exception cref="PopoloArgumentException">
    /// mean または standardDeviation が0以下の場合。
    /// </exception>
    public LogNormalRandom(MersenneTwister rnd, double mean = 1, double standardDeviation = 1)
    {
      if (mean <= 0)
        throw new PopoloArgumentException(
            $"mean must be positive. Got: {mean}", nameof(mean));
      if (standardDeviation <= 0)
        throw new PopoloArgumentException(
            $"standardDeviation must be positive. Got: {standardDeviation}",
            nameof(standardDeviation));

      Mean = mean;
      StandardDeviation = standardDeviation;

      double m2 = mean * mean;
      double s2 = standardDeviation * standardDeviation;
      double mu = Math.Log(m2) - 0.5 * Math.Log(m2 + s2);
      double sig = Math.Sqrt(Math.Log(1.0 + s2 / m2));
      nRnd = new NormalRandom(rnd, mu, sig);
    }

    #endregion

    /// <summary>対数正規分布に従う乱数を返す</summary>
    /// <returns>対数正規分布に従う乱数（常に正の値）</returns>
    public double NextDouble()
    {
      return Math.Exp(nRnd.NextDouble());
    }

  }
}
