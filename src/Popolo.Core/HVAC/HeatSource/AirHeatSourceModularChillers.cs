/* AirHeatSourceModularChillers.cs
 *
 * Copyright (C) 2016 E.Togashi
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
