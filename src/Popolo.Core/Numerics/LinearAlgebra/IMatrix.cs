/* IMatrix.cs
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
