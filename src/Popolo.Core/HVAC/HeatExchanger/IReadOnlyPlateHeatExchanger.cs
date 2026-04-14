/* IReadOnlyPlateHeatExchanger.cs
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

namespace Popolo.Core.HVAC.HeatExchanger
{
  /// <summary>Represents a read-only view of a plate heat exchanger.</summary>
  public interface IReadOnlyPlateHeatExchanger
  {
    /// <summary>Gets a value indicating whether the heat exchanger is overloaded.</summary>
    bool IsOverLoad { get; }

    /// <summary>Gets the overall heat transfer coefficient UA [kW/K].</summary>
    double HeatTransferCoefficient { get; }

    /// <summary>Gets the maximum allowable heat source flow rate [kg/s].</summary>
    double MaxHeatSourceFlowRate { get; }

    /// <summary>Gets the maximum allowable supply flow rate [kg/s].</summary>
    double MaxSupplyFlowRate { get; }

    /// <summary>Gets the current heat source flow rate [kg/s].</summary>
    double HeatSourceFlowRate { get; }

    /// <summary>Gets the current supply flow rate [kg/s].</summary>
    double SupplyFlowRate { get; }

    /// <summary>Gets the heat source inlet temperature [°C].</summary>
    double HeatSourceInletTemperature { get; }

    /// <summary>Gets the heat source outlet temperature [°C].</summary>
    double HeatSourceOutletTemperature { get; }

    /// <summary>Gets the supply temperature [°C].</summary>
    double SupplyTemperature { get; }

    /// <summary>Gets the return temperature [°C].</summary>
    double ReturnTemperature { get; }

    /// <summary>Gets the supply temperature setpoint [°C].</summary>
    double SupplyTemperatureSetpoint { get; }

    /// <summary>Gets the heat transfer rate [kW].</summary>
    double HeatTransfer { get; }
  }
}
