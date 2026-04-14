/* IReadOnlyMultipleStratifiedWaterTank.cs
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
  /// <summary>Represents a read-only view of a thermally stratified water storage tank.</summary>
  public interface IReadOnlyMultipleStratifiedWaterTank
  {
    /// <summary>Gets the time step [s].</summary>
    double TimeStep { get; }

    /// <summary>Gets the water depth [m].</summary>
    double WaterDepth { get; }

    /// <summary>Gets the horizontal cross-sectional area [m²].</summary>
    double SectionalArea { get; }

    /// <summary>Gets the tank volume [m³].</summary>
    double WaterVolume { get; }

    /// <summary>Gets the inlet water temperature [°C].</summary>
    double WaterInletTemperature { get; }

    /// <summary>Gets the outlet water temperature at the top port [°C].</summary>
    double UpperOutletTemperarture { get; }

    /// <summary>Gets the outlet water temperature at the bottom port [°C].</summary>
    double LowerOutletTemperarture { get; }

    /// <summary>Gets the volumetric flow rate [m³/s].</summary>
    double WaterFlowRate { get; }

    /// <summary>Gets the port diameter [m].</summary>
    double PipeDiameter { get; }

    /// <summary>Gets the layer index of the inlet/outlet port.</summary>
    int PipeInstallationLayer { get; }

    /// <summary>Gets the overall heat loss coefficient [kW/K].</summary>
    double HeatLossCoefficient { get; }

    /// <summary>Gets the ambient temperature [°C].</summary>
    double AmbientTemperature { get; }

    /// <summary>True if the flow is directed downward.</summary>
    bool IsDownFlow { get; }

    /// <summary>Gets the number of layers.</summary>
    int LayerNumber { get; }

    /// <summary>Gets the temperature of the specified layer [°C].</summary>
    /// <param name="layerNumber">Zero-based layer index.</param>
    /// <returns>Layer temperature [°C].</returns>
    double GetTemperature(int layerNumber);

    /// <summary>Computes the stored heat [MJ] relative to a reference temperature (positive for hot, negative for cold storage).</summary>
    /// <param name="referenceTemperature">Reference temperature [°C].</param>
    /// <returns>Stored heat [MJ].</returns>
    double GetHeatStorage(double referenceTemperature);

    /// <summary>Computes the heat storage rate [kW].</summary>
    /// <returns>Heat storage rate [kW].</returns>
    double GetHeatStorageFlow();

  }
}
