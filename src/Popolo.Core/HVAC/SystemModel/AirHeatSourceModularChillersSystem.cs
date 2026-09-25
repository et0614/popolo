/* AirHeatSourceModularChillersSystem.cs
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

using Popolo.Core.HVAC.HeatSource;
using Popolo.Core.HVAC.FluidCircuit;

namespace Popolo.Core.HVAC.SystemModel
{
  /// <summary>Obsolete alias of <see cref="SimpleModularAirSourceHeatPumpSystem"/>.</summary>
  [Obsolete("Renamed to SimpleModularAirSourceHeatPumpSystem. This alias will be removed in a future major version.")]
  public class AirHeatSourceModularChillersSystem : SimpleModularAirSourceHeatPumpSystem
  {

    /// <summary>Gets the air-heat-source modular chiller (alias of <see cref="SimpleModularAirSourceHeatPumpSystem.SimpleModularAirSourceHeatPump"/>).</summary>
    public IReadOnlySimpleModularAirSourceHeatPump AirHeatSourceModularChillers
    { get { return SimpleModularAirSourceHeatPump; } }

    /// <summary>Initializes a new instance.</summary>
    public AirHeatSourceModularChillersSystem
      (SimpleModularAirSourceHeatPump mChiller, CentrifugalPump chwPump, CentrifugalPump hwPump, int count)
      : base(mChiller, chwPump, hwPump, count)
    { }

  }
}
