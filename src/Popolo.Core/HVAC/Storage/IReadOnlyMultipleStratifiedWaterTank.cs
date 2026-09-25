/* IReadOnlyMultipleStratifiedWaterTank.cs
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
    int LayerCount { get; }

    /// <summary>Gets the temperature of the specified layer [°C].</summary>
    /// <param name="layerCount">Zero-based layer index.</param>
    /// <returns>Layer temperature [°C].</returns>
    double GetTemperature(int layerCount);

    /// <summary>Computes the stored heat [MJ] relative to a reference temperature (positive for hot, negative for cold storage).</summary>
    /// <param name="referenceTemperature">Reference temperature [°C].</param>
    /// <returns>Stored heat [MJ].</returns>
    double GetHeatStorage(double referenceTemperature);

    /// <summary>Computes the heat storage rate [kW].</summary>
    /// <returns>Heat storage rate [kW].</returns>
    double GetHeatStorageFlow();

  }
}
