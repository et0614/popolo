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
  /// <summary>メルセンヌ・ツイスターによる擬似乱数生成クラス</summary>
  /// <remarks>
  /// Makoto Matsumoto氏のC言語版をC#に移植。
  /// http://www.math.sci.hiroshima-u.ac.jp/~m-mat/MT/MT2002/CODES/mt19937ar.c
  /// このクラスはスレッドセーフではない。マルチスレッド環境では
  /// スレッドごとに個別のインスタンスを使用すること。
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

    /// <summary>乱数シードを取得する</summary>
    public uint Seed { get; private set; }

    private readonly uint[] mt = new uint[N];
    private readonly uint[] mag01 = new uint[] { 0x0U, MATRIX_A };
    private int mti;

    #endregion

    #region コンストラクタ

    /// <summary>コンストラクタ</summary>
    /// <param name="seed">乱数シード</param>
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

    /// <summary>符号なし32bitの擬似乱数を生成する</summary>
    /// <returns>符号なし32bitの擬似乱数</returns>
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

    /// <summary>0.0以上1.0以下のランダムな浮動小数点数を返す</summary>
    /// <returns>0.0以上1.0以下のランダムな浮動小数点数</returns>
    public double NextDouble()
    {
      return NextUInt32() * (1.0 / 4294967295.0);
    }

    /// <summary>0.0以上1.0未満のランダムな浮動小数点数を返す</summary>
    /// <returns>0.0以上1.0未満のランダムな浮動小数点数</returns>
    public double NextDouble2()
    {
      return NextUInt32() * (1.0 / 4294967296.0);
    }

    /// <summary>0.0よりも大きく1.0未満のランダムな浮動小数点数を返す</summary>
    /// <returns>0.0よりも大きく1.0未満のランダムな浮動小数点数</returns>
    public double NextDouble3()
    {
      return (NextUInt32() + 0.5) * (1.0 / 4294967296.0);
    }

    /// <summary>0以上のランダムな符号なし整数を返す</summary>
    /// <returns>0以上のランダムな符号なし整数</returns>
    public uint Next()
    {
      return NextUInt32();
    }

    /// <summary>ランダムな符号付き整数を返す</summary>
    /// <returns>ランダムな符号付き整数</returns>
    public int NextInt()
    {
      return (int)(NextUInt32() - 2147483648U);
    }

    #endregion

  }
}
