/* PhysicsConstants.cs
 *
 * Copyright (C) 2026 E.Togashi
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

namespace Popolo.Core.Physics
{
  /// <summary>
  /// Provides physical constants and unit conversion utilities.
  /// </summary>
  public static class PhysicsConstants
  {

    #region Constant declarations

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
    /// representative value for building energy simulation.
    /// </summary>
    /// <remarks>
    /// 4186 J/(kg·K) is the established engineering convention in ASHRAE,
    /// Japanese HVAC handbooks, and major building simulation tools
    /// (EnergyPlus, DOE-2, BEST). The true value varies by ±0.2% over
    /// 5–60 °C (NIST IAPWS-IF97: 4179–4184 J/(kg·K)); 4186 is chosen
    /// for consistency with the engineering literature rather than
    /// exact thermodynamic minimization.
    /// </remarks>
    public const double NominalWaterIsobaricSpecificHeat = 4186.0;

    #endregion

    #region Static methods

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
