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
  /// <summary>Represents a polygon composed of multiple triangles.</summary>
  public class MultiTrigon
  {

    /// <summary>Gets the total area.</summary>
    public double Area { get; private set; }

    /// <summary>Cumulative area-ratio array over the triangles.</summary>
    private double[] areaRatios = Array.Empty<double>();

    /// <summary>List of triangles.</summary>
    private readonly List<Trigon> trigons = new List<Trigon>();

    /// <summary>Gets or sets the name.</summary>
    public string? Name { get; set; }

    /// <summary>Initializes internal state for Monte Carlo simulation.</summary>
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

    /// <summary>Generates a random ray from the surface.</summary>
    /// <param name="mRnd">Uniform random number generator.</param>
    /// <returns>A ray with a random origin and direction.</returns>
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

    /// <summary>Determines whether a ray intersects this polygon.</summary>
    /// <param name="ray">Ray to test.</param>
    /// <param name="length">Distance from the ray origin to the nearest intersection.</param>
    /// <returns>True if the ray intersects the polygon; otherwise false.</returns>
    public bool IsCrossed(Line ray, out double length)
    {
      length = double.MaxValue;
      bool isCrossed = false;

      for (int i = 0; i < trigons.Count; i++)
      {
        Vector3D vec = ray.Point - trigons[i].Plane.Point;
        if (vec.Length != 0)
        {
          if (Vector3D.GeometryTolerance < Math.Abs(vec.GetDot(trigons[i].Plane.NormalUnit)))
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

    /// <summary>Probabilistically selects a triangle proportional to its area.</summary>
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

    /// <summary>Loads STL data in ASCII format.</summary>
    /// <param name="stlData">STL data string.</param>
    /// <returns>The loaded <see cref="MultiTrigon"/>, or null if the data does not terminate with "endsolid".</returns>
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
