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
  /// <summary>形態係数計算クラス</summary>
  /// <remarks>
  /// 座標系：
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

    /// <summary>対向する長方形面間の形態係数を計算する</summary>
    /// <param name="width">面の幅（Y方向）</param>
    /// <param name="height">面の高さ（Z方向）</param>
    /// <param name="distance">面間距離（X方向、0以上）</param>
    /// <returns>形態係数（0〜1）</returns>
    /// <remarks>
    /// 2枚の長方形面が完全に対向（平行）している場合の形態係数。
    /// distance=0のとき1を返す。width, height, distanceのいずれかが0のとき0を返す。
    /// </remarks>
    public static double GetViewFactorParallelRectangles(
        double width, double height, double distance)
    {
      return ViewParallelRectangle2FromRectangle1(width, height, distance);
    }

    /// <summary>共有辺を持つ垂直な長方形面間の形態係数を計算する</summary>
    /// <param name="width">面の幅（Y方向、共通）</param>
    /// <param name="height">面1の高さ（Z方向）</param>
    /// <param name="depth">面2の奥行き（X方向）</param>
    /// <returns>形態係数（0〜1）</returns>
    /// <remarks>
    /// 2枚の長方形面がY軸方向の共有辺で直交する場合の形態係数。
    /// depth=∞のとき0.5を返す。
    /// </remarks>
    public static double GetViewFactorPerpendicularRectangles(
        double width, double height, double depth)
    {
      return ViewVerticalRectangle2FromRectangle1(width, height, depth);
    }

    /// <summary>Z方向にオフセットした垂直な長方形面間の形態係数を計算する</summary>
    /// <param name="width">面の幅（Y方向、共通）</param>
    /// <param name="height">面1の高さ（Z方向）</param>
    /// <param name="depth">面2の奥行き（X方向）</param>
    /// <param name="deltaZ">Z方向オフセット（0以上）</param>
    /// <returns>形態係数（0〜1）</returns>
    /// <remarks>
    /// 面1の下端から deltaZ だけ離れた位置から始まる垂直面2への形態係数。
    /// deltaZ=0のとき共有辺を持つ場合と同じ結果を返す。
    /// </remarks>
    /// <exception cref="PopoloArgumentException">deltaZ が負の場合。</exception>
    public static double GetViewFactorPerpendicularRectangles(
        double width, double height, double depth, double deltaZ)
    {
      return ViewVerticalRectangle2FromRectangle1(width, height, depth, deltaZ);
    }

    /// <summary>Z・X方向にオフセットした垂直な長方形面間の形態係数を計算する</summary>
    /// <param name="width">面の幅（Y方向、共通）</param>
    /// <param name="height">面1の高さ（Z方向）</param>
    /// <param name="depth">面2の奥行き（X方向）</param>
    /// <param name="deltaZ">Z方向オフセット（0以上）</param>
    /// <param name="deltaX">X方向オフセット（0以上）</param>
    /// <returns>形態係数（0〜1）</returns>
    /// <exception cref="PopoloArgumentException">
    /// deltaZ または deltaX が負の場合。
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
