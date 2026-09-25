/* TakakusakiComfortModel.cs
 *
 * Copyright (C) 2016 E.Togashi
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

using Popolo.Core.Numerics;

namespace Popolo.Core.ThermalComfort
{
  /// <summary>Models individual differences in thermal comfort preference using the Takakusaki model.</summary>
  /// <remarks>
  /// Based on: Takakusaki, A. (1998). A method for estimating the probability of occupant
  /// dissatisfaction considering variability in indoor thermal environments.
  /// <see href="https://doi.org/10.3130/aija.63.13_2"/>
  /// </remarks>
  public class TakakusakiComfortModel : IComparable<TakakusakiComfortModel>
  {

    #region Constant declarations

    /// <summary>Standard deviation of individual optimum PMV values [-] across occupants.</summary>
    public const double SigmaM = 0.85;

    /// <summary>Shape parameter of the Weibull distribution.</summary>
    public const double Beta = 7.0;

    #endregion

    #region Enumeration definitions

    /// <summary>Thermal sensation vote reported by the occupant.</summary>
    public enum ThermalSensation
    {
      /// <summary>Warm/hot sensation.</summary>
      Hot,
      /// <summary>Cool/cold sensation.</summary>
      Cold,
      /// <summary>Neutral sensation.</summary>
      Neutral
    }

    #endregion

    #region Instance variables and properties

    /// <summary>Random number generator.</summary>
    private readonly MersenneTwister rnd;

    /// <summary>Gets the individual optimum PMV value [-] for this occupant.</summary>
    public double OptimumPMV { get; private set; } = 0;

    /// <summary>Gets the Weibull scale parameter for the warm-side dissatisfaction.</summary>
    public double HotEtaZero { get; private set; }

    /// <summary>Gets the Weibull scale parameter for the cool-side dissatisfaction.</summary>
    public double ColdEtaZero { get; private set; }

    /// <summary>Gets the current environmental PMV value [-].</summary>
    public double PMV { get; private set; }

    /// <summary>Gets the probability of dissatisfaction due to warmth [-].</summary>
    public double HotDissatisfiedProbability { get; private set; }

    /// <summary>Gets the probability of dissatisfaction due to coolness [-].</summary>
    public double ColdDissatisfiedProbability { get; private set; }

    #endregion

    #region Constructors

    /// <summary>Initializes a new instance with a random optimum PMV drawn from N(0, σ_M).</summary>
    /// <param name="rndSeed">Random seed.</param>
    public TakakusakiComfortModel(uint rndSeed)
    {
      rnd = new MersenneTwister(rndSeed);
      InitializeParameters((new NormalRandom(rnd, 0, SigmaM)).NextDouble());
    }

    /// <summary>Initializes a new instance with a specified optimum PMV value.</summary>
    /// <param name="rndSeed">Random seed.</param>
    /// <param name="optPMV">Individual optimum PMV value [-].</param>
    public TakakusakiComfortModel(uint rndSeed, double optPMV)
    {
      rnd = new MersenneTwister(rndSeed);
      InitializeParameters(optPMV);
    }

    /// <summary>Initializes Weibull scale parameters from the optimum PMV (Takakusaki eq. 7, 8).</summary>
    /// <param name="optPMV">Individual optimum PMV value [-].</param>
    private void InitializeParameters(double optPMV)
    {
      OptimumPMV = optPMV;
      HotEtaZero = -Math.Pow(1.5 - 0.185 * OptimumPMV, Beta) / Math.Log(0.5);
      ColdEtaZero = -Math.Pow(1.5 + 0.185 * OptimumPMV, Beta) / Math.Log(0.5);
    }

    #endregion

    #region Instance methods

    /// <summary>Sets the current environmental PMV and updates dissatisfaction probabilities (Takakusaki eq. 3).</summary>
    /// <param name="pmv">Environmental PMV value [-].</param>
    public void SetPMV(double pmv)
    {
      PMV = pmv;
      if (OptimumPMV < pmv)
      {
        HotDissatisfiedProbability = 1.0 - Math.Exp(-Math.Pow(pmv - OptimumPMV, Beta) / HotEtaZero);
        ColdDissatisfiedProbability = 0;
      }
      else
      {
        HotDissatisfiedProbability = 0;
        ColdDissatisfiedProbability = 1.0 - Math.Exp(-Math.Pow(OptimumPMV - pmv, Beta) / ColdEtaZero);
      }
    }

    /// <summary>Samples a thermal sensation vote stochastically based on dissatisfaction probabilities.</summary>
    /// <returns>Thermal sensation vote (result varies each call due to random sampling).</returns>
    public ThermalSensation UpdateThermalSensationVote()
    {
      if (OptimumPMV < PMV)
        return rnd.NextDouble() < HotDissatisfiedProbability ? ThermalSensation.Hot : ThermalSensation.Neutral;
      else
        return rnd.NextDouble() < ColdDissatisfiedProbability ? ThermalSensation.Cold : ThermalSensation.Neutral;
    }

    #endregion

    #region IComparable implementation

    /// <summary>Compares optimum PMV values for sorting purposes.</summary>
    /// <param name="other">The other instance to compare with.</param>
    /// <returns>Positive if this instance has a higher optimum PMV; negative if lower; zero if equal.</returns>
    public int CompareTo(TakakusakiComfortModel? other)
    {
      if (other == null) return 1;
      double diff = OptimumPMV - other.OptimumPMV;
      return 0 < diff ? 1 : diff < 0 ? -1 : 0;
    }

    #endregion

  }
}
