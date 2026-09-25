/* Point.cs
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
using Popolo.Core.Numerics;

namespace Popolo.Core.Geometry
{
  /// <summary>Represents a point in three-dimensional space.</summary>
  public class Point
  {
    /// <summary>Gets the X coordinate.</summary>
    public double X { get; private set; }

    /// <summary>Gets the Y coordinate.</summary>
    public double Y { get; private set; }

    /// <summary>Gets the Z coordinate.</summary>
    public double Z { get; private set; }

    /// <summary>Initializes a new instance.</summary>
    /// <param name="x">X coordinate.</param>
    /// <param name="y">Y coordinate.</param>
    /// <param name="z">Z coordinate.</param>
    public Point(double x, double y, double z)
    {
      X = x;
      Y = y;
      Z = z;
    }

    /// <summary>Copy constructor.</summary>
    /// <param name="point">Source point to copy.</param>
    public Point(Point point)
    {
      X = point.X;
      Y = point.Y;
      Z = point.Z;
    }

    /// <summary>Returns the Euclidean distance to another point.</summary>
    /// <param name="point">The other point.</param>
    /// <returns>Distance between the two points.</returns>
    public double GetDistance(Point point)
    {
      return Math.Sqrt(
          Math.Pow(X - point.X, 2) +
          Math.Pow(Y - point.Y, 2) +
          Math.Pow(Z - point.Z, 2));
    }

    /// <summary>Generates a random ray originating from this point.</summary>
    /// <param name="mRnd">Uniform random number generator.</param>
    /// <returns>A ray with a random direction.</returns>
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

    /// <summary>Returns the vector from <paramref name="pt2"/> to <paramref name="pt1"/> (pt1 - pt2).</summary>
    public static Vector3D operator -(Point pt1, Point pt2)
    {
      return new Vector3D(pt1.X - pt2.X, pt1.Y - pt2.Y, pt1.Z - pt2.Z);
    }

    /// <summary>Returns a vector formed by adding the coordinates of two points.</summary>
    public static Vector3D operator +(Point pt1, Point pt2)
    {
      return new Vector3D(pt1.X + pt2.X, pt1.Y + pt2.Y, pt1.Z + pt2.Z);
    }
  }
}
