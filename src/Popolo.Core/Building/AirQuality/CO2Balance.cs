/* CO2Balance.cs
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
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301, USA.
 */

using System;

using Popolo.Core.Exceptions;

namespace Popolo.Core.Building.AirQuality
{
  /// <summary>
  /// Provides static methods for indoor CO2 mass balance calculations of a
  /// well-mixed zone.
  /// </summary>
  /// <remarks>
  /// <para>
  /// Only the zone air acts as CO2 storage; unlike heat and moisture there is
  /// no accumulating body such as furniture. Concentrations are expressed as
  /// volumetric fractions [m³/m³]; multiply by 10⁶ to convert to ppm.
  /// </para>
  /// </remarks>
  public static class CO2Balance
  {

    #region 定数

    /// <summary>
    /// Standard CO2 generation rate per person [m³/s] for a Japanese adult
    /// male doing office work (0.02 m³/h).
    /// </summary>
    public const double StandardCO2GenerationRatePerPerson = 0.02 / 3600.0;

    /// <summary>
    /// Allowable indoor CO2 concentration [ppm] under the Japanese Building
    /// Sanitation Law (建築物衛生法).
    /// </summary>
    public const double BuildingSanitationLawLimit_PPM = 1000;

    #endregion

    #region staticメソッド

    /// <summary>
    /// Computes the CO2 generation rate of a person [m³/s] from the metabolic
    /// rate (Onishi's regression).
    /// </summary>
    /// <param name="metabolicRate">Metabolic rate [W].</param>
    /// <returns>CO2 generation rate [m³/s].</returns>
    public static double GetCO2GenerationRate(double metabolicRate)
    {
      //回帰式はm3/h単位のためm3/sに換算
      return (1.575e-4 * metabolicRate + 3.693e-4) / 3600.0;
    }

    /// <summary>
    /// Computes the CO2 generation rate of a person [m³/s] from body surface
    /// area, activity level, and sex.
    /// </summary>
    /// <param name="bodySurfaceArea">Body surface area (DuBois area) [m²].</param>
    /// <param name="met">Activity level [met].</param>
    /// <param name="isMale">True for male; false for female.</param>
    /// <returns>CO2 generation rate [m³/s].</returns>
    public static double GetCO2GenerationRate(double bodySurfaceArea, double met, bool isMale)
    {
      double metabolicRate =
        92.8 * bodySurfaceArea + 83.9 * met + 17.2 * (isMale ? 1.0 : 0.0) - 141.1;
      return GetCO2GenerationRate(metabolicRate);
    }

    /// <summary>
    /// Computes the CO2 concentration [m³/m³] after the specified time with
    /// constant ventilation and generation (Seidel's equation).
    /// </summary>
    /// <param name="initialCO2Level">Initial CO2 concentration [m³/m³].</param>
    /// <param name="outdoorCO2Level">Outdoor CO2 concentration [m³/m³].</param>
    /// <param name="co2Generation">CO2 generation rate [m³/s].</param>
    /// <param name="ventilationRate">Ventilation rate [m³/s].</param>
    /// <param name="airVolume">Zone air volume [m³].</param>
    /// <param name="time">Elapsed time [s].</param>
    /// <returns>CO2 concentration after the elapsed time [m³/m³].</returns>
    public static double GetConcentration
      (double initialCO2Level, double outdoorCO2Level, double co2Generation,
      double ventilationRate, double airVolume, double time)
    {
      if (airVolume <= 0)
        throw new PopoloArgumentException("airVolume must be positive.", nameof(airVolume));

      //換気量0の場合には発生分が単調に蓄積
      if (ventilationRate <= 0)
        return initialCO2Level + co2Generation * time / airVolume;

      double ex = Math.Exp(-ventilationRate / airVolume * time);
      return outdoorCO2Level
        + (initialCO2Level - outdoorCO2Level) * ex
        + co2Generation / ventilationRate * (1 - ex);
    }

    /// <summary>
    /// Computes the ventilation rate [m³/s] required to reach the target CO2
    /// concentration after the specified time (backward difference
    /// approximation of the mass balance).
    /// </summary>
    /// <param name="currentCO2Level">Current CO2 concentration [m³/m³].</param>
    /// <param name="targetCO2Level">Target CO2 concentration after the elapsed time [m³/m³].</param>
    /// <param name="outdoorCO2Level">Outdoor CO2 concentration [m³/m³].</param>
    /// <param name="co2Generation">CO2 generation rate [m³/s].</param>
    /// <param name="airVolume">Zone air volume [m³].</param>
    /// <param name="time">Elapsed time [s].</param>
    /// <returns>Required ventilation rate [m³/s].</returns>
    /// <exception cref="PopoloArgumentException">
    /// Thrown when the target concentration does not exceed the outdoor
    /// concentration: ventilation alone can never dilute the zone air below
    /// the outdoor level.
    /// </exception>
    public static double GetRequiredVentilationRate
      (double currentCO2Level, double targetCO2Level, double outdoorCO2Level,
      double co2Generation, double airVolume, double time)
    {
      if (airVolume <= 0)
        throw new PopoloArgumentException("airVolume must be positive.", nameof(airVolume));
      if (time <= 0)
        throw new PopoloArgumentException("time must be positive.", nameof(time));
      if (targetCO2Level <= outdoorCO2Level)
        throw new PopoloArgumentException(
          "targetCO2Level must exceed outdoorCO2Level: ventilation cannot "
          + "dilute the zone air below the outdoor concentration. "
          + $"target = {targetCO2Level:G4}, outdoor = {outdoorCO2Level:G4}.",
          nameof(targetCO2Level));

      return (airVolume * (targetCO2Level - currentCO2Level) - co2Generation * time)
        / ((outdoorCO2Level - targetCO2Level) * time);
    }

    #endregion

  }
}
