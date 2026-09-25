/* MatrixView.cs
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

using Popolo.Core.Exceptions;
using System;

namespace Popolo.Core.Numerics.LinearAlgebra
{
  /// <summary>A view over a rectangular block of an <see cref="IMatrix"/>.</summary>
  [Serializable]
  public class MatrixView : IMatrix
  {
    #region Instance variables

    /// <summary>Underlying source matrix.</summary>
    private IMatrix matrix;

    /// <summary>Starting row index of the submatrix.</summary>
    private int rowStartIndex;

    /// <summary>Starting column index of the submatrix.</summary>
    private int columnStartIndex;

    #endregion

    #region Properties

    /// <summary>Gets the number of rows in the view.</summary>
    public int Rows { get; private set; }

    /// <summary>Gets the number of columns in the view.</summary>
    public int Columns { get; private set; }

    /// <summary>Gets or sets the element at the specified row and column.</summary>
    /// <param name="row">Row index within the view.</param>
    /// <param name="column">Column index within the view.</param>
    /// <returns>Element value.</returns>
    public double this[int row, int column]
    {
      get
      {
        if (row < 0 || Rows <= row)
          throw new PopoloArgumentException(
              $"Row index {row} is out of range. Valid range is 0 to {Rows - 1}.",
              nameof(row));
        if (column < 0 || Columns <= column)
          throw new PopoloArgumentException(
              $"Column index {column} is out of range. Valid range is 0 to {Columns - 1}.",
              nameof(column));

        return matrix[row + rowStartIndex, column + columnStartIndex];
      }
      set
      {
        if (row < 0 || Rows <= row)
          throw new PopoloArgumentException(
              $"Row index {row} is out of range. Valid range is 0 to {Rows - 1}.",
              nameof(row));
        if (column < 0 || Columns <= column)
          throw new PopoloArgumentException(
              $"Column index {column} is out of range. Valid range is 0 to {Columns - 1}.",
              nameof(column));

        matrix[row + rowStartIndex, column + columnStartIndex] = value;
      }
    }

    #endregion

    /// <summary>Initializes a view over a rectangular block of a matrix.</summary>
    /// <param name="matrix">Source matrix.</param>
    /// <param name="rowSize">Number of rows in the view.</param>
    /// <param name="columnSize">Number of columns in the view.</param>
    /// <param name="rowStartIndex">Starting row index within the source matrix.</param>
    /// <param name="columnStartIndex">Starting column index within the source matrix.</param>
    public MatrixView(IMatrix matrix, int rowSize, int columnSize, int rowStartIndex, int columnStartIndex)
    {
      this.matrix = matrix;
      this.rowStartIndex = rowStartIndex;
      this.columnStartIndex = columnStartIndex;
      this.Rows = rowSize;
      this.Columns = columnSize;
    }

    /// <summary>Initializes all elements in the view to the specified value.</summary>
    /// <param name="val">Value to assign to every element.</param>
    public void Initialize(double val)
    {
      for (int i = 0; i < Rows; i++)
        for (int j = 0; j < Columns; j++)
          matrix[i + rowStartIndex, j + columnStartIndex] = val;
    }

  }
}
