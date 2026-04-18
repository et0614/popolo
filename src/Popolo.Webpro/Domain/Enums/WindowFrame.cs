/* WindowFrame.cs
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
  /// Window frame material classification used by WEBPRO.
  /// </summary>
  public enum WindowFrame
  {
    /// <summary>Unspecified; used when the enclosing JSON lacks a <c>frameType</c> property.</summary>
    None,
    /// <summary>Resin frame (樹脂製).</summary>
    Resin,
    /// <summary>Wood frame (木製).</summary>
    Wood,
    /// <summary>Metal frame (金属製).</summary>
    Metal,
    /// <summary>Composite metal-resin frame (金属樹脂複合製).</summary>
    MetalAndResin,
    /// <summary>Composite metal-wood frame (金属木複合製).</summary>
    MetalAndWood,
  }
}
