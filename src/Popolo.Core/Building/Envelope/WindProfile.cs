/* WindProfile.cs
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
 *
 * References:
 *   ASHRAE Handbook — Fundamentals (2021), Ch. 24.
 *     — two-terrain power-law form used by CorrectForHeight to translate
 *     the wind speed measured at a meteorological station to the wind
 *     speed at a building surface.
 */

using System;
using Popolo.Core.Climate;

namespace Popolo.Core.Building.Envelope
{
  /// <summary>
  /// Static helpers for the wind boundary-layer profile near a building.
  /// Translates a wind speed measured at one height/terrain (typically a
  /// meteorological anemometer) to the equivalent speed at another
  /// height/terrain (typically the mid-height of an exterior surface).
  /// </summary>
  public static class WindProfile
  {
    /// <summary>
    /// Adjusts a wind speed measured at one height/terrain to the equivalent
    /// wind speed at another height/terrain using the ASHRAE two-terrain
    /// power-law form (2021 Handbook—Fundamentals Ch. 24):
    /// <c>V_local = V_meteo · (δ_meteo/H_meteo)^a_meteo · (H_local/δ_local)^a_local</c>.
    /// </summary>
    /// <param name="meteoWindSpeed">Wind speed at the source (meteorological station) [m/s].</param>
    /// <param name="meteoHeight">Anemometer height at the station [m].</param>
    /// <param name="meteoTerrain">Terrain category of the station surroundings.</param>
    /// <param name="localHeight">Target (e.g., wall mid-) height above ground [m].</param>
    /// <param name="localTerrain">Terrain category at the target site.</param>
    /// <returns>Wind speed at the target height [m/s].</returns>
    public static double CorrectForHeight(
        double meteoWindSpeed, double meteoHeight, TerrainCategory meteoTerrain,
        double localHeight, TerrainCategory localTerrain)
    {
      if (meteoWindSpeed <= 0 || meteoHeight <= 0 || localHeight <= 0) return meteoWindSpeed;
      var (aMeteo, dMeteo) = meteoTerrain.GetParameters();
      var (aLocal, dLocal) = localTerrain.GetParameters();
      double factor = Math.Pow(dMeteo / meteoHeight, aMeteo) * Math.Pow(localHeight / dLocal, aLocal);
      return meteoWindSpeed * factor;
    }
  }
}
