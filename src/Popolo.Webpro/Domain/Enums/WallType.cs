/* WallType.cs
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

namespace Popolo.Webpro.Domain.Enums
{
  /// <summary>
  /// Wall type classification used by WEBPRO to determine the boundary-condition
  /// treatment of a wall surface.
  /// </summary>
  public enum WallType
  {
    /// <summary>Sun-exposed external wall (日の当たる外壁).</summary>
    ExternalWall,
    /// <summary>Shaded external wall (日の当たらない外壁).</summary>
    ShadingExternalWall,
    /// <summary>Ground-contact external wall (地盤に接する外壁).</summary>
    GroundWall,
    /// <summary>Internal wall between zones (内壁).</summary>
    InnerWall,
  }
}
