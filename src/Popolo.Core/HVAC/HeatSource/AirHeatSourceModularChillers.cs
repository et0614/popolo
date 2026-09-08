/* AirHeatSourceModularChillers.cs
 *
 * Copyright (C) 2016 E.Togashi
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 3 of the License, or (at
 * your option) any later version.
 *
 * This program is distributed in the hope that it will be useful, but
 * WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301, USA.
 */

using System;

namespace Popolo.Core.HVAC.HeatSource
{
  /// <summary>Obsolete alias of <see cref="SimpleModularAirSourceHeatPump"/>.</summary>
  /// <remarks>
  /// The class was renamed to align with the naming convention that distinguishes the model
  /// nature: the "Simple" prefix marks the performance-curve model (no refrigerant property),
  /// while the unprefixed name is reserved for the physics-based model.
  /// </remarks>
  [Obsolete("Renamed to SimpleModularAirSourceHeatPump. This alias will be removed in a future major version.")]
  public class AirHeatSourceModularChillers : SimpleModularAirSourceHeatPump, IReadOnlyAirHeatSourceModularChillers
  {

    /// <summary>Initializes a new heat-pump (cooling and heating) instance from rated conditions.</summary>
    public AirHeatSourceModularChillers(
      double coolingCapacity, double chilledWaterOutletTemperature, double chilledWaterFlowRate,
      double coolingAirTemperature, double coolingAirFlowRate, double coolingElectricity,
      double heatingCapacity, double hotWaterOutletTemperature, double hotWaterFlowRate,
      double heatingAirTemperature, double heatingAirFlowRate, double heatingElectricity,
      int unitCount, double auxiliaryElectricConsumption)
      : base(coolingCapacity, chilledWaterOutletTemperature, chilledWaterFlowRate,
          coolingAirTemperature, coolingAirFlowRate, coolingElectricity,
          heatingCapacity, hotWaterOutletTemperature, hotWaterFlowRate,
          heatingAirTemperature, heatingAirFlowRate, heatingElectricity,
          unitCount, auxiliaryElectricConsumption)
    { }

    /// <summary>Initializes a new cooling-only instance from rated conditions.</summary>
    public AirHeatSourceModularChillers(
      double coolingCapacity, double chilledWaterOutletTemperature, double chilledWaterFlowRate,
      double coolingAirTemperature, double coolingAirFlowRate, double coolingElectricity,
      int unitCount, double auxiliaryElectricConsumption)
      : base(coolingCapacity, chilledWaterOutletTemperature, chilledWaterFlowRate,
          coolingAirTemperature, coolingAirFlowRate, coolingElectricity,
          unitCount, auxiliaryElectricConsumption)
    { }

  }
}
