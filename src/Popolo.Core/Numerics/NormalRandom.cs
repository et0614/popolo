/* NormalRandom.cs
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
using Popolo.Exceptions;

namespace Popolo.Numerics
{
    /// <summary>正規分布に従う乱数系列を生成するクラス</summary>
    [Serializable]
    public class NormalRandom
    {

        #region インスタンス変数・プロパティ

        /// <summary>一様乱数生成器</summary>
        private readonly MersenneTwister rnd;

        /// <summary>乱数ストック</summary>
        private double rndStock;

        /// <summary>乱数ストックを持つか否か</summary>
        private bool hasStock = false;

        /// <summary>平均μを取得する</summary>
        public double Mean { get; private set; }

        /// <summary>標準偏差σを取得する</summary>
        public double StandardDeviation { get; private set; }

        #endregion

        #region コンストラクタ

        /// <summary>インスタンスを初期化する</summary>
        /// <param name="seed">乱数シード</param>
        /// <param name="mean">平均</param>
        /// <param name="standardDeviation">標準偏差（0より大きい値）</param>
        /// <exception cref="PopoloArgumentException">
        /// standardDeviation が0以下の場合。
        /// </exception>
        public NormalRandom(uint seed, double mean = 0, double standardDeviation = 1)
            : this(new MersenneTwister(seed), mean, standardDeviation) { }

        /// <summary>インスタンスを初期化する</summary>
        /// <param name="rnd">一様乱数生成器</param>
        /// <param name="mean">平均</param>
        /// <param name="standardDeviation">標準偏差（0より大きい値）</param>
        /// <exception cref="PopoloArgumentException">
        /// standardDeviation が0以下の場合。
        /// </exception>
        public NormalRandom(MersenneTwister rnd, double mean = 0, double standardDeviation = 1)
        {
            if (standardDeviation <= 0)
                throw new PopoloArgumentException(
                    $"standardDeviation must be positive. Got: {standardDeviation}",
                    nameof(standardDeviation));

            this.rnd = rnd;
            Mean = mean;
            StandardDeviation = standardDeviation;
        }

        #endregion

        #region インスタンスメソッド

        /// <summary>平均μ標準偏差σに従う正規乱数を返す</summary>
        /// <returns>正規乱数</returns>
        public double NextDouble()
        {
            if (hasStock)
            {
                hasStock = false;
                return rndStock * StandardDeviation + Mean;
            }
            else
            {
                hasStock = true;
                MakeNormalRandomNumbers(rnd, out double rtn, out rndStock);
                return rtn * StandardDeviation + Mean;
            }
        }

        /// <summary>標準正規分布（平均0・標準偏差1）に従う乱数を返す</summary>
        /// <returns>標準正規乱数</returns>
        public double NextDouble_Standard()
        {
            if (hasStock)
            {
                hasStock = false;
                return rndStock;
            }
            else
            {
                hasStock = true;
                MakeNormalRandomNumbers(rnd, out double rtn, out rndStock);
                return rtn;
            }
        }

        /// <summary>Box-Muller法で標準正規乱数を2つ生成する</summary>
        /// <param name="rnd">一様乱数生成器</param>
        /// <param name="nrnd1">標準正規乱数1</param>
        /// <param name="nrnd2">標準正規乱数2</param>
        private static void MakeNormalRandomNumbers(
            MersenneTwister rnd, out double nrnd1, out double nrnd2)
        {
            double v, u, r2;
            do
            {
                v = rnd.NextDouble() * 2 - 1;
                u = rnd.NextDouble() * 2 - 1;
                r2 = v * v + u * u;
            } while (1 <= r2);

            double w = Math.Sqrt(-2.0 * Math.Log(r2) / r2);
            nrnd1 = w * v;
            nrnd2 = w * u;
        }

        #endregion

        #region 静的メソッド

        /// <summary>正規分布の累積分布関数の値を計算する</summary>
        /// <param name="x">確率変数の値</param>
        /// <param name="mean">平均</param>
        /// <param name="standardDeviation">標準偏差（0より大きい値）</param>
        /// <returns>累積確率（0以上1以下）</returns>
        /// <exception cref="PopoloArgumentException">
        /// standardDeviation が0以下の場合。
        /// </exception>
        public static double CumulativeDistribution(
            double x, double mean = 0, double standardDeviation = 1)
        {
            if (standardDeviation <= 0)
                throw new PopoloArgumentException(
                    $"standardDeviation must be positive. Got: {standardDeviation}",
                    nameof(standardDeviation));

            return 0.5 * SpecialFunctions.ComplementaryErrorFunction(
                -((x - mean) / standardDeviation) / Math.Sqrt(2));
        }

        /// <summary>正規分布の累積分布関数の逆関数を計算する</summary>
        /// <param name="p">累積確率（0より大きく1より小さい値）</param>
        /// <param name="mean">平均</param>
        /// <param name="standardDeviation">標準偏差（0より大きい値）</param>
        /// <returns>確率変数の値</returns>
        /// <remarks>
        /// Acklam's Algorithm による近似。
        /// http://home.online.no/~pjacklam/notes/invnorm
        /// </remarks>
        /// <exception cref="PopoloArgumentException">
        /// standardDeviation が0以下の場合。
        /// </exception>
        public static double CumulativeDistributionInverse(
            double p, double mean = 0, double standardDeviation = 1)
        {
            if (standardDeviation <= 0)
                throw new PopoloArgumentException(
                    $"standardDeviation must be positive. Got: {standardDeviation}",
                    nameof(standardDeviation));

            double[] a = {
                -3.969683028665376e1,
                 2.209460984245205e2,
                -2.759285104469687e2,
                 1.383577518672690e2,
                -3.066479806614716e1,
                 2.506628277459239
            };
            double[] b = {
                -5.447609879822406e1,
                 1.615858368580409e2,
                -1.556989798598866e2,
                 6.680131188771972e1,
                -1.328068155288572e1
            };
            double[] c = {
                -7.784894002430293e-3,
                -3.223964580411365e-1,
                -2.400758277161838,
                -2.549732539343734,
                 4.374664141464968,
                 2.938163982698783
            };
            double[] d = {
                7.784695709041462e-3,
                3.224671290700398e-1,
                2.445134137142996,
                3.754408661907416
            };

            if (p <= 0) return double.NegativeInfinity;
            if (1.0 <= p) return double.PositiveInfinity;

            double x;
            if (p < 0.02425)
            {
                double q = Math.Sqrt(-2 * Math.Log(p));
                x = (((((c[0] * q + c[1]) * q + c[2]) * q + c[3]) * q + c[4]) * q + c[5]) /
                    ((((d[0] * q + d[1]) * q + d[2]) * q + d[3]) * q + 1);
            }
            else if (1 - 0.02425 < p)
            {
                double q = Math.Sqrt(-2 * Math.Log(1 - p));
                x = -(((((c[0] * q + c[1]) * q + c[2]) * q + c[3]) * q + c[4]) * q + c[5]) /
                     ((((d[0] * q + d[1]) * q + d[2]) * q + d[3]) * q + 1);
            }
            else
            {
                double q = p - 0.5;
                double r = q * q;
                x = (((((a[0] * r + a[1]) * r + a[2]) * r + a[3]) * r + a[4]) * r + a[5]) * q /
                    (((((b[0] * r + b[1]) * r + b[2]) * r + b[3]) * r + b[4]) * r + 1);
            }

            double e = 0.5 * SpecialFunctions.ComplementaryErrorFunction(-x / Math.Sqrt(2)) - p;
            double u = -e * Math.Sqrt(2 * Math.PI) * Math.Exp(x * x / 2.0);
            x -= u / (1 + x * u / 2.0);

            return standardDeviation * x + mean;
        }

        #endregion

    }
}
