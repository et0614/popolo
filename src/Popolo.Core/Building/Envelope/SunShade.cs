/* SunShade.cs
 * 
 * Copyright (C) 2016 E.Togashi
 * 
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 2 of the License, or (at
 * your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful, but
 * WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 675 Mass Ave, Cambridge, MA 02139, USA.
 */

using System;
using Popolo.Core.Climate;

namespace Popolo.Core.Building.Envelope
{
  /// <summary>Represents an exterior solar shading device (overhang, fin, louver, etc.)
  /// attached to a window or wall surface.</summary>
  public class SunShade
  {

    #region 列挙型

    /// <summary>Specifies the shape of the solar shading device.</summary>
    public enum Shapes
    {
      /// <summary>No shading device.</summary>
      None = 0,
      /// <summary>Horizontal overhang (finite length).</summary>
      Horizontal = 1,
      /// <summary>Horizontal overhang (infinite length).</summary>
      LongHorizontal = 2,
      /// <summary>Vertical side fin, left side (finite length).</summary>
      VerticalLeft = 3,
      /// <summary>Vertical side fin, right side (finite length).</summary>
      VerticalRight = 4,
      /// <summary>Vertical side fins, both sides (finite length).</summary>
      VerticalBoth = 5,
      /// <summary>Vertical side fin, left side (infinite length).</summary>
      LongVerticalLeft = 6,
      /// <summary>Vertical side fin, right side (infinite length).</summary>
      LongVerticalRight = 7,
      /// <summary>Vertical side fins, both sides (infinite length).</summary>
      LongVerticalBoth = 8,
      /// <summary>Grid (egg-crate) louver.</summary>
      Grid = 9
    }

    #endregion

    #region プロパティ

    /// <summary>Gets the tilted surface to which this shading device is attached.</summary>
    public IReadOnlyIncline Incline { get; internal set; }

    /// <summary>Gets the shape of the shading device.</summary>
    public Shapes Shape { private set; get; }

    /// <summary>Gets the window height [m].</summary>
    public double WinHeight { private set; get; }

    /// <summary>Gets the window width [m].</summary>
    public double WinWidth { private set; get; }

    /// <summary>Gets the projection depth of the shading device [m].</summary>
    public double Overhang { private set; get; }

    /// <summary>Gets the distance from the top of the shading device to the top of the window [m].</summary>
    public double TopMargin { private set; get; }

    /// <summary>Gets the distance from the bottom of the shading device to the bottom of the window [m].</summary>
    public double BottomMargin { private set; get; }

    /// <summary>Gets the distance from the left edge of the shading device to the left edge of the window [m].</summary>
    public double LeftMargin { private set; get; }

    /// <summary>Gets the distance from the right edge of the shading device to the right edge of the window [m].</summary>
    public double RightMargin { private set; get; }

    #endregion

    #region コンストラクタ

    /// <summary>Initializes a new instance with full geometry parameters.</summary>
    /// <param name="shape">Shading device shape.</param>
    /// <param name="incline">Tilted surface to which the device is attached.</param>
    /// <param name="winHeight">Window height [m].</param>
    /// <param name="winWidth">Window width [m].</param>
    /// <param name="overhang">Projection depth [m].</param>
    /// <param name="topMargin">Top margin [m].</param>
    /// <param name="bottomMargin">Bottom margin [m].</param>
    /// <param name="leftMargin">Left margin [m].</param>
    /// <param name="rightMargin">Right margin [m].</param>
    internal SunShade(
      Shapes shape, IReadOnlyIncline incline,
      double winHeight, double winWidth, double overhang, 
      double topMargin, double bottomMargin, double leftMargin, double rightMargin) 
    {
      Shape = shape;
      Incline = incline;
      WinHeight = winHeight;
      WinWidth = winWidth;
      Overhang = overhang;
      TopMargin = topMargin;
      BottomMargin = bottomMargin;
      LeftMargin = leftMargin;
      RightMargin = rightMargin;
    }

