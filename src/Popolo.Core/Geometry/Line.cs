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
  /// <summary>Represents a line in three-dimensional space.</summary>
  public class Line
  {
    /// <summary>Gets a point on the line.</summary>
    public Point Point { get; }

    /// <summary>Gets the direction vector of the line.</summary>
    public Vector3D Vector { get; }

    /// <summary>Initializes a new instance.</summary>
    /// <param name="point">A point on the line.</param>
    /// <param name="vector">Direction vector of the line.</param>
    public Line(Point point, Vector3D vector)
    {
      Point = new Point(point);
      Vector = new Vector3D(vector);
    }
  }
}
