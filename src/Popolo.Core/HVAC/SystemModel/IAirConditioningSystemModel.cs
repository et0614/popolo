/* IAirConditioningSystemModel.cs
 * 
 * Copyright (C) 2016 E.Togashi
 * 
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 2 of the License, or (at
 * your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful, but
 * WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 675 Mass Ave, Cambridge, MA 02139, USA.
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
