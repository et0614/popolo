/* Line.cs
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
