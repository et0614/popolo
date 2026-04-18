/* IMatrix.cs
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
  /// <summary>行列インターフェース</summary>
  public interface IMatrix : IReadOnlyMatrix
  {
    /// <summary>要素の値を設定・取得する</summary>
    /// <param name="row">行番号</param>
    /// <param name="column">列番号</param>
    /// <returns>要素の値値</returns>
    new double this[int row, int column] { get; set; }

    /// <summary>初期化する</summary>
    /// <param name="val">初期化する値</param>
    void Initialize(double val);
  }

  /// <summary>読み取り専用行列インターフェース</summary>
  public interface IReadOnlyMatrix
  {
    /// <summary>行数を取得する</summary>
    int Rows { get; }

    /// <summary>列数を取得する</summary>
    int Columns { get; }

    /// <summary>要素の値を取得する</summary>
    /// <param name="row">行番号</param>
    /// <param name="column">列番号</param>
    /// <returns>要素の値値</returns>
    double this[int row, int column] { get; }
  }

}
