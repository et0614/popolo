/* TerrainCategory.cs
 *
 * Copyright (C) 2026 E.Togashi
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 3 of the License, or (at
 * your option) any later version.
 *
 * This program is distributed in the hope that it will be useful, but
 * WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
 * General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301, USA.
 */

using System;

namespace Popolo.Core.Climate
{
  /// <summary>
  /// Atmospheric boundary-layer terrain category used for the wind-speed power-law
  /// height correction <c>V(z) = V_ref · (z/z_ref)^a</c>.
  /// </summary>
  /// <remarks>
  /// Categories follow 2021 ASHRAE Handbook—Fundamentals Ch. 24, Table 1
  /// (also referenced by ASHRAE Standard 140-2023 §7.2.1.1.2). Each category
  /// carries a wind-shear exponent a [-] and boundary-layer thickness δ [m].
  /// The two-terrain ASHRAE form
  /// <c>V_local = V_meteo · (δ_meteo/H_meteo)^a_meteo · (H_local/δ_local)^a_local</c>
  /// is recovered by combining the meteorological-station and building-site
  /// categories.
  /// </remarks>
  public enum TerrainCategory
  {
    /// <summary>
    /// Large city centers with at least 50% of buildings taller than 21 m,
    /// over a distance of at least 0.8 km upwind. a = 0.33, δ = 460 m.
    /// </summary>
    LargeCity,

    /// <summary>
    /// Urban and suburban areas, wooded terrain, or other terrain with
    /// numerous closely spaced obstructions the size of single-family
    /// dwellings or larger. a = 0.22, δ = 370 m.
    /// </summary>
    Suburban,

    /// <summary>
    /// Open terrain with scattered obstructions having heights generally
    /// less than 9 m, including flat open country typical of meteorological
    /// station surroundings. a = 0.14, δ = 270 m. ASHRAE Std 140-2023
    /// §7.2.1.1.2.2 specifies this category for the BESTEST site.
    /// </summary>
    OpenTerrain,

    /// <summary>
    /// Flat unobstructed areas exposed to wind flowing over open water for at
    /// least 1.6 km. a = 0.10, δ = 210 m.
    /// </summary>
    OpenSea,
  }

  /// <summary>Extension helpers for <see cref="TerrainCategory"/>.</summary>
  public static class TerrainCategoryExtensions
  {
    /// <summary>
    /// Returns the wind-shear exponent <c>a</c> [-] and boundary-layer
    /// thickness <c>δ</c> [m] for the given terrain category.
    /// </summary>
    public static (double Alpha, double Delta) GetParameters(this TerrainCategory category) => category switch
    {
      TerrainCategory.LargeCity   => (0.33, 460.0),
      TerrainCategory.Suburban    => (0.22, 370.0),
      TerrainCategory.OpenTerrain => (0.14, 270.0),
      TerrainCategory.OpenSea     => (0.10, 210.0),
      _ => throw new ArgumentOutOfRangeException(nameof(category), category, null),
    };
  }
}
