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
using Popolo.Exceptions;

namespace Popolo.Numerics.LinearAlgebra
{
  /// <summary>疎行列</summary>
  [Serializable]
  public class SparseMatrix : IMatrix
  {

    #region インスタンス変数・プロパティ

    /// <summary>非0要素を格納する連想配列</summary>
    private Dictionary<int, double>[] elem;

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

    /// <summary>インスタンスを初期化する</summary>
    /// <param name="rows">行数</param>
    /// <param name="columns">列数</param>
    public SparseMatrix(int rows, int columns)
    {
      Rows = rows;
      Columns = columns;
      elem = new Dictionary<int, double>[Rows];
      for (int i = 0; i < Rows; i++) elem[i] = new Dictionary<int, double>();
    }

    #endregion

    #region インスタンスメソッド

    /// <summary>初期化する</summary>
    /// <param name="val">初期化する値</param>
    /// <remarks>疎行列で0以外での初期化は無意味</remarks>
    public void Initialize(double val)
    {
      if (val == 0.0)
        for (int i = 0; i < Rows; i++) elem[i].Clear();
      else throw new PopoloArgumentException(
        "invalid value for initialization of SparseMatrix", 
        nameof(val));
    }

    /// <summary>入力ベクトルとの積を計算して出力ベクトルに格納する</summary>
    /// <param name="vec1">入力ベクトル</param>
    /// <param name="vec2">出力：出力ベクトル</param>
    public void Multiplicate(IVector vec1, ref IVector vec2)
    {
      vec2.Initialize(0);
      for (int i = 0; i < Rows; i++)
      {
        Dictionary<int, double> row = elem[i];
        foreach (int col in row.Keys) vec2[i] += row[col] * vec1[col];
      }
    }

    /// <summary>転置行列と入力ベクトルとの積を計算して出力ベクトルに格納する</summary>
    /// <param name="vec1">入力ベクトル</param>
    /// <param name="vec2">出力：出力ベクトル</param>
    public void MultiplicateTranspose(IVector vec1, ref IVector vec2)
    {
      vec2.Initialize(0);
      for (int i = 0; i < Rows; i++)
      {
        Dictionary<int, double> row = elem[i];
        foreach (int col in row.Keys) vec2[col] += row[col] * vec1[i];
      }
    }

    /// <summary>連立一次方程式Ax=bの解xを出力する</summary>
    /// <param name="vecB">右辺ベクトルb</param>
    /// <param name="vecX">出力:変数ベクトルx</param>
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
      Multiplicate(vecX, ref r);
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
        Multiplicate(p, ref ap);
        MultiplicateTranspose(pp, ref app);
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
        Multiplicate(vecX, ref ap);
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

    /// <summary>逆行列に変換する</summary>
    /// <remarks>Gauss Jordan法</remarks>
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
