/* Conduit.cs
 * 
 * Copyright (C) 2016 E.Togashi
 * 
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 3 of the License, or (at
 * your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful, but
 * WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301, USA.
 */

using System;

using Popolo.Core.Numerics;

namespace Popolo.Core.HVAC.FluidCircuit
{
  /// <summary>Provides static methods for internal pipe flow calculations.</summary>
  /// <remarks>Common processing shared by pipes and ducts.</remarks>
  public static class Conduit
  {

    #region 列挙型定義

    /// <summary>Pipe material type.</summary>
    public enum Material
    {
      /// <summary>Carbon steel.</summary>
      UncoatedCarbonSteel_Clean,
      /// <summary>Polyvinyl chloride (PVC).</summary>
      PVC_PlasticPipe,
      /// <summary>Aluminum.</summary>
      Aluminum,
      /// <summary>Galvalume steel.</summary>
      GalvanizedSteel,
      /// <summary>Stainless steel.</summary>
      StainlessSteel,
      /// <summary>Sprayed glass wool insulation.</summary>
      FibrousGlassSpray,
      /// <summary>Flexible duct.</summary>
      FlexibleDuct,
      /// <summary>Glass wool duct.</summary>
      FibrousGlassDuct,
    }

    #endregion

    #region 等価直径関連の処理

    /// <summary>Computes the equivalent diameter [m].</summary>
    /// <param name="flowArea">Flow cross-sectional area [m²].</param>
    /// <param name="perimeterLength">Perimeter length [m].</param>
    /// <returns>Equivalent diameter [m].</returns>
    public static double GetEquivalentDiameter(double flowArea, double perimeterLength)
    { return 4 * flowArea / perimeterLength; }

    /// <summary>Computes the equivalent diameter of a rectangular duct [m].</summary>
    /// <param name="side1Length">Side 1 length [m].</param>
    /// <param name="side2Length">Side 2 length [m].</param>
    /// <returns>Equivalent diameter of the rectangular duct [m].</returns>
    /// <remarks>Based on Huebscher (1948).</remarks>
    public static double GetEquivalentDiameterOfRectangularDuct(double side1Length, double side2Length)
    {
      return 1.30 * Math.Pow(side1Length * side2Length, 0.625)
        / Math.Pow(side1Length + side2Length, 0.250);
    }

    /// <summary>Computes the equivalent diameter of an oval duct [m].</summary>
    /// <param name="majorLength">Major axis length [m].</param>
    /// <param name="minorLength">Minor axis length [m].</param>
    /// <returns>Equivalent diameter of the oval duct [m].</returns>
    /// <remarks>Based on Heyt and Diaz (1975).</remarks>
    public static double GetEquivalentDiameterOfOvalDuct(double majorLength, double minorLength)
    {
      double a = majorLength / 1000d;
      double b = minorLength / 1000d;
      double aa = (Math.PI * b * b / 4d) + b * (a - b);
      double p = Math.PI * b + 2d * (a - b);
      return 1.55 * Math.Pow(aa, 0.625) / Math.Pow(p, 0.250);
    }

    #endregion

    #region ダルシーワイスバッハ式関連の処理

    /// <summary>Computes the pressure drop [Pa] using the Darcy-Weisbach equation.</summary>
    /// <param name="frictionFactor">Darcy-Weisbach friction factor [-].</param>
    /// <param name="density">Fluid density [kg/m³].</param>
    /// <param name="length">Pipe length [m].</param>
    /// <param name="diameter">Inner diameter [m].</param>
    /// <param name="velocity">Mean pipe velocity [m/s].</param>
    /// <returns>Pressure loss [Pa].</returns>
    public static double GetPressureDrop
      (double frictionFactor, double density, double length, double diameter, double velocity)
    { return frictionFactor * density * length * velocity * velocity / (2 * diameter); }

    /// <summary>Computes the mean pipe velocity [m/s] using the Darcy-Weisbach equation.</summary>
    /// <param name="frictionFactor">Darcy-Weisbach friction factor [-].</param>
    /// <param name="density">Fluid density [kg/m³].</param>
    /// <param name="length">Pipe length [m].</param>
    /// <param name="diameter">Inner diameter [m].</param>
    /// <param name="pressureDrop">Pressure drop [Pa].</param>
    /// <returns>Mean pipe velocity [m/s].</returns>
    public static double GetVelocity
      (double frictionFactor, double density, double length, double diameter, double pressureDrop)
    {
      double pda = Math.Abs(pressureDrop);
      double vel = (2 * pda * diameter) / (frictionFactor * density * length);
      return Math.Sign(pressureDrop) * Math.Sqrt(vel);
    }

    #endregion

    #region 管摩擦係数関連の処理

    /// <summary>Gets the surface roughness of the material [m].</summary>
    /// <param name="mat">Material.</param>
    /// <returns>Surface roughness [m].</returns>
    /// <remarks>Note: the friction factor calculation uses the relative roughness [-] = roughness [m] / inner diameter [m].</remarks>
    public static double GetRoughness(Material mat)
    {
      switch (mat)
      {
        case Material.Aluminum:
          return 0.03e-3;
        case Material.FibrousGlassSpray:
          return 3.0e-3;
        case Material.FibrousGlassDuct:
          return 0.9e-3;
        case Material.FlexibleDuct:
          return 3.0e-3;
        case Material.GalvanizedSteel:
          return 0.09e-3;
        case Material.StainlessSteel:
          return 0;
        case Material.UncoatedCarbonSteel_Clean:
          return 0.03e-3;
        case Material.PVC_PlasticPipe:
          return 0.03e-3;
        default:
          return 0;
      }
    }

    /// <summary>Computes the Darcy-Weisbach friction factor [-].</summary>
    /// <param name="reynoldsNumber">Reynolds number [-].</param>
    /// <param name="relRoughness">Relative roughness [-] (roughness [m] / inner diameter [m]).</param>
    /// <returns>Darcy-Weisbach friction factor [-].</returns>
    public static double GetFrictionFactor(double reynoldsNumber, double relRoughness)
    {
      //層流の場合はレイノルズ数のみに依存
      if (reynoldsNumber < 4000) return 64d / reynoldsNumber;
      else
      {
        //遷移領域と仮定してCole brookの式を解く
        Roots.ErrorFunction eFnc = delegate (double fc)
        {
          double fcc = -2.0 * Math.Log10(relRoughness + 9.34 / (reynoldsNumber * Math.Sqrt(fc))) + 1.14;
          return fc - 1d / (fcc * fcc);
        };
        double fCoef = Roots.Newton(eFnc, 0.02, 0.0001, 0.000001, 0.0001, 10);
        double bnd = reynoldsNumber * Math.Sqrt(fCoef) * relRoughness;
        if (bnd <= 200) return fCoef;
        else
        {
          //完全に粗い場合（fully rough）にはNikuradseの式を解く
          fCoef = -2.0 * Math.Log10(relRoughness) + 1.14;
          return 1d / (fCoef * fCoef);
        }
      }
    }

    #endregion

  }
}
