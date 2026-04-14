/* IReadOnlyGaggeModel.cs
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
