/* IReadOnlyMultiZoneCO2Model.cs
 * Copyright (C) 2026 E.Togashi
 * Licensed under the Apache License, Version 2.0 - see the accompanying LICENSE file.
 */

using System.Collections.Generic;

namespace Popolo.Core.Building.AirQuality
{
  /// <summary>Read-only view of a <see cref="MultiZoneCO2Model"/>.</summary>
  public interface IReadOnlyMultiZoneCO2Model
  {
    /// <summary>Gets the zones of the model.</summary>
    IReadOnlyList<IReadOnlyCO2ModelZone> Zones { get; }

    /// <summary>Gets the outdoor CO2 concentration [m³/m³].</summary>
    double OutdoorCO2Level { get; }

    /// <summary>Gets the outdoor CO2 concentration [ppm].</summary>
    double OutdoorCO2Level_PPM { get; }
  }
}
