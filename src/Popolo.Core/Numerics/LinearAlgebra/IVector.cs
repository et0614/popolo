/* IVector.cs
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

namespace Popolo.Core.Numerics.LinearAlgebra
{
  /// <summary>ベクトルインターフェース</summary>
  public interface IVector : IReadOnlyVector
  {
    /// <summary>要素の値を設定・取得する</summary>
    /// <param name="index">要素番号</param>
    /// <returns>要素の値</returns>
    new double this[int index] { get; set; }

    /// <summary>初期化する</summary>
    /// <param name="val">初期化する値</param>
    void Initialize(double val);
  }

  /// <summary>読み取り専用ベクトルインターフェース</summary>
  public interface IReadOnlyVector
  {
    /// <summary>ベクトル長を取得する</summary>
    int Length { get; }

    /// <summary>要素の値を取得する</summary>
    /// <param name="index">要素番号</param>
    /// <returns>要素の値</returns>
    double this[int index] { get; }

    /// <summary>ユークリッドノルムを計算する</summary>
    /// <returns>ユークリッドノルム</returns>
    double ComputeEuclideanNorm();
  }

}
