/* IReadOnlyGaggeModel.cs
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
  /// <summary>Represents a read-only view of the Gagge two-node thermoregulatory model.</summary>
  public interface IReadOnlyGaggeModel
  {
    /// <summary>Gets the age of the occupant [years].</summary>
    uint Age { get; }

    /// <summary>Gets the height [m].</summary>
    double Height { get; }

    /// <summary>Gets the weight [kg].</summary>
    double Weight { get; }

    /// <summary>Gets the basal metabolic rate [W/m²].</summary>
    double BasalMetabolism { get; }

    /// <summary>Gets the skin temperature [°C].</summary>
    double SkinTemperature { get; }

    /// <summary>Gets the core (rectal) temperature [°C].</summary>
    double CoreTemperature { get; }

    /// <summary>Gets the mean body temperature [°C].</summary>
    double BodyTemperature { get; }

    /// <summary>Gets the clothing surface temperature [°C].</summary>
    double ClothTemperature { get; }

    /// <summary>Gets the sensible heat loss from skin [W/m²].</summary>
    double SensibleHeatLossFromSkin { get; }

    /// <summary>Gets the latent heat loss from skin [W/m²].</summary>
    double LatentHeatLossFromSkin { get; }

    /// <summary>Gets the sensible heat loss by respiration [W/m²].</summary>
    double SensibleHeatLossByRespiration { get; }

    /// <summary>Gets the latent heat loss by respiration [W/m²].</summary>
    double LatentHeatLossByRespiration { get; }

    /// <summary>Gets the mean skin wettedness [-].</summary>
    double Wettedness { get; }

    /// <summary>Gets the body surface area (Du Bois) [m²].</summary>
    double BodySurface { get; }

    /// <summary>Gets the normal skin blood flow rate [mL/(m²·s)].</summary>
    double NormalBloodFlow { get; }
  }
}
