/* LogNormalRandom.cs
 *
 * Copyright (C) 2018 E.Togashi
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using Popolo.Core.Exceptions;

namespace Popolo.Core.Numerics
{
  /// <summary>Generates random samples from a log-normal distribution.</summary>
  public class LogNormalRandom
  {

    #region Instance variables and properties

    /// <summary>Underlying normal random number generator.</summary>
    private readonly NormalRandom nRnd;

    /// <summary>Gets the mean μ.</summary>
    public double Mean { get; private set; }

    /// <summary>Gets the standard deviation σ.</summary>
    public double StandardDeviation { get; private set; }

    #endregion

    #region Constructors

    /// <summary>Initializes a new instance seeded with a uniform RNG.</summary>
    /// <param name="seed">Random seed.</param>
    /// <param name="mean">Mean (must be positive).</param>
    /// <param name="standardDeviation">Standard deviation (must be positive).</param>
    /// <exception cref="PopoloArgumentException">
    /// Thrown when <paramref name="mean"/> or <paramref name="standardDeviation"/> is not positive.
    /// </exception>
    public LogNormalRandom(uint seed, double mean = 1, double standardDeviation = 1)
        : this(new MersenneTwister(seed), mean, standardDeviation) { }

    /// <summary>Initializes a new instance using the specified uniform RNG.</summary>
    /// <param name="rnd">Uniform random number generator.</param>
    /// <param name="mean">Mean (must be positive).</param>
    /// <param name="standardDeviation">Standard deviation (must be positive).</param>
    /// <exception cref="PopoloArgumentException">
    /// Thrown when <paramref name="mean"/> or <paramref name="standardDeviation"/> is not positive.
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

    /// <summary>Returns a sample drawn from the log-normal distribution.</summary>
    /// <returns>A random sample from the configured log-normal distribution (always positive).</returns>
    public double NextDouble()
    {
      return Math.Exp(nRnd.NextDouble());
    }

  }
}
