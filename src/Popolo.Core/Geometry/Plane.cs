/* Plane.cs
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
  /// <summary>3次元空間の平面を表すクラス</summary>
  public class Plane
  {
    /// <summary>ゼロとみなす誤差の閾値</summary>
    public const double EPSILON_TOL = 0.00001d;

    /// <summary>平面上の点を取得する</summary>
    public Point Point { get; }

    /// <summary>単位法線ベクトルを取得する</summary>
    public Vector3D NormalUnit { get; }

    /// <summary>平面の方程式 Ax+By+Cz+D=0 の係数Aを取得する</summary>
    public double A { get { return NormalUnit.X; } }

    /// <summary>平面の方程式 Ax+By+Cz+D=0 の係数Bを取得する</summary>
    public double B { get { return NormalUnit.Y; } }

    /// <summary>平面の方程式 Ax+By+Cz+D=0 の係数Cを取得する</summary>
    public double C { get { return NormalUnit.Z; } }

    /// <summary>平面の方程式 Ax+By+Cz+D=0 の定数項Dを取得する</summary>
    public double D { get; private set; }

    /// <summary>点と法線ベクトルからインスタンスを初期化する</summary>
    /// <param name="point">平面上の点</param>
    /// <param name="normal">法線ベクトル</param>
    public Plane(Point point, Vector3D normal)
    {
      Point = point;
      NormalUnit = Vector3D.GetUnitVector(normal);
      D = -(NormalUnit.X * point.X + NormalUnit.Y * point.Y + NormalUnit.Z * point.Z);
    }

    /// <summary>平面の方程式の係数からインスタンスを初期化する</summary>
    /// <param name="a">係数A</param>
    /// <param name="b">係数B</param>
    /// <param name="c">係数C</param>
    /// <param name="d">定数項D</param>
    /// <exception cref="PopoloArgumentException">
    /// a, b, c が全てゼロの場合（法線ベクトルがゼロ）。
    /// </exception>
    public Plane(double a, double b, double c, double d)
    {
      NormalUnit = new Vector3D(a, b, c);
      D = d;
      if (a != 0) Point = new Point(-d / a, 0, 0);
      else if (b != 0) Point = new Point(0, -d / b, 0);
      else if (c != 0) Point = new Point(0, 0, -d / c);
      else throw new PopoloArgumentException(
          "The normal vector (a, b, c) must not be zero.", nameof(a));
    }

    /// <summary>コピーコンストラクタ</summary>
    /// <param name="pln">コピーする平面</param>
    public Plane(Plane pln) : this(pln.Point, pln.NormalUnit) { }

    /// <summary>平面内に線を含んでいるか否か</summary>
    /// <param name="line">線</param>
    /// <returns>平面内に線を含んでいるか否か</returns>
    public bool Contains(Line line)
    {
      Vector3D vec = new Vector3D(
          line.Point.X - Point.X,
          line.Point.Y - Point.Y,
          line.Point.Z - Point.Z);
      return Math.Abs(NormalUnit.GetDot(vec)) < EPSILON_TOL;
    }

    /// <summary>線と交わっているか否か</summary>
    /// <param name="line">線</param>
    /// <returns>線と交わっているか否か</returns>
    public bool CrossedWith(Line line)
    {
      if (Point.X == line.Point.X &&
          Point.Y == line.Point.Y &&
          Point.Z == line.Point.Z) return true;
      if (Contains(line)) return true;
      return EPSILON_TOL <= Math.Abs(line.Vector.GetDot(NormalUnit));
    }

    /// <summary>直線と交差する点を求める</summary>
    /// <param name="line">直線</param>
    /// <returns>直線と交差する点。交わらない場合は null。</returns>
    public Point? GetCrossedPoint(Line line)
    {
      if (!CrossedWith(line)) return null;

      double t = -(A * line.Point.X + B * line.Point.Y + C * line.Point.Z + D)
          / (A * line.Vector.X + B * line.Vector.Y + C * line.Vector.Z);

      return new Point(
          line.Point.X + t * line.Vector.X,
          line.Point.Y + t * line.Vector.Y,
          line.Point.Z + t * line.Vector.Z);
    }
  }
}
