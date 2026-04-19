/* Vector.cs
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

  /// <summary>Dense vector backed by a one-dimensional array.</summary>
  [Serializable]
  public class Vector: IVector
  {
    /// <summary>Underlying element storage.</summary>
    private double[] vector;

    /// <summary>Gets the length (number of elements) of the vector.</summary>
    public int Length { get { return vector.Length; } }

    /// <summary>Gets or sets the element at the specified index.</summary>
    /// <param name="index">Element index.</param>
    /// <returns>Element value.</returns>
    public double this[int index]
    {
      get { return vector[index]; }
      set { vector[index] = value; }
    }

    /// <summary>Initializes a new instance with the specified length.</summary>
    /// <param name="length">Length of the vector.</param>
    public Vector(int length)
    { vector = new double[length]; }

    /// <summary>Initializes a new instance from the given array (copied).</summary>
    /// <param name="data">Source element array.</param>
    public Vector(double[] data)
    { vector = (double[])data.Clone(); }

    /// <summary>Computes the Euclidean norm of the vector.</summary>
    /// <returns>Euclidean norm.</returns>
    public double ComputeEuclideanNorm()
    { return new VectorView(this, 0).ComputeEuclideanNorm(); }

    /// <summary>Initializes all elements to the specified value.</summary>
    /// <param name="val">Value to assign to every element.</param>
    public void Initialize(double val)
    { for (int i = 0; i < vector.Length; i++) vector[i] = val; }

    /// <summary>Returns a copy of the underlying array.</summary>
    public double[] ToArray()
    {
      return (double[])vector.Clone();
    }

  }

}
