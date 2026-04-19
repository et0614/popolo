/* ViewFactor.cs
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
  /// <summary>Utility class for computing view factors between rectangular surfaces.</summary>
  /// <remarks>
  /// Coordinate system:
  /// Z
  /// ^    Y
  /// |   /
  /// |  /
  /// | /
  /// |/
  /// --------->X
  /// </remarks>
  public static class ViewFactor
  {

    #region 公開メソッド

    /// <summary>Computes the view factor between two parallel facing rectangles.</summary>
    /// <param name="width">Surface width (Y direction).</param>
    /// <param name="height">Surface height (Z direction).</param>
    /// <param name="distance">Distance between the surfaces (X direction, non-negative).</param>
    /// <returns>View factor in the range [0, 1].</returns>
    /// <remarks>
    /// View factor between two perfectly parallel (facing) rectangles.
    /// Returns 1 when distance = 0, and 0 when any of width, height, or distance is 0.
    /// </remarks>
    public static double GetViewFactorParallelRectangles(
        double width, double height, double distance)
    {
      return ViewParallelRectangle2FromRectangle1(width, height, distance);
    }

    /// <summary>Computes the view factor between two perpendicular rectangles sharing a common edge.</summary>
    /// <param name="width">Surface width (Y direction, shared).</param>
    /// <param name="height">Height of surface 1 (Z direction).</param>
    /// <param name="depth">Depth of surface 2 (X direction).</param>
    /// <returns>View factor in the range [0, 1].</returns>
    /// <remarks>
    /// View factor when two rectangles meet at right angles along a shared edge in the Y direction.
    /// Returns 0.5 when depth is infinite.
    /// </remarks>
    public static double GetViewFactorPerpendicularRectangles(
        double width, double height, double depth)
    {
      return ViewVerticalRectangle2FromRectangle1(width, height, depth);
    }

    /// <summary>Computes the view factor between perpendicular rectangles with an offset in the Z direction.</summary>
    /// <param name="width">Surface width (Y direction, shared).</param>
    /// <param name="height">Height of surface 1 (Z direction).</param>
    /// <param name="depth">Depth of surface 2 (X direction).</param>
    /// <param name="deltaZ">Offset in the Z direction (non-negative).</param>
    /// <returns>View factor in the range [0, 1].</returns>
    /// <remarks>
    /// View factor to a perpendicular surface 2 that begins <paramref name="deltaZ"/> above the bottom edge of surface 1.
    /// Reduces to the shared-edge case when deltaZ = 0.
    /// </remarks>
    /// <exception cref="PopoloArgumentException">Thrown when <paramref name="deltaZ"/> is negative.</exception>
    public static double GetViewFactorPerpendicularRectangles(
        double width, double height, double depth, double deltaZ)
    {
      return ViewVerticalRectangle2FromRectangle1(width, height, depth, deltaZ);
    }

    /// <summary>Computes the view factor between perpendicular rectangles with offsets in both the Z and X directions.</summary>
    /// <param name="width">Surface width (Y direction, shared).</param>
    /// <param name="height">Height of surface 1 (Z direction).</param>
    /// <param name="depth">Depth of surface 2 (X direction).</param>
    /// <param name="deltaZ">Offset in the Z direction (non-negative).</param>
    /// <param name="deltaX">Offset in the X direction (non-negative).</param>
    /// <returns>View factor in the range [0, 1].</returns>
    /// <exception cref="PopoloArgumentException">
    /// Thrown when <paramref name="deltaZ"/> or <paramref name="deltaX"/> is negative.
    /// </exception>
    public static double GetViewFactorPerpendicularRectangles(
        double width, double height, double depth, double deltaZ, double deltaX)
    {
      return ViewVerticalRectangle2FromRectangle1(width, height, depth, deltaZ, deltaX);
    }

    #endregion

    #region 非公開メソッド

    private static double ViewParallelRectangle2FromRectangle1(
        double width, double height, double distance)
    {
      if (distance == 0) return 1.0;
      if (width == 0 || height == 0 ||
          distance == double.PositiveInfinity || distance < 0) return 0;

      double x = width / distance;
      double y = height / distance;
      double rx = Math.Sqrt(1 + x * x);
      double ry = Math.Sqrt(1 + y * y);
      double rxy1 = Math.Sqrt(1 + x * x + y * y);

      return (2 / Math.PI)
          * (ry * Math.Atan(x / ry) / y
          + rx / x * Math.Atan(y / rx)
          - Math.Atan(x) / y
          - Math.Atan(y) / x
          + Math.Log(rx * ry / rxy1) / (x * y));
    }

    private static double ViewVerticalRectangle2FromRectangle1(
        double width, double height, double depth)
    {
      if (depth == double.PositiveInfinity) return 0.5;
      if (width == 0 || height == 0 || depth <= 0) return 0;

      double x = width / depth;
      double y = height / depth;
      double rx = Math.Sqrt(1 + x * x);
      double ry = Math.Sqrt(1 + y * y);
      double rxy1 = Math.Sqrt(1 + x * x + y * y);
      double rxy2 = Math.Sqrt(x * x + y * y);

      return (Math.Atan(x) / y
          - ry * Math.Atan(x / ry) / y
          + Math.Atan(x / y)
          + 0.5 / x / y * Math.Log(rxy1 * y / rxy2 / ry)
          + 0.5 * x / y * Math.Log(rxy2 * rx / rxy1 / x)) / Math.PI;
    }

    private static double ViewVerticalRectangle2FromRectangle1(
        double width, double height, double depth, double deltaZ)
    {
      if (deltaZ < 0)
        throw new PopoloArgumentException(
            $"deltaZ must be non-negative. Got: {deltaZ}", nameof(deltaZ));

      double ff1 = ViewVerticalRectangle2FromRectangle1(width, height + deltaZ, depth);
      if (deltaZ == 0) return ff1;

      double ff2 = ViewVerticalRectangle2FromRectangle1(width, deltaZ, depth);
      return ((width * (height + deltaZ)) * ff1 - (width * deltaZ) * ff2)
          / (width * height);
    }

    private static double ViewVerticalRectangle2FromRectangle1(
        double width, double height, double depth, double deltaZ, double deltaX)
    {
      if (deltaX < 0)
        throw new PopoloArgumentException(
            $"deltaX must be non-negative. Got: {deltaX}", nameof(deltaX));

      double ff1 = ViewVerticalRectangle2FromRectangle1(width, height, depth, deltaZ);
      if (deltaX == 0) return ff1;

      double ff2 = ViewVerticalRectangle2FromRectangle1(width, height, deltaX, deltaZ);
      return ff1 - ff2;
    }

    #endregion
  }
}
