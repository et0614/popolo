/* WindOrientation.cs
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
  /// Orientation of an exterior surface relative to the local wind vector,
  /// used to select the appropriate forced-convection correlation in the
  /// outdoor convective heat transfer coefficient calculations
  /// (<c>Popolo.Core.Building.Envelope.ExteriorConvection</c>).
  /// </summary>
  /// <remarks>
  /// The two regimes correspond to fundamentally different boundary-layer
  /// flow patterns (impingement vs. wake/recirculation) and use distinct
  /// fitted constants in the MoWiTT correlation. The discrete switch
  /// reflects that the underlying separation-driven physics is not well
  /// represented by continuous interpolation across the surface-normal /
  /// wind angle.
  /// </remarks>
  public enum WindOrientation
  {
    /// <summary>
    /// Wind blows toward the surface (component of wind velocity along the
    /// outward surface normal is positive). Higher convective coefficient.
    /// </summary>
    Windward,

    /// <summary>
    /// Wind blows away from the surface (surface lies in the wake / leeward
    /// recirculation zone). Lower convective coefficient.
    /// </summary>
    Leeward,
  }
}
