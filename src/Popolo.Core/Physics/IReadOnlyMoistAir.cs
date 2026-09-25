/* IMoistAir.cs
 *
 * Copyright (C) 2007 E.Togashi
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
  /// Represents a read-only view of moist air thermodynamic state.
  /// </summary>
  public interface IReadOnlyMoistAir
  {
    /// <summary>Gets the atmospheric pressure [kPa].</summary>
    double AtmosphericPressure { get; }

    /// <summary>Gets the dry-bulb temperature [°C].</summary>
    double DryBulbTemperature { get; }

    /// <summary>Gets the wet-bulb temperature [°C].</summary>
    double WetBulbTemperature { get; }

    /// <summary>Gets the humidity ratio [kg/kg(DA)].</summary>
    double HumidityRatio { get; }

    /// <summary>Gets the relative humidity [%].</summary>
    double RelativeHumidity { get; }

    /// <summary>Gets the specific enthalpy [kJ/kg].</summary>
    double Enthalpy { get; }

    /// <summary>Gets the specific volume [m³/kg].</summary>
    double SpecificVolume { get; }

    /// <summary>
    /// Copies this moist air state to the specified <see cref="MoistAir"/> instance.
    /// </summary>
    /// <param name="destination">The destination moist air instance.</param>
    void CopyTo(MoistAir destination);
  }
}
