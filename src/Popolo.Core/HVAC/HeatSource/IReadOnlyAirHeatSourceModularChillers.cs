/* IReadOnlyAirHeatSourceModularChillers.cs
 * Copyright (C) 2026 E.Togashi
 * GNU General Public License v3
 */

using System;

namespace Popolo.Core.HVAC.HeatSource
{
  /// <summary>Obsolete alias of <see cref="IReadOnlySimpleModularAirSourceHeatPump"/>.</summary>
  [Obsolete("Renamed to IReadOnlySimpleModularAirSourceHeatPump. This alias will be removed in a future major version.")]
  public interface IReadOnlyAirHeatSourceModularChillers : IReadOnlySimpleModularAirSourceHeatPump
  { }
}
