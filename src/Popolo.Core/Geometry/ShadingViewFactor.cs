/* ShadingViewFactor.cs
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

namespace Popolo.Core.Geometry
{
  /// <summary>
  /// Analytical view factors specialized for window-shading geometries
  /// (overhangs and side fins). Built on <see cref="ViewFactor"/>.
  /// </summary>
  /// <remarks>
  /// <para>
  /// The view factors here are used to estimate the reduction of the window's
  /// sky view caused by an opaque shading device above or beside the window,
  /// for the purpose of attenuating diffuse sky radiation that reaches the
  /// glazing.
  /// </para>
  /// <para>
  /// All geometries are assumed flat-rectangular: the window is vertical, the
  /// overhang is horizontal and perpendicular to the wall, and (when present)
  /// fins are vertical and perpendicular to the wall. The shading device is
  /// treated as fully opaque in the long-wave / sky-diffuse sense; partial
  /// transmittance is the caller's responsibility.
  /// </para>
  /// </remarks>
  public static class ShadingViewFactor
  {

    #region 庇 (overhang) による窓→天空遮蔽

    /// <summary>
    /// Computes the view factor from a vertical rectangular window to a
    /// horizontal rectangular overhang positioned above it (depth perpendicular
    /// to the wall). The overhang is assumed centered on the window in the
    /// lateral direction with the same width as the window.
    /// </summary>
    /// <param name="windowWidth">Window width [m] (lateral, parallel to wall).</param>
    /// <param name="windowHeight">Window height [m].</param>
    /// <param name="overhangDepth">Overhang depth [m] (extending outward perpendicular to wall).</param>
    /// <param name="gap">Vertical gap between window top and overhang underside [m] (≥ 0).</param>
    /// <returns>View factor F<sub>window→overhang</sub> in [0, 0.5].</returns>
    /// <remarks>
    /// <para>
    /// Used to attenuate diffuse sky radiation reaching the window: since an
    /// unobstructed vertical surface has F<sub>vertical→sky</sub> = 0.5 and the
    /// overhang only blocks the upper hemisphere (sky portion), the diffuse-sky
    /// view factor with overhang is approximately 0.5 − (returned value).
    /// </para>
    /// <para>
    /// Limit cases (verified by tests):
    /// </para>
    /// <list type="bullet">
    ///   <item><description><c>overhangDepth = 0</c> ⇒ F = 0 (no shading).</description></item>
    ///   <item><description><c>overhangDepth → ∞, gap = 0</c> ⇒ F → 0.5 (overhang fills the upper hemisphere as seen from the window).</description></item>
    ///   <item><description>F is monotonically increasing in <c>overhangDepth</c> and decreasing in <c>gap</c>.</description></item>
    /// </list>
    /// </remarks>
    public static double GetViewFactorWindowToOverhang(
        double windowWidth,
        double windowHeight,
        double overhangDepth,
        double gap = 0.0)
    {
      if (windowWidth <= 0 || windowHeight <= 0 || overhangDepth <= 0) return 0;
      if (gap < 0) gap = 0;

      // ViewFactor.GetViewFactorPerpendicularRectangles の deltaZ=gap で
      // 「窓の上部から gap だけ離れた庇」の F_window→overhang を計算。
      return ViewFactor.GetViewFactorPerpendicularRectangles(
          windowWidth, windowHeight, overhangDepth, gap);
    }

    /// <summary>
    /// Returns the fraction of the vertical window's sky view factor that is
    /// blocked by an overhang of the given geometry. Equivalent to
    /// <see cref="GetViewFactorWindowToOverhang"/> with the convention that an
    /// unshaded vertical window sees a full sky view factor of 0.5.
    /// </summary>
    /// <returns>Sky-blocking fraction in [0, 0.5].</returns>
    public static double GetSkyBlockedFractionByOverhang(
        double windowWidth,
        double windowHeight,
        double overhangDepth,
        double gap = 0.0)
        => GetViewFactorWindowToOverhang(windowWidth, windowHeight, overhangDepth, gap);

    /// <summary>
    /// Returns the effective sky-view factor for a vertical window with the
    /// given overhang above it. Without any overhang the value is 0.5.
    /// </summary>
    /// <returns>F<sub>window→sky</sub> with overhang attenuation, in [0, 0.5].</returns>
    public static double GetWindowSkyViewFactorWithOverhang(
        double windowWidth,
        double windowHeight,
        double overhangDepth,
        double gap = 0.0)
    {
      double blocked = GetViewFactorWindowToOverhang(
          windowWidth, windowHeight, overhangDepth, gap);
      double f = 0.5 - blocked;
      // 数値誤差で僅かに負になる可能性に保険。
      return f < 0 ? 0 : f;
    }

    #endregion

    #region 縦フィン (vertical fin) による窓→天空遮蔽

    /// <summary>
    /// Computes the view factor from a vertical rectangular window to a vertical
    /// rectangular fin attached to the wall on one side of the window
    /// (perpendicular to the wall, extending outward). The fin is assumed to
    /// match the window's vertical extent (same height, top and bottom flush
    /// with the window).
    /// </summary>
    /// <param name="windowWidth">Window width [m] (perpendicular to the shared vertical edge).</param>
    /// <param name="windowHeight">Window height [m] (along the shared vertical edge).</param>
    /// <param name="finDepth">Fin depth [m] (extending outward perpendicular to wall).</param>
    /// <param name="lateralGap">Lateral gap between the fin and the window's side edge [m] (≥ 0).</param>
    /// <returns>View factor F<sub>window→fin</sub> in [0, 0.5].</returns>
    public static double GetViewFactorWindowToVerticalFin(
        double windowWidth,
        double windowHeight,
        double finDepth,
        double lateralGap = 0.0)
    {
      if (windowWidth <= 0 || windowHeight <= 0 || finDepth <= 0) return 0;
      if (lateralGap < 0) lateralGap = 0;

      // 形態係数の共有エッジは窓の縦辺 (高さ H_window)。窓の "perpendicular" 寸法は W (横方向)、
      // フィンの "perpendicular" 寸法はフィン深さ D (壁から外向き)。
      // ViewFactor.GetViewFactorPerpendicularRectangles(width=共有エッジ長,
      //                                                  height=surface1垂直寸法,
      //                                                  depth=surface2垂直寸法,
      //                                                  deltaZ=横方向オフセット)
      return ViewFactor.GetViewFactorPerpendicularRectangles(
          windowHeight, windowWidth, finDepth, lateralGap);
    }

    /// <summary>
    /// Returns the fraction of the vertical window's sky-view factor that is
    /// blocked by a vertical fin of the given geometry.
    /// </summary>
    /// <remarks>
    /// Approximation: a vertical fin of the same height as the window blocks
    /// angles that span both the upper (sky) and lower (ground) hemispheres in
    /// roughly equal measure, so the sky-view reduction is taken as
    /// <c>½ · F<sub>window→fin</sub></c>. A taller fin extending above the window
    /// would block more sky; a shorter / lower fin would block less. The current
    /// implementation does not account for fins that extend above or below the
    /// window's top/bottom edges.
    /// </remarks>
    public static double GetSkyBlockedFractionByVerticalFin(
        double windowWidth,
        double windowHeight,
        double finDepth,
        double lateralGap = 0.0)
    {
      double fWinToFin = GetViewFactorWindowToVerticalFin(
          windowWidth, windowHeight, finDepth, lateralGap);
      return 0.5 * fWinToFin;
    }

    /// <summary>
    /// Returns the combined sky-view-factor reduction caused by vertical fins
    /// on both sides of the window (each computed independently and summed).
    /// </summary>
    public static double GetSkyBlockedFractionByVerticalFinPair(
        double windowWidth,
        double windowHeight,
        double finDepth,
        double leftLateralGap = 0.0,
        double rightLateralGap = 0.0)
    {
      double left  = GetSkyBlockedFractionByVerticalFin(windowWidth, windowHeight, finDepth, leftLateralGap);
      double right = GetSkyBlockedFractionByVerticalFin(windowWidth, windowHeight, finDepth, rightLateralGap);
      return left + right;
    }

    #endregion

    #region 庇 + 縦フィンの組合せ (grid louver)

    /// <summary>
    /// Returns the combined sky-view-factor reduction for a grid (egg-crate)
    /// shading device consisting of a horizontal overhang above the window and
    /// vertical fins on each side. Contributions from the overhang and the two
    /// fins are summed independently; possible double-counting at the upper
    /// corners is neglected (small for typical geometries where the overhang
    /// and fins each occupy distinct angular regions).
    /// </summary>
    public static double GetSkyBlockedFractionByGrid(
        double windowWidth,
        double windowHeight,
        double depth,
        double topGap = 0.0,
        double leftLateralGap = 0.0,
        double rightLateralGap = 0.0)
    {
      double overhang = GetSkyBlockedFractionByOverhang(
          windowWidth, windowHeight, depth, topGap);
      double fins = GetSkyBlockedFractionByVerticalFinPair(
          windowWidth, windowHeight, depth, leftLateralGap, rightLateralGap);
      return overhang + fins;
    }

    #endregion

  }
}
