/* WeatherRecordExtensions.cs
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
