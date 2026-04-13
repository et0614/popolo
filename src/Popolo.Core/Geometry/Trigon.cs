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
  /// <summary>三角形ポリゴンを表すクラス</summary>
  public class Trigon
  {
    #region プロパティ

    private readonly Point vertA, vertB, vertC;

    /// <summary>頂点Aを取得する</summary>
    public Point VertexA { get { return vertA; } }

    /// <summary>頂点Bを取得する</summary>
    public Point VertexB { get { return vertB; } }

    /// <summary>頂点Cを取得する</summary>
    public Point VertexC { get { return vertC; } }

    /// <summary>平面を取得する</summary>
    public Plane Plane { get; private set; }

    /// <summary>面積を取得する</summary>
    public double Area { get; private set; }

    /// <summary>(0,0,1) を基準としたときの回転行列</summary>
    public double[,] RotationMatrix { get; private set; }

    #endregion

    #region コンストラクタ

    /// <summary>インスタンスを初期化する</summary>
    /// <param name="vertA">頂点A</param>
    /// <param name="vertB">頂点B</param>
    /// <param name="vertC">頂点C</param>
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

    /// <summary>線と交わっているか否か</summary>
    /// <param name="line">線</param>
    /// <param name="crossedPoint">交差点の座標（交わらない場合は null）</param>
    /// <returns>線と交わっているか否か</returns>
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

    /// <summary>ベクトルを回転行列で変換する</summary>
    /// <param name="vec">変換するベクトル</param>
    /// <returns>変換後のベクトル</returns>
    public Vector3D Rotate(Vector3D vec)
    {
      return new Vector3D(
          vec.X * RotationMatrix[0, 0] + vec.Y * RotationMatrix[0, 1] + vec.Z * RotationMatrix[0, 2],
          vec.X * RotationMatrix[1, 0] + vec.Y * RotationMatrix[1, 1] + vec.Z * RotationMatrix[1, 2],
          vec.X * RotationMatrix[2, 0] + vec.Y * RotationMatrix[2, 1] + vec.Z * RotationMatrix[2, 2]);
    }

    #endregion

    #region 静的メソッド

    /// <summary>vec1 を vec2 の方向に回転させる回転行列を作成する</summary>
    /// <param name="vec1">元のベクトル</param>
    /// <param name="vec2">回転後のベクトル</param>
    /// <returns>3x3回転行列</returns>
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
