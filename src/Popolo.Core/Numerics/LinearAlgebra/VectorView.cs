/* VectorView.cs
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
using Popolo.Core.Exceptions;

namespace Popolo.Core.Numerics.LinearAlgebra
{

  /// <summary>A view of a contiguous slice of a <see cref="IVector"/> or a row/column of a <see cref="IMatrix"/>.</summary>
  [Serializable]
  public class VectorView : IVector
  {

    #region static変数

    /// <summary>Square root of the maximum representable value (used for scaling).</summary>
    private static double DGIANT = Math.Sqrt(double.MaxValue);

    /// <summary>Square root of the smallest positive representable value (used for scaling).</summary>
    private static double DDWARF = Math.Sqrt(double.Epsilon);

    #endregion

    #region インスタンス変数

    /// <summary>Start index of the subvector within the source vector.</summary>
    private int startIndex;

    /// <summary>Starting row index when the source is a matrix.</summary>
    private int rowStartIndex;

    /// <summary>Starting column index when the source is a matrix.</summary>
    private int columnStartIndex;

    /// <summary>True if the underlying source is a matrix.</summary>
    private bool isOrginalMatrix;

    /// <summary>True if this view represents a row of the source matrix; false for a column.</summary>
    private bool isRowVector;

    /// <summary>Underlying source vector (when backed by a vector).</summary>
    private IVector? vector;

    /// <summary>Underlying source matrix (when backed by a matrix).</summary>
    private IMatrix? matrix;

    #endregion

    #region プロパティ

    /// <summary>Gets the length (number of elements) of the view.</summary>
    public int Length { get; private set; }

    /// <summary>Gets or sets the element at the specified index.</summary>
    /// <param name="index">Element index.</param>
    /// <returns>Element value.</returns>
    public double this[int index]
    {
      get
      {
        if (index < 0 || Length <= index) throw new PopoloArgumentException(
          $"Index {index} is out of range. Vector length is {Length}.",
          nameof(index));

        if (isOrginalMatrix)
        {
          if (isRowVector) return matrix![rowStartIndex, columnStartIndex + index];
          else return matrix![rowStartIndex + index, columnStartIndex];
        }
        else return vector![index + startIndex];
      }
      set
      {
        if (index < 0 || Length <= index) throw new PopoloArgumentException(
          $"Index {index} is out of range. Vector length is {Length}.",
          nameof(index));

        if (isOrginalMatrix)
        {
          if (isRowVector) matrix![rowStartIndex, columnStartIndex + index] = value;
          else matrix![rowStartIndex + index, columnStartIndex] = value;
        }
        else vector![index + startIndex] = value;
      }
    }

    #endregion

    #region コンストラクタ

    /// <summary>Initializes a view over a contiguous slice of a vector.</summary>
    /// <param name="vector">Source vector.</param>
    /// <param name="startIndex">Starting index of the slice.</param>
    /// <param name="length">Length of the view.</param>
    public VectorView(IVector vector, int startIndex, int length)
    {
      isOrginalMatrix = false;
      this.vector = vector;
      this.startIndex = startIndex;
      this.Length = length;
    }

    /// <summary>Initializes a view that extends from <paramref name="startIndex"/> to the end of the vector.</summary>
    /// <param name="vector">Source vector.</param>
    /// <param name="startIndex">Starting index of the slice.</param>
    public VectorView(IVector vector, int startIndex)
      : this(vector, startIndex, vector.Length - startIndex)
    { }

    /// <summary>Initializes a view over a row or column of a matrix with an explicit length.</summary>
    /// <param name="matrix">Source matrix.</param>
    /// <param name="isRowVector">True to view a row; false to view a column.</param>
    /// <param name="rowStartIndex">Starting row index.</param>
    /// <param name="columnStartIndex">Starting column index.</param>
    /// <param name="length">Length of the view.</param>
    public VectorView(IMatrix matrix, bool isRowVector,
      int rowStartIndex, int columnStartIndex, int length)
    {
      isOrginalMatrix = true;
      this.matrix = matrix;
      this.isRowVector = isRowVector;
      this.rowStartIndex = rowStartIndex;
      this.columnStartIndex = columnStartIndex;
      this.Length = length;
    }

    /// <summary>Initializes a view that extends from the starting index to the end of the row or column.</summary>
    /// <param name="matrix">Source matrix.</param>
    /// <param name="isRowVector">True to view a row; false to view a column.</param>
    /// <param name="rowStartIndex">Starting row index.</param>
    /// <param name="columnStartIndex">Starting column index.</param>
    public VectorView(IMatrix matrix, bool isRowVector,
      int rowStartIndex, int columnStartIndex) :
        this(matrix, isRowVector, rowStartIndex, columnStartIndex, isRowVector ?
          matrix.Columns - columnStartIndex : matrix.Rows - rowStartIndex)
    { }

    #endregion

    #region インスタンスメソッド

    /// <summary>Computes the Euclidean norm of the view, using a numerically stable scaling.</summary>
    /// <returns>Euclidean norm.</returns>
    public double ComputeEuclideanNorm()
    {
      double aGiant = DGIANT / Length;   //1要素あたりの最大値
      double sN, sG, sD, maxG, maxD;
      sN = sG = sD = maxG = maxD = 0;

      for (int i = 0; i < Length; i++)
      {
        double abs = Math.Abs(this[i]);

        //値を確認して計算可能な範囲になるように拡大縮小する
        //計算可能な大きさの場合
        if (DDWARF < abs && abs < aGiant) sN += abs * abs;
        //小さすぎる場合
        else if (abs <= DDWARF)
        {
          if (abs <= maxD)
          {
            if (abs != 0.0d) sD += Math.Pow(abs / maxD, 2);
          }
          else
          {
            sD = 1 + sD * Math.Pow(maxD / abs, 2);
            maxD = abs;
          }
        }
        //大きすぎる場合
        else
        {
          if (abs <= maxG)
          {
            sG += Math.Pow(abs / maxG, 2);
          }
          else
          {
            sG = 1 + sG * Math.Pow(maxG / abs, 2);
            maxG = abs;
          }
        }
      }

      //maxGとmaxDの2乗の計算が発生しないように計算順序を調整
      if (sG != 0.0d) return maxG * Math.Sqrt(sG + (sN / maxG) / maxG);
      else if (sN == 0) return maxD * Math.Sqrt(sD);
      else
      {
        if (maxD <= sN) return Math.Sqrt(sN * (1 + (maxD / sN) * (maxD * sD)));
        else return Math.Sqrt(maxD * ((sN / maxD) + (maxD * sD)));
      }
    }

    /// <summary>Initializes all elements in the view to the specified value.</summary>
    /// <param name="val">Value to assign to every element.</param>
    public void Initialize(double val)
    { for (int i = 0; i < Length; i++) this[i] = val; }

    #endregion

  }
}
