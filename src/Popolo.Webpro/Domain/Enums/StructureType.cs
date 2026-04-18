/* StructureType.cs
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
  /// Structural classification of a building or wall configuration used by WEBPRO.
  /// </summary>
  public enum StructureType
  {
    /// <summary>Unspecified; used when the enclosing JSON lacks a <c>structureType</c> property.</summary>
    None,
    /// <summary>Wood construction (木造).</summary>
    Wood,
    /// <summary>Reinforced concrete construction and similar (鉄筋コンクリート造等).</summary>
    ReinforcedConcrete,
    /// <summary>Steel frame construction (鉄骨造).</summary>
    Steel,
    /// <summary>Other construction methods (その他).</summary>
    Others,
  }
}
