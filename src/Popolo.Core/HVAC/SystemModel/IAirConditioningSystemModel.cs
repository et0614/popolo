/* IAirConditioningSystemModel.cs
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

using Popolo.Core.Building;
using Popolo.Core.Physics;

namespace Popolo.Core.HVAC.SystemModel
{
  /// <summary>Secondary air-conditioning system.</summary>
  public interface IAirConditioningSystemModel: IReadOnlyAirConditioningSystemModel
  {
    /// <summary>Gets or sets the current date and time.</summary>
    new DateTime CurrentDateTime { get; set; }

    /// <summary>Gets or sets the simulation time step [s].</summary>
    new double TimeStep { get; set; }

    /// <summary>Gets or sets the outdoor air conditions.</summary>
    new IReadOnlyMoistAir OutdoorAir { get; set; }

    /// <summary>Fixes (commits) the forecast state as the current state.</summary>
    void FixState();
  }

  /// <summary>Read-only view of the secondary air-conditioning system.</summary>
  public interface IReadOnlyAirConditioningSystemModel
  {

    /// <summary>Gets the current date and time.</summary>
    DateTime CurrentDateTime { get; }

    /// <summary>Gets the simulation time step [s].</summary>
    double TimeStep { get; }

    /// <summary>Gets or sets the outdoor air conditions.</summary>
    IReadOnlyMoistAir OutdoorAir { get; }

    /// <summary>Gets the building thermal model associated with this air-conditioning system.</summary>
    IReadOnlyBuildingThermalModel BuildingThermalModel { get; }

    /// <summary>Gets the chilled water supply temperature [°C].</summary>
    double ChilledWaterSupplyTemperature { get; }

    /// <summary>Gets the chilled water return temperature [°C].</summary>
    double ChilledWaterReturnTemperature { get; }

    /// <summary>Gets the chilled water flow rate [kg/s].</summary>
    double ChilledWaterFlowRate { get; }

    /// <summary>Gets the hot water supply temperature [°C].</summary>
    double HotWaterSupplyTemperature { get; }

    /// <summary>Gets the hot water return temperature [°C].</summary>
    double HotWaterReturnTemperature { get; }

    /// <summary>Gets the hot water flow rate [kg/s].</summary>
    double HotWaterFlowRate { get; }

    /// <summary>Forecasts the return water temperatures for the given supply temperatures.</summary>
    /// <param name="chilledWaterSupplyTemperature">Chilled water supply temperature [°C].</param>
    /// <param name="hotWaterSupplyTemperature">Hot water supply temperature [°C].</param>
    void ForecastReturnWaterTemperature(double chilledWaterSupplyTemperature, double hotWaterSupplyTemperature);

  }

}
