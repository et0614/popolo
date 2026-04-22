/* SkyDiffuseModel.cs
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

namespace Popolo.Core.Climate
{
  /// <summary>
  /// Selects how sky diffuse irradiance is projected onto a tilted surface.
  /// </summary>
  /// <remarks>
  /// Ground-reflected diffuse is always treated as isotropic regardless of
  /// the chosen sky model, following the usual convention in the Perez
  /// literature and ASHRAE practice.
  /// </remarks>
  public enum SkyDiffuseModel
  {
    /// <summary>
    /// Perez all-weather anisotropic sky model
    /// (Perez, R. et al., <em>Solar Energy</em>, Vol. 44, 1990). Accounts
    /// for circumsolar brightening and horizon brightening in addition to
    /// the isotropic dome component.
    /// </summary>
    Perez,

    /// <summary>
    /// Isotropic sky: diffuse irradiance arrives uniformly from every point
    /// on the celestial hemisphere. The tilted-surface irradiance is simply
    /// the view factor to the sky times the horizontal diffuse irradiance.
    /// </summary>
    Isotropic,
  }
}