    /// <summary>Private constructor for use by factory methods.</summary>
    private SunShade(IReadOnlyIncline incline)
    {
      Incline = incline;
    }

    /// <summary>Copy constructor.</summary>
    /// <param name="sShade">The source shading device to copy.</param>
    public SunShade(SunShade sShade) 
    {
      Shape = sShade.Shape;
      Incline = new Incline(sShade.Incline);
      WinHeight = sShade.WinHeight;
      WinWidth = sShade.WinWidth;
      Overhang = sShade.Overhang;
      TopMargin = sShade.TopMargin;
      BottomMargin = sShade.BottomMargin;
      LeftMargin = sShade.LeftMargin;
      RightMargin = sShade.RightMargin;
    }

    #endregion

    #region インスタンスメソッド

    /// <summary>Gets the shadow area ratio [-] on the window for the given solar position.</summary>
    /// <param name="sun">Solar state.</param>
    /// <returns>Shadow area ratio [-] (0 = fully sunlit, 1 = fully shaded).</returns>
    public double GetShadowRate(IReadOnlySun sun)
    {
      //日の出前と日没後はすべて影      
      if (sun.Altitude <= 0) return 1;
      //日除けが無ければ影は無し
      if (Shape == Shapes.None) return 0;
      //太陽が裏面にある場合にはすべて影
      if (Incline.GetDirectSolarRadiationRate(sun) <= 0) return 1;

      double dpW = Overhang * Math.Tan(Incline.HorizontalAngle - sun.Orientation);
      double dpH = Overhang * Incline.GetTangentProfileAngle(sun);
      double sr = 0;
      switch (Shape)
      {
        case Shapes.Horizontal:
          sr = ComputeShadowArea_H(dpW, dpH, WinWidth, WinHeight, LeftMargin, TopMargin, RightMargin);
          break;
        case Shapes.VerticalLeft:
          sr = ComputeShadowArea_H(dpH, dpW, WinHeight, WinWidth, TopMargin, LeftMargin, BottomMargin);
          break;
        case Shapes.VerticalRight:
          sr = ComputeShadowArea_H(dpH, -dpW, WinHeight, WinWidth, TopMargin, RightMargin, BottomMargin);
          break;
        case Shapes.VerticalBoth:
          if (dpW < 0)
            sr = ComputeShadowArea_H(dpH, -dpW, WinHeight, WinWidth, TopMargin, RightMargin, BottomMargin);
          else sr = ComputeShadowArea_H(dpH, dpW, WinHeight, WinWidth, TopMargin, LeftMargin, BottomMargin);
          break;
        case Shapes.LongHorizontal:
          sr = ComputeShadowArea_LH(dpH, WinWidth, WinHeight, TopMargin);
          break;
        case Shapes.LongVerticalLeft:
          sr = ComputeShadowArea_LH(dpW, WinHeight, WinWidth, LeftMargin);
          break;
        case Shapes.LongVerticalRight:
          sr = ComputeShadowArea_LH(-dpW, WinHeight, WinWidth, RightMargin);
          break;
        case Shapes.LongVerticalBoth:
          if (dpW < 0) sr = ComputeShadowArea_LH(-dpW, WinHeight, WinWidth, RightMargin);
          else sr = ComputeShadowArea_LH(dpW, WinHeight, WinWidth, LeftMargin);
          break;
        case Shapes.Grid:
          sr = ComputeShadowArea_Grid
            (dpW, dpH, WinWidth, WinHeight, LeftMargin, TopMargin, RightMargin, BottomMargin);
          break;
      }
      return sr / (WinHeight * WinWidth);
    }

    #endregion

    #region 初期化処理

    /// <summary>Creates a <see cref="SunShade"/> instance with no shading effect.</summary>
    /// <returns>A <see cref="SunShade"/> with shape <see cref="Shapes.None"/>.</returns>
    public static SunShade MakeEmptySunShade()
    {
      SunShade ss = new SunShade(new Incline(0d, 0d));
      ss.Shape = Shapes.None;
      return ss;
    }

