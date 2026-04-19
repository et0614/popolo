/* Water.cs
 *
 * Copyright (C) 2007 E.Togashi
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

namespace Popolo.Core.Physics
{
    /// <summary>
    /// Provides static methods for thermophysical properties of water and steam.
    /// </summary>
    /// <remarks>
    /// Ported from HVACSIM+(J). References:
    /// 1) Irvine, T.F.Jr., and Liley, P.E., "Steam and Gas Tables with Computer Equations," Academic Press, 1984.
    /// 2) Van Wylen, G.J., and Sonntag, R.E., Fundamentals of Classical Thermodynamics (SI Version). Wiley.
    /// 3) Chapman, A.J. Heat Transfer (3rd Ed.). Macmillan, 1974.
    /// 4) Karlekar, B.V., and Desmond, R.M., Engineering Heat Transfer. West Publishing, 1977.
    /// 5) CRC Handbook of Chemistry and Physics, 61st Ed. (1980-1981).
    /// </remarks>
    public static class Water
    {

        #region 定数

        /// <summary>Critical temperature of water [K].</summary>
        public const double CriticalTemperature = 647.096;

        /// <summary>Specific enthalpy at the critical point [kJ/kg].</summary>
        public const double CriticalEnthalpy = 2099.3;

        /// <summary>Specific volume at the critical point [m³/kg].</summary>
        public const double CriticalSpecificVolume = 0.003155;

        /// <summary>Specific entropy at the critical point [kJ/(kg·K)].</summary>
        public const double CriticalEntropy = 4.4289;

        /// <summary>Saturation pressure at the critical point [kPa].</summary>
        public const double CriticalPressure = 22089;

        /// <summary>Latent heat of vaporization at the triple point [kJ/kg].</summary>
        public const double VaporizationHeatAtTriplePoint = 2500.9;

        #endregion

        #region 非公開メソッド

        /// <summary>Computes the reduced temperature (dimensionless distance from the critical temperature).</summary>
        private static double GetReducedTemperature(double saturationTemperature)
        {
            return (CriticalTemperature - PhysicsConstants.ToKelvin(saturationTemperature))
                / CriticalTemperature;
        }

        #endregion

        #region 飽和水蒸気圧・飽和温度

        /// <summary>
        /// Gets the saturation pressure [kPa] from the saturation temperature [°C].
        /// </summary>
        /// <param name="saturationTemperature">Saturation temperature [°C]</param>
        /// <returns>Saturation pressure [kPa]</returns>
        /// <remarks>
        /// Uses the Wexler-Hyland equation below 0.01 °C and the IAPWS-IF97 equation above.
        /// </remarks>
        public static double GetSaturationPressure(double saturationTemperature)
        {
            const double P_CONVERT = 0.001d;

            const double C1 = -5.6745359e3d;
            const double C2 = 6.3925247d;
            const double C3 = -9.6778430e-3d;
            const double C4 = 6.2215701e-7d;
            const double C5 = 2.0747825e-9d;
            const double C6 = -9.4840240e-13d;
            const double C7 = 4.1635019d;

            const double N1 = 0.11670521452767e4d;
            const double N2 = -0.72421316703206e6d;
            const double N3 = -0.17073846940092e2d;
            const double N4 = 0.12020824702470e5d;
            const double N5 = -0.32325550322333e7d;
            const double N6 = 0.14915108613530e2d;
            const double N7 = -0.4823265731591e4d;
            const double N8 = 0.40511340542057e6d;
            const double N9 = -0.23855557567849e0d;
            const double N10 = 0.65017534844798e3d;

            double ts = PhysicsConstants.ToKelvin(saturationTemperature);

            //-100~0.01°C：三重点以下はWexler-Hylandの式
            if (saturationTemperature < 0.01)
                return Math.Exp(C1 / ts + C2 + C3 * ts + C4 * Math.Pow(ts, 2)
                    + C5 * Math.Pow(ts, 3) + C6 * Math.Pow(ts, 4) + C7 * Math.Log(ts)) * P_CONVERT;
            //~647.096K：臨界温度まではIAPWS-IF97実用国際状態式
            else
            {
                double alpha = ts + N9 / (ts - N10);
                double a2 = alpha * alpha;
                double A = a2 + N1 * alpha + N2;
                double B = N3 * a2 + N4 * alpha + N5;
                double C = N6 * a2 + N7 * alpha + N8;
                return Math.Pow(2 * C / (-B + Math.Pow(B * B - 4 * A * C, 0.5)), 4) / P_CONVERT;
            }
        }

        /// <summary>
        /// Gets the saturation temperature [°C] from the saturation pressure [kPa].
        /// </summary>
        /// <param name="saturationPressure">Saturation pressure [kPa]</param>
        /// <returns>Saturation temperature [°C]</returns>
        /// <remarks>
        /// Uses an approximation of the Wexler-Hyland equation below 0.611213 kPa
        /// and the IAPWS-IF97 equation above.
        /// </remarks>
        public static double GetSaturationTemperature(double saturationPressure)
        {
            const double P_CONVERT = 0.001d;

            const double D1 = -6.0662e1d;
            const double D2 = 7.4624e0d;
            const double D3 = 2.0594e-1d;
            const double D4 = 1.6321e-2d;

            const double N1 = 0.11670521452767e4d;
            const double N2 = -0.72421316703206e6d;
            const double N3 = -0.17073846940092e2d;
            const double N4 = 0.12020824702470e5d;
            const double N5 = -0.32325550322333e7d;
            const double N6 = 0.14915108613530e2d;
            const double N7 = -0.4823265731591e4d;
            const double N8 = 0.40511340542057e6d;
            const double N9 = -0.23855557567849e0d;
            const double N10 = 0.65017534844798e3d;

            //~0°C：Wexler-Hylandの計算値を近似した式
            if (saturationPressure < 0.611213)
            {
                double y = Math.Log(saturationPressure / P_CONVERT);
                return D1 + y * (D2 + y * (D3 + y * D4));
            }
            //0°C~：臨界圧力まではIAPWS-IF97実用国際状態式
            else
            {
                double ps = saturationPressure * P_CONVERT;
                double beta = Math.Pow(ps, 0.25);
                double b2 = beta * beta;
                double E = b2 + N3 * beta + N6;
                double F = N1 * b2 + N4 * beta + N7;
                double G = N2 * b2 + N5 * beta + N8;
                double D = 2 * G / (-F - Math.Pow(F * F - 4 * E * G, 0.5));
                double tk = (N10 + D - Math.Pow(Math.Pow(N10 + D, 2) - 4 * (N9 + N10 * D), 0.5)) / 2d;
                return PhysicsConstants.ToCelsius(tk);
            }
        }

        #endregion

        #region 蒸発潜熱

        /// <summary>
        /// Gets the latent heat of vaporization [kJ/kg] from the saturation temperature [°C].
        /// </summary>
        /// <param name="saturationTemperature">Saturation temperature [°C]</param>
        /// <returns>Latent heat of vaporization [kJ/kg]</returns>
        public static double GetVaporizationLatentHeat(double saturationTemperature)
        {
            const double E1 = -3.87446;
            const double E2 = 2.94553;
            const double E3 = -8.06395;
            const double E4 = 11.5633;
            const double E5 = -6.02884;
            const double B = 0.779221;
            const double C = 4.62668;
            const double D = -1.07931;

            //0°C以上とする
            saturationTemperature = Math.Max(0, saturationTemperature);

            double tr = GetReducedTemperature(saturationTemperature);
            if (tr < 0.0) return 0.0;
            double y = B * Math.Pow(tr, 1.0 / 3.0)
                + C * Math.Pow(tr, 5.0 / 6.0) + D * Math.Pow(tr, 0.875);
            y += tr * (E1 + tr * (E2 + tr * (E3 + tr * (E4 + tr * E5))));
            return y * VaporizationHeatAtTriplePoint;
        }

        #endregion

        #region 飽和水の物性値

        /// <summary>
        /// Gets the specific volume of saturated liquid water [m³/kg]
        /// from the saturation temperature [°C].
        /// </summary>
        /// <param name="saturationTemperature">Saturation temperature [°C]</param>
        /// <returns>Specific volume of saturated liquid [m³/kg]</returns>
        public static double GetSaturatedLiquidSpecificVolume(double saturationTemperature)
        {
            const double A = 1.0;
            const double B = -1.9153882;
            const double C = 12.015186;
            const double D = -7.8464025;
            const double E1 = -3.8886414;
            const double E2 = 2.0582238;
            const double E3 = -2.0829991;
            const double E4 = 0.82180004;
            const double E5 = 0.47549742;

            double tr = GetReducedTemperature(saturationTemperature);
            double y = A + B * Math.Pow(tr, 1.0 / 3.0) + C * Math.Pow(tr, 5.0 / 6.0) + D * Math.Pow(tr, 0.875);
            y += tr * (E1 + tr * (E2 + tr * (E3 + tr * (E4 + tr * E5))));
            return y * CriticalSpecificVolume;
        }

        /// <summary>
        /// Gets the specific enthalpy of saturated liquid water [kJ/kg]
        /// from the saturation temperature [°C].
        /// </summary>
        /// <param name="saturationTemperature">Saturation temperature [°C]</param>
        /// <returns>Specific enthalpy of saturated liquid [kJ/kg]</returns>
        public static double GetSaturatedLiquidEnthalpy(double saturationTemperature)
        {
            //273.16<Ts<300の係数
            const double E11 = 624.698837;
            const double E21 = -2343.85369;
            const double E31 = -9508.12101;
            const double E41 = 71628.7928;
            const double E51 = -163535.221;
            const double E61 = 166531.093;
            const double E71 = -64785.4585;
            //300<Ts<600の係数
            const double A2 = 0.8839230108;
            const double E12 = -2.67172935;
            const double E22 = 6.22640035;
            const double E32 = -13.1789573;
            const double E42 = -1.91322436;
            const double E52 = 68.793763;
            const double E62 = -124.819906;
            const double E72 = 72.1435404;
            //600<Tsの係数
            const double A3 = 1.0;
            const double B3 = -0.441057805;
            const double C3 = -5.52255517;
            const double D3 = 6.43994847;
            const double E13 = -1.64578795;
            const double E23 = -1.30574143;

            double tk = PhysicsConstants.ToKelvin(saturationTemperature);
            double tr = GetReducedTemperature(saturationTemperature);
            double y;
            if (tk < 300.0)
                y = tr * (E11 + tr * (E21 + tr * (E31 + tr * (E41 + tr * (E51 + tr * (E61 + tr * E71))))));
            else if (tk < 600.0)
                y = tr * (E12 + tr * (E22 + tr * (E32 + tr * (E42 + tr * (E52 + tr * (E62 + tr * E72)))))) + A2;
            else
                y = A3 + B3 * Math.Pow(tr, 1.0 / 3.0) + C3 * Math.Pow(tr, 5.0 / 6.0)
                    + D3 * Math.Pow(tr, 0.875) + tr * (E13 + tr * E23);

            return y * CriticalEnthalpy;
        }

        /// <summary>
        /// Gets the specific entropy of saturated liquid water [kJ/(kg·K)]
        /// from the saturation temperature [°C].
        /// </summary>
        /// <param name="saturationTemperature">Saturation temperature [°C]</param>
        /// <returns>Specific entropy of saturated liquid [kJ/(kg·K)]</returns>
        public static double GetSaturatedLiquidEntropy(double saturationTemperature)
        {
            //273.16<Ts<300の係数
            const double E11 = -1836.92956;
            const double E21 = 14706.6352;
            const double E31 = -43146.6046;
            const double E41 = 48606.6733;
            const double E51 = 7997.5096;
            const double E61 = -58333.9887;
            const double E71 = 33140.0718;
            //300<Ts<600の係数
            const double A2 = 0.912762917;
            const double E12 = -1.75702956;
            const double E22 = 1.68754095;
            const double E32 = 5.82215341;
            const double E42 = -63.3354786;
            const double E52 = 188.076546;
            const double E62 = -252.344531;
            const double E72 = 128.058531;
            //600<Tsの係数
            const double A3 = 1.0;
            const double B3 = -0.324817650;
            const double C3 = -2.990556709;
            const double D3 = 3.2341900;
            const double E13 = -0.678067859;
            const double E23 = -1.91910364;

            double tk = PhysicsConstants.ToKelvin(saturationTemperature);
            double tr = GetReducedTemperature(saturationTemperature);
            double y;
            if (tk < 300.0)
                y = tr * (E11 + tr * (E21 + tr * (E31 + tr * (E41 + tr * (E51 + tr * (E61 + tr * E71))))));
            else if (tk < 600.0)
                y = tr * (E12 + tr * (E22 + tr * (E32 + tr * (E42 + tr * (E52 + tr * (E62 + tr * E72)))))) + A2;
            else
                y = A3 + B3 * Math.Pow(tr, 1.0 / 3.0) + C3 * Math.Pow(tr, 5.0 / 6.0)
                    + D3 * Math.Pow(tr, 0.875) + tr * (E13 + tr * E23);

            return y * CriticalEntropy;
        }

        #endregion

        #region 飽和蒸気の物性値

        /// <summary>
        /// Gets the specific volume of saturated vapor [m³/kg]
        /// from the saturation temperature [°C] and saturation pressure [kPa].
        /// </summary>
        /// <param name="saturationTemperature">Saturation temperature [°C]</param>
        /// <param name="saturationPressure">Saturation pressure [kPa]</param>
        /// <returns>Specific volume of saturated vapor [m³/kg]</returns>
        public static double GetSaturatedVaporSpecificVolume(
            double saturationTemperature, double saturationPressure)
        {
            const double A = 1.0d;
            const double B = 1.6351057d;
            const double C = 52.584599d;
            const double D = -44.694653;
            const double E1 = -8.9751114d;
            const double E2 = -0.43845530d;
            const double E3 = -19.179576d;
            const double E4 = 36.765319d;
            const double E5 = -19.462437d;

            double tr = GetReducedTemperature(saturationTemperature);
            double y = A + B * Math.Pow(tr, 1.0 / 3.0) + C * Math.Pow(tr, 5.0 / 6.0) + D * Math.Pow(tr, 0.875);
            y += tr * (E1 + tr * (E2 + tr * (E3 + tr * (E4 + tr * E5))));
            return y * CriticalPressure * CriticalSpecificVolume / saturationPressure;
        }

        /// <summary>
        /// Gets the specific enthalpy of saturated vapor [kJ/kg]
        /// from the saturation temperature [°C].
        /// </summary>
        /// <param name="saturationTemperature">Saturation temperature [°C]</param>
        /// <returns>Specific enthalpy of saturated vapor [kJ/kg]</returns>
        public static double GetSaturatedVaporEnthalpy(double saturationTemperature)
        {
            const double E1 = -4.81351884;
            const double E2 = 2.69411792;
            const double E3 = -7.39064542;
            const double E4 = 10.4961689;
            const double E5 = -5.46840036;
            const double A = 1.0;
            const double B = 0.457874342;
            const double C = 5.08441288;
            const double D = -1.48513244;

            double tr = GetReducedTemperature(saturationTemperature);
            double y = A + B * Math.Pow(tr, 1.0 / 3.0) + C * Math.Pow(tr, 5.0 / 6.0) + D * Math.Pow(tr, 0.875);
            y += tr * (E1 + tr * (E2 + tr * (E3 + tr * (E4 + tr * E5))));
            return y * CriticalEnthalpy;
        }

        /// <summary>
        /// Gets the specific entropy of saturated vapor [kJ/(kg·K)]
        /// from the saturation temperature [°C].
        /// </summary>
        /// <param name="saturationTemperature">Saturation temperature [°C]</param>
        /// <returns>Specific entropy of saturated vapor [kJ/(kg·K)]</returns>
        public static double GetSaturatedVaporEntropy(double saturationTemperature)
        {
            const double E1 = -4.34839;
            const double E2 = 1.34672;
            const double E3 = 1.75261;
            const double E4 = -6.22295;
            const double E5 = 9.99004;
            const double A = 1.0;
            const double B = 0.377391;
            const double C = -2.78368;
            const double D = 6.93135;

            double tr = GetReducedTemperature(saturationTemperature);
            double y = A + B * Math.Pow(tr, 1.0 / 3.0) + C * Math.Pow(tr, 5.0 / 6.0) + D * Math.Pow(tr, 0.875);
            y += tr * (E1 + tr * (E2 + tr * (E3 + tr * (E4 + tr * E5))));
            return y * CriticalEntropy;
        }

        #endregion

        #region 液体水の物性値

        /// <summary>
        /// Gets the density of liquid water [kg/m³] from the temperature [°C].
        /// </summary>
        /// <param name="temperature">Temperature [°C]</param>
        /// <returns>Density of liquid water [kg/m³]</returns>
        public static double GetLiquidDensity(double temperature)
        {
            double[] a = { 9.8811040e2, -1.3273604e3, 4.7162295e3, -4.1245328e3 };

            double tk = PhysicsConstants.ToKelvin(temperature);
            double tr = (CriticalTemperature - tk) / CriticalTemperature;

            double rho = a[a.Length - 1];
            for (int i = a.Length - 2; 0 <= i; i--) rho = a[i] + rho * tr;
            return rho;
        }

        /// <summary>
        /// Gets the isobaric specific heat of liquid water [kJ/(kg·K)] from the temperature [°C].
        /// </summary>
        /// <param name="temperature">Temperature [°C]</param>
        /// <returns>Isobaric specific heat of liquid water [kJ/(kg·K)]</returns>
        public static double GetLiquidIsobaricSpecificHeat(double temperature)
        {
            double[] a = { 1.0570130, 2.1952960e1, -4.9895501e1, 3.6963413e1 };

            double tk = PhysicsConstants.ToKelvin(temperature);
            double tr = (CriticalTemperature - tk) / CriticalTemperature;

            double cpw = a[a.Length - 1];
            for (int i = a.Length - 2; 0 <= i; i--) cpw = a[i] + cpw * tr;
            return cpw;
        }

        /// <summary>
        /// Gets the thermal conductivity of liquid water [W/(m·K)] from the temperature [°C].
        /// </summary>
        /// <param name="temperature">Temperature [°C]</param>
        /// <returns>Thermal conductivity of liquid water [W/(m·K)]</returns>
        public static double GetLiquidThermalConductivity(double temperature)
        {
            double[] a = { -1.3734399e-1, 4.2128755, -5.9412196, 1.2794890 };

            double tk = PhysicsConstants.ToKelvin(temperature);
            double tr = (CriticalTemperature - tk) / CriticalTemperature;

            double lambda = a[a.Length - 1];
            for (int i = a.Length - 2; 0 <= i; i--) lambda = a[i] + lambda * tr;
            return lambda;
        }

        /// <summary>
        /// Gets the dynamic viscosity of liquid water [Pa·s] from the temperature [°C].
        /// </summary>
        /// <param name="temperature">Temperature [°C]</param>
        /// <returns>Dynamic viscosity of liquid water [Pa·s]</returns>
        public static double GetLiquidViscosity(double temperature)
        {
            double[] a = { 5.2136906e1, -4.0910405e2, 1.3270844e3, -1.9089622e3, 1.0489917e3 };

            double tk = PhysicsConstants.ToKelvin(temperature);
            double tr = (CriticalTemperature - tk) / CriticalTemperature;

            double mu = a[a.Length - 1];
            for (int i = a.Length - 2; 0 <= i; i--) mu = a[i] + mu * tr;
            return Math.Exp(mu) * 0.000001;
        }

        /// <summary>
        /// Gets the kinematic viscosity of liquid water [m²/s] from the temperature [°C].
        /// </summary>
        /// <param name="temperature">Temperature [°C]</param>
        /// <returns>Kinematic viscosity of liquid water [m²/s]</returns>
        public static double GetLiquidDynamicViscosity(double temperature)
        {
            return GetLiquidViscosity(temperature) / GetLiquidDensity(temperature);
        }

        /// <summary>
        /// Gets the thermal diffusivity of liquid water [m²/s] from the temperature [°C].
        /// </summary>
        /// <param name="temperature">Temperature [°C]</param>
        /// <returns>Thermal diffusivity of liquid water [m²/s]</returns>
        public static double GetLiquidThermalDiffusivity(double temperature)
        {
            double lambda = GetLiquidThermalConductivity(temperature);
            double cp = GetLiquidIsobaricSpecificHeat(temperature);
            double rho = GetLiquidDensity(temperature);
            return lambda / (1000.0 * cp * rho);
        }

        /// <summary>
        /// Gets the volumetric thermal expansion coefficient of liquid water [1/K]
        /// from the temperature [°C].
        /// </summary>
        /// <param name="temperature">Temperature [°C]</param>
        /// <returns>Volumetric thermal expansion coefficient [1/K]</returns>
        public static double GetLiquidThermalExpansionCoefficient(double temperature)
        {
            const double deltaT = 1.0;
            double rho1 = GetLiquidDensity(temperature + deltaT);
            double rho2 = GetLiquidDensity(temperature - deltaT);
            double rho0 = GetLiquidDensity(temperature);
            return -(rho1 - rho2) / (2.0 * deltaT * rho0);
        }

        #endregion

        #region 過熱蒸気の物性値（HVACSIM+(J)から移植）

        /// <summary>
        /// Gets the specific volume of superheated steam [m³/kg]
        /// from the pressure [kPa] and temperature [°C].
        /// </summary>
        /// <param name="pressure">Pressure [kPa]</param>
        /// <param name="temperature">Temperature [°C]</param>
        /// <returns>Specific volume of superheated steam [m³/kg]</returns>
        public static double GetSuperheatedVaporSpecificVolume(double pressure, double temperature)
        {
            const double r = 4.61631e-4d;
            const double b1 = 5.27993e-2d;
            const double b2 = 3.75928e-3d;
            const double b3 = 0.022d;
            const double em = 40.0d;
            const double a0 = -3.741378d;
            const double a1 = -4.7838281e-3d;
            const double a2 = 1.5923434e-5d;
            const double a3 = 10.0d;
            const double c1 = 42.6776d;
            const double c2 = -3892.70d;
            const double c3 = -9.48654d;
            const double pcnv = 0.001d;
            const double c4 = -387.592d;
            const double c5 = -12587.5d;
            const double c6 = -15.2578d;

            double p = pressure * pcnv;
            double t = PhysicsConstants.ToKelvin(temperature);
            double ts = c1 + c2 / (Math.Log(p) + c3);
            if (12.33d <= p) ts = c4 + c5 / (Math.Log(p) + c6);
            return r * t / p - b1 * Math.Exp(-b2 * t)
                + (b3 - Math.Exp(a0 + ts * (a1 + ts * a2))) / (a3 * p) * Math.Exp((ts - t) / em);
        }

        /// <summary>
        /// Gets the specific enthalpy of superheated steam [kJ/kg]
        /// from the pressure [kPa] and temperature [°C].
        /// </summary>
        /// <param name="pressure">Pressure [kPa]</param>
        /// <param name="temperature">Temperature [°C]</param>
        /// <returns>Specific enthalpy of superheated steam [kJ/kg]</returns>
        public static double GetSuperheatedVaporEnthalpy(double pressure, double temperature)
        {
            const double b11 = 2041.21d;
            const double b12 = -40.4002d;
            const double b13 = -0.48095d;
            const double b21 = 1.610693d;
            const double b22 = 5.472051e-2d;
            const double b23 = 7.517537e-4d;
            const double b31 = 3.383117e-4d;
            const double b32 = -1.975736e-5d;
            const double b33 = -2.87409e-7d;
            const double b41 = 1707.82d;
            const double b42 = -16.99419d;
            const double b43 = 6.2746295e-2d;
            const double b44 = -1.0284259e-4d;
            const double b45 = 6.4561298e-8d;
            const double em = 45.0d;
            const double c1 = 42.6776d;
            const double c2 = -3892.70d;
            const double c3 = -9.48654d;
            const double pcnv = 0.001d;
            const double c4 = -387.592d;
            const double c5 = -12587.5d;
            const double c6 = -15.2578d;

            double p = pressure * pcnv;
            double t = PhysicsConstants.ToKelvin(temperature);
            double ts = c1 + c2 / (Math.Log(p) + c3);
            if (12.33d <= p) ts = c4 + c5 / (Math.Log(p) + c6);
            double a0 = b11 + p * (b12 + p * b13);
            double a1 = b21 + p * (b22 + p * b23);
            double a2 = b31 + p * (b32 + p * b33);
            double a3 = b41 + ts * (b42 + ts * (b43 + ts * (b44 + ts * b45)));
            return a0 + t * (a1 + t * a2) - a3 * Math.Exp((ts - t) / em);
        }

        /// <summary>
        /// Gets the specific entropy of superheated steam [kJ/(kg·K)]
        /// from the pressure [kPa] and temperature [°C].
        /// </summary>
        /// <param name="pressure">Pressure [kPa]</param>
        /// <param name="temperature">Temperature [°C]</param>
        /// <returns>Specific entropy of superheated steam [kJ/(kg·K)]</returns>
        public static double GetSuperheatedVaporEntropy(double pressure, double temperature)
        {
            const double a0 = 4.6162961d;
            const double a1 = 1.039008e-2d;
            const double a2 = -9.873085e-6d;
            const double a3 = 5.4311e-9d;
            const double a4 = -1.170465e-12d;
            const double b1 = -0.4650306d;
            const double b2 = 0.001d;
            const double b3 = 10.0d;
            const double c0 = 1.777804d;
            const double c1 = -1.802468e-2d;
            const double c2 = 6.854459e-5d;
            const double c3 = -1.184434e-7d;
            const double em = 85.0d;
            const double c4 = 8.142201e-11d;
            const double e1 = 42.6776d;
            const double e2 = -3892.70d;
            const double e3 = -9.48654d;
            const double e4 = -387.592d;
            const double e5 = -12587.5d;
            const double e6 = -15.2578d;

            double p = pressure * b2;
            double t = PhysicsConstants.ToKelvin(temperature);
            double ts = e1 + e2 / (Math.Log(p) + e3);
            if (12.33d <= p) ts = e4 + e5 / (Math.Log(p) + e6);
            return a0 + t * (a1 + t * (a2 + t * (a3 + t * a4))) + b1 * Math.Log(b2 + p * b3)
                - Math.Exp((ts - t) / em) * (c0 + ts * (c1 + ts * (c2 + ts * (c3 + ts * c4))));
        }

        /// <summary>
        /// Gets the temperature of superheated steam [°C]
        /// from the pressure [kPa] and specific entropy [kJ/(kg·K)].
        /// </summary>
        /// <param name="pressure">Pressure [kPa]</param>
        /// <param name="entropy">Specific entropy [kJ/(kg·K)]</param>
        /// <returns>Temperature of superheated steam [°C]</returns>
        /// <exception cref="PopoloNumericalException">
        /// Thrown when the iterative calculation fails to converge.
        /// </exception>
        public static double GetSuperheatedVaporTemperature(double pressure, double entropy)
        {
            const double e1 = 42.6776d;
            const double e2 = -3892.70d;
            const double e3 = -9.48654d;
            const double pcnv = 0.001d;
            const double e4 = -387.592d;
            const double e5 = -12587.5d;
            const double e6 = -15.2578d;
            //e1, e4 は飽和温度近似式の定数（絶対温度ではない）ため CelsiusToKelvinOffset で補正する
            const double tabs = PhysicsConstants.CelsiusToKelvinOffset;

            //エントロピーの入力値と飽和値を比較
            double t0 = e1 - tabs + e2 / (Math.Log(pressure * pcnv) + e3);
            if (pressure >= 12330.0d) t0 = e4 - tabs + e5 / (Math.Log(pressure * pcnv) + e6);
            double s0 = GetSaturatedVaporEntropy(t0);
            if (s0 >= entropy) return t0;

            //初期推定：定比熱を仮定してケルビン換算で計算
            double tac = PhysicsConstants.ToKelvin(t0)
                * (1.0d + (entropy - s0) / GetSuperheatedVaporIsobaricSpecificHeat(t0));
            double ta = PhysicsConstants.ToCelsius(tac);
            double sa = GetSuperheatedVaporEntropy(pressure, ta);
            double t = 0.0d;
            for (int i = 0; i < 10; i++)
            {
                t = ta + (t0 - ta) * (entropy - sa) / (s0 - sa);
                if (Math.Abs(t - ta) < 0.05d) break;
                t0 = ta;
                s0 = sa;
                ta = t;
                sa = GetSuperheatedVaporEntropy(pressure, ta);
                if (i == 9)
                    throw new PopoloNumericalException(
                        "GetSuperheatedVaporTemperature",
                        $"Convergence failed after 10 iterations. "
                        + $"pressure={pressure} kPa, entropy={entropy} kJ/(kg·K), last t={t} °C.");
            }
            return t;
        }

        /// <summary>
        /// Gets the isobaric specific heat of superheated steam [kJ/(kg·K)]
        /// from the temperature [°C].
        /// </summary>
        /// <param name="temperature">Temperature [°C] (valid range: 27 to 3227 °C, i.e. 300 to 3500 K)</param>
        /// <returns>Isobaric specific heat of superheated steam [kJ/(kg·K)]</returns>
        /// <remarks>
        /// Specific heat equation from Van Wylen and Sonntag, Table A.9, p.683.
        /// Valid for T between 300 and 3500 K; maximum error = 0.43%.
        /// </remarks>
        /// <exception cref="PopoloArgumentException">
        /// Thrown when the temperature is outside the valid range (300–3500 K).
        /// </exception>
        public static double GetSuperheatedVaporIsobaricSpecificHeat(double temperature)
        {
            const double c1 = 143.05d;
            const double c2 = -183.54d;
            const double c3 = 82.751d;
            const double c4 = -3.6989d;
            const double e1 = 0.25d;
            const double e2 = 0.5d;

            double tk = PhysicsConstants.ToKelvin(temperature);
            if (tk < 300.0d || tk > 3500.0d)
                throw new PopoloArgumentException(
                    $"Temperature is out of valid range (300–3500 K). Got: {tk:F2} K ({temperature:F2} °C).",
                    nameof(temperature));
            double t1 = tk / 100.0d;
            return (c1 + c2 * Math.Pow(t1, e1) + c3 * Math.Pow(t1, e2) + c4 * t1) / 18.015d;
        }

        /// <summary>
        /// Gets the isochoric specific heat of superheated steam [kJ/(kg·K)]
        /// from the specific volume [m³/kg] and temperature [°C].
        /// </summary>
        /// <param name="specificVolume">Specific volume [m³/kg]</param>
        /// <param name="temperature">Temperature [°C]</param>
        /// <returns>Isochoric specific heat of superheated steam [kJ/(kg·K)]</returns>
        public static double GetSuperheatedVaporIsochoricSpecificHeat(
            double specificVolume, double temperature)
        {
            const double tc = 1165.11d;
            const double tfr = 459.67d;
            const double b1 = 0.0063101d;
            const double a0 = 0.99204818d;
            const double a1 = -33.137211d;
            const double a2 = 416.29663d;
            const double a3 = 0.185053d;
            const double a4 = 5.475d;
            const double a5 = -2590.5815d;
            const double a6 = 113.95968d;

            //摂氏→ランキン温度に変換
            double tr = 9.0d / 5.0d * temperature + 32.0d + tfr;
            //SI比体積→英国単位に変換
            double ve = (specificVolume - b1) / 0.062428d;
            return (a0 + a1 / Math.Sqrt(tr) + a2 / tr
                - a3 * Math.Pow(a4, 2) * tr / Math.Pow(tc, 2)
                * Math.Exp(-a4 * tr / tc) * (a5 / ve + a6 / Math.Pow(ve, 2))) * 4.1868;
        }

        /// <summary>
        /// Gets the dynamic viscosity of saturated steam [kg/(m·s)]
        /// from the pressure [kPa].
        /// </summary>
        /// <param name="pressure">Pressure [kPa]</param>
        /// <returns>Dynamic viscosity of saturated steam [kg/(m·s)]</returns>
        /// <remarks>'Heat Transfer' by Alan J. Chapman, 1974.</remarks>
        public static double GetSaturatedVaporDynamicViscosity(double pressure)
        {
            const double c1 = 0.0314d;
            const double c2 = 2.9675e-5d;
            const double c3 = -1.60583e-8d;
            const double c4 = 3.768986e-12d;

            //Convert pressure from kPa to psi
            double psi = pressure / 6.894757d;
            double vissv = c1 + c2 * psi + c3 * Math.Pow(psi, 2) + c4 * Math.Pow(psi, 3);
            //Convert viscosity from lbm/ft-hr to kg/m-s
            return vissv * 4.1338e-4d;
        }

        /// <summary>
        /// Gets the dynamic viscosity of superheated steam [kg/(m·s)]
        /// from the temperature [°C].
        /// </summary>
        /// <param name="temperature">Temperature [°C]</param>
        /// <returns>Dynamic viscosity of superheated steam [kg/(m·s)]</returns>
        /// <remarks>
        /// 'Heat Transfer' by Alan J. Chapman, 1974.
        /// Note: there is little variation in viscosity at higher pressures.
        /// </remarks>
        public static double GetSuperheatedVaporDynamicViscosity(double temperature)
        {
            const double c1 = 0.0183161d;
            const double c2 = 5.7067e-5d;
            const double c3 = -1.42253e-8d;
            const double c4 = 7.241555e-12d;

            //Convert temperature from C to F
            double tf = temperature * 1.8 + 32.0d;
            double vissph = c1 + c2 * tf + c3 * Math.Pow(tf, 2) + c4 * Math.Pow(tf, 3);
            //Convert viscosity from lbm/ft-hr to kg/m-s
            return vissph * 4.1338e-4d;
        }

        /// <summary>
        /// Gets the thermal conductivity of superheated steam [kW/(m·°C)]
        /// from the temperature [°C].
        /// </summary>
        /// <param name="temperature">Temperature [°C]</param>
        /// <returns>Thermal conductivity of superheated steam [kW/(m·°C)]</returns>
        /// <remarks>'Heat Transfer' by Alan J. Chapman, 1974.</remarks>
        public static double GetSuperheatedVaporThermalConductivity(double temperature)
        {
            const double c1 = 0.824272d;
            const double c2 = 0.00254627d;
            const double c3 = 9.848539e-8d;

            //Convert temperature from C to F
            double tf = temperature * 1.8d + 32.0d;
            double steamk = (c1 + c2 * tf + c3 * Math.Pow(tf, 2)) * 0.01d;
            //Convert K from Btu/hr-ft-F to kW/m-C
            return steamk * 0.0017308d;
        }

        #endregion

    }
}
