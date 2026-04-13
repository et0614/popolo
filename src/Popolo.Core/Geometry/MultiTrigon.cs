/* MultiTrigon.cs
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
using System.Collections.Generic;
using System.IO;
using Popolo.Core.Numerics;

namespace Popolo.Core.Geometry
{
  /// <summary>複数のトリゴンから構成される多角形を表すクラス</summary>
  public class MultiTrigon
  {
    /// <summary>ゼロとみなす誤差の閾値</summary>
    public const double EPSILON_TOL = 0.00001d;

    /// <summary>面積を取得する</summary>
    public double Area { get; private set; }

    /// <summary>トリゴンの面積比累積配列</summary>
    private double[] areaRatios = Array.Empty<double>();

    /// <summary>トリゴンのリスト</summary>
    private readonly List<Trigon> trigons = new List<Trigon>();

    /// <summary>名称を設定・取得する</summary>
    public string? Name { get; set; }

    /// <summary>モンテカルロ法に備えた初期化処理</summary>
    public void InitializeForMonteCarloSimulation()
    {
      areaRatios = new double[trigons.Count + 2];
      Area = 0;
      for (int i = 0; i < trigons.Count; i++) Area += trigons[i].Area;

      areaRatios[0] = double.MinValue;
      areaRatios[1] = trigons[0].Area / Area;
      for (int i = 2; i < trigons.Count + 1; i++)
        areaRatios[i] = areaRatios[i - 1] + trigons[i - 1].Area / Area;
      areaRatios[areaRatios.Length - 1] = double.MaxValue;
    }

    /// <summary>ランダムな光線を発生させる</summary>
    /// <param name="mRnd">一様乱数生成器</param>
    /// <returns>ランダムな光線</returns>
    public Line GenerateRandomRay(MersenneTwister mRnd)
    {
      Trigon trg = SelectTrigon(mRnd.NextDouble());

      double rnd1 = Math.Sqrt(mRnd.NextDouble());
      double rnd2 = mRnd.NextDouble();
      double cA = 1.0 - rnd1;
      double cB = rnd1 * (1.0 - rnd2);
      double cC = rnd1 * rnd2;
      Point org = new Point(
          cA * trg.VertexA.X + cB * trg.VertexB.X + cC * trg.VertexC.X,
          cA * trg.VertexA.Y + cB * trg.VertexB.Y + cC * trg.VertexC.Y,
          cA * trg.VertexA.Z + cB * trg.VertexB.Z + cC * trg.VertexC.Z);

      double theta = 2 * Math.PI * mRnd.NextDouble();
      double eta = Math.Acos(Math.Sqrt(1 - mRnd.NextDouble()));
      double x = Math.Cos(eta);
      Vector3D direction = trg.Rotate(
          new Vector3D(Math.Cos(theta) * x, Math.Sin(theta) * x, Math.Sin(eta)));

      return new Line(org, direction);
    }

    /// <summary>光線が交差するか否か</summary>
    /// <param name="ray">光線</param>
    /// <param name="length">交差点までの距離</param>
    /// <returns>光線が交差するか否か</returns>
    public bool IsCrossed(Line ray, out double length)
    {
      length = double.MaxValue;
      bool isCrossed = false;

      for (int i = 0; i < trigons.Count; i++)
      {
        Vector3D vec = ray.Point - trigons[i].Plane.Point;
        if (vec.Length != 0)
        {
          if (EPSILON_TOL < Math.Abs(vec.GetDot(trigons[i].Plane.NormalUnit)))
          {
            if (trigons[i].CrossedWith(ray, out Point? cpt) && cpt != null)
            {
              Vector3D vRay = cpt - ray.Point;
              if (Math.Sign(vRay.X) == Math.Sign(ray.Vector.X) &&
                  Math.Sign(vRay.Y) == Math.Sign(ray.Vector.Y) &&
                  Math.Sign(vRay.Z) == Math.Sign(ray.Vector.Z))
              {
                isCrossed = true;
                length = Math.Min(length, ray.Point.GetDistance(cpt));
              }
            }
          }
        }
      }
      return isCrossed;
    }

    /// <summary>面積比に応じて確率的にトリゴンを選択する</summary>
    private Trigon SelectTrigon(double rnd)
    {
      int hIndx = areaRatios.Length - 1;
      int lIndx = 0;
      while (1 < hIndx - lIndx)
      {
        int mIndx = (hIndx + lIndx) >> 1;
        if (areaRatios[mIndx] < rnd) lIndx = mIndx;
        else hIndx = mIndx;
      }
      return trigons[lIndx];
    }

    /// <summary>ASCII形式のSTLデータをロードする</summary>
    /// <param name="stlData">STLデータ文字列</param>
    /// <returns>読み込んだ MultiTrigon。endsolid で正常終了しない場合は null。</returns>
    public static MultiTrigon? LoadSTL_ASCII(string stlData)
    {
      MultiTrigon mtr = new MultiTrigon();
      StringReader sReader = new StringReader(stlData);
      string? line = sReader.ReadLine();
      if (line != null)
        mtr.Name = line.Split(' ').Length > 1 ? line.Split(' ')[1] : null;

      bool endWithEndSolid = false;
      while ((line = sReader.ReadLine()) != null)
      {
        if (line.StartsWith("endsolid"))
        {
          endWithEndSolid = true;
          break;
        }
        if (line.Trim().StartsWith("facet normal"))
        {
          sReader.ReadLine();
          string[]? vertA = sReader.ReadLine()?.Trim().Split(' ');
          string[]? vertB = sReader.ReadLine()?.Trim().Split(' ');
          string[]? vertC = sReader.ReadLine()?.Trim().Split(' ');
          sReader.ReadLine();
          sReader.ReadLine();

          if (vertA == null || vertB == null || vertC == null) continue;

          mtr.trigons.Add(new Trigon(
              new Point(double.Parse(vertA[1]), double.Parse(vertA[2]), double.Parse(vertA[3])),
              new Point(double.Parse(vertB[1]), double.Parse(vertB[2]), double.Parse(vertB[3])),
              new Point(double.Parse(vertC[1]), double.Parse(vertC[2]), double.Parse(vertC[3]))));
        }
      }

      if (!endWithEndSolid) return null;
      return mtr;
    }
  }
}
