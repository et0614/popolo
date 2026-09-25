/* IReadOnlySimpleGroundHeatExchanger.cs
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
  /// <summary>Represents a read-only view of a simplified ground heat exchanger model.</summary>
  public interface IReadOnlySimpleGroundHeatExchanger
  {
    /// <summary>Gets the constant-temperature layer temperature [°C].</summary>
    double ConstantGroundTemperature { get; }

    /// <summary>Gets the near-field soil temperature (Tcnt) [°C].</summary>
    double NearGroundTemperature { get; }

    /// <summary>Gets the far-field soil temperature (Tfar) [°C].</summary>
    double DistantGroundTemperature { get; }

    /// <summary>Gets the heat transfer effectiveness of the ground heat exchanger (0–1) [-].</summary>
    double Effectiveness { get; }

    /// <summary>Gets the heat source fluid specific heat [kJ/(kg·K)].</summary>
    double FluidSpecificHeat { get; }

    /// <summary>Gets the heat conductance between near-field and far-field soil (Kcnt) [kW/K].</summary>
    double NearGroundHeatConductance { get; }

    /// <summary>Gets the heat conductance between far-field soil and constant-temperature layer (Kfar) [kW/K].</summary>
    double DistantGroundHeatConductance { get; }

    /// <summary>Gets the thermal capacitance of the near-field soil (Ccnt) [kJ/K].</summary>
    double NearGroundHeatCapacity { get; }

    /// <summary>Gets the thermal capacitance of the far-field soil (Cfar) [kJ/K].</summary>
    double DistantGroundHeatCapacity { get; }

    /// <summary>Gets the calculation time step [s].</summary>
    double TimeStep { get; }

    /// <summary>Gets the heat source fluid inlet temperature (Twi) [°C].</summary>
    double FluidInletTemperature { get; }

    /// <summary>Gets the heat source fluid outlet temperature (Two) [°C].</summary>
    double FluidOutletTemperature { get; }

    /// <summary>Gets the heat source fluid mass flow rate [kg/s].</summary>
    double FluidMassFlowRate { get; }

    /// <summary>Gets the heat exchange rate [kW] (positive = heat extraction, negative = heat rejection).</summary>
    double HeatExchange { get; }
  }

}
