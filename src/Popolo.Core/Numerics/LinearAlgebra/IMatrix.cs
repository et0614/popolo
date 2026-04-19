/* IMatrix.cs
 * 
 * Copyright (C) 2015 E.Togashi
 * 
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 3 of the License, or (at
 * your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful, but
 * WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301, USA.
 */

using System;

namespace Popolo.Core.Numerics.LinearAlgebra
{
  /// <summary>Mutable matrix interface.</summary>
  public interface IMatrix : IReadOnlyMatrix
  {
    /// <summary>Gets or sets the element at the specified row and column.</summary>
    /// <param name="row">Row index.</param>
    /// <param name="column">Column index.</param>
    /// <returns>Element value.</returns>
    new double this[int row, int column] { get; set; }

    /// <summary>Initializes all elements to the specified value.</summary>
    /// <param name="val">Value to assign to every element.</param>
    void Initialize(double val);
  }

  /// <summary>Read-only matrix interface.</summary>
  public interface IReadOnlyMatrix
  {
    /// <summary>Gets the number of rows.</summary>
    int Rows { get; }

    /// <summary>Gets the number of columns.</summary>
    int Columns { get; }

    /// <summary>Gets the element at the specified row and column.</summary>
    /// <param name="row">Row index.</param>
    /// <param name="column">Column index.</param>
    /// <returns>Element value.</returns>
    double this[int row, int column] { get; }
  }

}