    /// <summary>Creates an infinite-length horizontal overhang.</summary>
    /// <param name="wWidth">Window width [m].</param>
    /// <param name="wHeight">Window height [m].</param>
    /// <param name="depth">Projection depth [m].</param>
    /// <param name="tMargin">Top margin [m].</param>
    /// <param name="incline">Tilted surface.</param>
    /// <returns>A <see cref="SunShade"/> representing an infinite horizontal overhang.</returns>
    public static SunShade MakeHorizontalSunShade
      (double wWidth, double wHeight, double depth, double tMargin, IReadOnlyIncline incline)
    {
      SunShade ss = new SunShade(incline);
      ss.Shape = Shapes.LongHorizontal;
      ss.WinWidth = wWidth;
      ss.WinHeight = wHeight;
      ss.Overhang = depth;
      ss.TopMargin = tMargin;
      return ss;
    }

    /// <summary>Creates a finite-length horizontal overhang.</summary>
    /// <param name="wWidth">Window width [m].</param>
    /// <param name="wHeight">Window height [m].</param>
    /// <param name="depth">Projection depth [m].</param>
    /// <param name="lMargin">Left margin [m].</param>
    /// <param name="rMargin">Right margin [m].</param>
    /// <param name="tMargin">Top margin [m].</param>
    /// <param name="incline">Tilted surface.</param>
    /// <returns>A <see cref="SunShade"/> representing a horizontal overhang.</returns>
    public static SunShade MakeHorizontalSunShade(double wWidth, double wHeight, double depth, 
      double lMargin, double rMargin, double tMargin, IReadOnlyIncline incline)
    {
      SunShade ss = MakeHorizontalSunShade(wWidth, wHeight, depth, tMargin, incline);
      ss.Shape = Shapes.Horizontal;
      ss.LeftMargin = lMargin;
      ss.RightMargin = rMargin;
      return ss;
    }

    /// <summary>Creates an infinite-length vertical side fin.</summary>
    /// <param name="wWidth">Window width [m].</param>
    /// <param name="wHeight">Window height [m].</param>
    /// <param name="depth">Projection depth [m].</param>
    /// <param name="sMargin">Side margin [m].</param>
    /// <param name="isLeftSide">True for the left side fin; false for the right.</param>
    /// <param name="incline">Tilted surface.</param>
    /// <returns>A <see cref="SunShade"/> representing an infinite vertical fin.</returns>
    public static SunShade MakeVerticalSunShade(double wWidth, double wHeight, double depth, 
      double sMargin, bool isLeftSide, IReadOnlyIncline incline)
    {
      SunShade ss = new SunShade(incline);
      ss.WinWidth = wWidth;
      ss.WinHeight = wHeight;
      ss.Overhang = depth;
      if (isLeftSide)
      {
        ss.Shape = Shapes.LongVerticalLeft;
        ss.LeftMargin = sMargin;
      }
      else
      {
        ss.Shape = Shapes.LongVerticalRight;
        ss.RightMargin = sMargin;
      }
      return ss;
    }

    /// <summary>Creates a finite-length vertical side fin.</summary>
    /// <param name="wWidth">Window width [m].</param>
    /// <param name="wHeight">Window height [m].</param>
    /// <param name="depth">Projection depth [m].</param>
    /// <param name="sMargin">Side margin [m].</param>
    /// <param name="isLeftSide">True for the left side fin; false for the right.</param>
    /// <param name="tMargin">Top margin [m].</param>
    /// <param name="bMargin">Bottom margin [m].</param>
    /// <param name="incline">Tilted surface.</param>
    /// <returns>A <see cref="SunShade"/> representing a vertical side fin.</returns>
    public static SunShade MakeVerticalSunShade(double wWidth, double wHeight, double depth, 
      double sMargin, bool isLeftSide, double tMargin, double bMargin, IReadOnlyIncline incline)
    {
      SunShade ss = MakeVerticalSunShade(wWidth, wHeight, depth, sMargin, isLeftSide, incline);
      if (isLeftSide) ss.Shape = Shapes.VerticalLeft;
      else ss.Shape = Shapes.VerticalRight;
      ss.TopMargin = tMargin;
      ss.BottomMargin = bMargin;
      return ss;
    }

