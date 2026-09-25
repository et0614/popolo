/* SpecialFunctions.cs
 *
 * Copyright (C) 2014 E.Togashi
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
  /// <summary>Provides numerical special functions.</summary>
  /// <remarks>
  /// References:
  /// Abramowitz, M. and Stegun, I.A. (eds.), Handbook of Mathematical Functions,
  /// NBS Applied Mathematics Series 55, 1964, Eqs. 6.1.15, 6.1.40, 6.5.17, 6.5.29 and 6.5.31;
  /// Thompson, I.J. and Barnett, A.R., Coulomb and Bessel functions of complex arguments
  /// and order, Journal of Computational Physics 64, pp. 490-509, 1986 (modified Lentz method).
  /// </remarks>
  public static class SpecialFunctions
  {

    /// <summary>Relative convergence tolerance of the series and continued fraction.</summary>
    private const double CONVERGENCE_TOLERANCE = 1e-15;

    /// <summary>Maximum number of series terms or continued-fraction levels.</summary>
    private const int MAX_TERMS = 10000;

    /// <summary>ln(2π)/2.</summary>
    private const double HALF_LOG_TWO_PI = 0.91893853320467274178;

    #region Incomplete gamma function

    /// <summary>Computes the regularized incomplete gamma function P(a, x).</summary>
    /// <param name="a">Shape parameter (must be positive).</param>
    /// <param name="x">Upper integration limit (non-negative).</param>
    /// <returns>Value of P(a, x).</returns>
    /// <exception cref="PopoloArgumentException">
    /// Thrown when <paramref name="a"/> is not positive or <paramref name="x"/> is negative.
    /// </exception>
    /// <exception cref="PopoloNumericalException">
    /// Thrown when the iterative computation does not converge.
    /// </exception>
    public static double GammaP(double a, double x)
    {
      if (a <= 0.0)
        throw new PopoloArgumentException(
            $"a must be positive. Got: {a}",
            nameof(a));
      if (x < 0.0)
        throw new PopoloArgumentException(
            $"x must be non-negative. Got: {x}",
            nameof(x));

      if (x < a + 1.0) return LowerRegularizedGammaBySeries(a, x);
      else return 1.0 - UpperRegularizedGammaByContinuedFraction(a, x);
    }

    /// <summary>Computes the regularized incomplete gamma function Q(a, x) = 1 - P(a, x).</summary>
    /// <param name="a">Shape parameter (must be positive).</param>
    /// <param name="x">Upper integration limit (non-negative).</param>
    /// <returns>Value of Q(a, x).</returns>
    /// <exception cref="PopoloArgumentException">
    /// Thrown when <paramref name="a"/> is not positive or <paramref name="x"/> is negative.
    /// </exception>
    /// <exception cref="PopoloNumericalException">
    /// Thrown when the iterative computation does not converge.
    /// </exception>
    public static double GammaQ(double a, double x)
    {
      if (a <= 0.0)
        throw new PopoloArgumentException(
            $"a must be positive. Got: {a}",
            nameof(a));
      if (x < 0.0)
        throw new PopoloArgumentException(
            $"x must be non-negative. Got: {x}",
            nameof(x));

      if (x < a + 1.0) return 1.0 - LowerRegularizedGammaBySeries(a, x);
      else return UpperRegularizedGammaByContinuedFraction(a, x);
    }

    /// <summary>Computes the complementary error function erfc(x).</summary>
    /// <param name="x">Input value.</param>
    /// <returns>Value of erfc(x).</returns>
    /// <remarks>Uses erf(x) = P(1/2, x²) for x ≥ 0 (A&amp;S 6.5.17) and erfc(−x) = 2 − erfc(x).</remarks>
    public static double ComplementaryErrorFunction(double x)
    {
      return x < 0.0 ? 1.0 + GammaP(0.5, x * x) : GammaQ(0.5, x * x);
    }

    /// <summary>Computes ln Γ(z) for z &gt; 0.</summary>
    /// <remarks>
    /// Arguments below 10 are raised with Γ(z+1) = z·Γ(z) (A&amp;S 6.1.15); the Stirling
    /// asymptotic series (A&amp;S 6.1.40) is then summed through the z⁻¹³ term, whose
    /// truncation error is below 1e-16 for z ≥ 10.
    /// </remarks>
    private static double LogGamma(double z)
    {
      double shiftProduct = 1.0;
      while (z < 10.0)
      {
        shiftProduct *= z;
        z += 1.0;
      }

      // Σ B_2k / (2k(2k−1)·z^(2k−1)), k = 1..7, in Horner form over 1/z²
      double r = 1.0 / (z * z);
      double correction = (1.0 / 12.0 + r * (-1.0 / 360.0 + r * (1.0 / 1260.0
          + r * (-1.0 / 1680.0 + r * (1.0 / 1188.0 + r * (-691.0 / 360360.0
          + r * (1.0 / 156.0))))))) / z;

      return (z - 0.5) * Math.Log(z) - z + HALF_LOG_TWO_PI + correction
          - Math.Log(shiftProduct);
    }

    /// <summary>Computes P(a, x) by its power series; converges quickly for x &lt; a + 1.</summary>
    /// <remarks>
    /// A&amp;S 6.5.29: P(a, x) = x^a·e^(−x)/Γ(a+1) · Σ_{k≥0} x^k / ((a+1)(a+2)…(a+k)).
    /// </remarks>
    private static double LowerRegularizedGammaBySeries(double a, double x)
    {
      if (x == 0.0) return 0.0;

      double term = 1.0;
      double sum = 1.0;
      for (int k = 1; k <= MAX_TERMS; k++)
      {
        term *= x / (a + k);
        sum += term;
        if (term < sum * CONVERGENCE_TOLERANCE)
          return sum * Math.Exp(a * Math.Log(x) - x - LogGamma(a + 1.0));
      }
      throw new PopoloNumericalException(
          nameof(LowerRegularizedGammaBySeries),
          $"The series did not converge within {MAX_TERMS} terms. a={a}, x={x}.");
    }

    /// <summary>Computes Q(a, x) by its continued fraction; converges quickly for x ≥ a + 1.</summary>
    /// <remarks>
    /// Even contraction of A&amp;S 6.5.31:
    /// Q(a, x) = x^a·e^(−x)/Γ(a) · 1/(b₀ + a₁/(b₁ + a₂/(b₂ + …))),
    /// with b_k = x + 2k + 1 − a and a_k = −k(k − a), evaluated by the modified Lentz method
    /// (Thompson and Barnett, 1986).
    /// </remarks>
    private static double UpperRegularizedGammaByContinuedFraction(double a, double x)
    {
      const double TINY = 1e-300;

      double fraction = x + 1.0 - a;   // b₀ ≥ 2 in the range where this is called
      double numeratorRatio = fraction;
      double denominatorRatio = 0.0;
      for (int k = 1; k <= MAX_TERMS; k++)
      {
        double ak = -k * (k - a);
        double bk = x + 2 * k + 1.0 - a;

        denominatorRatio = bk + ak * denominatorRatio;
        if (Math.Abs(denominatorRatio) < TINY) denominatorRatio = TINY;
        denominatorRatio = 1.0 / denominatorRatio;

        numeratorRatio = bk + ak / numeratorRatio;
        if (Math.Abs(numeratorRatio) < TINY) numeratorRatio = TINY;

        double factor = numeratorRatio * denominatorRatio;
        fraction *= factor;
        if (Math.Abs(factor - 1.0) < CONVERGENCE_TOLERANCE)
          return Math.Exp(a * Math.Log(x) - x - LogGamma(a)) / fraction;
      }
      throw new PopoloNumericalException(
          nameof(UpperRegularizedGammaByContinuedFraction),
          $"The continued fraction did not converge within {MAX_TERMS} levels. a={a}, x={x}.");
    }

    #endregion

  }
}
