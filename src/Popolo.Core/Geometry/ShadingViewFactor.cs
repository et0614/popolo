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

  }
}
