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

namespace Popolo.Numerics.LinearAlgebra
{
  /// <summary>行列</summary>
  [Serializable]
  public class Matrix : IMatrix
  {

    /// <summary>行列</summary>
    private double[][] matrix;

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
      get { return matrix[row][column]; }
      set { matrix[row][column] = value; }
    }

    /// <summary>コンストラクタ</summary>
    /// <param name="rowSize">行数</param>
    /// <param name="columnSize">列数</param>
    public Matrix(int rowSize, int columnSize)
    {
      Rows = rowSize;
      Columns = columnSize;
      matrix = new double[Rows][];
      for (int i = 0; i < Rows; i++) matrix[i] = new double[Columns];
    }

    /// <summary>コンストラクタ</summary>
    /// <param name="matrix">行列データ</param>
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

    /// <summary>初期化する</summary>
    /// <param name="val">初期化する値</param>
    public void Initialize(double val)
    {
      for (int i = 0; i < Rows; i++)
        for (int j = 0; j < Columns; j++)
          matrix[i][j] = val;
    }

    /// <summary>行列の中身をmatにコピーする</summary>
    /// <param name="mat">コピー対象の行列</param>
    public void CopyTo(IMatrix mat)
    {
      for (int i = 0; i < Rows; i++)
        for (int j = 0; j < Columns; j++)
          mat[i, j] = this[i, j];
    }

    /// <summary>転置行列を作る</summary>
    /// <returns>転置行列</returns>
    public Matrix MakeTranspose()
    {
      Matrix transposed = new Matrix(Columns, Rows);
      for (int i = 0; i < Rows; i++)
        for (int j = 0; j < Columns; j++)
          transposed[j, i] = this[i, j];
      return transposed;
    }

    /// <summary>行列を2次元配列に変換する</summary>
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
