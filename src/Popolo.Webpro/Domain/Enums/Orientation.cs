/* Orientation.cs
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
  /// Surface orientation used by WEBPRO for wall and window directions.
  /// </summary>
  /// <remarks>
  /// The eight cardinal / intercardinal directions cover vertical surfaces,
  /// <see cref="UpperHorizontal"/> and <see cref="LowerHorizontal"/> represent
  /// roofs and floors respectively, and the remaining two values
  /// (<see cref="Shade"/>, <see cref="Horizontal"/>) appear in older
  /// WEBPRO versions.
  /// </remarks>
  public enum Orientation
  {
    /// <summary>North (北).</summary>
    N,
    /// <summary>Northwest (北西).</summary>
    NW,
    /// <summary>West (西).</summary>
    W,
    /// <summary>Southwest (南西).</summary>
    SW,
    /// <summary>South (南).</summary>
    S,
    /// <summary>Southeast (南東).</summary>
    SE,
    /// <summary>East (東).</summary>
    E,
    /// <summary>Northeast (北東).</summary>
    NE,
    /// <summary>Upper horizontal surface such as a roof (水平（上）).</summary>
    UpperHorizontal,
    /// <summary>Lower horizontal surface such as a floor (水平（下）).</summary>
    LowerHorizontal,
    /// <summary>Shaded surface (日陰); legacy value from older WEBPRO versions.</summary>
    Shade,
    /// <summary>Horizontal surface (水平); legacy value from older WEBPRO versions.</summary>
    Horizontal,
  }
}
