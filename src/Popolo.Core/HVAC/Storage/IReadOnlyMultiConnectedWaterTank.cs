/* IReadOnlyMultiConnectedWaterTank.cs
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
