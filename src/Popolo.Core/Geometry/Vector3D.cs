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
  /// <summary>Represents a vector in three-dimensional space.</summary>
  public class Vector3D
  {

    #region 定数

    /// <summary>Tolerance below which a value is treated as zero.</summary>
    public const double GeometryTolerance = 0.00001d;

    #endregion

    #region プロパティ

    /// <summary>Gets the X component.</summary>
    public double X { get; private set; }

    /// <summary>Gets the Y component.</summary>
    public double Y { get; private set; }

    /// <summary>Gets the Z component.</summary>
    public double Z { get; private set; }

    /// <summary>Gets the length (magnitude) of the vector.</summary>
    public double Length { get; private set; }

    #endregion

    #region コンストラクタ

    /// <summary>Initializes a new instance.</summary>
    /// <param name="x">X component.</param>
    /// <param name="y">Y component.</param>
    /// <param name="z">Z component.</param>
    public Vector3D(double x, double y, double z)
    {
      X = x;
      Y = y;
      Z = z;
      Length = GetLength(this);
    }

    /// <summary>Copy constructor.</summary>
    /// <param name="vector">Source vector to copy.</param>
    public Vector3D(Vector3D vector)
    {
      X = vector.X;
      Y = vector.Y;
      Z = vector.Z;
      Length = vector.Length;
    }

    #endregion

    #region 静的メソッド

    /// <summary>Returns the length (magnitude) of a vector.</summary>
    /// <param name="vector">Input vector.</param>
    /// <returns>Length of the vector.</returns>
    public static double GetLength(Vector3D vector)
    {
      return Math.Sqrt(
          vector.X * vector.X +
          vector.Y * vector.Y +
          vector.Z * vector.Z);
    }

    /// <summary>Returns the normalized (unit) vector.</summary>
    /// <param name="vec">Vector to normalize.</param>
    /// <returns>Unit vector in the same direction as <paramref name="vec"/>.</returns>
    /// <exception cref="PopoloArgumentException">
    /// Thrown when the length of <paramref name="vec"/> is zero.
    /// </exception>
    public static Vector3D GetUnitVector(Vector3D vec)
    {
      if (vec.Length < GeometryTolerance)
        throw new PopoloArgumentException(
            "Cannot normalize a zero-length vector.", nameof(vec));

      return new Vector3D(
          vec.X / vec.Length,
          vec.Y / vec.Length,
          vec.Z / vec.Length);
    }

    #endregion

    #region インスタンスメソッド

    /// <summary>Returns the dot product with another vector.</summary>
    /// <param name="vector">The other vector.</param>
    /// <returns>Dot product.</returns>
    public double GetDot(Vector3D vector)
    {
      return X * vector.X + Y * vector.Y + Z * vector.Z;
    }

    /// <summary>Returns the cross product with another vector.</summary>
    /// <param name="vector">The other vector.</param>
    /// <returns>Cross-product vector.</returns>
    public Vector3D GetCross(Vector3D vector)
    {
      return new Vector3D(
          Y * vector.Z - Z * vector.Y,
          Z * vector.X - X * vector.Z,
          X * vector.Y - Y * vector.X);
    }

    /// <summary>Normalizes this vector to unit length.</summary>
    /// <exception cref="PopoloArgumentException">
    /// Thrown when the length of the vector is zero.
    /// </exception>
    public void Normalize()
    {
      if (Length < GeometryTolerance)
        throw new PopoloArgumentException(
            "Cannot normalize a zero-length vector.", "this");

      X /= Length;
      Y /= Length;
      Z /= Length;
      Length = 1.0;
    }

    #endregion

    #region 演算子

    /// <summary>Vector addition.</summary>
    public static Vector3D operator +(Vector3D vec1, Vector3D vec2)
    {
      return new Vector3D(vec1.X + vec2.X, vec1.Y + vec2.Y, vec1.Z + vec2.Z);
    }

    /// <summary>Vector subtraction.</summary>
    public static Vector3D operator -(Vector3D vec1, Vector3D vec2)
    {
      return new Vector3D(vec1.X - vec2.X, vec1.Y - vec2.Y, vec1.Z - vec2.Z);
    }

    /// <summary>Equality comparison (components are considered equal if their differences are below <see cref="GeometryTolerance"/>).</summary>
    public override bool Equals(object? obj)
    {
      if (obj == null || GetType() != obj.GetType()) return false;
      Vector3D tgt = (Vector3D)obj;
      return
          Math.Abs(tgt.X - X) < GeometryTolerance &&
          Math.Abs(tgt.Y - Y) < GeometryTolerance &&
          Math.Abs(tgt.Z - Z) < GeometryTolerance;
    }

    /// <summary>Returns a hash code for the vector.</summary>
    public override int GetHashCode()
    {
      return HashCode.Combine(
          Math.Round(X / GeometryTolerance),
          Math.Round(Y / GeometryTolerance),
          Math.Round(Z / GeometryTolerance));
    }

    #endregion

  }
}
