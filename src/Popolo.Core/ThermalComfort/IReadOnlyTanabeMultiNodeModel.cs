/* IReadOnlyTanabeMultiNodeModel.cs
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

namespace Popolo.Core.ThermalComfort
{
  /// <summary>Represents a read-only view of the Tanabe 65-node multi-segment thermoregulatory model.</summary>
  public interface IReadOnlyTanabeMultiNodeModel
  {
    #region Properties

    /// <summary>Gets the weight [kg].</summary>
    double Weight { get; }

    /// <summary>Gets the height [m].</summary>
    double Height { get; }

    /// <summary>Gets the age [years].</summary>
    double Age { get; }

    /// <summary>Gets a value indicating whether the occupant is male.</summary>
    bool IsMale { get; }

    /// <summary>Gets or sets a value indicating whether the occupant is standing.</summary>
    bool IsStanding { get; }

    /// <summary>Gets the body fat percentage [%].</summary>
    double FatPercentage { get; }

    /// <summary>Gets the total body surface area (Du Bois) [m²].</summary>
    double SurfaceArea { get; }

    /// <summary>Gets the central blood pool temperature [°C].</summary>
    double CentralBloodTemperature { get; }

    /// <summary>Gets the respiratory heat loss [W].</summary>
    double HeatLossByBreathing { get; }

    /// <summary>Gets the whole-body basal blood flow rate [mL/s].</summary>
    double BasalBloodFlow { get; }

    /// <summary>Gets the total whole-body blood flow rate [mL/s].</summary>
    double BloodFlow { get; }

    /// <summary>Gets the whole-body basal metabolic rate [W].</summary>
    double BasalMetabolicRate { get; }

    /// <summary>Gets the total whole-body metabolic rate [W].</summary>
    double MetabolicRate { get; }

    /// <summary>Gets the calculation time step [s].</summary>
    double TimeStep { get; }

    #endregion

    #region Methods

    /// <summary>Gets the air velocity [m/s] for the specified body segment.</summary>
    /// <param name="node">Body segment node.</param>
    /// <returns>Air velocity [m/s].</returns>
    double GetVelocity(TanabeMultiNodeModel.Node node);

    /// <summary>Gets the mean radiant temperature [°C] for the specified body segment.</summary>
    /// <param name="node">Body segment node.</param>
    /// <returns>Mean radiant temperature [°C].</returns>
    double GetMeanRadiantTemperature(TanabeMultiNodeModel.Node node);

    /// <summary>Gets the dry-bulb temperature [°C] for the specified body segment.</summary>
    /// <param name="node">Body segment node.</param>
    /// <returns>Dry-bulb temperature [°C].</returns>
    double GetDryBulbTemperature(TanabeMultiNodeModel.Node node);

    /// <summary>Gets the equivalent (operative) temperature [°C] for the specified body segment.</summary>
    /// <param name="node">Body segment node.</param>
    /// <returns>Equivalent temperature [°C].</returns>
    double GetOperatingTemperature(TanabeMultiNodeModel.Node node);

    /// <summary>Gets the relative humidity [%] for the specified body segment.</summary>
    /// <param name="node">Body segment node.</param>
    /// <returns>Relative humidity [%].</returns>
    double GetRelativeHumidity(TanabeMultiNodeModel.Node node);

    /// <summary>Gets the clothing insulation [clo] for the specified body segment.</summary>
    /// <param name="node">Body segment node.</param>
    /// <returns>Clothing insulation [clo].</returns>
    double GetClothingIndex(TanabeMultiNodeModel.Node node);

    /// <summary>Gets the thermal conductance between tissue layers [W/K].</summary>
    /// <param name="node">Body segment node.</param>
    /// <param name="layer1">First tissue layer.</param>
    /// <param name="layer2">Second tissue layer.</param>
    /// <returns>Thermal conductance between tissue layers [W/K].</returns>
    double GetHeatConductance(
      TanabeMultiNodeModel.Node node,
      TanabeMultiNodeModel.Layer layer1,
      TanabeMultiNodeModel.Layer layer2);

    /// <summary>Gets the metabolic rate of the specified tissue layer [W].</summary>
    /// <param name="node">Body segment node.</param>
    /// <param name="layer">Tissue layer.</param>
    /// <returns>Metabolic rate [W].</returns>
    double GetMetabolicRate(TanabeMultiNodeModel.Node node, TanabeMultiNodeModel.Layer layer);

    /// <summary>Gets the temperature of the specified tissue layer [°C].</summary>
    /// <param name="node">Body segment node.</param>
    /// <param name="layer">Tissue layer.</param>
    /// <returns>Temperature [°C].</returns>
    double GetTemperature(TanabeMultiNodeModel.Node node, TanabeMultiNodeModel.Layer layer);

    /// <summary>Gets the blood flow rate of the specified tissue layer [mL/s].</summary>
    /// <param name="node">Body segment node.</param>
    /// <param name="layer">Tissue layer.</param>
    /// <returns>Blood flow rate [mL/s].</returns>
    double GetBloodFlow(TanabeMultiNodeModel.Node node, TanabeMultiNodeModel.Layer layer);

    /// <summary>Gets the area-weighted mean skin temperature [°C].</summary>
    /// <returns>Mean skin temperature [°C].</returns>
    double GetAverageSkinTemperature();

    /// <summary>Gets the sensible heat loss from the skin surface of the specified segment [W].</summary>
    /// <param name="node">Body segment node.</param>
    /// <returns>Sensible heat loss [W].</returns>
    double GetSensibleHeatLoss(TanabeMultiNodeModel.Node node);

    /// <summary>Gets the latent heat loss from the skin surface of the specified segment [W].</summary>
    /// <param name="node">Body segment node.</param>
    /// <returns>Latent heat loss [W].</returns>
    double GetLatentHeatLoss(TanabeMultiNodeModel.Node node);

    #endregion
  }
}
