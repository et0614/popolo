/* Vector.cs
 * 
 * Copyright (C) 2015 E.Togashi
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
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
