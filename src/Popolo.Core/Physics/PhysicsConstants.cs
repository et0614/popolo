/* PhysicsConstants.cs
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

namespace Popolo.Core.Physics
{
  /// <summary>
  /// Provides physical constants and unit conversion utilities.
  /// </summary>
  public static class PhysicsConstants
  {

    #region 定数宣言

    /// <summary>
    /// Offset for converting between Celsius and Kelvin [K].
    /// </summary>
    public const double CelsiusToKelvinOffset = 273.15;

    /// <summary>
    /// Standard atmospheric pressure at sea level [kPa].
    /// </summary>
    public const double StandardAtmosphericPressure = 101.325;

    /// <summary>
    /// Stefan-Boltzmann constant [W/(m²·K⁴)].
    /// </summary>
    public const double StefanBoltzmannConstant = 5.67e-8;

    /// <summary>
    /// Nominal density of moist air [kg/m³] used as a representative value
    /// for building energy simulation (approximately valid at 20–25°C, 50–60% RH).
    /// </summary>
    public const double NominalMoistAirDensity = 1.2;

    /// <summary>
    /// Nominal isobaric specific heat of moist air [J/(kg·K)] used as a
    /// representative value for building energy simulation
    /// (approximately valid at 24°C, 50% RH, humidity ratio ≈ 9.5 g/kg).
    /// </summary>
    public const double NominalMoistAirIsobaricSpecificHeat = 1005 + 1846 * 0.0095;

    /// <summary>
    /// Nominal density of liquid water [kg/m³] used as a representative value
    /// for building energy simulation (approximately valid for 5–60°C).
    /// </summary>
    public const double NominalWaterDensity = 997.0;

    /// <summary>
    /// Nominal isobaric specific heat of liquid water [J/(kg·K)] used as a
    /// representative value for building energy simulation (approximately valid for 5–60°C).
    /// </summary>
    public const double NominalWaterIsobaricSpecificHeat = 4182.0;

    #endregion

    #region staticメソッド

    /// <summary>
    /// Converts a temperature from Celsius [°C] to Kelvin [K].
    /// </summary>
    /// <param name="celsius">Temperature in Celsius [°C]</param>
    /// <returns>Temperature in Kelvin [K]</returns>
    public static double ToKelvin(double celsius) => celsius + CelsiusToKelvinOffset;

    /// <summary>
    /// Converts a temperature from Kelvin [K] to Celsius [°C].
    /// </summary>
    /// <param name="kelvin">Temperature in Kelvin [K]</param>
    /// <returns>Temperature in Celsius [°C]</returns>
    public static double ToCelsius(double kelvin) => kelvin - CelsiusToKelvinOffset;

    #endregion

  }
}
