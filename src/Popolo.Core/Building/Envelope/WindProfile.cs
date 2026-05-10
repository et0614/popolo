/* WindProfile.cs
 *
 * Copyright (C) 2026 E.Togashi
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 2 of the License, or (at
 * your option) any later version.
 *
 * This program is distributed in the hope that it will be useful, but
 * WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
 * General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 675 Mass Ave, Cambridge, MA 02139, USA.
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
