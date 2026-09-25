/* IVector.cs
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
