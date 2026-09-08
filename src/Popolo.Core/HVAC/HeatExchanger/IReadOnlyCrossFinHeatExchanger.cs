/* IReadOnlyCrossFinHeatExchanger.cs
 * Copyright (C) 2026 E.Togashi
 * GNU General Public License v3
 */

using System;

namespace Popolo.Core.HVAC.HeatExchanger
{
  /// <summary>Obsolete alias of <see cref="IReadOnlyAirToWaterCrossFinHeatExchanger"/>.</summary>
  [Obsolete("Renamed to IReadOnlyAirToWaterCrossFinHeatExchanger. This alias will be removed in a future major version.")]
  public interface IReadOnlyCrossFinHeatExchanger : IReadOnlyAirToWaterCrossFinHeatExchanger
  { }
}
