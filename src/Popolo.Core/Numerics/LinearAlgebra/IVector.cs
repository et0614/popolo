/* IVector.cs
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

namespace Popolo.Core.Numerics.LinearAlgebra
{
  /// <summary>Mutable vector interface.</summary>
  public interface IVector : IReadOnlyVector
  {
    /// <summary>Gets or sets the element at the specified index.</summary>
    /// <param name="index">Element index.</param>
    /// <returns>Element value.</returns>
    new double this[int index] { get; set; }

    /// <summary>Initializes all elements to the specified value.</summary>
    /// <param name="val">Value to assign to every element.</param>
    void Initialize(double val);
  }

  /// <summary>Read-only vector interface.</summary>
  public interface IReadOnlyVector
  {
    /// <summary>Gets the length (number of elements) of the vector.</summary>
    int Length { get; }

    /// <summary>Gets the element at the specified index.</summary>
    /// <param name="index">Element index.</param>
    /// <returns>Element value.</returns>
    double this[int index] { get; }

    /// <summary>Computes the Euclidean norm of the vector.</summary>
    /// <returns>Euclidean norm.</returns>
    double ComputeEuclideanNorm();
  }

}
