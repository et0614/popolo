/* Point.cs
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
using Popolo.Core.Numerics;

namespace Popolo.Core.Geometry
{
  /// <summary>3次元空間の点を表すクラス</summary>
  public class Point
  {
    /// <summary>X座標を取得する</summary>
    public double X { get; private set; }

    /// <summary>Y座標を取得する</summary>
    public double Y { get; private set; }

    /// <summary>Z座標を取得する</summary>
    public double Z { get; private set; }

    /// <summary>インスタンスを初期化する</summary>
    /// <param name="x">X座標</param>
    /// <param name="y">Y座標</param>
    /// <param name="z">Z座標</param>
    public Point(double x, double y, double z)
    {
      X = x;
      Y = y;
      Z = z;
    }

    /// <summary>コピーコンストラクタ</summary>
    /// <param name="point">コピー元の点</param>
    public Point(Point point)
    {
      X = point.X;
      Y = point.Y;
      Z = point.Z;
    }

    /// <summary>他の点までの距離を求める</summary>
    /// <param name="point">他の点</param>
    /// <returns>距離</returns>
    public double GetDistance(Point point)
    {
      return Math.Sqrt(
          Math.Pow(X - point.X, 2) +
          Math.Pow(Y - point.Y, 2) +
          Math.Pow(Z - point.Z, 2));
    }

    /// <summary>ランダムな光線を生成する</summary>
    /// <param name="mRnd">一様乱数生成器</param>
    /// <returns>ランダムな光線</returns>
    public Line GenerateRandomRay(MersenneTwister mRnd)
    {
      double theta = 2 * Math.PI * mRnd.NextDouble();
      double eta = 2 * Math.PI * mRnd.NextDouble();
      double x = Math.Cos(eta);
      Vector3D direction = new Vector3D(
          Math.Cos(theta) * x,
          Math.Sin(theta) * x,
          Math.Sin(eta));
      return new Line(this, direction);
    }

    /// <summary>点から点へのベクトルを生成する（pt1 - pt2）</summary>
    public static Vector3D operator -(Point pt1, Point pt2)
    {
      return new Vector3D(pt1.X - pt2.X, pt1.Y - pt2.Y, pt1.Z - pt2.Z);
    }

    /// <summary>2点の座標和からベクトルを生成する</summary>
    public static Vector3D operator +(Point pt1, Point pt2)
    {
      return new Vector3D(pt1.X + pt2.X, pt1.Y + pt2.Y, pt1.Z + pt2.Z);
    }
  }
}
