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
  /// <summary>Generates random samples from a gamma distribution.</summary>
  /// <remarks>
  /// H. Tanizaki (2008) "A Simple Gamma Random Number Generator for Arbitrary Shape Parameters,"
  /// Economics Bulletin, Vol. 3, No. 7, pp. 1-10.
  /// </remarks>
  [Serializable]
  public class GammaRandom
  {

    #region インスタンス変数

    /// <summary>Uniform random number generator.</summary>
    private readonly MersenneTwister random;

    /// <summary>Internal parameters used by the sampler.</summary>
    private readonly double n, b1, b2, c1, c2;

    #endregion

    #region プロパティ

    /// <summary>Gets the shape parameter α.</summary>
    public double Alpha { get; private set; }

    /// <summary>Gets the scale parameter β.</summary>
    public double Beta { get; private set; }

    #endregion

    #region コンストラクタ

    /// <summary>Initializes a new instance seeded with a uniform RNG.</summary>
    /// <param name="seed">Random seed.</param>
    /// <param name="alpha">Shape parameter α (must be positive).</param>
    /// <param name="beta">Scale parameter β (must be positive).</param>
    /// <exception cref="PopoloArgumentException">
    /// Thrown when <paramref name="alpha"/> or <paramref name="beta"/> is not positive.
    /// </exception>
    public GammaRandom(uint seed, double alpha, double beta)
        : this(new MersenneTwister(seed), alpha, beta) { }

    /// <summary>Initializes a new instance using the specified uniform RNG.</summary>
    /// <param name="rnd">Uniform random number generator.</param>
    /// <param name="alpha">Shape parameter α (must be positive).</param>
    /// <param name="beta">Scale parameter β (must be positive).</param>
    /// <exception cref="PopoloArgumentException">
    /// Thrown when <paramref name="alpha"/> or <paramref name="beta"/> is not positive.
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

    /// <summary>Returns a sample drawn from the gamma distribution.</summary>
    /// <returns>A random sample from the configured gamma distribution.</returns>
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
