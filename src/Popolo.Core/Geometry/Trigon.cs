/* Trigon.cs
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

namespace Popolo.Core.Geometry
{
  /// <summary>Represents a triangular polygon.</summary>
  public class Trigon
  {
    #region プロパティ

    private readonly Point vertA, vertB, vertC;

    /// <summary>Gets vertex A.</summary>
    public Point VertexA { get { return vertA; } }

    /// <summary>Gets vertex B.</summary>
    public Point VertexB { get { return vertB; } }

    /// <summary>Gets vertex C.</summary>
    public Point VertexC { get { return vertC; } }

    /// <summary>Gets the plane on which the triangle lies.</summary>
    public Plane Plane { get; private set; }

    /// <summary>Gets the area of the triangle.</summary>
    public double Area { get; private set; }

    /// <summary>Gets the rotation matrix that aligns (0, 0, 1) with the plane normal.</summary>
    public double[,] RotationMatrix { get; private set; }

    #endregion

    #region コンストラクタ

    /// <summary>Initializes a new instance.</summary>
    /// <param name="vertA">Vertex A.</param>
    /// <param name="vertB">Vertex B.</param>
    /// <param name="vertC">Vertex C.</param>
    public Trigon(Point vertA, Point vertB, Point vertC)
    {
      this.vertA = new Point(vertA);
      this.vertB = new Point(vertB);
      this.vertC = new Point(vertC);

      Vector3D vecAB = this.vertB - this.vertA;
      Vector3D vecBC = this.vertC - this.vertB;
      Vector3D crss = vecAB.GetCross(vecBC);
      Plane = new Plane(vertA, crss);

      Area = 0.5 * Math.Sqrt(
          Math.Pow(vecAB.Y * vecBC.Z - vecAB.Z * vecBC.Y, 2) +
          Math.Pow(vecAB.Z * vecBC.X - vecAB.X * vecBC.Z, 2) +
          Math.Pow(vecAB.X * vecBC.Y - vecAB.Y * vecBC.X, 2));

      RotationMatrix = MakeRotationMatrix(
          new Vector3D(0, 0, 1),
          new Vector3D(Plane.NormalUnit));
    }

    #endregion

    #region インスタンスメソッド

    /// <summary>Determines whether the triangle intersects the specified line.</summary>
    /// <param name="line">The line to test.</param>
    /// <param name="crossedPoint">The intersection point (null if the line does not intersect).</param>
    /// <returns>True if the triangle intersects the line; otherwise false.</returns>
    public bool CrossedWith(Line line, out Point? crossedPoint)
    {
      crossedPoint = Plane.GetCrossedPoint(line);
      if (crossedPoint == null) return false;

      Vector3D vecAN = (vertB - vertA).GetCross(crossedPoint - vertB);
      vecAN.Normalize();
      if (!vecAN.Equals(Plane.NormalUnit)) return false;

      Vector3D vecBN = (vertC - vertB).GetCross(crossedPoint - vertC);
      vecBN.Normalize();
      if (!vecBN.Equals(Plane.NormalUnit)) return false;

      Vector3D vecCN = (vertA - vertC).GetCross(crossedPoint - vertA);
      vecCN.Normalize();
      if (!vecCN.Equals(Plane.NormalUnit)) return false;

      return true;
    }

    /// <summary>Transforms a vector by the rotation matrix.</summary>
    /// <param name="vec">Vector to transform.</param>
    /// <returns>Transformed vector.</returns>
    public Vector3D Rotate(Vector3D vec)
    {
      return new Vector3D(
          vec.X * RotationMatrix[0, 0] + vec.Y * RotationMatrix[0, 1] + vec.Z * RotationMatrix[0, 2],
          vec.X * RotationMatrix[1, 0] + vec.Y * RotationMatrix[1, 1] + vec.Z * RotationMatrix[1, 2],
          vec.X * RotationMatrix[2, 0] + vec.Y * RotationMatrix[2, 1] + vec.Z * RotationMatrix[2, 2]);
    }

    #endregion

    #region 静的メソッド

    /// <summary>Builds a 3x3 rotation matrix that rotates <paramref name="vec1"/> to align with <paramref name="vec2"/>.</summary>
    /// <param name="vec1">Source vector.</param>
    /// <param name="vec2">Target vector after rotation.</param>
    /// <returns>3x3 rotation matrix.</returns>
    public static double[,] MakeRotationMatrix(Vector3D vec1, Vector3D vec2)
    {
      Vector3D vc = vec1 + vec2;
      double[,] mat = new double[3, 3];

      if (vc.Length == 0)
      {
        mat[0, 0] = 1.0 - 2.0 * vec1.X * vec1.X;
        mat[0, 1] = -2.0 * vec1.X * vec1.Y;
        mat[0, 2] = -2.0 * vec1.X * vec1.Z;
        mat[1, 0] = -2.0 * vec1.Y * vec1.X;
        mat[1, 1] = 1.0 - 2.0 * vec1.Y * vec1.Y;
        mat[1, 2] = -2.0 * vec1.Y * vec1.Z;
        mat[2, 0] = -2.0 * vec1.Z * vec1.X;
        mat[2, 1] = -2.0 * vec1.Z * vec1.Y;
        mat[2, 2] = 1.0 - 2.0 * vec1.Z * vec1.Z;
      }
      else
      {
        double denom = 2.0 / (vc.X * vc.X + vc.Y * vc.Y + vc.Z * vc.Z);
        mat[0, 0] = denom * vc.X * vc.X - 1.0;
        mat[0, 1] = denom * vc.X * vc.Y;
        mat[0, 2] = denom * vc.X * vc.Z;
        mat[1, 0] = denom * vc.Y * vc.X;
        mat[1, 1] = denom * vc.Y * vc.Y - 1.0;
        mat[1, 2] = denom * vc.Y * vc.Z;
        mat[2, 0] = denom * vc.Z * vc.X;
        mat[2, 1] = denom * vc.Z * vc.Y;
        mat[2, 2] = denom * vc.Z * vc.Z - 1.0;
      }
      return mat;
    }

    #endregion
  }
}
