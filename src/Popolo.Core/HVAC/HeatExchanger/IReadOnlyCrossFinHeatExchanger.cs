/* IReadOnlyCrossFinHeatExchanger.cs
 * Copyright (C) 2026 E.Togashi
 * Licensed under the Apache License, Version 2.0 - see the accompanying LICENSE file.
 */

using System;

namespace Popolo.Core.HVAC.HeatExchanger
{
  /// <summary>Obsolete alias of <see cref="IReadOnlyAirToWaterCrossFinHeatExchanger"/>.</summary>
  [Obsolete("Renamed to IReadOnlyAirToWaterCrossFinHeatExchanger. This alias will be removed in a future major version.")]
  public interface IReadOnlyCrossFinHeatExchanger : IReadOnlyAirToWaterCrossFinHeatExchanger
  { }
}
