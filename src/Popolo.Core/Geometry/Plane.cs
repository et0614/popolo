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
  /// <summary>Represents a plane in three-dimensional space.</summary>
  public class Plane
  {
    /// <summary>Gets a point on the plane.</summary>
    public Point Point { get; }

    /// <summary>Gets the unit normal vector.</summary>
    public Vector3D NormalUnit { get; }

    /// <summary>Gets the coefficient A of the plane equation Ax + By + Cz + D = 0.</summary>
    public double A { get { return NormalUnit.X; } }

    /// <summary>Gets the coefficient B of the plane equation Ax + By + Cz + D = 0.</summary>
    public double B { get { return NormalUnit.Y; } }

    /// <summary>Gets the coefficient C of the plane equation Ax + By + Cz + D = 0.</summary>
    public double C { get { return NormalUnit.Z; } }

    /// <summary>Gets the constant term D of the plane equation Ax + By + Cz + D = 0.</summary>
    public double D { get; private set; }

    /// <summary>Initializes a new instance from a point and a normal vector.</summary>
    /// <param name="point">A point on the plane.</param>
    /// <param name="normal">Normal vector of the plane.</param>
    public Plane(Point point, Vector3D normal)
    {
      Point = point;
      NormalUnit = Vector3D.GetUnitVector(normal);
      D = -(NormalUnit.X * point.X + NormalUnit.Y * point.Y + NormalUnit.Z * point.Z);
    }

    /// <summary>Initializes a new instance from the coefficients of the plane equation.</summary>
    /// <param name="a">Coefficient A.</param>
    /// <param name="b">Coefficient B.</param>
    /// <param name="c">Coefficient C.</param>
    /// <param name="d">Constant term D.</param>
    /// <exception cref="PopoloArgumentException">
    /// Thrown when a, b, and c are all zero (the normal vector would be zero).
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

    /// <summary>Copy constructor.</summary>
    /// <param name="pln">Source plane to copy.</param>
    public Plane(Plane pln) : this(pln.Point, pln.NormalUnit) { }

    /// <summary>Determines whether this plane contains the specified line.</summary>
    /// <param name="line">The line to test.</param>
    /// <returns>True if the plane contains the line; otherwise false.</returns>
    public bool Contains(Line line)
    {
      Vector3D vec = new Vector3D(
          line.Point.X - Point.X,
          line.Point.Y - Point.Y,
          line.Point.Z - Point.Z);
      return Math.Abs(NormalUnit.GetDot(vec)) < Vector3D.GeometryTolerance;
    }

    /// <summary>Determines whether this plane intersects the specified line.</summary>
    /// <param name="line">The line to test.</param>
    /// <returns>True if the plane intersects the line; otherwise false.</returns>
    public bool CrossedWith(Line line)
    {
      if (Point.X == line.Point.X &&
          Point.Y == line.Point.Y &&
          Point.Z == line.Point.Z) return true;
      if (Contains(line)) return true;
      return Vector3D.GeometryTolerance <= Math.Abs(line.Vector.GetDot(NormalUnit));
    }

    /// <summary>Returns the intersection point with the specified line.</summary>
    /// <param name="line">The line to test.</param>
    /// <returns>The intersection point, or null if the line does not intersect the plane.</returns>
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
