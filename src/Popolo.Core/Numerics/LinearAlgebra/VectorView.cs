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

  /// <summary>ベクトル</summary>
  [Serializable]
  public class VectorView : IVector
  {

    #region static変数

    /// <summary>取り扱える最大値の平方根</summary>
    private static double DGIANT = Math.Sqrt(double.MaxValue);

    /// <summary>取り扱える正の最小値の平方根</summary>
    private static double DDWARF = Math.Sqrt(double.Epsilon);

    #endregion

    #region インスタンス変数

    /// <summary>部分ベクトル開始番号</summary>
    private int startIndex;

    /// <summary>部分ベクトル開始行番号</summary>
    private int rowStartIndex;

    /// <summary>部分ベクトル開始列番号</summary>
    private int columnStartIndex;

    /// <summary>もとのデータは行列か否か</summary>
    private bool isOrginalMatrix;

    /// <summary>行方向の部分ベクトルか否か</summary>
    private bool isRowVector;

    /// <summary>もとのベクトル</summary>
    private IVector? vector;

    /// <summary>もとの行列</summary>
    private IMatrix? matrix;

    #endregion

    #region プロパティ

    /// <summary>ベクトル長を取得する</summary>
    public int Length { get; private set; }

    /// <summary>要素の値を設定・取得する</summary>
    /// <param name="index">要素番号</param>
    /// <returns>要素の値</returns>
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

    /// <summary>コンストラクタ</summary>
    /// <param name="vector">もとのベクトル</param>
    /// <param name="startIndex">部分ベクトル開始番号</param>
    /// <param name="length">ベクトル長</param>
    public VectorView(IVector vector, int startIndex, int length)
    {
      isOrginalMatrix = false;
      this.vector = vector;
      this.startIndex = startIndex;
      this.Length = length;
    }

    /// <summary>コンストラクタ</summary>
    /// <param name="vector">もとのベクトル</param>
    /// <param name="startIndex">部分ベクトル開始番号</param>
    public VectorView(IVector vector, int startIndex)
      : this(vector, startIndex, vector.Length - startIndex)
    { }

    /// <summary>コンストラクタ</summary>
    /// <param name="matrix">もとの行列</param>
    /// <param name="isRowVector">行ベクトルか否か</param>
    /// <param name="rowStartIndex">部分ベクトル開始行番号</param>
    /// <param name="columnStartIndex">部分ベクトル開始列番号</param>
    /// <param name="length">ベクトル長</param>
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

    /// <summary>コンストラクタ</summary>
    /// <param name="matrix">もとの行列</param>
    /// <param name="isRowVector">行ベクトルか否か</param>
    /// <param name="rowStartIndex">部分ベクトル開始行番号</param>
    /// <param name="columnStartIndex">部分ベクトル開始列番号</param>
    public VectorView(IMatrix matrix, bool isRowVector,
      int rowStartIndex, int columnStartIndex) :
        this(matrix, isRowVector, rowStartIndex, columnStartIndex, isRowVector ?
          matrix.Columns - columnStartIndex : matrix.Rows - rowStartIndex)
    { }

    #endregion

    #region インスタンスメソッド

    /// <summary>ユークリッドノルムを計算する</summary>
    /// <returns>ユークリッドノルム</returns>
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

    /// <summary>初期化する</summary>
    /// <param name="val">初期化する値</param>
    public void Initialize(double val)
    { for (int i = 0; i < Length; i++) this[i] = val; }

    #endregion

  }
}
