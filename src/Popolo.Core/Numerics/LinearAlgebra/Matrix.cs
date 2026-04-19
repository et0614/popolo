/* Matrix.cs
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
  /// <summary>Dense matrix backed by a jagged array.</summary>
  [Serializable]
  public class Matrix : IMatrix
  {

    /// <summary>Underlying element storage.</summary>
    private double[][] matrix;

    /// <summary>Gets the number of rows.</summary>
    public int Rows { get; private set; }

    /// <summary>Gets the number of columns.</summary>
    public int Columns { get; private set; }

    /// <summary>Gets or sets the element at the specified row and column.</summary>
    /// <param name="row">Row index.</param>
    /// <param name="column">Column index.</param>
    /// <returns>Element value.</returns>
    public double this[int row, int column]
    {
      get { return matrix[row][column]; }
      set { matrix[row][column] = value; }
    }

    /// <summary>Initializes a new instance with the specified dimensions.</summary>
    /// <param name="rowSize">Number of rows.</param>
    /// <param name="columnSize">Number of columns.</param>
    public Matrix(int rowSize, int columnSize)
    {
      Rows = rowSize;
      Columns = columnSize;
      matrix = new double[Rows][];
      for (int i = 0; i < Rows; i++) matrix[i] = new double[Columns];
    }

    /// <summary>Initializes a new instance from the given jagged array (copied).</summary>
    /// <param name="matrix">Source matrix data.</param>
    public Matrix(double[][] matrix)
    {
      Rows = matrix.Length;
      Columns = matrix[0].Length;
      this.matrix = new double[Rows][];
      for (int i = 0; i < Rows; i++)
      {
        this.matrix[i] = new double[Columns];
        Array.Copy(matrix[i], this.matrix[i], Columns);
      }
    }

    /// <summary>Initializes all elements to the specified value.</summary>
    /// <param name="val">Value to assign to every element.</param>
    public void Initialize(double val)
    {
      for (int i = 0; i < Rows; i++)
        for (int j = 0; j < Columns; j++)
          matrix[i][j] = val;
    }

    /// <summary>Copies the contents of this matrix to <paramref name="mat"/>.</summary>
    /// <param name="mat">Destination matrix.</param>
    public void CopyTo(IMatrix mat)
    {
      for (int i = 0; i < Rows; i++)
        for (int j = 0; j < Columns; j++)
          mat[i, j] = this[i, j];
    }

    /// <summary>Returns the transpose of this matrix.</summary>
    /// <returns>Transposed matrix.</returns>
    public Matrix MakeTranspose()
    {
      Matrix transposed = new Matrix(Columns, Rows);
      for (int i = 0; i < Rows; i++)
        for (int j = 0; j < Columns; j++)
          transposed[j, i] = this[i, j];
      return transposed;
    }

    /// <summary>Returns the matrix contents as a two-dimensional jagged array.</summary>
    public double[][] ToArray()
    {
      double[][] array = new double[Rows][];
      for (int i = 0; i < Rows; i++)
      {
        array[i] = new double[Columns];
        Array.Copy(matrix[i], array[i], Columns);
      }
      return array;
    }

  }
}
