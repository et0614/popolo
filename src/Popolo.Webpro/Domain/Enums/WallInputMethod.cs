/* WallInputMethod.cs
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
  /// Method by which the thermal performance of a wall construction is
  /// specified in WEBPRO input data.
  /// </summary>
  public enum WallInputMethod
  {
    /// <summary>Unspecified; used when the enclosing JSON lacks an <c>inputMethod</c> property.</summary>
    None,
    /// <summary>Heat transfer coefficient (U-value) is given directly (熱貫流率を入力).</summary>
    HeatTransferCoefficient,
    /// <summary>Each layer's material ID and thickness are listed (建材構成を入力).</summary>
    MaterialNumberAndThickness,
    /// <summary>Insulation type is selected from a predefined set (断熱材種類を入力).</summary>
    InsulationType,
  }
}
