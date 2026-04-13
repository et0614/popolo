/* Vector.cs
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

  /// <summary>ベクトル</summary>
  [Serializable]
  public class Vector: IVector
  {
    /// <summary>ベクトル</summary>
    private double[] vector;

    /// <summary>ベクトル長を取得する</summary>
    public int Length { get { return vector.Length; } }

    /// <summary>要素の値を設定・取得する</summary>
    /// <param name="index">要素番号</param>
    /// <returns>要素の値</returns>
    public double this[int index]
    {
      get { return vector[index]; }
      set { vector[index] = value; }
    }

    /// <summary>コンストラクタ</summary>
    /// <param name="length">ベクトル長</param>
    public Vector(int length)
    { vector = new double[length]; }

    /// <summary>コンストラクタ</summary>
    /// <param name="data">データ</param>
    public Vector(double[] data)
    { vector = (double[])data.Clone(); }

    /// <summary>ユークリッドノルムを計算する</summary>
    /// <returns>ユークリッドノルム</returns>
    public double ComputeEuclideanNorm()
    { return new VectorView(this, 0).ComputeEuclideanNorm(); }

    /// <summary>初期化する</summary>
    /// <param name="val">初期化する値</param>
    public void Initialize(double val)
    { for (int i = 0; i < vector.Length; i++) vector[i] = val; }

    /// <summary>配列に変換する</summary>
    public double[] ToArray()
    {
      return (double[])vector.Clone();
    }

  }

}