    /// <summary>Creates infinite-length vertical side fins on both sides.</summary>
    /// <param name="wWidth">Window width [m].</param>
    /// <param name="wHeight">Window height [m].</param>
    /// <param name="depth">Projection depth [m].</param>
    /// <param name="lMargin">Left margin [m].</param>
    /// <param name="rMargin">Right margin [m].</param>
    /// <param name="incline">Tilted surface.</param>
    /// <returns>A <see cref="SunShade"/> representing infinite fins on both sides.</returns>
    public static SunShade MakeVerticalSunShade(double wWidth, double wHeight, double depth, 
      double lMargin, double rMargin, IReadOnlyIncline incline)
    {
      SunShade ss = MakeVerticalSunShade(wWidth, wHeight, depth, lMargin, true, incline);
      ss.RightMargin = rMargin;
      ss.Shape = Shapes.LongVerticalBoth;
      return ss;
    }

    /// <summary>Creates finite-length vertical side fins on both sides.</summary>
    /// <param name="wWidth">Window width [m].</param>
    /// <param name="wHeight">Window height [m].</param>
    /// <param name="depth">Projection depth [m].</param>
    /// <param name="lMargin">Left margin [m].</param>
    /// <param name="rMargin">Right margin [m].</param>
    /// <param name="tMargin">Top margin [m].</param>
    /// <param name="bMargin">Bottom margin [m].</param>
    /// <param name="incline">Tilted surface.</param>
    /// <returns>A <see cref="SunShade"/> representing vertical fins on both sides.</returns>
    public static SunShade MakeVerticalSunShade(double wWidth, double wHeight, double depth, 
      double lMargin, double rMargin, double tMargin, double bMargin, IReadOnlyIncline incline)
    {
      SunShade ss = MakeVerticalSunShade(wWidth, wHeight, depth, lMargin, rMargin, incline);
      ss.Shape = Shapes.VerticalBoth;
      ss.TopMargin = tMargin;
      ss.BottomMargin = bMargin;
      return ss;
    }

    /// <summary>Creates a grid (egg-crate) louver.</summary>
    /// <param name="wWidth">Window width [m].</param>
    /// <param name="wHeight">Window height [m].</param>
    /// <param name="depth">Projection depth [m].</param>
    /// <param name="lMargin">Left margin [m].</param>
    /// <param name="rMargin">Right margin [m].</param>
    /// <param name="tMargin">Top margin [m].</param>
    /// <param name="bMargin">Bottom margin [m].</param>
    /// <param name="incline">Tilted surface.</param>
    /// <returns>A <see cref="SunShade"/> representing a grid louver.</returns>
    public static SunShade MakeGridSunShade(double wWidth, double wHeight, double depth, 
      double lMargin, double rMargin, double tMargin, double bMargin, IReadOnlyIncline incline)
    {
      SunShade ss = new SunShade(incline);
      ss.Shape = Shapes.Grid;
      ss.WinWidth = wWidth;
      ss.WinHeight = wHeight;
      ss.Overhang = depth;
      ss.LeftMargin = lMargin;
      ss.RightMargin = rMargin;
      ss.TopMargin = tMargin;
      ss.BottomMargin = bMargin;
      return ss;
    }

    #endregion

    #region private staticメソッド

