/* IReadOnlyModeParameters.cs
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

namespace Popolo.Core.HVAC.VRF
{
  /// <summary>Read-only view of mode-specific (cooling or heating) parameters and state of a VRF system.</summary>
  public interface IReadOnlyModeParameters
  {
    /// <summary>Gets the outdoor unit (acts as condenser in cooling mode, evaporator in heating mode).</summary>
    IReadOnlyVRFUnit OutdoorUnit { get; }

    /// <summary>Gets the nominal capacity [kW] (negative for cooling, positive for heating).</summary>
    double NominalCapacity { get; }

    /// <summary>Gets the nominal compression head [kW].</summary>
    double NominalHead { get; }

    /// <summary>Gets the pipe resistance coefficient [1/m].</summary>
    double PipeResistanceCoefficient { get; }

    /// <summary>Gets the compression head efficiency ratio at the nominal operating point [-].</summary>
    double NominalEfficiency { get; }

    /// <summary>Gets coefficient A of the compression head efficiency ratio characteristic curve [-].</summary>
    double HeadEfficiencyRatioCoefA { get; }

    /// <summary>Gets coefficient B of the compression head efficiency ratio characteristic curve [-].</summary>
    double HeadEfficiencyRatioCoefB { get; }

    /// <summary>Gets the electric power consumption at the nominal operating point [kW].</summary>
    double NominalElectricity { get; }

    /// <summary>Gets the maximum evaporating temperature [°C].</summary>
    /// <remarks>
    /// In cooling mode, this is the primary upper bound for the evaporating temperature.
    /// In heating mode, this is a secondary boundary used when solving the condensing temperature.
    /// </remarks>
    double MaxEvaporatingTemperature { get; }

    /// <summary>Gets the minimum evaporating temperature [°C].</summary>
    /// <remarks>In cooling mode, this is the primary lower bound for the evaporating temperature.</remarks>
    double MinEvaporatingTemperature { get; }

    /// <summary>Gets the maximum condensing temperature [°C].</summary>
    /// <remarks>In heating mode, this is the primary upper bound for the condensing temperature.</remarks>
    double MaxCondensingTemperature { get; }

    /// <summary>Gets the minimum condensing temperature [°C].</summary>
    /// <remarks>
    /// In heating mode, this is the primary lower bound for the condensing temperature.
    /// In cooling mode, this is a secondary boundary used when solving the evaporating temperature.
    /// </remarks>
    double MinCondensingTemperature { get; }
  }
}