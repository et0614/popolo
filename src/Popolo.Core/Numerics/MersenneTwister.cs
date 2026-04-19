/* MersenneTwister.cs
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

namespace Popolo.Core.Numerics
{
  /// <summary>Mersenne Twister pseudo-random number generator.</summary>
  /// <remarks>
  /// C# port of Makoto Matsumoto's C reference implementation
  /// (http://www.math.sci.hiroshima-u.ac.jp/~m-mat/MT/MT2002/CODES/mt19937ar.c).
  /// This class is not thread-safe; use a separate instance per thread.
  /// </remarks>
  [Serializable]
  public class MersenneTwister
  {

    #region 定数

    private const int N = 624;
    private const int M = 397;
    private const uint MATRIX_A = 0x9908b0dfU;
    private const uint UPPER_MASK = 0x80000000U;
    private const uint LOWER_MASK = 0x7fffffffU;

    #endregion

    #region インスタンス変数

    /// <summary>Gets the random seed.</summary>
    public uint Seed { get; private set; }

    private readonly uint[] mt = new uint[N];
    private readonly uint[] mag01 = new uint[] { 0x0U, MATRIX_A };
    private int mti;

    #endregion

    #region コンストラクタ

    /// <summary>Initializes a new instance with the specified seed.</summary>
    /// <param name="seed">Random seed.</param>
    public MersenneTwister(uint seed)
    {
      Seed = seed;
      mt[0] = seed & 0xffffffffU;
      for (mti = 1; mti < N; mti++)
      {
        mt[mti] = 1812433253U * (mt[mti - 1] ^ (mt[mti - 1] >> 30)) + (uint)mti;
        mt[mti] &= 0xffffffffU;
      }
    }

    #endregion

    #region 乱数生成

    /// <summary>Generates the next 32-bit unsigned pseudo-random number.</summary>
    /// <returns>A 32-bit unsigned pseudo-random number.</returns>
    private uint NextUInt32()
    {
      uint y;

      if (mti >= N)
      {
        int kk;
        for (kk = 0; kk < N - M; kk++)
        {
          y = (mt[kk] & UPPER_MASK) | (mt[kk + 1] & LOWER_MASK);
          mt[kk] = mt[kk + M] ^ (y >> 1) ^ mag01[y & 0x1UL];
        }
        for (; kk < N - 1; kk++)
        {
          y = (mt[kk] & UPPER_MASK) | (mt[kk + 1] & LOWER_MASK);
          mt[kk] = mt[kk + (M - N)] ^ (y >> 1) ^ mag01[y & 0x1UL];
        }
        y = (mt[N - 1] & UPPER_MASK) | (mt[0] & LOWER_MASK);
        mt[N - 1] = mt[M - 1] ^ (y >> 1) ^ mag01[y & 0x1UL];

        mti = 0;
      }

      y = mt[mti++];

      y ^= (y >> 11);
      y ^= (y << 7) & 0x9d2c5680U;
      y ^= (y << 15) & 0xefc60000U;
      y ^= (y >> 18);

      return y;
    }

    /// <summary>Returns a random double in the closed interval [0.0, 1.0].</summary>
    /// <returns>A random double in [0.0, 1.0].</returns>
    public double NextDouble()
    {
      return NextUInt32() * (1.0 / 4294967295.0);
    }

    /// <summary>Returns a random double in the half-open interval [0.0, 1.0).</summary>
    /// <returns>A random double in [0.0, 1.0).</returns>
    public double NextDouble2()
    {
      return NextUInt32() * (1.0 / 4294967296.0);
    }

    /// <summary>Returns a random double in the open interval (0.0, 1.0).</summary>
    /// <returns>A random double in (0.0, 1.0).</returns>
    public double NextDouble3()
    {
      return (NextUInt32() + 0.5) * (1.0 / 4294967296.0);
    }

    /// <summary>Returns a random non-negative unsigned integer.</summary>
    /// <returns>A random unsigned 32-bit integer.</returns>
    public uint Next()
    {
      return NextUInt32();
    }

    /// <summary>Returns a random signed integer.</summary>
    /// <returns>A random signed 32-bit integer.</returns>
    public int NextInt()
    {
      return (int)(NextUInt32() - 2147483648U);
    }

    #endregion

  }
}