    /// <summary>Computes the shadow area [m²] cast by a finite horizontal overhang.</summary>
    /// <param name="dpW">庇端部から影端部までの水平距離[m]</param>
    /// <param name="dpH">庇端部から影端部までの垂直距離[m]</param>
    /// <param name="dwW">窓幅[m]</param>
    /// <param name="dwH">窓高さ[m]</param>
    /// <param name="dmWA">庇と窓の左端距離[m]</param>
    /// <param name="dmH">庇と窓の上端距離[m]</param>
    /// <param name="dmWB">庇と窓の右端距離[m]</param>
    /// <returns>Shadow area [m²].</returns>
    private static double ComputeShadowArea_H
      (double dpW, double dpH, double dwW, double dwH, double dmWA, double dmH, double dmWB)
    {
      if (dpH <= dmH) return 0;

      double dmW;
      if (0 < dpW) dmW = dmWA;
      else dmW = dmWB;
      dpW = Math.Abs(dpW);

      double dshHA, dshHB, dshWA, dshWB;
      double dshHAd, dshHBd, dshWAd, dshWBd;
      if (dmW < dpW) dshHAd = dmW * (dpH / dpW) - dmH;
      else dshHAd = dpH - dmH;
      dshHA = Math.Min(dwH, Math.Max(0, dshHAd));
      if (dmW + dwW < dpW) dshHBd = (dmW + dwW) * (dpH / dpW) - dmH;
      else dshHBd = dpH - dmH;
      dshHB = Math.Min(dwH, Math.Max(0, dshHBd));
      dshWAd = (dmW + dwW) - dmH * (dpW / dpH);
      dshWA = Math.Min(dwW, Math.Max(0, dshWAd));
      if (dpH < dmH + dwH) dshWBd = (dmW + dwW) - dpW;
      else dshWBd = (dmW + dwW) - (dmH + dwH) * dpW / dpH;
      dshWB = Math.Min(dwW, Math.Max(0, dshWBd));

      return dshWA * dshHA + 0.5 * (dshWA + dshWB) * (dshHB - dshHA);
    }

    /// <summary>Computes the shadow area [m²] cast by an infinite-length horizontal overhang.</summary>
    /// <param name="dpH">庇端部から影端部までの垂直距離[m]</param>
    /// <param name="dwW">窓幅[m]</param>
    /// <param name="dwH">窓高さ[m]</param>
    /// <param name="dmH">庇と窓の上端距離[m]</param>
    /// <returns>Shadow area [m²].</returns>
    private static double ComputeShadowArea_LH(double dpH, double dwW, double dwH, double dmH)
    {
      if (dpH <= dmH) return 0;
      return dwW * Math.Min(dpH - dmH, dwH);
    }

    /// <summary>Computes the shadow area [m²] cast by a grid (egg-crate) louver.</summary>
    /// <param name="dpW">庇端部から影端部までの水平距離[m]</param>
    /// <param name="dpH">庇端部から影端部までの垂直距離[m]</param>
    /// <param name="dwW">窓幅[m]</param>
    /// <param name="dwH">窓高さ[m]</param>
    /// <param name="dmWA">庇と窓の左端距離[m]</param>
    /// <param name="dmHA">庇と窓の上端距離[m]</param>
    /// <param name="dmWB">庇と窓の右端距離[m]</param>
    /// <param name="dmHB">庇と窓の下端距離[m]</param>
    /// <returns>Shadow area [m²].</returns>
    private static double ComputeShadowArea_Grid
      (double dpW, double dpH, double dwW, double dwH, double dmWA, double dmHA, double dmWB, double dmHB)
    {
      double dWA = Math.Min(Math.Max(0, dpW - dmWA), dwW);
      double dWB = Math.Min(Math.Max(0, -(dpW + dmWB)), dwW);
      double dHA = Math.Min(Math.Max(0, dpH - dmHA), dwH);
      double dHB = Math.Min(Math.Max(0, -(dpH + dmHB)), dwH);
      return dwW * (dHA + dHB) + (dWA + dWB) * (dwH - dHA - dHB);
    }

    #endregion

  }
}
