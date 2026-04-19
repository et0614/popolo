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
using Popolo.Core.Exceptions;

namespace Popolo.Core.Numerics
{
  /// <summary>Generates random samples from a normal distribution.</summary>
  [Serializable]
  public class NormalRandom
  {

    #region インスタンス変数・プロパティ

    /// <summary>Uniform random number generator.</summary>
    private readonly MersenneTwister rnd;

    /// <summary>Cached second value from the Box-Muller method.</summary>
    private double rndStock;

    /// <summary>Whether a cached value is available.</summary>
    private bool hasStock = false;

    /// <summary>Gets the mean μ.</summary>
    public double Mean { get; private set; }

    /// <summary>Gets the standard deviation σ.</summary>
    public double StandardDeviation { get; private set; }

    #endregion

    #region コンストラクタ

    /// <summary>Initializes a new instance seeded with a uniform RNG.</summary>
    /// <param name="seed">Random seed.</param>
    /// <param name="mean">Mean.</param>
    /// <param name="standardDeviation">Standard deviation (must be positive).</param>
    /// <exception cref="PopoloArgumentException">
    /// Thrown when <paramref name="standardDeviation"/> is not positive.
    /// </exception>
    public NormalRandom(uint seed, double mean = 0, double standardDeviation = 1)
        : this(new MersenneTwister(seed), mean, standardDeviation) { }

    /// <summary>Initializes a new instance using the specified uniform RNG.</summary>
    /// <param name="rnd">Uniform random number generator.</param>
    /// <param name="mean">Mean.</param>
    /// <param name="standardDeviation">Standard deviation (must be positive).</param>
    /// <exception cref="PopoloArgumentException">
    /// Thrown when <paramref name="standardDeviation"/> is not positive.
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

    /// <summary>Returns a sample drawn from the configured normal distribution.</summary>
    /// <returns>A random sample from N(μ, σ).</returns>
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

    /// <summary>Returns a sample drawn from the standard normal distribution (mean 0, standard deviation 1).</summary>
    /// <returns>A random sample from N(0, 1).</returns>
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

    /// <summary>Generates two standard normal samples using the Box-Muller transform.</summary>
    /// <param name="rnd">Uniform random number generator.</param>
    /// <param name="nrnd1">First standard normal sample.</param>
    /// <param name="nrnd2">Second standard normal sample.</param>
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

    /// <summary>Evaluates the cumulative distribution function (CDF) of the normal distribution.</summary>
    /// <param name="x">Value of the random variable.</param>
    /// <param name="mean">Mean.</param>
    /// <param name="standardDeviation">Standard deviation (must be positive).</param>
    /// <returns>Cumulative probability in [0, 1].</returns>
    /// <exception cref="PopoloArgumentException">
    /// Thrown when <paramref name="standardDeviation"/> is not positive.
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

    /// <summary>Evaluates the inverse CDF (quantile function) of the normal distribution.</summary>
    /// <param name="p">Cumulative probability in (0, 1).</param>
    /// <param name="mean">Mean.</param>
    /// <param name="standardDeviation">Standard deviation (must be positive).</param>
    /// <returns>Value of the random variable.</returns>
    /// <remarks>
    /// Approximation based on Acklam's algorithm
    /// (http://home.online.no/~pjacklam/notes/invnorm).
    /// </remarks>
    /// <exception cref="PopoloArgumentException">
    /// Thrown when <paramref name="standardDeviation"/> is not positive.
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
