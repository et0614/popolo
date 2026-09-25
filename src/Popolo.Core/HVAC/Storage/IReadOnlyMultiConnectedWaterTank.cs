/* IReadOnlyMultiConnectedWaterTank.cs
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

namespace Popolo.Core.HVAC.Storage
{
  /// <summary>Represents a read-only view of a multi-tank series-connected fully-mixed thermal storage.</summary>
  public interface IReadOnlyMultiConnectedWaterTank
  {
    /// <summary>Gets the time step [s].</summary>
    double TimeStep { get; }

    /// <summary>Gets the inlet water temperature [°C].</summary>
    double WaterInletTemperature { get; }

    /// <summary>Gets the outlet water temperature [°C].</summary>
    double WaterOutletTemperarture { get; }

    /// <summary>Gets the water flow rate [m³/s].</summary>
    double WaterFlowRate { get; }

    /// <summary>Gets the ambient temperature [°C].</summary>
    double AmbientTemperature { get; }

    /// <summary>Gets a value indicating whether flow is in the forward direction.</summary>
    bool IsForwardFlow { get; }

    /// <summary>Gets the total number of tanks.</summary>
    int TankCount { get; }

    /// <summary>Gets the temperature of the first tank [°C].</summary>
    double FirstTankTemperature { get; }

    /// <summary>Gets the temperature of the last tank [°C].</summary>
    double LastTankTemperature { get; }

    /// <summary>Gets the tank temperature [°C].</summary>
    /// <param name="tankIndex">Zero-based tank index.</param>
    /// <returns>Tank temperature [°C].</returns>
    double GetTemperature(int tankIndex);

    /// <summary>Computes the stored heat [MJ] relative to a reference temperature (positive for hot, negative for cold storage).</summary>
    /// <param name="referenceTemperature">Reference temperature [°C].</param>
    /// <returns>Stored heat [MJ].</returns>
    double GetHeatStorage(double referenceTemperature);

    /// <summary>Computes the heat storage rate [kW].</summary>
    /// <returns>Heat storage rate [kW].</returns>
    double GetHeatStorageFlow();

    /// <summary>Gets the heat loss coefficient [kW/K] for the specified tank.</summary>
    /// <param name="tankIndex">Zero-based tank index.</param>
    /// <returns>Heat loss coefficient [kW/K].</returns>
    double GetHeatLossCoefficient(int tankIndex);
  }
}
