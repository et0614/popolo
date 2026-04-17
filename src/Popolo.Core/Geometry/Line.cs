/* Line.cs
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

namespace Popolo.Core.Geometry
{
  /// <summary>3次元空間の直線を表すクラス</summary>
  public class Line
  {
    /// <summary>直線上の点を取得する</summary>
    public Point Point { get; }

    /// <summary>直線の方向を表すベクトルを取得する</summary>
    public Vector3D Vector { get; }

    /// <summary>インスタンスを初期化する</summary>
    /// <param name="point">直線上の点</param>
    /// <param name="vector">直線の方向を表すベクトル</param>
    public Line(Point point, Vector3D vector)
    {
      Point = new Point(point);
      Vector = new Vector3D(vector);
    }
  }
}
