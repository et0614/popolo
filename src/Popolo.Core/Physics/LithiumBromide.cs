/* LithiumBromide.cs
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
using Popolo.Core.Numerics;

namespace Popolo.Core.Physics
{
  /// <summary>
  /// Provides thermodynamic properties of lithium bromide (LiBr) aqueous solution.
  /// </summary>
  public class LithiumBromide
  {

    #region プロパティ

    /// <summary>Gets the specific enthalpy [kJ/kg].</summary>
    public double Enthalpy { get; private set; }

    /// <summary>Gets the liquid (solution) temperature [K].</summary>
    public double LiquidTemperature { get; private set; }

    /// <summary>Gets the equilibrium vapor temperature [K].</summary>
    public double VaporTemperature { get; private set; }

    /// <summary>Gets the mass fraction of LiBr [-].</summary>
    public double MassFraction { get; private set; }

    /// <summary>Gets the specific heat [kJ/(kg·K)].</summary>
    public double SpecificHeat { get; private set; }

    #endregion

    #region 近似係数（静的フィールド）

    /// <summary>比エンタルピー近似係数 C</summary>
    private static readonly double[] C_LBN =
        { -2024.33, 16330.9, -48816.1, 6.302948e4, -2.913705e4 };

    /// <summary>比エンタルピー近似係数 D（比熱の一次項）</summary>
    private static readonly double[] D_LBN =
        { 18.2829, -116.91757, 3.248041e2, -4.034184e2, 1.8520569e2 };

    /// <summary>比エンタルピー近似係数 E（比熱の二次項）</summary>
    private static readonly double[] E_LBN =
        { -3.7008214e-2, 2.8877666e-1, -8.1313015e-1, 9.9116628e-1, -4.4441207e-1 };

    #endregion

    #region 飽和温度・溶液温度・質量分率の計算

    /// <summary>
    /// Gets the equilibrium vapor temperature [K]
    /// from the liquid temperature [K] and mass fraction [-].
    /// </summary>
    /// <param name="liquidTemperature">Liquid (solution) temperature [K]</param>
    /// <param name="massFraction">Mass fraction of LiBr [-] (0 to 1)</param>
    /// <returns>Equilibrium vapor temperature [K]</returns>
    public static double GetVaporTemperatureFromLiquidTemperatureAndMassFraction(
        double liquidTemperature, double massFraction)
    {
      ValidateTemperatureK(liquidTemperature, nameof(liquidTemperature));
      ValidateMassFraction(massFraction, nameof(massFraction));
      GetCoefficientAB(massFraction, out double alb, out double blb);
      return alb + blb * liquidTemperature;
    }

    /// <summary>
    /// Gets the liquid temperature [K]
    /// from the equilibrium vapor temperature [K] and mass fraction [-].
    /// </summary>
    /// <param name="vaporTemperature">Equilibrium vapor temperature [K]</param>
    /// <param name="massFraction">Mass fraction of LiBr [-] (0 to 1)</param>
    /// <returns>Liquid (solution) temperature [K]</returns>
    public static double GetLiquidTemperatureFromVaporTemperatureAndMassFraction(
        double vaporTemperature, double massFraction)
    {
      ValidateTemperatureK(vaporTemperature, nameof(vaporTemperature));
      ValidateMassFraction(massFraction, nameof(massFraction));
      GetCoefficientAB(massFraction, out double alb, out double blb);
      return (vaporTemperature - alb) / blb;
    }

    /// <summary>
    /// Gets the mass fraction of LiBr [-]
    /// from the liquid temperature [K] and equilibrium vapor temperature [K].
    /// </summary>
    /// <param name="liquidTemperature">Liquid (solution) temperature [K]</param>
    /// <param name="vaporTemperature">Equilibrium vapor temperature [K]</param>
    /// <returns>Mass fraction of LiBr [-]</returns>
    public static double GetMassFractionFromLiquidTemperatureAndVaporTemperature(
        double liquidTemperature, double vaporTemperature)
    {
      ValidateTemperatureK(liquidTemperature, nameof(liquidTemperature));
      ValidateTemperatureK(vaporTemperature, nameof(vaporTemperature));
      Roots.ErrorFunction eFnc = mf =>
          vaporTemperature - GetVaporTemperatureFromLiquidTemperatureAndMassFraction(
              liquidTemperature, mf);
      return Roots.Newton(eFnc, 0.5, 0.001, 0.00001, 0.00001, 20);
    }

    #endregion

    #region 比エンタルピーの計算

    /// <summary>
    /// Gets the specific enthalpy [kJ/kg]
    /// from the liquid temperature [K] and mass fraction [-].
    /// </summary>
    /// <param name="liquidTemperature">Liquid (solution) temperature [K]</param>
    /// <param name="massFraction">Mass fraction of LiBr [-] (0 to 1)</param>
    /// <returns>Specific enthalpy of the solution [kJ/kg]</returns>
    public static double GetEnthalpyFromLiquidTemperatureAndMassFraction(
        double liquidTemperature, double massFraction)
    {
      ValidateTemperatureK(liquidTemperature, nameof(liquidTemperature));
      ValidateMassFraction(massFraction, nameof(massFraction));
      GetCoefficientCDE(massFraction, out double clb, out double dlb, out double elb);
      // 近似式は°C基準のため変換する
      double ltc = PhysicsConstants.ToCelsius(liquidTemperature);
      return clb + ltc * (dlb + ltc * elb);
    }

    /// <summary>
    /// Gets the specific enthalpy [kJ/kg]
    /// from the liquid temperature [K] and equilibrium vapor temperature [K].
    /// </summary>
    /// <param name="liquidTemperature">Liquid (solution) temperature [K]</param>
    /// <param name="vaporTemperature">Equilibrium vapor temperature [K]</param>
    /// <returns>Specific enthalpy of the solution [kJ/kg]</returns>
    public static double GetEnthalpyFromLiquidTemperatureAndVaporTemperature(
        double liquidTemperature, double vaporTemperature)
    {
      double mf = GetMassFractionFromLiquidTemperatureAndVaporTemperature(
          liquidTemperature, vaporTemperature);
      return GetEnthalpyFromLiquidTemperatureAndMassFraction(liquidTemperature, mf);
    }

    /// <summary>
    /// Gets the liquid temperature [K]
    /// from the specific enthalpy [kJ/kg] and mass fraction [-].
    /// </summary>
    /// <param name="enthalpy">Specific enthalpy of the solution [kJ/kg]</param>
    /// <param name="massFraction">Mass fraction of LiBr [-] (0 to 1)</param>
    /// <returns>Liquid (solution) temperature [K]</returns>
    public static double GetLiquidTemperatureFromEnthalpyAndMassFraction(
        double enthalpy, double massFraction)
    {
      ValidateMassFraction(massFraction, nameof(massFraction));
      GetCoefficientCDE(massFraction, out double clb, out double dlb, out double elb);

      //ニュートン法で収束計算（近似式は°C基準）
      Roots.ErrorFunction eFnc = lTemp =>
          enthalpy - (clb + lTemp * (dlb + lTemp * elb));
      double ltc = Roots.Newton(eFnc, 40, 0.001, 0.00001, 0.00001, 20);
      return PhysicsConstants.ToKelvin(ltc);
    }

    /// <summary>
    /// Gets the mass fraction of LiBr [-]
    /// from the specific enthalpy [kJ/kg] and equilibrium vapor temperature [K].
    /// </summary>
    /// <param name="enthalpy">Specific enthalpy of the solution [kJ/kg]</param>
    /// <param name="vaporTemperature">Equilibrium vapor temperature [K]</param>
    /// <returns>Mass fraction of LiBr [-]</returns>
    public static double GetMassFractionFromEnthalpyAndVaporTemperature(
        double enthalpy, double vaporTemperature)
    {
      ValidateTemperatureK(vaporTemperature, nameof(vaporTemperature));
      Roots.ErrorFunction eFnc = mf =>
          GetLiquidTemperatureFromEnthalpyAndMassFraction(enthalpy, mf)
          - GetLiquidTemperatureFromVaporTemperatureAndMassFraction(vaporTemperature, mf);
      return Roots.Newton(eFnc, 0.5, 0.001, 0.00001, 0.00001, 20);
    }

    #endregion

    #region その他の物性値

    /// <summary>
    /// Gets the density [kg/m³]
    /// from the liquid temperature [K] and mass fraction [-].
    /// </summary>
    /// <param name="liquidTemperature">Liquid (solution) temperature [K]</param>
    /// <param name="massFraction">Mass fraction of LiBr [-] (0 to 1)</param>
    /// <returns>Density of the solution [kg/m³]</returns>
    public static double GetDensity(double liquidTemperature, double massFraction)
    {
      ValidateTemperatureK(liquidTemperature, nameof(liquidTemperature));
      ValidateMassFraction(massFraction, nameof(massFraction));
      const double LBD = 3460.0; //臭化リチウムの密度[kg/m3]
                                 // Water.GetLiquidDensity は°C を受け取るため変換する（バグ修正）
      double wd = Water.GetLiquidDensity(PhysicsConstants.ToCelsius(liquidTemperature));
      return LBD * massFraction + wd * (1.0 - massFraction);
    }

    /// <summary>
    /// Gets the specific heat [kJ/(kg·K)]
    /// from the liquid temperature [K] and mass fraction [-].
    /// </summary>
    /// <param name="liquidTemperature">Liquid (solution) temperature [K]</param>
    /// <param name="massFraction">Mass fraction of LiBr [-] (0 to 1)</param>
    /// <returns>Specific heat of the solution [kJ/(kg·K)]</returns>
    public static double GetSpecificHeat(double liquidTemperature, double massFraction)
    {
      ValidateTemperatureK(liquidTemperature, nameof(liquidTemperature));
      ValidateMassFraction(massFraction, nameof(massFraction));
      double dlb = D_LBN[4];
      double elb = E_LBN[4];
      for (int i = 3; 0 <= i; i--)
      {
        dlb = dlb * massFraction + D_LBN[i];
        elb = elb * massFraction + E_LBN[i];
      }
      //近似式は°C基準
      return dlb + 2.0 * elb * PhysicsConstants.ToCelsius(liquidTemperature);
    }

    #endregion

    #region ファクトリメソッド

    /// <summary>
    /// Creates a <see cref="LithiumBromide"/> instance
    /// from the liquid temperature [K] and mass fraction [-].
    /// </summary>
    /// <param name="liquidTemperature">Liquid (solution) temperature [K]</param>
    /// <param name="massFraction">Mass fraction of LiBr [-] (0 to 1)</param>
    /// <returns>A new <see cref="LithiumBromide"/> instance.</returns>
    public static LithiumBromide MakeFromLiquidTemperatureAndMassFraction(
        double liquidTemperature, double massFraction)
    {
      var lb = new LithiumBromide();
      lb.LiquidTemperature = liquidTemperature;
      lb.MassFraction = massFraction;
      lb.VaporTemperature = GetVaporTemperatureFromLiquidTemperatureAndMassFraction(
          liquidTemperature, massFraction);
      lb.Enthalpy = GetEnthalpyFromLiquidTemperatureAndMassFraction(
          liquidTemperature, massFraction);
      lb.SpecificHeat = GetSpecificHeat(liquidTemperature, massFraction);
      return lb;
    }

    /// <summary>
    /// Creates a <see cref="LithiumBromide"/> instance
    /// from the equilibrium vapor temperature [K] and mass fraction [-].
    /// </summary>
    /// <param name="vaporTemperature">Equilibrium vapor temperature [K]</param>
    /// <param name="massFraction">Mass fraction of LiBr [-] (0 to 1)</param>
    /// <returns>A new <see cref="LithiumBromide"/> instance.</returns>
    public static LithiumBromide MakeFromVaporTemperatureAndMassFraction(
        double vaporTemperature, double massFraction)
    {
      var lb = new LithiumBromide();
      lb.VaporTemperature = vaporTemperature;
      lb.MassFraction = massFraction;
      lb.LiquidTemperature = GetLiquidTemperatureFromVaporTemperatureAndMassFraction(
          vaporTemperature, massFraction);
      lb.Enthalpy = GetEnthalpyFromLiquidTemperatureAndMassFraction(
          lb.LiquidTemperature, massFraction);
      lb.SpecificHeat = GetSpecificHeat(lb.LiquidTemperature, massFraction);
      return lb;
    }

    /// <summary>
    /// Creates a <see cref="LithiumBromide"/> instance
    /// from the liquid temperature [K] and equilibrium vapor temperature [K].
    /// </summary>
    /// <param name="liquidTemperature">Liquid (solution) temperature [K]</param>
    /// <param name="vaporTemperature">Equilibrium vapor temperature [K]</param>
    /// <returns>A new <see cref="LithiumBromide"/> instance.</returns>
    public static LithiumBromide MakeFromLiquidTemperatureAndVaporTemperature(
        double liquidTemperature, double vaporTemperature)
    {
      double mf = GetMassFractionFromLiquidTemperatureAndVaporTemperature(
          liquidTemperature, vaporTemperature);
      return MakeFromLiquidTemperatureAndMassFraction(liquidTemperature, mf);
    }

    /// <summary>
    /// Creates a <see cref="LithiumBromide"/> instance
    /// from the specific enthalpy [kJ/kg] and mass fraction [-].
    /// </summary>
    /// <param name="enthalpy">Specific enthalpy of the solution [kJ/kg]</param>
    /// <param name="massFraction">Mass fraction of LiBr [-] (0 to 1)</param>
    /// <returns>A new <see cref="LithiumBromide"/> instance.</returns>
    public static LithiumBromide MakeFromEnthalpyAndMassFraction(
        double enthalpy, double massFraction)
    {
      double lt = GetLiquidTemperatureFromEnthalpyAndMassFraction(enthalpy, massFraction);
      return MakeFromLiquidTemperatureAndMassFraction(lt, massFraction);
    }

    /// <summary>
    /// Creates a <see cref="LithiumBromide"/> instance
    /// from the specific enthalpy [kJ/kg] and equilibrium vapor temperature [K].
    /// </summary>
    /// <param name="enthalpy">Specific enthalpy of the solution [kJ/kg]</param>
    /// <param name="vaporTemperature">Equilibrium vapor temperature [K]</param>
    /// <returns>A new <see cref="LithiumBromide"/> instance.</returns>
    public static LithiumBromide MakeFromEnthalpyAndVaporTemperature(
        double enthalpy, double vaporTemperature)
    {
      double mf = GetMassFractionFromEnthalpyAndVaporTemperature(enthalpy, vaporTemperature);
      return MakeFromVaporTemperatureAndMassFraction(vaporTemperature, mf);
    }

    #endregion

    #region 非公開メソッド

    /// <summary>飽和温度計算用の係数 a, b を求める</summary>
    private static void GetCoefficientAB(
        double massFraction, out double ca, out double cb)
    {
      const double a0 = -22.8937; const double a1 = 152.554;
      const double a2 = -254.786; const double a3 = 152.949;
      const double a4 = -171.599;
      const double b0 = 1.09851; const double b1 = -0.394508;

      double mf = Math.Min(1.0, Math.Max(0.0, massFraction));
      ca = a0 + mf * (a1 + mf * (a2 + mf * (a3 + mf * mf * mf * mf * a4)));
      cb = b0 + mf * b1;
    }

    /// <summary>エンタルピー・比熱計算用の係数 c, d, e を求める</summary>
    private static void GetCoefficientCDE(
        double massFraction, out double cc, out double cd, out double ce)
    {
      massFraction = Math.Min(1.0, massFraction);
      cc = C_LBN[4]; cd = D_LBN[4]; ce = E_LBN[4];
      for (int i = 3; 0 <= i; i--)
      {
        cc = cc * massFraction + C_LBN[i];
        cd = cd * massFraction + D_LBN[i];
        ce = ce * massFraction + E_LBN[i];
      }
    }

    /// <summary>温度 [K] の下限チェック（絶対零度以上）</summary>
    private static void ValidateTemperatureK(double temperatureK, string paramName)
    {
      if (temperatureK < 0)
        throw new PopoloOutOfRangeException(paramName, temperatureK, 0.0, null,
            "Temperature in Kelvin must be non-negative.");
    }

    /// <summary>質量分率の範囲チェック（0〜1）</summary>
    private static void ValidateMassFraction(double massFraction, string paramName)
    {
      if (massFraction < 0.0 || massFraction > 1.0)
        throw new PopoloOutOfRangeException(paramName, massFraction, 0.0, 1.0);
    }

    /// <summary>コンストラクタ（外部からのインスタンス生成を禁止）</summary>
    private LithiumBromide() { }

    #endregion

  }
}
