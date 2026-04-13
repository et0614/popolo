/* GeometryTests.cs
 *
 * Copyright (C) 2026 E.Togashi
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
using Xunit;
using Popolo.Core.Geometry;
using Popolo.Core.Exceptions;

namespace Popolo.Core.Tests.Geometry
{
  /// <summary>Point のテスト</summary>
  public class PointTests
  {
    [Fact]
    public void Constructor_SetsCoordinates()
    {
      var p = new Point(1.0, 2.0, 3.0);
      Assert.Equal(1.0, p.X);
      Assert.Equal(2.0, p.Y);
      Assert.Equal(3.0, p.Z);
    }

    [Fact]
    public void CopyConstructor_CopiesCoordinates()
    {
      var p1 = new Point(1.0, 2.0, 3.0);
      var p2 = new Point(p1);
      Assert.Equal(p1.X, p2.X);
      Assert.Equal(p1.Y, p2.Y);
      Assert.Equal(p1.Z, p2.Z);
    }

    [Fact]
    public void GetDistance_KnownPoints_ReturnsCorrectDistance()
    {
      var p1 = new Point(0.0, 0.0, 0.0);
      var p2 = new Point(3.0, 4.0, 0.0);
      Assert.Equal(5.0, p1.GetDistance(p2), precision: 10);
    }

    [Fact]
    public void OperatorMinus_ReturnsDirectionVector()
    {
      var p1 = new Point(3.0, 4.0, 5.0);
      var p2 = new Point(1.0, 2.0, 3.0);
      var v = p1 - p2;
      Assert.Equal(2.0, v.X);
      Assert.Equal(2.0, v.Y);
      Assert.Equal(2.0, v.Z);
    }
  }

  /// <summary>Line のテスト</summary>
  public class LineTests
  {
    [Fact]
    public void Constructor_StoresPointAndVector()
    {
      var p = new Point(1.0, 0.0, 0.0);
      var v = new Vector3D(0.0, 1.0, 0.0);
      var line = new Line(p, v);
      Assert.Equal(1.0, line.Point.X);
      Assert.Equal(1.0, line.Vector.Y);
    }
  }

  /// <summary>Plane のテスト</summary>
  public class PlaneTests
  {
    [Fact]
    public void Constructor_PointNormal_SetsCorrectCoefficients()
    {
      // XY平面（z=0）：法線=(0,0,1)、点=(0,0,0)
      var p = new Point(0, 0, 0);
      var n = new Vector3D(0, 0, 1);
      var plane = new Plane(p, n);

      Assert.Equal(0.0, plane.A, precision: 10);
      Assert.Equal(0.0, plane.B, precision: 10);
      Assert.Equal(1.0, plane.C, precision: 10);
      Assert.Equal(0.0, plane.D, precision: 10);
    }

    [Fact]
    public void Constructor_ZeroNormal_ThrowsPopoloArgumentException()
    {
      Assert.Throws<PopoloArgumentException>(
          () => new Plane(0.0, 0.0, 0.0, 1.0));
    }

    [Fact]
    public void CrossedWith_ParallelLine_ReturnsFalse()
    {
      // XY平面と、XY平面と平行な直線（z方向に上にある）
      var plane = new Plane(new Point(0, 0, 0), new Vector3D(0, 0, 1));
      var line = new Line(new Point(0, 0, 1), new Vector3D(1, 0, 0));
      Assert.False(plane.CrossedWith(line));
    }

    [Fact]
    public void CrossedWith_IntersectingLine_ReturnsTrue()
    {
      var plane = new Plane(new Point(0, 0, 0), new Vector3D(0, 0, 1));
      var line = new Line(new Point(0, 0, 1), new Vector3D(0, 0, -1));
      Assert.True(plane.CrossedWith(line));
    }

    [Fact]
    public void GetCrossedPoint_IntersectingLine_ReturnsCorrectPoint()
    {
      // XY平面（z=0）と、z軸方向の直線
      var plane = new Plane(new Point(0, 0, 0), new Vector3D(0, 0, 1));
      var line = new Line(new Point(2, 3, 5), new Vector3D(0, 0, -1));

      Point? pt = plane.GetCrossedPoint(line);
      Assert.NotNull(pt);
      Assert.Equal(2.0, pt!.X, precision: 6);
      Assert.Equal(3.0, pt.Y, precision: 6);
      Assert.Equal(0.0, pt.Z, precision: 6);
    }

    [Fact]
    public void GetCrossedPoint_ParallelLine_ReturnsNull()
    {
      var plane = new Plane(new Point(0, 0, 0), new Vector3D(0, 0, 1));
      var line = new Line(new Point(0, 0, 1), new Vector3D(1, 0, 0));
      Assert.Null(plane.GetCrossedPoint(line));
    }
  }

  /// <summary>Trigon のテスト</summary>
  public class TrigonTests
  {
    private static Trigon MakeXYTriangle()
    {
      // XY平面上の直角三角形
      return new Trigon(
          new Point(0, 0, 0),
          new Point(1, 0, 0),
          new Point(0, 1, 0));
    }

    [Fact]
    public void Constructor_CalculatesCorrectArea()
    {
      var t = MakeXYTriangle();
      Assert.Equal(0.5, t.Area, precision: 10);
    }

    [Fact]
    public void Constructor_CalculatesCorrectNormal()
    {
      var t = MakeXYTriangle();
      // XY平面の法線は (0,0,1) または (0,0,-1)
      Assert.Equal(0.0, t.Plane.A, precision: 6);
      Assert.Equal(0.0, t.Plane.B, precision: 6);
      Assert.Equal(1.0, Math.Abs(t.Plane.C), precision: 6);
    }

    [Fact]
    public void CrossedWith_LineThrough_ReturnsTrue()
    {
      var t = MakeXYTriangle();
      // 三角形の中心を通るz軸方向の直線
      var line = new Line(new Point(0.2, 0.2, 1), new Vector3D(0, 0, -1));
      bool result = t.CrossedWith(line, out Point? pt);
      Assert.True(result);
      Assert.NotNull(pt);
    }

    [Fact]
    public void CrossedWith_LineOutside_ReturnsFalse()
    {
      var t = MakeXYTriangle();
      // 三角形の外を通るz軸方向の直線
      var line = new Line(new Point(2, 2, 1), new Vector3D(0, 0, -1));
      bool result = t.CrossedWith(line, out _);
      Assert.False(result);
    }

    [Fact]
    public void MakeRotationMatrix_SameVector_RotatesVectorToItself()
    {
      var v = new Vector3D(0, 0, 1);
      var mat = Trigon.MakeRotationMatrix(v, v);

      // v を mat で変換すると v 自身に写ることを確認
      double rx = v.X * mat[0, 0] + v.Y * mat[0, 1] + v.Z * mat[0, 2];
      double ry = v.X * mat[1, 0] + v.Y * mat[1, 1] + v.Z * mat[1, 2];
      double rz = v.X * mat[2, 0] + v.Y * mat[2, 1] + v.Z * mat[2, 2];

      Assert.Equal(v.X, rx, precision: 6);
      Assert.Equal(v.Y, ry, precision: 6);
      Assert.Equal(v.Z, rz, precision: 6);
    }
  }

  /// <summary>MultiTrigon のテスト</summary>
  public class MultiTrigonTests
  {
    private static readonly string ValidStl =
        "solid test\n" +
        "  facet normal 0 0 1\n" +
        "    outer loop\n" +
        "      vertex 0 0 0\n" +
        "      vertex 1 0 0\n" +
        "      vertex 0 1 0\n" +
        "    endloop\n" +
        "  endfacet\n" +
        "  facet normal 0 0 1\n" +
        "    outer loop\n" +
        "      vertex 1 0 0\n" +
        "      vertex 1 1 0\n" +
        "      vertex 0 1 0\n" +
        "    endloop\n" +
        "  endfacet\n" +
        "endsolid test\n";

    private static readonly string InvalidStl =
        "solid test\n" +
        "  facet normal 0 0 1\n" +
        "    outer loop\n" +
        "      vertex 0 0 0\n" +
        "      vertex 1 0 0\n" +
        "      vertex 0 1 0\n" +
        "    endloop\n" +
        "  endfacet\n";
    // endsolid がない → null を返す

    [Fact]
    public void LoadSTL_ASCII_ValidData_ReturnsMultiTrigon()
    {
      var mt = MultiTrigon.LoadSTL_ASCII(ValidStl);
      Assert.NotNull(mt);
    }

    [Fact]
    public void LoadSTL_ASCII_InvalidData_ReturnsNull()
    {
      var mt = MultiTrigon.LoadSTL_ASCII(InvalidStl);
      Assert.Null(mt);
    }

    [Fact]
    public void InitializeForMonteCarloSimulation_CalculatesArea()
    {
      var mt = MultiTrigon.LoadSTL_ASCII(ValidStl);
      Assert.NotNull(mt);
      mt!.InitializeForMonteCarloSimulation();
      // 2つの直角三角形（各0.5）の合計面積 = 1.0
      Assert.Equal(1.0, mt.Area, precision: 6);
    }
  }
}
