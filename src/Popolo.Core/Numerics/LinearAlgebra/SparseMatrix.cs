/* SparseMatrix.cs
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
using System.Collections.Generic;
using Popolo.Core.Exceptions;

namespace Popolo.Core.Numerics.LinearAlgebra
{
  /// <summary>Sparse matrix storing non-zero elements in per-row dictionaries.</summary>
  [Serializable]
  public class SparseMatrix : IMatrix
  {

    #region インスタンス変数・プロパティ

    /// <summary>Per-row dictionaries that hold the non-zero elements.</summary>
    private Dictionary<int, double>[] elem;

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
      get
      {
        if (row < Rows)
        {
          if (column < Columns && elem[row].ContainsKey(column)) return elem[row][column];
          else return 0;
        }
        else return 0;
      }
      set
      {
        if (row < Rows && column < Columns)
        {
          if (value == 0.0) elem[row].Remove(column);
          else elem[row][column] = value;
        }
      }
    }

    #endregion

    #region コンストラクタ

    /// <summary>Initializes a new instance with the specified dimensions.</summary>
    /// <param name="rows">Number of rows.</param>
    /// <param name="columns">Number of columns.</param>
    public SparseMatrix(int rows, int columns)
    {
      Rows = rows;
      Columns = columns;
      elem = new Dictionary<int, double>[Rows];
      for (int i = 0; i < Rows; i++) elem[i] = new Dictionary<int, double>();
    }

    #endregion

    #region インスタンスメソッド

    /// <summary>Clears all non-zero elements (only zero initialization is supported).</summary>
    /// <param name="val">Initialization value. Must be zero.</param>
    /// <remarks>Initializing a sparse matrix with a non-zero value is not meaningful.</remarks>
    public void Initialize(double val)
    {
      if (val == 0.0)
        for (int i = 0; i < Rows; i++) elem[i].Clear();
      else throw new PopoloArgumentException(
        "invalid value for initialization of SparseMatrix", 
        nameof(val));
    }

    /// <summary>Computes the matrix-vector product and stores the result in <paramref name="vec2"/>.</summary>
    /// <param name="vec1">Input vector.</param>
    /// <param name="vec2">Output vector that receives the product.</param>
    public void Multiply(IVector vec1, ref IVector vec2)
    {
      vec2.Initialize(0);
      for (int i = 0; i < Rows; i++)
      {
        Dictionary<int, double> row = elem[i];
        foreach (int col in row.Keys) vec2[i] += row[col] * vec1[col];
      }
    }

    /// <summary>Computes the product of the transposed matrix with the input vector.</summary>
    /// <param name="vec1">Input vector.</param>
    /// <param name="vec2">Output vector that receives the product.</param>
    public void MultiplyTransposed(IVector vec1, ref IVector vec2)
    {
      vec2.Initialize(0);
      for (int i = 0; i < Rows; i++)
      {
        Dictionary<int, double> row = elem[i];
        foreach (int col in row.Keys) vec2[col] += row[col] * vec1[i];
      }
    }

    /// <summary>Solves the linear system Ax = b for x.</summary>
    /// <param name="vecB">Right-hand side vector b.</param>
    /// <param name="vecX">Output solution vector x.</param>
    public void SolveLinearEquation(IVector vecB, ref IVector vecX)
    {
      IVector ap = new Vector(Rows);
      IVector app = new Vector(Rows);
      IVector p = new Vector(Rows);
      IVector pp = new Vector(Rows);
      IVector r = new Vector(Rows);
      IVector rr = new Vector(Rows);

      //初回の残差ベクトルを設定
      double bnrm = 0;
      double rnrm = 0;
      Multiply(vecX, ref r);
      for (int i = 0; i < Rows; i++)
      {
        r[i] = rr[i] = p[i] = pp[i] = vecB[i] - r[i];
        bnrm += vecB[i] * vecB[i];
        rnrm += r[i] * rr[i];
      }

      //収束計算開始
      int maxIter = 10 * Rows;
      for (int iter = 0; iter < maxIter; iter++)
      {
        Multiply(p, ref ap);
        MultiplyTransposed(pp, ref app);
        double apnrm = 0;
        for (int i = 0; i < Rows; i++) apnrm += ap[i] * pp[i];
        double ak = rnrm / apnrm;
        for (int i = 0; i < Rows; i++)
        {
          vecX[i] += ak * p[i];
          r[i] -= ak * ap[i];
          rr[i] -= ak * app[i];
        }

        //収束判定
        Multiply(vecX, ref ap);
        double err = 0;
        for (int i = 0; i < Rows; i++)
        {
          ap[i] = vecB[i] - ap[i];
          err += ap[i] * ap[i];
        }
        if (err / bnrm < 1e-10) return;
        double rnrmbf = rnrm;
        rnrm = 0;
        for (int i = 0; i < Rows; i++) rnrm += r[i] * rr[i];
        double bk = rnrm / rnrmbf;
        for (int i = 0; i < Rows; i++)
        {
          p[i] = r[i] + bk * p[i];
          pp[i] = rr[i] + bk * pp[i];
        }
      }
      throw new PopoloNumericalException(
        "SolveLinearEquation",
        "Iteration error in SparseMatrix");
    }

    /// <summary>Replaces this matrix in place with its inverse.</summary>
    /// <remarks>Uses the Gauss-Jordan elimination method.</remarks>
    public void ConvertToInverseMatrix()
    {
      //Pivot配列初期化
      int[] pivot = new int[Rows];
      for (int i = 0; i < pivot.Length; i++) pivot[i] = i;

      //各行の規準化係数を計算
      double[] rate = new double[Rows];
      for (int i = 0; i < Rows; i++)
      {
        double big = 0.0;
        Dictionary<int, double> row = elem[i];
        foreach (int key in row.Keys) big = Math.Max(big, Math.Abs(row[key]));
        if (big == 0.0) throw new PopoloNumericalException(
          "ConvertToInverseMatrix",
          $"Singular matrix detected at row {i}. All elements in the row are zero.");

        rate[i] = 1.0 / big;
      }

      for (int i = 0; i < Rows; i++)
      {
        //対角要素最大の行を特定//Pivotting        
        int prNum = pivot[i];
        double big = Math.Abs(this[prNum, i] * rate[prNum]);
        int tgtJ = i;
        for (int j = i + 1; j < Rows; j++)
        {
          double diag = Math.Abs(this[pivot[j], i] * rate[pivot[j]]);
          if (big < diag)
          {
            big = diag;
            tgtJ = j;
          }
        }
        prNum = pivot[tgtJ];
        pivot[tgtJ] = pivot[i];
        pivot[i] = prNum;

        //Pivot行を対角要素で除する
        Dictionary<int, double> pRow = elem[prNum];
        double inv = 1d / pRow[i];
        pRow[i] = 1.0;
        Dictionary<int, double> nRow = new Dictionary<int, double>();
        foreach (int key in pRow.Keys) nRow.Add(key, pRow[key] * inv);
        elem[prNum] = pRow = nRow;

        //他の行を処理
        for (int j = 0; j < Rows; j++)
        {
          if (i != j)
          {
            Dictionary<int, double> tRow = elem[pivot[j]];
            if (tRow.ContainsKey(i))
            {
              double bf = tRow[i];
              tRow.Remove(i);
              foreach (int key in pRow.Keys)
              {
                if (tRow.ContainsKey(key)) tRow[key] -= pRow[key] * bf;
                else tRow.Add(key, -pRow[key] * bf);
              }
            }
          }
        }
      }

      //行列入替
      Dictionary<int, double> bfRow;
      for (int i = 0; i < Rows; i++)
      {
        int tgt = pivot[i];
        if (i != tgt)
        {
          bfRow = elem[i];
          elem[i] = elem[tgt];
          elem[tgt] = bfRow;

          int tgt2 = Array.IndexOf(pivot, i);
          for (int j = 0; j < Rows; j++)
          {
            double bf = this[j, i];
            this[j, i] = this[j, tgt2];
            this[j, tgt2] = bf;
          }
          pivot[i] = i;
          pivot[tgt2] = tgt;
        }
      }
    }

    #endregion

  }
}
