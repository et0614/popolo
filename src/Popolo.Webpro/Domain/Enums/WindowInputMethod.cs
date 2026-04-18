/* WindowInputMethod.cs
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
  /// Method by which the thermal performance of a window is specified in WEBPRO
  /// input data.
  /// </summary>
  public enum WindowInputMethod
  {
    /// <summary>Unspecified; used when the enclosing JSON lacks an <c>inputMethod</c> property.</summary>
    None,
    /// <summary>The window's overall U-value and solar heat gain rate are given (性能値を入力).</summary>
    WindowSpec,
    /// <summary>The frame type is chosen and the glazing's U-value and solar heat gain rate are given (ガラスの性能を入力).</summary>
    FrameTypeAndGlazingSpec,
    /// <summary>Both the frame type and the glazing type are chosen from a predefined catalog (ガラスの種類を入力).</summary>
    FrameAndGlazingType,
  }
}
