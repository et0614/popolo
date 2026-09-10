/* PowerConverter.cs
 *
 * Copyright (C) 2026 E.Togashi
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

namespace Popolo.Core.Energy
{
  /// <summary>
  /// Represents a power converter (inverter, rectifier or DC-DC converter) with a
  /// part-load dependent conversion efficiency.
  /// </summary>
  /// <remarks>
  /// The efficiency is expressed by the normalized quadratic loss model:
  /// eta(p) = p / (p + a*p^2 + b*p + c), where p is the load ratio to the rated power.
  /// The coefficient c represents the no-load loss, b the losses proportional to the
  /// power (switching losses and forward voltage drops), and a the ohmic losses
  /// proportional to the squared power.
  /// The load ratio should be evaluated with the power on the known (reference) side:
  /// e.g. the AC side for a battery storage system, the DC side for a PV inverter.
  /// References:
  /// - EnergyPlus Engineering Reference: Electric Load Center Distribution Manager.
  /// </remarks>
  public class PowerConverter : IReadOnlyPowerConverter
  {

    #region Constants

    /// <summary>Default quadratic loss coefficient a [-].</summary>
    public const double DefaultCoefficientA = 0.01;

    /// <summary>Default linear loss coefficient b [-].</summary>
    public const double DefaultCoefficientB = 0.01;

    /// <summary>Default no-load loss coefficient c [-].</summary>
    public const double DefaultCoefficientC = 0.03;

    #endregion

    #region Properties

    /// <summary>Backing field for the quadratic loss coefficient.</summary>
    private double _coefficientA;

    /// <summary>Backing field for the linear loss coefficient.</summary>
    private double _coefficientB;

    /// <summary>Backing field for the no-load loss coefficient.</summary>
    private double _coefficientC;

    /// <summary>Gets the rated power [W] on the reference side.</summary>
    public double RatedPower { get; private set; }

    /// <summary>Gets or sets the quadratic loss coefficient a [-] (clamped to 0 or more).</summary>
    public double CoefficientA
    {
      get => _coefficientA;
      set => _coefficientA = Math.Max(0, value);
    }

    /// <summary>Gets or sets the linear loss coefficient b [-] (clamped to 0 or more).</summary>
    public double CoefficientB
    {
      get => _coefficientB;
      set => _coefficientB = Math.Max(0, value);
    }

    /// <summary>Gets or sets the no-load loss coefficient c [-] (clamped to 0 or more).</summary>
    public double CoefficientC
    {
      get => _coefficientC;
      set => _coefficientC = Math.Max(0, value);
    }

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new instance with the default loss coefficients.
    /// </summary>
    /// <param name="ratedPower">Rated power [W] on the reference side.</param>
    public PowerConverter(double ratedPower)
        : this(ratedPower, DefaultCoefficientA, DefaultCoefficientB, DefaultCoefficientC)
    { }

    /// <summary>
    /// Initializes a new instance with the specified loss coefficients.
    /// </summary>
    /// <param name="ratedPower">Rated power [W] on the reference side.</param>
    /// <param name="coefficientA">Quadratic loss coefficient a [-].</param>
    /// <param name="coefficientB">Linear loss coefficient b [-].</param>
    /// <param name="coefficientC">No-load loss coefficient c [-].</param>
    public PowerConverter(
        double ratedPower, double coefficientA, double coefficientB, double coefficientC)
    {
      RatedPower = ratedPower;
      CoefficientA = coefficientA;
      CoefficientB = coefficientB;
      CoefficientC = coefficientC;
    }

    #endregion

    #region Instance methods

    /// <summary>
    /// Gets the conversion efficiency [-] at the specified power.
    /// </summary>
    /// <param name="power">Power [W] on the reference side.</param>
    /// <returns>Conversion efficiency [-]</returns>
    public double GetEfficiency(double power)
    {
      return GetEfficiencyAtLoadRatio(power / RatedPower);
    }

    /// <summary>
    /// Gets the conversion efficiency [-] at the specified load ratio.
    /// </summary>
    /// <param name="loadRatio">Load ratio [-] to the rated power (clamped to [0, 1]).</param>
    /// <returns>Conversion efficiency [-]</returns>
    public double GetEfficiencyAtLoadRatio(double loadRatio)
    {
      double p = Math.Max(0, Math.Min(1, loadRatio));
      if (p <= 0) return 0;
      return p / (p + _coefficientA * p * p + _coefficientB * p + _coefficientC);
    }

    #endregion

    #region Static methods

    /// <summary>
    /// Estimates the loss coefficients from three measured operating points.
    /// </summary>
    /// <remarks>
    /// The efficiency model is linear with respect to the coefficients:
    /// a*p^2 + b*p + c = p * (1 / eta - 1). The three point condition is
    /// therefore solved as a 3x3 linear system with Cramer's rule.
    /// </remarks>
    /// <param name="loadRatio1">Load ratio [-] of the first operating point.</param>
    /// <param name="efficiency1">Efficiency [-] of the first operating point.</param>
    /// <param name="loadRatio2">Load ratio [-] of the second operating point.</param>
    /// <param name="efficiency2">Efficiency [-] of the second operating point.</param>
    /// <param name="loadRatio3">Load ratio [-] of the third operating point.</param>
    /// <param name="efficiency3">Efficiency [-] of the third operating point.</param>
    /// <returns>Tuple of the loss coefficients (a, b, c).</returns>
    public static (double coefficientA, double coefficientB, double coefficientC)
        EstimateCoefficients(
        double loadRatio1, double efficiency1,
        double loadRatio2, double efficiency2,
        double loadRatio3, double efficiency3)
    {
      //Right hand side: p * (1 / eta - 1)
      double y1 = loadRatio1 * (1.0 / efficiency1 - 1.0);
      double y2 = loadRatio2 * (1.0 / efficiency2 - 1.0);
      double y3 = loadRatio3 * (1.0 / efficiency3 - 1.0);

      //Solve [p^2 p 1][a b c]' = y with Cramer's rule
      double det = determinant(
          loadRatio1 * loadRatio1, loadRatio1, 1,
          loadRatio2 * loadRatio2, loadRatio2, 1,
          loadRatio3 * loadRatio3, loadRatio3, 1);
      double detA = determinant(
          y1, loadRatio1, 1,
          y2, loadRatio2, 1,
          y3, loadRatio3, 1);
      double detB = determinant(
          loadRatio1 * loadRatio1, y1, 1,
          loadRatio2 * loadRatio2, y2, 1,
          loadRatio3 * loadRatio3, y3, 1);
      double detC = determinant(
          loadRatio1 * loadRatio1, loadRatio1, y1,
          loadRatio2 * loadRatio2, loadRatio2, y2,
          loadRatio3 * loadRatio3, loadRatio3, y3);

      return (detA / det, detB / det, detC / det);
    }

    /// <summary>Computes the determinant of a 3x3 matrix.</summary>
    private static double determinant(
        double m11, double m12, double m13,
        double m21, double m22, double m23,
        double m31, double m32, double m33)
    {
      return m11 * (m22 * m33 - m23 * m32)
          - m12 * (m21 * m33 - m23 * m31)
          + m13 * (m21 * m32 - m22 * m31);
    }

    #endregion

  }

  /// <summary>
  /// Represents a read-only view of a power converter.
  /// </summary>
  public interface IReadOnlyPowerConverter
  {
    /// <summary>Gets the rated power [W] on the reference side.</summary>
    double RatedPower { get; }

    /// <summary>Gets the quadratic loss coefficient a [-].</summary>
    double CoefficientA { get; }

    /// <summary>Gets the linear loss coefficient b [-].</summary>
    double CoefficientB { get; }

    /// <summary>Gets the no-load loss coefficient c [-].</summary>
    double CoefficientC { get; }

    /// <summary>Gets the conversion efficiency [-] at the specified power [W].</summary>
    /// <param name="power">Power [W] on the reference side.</param>
    /// <returns>Conversion efficiency [-]</returns>
    double GetEfficiency(double power);

    /// <summary>Gets the conversion efficiency [-] at the specified load ratio [-].</summary>
    /// <param name="loadRatio">Load ratio [-] to the rated power (clamped to [0, 1]).</param>
    /// <returns>Conversion efficiency [-]</returns>
    double GetEfficiencyAtLoadRatio(double loadRatio);
  }

}
