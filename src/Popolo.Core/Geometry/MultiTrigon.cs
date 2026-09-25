/* MultiTrigon.cs
 *
 * Copyright (C) 2015 E.Togashi
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Popolo.Core.Exceptions;
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
    /// <remarks>
    /// Tokens may be separated by any amount of whitespace (spaces or tabs), and numbers are
    /// parsed with the invariant culture regardless of the current culture. Vertices are
    /// identified by the "vertex" keyword and collected per facet ("facet" ... "endfacet").
    /// </remarks>
    /// <exception cref="PopoloArgumentException">
    /// Thrown when a facet does not have exactly three vertices, or a vertex line does not
    /// have three coordinates.
    /// </exception>
    /// <exception cref="FormatException">Thrown when a coordinate is not a valid number.</exception>
    public static MultiTrigon? LoadSTL_ASCII(string stlData)
    {
      char[] separators = { ' ', '\t' };
      MultiTrigon mtr = new MultiTrigon();
      StringReader sReader = new StringReader(stlData);
      string? line = sReader.ReadLine();
      if (line != null)
      {
        string[] header = line.Split(separators, StringSplitOptions.RemoveEmptyEntries);
        mtr.Name = header.Length > 1 ? header[1] : null;
      }

      bool endWithEndSolid = false;
      List<Point>? vertices = null;
      while ((line = sReader.ReadLine()) != null)
      {
        string[] tokens = line.Split(separators, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0) continue;
        string keyword = tokens[0];

        if (keyword == "endsolid")
        {
          endWithEndSolid = true;
          break;
        }
        else if (keyword == "facet")
        {
          vertices = new List<Point>(3);
        }
        else if (keyword == "vertex" && vertices != null)
        {
          if (tokens.Length < 4)
            throw new PopoloArgumentException(
                $"Invalid STL vertex line: \"{line.Trim()}\" (three coordinates required).",
                nameof(stlData));
          vertices.Add(new Point(
              double.Parse(tokens[1], NumberStyles.Float, CultureInfo.InvariantCulture),
              double.Parse(tokens[2], NumberStyles.Float, CultureInfo.InvariantCulture),
              double.Parse(tokens[3], NumberStyles.Float, CultureInfo.InvariantCulture)));
        }
        else if (keyword == "endfacet" && vertices != null)
        {
          if (vertices.Count != 3)
            throw new PopoloArgumentException(
                $"Invalid STL facet: {vertices.Count} vertices found (three required).",
                nameof(stlData));
          mtr.trigons.Add(new Trigon(vertices[0], vertices[1], vertices[2]));
          vertices = null;
        }
      }

      if (!endWithEndSolid) return null;
      return mtr;
    }
  }
}
