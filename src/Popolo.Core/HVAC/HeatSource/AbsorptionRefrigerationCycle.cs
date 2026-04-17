/* AbsorptionRefrigerationCycle.cs
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
using Popolo.Core.HVAC.HeatExchanger;

namespace Popolo.Core.HVAC.HeatSource
{
  /// <summary>Provides static methods for single-effect and double-effect absorption refrigeration cycle calculations.</summary>
  public static class AbsorptionRefrigerationCycle
  {

    #region 定数宣言


    /// <summary>Nominal evaporating temperature [°C].</summary>
    /// <remarks>A lower limit of approximately 5°C applies due to vacuum maintenance requirements.</remarks>
    public const double NOM_EVP_TEMP = 5;

    /// <summary>Nominal condensing temperature [°C].</summary>
    public const double NOM_CND_TEMP = 40;

    /// <summary>Nominal desorption temperature (solution side) [°C].</summary>
    /// <remarks>An upper limit of approximately 160°C applies due to mild-steel corrosion concerns.</remarks>
    public const double NOM_DSB_LIQ_TEMP = 155;

    /// <summary>Nominal desorption temperature (saturated vapour side) [°C].</summary>
    /// <remarks>To avoid pressure vessel requirements, keeping pressure below atmospheric gives approximately 98°C.</remarks>
    public const double NOM_DSB_VAP_TEMP = 98;
    
    /// <summary>Heat loss fraction relative to the high-temperature desorber heat input.</summary>
    private const double HEATLOSS_RATE = 0.05;

    #endregion

    #region 単効用吸収冷凍サイクル関連のメソッド

    /// <summary>Computes the overall heat transfer conductances [kW/K] from rated operating conditions.</summary>
    /// <param name="chWaterITemperature">Chilled water inlet temperature [°C].</param>
    /// <param name="chWaterOTemperature">Chilled water outlet temperature [°C].</param>
    /// <param name="chWaterFlowRate">Chilled water mass flow rate [kg/s].</param>
    /// <param name="cdWaterITemperature">Cooling water inlet temperature [°C].</param>
    /// <param name="cdWaterOTemperature">Cooling water outlet temperature [°C].</param>
    /// <param name="cdWaterFlowRate">Cooling water mass flow rate [kg/s].</param>
    /// <param name="htWaterITemperature">Hot water inlet temperature [°C].</param>
    /// <param name="hotWaterFlowRate">Hot water mass flow rate [kg/s].</param>
    /// <param name="dsbTemperatureApploach">Desorption temperature approach [°C].</param>
    /// <param name="evaporatorKA">Evaporator overall heat transfer conductance [kW/K].</param>
    /// <param name="condensorKA">Condenser (absorber) overall heat transfer conductance [kW/K].</param>
    /// <param name="desorborKA">Desorber overall heat transfer conductance [kW/K].</param>
    /// <param name="hexKA">Solution heat exchanger overall heat transfer conductance [kW/K].</param>
    /// <param name="solFlowRate">Dilute solution circulation rate [kg/s].</param>
    /// <param name="desorbHeat">High-temperature desorber heat input [kW].</param>
    public static void GetHeatTransferCoefficients
      (double chWaterITemperature, double chWaterOTemperature, double chWaterFlowRate, double cdWaterITemperature,
      double cdWaterOTemperature, double cdWaterFlowRate, double htWaterITemperature, double hotWaterFlowRate, 
      double dsbTemperatureApploach, out double evaporatorKA, out double condensorKA, out double desorborKA,
      out double hexKA, out double solFlowRate, out double desorbHeat)
    {
      //凝縮器（吸収器）と蒸発器の伝熱係数KA[kW/K]
      evaporatorKA = GetRefrigerantHexKA(chWaterITemperature, chWaterOTemperature, chWaterFlowRate, NOM_EVP_TEMP);
      condensorKA = GetRefrigerantHexKA(cdWaterITemperature, cdWaterOTemperature, cdWaterFlowRate, NOM_CND_TEMP);

      //再生器投入熱量[kW]
      double qE = chWaterFlowRate * 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat * (chWaterITemperature - chWaterOTemperature);
      double qCDAB = cdWaterFlowRate * 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat * (cdWaterOTemperature - cdWaterITemperature);
      desorbHeat = qCDAB - qE;
      double hotWaterOutletTemperature =
        htWaterITemperature - desorbHeat / (0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat * hotWaterFlowRate);

      //再生器と吸収器出口の水溶液状態
      LithiumBromide lbDo = LithiumBromide.MakeFromLiquidTemperatureAndVaporTemperature
        (PhysicsConstants.ToKelvin(htWaterITemperature - dsbTemperatureApploach), PhysicsConstants.ToKelvin(NOM_CND_TEMP));
      LithiumBromide lbAo = LithiumBromide.MakeFromLiquidTemperatureAndVaporTemperature
        (PhysicsConstants.ToKelvin(NOM_CND_TEMP), PhysicsConstants.ToKelvin(NOM_EVP_TEMP));

      //溶液循環比[-]
      double aW = lbDo.MassFraction / (lbDo.MassFraction - lbAo.MassFraction);

      //冷媒の比エンタルピー[kJ/kg]
      double hRVDo = Water.GetSaturatedVaporEnthalpy(NOM_CND_TEMP);
      double hRLEi = Water.GetSaturatedLiquidEnthalpy(NOM_CND_TEMP);
      double hRVEo = Water.GetSaturatedVaporEnthalpy(NOM_EVP_TEMP);

      //冷媒および水溶液の循環量[kg/s]
      double mR = qE / (hRVEo - hRLEi);
      solFlowRate = mR * aW;
      double mAi = solFlowRate - mR;

      //溶液熱交換器の伝熱係数KA[kW/K]
      double hSDi = (mR * hRVDo + lbDo.Enthalpy * mAi - desorbHeat) / solFlowRate;
      double qX = (hSDi - lbAo.Enthalpy) * solFlowRate;
      hexKA = HeatExchange.GetHeatTransferCoefficient(lbDo.LiquidTemperature, lbAo.LiquidTemperature, 
        lbDo.SpecificHeat * mAi, lbAo.SpecificHeat * solFlowRate, qX, HeatExchange.FlowType.CounterFlow);

      //再生器の伝熱係数KA[kW/K]
      LithiumBromide lbDi2 = LithiumBromide.MakeFromEnthalpyAndVaporTemperature(hSDi, PhysicsConstants.ToKelvin(NOM_CND_TEMP));
      double cp = GetSolutionAverageSpecificHeat(lbDi2, lbDo);
      double mcHW = hotWaterFlowRate * 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat;
      double mcSL = solFlowRate * cp;
      double mcMin = Math.Min(mcHW, mcSL);
      double mcMax = Math.Max(mcHW, mcSL);
      double effectiveness = desorbHeat / (mcMin * (htWaterITemperature - (PhysicsConstants.ToCelsius(lbDi2.LiquidTemperature))));
      desorborKA = HeatExchange.GetNTU(effectiveness, mcMin / mcMax, HeatExchange.FlowType.CounterFlow) * mcMin;
    }

    /// <summary>Computes the outlet temperatures in free-running mode.</summary>
    /// <param name="chWaterITemperature">Chilled water inlet temperature [°C].</param>
    /// <param name="chWaterFlowRate">Chilled water mass flow rate [kg/s].</param>
    /// <param name="cdWaterITemperature">Cooling water inlet temperature [°C].</param>
    /// <param name="cdWaterFlowRate">Cooling water mass flow rate [kg/s].</param>
    /// <param name="htWaterITemperature">Hot water inlet temperature [°C].</param>
    /// <param name="htWaterFlowRate">Hot water mass flow rate [kg/s].</param>
    /// <param name="evaporatorKA">Evaporator overall heat transfer conductance [kW/K].</param>
    /// <param name="condensorKA">Condenser overall heat transfer conductance [kW/K].</param>
    /// <param name="desorborKA">Desorber overall heat transfer conductance [kW/K].</param>
    /// <param name="hexKA">Solution heat exchanger overall heat transfer conductance [kW/K].</param>
    /// <param name="solFlowRate">Solution mass flow rate [kg/s].</param>
    /// <param name="chWaterOTemperature">Output: chilled water outlet temperature [°C].</param>
    /// <param name="cdWaterOTemperature">Output: cooling water outlet temperature [°C].</param>
    /// <param name="htWaterOTemperature">Output: hot water outlet temperature [°C].</param>
    public static void GetOutletTemperatures
      (double chWaterITemperature, double chWaterFlowRate, double cdWaterITemperature, 
      double cdWaterFlowRate, double htWaterITemperature, double htWaterFlowRate, double evaporatorKA, 
      double condensorKA, double desorborKA, double hexKA, double solFlowRate,
      out double chWaterOTemperature, out double cdWaterOTemperature, out double htWaterOTemperature)
    {
      double tcdo = 0;
      double tho = 0;
      Minimization.MinimizeFunction mFnc = delegate (double tcho)
      {
        return GetError(chWaterITemperature, chWaterFlowRate, cdWaterITemperature, cdWaterFlowRate,
          htWaterITemperature, htWaterFlowRate, evaporatorKA, condensorKA, desorborKA, hexKA, solFlowRate, tcho, 
          out tcdo, out tho);
      };

      chWaterOTemperature = NOM_EVP_TEMP + 0.001;
      Minimization.GoldenSection(ref chWaterOTemperature, chWaterITemperature - 0.001, mFnc);
      cdWaterOTemperature = tcdo;
      htWaterOTemperature = tho;
    }

    /// <summary>Computes the outlet temperatures for the specified chilled water outlet temperature.</summary>
    /// <param name="chWaterITemperature">Chilled water inlet temperature [°C].</param>
    /// <param name="chWaterFlowRate">Chilled water mass flow rate [kg/s].</param>
    /// <param name="cdWaterITemperature">Cooling water inlet temperature [°C].</param>
    /// <param name="cdWaterFlowRate">Cooling water mass flow rate [kg/s].</param>
    /// <param name="htWaterITemperature">Hot water inlet temperature [°C].</param>
    /// <param name="htWaterFlowRate">Hot water mass flow rate [kg/s].</param>
    /// <param name="evaporatorKA">Evaporator overall heat transfer conductance [kW/K].</param>
    /// <param name="condensorKA">Condenser overall heat transfer conductance [kW/K].</param>
    /// <param name="desorborKA">Desorber overall heat transfer conductance [kW/K].</param>
    /// <param name="hexKA">Solution heat exchanger overall heat transfer conductance [kW/K].</param>
    /// <param name="solFlowRate">Solution mass flow rate [kg/s].</param>
    /// <param name="chWaterOTemperatureSP">Chilled water outlet temperature setpoint [°C].</param>
    /// <param name="cdWaterOTemperature">Output: cooling water outlet temperature [°C].</param>
    /// <param name="htWaterOTemperature">Output: hot water outlet temperature [°C].</param>
    public static void GetOutletTemperatures(double chWaterITemperature, double chWaterFlowRate,
      double cdWaterITemperature, double cdWaterFlowRate, double htWaterITemperature, double htWaterFlowRate,
      double evaporatorKA, double condensorKA, double desorborKA, double hexKA, double solFlowRate,
      double chWaterOTemperatureSP, out double cdWaterOTemperature, out double htWaterOTemperature)
    {
      double tcdo = 0;
      double tho = 0;
      Minimization.MinimizeFunction mFnc = delegate (double hwr)
      {
        return GetError(chWaterITemperature, chWaterFlowRate, cdWaterITemperature, cdWaterFlowRate,
          htWaterITemperature, htWaterFlowRate * hwr, evaporatorKA, condensorKA, desorborKA, hexKA,
          solFlowRate, chWaterOTemperatureSP, out tcdo, out tho);
      };

      double hwRatio = 0.01;
      Minimization.GoldenSection(ref hwRatio, 1.0, mFnc);
      cdWaterOTemperature = tcdo;
      htWaterOTemperature = tho * hwRatio + htWaterITemperature * (1 - hwRatio);
    }

    /// <summary>Error function for the single-effect absorption refrigeration cycle.</summary>
    /// <param name="chWaterITemperature">Chilled water inlet temperature [°C].</param>
    /// <param name="chWaterFlowRate">Chilled water mass flow rate [kg/s].</param>
    /// <param name="cdWaterITemperature">Cooling water inlet temperature [°C].</param>
    /// <param name="cdWaterFlowRate">Cooling water mass flow rate [kg/s].</param>
    /// <param name="htWaterITemperature">Hot water inlet temperature [°C].</param>
    /// <param name="htWaterFlowRate">Hot water mass flow rate [kg/s].</param>
    /// <param name="evaporatorKA">Evaporator overall heat transfer conductance [kW/K].</param>
    /// <param name="condensorKA">Condenser overall heat transfer conductance [kW/K].</param>
    /// <param name="desorborKA">Desorber overall heat transfer conductance [kW/K].</param>
    /// <param name="hexKA">Solution heat exchanger overall heat transfer conductance [kW/K].</param>
    /// <param name="solFlowRate">Solution mass flow rate [kg/s].</param>
    /// <param name="chWaterOTemperature">Chilled water outlet temperature [°C].</param>
    /// <param name="cdWaterOTemperature">Output: cooling water outlet temperature [°C].</param>
    /// <param name="htWaterOTemperature">Output: hot water outlet temperature [°C].</param>
    /// <returns>Single-effect absorption cycle error value.</returns>
    private static double GetError(double chWaterITemperature, double chWaterFlowRate,
      double cdWaterITemperature, double cdWaterFlowRate, double htWaterITemperature,
      double htWaterFlowRate, double evaporatorKA, double condensorKA, double desorborKA, double hexKA,
      double solFlowRate, double chWaterOTemperature,
      out double cdWaterOTemperature, out double htWaterOTemperature)
    {
      //蒸発温度の計算
      double qE = chWaterFlowRate * 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat * (chWaterITemperature - chWaterOTemperature);
      double evaporatingTemperature = GetRefrigerantTemperature
        (chWaterITemperature, chWaterOTemperature, chWaterFlowRate, evaporatorKA);
      double hRVEo = Water.GetSaturatedVaporEnthalpy(evaporatingTemperature);

      LithiumBromide lbAo = null!;
      LithiumBromide lbDo = null!;
      double qX = 0;
      double condensingTemperature = 0;
      double tcdo = 0;
      Roots.ErrorFunction eFnc = delegate (double dsvH)
      {
        //凝縮温度と比エンタルピー
        double qCDAB = qE + dsvH;
        tcdo = cdWaterITemperature + qCDAB / (cdWaterFlowRate * 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat);
        condensingTemperature = GetRefrigerantTemperature
        (cdWaterITemperature, tcdo, cdWaterFlowRate, condensorKA);
        double hRVDo = Water.GetSaturatedVaporEnthalpy(condensingTemperature);
        double hRLCDo = Water.GetSaturatedLiquidEnthalpy(condensingTemperature);

        //冷媒流量と溶液循環比[-]
        double mR = qE / (hRVEo - hRLCDo);
        double aW = solFlowRate / mR;
        double mSAi = solFlowRate - mR;

        //吸収器・再生器の出口水溶液状態
        lbAo = LithiumBromide.MakeFromLiquidTemperatureAndVaporTemperature
        (condensingTemperature + 273.15, evaporatingTemperature + 273.15);
        lbDo = LithiumBromide.MakeFromVaporTemperatureAndMassFraction
        (condensingTemperature + 273.15, aW / (aW - 1) * lbAo.MassFraction);

        //冷却水熱量にもとづく溶液熱交換器の処理熱量
        double qAB = qCDAB - mR * (hRVDo - hRLCDo);
        double hSAi = lbAo.Enthalpy + (qAB - mR * (hRVEo - lbAo.Enthalpy)) / mSAi;
        qX = (lbDo.Enthalpy - hSAi) * mSAi;

        //溶液熱交換器伝熱係数にもとづく処理熱量
        double qX2 = HeatExchange.GetHeatTransfer
        (lbDo.LiquidTemperature, lbAo.LiquidTemperature, lbDo.SpecificHeat * mSAi,
        lbAo.SpecificHeat * solFlowRate, hexKA, HeatExchange.FlowType.CounterFlow);

        return qX - qX2;
      };

      //投入熱量を収束計算
      double desorbHeat = qE / 0.75;
      desorbHeat = Roots.Newton(eFnc, desorbHeat, 0.001, 0.0001, desorbHeat * 0.001, 20);

      //冷却水と温水の出口温度を計算
      cdWaterOTemperature = tcdo;
      htWaterOTemperature = htWaterITemperature - desorbHeat / (htWaterFlowRate * 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat);

      //再生器が必要とする再生温度を計算
      LithiumBromide lbDi = LithiumBromide.MakeFromEnthalpyAndMassFraction
        (lbAo.Enthalpy + qX / solFlowRate, lbAo.MassFraction);
      LithiumBromide lbDi2 = LithiumBromide.MakeFromEnthalpyAndVaporTemperature
        (lbDi.Enthalpy, condensingTemperature + 273.15);
      double desorbTemp = GetDesorbTemperature
        (desorborKA, desorbHeat, htWaterFlowRate, lbDi2, lbDo, solFlowRate);

      //温水入口温度が必要温度未満の場合
      if (0 < desorbTemp - (htWaterITemperature + 273.15))
        return desorbTemp - (htWaterITemperature + 273.15);
      //必要温度以上の場合には余剰温水流量を計算
      else
        return htWaterFlowRate - GetHotWaterFlowRate
          (desorborKA, desorbHeat, htWaterITemperature + 273.15, lbDi2, lbDo, solFlowRate);
    }

    #endregion

    #region 二重効用吸収冷凍サイクル関連のメソッド

    /// <summary>Computes the overall heat transfer conductances [kW/K] from rated operating conditions.</summary>
    /// <param name="chWaterITemperature">Chilled water inlet temperature [°C].</param>
    /// <param name="chWaterOTemperature">Chilled water outlet temperature [°C].</param>
    /// <param name="cdWaterITemperature">Cooling water inlet temperature [°C].</param>
    /// <param name="cdWaterOTemperature">Cooling water outlet temperature [°C].</param>
    /// <param name="chWaterFlowRate">Chilled water mass flow rate [kg/s].</param>
    /// <param name="cdWaterFlowRate">Cooling water mass flow rate [kg/s].</param>
    /// <param name="evaporatorKA">Evaporator overall heat transfer conductance [kW/K].</param>
    /// <param name="condensorKA">Condenser (absorber) overall heat transfer conductance [kW/K].</param>
    /// <param name="lowDesorborKA">Low-temperature desorber overall heat transfer conductance [kW/K].</param>
    /// <param name="lHexKA">Low-temperature solution heat exchanger conductance [kW/K].</param>
    /// <param name="solFlowRate">Dilute solution circulation rate [kg/s].</param>
    /// <param name="desorbHeat">High-temperature desorber heat input [kW].</param>
    public static void GetHeatTransferCoefficients
      (double chWaterITemperature, double chWaterOTemperature, double chWaterFlowRate,
      double cdWaterITemperature, double cdWaterOTemperature, double cdWaterFlowRate,
      out double evaporatorKA, out double condensorKA, out double lowDesorborKA, 
      out double lHexKA, out double solFlowRate, out double desorbHeat)
    {
      //凝縮器（吸収器）と蒸発器の伝熱係数KA[kW/K]
      evaporatorKA = GetRefrigerantHexKA(chWaterITemperature, chWaterOTemperature, chWaterFlowRate, NOM_EVP_TEMP);
      condensorKA = GetRefrigerantHexKA(cdWaterITemperature, cdWaterOTemperature, cdWaterFlowRate, NOM_CND_TEMP);

      //再生器投入熱量[kW]の計算
      double qE = chWaterFlowRate * 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat * (chWaterITemperature - chWaterOTemperature);
      double qCDAB = cdWaterFlowRate * 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat * (cdWaterOTemperature - cdWaterITemperature);
      double qD = desorbHeat = (qCDAB - qE) / (1 - HEATLOSS_RATE);

      //水溶液状態の計算
      LithiumBromide lbHDo = LithiumBromide.MakeFromLiquidTemperatureAndVaporTemperature
        (NOM_DSB_LIQ_TEMP + 273.15, NOM_DSB_VAP_TEMP + 273.15);
      LithiumBromide lbAo = LithiumBromide.MakeFromLiquidTemperatureAndVaporTemperature
        (NOM_CND_TEMP + 273.15, NOM_EVP_TEMP + 273.15);
      LithiumBromide lbLDo = LithiumBromide.MakeFromVaporTemperatureAndMassFraction
        (NOM_CND_TEMP + 273.15, lbHDo.MassFraction);

      //溶液循環比[-]
      double aW = lbHDo.MassFraction / (lbHDo.MassFraction - lbAo.MassFraction);

      //冷媒比エンタルピー[kJ/kg]の計算
      double hRVHDo = Water.GetSaturatedVaporEnthalpy(NOM_DSB_VAP_TEMP);
      double hRLHDo = Water.GetSaturatedLiquidEnthalpy(NOM_DSB_VAP_TEMP);
      double hRVLDo = Water.GetSaturatedVaporEnthalpy(NOM_CND_TEMP);
      double hRLEi = Water.GetSaturatedLiquidEnthalpy(NOM_CND_TEMP);
      double hRVEo = Water.GetSaturatedVaporEnthalpy(NOM_EVP_TEMP);

      //冷媒循環量[kg/s]
      double mR = qE / (hRVEo - hRLEi);
      solFlowRate = mR * aW;

      //溶液状態保持変数
      LithiumBromide lbLDi = null!;  //低温再生器入口水溶液
      LithiumBromide lbAi = null!;  //低温熱交換器出口水溶液
      double mRH = 0;  //高温側冷媒流量[kg/s]
      double mRL = 0;  //低温側冷媒流量[kg/s]

      //誤差関数の定義
      Roots.ErrorFunction eFnc = delegate (double rhgRate)
      {
        //冷媒流量[kg/s]の計算
        mRH = mR * rhgRate;
        mRL = mR - mRH;
        double mSAo = mR * aW;
        double mSAi = mSAo - mR;

        //凝縮器・吸収器の処理熱量[kW]
        double qCD = (hRLHDo - hRLEi) * mRH + (hRVLDo - hRLEi) * mRL - qD * HEATLOSS_RATE;
        double qAB = qCDAB - qCD;

        //低温溶液熱交換器出口水溶液
        double hSAi = lbAo.Enthalpy + (qAB - mR * (hRVEo - lbAo.Enthalpy)) / mSAi;
        lbAi = LithiumBromide.MakeFromEnthalpyAndMassFraction(hSAi, lbHDo.MassFraction);

        //低温再生器入口水溶液
        double hLDi = lbAo.Enthalpy + (lbLDo.Enthalpy - lbAi.Enthalpy) * mSAi / mSAo;
        lbLDi = LithiumBromide.MakeFromEnthalpyAndMassFraction(hLDi, lbAo.MassFraction);

        //低温再生器投入熱量
        double qLD1 = (hRVHDo - hRLHDo) * mRH;
        double qLD2 = hRVLDo * mRL + lbLDo.Enthalpy * (mRL * (aW - 1)) - hLDi * (mRL * aW);

        return qLD1 - qLD2;
      };

      //溶液配分比[-]を収束計算
      double rRatio = Roots.Newton(eFnc, 0.5, 0.001, 0.0001, 0.0001, 20);

      //低温再生器の伝熱係数KA[kW/K]の計算
      LithiumBromide lbLDi2 = LithiumBromide.MakeFromEnthalpyAndVaporTemperature
        (lbLDi.Enthalpy, NOM_CND_TEMP + 273.15);
      double cp = GetSolutionAverageSpecificHeat(lbLDi2, lbLDo);
      double effectiveness = (lbLDo.LiquidTemperature - lbLDi2.LiquidTemperature) 
        / (NOM_DSB_VAP_TEMP + 273.15 - lbLDi2.LiquidTemperature);
      lowDesorborKA = -Math.Log(1 - effectiveness) * (cp * (mRL * aW));

      //溶液熱交換器の伝熱係数KA[kW/K]の計算
      double qLX = (lbLDo.Enthalpy - lbAi.Enthalpy) * mR * (aW - 1);
      double mcH = lbLDo.SpecificHeat * mR * (aW - 1);
      double mcC = lbAo.SpecificHeat * mR * aW;
      lHexKA = HeatExchange.GetHeatTransferCoefficient
        (lbLDo.LiquidTemperature, lbAo.LiquidTemperature, mcC, mcH, qLX, HeatExchange.FlowType.CounterFlow);
    }

    /// <summary>Computes the chilled water outlet temperature [°C].</summary>
    /// <param name="chWaterITemperature">Chilled water inlet temperature [°C].</param>
    /// <param name="chWaterFlowRate">Chilled water mass flow rate [kg/s].</param>
    /// <param name="cdWaterITemperature">Cooling water inlet temperature [°C].</param>
    /// <param name="cdWaterFlowRate">Cooling water mass flow rate [kg/s].</param>
    /// <param name="evaporatorKA">Evaporator overall heat transfer conductance [kW/K].</param>
    /// <param name="condensorKA">Condenser overall heat transfer conductance [kW/K].</param>
    /// <param name="lowDesorborKA">Low-temperature desorber overall heat transfer conductance [kW/K].</param>
    /// <param name="lHexKA">Low-temperature solution heat exchanger conductance [kW/K].</param>
    /// <param name="solFlowRate">Solution mass flow rate [kg/s].</param>
    /// <param name="desorbHeat">High-temperature desorber heat input [kW].</param>
    /// <param name="cdWaterOTemperature">Output: cooling water outlet temperature [°C].</param>
    /// <param name="dsbTemperature">Output: high-temperature desorber temperature [°C].</param>
    /// <param name="evpTemperature">Output: evaporating temperature [°C].</param>
    /// <param name="cndTemperature">Output: condensing temperature [°C].</param>
    /// <param name="thinMFraction">Output: dilute solution mass fraction [-].</param>
    /// <param name="thickMFraction">Output: concentrated solution mass fraction [-].</param>
    /// <returns>Chilled water outlet temperature [°C].</returns>
    public static double GetChilledWaterOutletTemperature
      (double chWaterITemperature, double chWaterFlowRate, double cdWaterITemperature, double cdWaterFlowRate,
      double evaporatorKA, double condensorKA, double lowDesorborKA, double lHexKA, double solFlowRate,
      double desorbHeat, out double cdWaterOTemperature, out double dsbTemperature, out double evpTemperature, 
      out double cndTemperature, out double thinMFraction, out double thickMFraction)
    {
      double dtCH, dtCD;
      AdjustRange(ref chWaterITemperature, ref cdWaterITemperature, out dtCH, out dtCD);

      double tdsv, tcnd, tevp, tcdo, wth, wtk;
      tdsv = tcnd = tevp = tcdo = wth = wtk = 0;
      Roots.ErrorFunction eFnc = delegate (double tcho)
      {

        return GetError(chWaterITemperature, chWaterFlowRate, cdWaterITemperature, cdWaterFlowRate, 
          evaporatorKA, condensorKA, lowDesorborKA, lHexKA, desorbHeat, solFlowRate, tcho,
          out tcdo, out tdsv, out tevp, out tcnd, out wth, out wtk);
      };

      //冷水出口温度を収束計算
      double chilledWaterOutletTemperature = Roots.Newton(eFnc, NOM_EVP_TEMP + 0.01, 0.001, 0.0001, 0.01, 20);
      dsbTemperature = tdsv;
      thinMFraction = wth;
      thickMFraction = wtk;
      cdWaterOTemperature = tcdo + dtCD;
      evpTemperature = tevp + dtCH;
      cndTemperature = tcnd + dtCD;

      return chilledWaterOutletTemperature + dtCH;
    }

    /// <summary>Computes the heat input to the high-temperature desorber [kW].</summary>
    /// <param name="chWaterITemperature">Chilled water inlet temperature [°C].</param>
    /// <param name="chWaterFlowRate">Chilled water mass flow rate [kg/s].</param>
    /// <param name="cdWaterITemperature">Cooling water inlet temperature [°C].</param>
    /// <param name="cdWaterFlowRate">Cooling water mass flow rate [kg/s].</param>
    /// <param name="evaporatorKA">Evaporator overall heat transfer conductance [kW/K].</param>
    /// <param name="condensorKA">Condenser overall heat transfer conductance [kW/K].</param>
    /// <param name="lowDesorborKA">Low-temperature desorber overall heat transfer conductance [kW/K].</param>
    /// <param name="lHexKA">Low-temperature solution heat exchanger conductance [kW/K].</param>
    /// <param name="solFlowRate">Solution mass flow rate [kg/s].</param>
    /// <param name="chWaterOTemperature">Chilled water outlet temperature setpoint [°C].</param>
    /// <param name="cdWaterOTemperature">Output: cooling water outlet temperature [°C].</param>
    /// <param name="dsbTemperature">Output: high-temperature desorber temperature [°C].</param>
    /// <param name="evpTemperature">Output: evaporating temperature [°C].</param>
    /// <param name="cndTemperature">Output: condensing temperature [°C].</param>
    /// <param name="thinMFraction">Output: dilute solution mass fraction [-].</param>
    /// <param name="thickMFraction">Output: concentrated solution mass fraction [-].</param>
    /// <returns>High-temperature desorber heat input [kW].</returns>
    public static double GetDesorbHeat
      (double chWaterITemperature, double chWaterFlowRate, double cdWaterITemperature, double cdWaterFlowRate,
      double evaporatorKA, double condensorKA, double lowDesorborKA, double lHexKA, double solFlowRate,
      double chWaterOTemperature, out double cdWaterOTemperature, out double dsbTemperature,
      out double evpTemperature, out double cndTemperature, out double thinMFraction, out double thickMFraction)
    {
      double dtCH, dtCD;
      AdjustRange(ref chWaterITemperature, ref cdWaterITemperature, out dtCH, out dtCD);
      chWaterOTemperature += dtCH;

      double tdsv, tcnd, tevp, tcdo, wth, wtk;
      tdsv = tcnd = tevp = tcdo = wth = wtk = 0;
      Roots.ErrorFunction eFnc = delegate (double dsvH)
      {
        return GetError
        (chWaterITemperature, chWaterFlowRate, cdWaterITemperature, cdWaterFlowRate, evaporatorKA, condensorKA,
        lowDesorborKA, lHexKA, dsvH, solFlowRate, chWaterOTemperature,
        out tcdo, out tdsv, out tevp, out tcnd, out wth, out wtk);
      };

      //再生器投入熱量を収束計算
      double qE = chWaterFlowRate * 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat * (chWaterITemperature - chWaterOTemperature);
      double desorbHeat = qE / 1.4;
      desorbHeat = Roots.Newton(eFnc, desorbHeat, 0.001, 0.0001, desorbHeat * 0.001, 20);
      dsbTemperature = tdsv;
      thinMFraction = wth;
      thickMFraction = wtk;
      cdWaterOTemperature = tcdo + dtCD;
      evpTemperature = tevp + dtCH;
      cndTemperature = tcnd + dtCD;

      return desorbHeat;
    }

    /// <summary>Adjusts chilled water and cooling water temperatures to satisfy energy balance constraints.</summary>
    /// <param name="tCHi">Chilled water inlet temperature [°C].</param>
    /// <param name="tCDi">Cooling water inlet temperature [°C].</param>
    /// <param name="dtCH">Output: chilled water temperature adjustment.</param>
    /// <param name="dtCD">Output: cooling water temperature adjustment.</param>
    private static void AdjustRange(ref double tCHi, ref double tCDi, out double dtCH, out double dtCD)
    {
      dtCH = dtCD = 0;
      if (tCHi < 3)
      {
        dtCH = tCHi - 5;
        tCHi = 5;
      }
      else if (18 < tCHi)
      {
        dtCH = tCHi - 18;
        tCHi = 18;
      }
      if (tCDi < 20)
      {
        dtCD = tCDi - 20;
        tCDi = 20;
      }
      else if (37 < tCDi)
      {
        dtCD = tCDi - 37;
        tCDi = 37;
      }
    }

    /// <summary>Error function for the double-effect absorption refrigeration cycle.</summary>
    /// <param name="chWaterITemperature">Chilled water inlet temperature [°C].</param>
    /// <param name="chWaterFlowRate">Chilled water mass flow rate [kg/s].</param>
    /// <param name="cdWaterITemperature">Cooling water inlet temperature [°C].</param>
    /// <param name="cdWaterFlowRate">Cooling water mass flow rate [kg/s].</param>
    /// <param name="evaporatorKA">Evaporator overall heat transfer conductance [kW/K].</param>
    /// <param name="condensorKA">Condenser overall heat transfer conductance [kW/K].</param>
    /// <param name="lowDesorborKA">Low-temperature desorber overall heat transfer conductance [kW/K].</param>
    /// <param name="lHexKA">Low-temperature solution heat exchanger conductance [kW/K].</param>
    /// <param name="desorbHeat">High-temperature desorber heat input [kW].</param>
    /// <param name="solFlowRate">Solution mass flow rate [kg/s].</param>
    /// <param name="chWaterOTemperature">Chilled water outlet temperature [°C].</param>
    /// <param name="cdWaterOTemperature">Output: cooling water outlet temperature [°C].</param>
    /// <param name="dsbTemperature">Output: high-temperature desorber temperature [°C].</param>
    /// <param name="evpTemperature">Output: evaporating temperature [°C].</param>
    /// <param name="cndTemperature">Output: condensing temperature [°C].</param>
    /// <param name="thinMFraction">Output: dilute solution mass fraction [-].</param>
    /// <param name="thickMFraction">Output: concentrated solution mass fraction [-].</param>
    /// <returns>Double-effect absorption cycle error value.</returns>
    private static double GetError
      (double chWaterITemperature, double chWaterFlowRate, double cdWaterITemperature, double cdWaterFlowRate, 
      double evaporatorKA, double condensorKA, double lowDesorborKA, double lHexKA, double desorbHeat,
      double solFlowRate, double chWaterOTemperature, out double cdWaterOTemperature, out double dsbTemperature,
      out double evpTemperature, out double cndTemperature, out double thinMFraction, out double thickMFraction)
    {
      //冷却水出口温度
      double qE = chWaterFlowRate * 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat * (chWaterITemperature - chWaterOTemperature);
      double qCDAB = qE + desorbHeat / (1 + HEATLOSS_RATE);
      cdWaterOTemperature = cdWaterITemperature + qCDAB / (cdWaterFlowRate * 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat);

      //蒸発温度と凝縮温度
      evpTemperature = GetRefrigerantTemperature
        (chWaterITemperature, chWaterOTemperature, chWaterFlowRate, evaporatorKA);
      cndTemperature = GetRefrigerantTemperature
        (cdWaterITemperature, cdWaterOTemperature, cdWaterFlowRate, condensorKA);

      //凝縮・蒸発温度における冷媒エンタルピー
      double hRVLDo = Water.GetSaturatedVaporEnthalpy(cndTemperature);
      double hRLLDo = Water.GetSaturatedLiquidEnthalpy(cndTemperature);
      double hRVEo = Water.GetSaturatedVaporEnthalpy(evpTemperature);

      //冷媒流量と溶液循環比[-]
      double mR = qE / (hRVEo - hRLLDo);
      double aW = solFlowRate / mR;

      LithiumBromide lbAo = LithiumBromide.MakeFromLiquidTemperatureAndVaporTemperature
        (cndTemperature + 273.15, evpTemperature + 273.15);
      LithiumBromide lbLDo = LithiumBromide.MakeFromVaporTemperatureAndMassFraction
        (cndTemperature + 273.15, aW / (aW - 1) * lbAo.MassFraction);

      double mSAo = solFlowRate;
      double mSAi = solFlowRate - mR;
      double tDesorb = 0;
      double tcndK = cndTemperature + 273.15;
      Roots.ErrorFunction eFnc = delegate (double rRatio)
      {
        //冷媒・水溶液流量
        double mRH = mR * rRatio;
        double mRL = mR - mRH;
        double mSLDi = mSAo * (1 - rRatio);
        double mSLDo = mSLDi - mRL;

        //低温再生器の再生温度
        double qLX = HeatExchange.GetHeatTransfer
        (lbLDo.LiquidTemperature, lbAo.LiquidTemperature, lbLDo.SpecificHeat * mSAi,
        lbAo.SpecificHeat * mSAo, lHexKA, HeatExchange.FlowType.CounterFlow);
        LithiumBromide lbLDi = LithiumBromide.MakeFromEnthalpyAndMassFraction
        (lbAo.Enthalpy + qLX / mSAo, lbAo.MassFraction);
        LithiumBromide lbLDi2 = LithiumBromide.MakeFromEnthalpyAndVaporTemperature(lbLDi.Enthalpy, tcndK);
        tDesorb = GetDesorbTemperature(lowDesorborKA, mSLDi, lbLDi2, lbLDo);

        //低温再生器の処理熱量1[kW]
        double hRVHD = Water.GetSaturatedVaporEnthalpy(tDesorb - 273.15);
        double hRLHD = Water.GetSaturatedLiquidEnthalpy(tDesorb - 273.15);
        double qLD1 = (hRVHD - hRLHD) * mRH;
        //低温再生器の処理熱量2[kW]
        double qLD2 = hRVLDo * mRL + lbLDo.Enthalpy * mSLDo - lbLDi.Enthalpy * mSLDi;

        //冷却水除去熱量
        qCDAB = mRH * (hRLHD - hRLLDo) + mRL * (hRVLDo - hRLLDo)
        + mR * (hRVEo - lbAo.Enthalpy) + mSAi * (lbLDo.Enthalpy - lbAo.Enthalpy) - qLX;

        return qLD1 - qLD2;
      };

      //溶液配分比を収束計算
      Roots.Newton(eFnc, 0.5, 0.001, 0.0001, 0.0001, 20);

      //再生温度と溶液質量分率を出力
      dsbTemperature = LithiumBromide.GetLiquidTemperatureFromVaporTemperatureAndMassFraction
        (tDesorb, lbLDo.MassFraction) - 273.15;
      thinMFraction = lbAo.MassFraction;
      thickMFraction = lbLDo.MassFraction;

      return (qCDAB - qE) - desorbHeat;
    }

    #endregion

    #region 蒸発器・凝縮器関連の処理

    /// <summary>Computes the evaporating/condensing temperature [°C].</summary>
    /// <param name="iwTemperature">Inlet water temperature [°C].</param>
    /// <param name="owTemperature">Outlet water temperature [°C].</param>
    /// <param name="wFlowRate">Water mass flow rate [kg/s].</param>
    /// <param name="heatTransferCoefficient">Evaporator/condenser overall heat transfer conductance [kW/K].</param>
    /// <returns>Evaporating/condensing temperature [°C].</returns>
    private static double GetRefrigerantTemperature
      (double iwTemperature, double owTemperature, double wFlowRate, double heatTransferCoefficient)
    {
      double ntu = heatTransferCoefficient / (wFlowRate * 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat);
      double effectiveness = 1 - Math.Exp(-ntu);
      return iwTemperature - (iwTemperature - owTemperature) / effectiveness;
    }

    /// <summary>Computes the overall heat transfer conductance of the evaporator/condenser [kW/K].</summary>
    /// <param name="iwTemperature">Inlet water temperature [°C].</param>
    /// <param name="owTemperature">Outlet water temperature [°C].</param>
    /// <param name="wFlowRate">Water mass flow rate [kg/s].</param>
    /// <param name="rTemperature">Evaporating/condensing temperature [°C].</param>
    /// <returns>Evaporator/condenser overall heat transfer conductance [kW/K].</returns>
    private static double GetRefrigerantHexKA
      (double iwTemperature, double owTemperature, double wFlowRate, double rTemperature)
    {
      double effectiveness = (iwTemperature - owTemperature) / (iwTemperature - rTemperature);
      return -Math.Log(1 - effectiveness) * wFlowRate * 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat;
    }

    #endregion

    #region 再生器関連の処理

    /// <summary>Computes the desorption temperature [K].</summary>
    /// <param name="desorborKA">Desorber overall heat transfer conductance [kW/K].</param>
    /// <param name="slFlowRate">Solution mass flow rate [kg/s].</param>
    /// <param name="iSolution">Inlet solution state.</param>
    /// <param name="oSolution">Outlet solution state.</param>
    /// <returns>Desorption temperature [K].</returns>
    private static double GetDesorbTemperature
      (double desorborKA, double slFlowRate, LithiumBromide iSolution, LithiumBromide oSolution)
    {
      double cp = GetSolutionAverageSpecificHeat(iSolution, oSolution);
      double effectiveness = 1 - Math.Exp(-desorborKA / (cp * slFlowRate));
      return (oSolution.LiquidTemperature - iSolution.LiquidTemperature) / effectiveness + iSolution.LiquidTemperature;
    }

    /// <summary>Computes the desorption temperature [K].</summary>
    /// <param name="desorborKA">Desorber overall heat transfer conductance [kW/K].</param>
    /// <param name="desorbHeat">Desorber heat input [kW].</param>
    /// <param name="hwFlowRate">Hot water flow rate [kg/s].</param>
    /// <param name="iSolution">Inlet solution state.</param>
    /// <param name="oSolution">Outlet solution state.</param>
    /// <param name="slFlowRate">Solution mass flow rate [kg/s].</param>
    /// <returns>Desorption temperature [K].</returns>
    private static double GetDesorbTemperature
      (double desorborKA, double desorbHeat, double hwFlowRate, 
      LithiumBromide iSolution, LithiumBromide oSolution, double slFlowRate)
    {
      double cp = GetSolutionAverageSpecificHeat(iSolution, oSolution);
      double mcHW = hwFlowRate * 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat;
      double mcSL = slFlowRate * cp;
      double mcMin = Math.Min(mcHW, mcSL);
      double mcMax = Math.Max(mcHW, mcSL); 
      double effectiveness = HeatExchange.GetEffectiveness
        (desorborKA / mcMin, mcMin / mcMax, HeatExchange.FlowType.CounterFlow);
      return desorbHeat / (mcMin * effectiveness) + iSolution.LiquidTemperature;
    }

    /// <summary>Computes the hot water flow rate [kg/s] required to meet the specified conditions.</summary>
    /// <param name="desorborKA">Desorber overall heat transfer conductance [kW/K].</param>
    /// <param name="desorbHeat">Desorber heat input [kW].</param>
    /// <param name="hwITemperature">Desorption temperature [K].</param>
    /// <param name="iSolution">Inlet solution state.</param>
    /// <param name="oSolution">Outlet solution state.</param>
    /// <param name="slFlowRate">Solution mass flow rate [kg/s].</param>
    /// <returns>Required hot water flow rate [kg/s].</returns>
    private static double GetHotWaterFlowRate
      (double desorborKA, double desorbHeat, double hwITemperature,
      LithiumBromide iSolution, LithiumBromide oSolution, double slFlowRate)
    {
      Roots.ErrorFunction eFnc = delegate (double hwf)
      {
        return GetDesorbTemperature
        (desorborKA, desorbHeat, hwf, iSolution, oSolution, slFlowRate) - hwITemperature;
      };
      return Roots.Newton(eFnc, 0.0001, 0.001, 0.001, 0.0001, 20);
    }

    /// <summary>Computes the specific heat [kJ/(kg·K)] of the solution mixture relative to solution 1.</summary>
    /// <param name="sol1">Solution 1 state.</param>
    /// <param name="sol2">Solution 2 state (partially evaporated).</param>
    /// <returns>Specific heat of the solution mixture relative to solution 1 [kJ/(kg·K)].</returns>
    private static double GetSolutionAverageSpecificHeat(LithiumBromide sol1, LithiumBromide sol2)
    {
      double hw = Water.GetSaturatedVaporEnthalpy(sol1.VaporTemperature - 273.15);
      double slRate = sol1.MassFraction / sol2.MassFraction;
      double hco = sol2.Enthalpy * slRate + hw * (1 - slRate);
      return (hco - sol1.Enthalpy) / (sol2.LiquidTemperature - sol1.LiquidTemperature);
    }

    #endregion

  }
}
