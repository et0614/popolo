/* GaggeModel.cs
 * 
 * Copyright (C) 2014 E.Togashi
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

using Popolo.Core.Physics;
using Popolo.Core.Numerics;

namespace Popolo.Core.ThermalComfort
{
  /// <summary>Implements the Gagge two-node thermoregulatory model for computing skin temperature,
  /// core temperature, SET*, and related heat loss components.</summary>
  /// <remarks>
  /// Based on: Gagge et al. (1986). A standard predictive index of human response to the thermal environment.
  /// ASHRAE Transactions, 92(2), 709–731.
  /// </remarks>
  public class GaggeModel : IReadOnlyGaggeModel
  {

    #region 定数宣言

    /// <summary>Clothing area factor coefficient [-].</summary>
    private const double K_CLO = 0.25;

    /// <summary>Clothing moisture permeability index [K/kPa].</summary>
    private const double I_CLS = 0.45;

    /// <summary>Conversion factor from metabolic rate [met] to heat flux [W/m²].</summary>
    private const double CONVERT_MET_TO_W = 58.2;

    #endregion

    #region インスタンス変数

    /// <summary>Gets the age of the occupant [years].</summary>
    public uint Age { private set; get; }

    /// <summary>Gets the height [m].</summary>
    public double Height { private set; get; }

    /// <summary>Gets the weight [kg].</summary>
    public double Weight { private set; get; }

    /// <summary>Gets the basal metabolic rate [W/m²].</summary>
    public double BasalMetabolism { private set; get; }

    /// <summary>Gets the skin temperature [°C].</summary>
    public double SkinTemperature { private set; get; } = 33;

    /// <summary>Gets the core (rectal) temperature [°C].</summary>
    public double CoreTemperature { private set; get; } = 35;

    /// <summary>Gets the mean body temperature [°C].</summary>
    public double BodyTemperature { private set; get; } = 34;

    /// <summary>Gets the clothing surface temperature [°C].</summary>
    public double ClothTemperature { private set; get; } = 25;

    /// <summary>Gets the sensible heat loss from skin [W/m²].</summary>
    public double SensibleHeatLossFromSkin { private set; get; }

    /// <summary>Gets the latent heat loss from skin [W/m²].</summary>
    public double LatentHeatLossFromSkin { private set; get; }

    /// <summary>Gets the sensible heat loss by respiration [W/m²].</summary>
    public double SensibleHeatLossByRespiration { private set; get; }

    /// <summary>Gets the latent heat loss by respiration [W/m²].</summary>
    public double LatentHeatLossByRespiration { private set; get; }

    /// <summary>Gets the mean skin wettedness [-].</summary>
    public double Wettedness { private set; get; }

    /// <summary>Gets the body surface area (Du Bois) [m²].</summary>
    public double BodySurface { private set; get; }

    /// <summary>Gets the normal skin blood flow rate [mL/(m²·s)].</summary>
    public double NormalBloodFlow { private set; get; }

    #endregion

    #region コンストラクタ・インスタンスメソッド

    /// <summary>Initializes a new instance of the Gagge two-node model.</summary>
    /// <param name="age">Age [years].</param>
    /// <param name="isMale">True for male; false for female.</param>
    /// <param name="height">Height [m].</param>
    /// <param name="weight">Weight [kg].</param>
    public GaggeModel(uint age, bool isMale, double height, double weight)
    {
      this.Age = age;
      this.Height = height;
      this.Weight = weight;
      this.BodySurface = 0.202 * Math.Pow(weight, 0.425) * Math.Pow(height, 0.725);

      double rCI = 1.66 + age * (-3.48e-2 + age * (2.42e-4 + age * (5.16e-6 - age * 6.22e-8)));
      this.NormalBloodFlow = 1.75 * rCI * BodySurface / 1.8;
      this.BasalMetabolism = (0.1238 + 2.34 * Height + 0.0481 * Weight - 0.0138 * Age - 0.5473 * (isMale ? 1 : 2)) / (0.0864 * BodySurface);
    }

    /// <summary>Updates the thermoregulatory state for one time step.</summary>
    /// <param name="timeStep">Time step [s].</param>
    /// <param name="dryBulbTemperature">Dry-bulb temperature [°C].</param>
    /// <param name="meanRadiantTemperature">Mean radiant temperature [°C].</param>
    /// <param name="relativeHumidity">Relative humidity [%].</param>
    /// <param name="velocity">Relative air velocity [m/s].</param>
    /// <param name="clothing">Clothing insulation [clo].</param>
    /// <param name="metabolicRate">Metabolic rate [met].</param>
    /// <param name="externalWork">External mechanical work [W/m²].</param>
    /// <param name="atmosphericPressure">Atmospheric pressure [kPa].</param>
    public void UpdateState
      (double timeStep, double dryBulbTemperature, double meanRadiantTemperature, double relativeHumidity, 
      double velocity, double clothing, double metabolicRate, double externalWork, double atmosphericPressure)
    {
      //定数宣言
      const double C_SWEATING = 47.2;         //発汗の係数[mg/(m2 s K)]
      const double C_VASODILATATION = 55.6;   //血管拡張の係数[L/(m2 s K)]
      const double C_VASOCONSTRICTION = 0.1;  //血管収縮の係数[1/K]
      const double SETPOINT_SKIN = 33.7;      //皮膚セットポイント温度[C]
      const double SETPOINT_CORE = 36.8;      //コアセットポイント温度[C]
      const double SETPOINT_BODY = 36.49;     //体温セットポイント温度[C]
      const double CRITICAL_WETTEDNESS = 0.85;//最大濡れ率[-]

      //代謝量は基礎代謝を0.7metとして線形比例させる
      double metab = BasalMetabolism * metabolicRate / 0.7;
      
      //水蒸気分圧[kPa]の計算
      double pa = relativeHumidity / 100 * Water.GetSaturationPressure(dryBulbTemperature);
      //着衣抵抗[m2K/W]の計算
      double rcl = 0.155 * clothing;
      //着衣面積率[-]の計算
      double clothRate = 1d + K_CLO * clothing;

      //対流熱伝達率の計算（代謝量か気流で決定）
      double chcv = 8.6 * Math.Pow(Math.Max(0.15, velocity), 0.53);
      double chcm = 5.66 * Math.Pow(Math.Max(0, metab / CONVERT_MET_TO_W - 0.85), 0.39);
      double convectiveHTransCoef = Math.Max(chcv, chcm);

      //最大で1minの時間間隔で更新
      while (true)
      {
        double tStep = Math.Min(timeStep, 60);
        if (timeStep < 60) tStep = timeStep;
        else tStep = 60;

        //衣服の表面温度を収束計算
        double operatingTemp, ra;
        while (true)
        {
          double ctOld = ClothTemperature;
          //放射熱伝達率[W/(m2K)]の計算
          double hr = 4d * PhysicsConstants.StefanBoltzmannConstant * 0.72
            * Math.Pow(PhysicsConstants.ToKelvin((ClothTemperature + meanRadiantTemperature) / 2d), 3);
          //総合熱伝達率[W/(m2K)]の計算
          double hcr = hr + convectiveHTransCoef;
          //空気層顕熱抵抗[(m2K)/W]の計算
          ra = 1 / (clothRate * hcr);
          //作用温度[C]の計算
          operatingTemp = (hr * meanRadiantTemperature + convectiveHTransCoef * dryBulbTemperature) / hcr;
          //衣服温度[C]の計算
          ClothTemperature = (ra * SkinTemperature + rcl * operatingTemp) / (ra + rcl);
          //衣服温度の更新量が0.01C以下で収束と判定
          if (Math.Abs(ctOld - ClothTemperature) < 0.01) break;
        }

        //制御量の計算/////////////////////////////////////////////////////////
        double sDil = Math.Max(0, CoreTemperature - SETPOINT_CORE);
        double sStr = Math.Max(0, SETPOINT_SKIN - SkinTemperature);
        double sSw1 = Math.Max(0, BodyTemperature - SETPOINT_BODY);
        double sSw2 = Math.Max(0, SkinTemperature - SETPOINT_SKIN);
        double sShv1 = Math.Max(0, SETPOINT_SKIN - SkinTemperature);
        double sShv2 = Math.Max(0, SETPOINT_CORE - CoreTemperature);

        //皮膚血流量[L/(m2 s)]の計算
        double skinBloodFlow = (NormalBloodFlow + C_VASODILATATION * sDil) / (1d + C_VASOCONSTRICTION * sStr);
        skinBloodFlow = Math.Max(Math.Min(skinBloodFlow, 25), 0.139);
        //皮膚・コアの重量比を更新
        double alpha = 0.0417737 + 0.2069953 / (skinBloodFlow + 0.1626158);

        //調節発汗による蒸発熱損失[W/m2]を計算
        double msw = C_SWEATING * sSw1 * Math.Exp(sSw2 / 10.7);
        double esw = 2.501 * msw;

        //不感蒸泄による蒸発熱損失[W/m2]を計算
        double lewis = 0.0555 * PhysicsConstants.ToKelvin(SkinTemperature);
        double latentHTransCoef = 1 / (rcl / (I_CLS * lewis) + 1d / (clothRate * convectiveHTransCoef * lewis));
        double emax = latentHTransCoef * (Water.GetSaturationPressure(SkinTemperature) - pa);
        double wSw = esw / emax;
        double eb = 0.06 * (emax - esw);
        double wB = eb / emax;
        Wettedness = wSw + wB;
        LatentHeatLossFromSkin = esw + eb;

        //ぬれ率上限を上回る場合には蒸発熱損失等を修正
        if (CRITICAL_WETTEDNESS < Wettedness)
        {
          Wettedness = CRITICAL_WETTEDNESS;
          wSw = (CRITICAL_WETTEDNESS - 0.06) / 0.94;
          esw = wSw * emax;
          eb = 0.06 * (1 - wSw) * emax;
          LatentHeatLossFromSkin = esw + eb;
        }
        //皮膚表面が露点以下の場合
        if (emax < 0)
        {
          eb = 0;
          esw = 0;
          wB = CRITICAL_WETTEDNESS;
          wSw = CRITICAL_WETTEDNESS;
          LatentHeatLossFromSkin = emax;
        }

        //ふるえ熱産生量[W/m2]を計算
        double mshiv = 19.4 * sShv1 * sShv2;
        //基礎代謝とふるえ熱産生を加算した値を代謝量とする
        double metabolism = metab + mshiv;

        //制御量の計算ここまで/////////////////////////////////////////////////////////

        //皮膚からの顕熱損失量[W/(m2K)]の計算
        SensibleHeatLossFromSkin = (SkinTemperature - operatingTemp) / (ra + rcl);
        //コアから皮膚への熱流[W/m2]の計算
        double hfcs = (CoreTemperature - SkinTemperature) * (5.28 + 3.842 * skinBloodFlow);

        //呼吸による熱損失量[W/m2]の計算
        LatentHeatLossByRespiration = metabolism * 0.017251 * (5.8662 - pa);
        SensibleHeatLossByRespiration = metabolism * 0.0014 * (34 - dryBulbTemperature);

        //コアと皮膚への熱流[W/m2]を計算
        double scr = metabolism - hfcs - LatentHeatLossByRespiration - SensibleHeatLossByRespiration - externalWork;
        double ssk = hfcs - SensibleHeatLossFromSkin - LatentHeatLossFromSkin;
        //コアと皮膚の熱容量[J/K]を計算
        double tccr = 3492 * (1 - alpha) * Weight;
        double tcsk = 3492 * alpha * Weight;
        //コアと皮膚の温度変化量[K/s]を計算
        double dtcr = (scr * BodySurface) / tccr;
        double dtsk = (ssk * BodySurface) / tcsk;
        //コアと皮膚の温度を更新
        CoreTemperature = CoreTemperature + dtcr * tStep;
        SkinTemperature = SkinTemperature + dtsk * tStep;

        //重み付けして体温を計算
        BodyTemperature = alpha * SkinTemperature + (1 - alpha) * CoreTemperature;

        //衣服の温度を更新
        ClothTemperature = (ra * SkinTemperature + rcl * operatingTemp) / (ra + rcl);

        if (timeStep < 60) return;
        else timeStep -= tStep;
      }
    }

    #endregion

    #region staticメソッド

    /// <summary>Computes the steady-state thermoregulatory conditions.</summary>
    /// <param name="dryBulbTemperature">Dry-bulb temperature [°C].</param>
    /// <param name="meanRadiantTemperature">Mean radiant temperature [°C].</param>
    /// <param name="relativeHumidity">Relative humidity [%].</param>
    /// <param name="velocity">Relative air velocity [m/s].</param>
    /// <param name="clothing">Clothing insulation [clo].</param>
    /// <param name="basalMetabolism">Basal metabolic rate [W/m²].</param>
    /// <param name="externalWork">External mechanical work [W/m²].</param>
    /// <param name="skinTemperature">Output: skin temperature [°C].</param>
    /// <param name="coreTemperature">Output: core temperature [°C].</param>
    /// <param name="bodyTemperature">Output: mean body temperature [°C].</param>
    /// <param name="clothTemperature">Output: clothing surface temperature [°C].</param>
    /// <param name="sensibleHFSkin">Output: sensible heat loss from skin [W/m²].</param>
    /// <param name="latentHFSkin">Output: latent heat loss from skin [W/m²].</param>
    /// <param name="sensibleRespiration">Output: sensible heat loss by respiration [W/m²].</param>
    /// <param name="latentRespiration">Output: latent heat loss by respiration [W/m²].</param>
    /// <param name="wettedness">Output: mean skin wettedness [-].</param>
    public static void GetSteadyState
      (double dryBulbTemperature, double meanRadiantTemperature, double relativeHumidity, double velocity, 
      double clothing, double basalMetabolism, double externalWork, 
      out double skinTemperature, out double coreTemperature, out double bodyTemperature, 
      out double clothTemperature, out double sensibleHFSkin, out double latentHFSkin, 
      out double sensibleRespiration, out double latentRespiration, out double wettedness)
    {
      //定数宣言
      const double WEIGHT = 70d;              //標準体重[kg]
      const double BODY_SURFACE = 1.8;        //標準体表面積[m2]
      const double C_SWEATING = 47.2;         //発汗の係数[mg/(m2 s K)]
      const double C_VASODILATATION = 55.6;   //血管拡張の係数[L/(m2 s K)]
      const double C_VASOCONSTRICTION = 0.1;  //血管収縮の係数[1/K]
      const double SETPOINT_SKIN = 33.7;      //皮膚セットポイント温度[C]
      const double SETPOINT_CORE = 36.8;      //コアセットポイント温度[C]
      const double SETPOINT_BODY = 36.49;     //体温セットポイント温度[C]
      const double NORMAL_BLOOD_FLOW = 1.75;  //標準状態の皮膚血流量[mL/(m2 s)]
      const double CRITICAL_WETTEDNESS = 0.85;//最大濡れ率[-]

      //水蒸気分圧[kPa]の計算
      double pa = relativeHumidity / 100 * Water.GetSaturationPressure(dryBulbTemperature);
      //着衣抵抗[m2K/W]の計算
      double rcl = 0.155 * clothing;
      //着衣面積率[-]の計算
      double clothRate = 1d + K_CLO * clothing;

      //初期値を設定する
      sensibleHFSkin = sensibleRespiration = latentRespiration = wettedness = 0;
      skinTemperature = SETPOINT_SKIN;
      coreTemperature = SETPOINT_CORE;
      bodyTemperature = SETPOINT_BODY;
      double skinBloodFlow = NORMAL_BLOOD_FLOW;
      double alpha = 0.1;
      latentHFSkin = 0.1 * basalMetabolism;
      double metabolism = basalMetabolism;   //ふるえ産熱は0とする

      //対流熱伝達率の計算（代謝量か気流で決定）
      double chcv = 8.6 * Math.Pow(Math.Max(0.15, velocity), 0.53);
      double chcm = 5.66 * Math.Pow(Math.Max(0, basalMetabolism / CONVERT_MET_TO_W - 0.85), 0.39);
      double convectiveHTransCoef = Math.Max(chcv, chcm);

      //Δt=1minとして60minの繰り返し計算を行う
      const int DELTA_T = 1;
      clothTemperature = (skinTemperature + dryBulbTemperature) / 2d;
      for (int tim = 0; tim < 60; tim += DELTA_T)
      {
        //衣服の表面温度を収束計算
        double operatingTemp, ra;
        while (true)
        {
          double ctOld = clothTemperature;
          //放射熱伝達率[W/(m2K)]の計算
          double hr = 4d * PhysicsConstants.StefanBoltzmannConstant * 0.72 
            * Math.Pow(PhysicsConstants.ToKelvin((clothTemperature + meanRadiantTemperature) / 2d), 3);
          //総合熱伝達率[W/(m2K)]の計算
          double hcr = hr + convectiveHTransCoef;
          //空気層顕熱抵抗[(m2K)/W]の計算
          ra = 1 / (clothRate * hcr);
          //作用温度[C]の計算
          operatingTemp = (hr * meanRadiantTemperature + convectiveHTransCoef * dryBulbTemperature) / hcr;
          //衣服温度[C]の計算
          clothTemperature = (ra * skinTemperature + rcl * operatingTemp) / (ra + rcl);
          //衣服温度の更新量が0.01C以下で収束と判定
          if (Math.Abs(ctOld - clothTemperature) < 0.01) break;
        }

        //皮膚からの顕熱損失量[W/(m2K)]の計算
        sensibleHFSkin = (skinTemperature - operatingTemp) / (ra + rcl);
        //コアから皮膚への熱流[W/m2]の計算
        double hfcs = (coreTemperature - skinTemperature) * (5.28 + 3.842 * skinBloodFlow);

        //呼吸による熱損失量[W/m2]の計算
        latentRespiration = metabolism * 0.017251 * (5.8662 - pa);
        sensibleRespiration = metabolism * 0.0014 * (34 - dryBulbTemperature);

        //コアと皮膚への熱流[W/m2]を計算
        double scr = metabolism - hfcs - latentRespiration - sensibleRespiration - externalWork;
        double ssk = hfcs - sensibleHFSkin - latentHFSkin;
        //コアと皮膚の熱容量[J/K]を計算
        double tccr = 3492 * (1 - alpha) * WEIGHT;
        double tcsk = 3492 * alpha * WEIGHT;
        //コアと皮膚の温度変化量[K/s]を計算
        double dtcr = (scr * BODY_SURFACE) / tccr;
        double dtsk = (ssk * BODY_SURFACE) / tcsk;
        //コアと皮膚の温度を更新
        coreTemperature = coreTemperature + dtcr * (DELTA_T * 60);
        skinTemperature = skinTemperature + dtsk * (DELTA_T * 60);

        //重み付けして体温を計算
        bodyTemperature = alpha * skinTemperature + (1 - alpha) * coreTemperature;

        //制御量の計算
        double sDil = Math.Max(0, coreTemperature - SETPOINT_CORE);
        double sStr = Math.Max(0, SETPOINT_SKIN - skinTemperature);
        double sSw1 = Math.Max(0, bodyTemperature - SETPOINT_BODY);
        double sSw2 = Math.Max(0, skinTemperature - SETPOINT_SKIN);
        double sShv1 = Math.Max(0, SETPOINT_SKIN - skinTemperature);
        double sShv2 = Math.Max(0, SETPOINT_CORE - coreTemperature);

        //皮膚血流量[L/(m2 s)]の計算
        skinBloodFlow = (NORMAL_BLOOD_FLOW + C_VASODILATATION * sDil) / (1d + C_VASOCONSTRICTION * sStr);
        skinBloodFlow = Math.Max(Math.Min(skinBloodFlow, 25), 0.139);
        //皮膚・コアの重量比を更新
        alpha = 0.0417737 + 0.2069953 / (skinBloodFlow + 0.1626158);

        //調節発汗による蒸発熱損失[W/m2]を計算
        double msw = C_SWEATING * sSw1 * Math.Exp(sSw2 / 10.7);
        double esw = 2.501 * msw;

        //不感蒸泄による蒸発熱損失[W/m2]を計算
        double lewis = 0.0555 * (PhysicsConstants.ToKelvin(skinTemperature));
        double latentHTransCoef = 1 / (rcl / (I_CLS * lewis) + 1d / (clothRate * convectiveHTransCoef * lewis));
        double emax = latentHTransCoef * (Water.GetSaturationPressure(skinTemperature) - pa);
        double wSw = esw / emax;
        double eb = 0.06 * (emax - esw);
        double wB = eb / emax;
        wettedness = wSw + wB;
        latentHFSkin = esw + eb;

        //ぬれ率上限を上回る場合には蒸発熱損失等を修正
        if (CRITICAL_WETTEDNESS < wettedness)
        {
          wettedness = CRITICAL_WETTEDNESS;
          wSw = (CRITICAL_WETTEDNESS - 0.06) / 0.94;
          esw = wSw * emax;
          eb = 0.06 * (1 - wSw) * emax;
          latentHFSkin = esw + eb;
        }
        //皮膚表面が露点以下の場合
        if (emax < 0) latentHFSkin = emax;

        //ふるえ熱産生量[W/m2]を計算
        double mshiv = 19.4 * sShv1 * sShv2;
        //基礎代謝とふるえ熱産生を加算した値を代謝量とする
        metabolism = basalMetabolism + mshiv;

        //衣服の温度を更新
        clothTemperature = (ra * skinTemperature + rcl * operatingTemp) / (ra + rcl);
      }
    }

    /// <summary>Computes the steady-state thermoregulatory conditions with body dimension adjustment.</summary>
    /// <param name="age">Age [years].</param>
    /// <param name="height">Height [m].</param>
    /// <param name="weight">Weight [kg].</param>
    /// <param name="dryBulbTemperature">Dry-bulb temperature [°C].</param>
    /// <param name="meanRadiantTemperature">Mean radiant temperature [°C].</param>
    /// <param name="relativeHumidity">Relative humidity [%].</param>
    /// <param name="velocity">Relative air velocity [m/s].</param>
    /// <param name="clothing">Clothing insulation [clo].</param>
    /// <param name="basalMetabolism">Basal metabolic rate [W/m²].</param>
    /// <param name="externalWork">External mechanical work [W/m²].</param>
    /// <param name="skinTemperature">Output: skin temperature [°C].</param>
    /// <param name="coreTemperature">Output: core temperature [°C].</param>
    /// <param name="bodyTemperature">Output: mean body temperature [°C].</param>
    /// <param name="clothTemperature">Output: clothing surface temperature [°C].</param>
    /// <param name="sensibleHFSkin">Output: sensible heat loss from skin [W/m²].</param>
    /// <param name="latentHFSkin">Output: latent heat loss from skin [W/m²].</param>
    /// <param name="sensibleRespiration">Output: sensible heat loss by respiration [W/m²].</param>
    /// <param name="latentRespiration">Output: latent heat loss by respiration [W/m²].</param>
    /// <param name="wettedness">Output: mean skin wettedness [-].</param>
    public static void GetSteadyState
      (int age, double height, double weight, 
      double dryBulbTemperature, double meanRadiantTemperature, double relativeHumidity, double velocity,
      double clothing, double basalMetabolism, double externalWork,
      out double skinTemperature, out double coreTemperature, out double bodyTemperature,
      out double clothTemperature, out double sensibleHFSkin, out double latentHFSkin,
      out double sensibleRespiration, out double latentRespiration, out double wettedness)
    {
      //定数宣言
      const double C_SWEATING = 47.2;         //発汗の係数[mg/(m2 s K)]
      const double C_VASODILATATION = 55.6;   //血管拡張の係数[L/(m2 s K)]
      const double C_VASOCONSTRICTION = 0.1;  //血管収縮の係数[1/K]
      const double SETPOINT_SKIN = 33.7;      //皮膚セットポイント温度[C]
      const double SETPOINT_CORE = 36.8;      //コアセットポイント温度[C]
      const double SETPOINT_BODY = 36.49;     //体温セットポイント温度[C]
      const double CRITICAL_WETTEDNESS = 0.85;//最大濡れ率[-]

      //体躯を反映した体表面と血流を計算
      double bodySurface = 0.202 * Math.Pow(weight, 0.425) * Math.Pow(height, 0.725);
      double normalBloodFlow = 3.14 + age * (-6.55e-2 + age * (4.55e-4 + age * (9.71e-6 - 1.17e-7 * age)));

      //水蒸気分圧[kPa]の計算
      double pa = relativeHumidity / 100 * Water.GetSaturationPressure(dryBulbTemperature);
      //着衣抵抗[m2K/W]の計算
      double rcl = 0.155 * clothing;
      //着衣面積率[-]の計算
      double clothRate = 1d + K_CLO * clothing;

      //初期値を設定する
      sensibleHFSkin = sensibleRespiration = latentRespiration = wettedness = 0;
      skinTemperature = SETPOINT_SKIN;
      coreTemperature = SETPOINT_CORE;
      bodyTemperature = SETPOINT_BODY;
      double skinBloodFlow = normalBloodFlow;
      double alpha = 0.1;
      latentHFSkin = 0.1 * basalMetabolism;
      double metabolism = basalMetabolism;   //ふるえ産熱は0とする

      //対流熱伝達率の計算（代謝量か気流で決定）
      double chcv = 8.6 * Math.Pow(Math.Max(0.15, velocity), 0.53);
      double chcm = 5.66 * Math.Pow(Math.Max(0, basalMetabolism / CONVERT_MET_TO_W - 0.85), 0.39);
      double convectiveHTransCoef = Math.Max(chcv, chcm);

      //Δt=1minとして60minの繰り返し計算を行う
      const int DELTA_T = 1;
      clothTemperature = (skinTemperature + dryBulbTemperature) / 2d;
      for (int tim = 0; tim < 60; tim += DELTA_T)
      {
        //衣服の表面温度を収束計算
        double operatingTemp, ra;
        while (true)
        {
          double ctOld = clothTemperature;
          //放射熱伝達率[W/(m2K)]の計算
          double hr = 4d * PhysicsConstants.StefanBoltzmannConstant * 0.72
            * Math.Pow(PhysicsConstants.ToKelvin((clothTemperature + meanRadiantTemperature) / 2d), 3);
          //総合熱伝達率[W/(m2K)]の計算
          double hcr = hr + convectiveHTransCoef;
          //空気層顕熱抵抗[(m2K)/W]の計算
          ra = 1 / (clothRate * hcr);
          //作用温度[C]の計算
          operatingTemp = (hr * meanRadiantTemperature + convectiveHTransCoef * dryBulbTemperature) / hcr;
          //衣服温度[C]の計算
          clothTemperature = (ra * skinTemperature + rcl * operatingTemp) / (ra + rcl);
          //衣服温度の更新量が0.01C以下で収束と判定
          if (Math.Abs(ctOld - clothTemperature) < 0.01) break;
        }

        //皮膚からの顕熱損失量[W/(m2K)]の計算
        sensibleHFSkin = (skinTemperature - operatingTemp) / (ra + rcl);
        //コアから皮膚への熱流[W/m2]の計算
        double hfcs = (coreTemperature - skinTemperature) * (5.28 + 3.842 * skinBloodFlow);

        //呼吸による熱損失量[W/m2]の計算
        latentRespiration = metabolism * 0.017251 * (5.8662 - pa);
        sensibleRespiration = metabolism * 0.0014 * (34 - dryBulbTemperature);

        //コアと皮膚への熱流[W/m2]を計算
        double scr = metabolism - hfcs - latentRespiration - sensibleRespiration - externalWork;
        double ssk = hfcs - sensibleHFSkin - latentHFSkin;
        //コアと皮膚の熱容量[J/K]を計算
        double tccr = 3492 * (1 - alpha) * weight;
        double tcsk = 3492 * alpha * weight;
        //コアと皮膚の温度変化量[K/s]を計算
        double dtcr = (scr * bodySurface) / tccr;
        double dtsk = (ssk * bodySurface) / tcsk;
        //コアと皮膚の温度を更新
        coreTemperature = coreTemperature + dtcr * (DELTA_T * 60);
        skinTemperature = skinTemperature + dtsk * (DELTA_T * 60);

        //重み付けして体温を計算
        bodyTemperature = alpha * skinTemperature + (1 - alpha) * coreTemperature;

        //制御量の計算
        double sDil = Math.Max(0, coreTemperature - SETPOINT_CORE);
        double sStr = Math.Max(0, SETPOINT_SKIN - skinTemperature);
        double sSw1 = Math.Max(0, bodyTemperature - SETPOINT_BODY);
        double sSw2 = Math.Max(0, skinTemperature - SETPOINT_SKIN);
        double sShv1 = Math.Max(0, SETPOINT_SKIN - skinTemperature);
        double sShv2 = Math.Max(0, SETPOINT_CORE - coreTemperature);

        //皮膚血流量[L/(m2 s)]の計算
        skinBloodFlow = (normalBloodFlow + C_VASODILATATION * sDil) / (1d + C_VASOCONSTRICTION * sStr);
        skinBloodFlow = Math.Max(Math.Min(skinBloodFlow, 25), 0.139);
        //皮膚・コアの重量比を更新
        alpha = 0.0417737 + 0.2069953 / (skinBloodFlow + 0.1626158);

        //調節発汗による蒸発熱損失[W/m2]を計算
        double msw = C_SWEATING * sSw1 * Math.Exp(sSw2 / 10.7);
        double esw = 2.501 * msw;

        //不感蒸泄による蒸発熱損失[W/m2]を計算
        double lewis = 0.0555 * (PhysicsConstants.ToKelvin(skinTemperature));
        double latentHTransCoef = 1 / (rcl / (I_CLS * lewis) + 1d / (clothRate * convectiveHTransCoef * lewis));
        double emax = latentHTransCoef * (Water.GetSaturationPressure(skinTemperature) - pa);
        double wSw = esw / emax;
        double eb = 0.06 * (emax - esw);
        double wB = eb / emax;
        wettedness = wSw + wB;
        latentHFSkin = esw + eb;

        //ぬれ率上限を上回る場合には蒸発熱損失等を修正
        if (CRITICAL_WETTEDNESS < wettedness)
        {
          wettedness = CRITICAL_WETTEDNESS;
          wSw = (CRITICAL_WETTEDNESS - 0.06) / 0.94;
          esw = wSw * emax;
          eb = 0.06 * (1 - wSw) * emax;
          latentHFSkin = esw + eb;
        }
        //皮膚表面が露点以下の場合
        if (emax < 0) latentHFSkin = emax;

        //ふるえ熱産生量[W/m2]を計算
        double mshiv = 19.4 * sShv1 * sShv2;
        //基礎代謝とふるえ熱産生を加算した値を代謝量とする
        metabolism = basalMetabolism + mshiv;

        //衣服の温度を更新
        clothTemperature = (ra * skinTemperature + rcl * operatingTemp) / (ra + rcl);
      }
    }

    /// <summary>Computes SET* [°C] directly from ambient conditions.</summary>
    /// <param name="dryBulbTemperature">Dry-bulb temperature [°C].</param>
    /// <param name="meanRadiantTemperature">Mean radiant temperature [°C].</param>
    /// <param name="velocity">Relative air velocity [m/s].</param>
    /// <param name="relativeHumidity">Relative humidity [%].</param>
    /// <param name="clothing">Clothing insulation [clo].</param>
    /// <param name="basalMetabolism">Basal metabolic rate [W/m²].</param>
    /// <param name="externalWork">External mechanical work [W/m²].</param>
    /// <returns>SET*[C]</returns>
    public static double GetSETStarFromAmbientCondition
      (double dryBulbTemperature, double meanRadiantTemperature, double relativeHumidity, double velocity, 
      double clothing, double basalMetabolism, double externalWork)
    {
      double st, ct, bt, clt, ss, ls, sr, lr, wd;
      GetSteadyState(dryBulbTemperature, meanRadiantTemperature, relativeHumidity,
        velocity, clothing, basalMetabolism, externalWork,
        out st, out ct, out bt, out clt, out ss, out ls, out sr, out lr, out wd);
      return GetSETStar(meanRadiantTemperature, basalMetabolism, externalWork, clt, st, ss, ls, wd);
    }

    /// <summary>Computes the Standard Effective Temperature (SET*) [°C].</summary>
    /// <param name="meanRadiantTemperature">Mean radiant temperature [°C].</param>
    /// <param name="basalMetabolism">Basal metabolic rate [W/m²].</param>
    /// <param name="externalWork">External mechanical work [W/m²].</param>
    /// <param name="clothTemperature">Clothing surface temperature [°C].</param>
    /// <param name="skinTemperature">Skin temperature [°C].</param>
    /// <param name="sensibleHFSkin">Sensible heat loss from skin [W/m²].</param>
    /// <param name="latentHFSkin">Latent heat loss from skin [W/m²].</param>
    /// <param name="wettedness">Mean skin wettedness [-].</param>
    /// <returns>SET*[C]</returns>
    public static double GetSETStar
      (double meanRadiantTemperature, double basalMetabolism, double externalWork, double clothTemperature,
      double skinTemperature, double sensibleHFSkin, double latentHFSkin, double wettedness)
    {
      //放射熱伝達率[W/(m2K)]の計算
      double radiativeHTransCoef = 4d * PhysicsConstants.StefanBoltzmannConstant * 0.72
        * Math.Pow(PhysicsConstants.ToKelvin((clothTemperature + meanRadiantTemperature) / 2d), 3);
      //対流熱伝達率[W/(m2 K)]の計算
      double convectiveHTransCoef = 5.66 * Math.Pow(Math.Max(0, basalMetabolism / CONVERT_MET_TO_W - 0.85), 0.39);
      convectiveHTransCoef = Math.Max(convectiveHTransCoef, 3d);

      //標準着衣量[clo]の計算
      double sClothing = 1.3264 / ((basalMetabolism - externalWork) / CONVERT_MET_TO_W + 0.7383) - 0.0953;

      //着衣抵抗[m2K/W]の計算
      double rcl = 0.155 * sClothing;
      //着衣面積率[-]の計算
      double clothRate = 1d + K_CLO * sClothing;

      //潜熱伝達率[W/(m2 K)]の計算
      double lewis = 0.0555 * (PhysicsConstants.ToKelvin(skinTemperature));
      double latentHTransCoef = 1 / (rcl / (I_CLS * lewis) + 1d / (clothRate * convectiveHTransCoef * lewis));

      //顕熱伝達率[W/(m2K)]の計算
      double hcr = radiativeHTransCoef + convectiveHTransCoef;
      double sensibleHTransCoef = 1 / (1 / (clothRate * hcr) + rcl);

      //反復計算でSET*を計算
      double hfSkin = sensibleHFSkin + latentHFSkin;
      double psk = Water.GetSaturationPressure(skinTemperature);
      Roots.ErrorFunction eFnc = delegate (double setStar)
      {
        return hfSkin - sensibleHTransCoef * (skinTemperature - setStar)
        - wettedness * latentHTransCoef * (psk - Water.GetSaturationPressure(setStar) * 0.5);
      };
      return Roots.Newton(eFnc, 26, 1e-3, 1e-3, 1e-3, 20);
    }

    #endregion

  }

}
