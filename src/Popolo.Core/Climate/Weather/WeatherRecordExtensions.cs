/* WeatherRecordExtensions.cs
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

using Popolo.Core.Exceptions;
using Popolo.Core.Physics;

namespace Popolo.Core.Climate.Weather
{
  /// <summary>
  /// Convenience extensions for <see cref="WeatherRecord"/>.
  /// </summary>
  public static class WeatherRecordExtensions
  {
    /// <summary>
    /// Builds a <see cref="MoistAir"/> instance from this record.
    /// </summary>
    /// <param name="record">The weather record.</param>
    /// <param name="fallbackAtmosphericPressure">
    /// Atmospheric pressure [kPa] to use when
    /// <see cref="WeatherField.AtmosphericPressure"/> is not recorded on the
    /// record. Defaults to
    /// <see cref="PhysicsConstants.StandardAtmosphericPressure"/>.
    /// </param>
    /// <returns>
    /// A new <see cref="MoistAir"/> initialized from the record's dry-bulb
    /// temperature, humidity ratio (converted from g/kg to kg/kg), and
    /// atmospheric pressure.
    /// </returns>
    /// <exception cref="PopoloInvalidOperationException">
    /// Thrown when the record does not have both
    /// <see cref="WeatherField.DryBulbTemperature"/> and
    /// <see cref="WeatherField.HumidityRatio"/> recorded.
    /// </exception>
    public static MoistAir ToMoistAir(
        this WeatherRecord record,
        double fallbackAtmosphericPressure = PhysicsConstants.StandardAtmosphericPressure)
    {
      if (!record.Has(WeatherField.DryBulbTemperature | WeatherField.HumidityRatio))
      {
        throw new PopoloInvalidOperationException(
            "WeatherRecord must have DryBulbTemperature and HumidityRatio "
            + "recorded to build a MoistAir instance.");
      }

      double pressure = record.Has(WeatherField.AtmosphericPressure)
          ? record.AtmosphericPressure
          : fallbackAtmosphericPressure;

      // g/kg(DA) → kg/kg(DA)
      double humidityRatioKgKg = record.HumidityRatio * 1.0e-3;

      return new MoistAir(record.DryBulbTemperature, humidityRatioKgKg, pressure);
    }
  }
}
