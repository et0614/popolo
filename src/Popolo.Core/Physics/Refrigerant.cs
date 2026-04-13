/* Refrigerant.cs
 *
 * Copyright (C) 2013 E.Togashi
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
  /// Provides thermophysical property calculations for refrigerants
  /// based on a PVT equation of state developed for building energy simulation.
  /// </summary>
  /// <remarks>
  /// The equation of state and approximation coefficients are based on:
  /// Togashi, E., "Development of Equation of State for the Thermodynamic Properties
  /// of HFC32 (R32) for the Purpose of Annual Building Energy Simulation,"
  /// Transactions of the Society of Heating, Air-conditioning and Sanitary Engineers of Japan
  /// DOI: 10.18948/shase.39.204_69
  ///
  /// Supported refrigerants and valid pressure ranges (approximation fitting range):
  /// - R32   : 700 to 4500 kPa
  /// - R410A : 700 to 4000 kPa
  /// - R134a : 200 to 1500 kPa
  ///
  /// Reference state follows ASHRAE/IIR convention:
  /// saturated liquid at 0 °C → enthalpy = 200 kJ/kg, entropy = 1.0 kJ/(kg·K).
  /// </remarks>
  public class Refrigerant
  {

    #region 列挙型

    /// <summary>
    /// Specifies the type of refrigerant.
    /// </summary>
    public enum Fluid
    {
      /// <summary>R410A (pseudo-azeotropic HFC mixture)</summary>
      R410A,
      /// <summary>R32 (difluoromethane, HFC-32)</summary>
      R32,
      /// <summary>R134a (1,1,1,2-tetrafluoroethane, HFC-134a)</summary>
      R134a
    }

    /// <summary>
    /// Specifies the thermodynamic phase of the refrigerant.
    /// </summary>
    public enum Phase
    {
      /// <summary>Superheated vapor (gas phase)</summary>
      Vapor,
      /// <summary>Subcooled liquid (liquid phase)</summary>
      Liquid,
      /// <summary>Two-phase equilibrium (vapor-liquid coexistence)</summary>
      Equilibrium
    }

    #endregion

    #region プロパティ

    /// <summary>Gets the type of refrigerant.</summary>
    public Fluid FluidType { get; private set; }

    /// <summary>
    /// Gets the maximum pressure [kPa] of the valid range
    /// (approximation fitting range from the reference paper, Fig.3).
    /// </summary>
    public double MaxPressure { get; private set; }

    /// <summary>
    /// Gets the minimum pressure [kPa] of the valid range
    /// (approximation fitting range from the reference paper, Fig.3).
    /// </summary>
    public double MinPressure { get; private set; }

    /// <summary>Gets the critical temperature [K].</summary>
    public double CriticalTemperature { get; private set; }

    /// <summary>Gets the critical density [kg/m³].</summary>
    public double CriticalDensity { get; private set; }

    /// <summary>Gets the critical pressure [kPa].</summary>
    public double CriticalPressure { get; private set; }

    #endregion

    #region 近似係数（プライベートフィールド）

    /// <summary>近似係数 m の数</summary>
    private readonly int _mNumber;

    /// <summary>近似係数 n の数</summary>
    private readonly int _nNumber;

    /// <summary>ガス定数 [kJ/(kg·K)]</summary>
    private readonly double _gasConstant;

    /// <summary>基準状態の温度 [K]</summary>
    private readonly double _refTemperature;

    /// <summary>基準状態の密度 [kg/m³]</summary>
    private readonly double _refDensity;

    /// <summary>基準状態のエンタルピー [kJ/kg]</summary>
    private readonly double _refEnthalpy;

    /// <summary>基準状態のエントロピー [kJ/(kg·K)]</summary>
    private readonly double _refEntropy;

    /// <summary>PVT近似係数 α[m,n]</summary>
    private readonly double[,] _alpha;

    /// <summary>理想気体定圧比熱の近似係数</summary>
    private readonly double[] _ccp;

    /// <summary>飽和圧力初期値推定用の近似係数</summary>
    private readonly double[] _cps;

    /// <summary>飽和温度初期値推定用の近似係数</summary>
    private readonly double[] _cts;

    #endregion

    #region 近似係数（静的データ）

    // R32 coefficients (Togashi 2014, Table 2-4)
    private static readonly double[] AlphaR32 = {
            9.7665906E+03,  3.4609053E+04, -4.1024673E+04,  1.1966225E+04, -1.6994460E+02,  1.9091076E+02,
            1.0932188E+03, -8.0842415E+03,  1.4466692E+04, -1.0718168E+04,  3.5651716E+03, -4.4110895E+02,
           -1.3085150E+05, -1.8447255E+05,  2.9538016E+05, -7.9536583E+04, -1.0279583E+04,  3.3629135E+03,
            1.2591969E+05,  3.3280998E+05, -5.4534004E+05,  1.9771955E+05, -7.5414922E+03, -3.2633069E+03,
           -4.3690554E+04, -1.4724183E+05,  2.7062311E+05, -1.2341107E+05,  1.6622311E+04,  0.0000000E+00
        };
    private static readonly double[] CcpR32 = { 4.0186023E+00, -3.1370881E-01, 6.8796834E-01, 2.6831619E+00, -1.3934091E+00 };
    private static readonly double[] CpsR32 = { 102795, -204886, 141476, -33645 };
    private static readonly double[] CtsR32 = { 148, -298, 269, 241 };

    // R410A coefficients (Togashi 2014, Table 5-7)
    private static readonly double[] AlphaR410A = {
            6.7196006E+03,  3.1935694E+04, -3.2418379E+04,  8.5482532E-01,  8.1295227E+03, -1.7327422E+03,
            8.6392377E+02, -5.7171597E+03,  6.8653458E+03, -3.3190647E+03,  7.1308696E+02, -5.6028267E+01,
           -8.5841472E+04, -1.9218673E+05,  3.2004848E+05, -1.2678673E+05,  3.6577226E+03,  3.5479124E+03,
            8.2190468E+04,  3.1572566E+05, -5.5079066E+05,  2.6367950E+05, -3.7740069E+04, -4.9244792E+02,
           -2.8629986E+04, -1.3682605E+05,  2.5924131E+05, -1.3997914E+05,  2.7545424E+04, -1.4185289E+03
        };
    private static readonly double[] CcpR410A = { 3.8200427E+00, 2.5486066E+00, 1.0799339E+00, 9.8471295E-01, -7.6910990E-01 };
    private static readonly double[] CpsR410A = { 86584, -171854, 118095, -27955 };
    private static readonly double[] CtsR410A = { 151, -292, 260, 238 };

    // R134a coefficients
    private static readonly double[] AlphaR134a = {
            9.6110418E+03,  2.9590018E+04, -2.2904579E+04,  2.7602291E+03,  6.3436858E+02, -5.9229642E+01,
            1.1017426E+03, -1.2925788E+03,  5.1927422E+02, -6.9885390E+01,
           -7.5524176E+04, -2.8006887E+05,  3.3388181E+05, -1.0398404E+05,  9.7152916E+03,
            6.4037701E+04,  4.5332427E+05, -5.7147500E+05,  1.9611019E+05, -2.0375143E+04,
           -1.9195915E+04, -1.9499885E+05,  2.6634260E+05, -1.0132763E+05,  1.1874162E+04
        };
    private static readonly double[] CcpR134a = { 1.9747230E-01, 2.6221924E+01, -3.8424120E+01, 3.8198553E+01, -1.4361012E+01 };
    private static readonly double[] CpsR134a = { 56756, -104220, 65405, -13992 };
    private static readonly double[] CtsR134a = { 1355, -1252, 513, 242 };

    #endregion

    #region コンストラクタ

    /// <summary>
    /// Initializes a new instance for the specified refrigerant type.
    /// </summary>
    /// <param name="fluid">The type of refrigerant.</param>
    public Refrigerant(Fluid fluid)
    {
      FluidType = fluid;

      int index;
      switch (fluid)
      {
        case Fluid.R410A:
          _mNumber = 5; _nNumber = 8;
          CriticalTemperature = 71.358 + PhysicsConstants.CelsiusToKelvinOffset;
          CriticalDensity = 459.53;
          CriticalPressure = 4902.6;
          _gasConstant = 8.3144621 / 72.585;
          _refTemperature = PhysicsConstants.ToKelvin(0);
          _refDensity = 1170; _refEnthalpy = 200; _refEntropy = 1.0;
          _ccp = CcpR410A; _cps = CpsR410A; _cts = CtsR410A;
          MaxPressure = 4000; MinPressure = 700;
          _alpha = new double[_mNumber, _nNumber - 2];
          index = 0;
          for (int m = 0; m < _alpha.GetLength(0); m++)
            for (int n = 0; n < _alpha.GetLength(1); n++)
              _alpha[m, n] = AlphaR410A[index++];
          break;

        case Fluid.R32:
          _mNumber = 5; _nNumber = 8;
          CriticalTemperature = 78.105 + PhysicsConstants.CelsiusToKelvinOffset;
          CriticalDensity = 424.0;
          CriticalPressure = 5782;
          _gasConstant = 8.3144621 / 52.024;
          _refTemperature = PhysicsConstants.ToKelvin(0);
          _refDensity = 1055.3; _refEnthalpy = 200; _refEntropy = 1.0;
          _ccp = CcpR32; _cps = CpsR32; _cts = CtsR32;
          MaxPressure = 4500; MinPressure = 700;
          _alpha = new double[_mNumber, _nNumber - 2];
          index = 0;
          for (int m = 0; m < _alpha.GetLength(0); m++)
            for (int n = 0; n < _alpha.GetLength(1); n++)
              _alpha[m, n] = AlphaR32[index++];
          break;

        case Fluid.R134a:
          _mNumber = 5; _nNumber = 7;
          CriticalTemperature = 101.06 + PhysicsConstants.CelsiusToKelvinOffset;
          CriticalDensity = 511.9;
          CriticalPressure = 4059.3;
          _gasConstant = 8.3144621 / 102.03;
          _refTemperature = PhysicsConstants.ToKelvin(0);
          _refDensity = 1294.8; _refEnthalpy = 200; _refEntropy = 1.0;
          _ccp = CcpR134a; _cps = CpsR134a; _cts = CtsR134a;
          MaxPressure = 1500; MinPressure = 200;
          _alpha = new double[_mNumber, _nNumber - 2];
          index = 0;
          for (int m = 0; m < _alpha.GetLength(0); m++)
            for (int n = 0; n < _alpha.GetLength(1); n++)
              _alpha[m, n] = AlphaR134a[index++];
          break;

        default:
          _alpha = new double[0, 0];
          _ccp = _cps = _cts = Array.Empty<double>();
          break;
      }
    }

    #endregion

    #region PVT関係式

    /// <summary>
    /// Gets the pressure [kPa] from the temperature [K] and density [kg/m³].
    /// </summary>
    /// <param name="temperature">Temperature [K]</param>
    /// <param name="density">Density [kg/m³]</param>
    /// <returns>Pressure [kPa]</returns>
    public double GetPressureFromTemperatureAndDensity(double temperature, double density)
    {
      double tau = CriticalTemperature / temperature;
      double rho = density / CriticalDensity;

      double pressure = 0;
      for (int m = _alpha.GetLength(0) - 1; 0 <= m; m--)
      {
        double buff = 0;
        for (int n = _alpha.GetLength(1) - 1; 0 <= n; n--)
          buff = buff * rho + _alpha[m, n];
        pressure = pressure * tau + buff;
      }
      return pressure * rho * rho + density * _gasConstant * temperature;
    }

    /// <summary>
    /// Gets the density [kg/m³] from the pressure [kPa] and temperature [K]
    /// using Newton's method. The density argument serves as the initial guess
    /// and is updated in place.
    /// </summary>
    /// <remarks>
    /// This is an internal method used during convergence iterations.
    /// No range validation is performed here because intermediate values
    /// during convergence may temporarily exceed the valid range.
    /// </remarks>
    private void GetDensityFromPressureAndTemperatureInternal(
        double pressure, double temperature, ref double density)
    {
      Roots.ErrorFunction eFnc = dns =>
          pressure - GetPressureFromTemperatureAndDensity(temperature, dns);

      Roots.ErrorFunction eFncD = dns =>
      {
        double tau = CriticalTemperature / temperature;
        double rho = dns / CriticalDensity;
        double dPdRho = 0;
        int len = _alpha.GetLength(1) + 1;
        for (int m = _alpha.GetLength(0) - 1; 0 <= m; m--)
        {
          double buff = 0;
          for (int n = len; 2 <= n; n--)
            buff = buff * rho + n * _alpha[m, n - 2];
          dPdRho = dPdRho * tau + buff;
        }
        dPdRho *= rho;
        return -(dPdRho / CriticalDensity + _gasConstant * temperature);
      };

      try
      {
        density = Roots.Newton(eFnc, eFncD, density, 1e-3, 1e-3, 10);
      }
      catch (Exception e)
      {
        throw new PopoloNumericalException(
            "GetDensityFromPressureAndTemperatureInternal",
            $"Newton iteration failed. pressure={pressure} kPa, "
            + $"temperature={temperature} K, density={density} kg/m³."
            + Environment.NewLine + e.Message);
      }
    }

    #endregion

    #region 飽和状態の計算

    /// <summary>
    /// Gets the saturated liquid density [kg/m³], saturated vapor density [kg/m³],
    /// and saturation temperature [K] from the saturation pressure [kPa].
    /// </summary>
    /// <param name="pressure">Saturation pressure [kPa]</param>
    /// <param name="saturatedLiquidDensity">Saturated liquid density [kg/m³]</param>
    /// <param name="saturatedVaporDensity">Saturated vapor density [kg/m³]</param>
    /// <param name="saturatedTemperature">Saturation temperature [K]</param>
    /// <exception cref="PopoloOutOfRangeException">
    /// Thrown when the pressure is outside the valid range [MinPressure, MaxPressure].
    /// </exception>
    public void GetSaturatedPropertyFromPressure(double pressure,
        out double saturatedLiquidDensity, out double saturatedVaporDensity,
        out double saturatedTemperature)
    {
      ValidatePressure(pressure);

      double sld = 0, svd = 0;
      Roots.ErrorFunction eFnc = tmp =>
          GetGibbsEnergyDifference(tmp, pressure, out sld, out svd);

      //飽和温度の初期値を推定（3次多項式近似）
      double pr = pressure / CriticalPressure;
      double ts = _cts[0];
      for (int i = 1; i < _cts.Length; i++) ts = ts * pr + _cts[i];
      //2020.02.23: R410A高圧で収束エラーが出るケースへの対応として -2K オフセット
      ts -= 2.0;

      try
      {
        saturatedTemperature = Roots.Newton(eFnc, ts, 1e-5, 1e-5, 1e-3, 10);
      }
      catch (Exception e)
      {
        throw new PopoloNumericalException(
            "GetSaturatedPropertyFromPressure",
            $"Newton iteration failed. pressure={pressure} kPa."
            + Environment.NewLine + e.Message);
      }
      saturatedLiquidDensity = sld;
      saturatedVaporDensity = svd;
    }

    /// <summary>
    /// Gets the saturated liquid density [kg/m³], saturated vapor density [kg/m³],
    /// and saturation pressure [kPa] from the saturation temperature [K].
    /// </summary>
    /// <param name="temperature">Saturation temperature [K]</param>
    /// <param name="saturatedLiquidDensity">Saturated liquid density [kg/m³]</param>
    /// <param name="saturatedVaporDensity">Saturated vapor density [kg/m³]</param>
    /// <param name="saturatedPressure">Saturation pressure [kPa]</param>
    /// <exception cref="PopoloOutOfRangeException">
    /// Thrown when the temperature is below absolute zero.
    /// </exception>
    public void GetSaturatedPropertyFromTemperature(double temperature,
        out double saturatedLiquidDensity, out double saturatedVaporDensity,
        out double saturatedPressure)
    {
      ValidateTemperature(temperature);

      double sld = 0, svd = 0;
      Roots.ErrorFunction eFnc = pres =>
          GetGibbsEnergyDifference(temperature, pres, out sld, out svd);

      //飽和圧力の初期値を推定（3次多項式近似）
      double tr = temperature / CriticalTemperature;
      double ps = _cps[0];
      for (int i = 1; i < _cps.Length; i++) ps = ps * tr + _cps[i];

      try
      {
        saturatedPressure = Roots.Newton(eFnc, ps, 1e-5, 1e-4, 1e-3, 10);
      }
      catch (Exception e)
      {
        throw new PopoloNumericalException(
            "GetSaturatedPropertyFromTemperature",
            $"Newton iteration failed. temperature={temperature} K."
            + Environment.NewLine + e.Message);
      }
      saturatedLiquidDensity = sld;
      saturatedVaporDensity = svd;
    }

    /// <summary>
    /// Computes the difference in residual Gibbs free energy between liquid and vapor phases.
    /// Used as the error function for saturation property convergence (Eq.17 in the reference).
    /// </summary>
    private double GetGibbsEnergyDifference(
        double temperature, double pressure,
        out double liquidDensity, out double vaporDensity)
    {
      double tr = temperature / CriticalTemperature;

      //液体密度の初期値をRackett式で推定（式15）
      double rc = CriticalPressure / (CriticalDensity * CriticalTemperature * _gasConstant);
      double rhol = 1.0 / (Math.Pow(rc, 1.0 + Math.Pow(1.0 - tr, 2.0 / 7.0))
          / CriticalPressure * _gasConstant * CriticalTemperature);
      GetDensityFromPressureAndTemperatureInternal(pressure, temperature, ref rhol);
      liquidDensity = rhol;

      //気体密度の初期値を理想気体式で推定（式1）
      double rhov = pressure / (temperature * _gasConstant);
      GetDensityFromPressureAndTemperatureInternal(pressure, temperature, ref rhov);
      vaporDensity = rhov;

      //ギブスエネルギー差分を計算（式17）
      double gL = GetResidualGibbsFreeEnergy(temperature, liquidDensity);
      double gV = GetResidualGibbsFreeEnergy(temperature, vaporDensity);
      return gL - gV + _gasConstant * temperature * Math.Log(liquidDensity / vaporDensity);
    }

    /// <summary>
    /// Computes the residual Gibbs free energy [kJ] (Eq.13 in the reference).
    /// </summary>
    private double GetResidualGibbsFreeEnergy(double temperature, double density)
    {
      double tau = CriticalTemperature / temperature;
      double rho = density / CriticalDensity;

      double gr = 0;
      int len = _alpha.GetLength(1) + 1;
      for (int m = _alpha.GetLength(0) - 1; 0 <= m; m--)
      {
        double buff = 0;
        for (int n = len; 2 <= n; n--)
          buff = buff * rho + _alpha[m, n - 2] * n / (n - 1.0);
        gr = gr * tau + buff;
      }
      gr *= rho;
      return gr / CriticalDensity;
    }

    #endregion

    #region 温度・密度からの物性値計算

    /// <summary>
    /// Gets the specific enthalpy [kJ/kg] from the temperature [K] and density [kg/m³]
    /// (Eq.12 and Eq.20 in the reference).
    /// </summary>
    /// <param name="temperature">Temperature [K]</param>
    /// <param name="density">Density [kg/m³]</param>
    /// <returns>Specific enthalpy [kJ/kg]</returns>
    public double GetEnthalpyFromTemperatureAndDensity(double temperature, double density)
    {
      double h = GetResidualEnthalpy(temperature, density)
          - GetResidualEnthalpy(_refTemperature, _refDensity);
      return h + GetIntegralCp0(temperature) + _refEnthalpy;
    }

    /// <summary>
    /// Gets the specific entropy [kJ/(kg·K)] from the temperature [K] and density [kg/m³]
    /// (Eq.11 and Eq.18 in the reference).
    /// </summary>
    /// <param name="temperature">Temperature [K]</param>
    /// <param name="density">Density [kg/m³]</param>
    /// <returns>Specific entropy [kJ/(kg·K)]</returns>
    public double GetEntropyFromTemperatureAndDensity(double temperature, double density)
    {
      double s = GetResidualEntropy(temperature, density)
          - GetResidualEntropy(_refTemperature, _refDensity);
      s += _gasConstant * Math.Log(_refDensity / density);
      s += GetIntegralCp0T(temperature) - _gasConstant * Math.Log(temperature / _refTemperature);
      return s + _refEntropy;
    }

    /// <summary>
    /// Gets the specific internal energy [kJ/kg] from the temperature [K] and density [kg/m³]
    /// (Eq.22 in the reference).
    /// </summary>
    /// <param name="temperature">Temperature [K]</param>
    /// <param name="density">Density [kg/m³]</param>
    /// <returns>Specific internal energy [kJ/kg]</returns>
    public double GetInternalEnergyFromTemperatureAndDensity(double temperature, double density)
    {
      double p = GetPressureFromTemperatureAndDensity(temperature, density);
      return GetEnthalpyFromTemperatureAndDensity(temperature, density) - p / density;
    }

    /// <summary>
    /// Gets the isochoric (constant-volume) specific heat [kJ/(kg·K)]
    /// from the temperature [K] and density [kg/m³].
    /// </summary>
    /// <param name="temperature">Temperature [K]</param>
    /// <param name="density">Density [kg/m³]</param>
    /// <returns>Isochoric specific heat [kJ/(kg·K)]</returns>
    public double GetIsovolumetricSpecificHeatFromTemperatureAndDensity(
        double temperature, double density)
    {
      double tau = CriticalTemperature / temperature;
      double rho = density / CriticalDensity;

      double cv = 0;
      int len = _alpha.GetLength(1) + 1;
      for (int m = _alpha.GetLength(0) - 1; 2 <= m; m--)
      {
        double buff = 0;
        for (int n = len; 2 <= n; n--)
          buff = buff * rho + _alpha[m, n - 2] * m * (m + 1) / (1.0 - n);
        cv = cv * tau + buff;
      }
      cv *= rho * tau * tau;
      return cv / (CriticalDensity * temperature)
          + GetIsovolumetricHeatCapacityOfIdealGas(temperature);
    }

    /// <summary>
    /// Gets the isobaric (constant-pressure) specific heat [kJ/(kg·K)]
    /// from the temperature [K] and density [kg/m³].
    /// </summary>
    /// <param name="temperature">Temperature [K]</param>
    /// <param name="density">Density [kg/m³]</param>
    /// <returns>Isobaric specific heat [kJ/(kg·K)]</returns>
    public double GetIsobaricSpecificHeatFromTemperatureAndDensity(
        double temperature, double density)
    {
      double tau = CriticalTemperature / temperature;
      double rho = density / CriticalDensity;

      double cp1 = 0, cp2 = 0;
      int len = _alpha.GetLength(1) + 1;
      for (int m = _alpha.GetLength(0) - 1; 0 <= m; m--)
      {
        double buff1 = 0, buff2 = 0;
        for (int n = len; 2 <= n; n--)
        {
          if (m != 0) buff1 = buff1 * rho + _alpha[m, n - 2] * m;
          buff2 = buff2 * rho + _alpha[m, n - 2] * n;
        }
        if (m != 0) cp1 = cp1 * tau + buff1;
        cp2 = cp2 * tau + buff2;
      }
      cp1 = density * _gasConstant - cp1 * rho * rho * tau / temperature;
      cp2 = _gasConstant * temperature + cp2 * rho / CriticalDensity;

      double bf = cp1 / density;
      return GetIsovolumetricSpecificHeatFromTemperatureAndDensity(temperature, density)
          + temperature * bf * bf / cp2;
    }

    /// <summary>
    /// Gets the specific heat ratio (Cp/Cv) [-]
    /// from the temperature [K] and density [kg/m³].
    /// </summary>
    /// <param name="temperature">Temperature [K]</param>
    /// <param name="density">Density [kg/m³]</param>
    /// <returns>Specific heat ratio [-]</returns>
    public double GetSpecificHeatRatioFromTemperatureAndDensity(
        double temperature, double density)
    {
      double cv = GetIsovolumetricSpecificHeatFromTemperatureAndDensity(temperature, density);
      double cp = GetIsobaricSpecificHeatFromTemperatureAndDensity(temperature, density);
      return cp / cv;
    }

    /// <summary>Residual enthalpy Hr [kJ/kg] (Eq.12 in the reference).</summary>
    private double GetResidualEnthalpy(double temperature, double density)
    {
      double tau = CriticalTemperature / temperature;
      double rho = density / CriticalDensity;

      double hr = 0;
      int len = _alpha.GetLength(1) + 1;
      for (int m = _alpha.GetLength(0) - 1; 0 <= m; m--)
      {
        double buff = 0;
        for (int n = len; 2 <= n; n--)
          buff = buff * rho + _alpha[m, n - 2] * (n + m) / (n - 1.0);
        hr = hr * tau + buff;
      }
      hr *= rho;
      return hr / CriticalDensity;
    }

    /// <summary>Residual entropy Sr [kJ/(kg·K)] (Eq.11 in the reference).</summary>
    private double GetResidualEntropy(double temperature, double density)
    {
      double tau = CriticalTemperature / temperature;
      double rho = density / CriticalDensity;

      double sr = 0;
      int len = _alpha.GetLength(1) + 1;
      for (int m = _alpha.GetLength(0) - 1; 1 <= m; m--)
      {
        double buff = 0;
        for (int n = len; 2 <= n; n--)
          buff = buff * rho + _alpha[m, n - 2] * m / (n - 1.0);
        sr = sr * tau + buff;
      }
      sr *= rho * tau;
      return sr / (CriticalDensity * temperature);
    }

    /// <summary>
    /// Integral of ideal-gas isobaric specific heat Cp0 [kJ/kg]
    /// from refTemperature to temperature (Eq.21 in the reference).
    /// </summary>
    private double GetIntegralCp0(double temperature)
    {
      double tr = temperature / CriticalTemperature;
      double tr0 = _refTemperature / CriticalTemperature;
      double cp0 = 0, cp0r = 0;
      for (int i = _ccp.Length - 1; 0 <= i; i--)
      {
        cp0 = (cp0 + _ccp[i] / (i + 1) * CriticalTemperature) * tr;
        cp0r = (cp0r + _ccp[i] / (i + 1) * CriticalTemperature) * tr0;
      }
      return (cp0 - cp0r) * _gasConstant;
    }

    /// <summary>
    /// Integral of Cp0/T [kJ/(kg·K)] from refTemperature to temperature (Eq.19 in the reference).
    /// </summary>
    private double GetIntegralCp0T(double temperature)
    {
      double tr = temperature / CriticalTemperature;
      double tr0 = _refTemperature / CriticalTemperature;
      double cp0 = 0, cp0r = 0;
      for (int i = _ccp.Length - 1; 0 < i; i--)
      {
        cp0 = cp0 * tr + _ccp[i] / i;
        cp0r = cp0r * tr0 + _ccp[i] / i;
      }
      cp0 *= tr;
      cp0r *= tr0;
      return ((cp0 + _ccp[0] * Math.Log(temperature))
            - (cp0r + _ccp[0] * Math.Log(_refTemperature))) * _gasConstant;
    }

    /// <summary>Ideal-gas isobaric specific heat Cp0 [kJ/(kg·K)] (Eq.8 in the reference).</summary>
    private double GetIsobaricHeatCapacityOfIdealGas(double temperature)
    {
      double tr = temperature / CriticalTemperature;
      double cp0 = _ccp[_ccp.Length - 1];
      for (int i = _ccp.Length - 2; 0 <= i; i--) cp0 = cp0 * tr + _ccp[i];
      return cp0 * _gasConstant;
    }

    /// <summary>Ideal-gas isochoric specific heat Cv0 [kJ/(kg·K)].</summary>
    private double GetIsovolumetricHeatCapacityOfIdealGas(double temperature)
        => GetIsobaricHeatCapacityOfIdealGas(temperature) - _gasConstant;

    #endregion

    #region 圧力・エンタルピーからの状態計算

    /// <summary>
    /// Gets the thermodynamic state from the pressure [kPa] and specific enthalpy [kJ/kg].
    /// </summary>
    /// <param name="pressure">Pressure [kPa]</param>
    /// <param name="enthalpy">Specific enthalpy [kJ/kg]</param>
    /// <param name="temperature">Temperature [K]</param>
    /// <param name="density">Density [kg/m³]</param>
    /// <param name="entropy">Specific entropy [kJ/(kg·K)]</param>
    /// <param name="internalEnergy">Specific internal energy [kJ/kg]</param>
    /// <exception cref="PopoloOutOfRangeException">
    /// Thrown when the pressure is outside the valid range [MinPressure, MaxPressure].
    /// </exception>
    /// <exception cref="PopoloNumericalException">
    /// Thrown when the Newton iteration fails to converge.
    /// </exception>
    public void GetStateFromPressureAndEnthalpy(
        double pressure, double enthalpy,
        out double temperature, out double density,
        out double entropy, out double internalEnergy)
    {
      ValidatePressure(pressure);

      //飽和状態を計算して相を判定
      GetSaturatedPropertyFromPressure(pressure, out double rhoL, out double rhoV, out double tSat);
      double hl = GetEnthalpyFromTemperatureAndDensity(tSat, rhoL);
      double hv = GetEnthalpyFromTemperatureAndDensity(tSat, rhoV);

      Phase phase;
      if (enthalpy < hl) phase = Phase.Liquid;
      else if (hv < enthalpy) phase = Phase.Vapor;
      else phase = Phase.Equilibrium;

      //二相域：気液比で加重平均
      if (phase == Phase.Equilibrium)
      {
        temperature = tSat;
        double vRate = (enthalpy - hl) / (hv - hl);
        double lRate = 1.0 - vRate;
        density = 1.0 / (vRate / rhoV + lRate / rhoL);
        double sL = GetEntropyFromTemperatureAndDensity(tSat, rhoL);
        double sV = GetEntropyFromTemperatureAndDensity(tSat, rhoV);
        entropy = sL * lRate + sV * vRate;
        double uL = GetInternalEnergyFromTemperatureAndDensity(tSat, rhoL);
        double uV = GetInternalEnergyFromTemperatureAndDensity(tSat, rhoV);
        internalEnergy = uL * lRate + uV * vRate;
        return;
      }

      //単相域：ニュートン法で温度を収束計算
      temperature = phase == Phase.Liquid ? tSat - 3.0 : tSat + 3.0;
      density = phase == Phase.Liquid ? rhoL : rhoV;

      double dns = density;
      Roots.ErrorFunction eFnc = tmp =>
      {
        GetDensityFromPressureAndTemperatureInternal(pressure, tmp, ref dns);
        return enthalpy - GetEnthalpyFromTemperatureAndDensity(tmp, dns);
      };

      try
      {
        //2023.04.11: 10回で足りないケースがあったため15回に増加
        temperature = Roots.Newton(eFnc, temperature, 1e-5, 1e-3, 1e-3, 15);
      }
      catch (Exception e)
      {
        throw new PopoloNumericalException(
            "GetStateFromPressureAndEnthalpy",
            $"Newton iteration failed. pressure={pressure} kPa, enthalpy={enthalpy} kJ/kg."
            + Environment.NewLine + e.Message);
      }
      density = dns;
      entropy = GetEntropyFromTemperatureAndDensity(temperature, density);
      internalEnergy = GetInternalEnergyFromTemperatureAndDensity(temperature, density);
    }

    #endregion

    #region 圧力・エントロピーからの状態計算

    /// <summary>
    /// Gets the thermodynamic state from the pressure [kPa] and specific entropy [kJ/(kg·K)].
    /// </summary>
    /// <param name="pressure">Pressure [kPa]</param>
    /// <param name="entropy">Specific entropy [kJ/(kg·K)]</param>
    /// <param name="temperature">Temperature [K]</param>
    /// <param name="density">Density [kg/m³]</param>
    /// <param name="enthalpy">Specific enthalpy [kJ/kg]</param>
    /// <param name="internalEnergy">Specific internal energy [kJ/kg]</param>
    /// <exception cref="PopoloOutOfRangeException">
    /// Thrown when the pressure is outside the valid range [MinPressure, MaxPressure].
    /// </exception>
    /// <exception cref="PopoloNumericalException">
    /// Thrown when the Newton iteration fails to converge.
    /// </exception>
    public void GetStateFromPressureAndEntropy(
        double pressure, double entropy,
        out double temperature, out double density,
        out double enthalpy, out double internalEnergy)
    {
      ValidatePressure(pressure);

      GetSaturatedPropertyFromPressure(pressure, out double rhoL, out double rhoV, out double tSat);
      double sl = GetEntropyFromTemperatureAndDensity(tSat, rhoL);
      double sv = GetEntropyFromTemperatureAndDensity(tSat, rhoV);

      Phase phase;
      if (entropy < sl) phase = Phase.Liquid;
      else if (sv < entropy) phase = Phase.Vapor;
      else phase = Phase.Equilibrium;

      //二相域：気液比で加重平均
      if (phase == Phase.Equilibrium)
      {
        temperature = tSat;
        double vRate = (entropy - sl) / (sv - sl);
        double lRate = 1.0 - vRate;
        density = 1.0 / (vRate / rhoV + lRate / rhoL);
        double hL = GetEnthalpyFromTemperatureAndDensity(tSat, rhoL);
        double hV = GetEnthalpyFromTemperatureAndDensity(tSat, rhoV);
        enthalpy = hL * lRate + hV * vRate;
        double uL = GetInternalEnergyFromTemperatureAndDensity(tSat, rhoL);
        double uV = GetInternalEnergyFromTemperatureAndDensity(tSat, rhoV);
        internalEnergy = uL * lRate + uV * vRate;
        return;
      }

      //単相域：ニュートン法で温度を収束計算
      temperature = phase == Phase.Liquid ? tSat - 3.0 : tSat + 3.0;
      density = phase == Phase.Liquid ? rhoL : rhoV;

      double dns = density;
      Roots.ErrorFunction eFnc = tmp =>
      {
        GetDensityFromPressureAndTemperatureInternal(pressure, tmp, ref dns);
        return entropy - GetEntropyFromTemperatureAndDensity(tmp, dns);
      };

      try
      {
        temperature = Roots.Newton(eFnc, temperature, 1e-5, 1e-3, 1e-3, 10);
      }
      catch (Exception e)
      {
        throw new PopoloNumericalException(
            "GetStateFromPressureAndEntropy",
            $"Newton iteration failed. pressure={pressure} kPa, entropy={entropy} kJ/(kg·K)."
            + Environment.NewLine + e.Message);
      }
      density = dns;
      enthalpy = GetEnthalpyFromTemperatureAndDensity(temperature, density);
      internalEnergy = GetInternalEnergyFromTemperatureAndDensity(temperature, density);
    }

    #endregion

    #region 圧力・温度からの状態計算

    /// <summary>
    /// Gets the thermodynamic state from the pressure [kPa] and temperature [K].
    /// </summary>
    /// <param name="pressure">Pressure [kPa]</param>
    /// <param name="temperature">Temperature [K]</param>
    /// <param name="entropy">Specific entropy [kJ/(kg·K)]</param>
    /// <param name="density">Density [kg/m³]</param>
    /// <param name="enthalpy">Specific enthalpy [kJ/kg]</param>
    /// <param name="internalEnergy">Specific internal energy [kJ/kg]</param>
    /// <remarks>
    /// In the two-phase region, pressure and temperature alone do not uniquely determine
    /// the thermodynamic state (Gibbs phase rule: F = C - P + 2 = 1 for a pure substance).
    /// In this case, the saturated liquid properties are returned as a convention.
    /// </remarks>
    /// <exception cref="PopoloOutOfRangeException">
    /// Thrown when the pressure is outside the valid range [MinPressure, MaxPressure].
    /// </exception>
    /// <exception cref="PopoloNumericalException">
    /// Thrown when the Newton iteration fails to converge.
    /// </exception>
    public void GetStateFromPressureAndTemperature(
        double pressure, double temperature,
        out double entropy, out double density,
        out double enthalpy, out double internalEnergy)
    {
      ValidatePressure(pressure);
      ValidateTemperature(temperature);

      GetSaturatedPropertyFromPressure(pressure, out double rhoL, out double rhoV, out double tSat);

      Phase phase;
      if (temperature < tSat) phase = Phase.Liquid;
      else if (tSat < temperature) phase = Phase.Vapor;
      else phase = Phase.Equilibrium;

      //二相域：圧力・温度のみでは気液比が定まらないため飽和液の物性を返す
      if (phase == Phase.Equilibrium)
      {
        density = rhoL;
        enthalpy = GetEnthalpyFromTemperatureAndDensity(tSat, rhoL);
        internalEnergy = GetInternalEnergyFromTemperatureAndDensity(tSat, rhoL);
        entropy = GetEntropyFromTemperatureAndDensity(tSat, rhoL);
        return;
      }

      density = phase == Phase.Liquid ? rhoL + 0.1 : rhoV - 0.1;

      double tmp = temperature;
      Roots.ErrorFunction eFnc = dns =>
          pressure - GetPressureFromTemperatureAndDensity(tmp, dns);

      try
      {
        density = Roots.Newton(eFnc, density, 1e-5, 1e-3, 1e-3, 10);
      }
      catch (Exception e)
      {
        throw new PopoloNumericalException(
            "GetStateFromPressureAndTemperature",
            $"Newton iteration failed. pressure={pressure} kPa, temperature={temperature} K."
            + Environment.NewLine + e.Message);
      }
      enthalpy = GetEnthalpyFromTemperatureAndDensity(temperature, density);
      internalEnergy = GetInternalEnergyFromTemperatureAndDensity(temperature, density);
      entropy = GetEntropyFromTemperatureAndDensity(temperature, density);
    }

    /// <summary>
    /// Gets the density [kg/m³] from the pressure [kPa] and temperature [K].
    /// </summary>
    /// <param name="pressure">Pressure [kPa]</param>
    /// <param name="temperature">Temperature [K]</param>
    /// <returns>Density [kg/m³]</returns>
    /// <exception cref="PopoloOutOfRangeException">
    /// Thrown when the pressure is outside the valid range [MinPressure, MaxPressure].
    /// </exception>
    public double GetDensityFromPressureAndTemperature(double pressure, double temperature)
    {
      ValidatePressure(pressure);
      ValidateTemperature(temperature);

      GetSaturatedPropertyFromPressure(pressure, out double rhoL, out double rhoV, out double satT);
      double rho = satT < temperature ? rhoV : rhoL;
      GetDensityFromPressureAndTemperatureInternal(pressure, temperature, ref rho);
      return rho;
    }

    #endregion

    #region 入力検証

    /// <summary>圧力が適用範囲内かチェックする</summary>
    private void ValidatePressure(double pressure)
    {
      if (pressure < MinPressure || pressure > MaxPressure)
        throw new PopoloOutOfRangeException(
            "pressure", pressure, MinPressure, MaxPressure,
            $"Pressure is outside the valid range for {FluidType}.");
    }

    /// <summary>温度が絶対零度以上かチェックする</summary>
    private void ValidateTemperature(double temperature)
    {
      if (temperature <= 0)
        throw new PopoloOutOfRangeException(
            "temperature", temperature, 0.0, null,
            "Temperature in Kelvin must be positive.");
    }

    #endregion

  }
}
