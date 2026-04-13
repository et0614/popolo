/* Vector3D.cs
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
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
 * General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301, USA.
 */

using System;
using Popolo.Core.Exceptions;

namespace Popolo.Core.Geometry
{
  /// <summary>3次元空間のベクトルを表すクラス</summary>
  public class Vector3D
  {

    #region 定数

    /// <summary>ゼロとみなす誤差の閾値</summary>
    public const double EPSILON_TOL = 0.00001d;

    #endregion

    #region プロパティ

    /// <summary>X成分を取得する</summary>
    public double X { get; private set; }

    /// <summary>Y成分を取得する</summary>
    public double Y { get; private set; }

    /// <summary>Z成分を取得する</summary>
    public double Z { get; private set; }

    /// <summary>ベクトルの長さを取得する</summary>
    public double Length { get; private set; }

    #endregion

    #region コンストラクタ

    /// <summary>インスタンスを初期化する</summary>
    /// <param name="x">X成分</param>
    /// <param name="y">Y成分</param>
    /// <param name="z">Z成分</param>
    public Vector3D(double x, double y, double z)
    {
      X = x;
      Y = y;
      Z = z;
      Length = GetLength(this);
    }

    /// <summary>コピーコンストラクタ</summary>
    /// <param name="vector">コピー元のベクトル</param>
    public Vector3D(Vector3D vector)
    {
      X = vector.X;
      Y = vector.Y;
      Z = vector.Z;
      Length = vector.Length;
    }

    #endregion

    #region 静的メソッド

    /// <summary>ベクトルの長さを求める</summary>
    /// <param name="vector">ベクトル</param>
    /// <returns>ベクトルの長さ</returns>
    public static double GetLength(Vector3D vector)
    {
      return Math.Sqrt(
          vector.X * vector.X +
          vector.Y * vector.Y +
          vector.Z * vector.Z);
    }

    /// <summary>正規化した単位ベクトルを取得する</summary>
    /// <param name="vec">正規化するベクトル</param>
    /// <returns>正規化した単位ベクトル</returns>
    /// <exception cref="PopoloArgumentException">
    /// vec の長さがゼロの場合。
    /// </exception>
    public static Vector3D GetUnitVector(Vector3D vec)
    {
      if (vec.Length < EPSILON_TOL)
        throw new PopoloArgumentException(
            "Cannot normalize a zero-length vector.", nameof(vec));

      return new Vector3D(
          vec.X / vec.Length,
          vec.Y / vec.Length,
          vec.Z / vec.Length);
    }

    #endregion

    #region インスタンスメソッド

    /// <summary>他のベクトルとの内積を取得する</summary>
    /// <param name="vector">他のベクトル</param>
    /// <returns>内積</returns>
    public double GetDot(Vector3D vector)
    {
      return X * vector.X + Y * vector.Y + Z * vector.Z;
    }

    /// <summary>他のベクトルとの外積を取得する</summary>
    /// <param name="vector">他のベクトル</param>
    /// <returns>外積ベクトル</returns>
    public Vector3D GetCross(Vector3D vector)
    {
      return new Vector3D(
          Y * vector.Z - Z * vector.Y,
          Z * vector.X - X * vector.Z,
          X * vector.Y - Y * vector.X);
    }

    /// <summary>自身を単位ベクトルに正規化する</summary>
    /// <exception cref="PopoloArgumentException">
    /// ベクトルの長さがゼロの場合。
    /// </exception>
    public void Normalize()
    {
      if (Length < EPSILON_TOL)
        throw new PopoloArgumentException(
            "Cannot normalize a zero-length vector.", "this");

      X /= Length;
      Y /= Length;
      Z /= Length;
      Length = 1.0;
    }

    #endregion

    #region 演算子

    /// <summary>ベクトルの加算</summary>
    public static Vector3D operator +(Vector3D vec1, Vector3D vec2)
    {
      return new Vector3D(vec1.X + vec2.X, vec1.Y + vec2.Y, vec1.Z + vec2.Z);
    }

    /// <summary>ベクトルの減算</summary>
    public static Vector3D operator -(Vector3D vec1, Vector3D vec2)
    {
      return new Vector3D(vec1.X - vec2.X, vec1.Y - vec2.Y, vec1.Z - vec2.Z);
    }

    /// <summary>等値比較（各成分の誤差が EPSILON_TOL 未満のとき等しいとみなす）</summary>
    public override bool Equals(object? obj)
    {
      if (obj == null || GetType() != obj.GetType()) return false;
      Vector3D tgt = (Vector3D)obj;
      return
          Math.Abs(tgt.X - X) < EPSILON_TOL &&
          Math.Abs(tgt.Y - Y) < EPSILON_TOL &&
          Math.Abs(tgt.Z - Z) < EPSILON_TOL;
    }

    /// <summary>ハッシュコードを取得する</summary>
    public override int GetHashCode()
    {
      return HashCode.Combine(
          Math.Round(X / EPSILON_TOL),
          Math.Round(Y / EPSILON_TOL),
          Math.Round(Z / EPSILON_TOL));
    }

    #endregion

  }
}
