/* IMoistAir.cs
 *
 * Copyright (C) 2007 E.Togashi
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
