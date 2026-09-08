/* AirHeatSourceModularChillersSystem.cs
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
