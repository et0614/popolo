/* MatrixView.cs
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

using Popolo.Exceptions;
using System;

namespace Popolo.Numerics.LinearAlgebra
{
  /// <summary>部分行列</summary>
  [Serializable]
  public class MatrixView : IMatrix
  {
    #region インスタンス変数

    /// <summary>もとの行列</summary>
    private IMatrix matrix;

    /// <summary>部分行列開始行番号</summary>
    private int rowStartNumber;

    /// <summary>部分行列開始列番号</summary>
    private int columnStartNumber;

    #endregion

    #region プロパティ

    /// <summary>行数を取得する</summary>
    public int Rows { get; private set; }

    /// <summary>列数を取得する</summary>
    public int Columns { get; private set; }

    /// <summary>要素の値を設定・取得する</summary>
    /// <param name="row">行番号</param>
    /// <param name="column">列番号</param>
    /// <returns>要素の値値</returns>
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

        return matrix[row + rowStartNumber, column + columnStartNumber];
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

        matrix[row + rowStartNumber, column + columnStartNumber] = value;
      }
    }

    #endregion

    /// <summary>インスタンスを初期化する</summary>
    /// <param name="matrix">もとの行列</param>
    /// <param name="rowSize">行数</param>
    /// <param name="columnSize">列数</param>
    /// <param name="rowStartNumber">部分行列開始行番号</param>
    /// <param name="columnStartNumber">部分行列開始列番号</param>
    public MatrixView(IMatrix matrix, int rowSize, int columnSize, int rowStartNumber, int columnStartNumber)
    {
      this.matrix = matrix;
      this.rowStartNumber = rowStartNumber;
      this.columnStartNumber = columnStartNumber;
      this.Rows = rowSize;
      this.Columns = columnSize;
    }

    /// <summary>初期化する</summary>
    /// <param name="val">初期化する値</param>
    public void Initialize(double val)
    {
      for (int i = 0; i < Rows; i++)
        for (int j = 0; j < Columns; j++)
          matrix[i + rowStartNumber, j + columnStartNumber] = val;
    }

  }
}
