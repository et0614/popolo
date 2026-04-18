/* IReadOnlyIceOnCoilThermalStorage.cs
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
  /// <summary>Represents a read-only view of an internal-melt ice-on-coil thermal storage tank.</summary>
  public interface IReadOnlyIceOnCoilThermalStorage
  {
    /// <summary>Gets the time step [s].</summary>
    double TimeStep { get; }

    /// <summary>Gets the current ice state of the tank.</summary>
    IceOnCoilThermalStorage.IceState CurrentState { get; }

    /// <summary>Gets the total water volume of the tank [m³].</summary>
    double WaterVolume { get; }

    /// <summary>Gets the number of parallel coil branches.</summary>
    double NumberOfBranches { get; }

    /// <summary>Gets the length of a single coil branch [m].</summary>
    double BranchLength { get; }

    /// <summary>Gets the pipe inner diameter [m].</summary>
    double PipeInnerDiameter { get; }

    /// <summary>Gets the pipe wall thickness [m].</summary>
    double PipeThickness { get; }

    /// <summary>Gets the pipe outer diameter [m].</summary>
    double PipeOuterDiameter { get; }

    /// <summary>Gets a value indicating whether air bubbling (forced convection) is active.</summary>
    bool IsBubbling { get; }

    /// <summary>Gets the brine inlet temperature [°C].</summary>
    double InletBrineTemperature { get; }

    /// <summary>Gets the brine outlet temperature [°C].</summary>
    double OutletBrineTemperature { get; }

    /// <summary>Gets the brine mass flow rate [kg/s].</summary>
    double BrineFlowRate { get; }

    /// <summary>Gets the specific heat of brine [kJ/(kg·K)].</summary>
    double BrineSpecificHeat { get; }

    /// <summary>Gets the heat transfer to the coil [kW].
    /// Positive value: heat rejected to brine (melting); negative value: heat extracted from brine (ice making).</summary>
    double HeatTransferToCoil { get; }

    /// <summary>Gets the overall heat loss coefficient of the tank [W/K].</summary>
    double HeatLossCoefficient { get; }

    /// <summary>Gets the ambient temperature surrounding the tank [°C].</summary>
    double AmbientTemperature { get; }

    /// <summary>Gets the heat loss from the tank to the ambient [kW].</summary>
    double HeatLoss { get; }

    /// <summary>Gets the current ice packing factor (IPF) [-],
    /// defined as the ratio of total ice mass to total tank water mass.</summary>
    /// <returns>Ice packing factor [-].</returns>
    double GetIcePackingFactor();

    /// <summary>Gets the spatially averaged water/ice temperature across all coil segments [°C].</summary>
    /// <returns>Average water/ice temperature [°C].</returns>
    double GetAverageWaterIceTemperature();
  }
}
