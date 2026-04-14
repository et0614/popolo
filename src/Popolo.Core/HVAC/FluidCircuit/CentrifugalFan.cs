/* CentrifugalFan.cs
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

namespace Popolo.Core.HVAC.FluidCircuit
{
  /// <summary>Represents a centrifugal fan.</summary>
  public class CentrifugalFan : FluidMachinery
  {

    #region コンストラクタ

    /// <summary>Initializes a new instance.</summary>
    /// <param name="nomPressure">Nominal (peak efficiency) total pressure [kPa].</param>
    /// <param name="nomFlowRate">Nominal (peak efficiency) air flow rate [m³/s].</param>
    /// <param name="designPressure">Design total pressure [kPa].</param>
    /// <param name="designFlowRate">Design air flow rate [m³/s].</param>
    /// <param name="number">Size number [-].</param>
    /// <param name="hasInverter">True if the machine has an inverter.</param>
    public CentrifugalFan(double nomPressure, double nomFlowRate, double designPressure,
      double designFlowRate, double number, bool hasInverter) :
      this(nomPressure, nomFlowRate, designPressure, designFlowRate, number, 0.02, hasInverter)
    { }

    /// <summary>Initializes a new instance.</summary>
    /// <param name="nomPressure">Nominal total pressure [kPa].</param>
    /// <param name="nomFlowRate">Nominal air flow rate [m³/s].</param>
    /// <param name="designPressure">Design total pressure [kPa].</param>
    /// <param name="designFlowRate">Design air flow rate [m³/s].</param>
    /// <param name="number">Size number [-].</param>
    /// <param name="dynamicPressure">Design dynamic pressure [kPa].</param>
    /// <param name="hasInverter">True if the machine has an inverter.</param>
    public CentrifugalFan(double nomPressure, double nomFlowRate, double designPressure,
      double designFlowRate, double number, double dynamicPressure, bool hasInverter) :
      base(nomPressure, nomFlowRate, designPressure, designFlowRate, dynamicPressure, hasInverter)
    {
      //効率・圧力特性係数を計算
      efficiencyCoefficient = new double[4];
      pressureCoefficient = new double[3];
      GetGeneralParameters
        (nomFlowRate, nomPressure, number, ref efficiencyCoefficient, ref pressureCoefficient);
    }

    /// <summary>Initializes a new instance.</summary>
    /// <param name="nomPressure">Nominal total pressure [kPa].</param>
    /// <param name="nomFlowRate">Nominal air flow rate [m³/s].</param>
    /// <param name="designPressure">Design total pressure [kPa].</param>
    /// <param name="designFlowRate">Design air flow rate [m³/s].</param>
    /// <param name="number">Size number [-].</param>
    /// <param name="dynamicPressure">Design dynamic pressure [kPa].</param>
    /// <param name="nomElectricity">Nominal power consumption [kW].</param>
    /// <param name="hasInverter">True if the machine has an inverter.</param>
    public CentrifugalFan(double nomPressure, double nomFlowRate, double designPressure,
      double designFlowRate, double number, double dynamicPressure, double nomElectricity, bool hasInverter) :
      this(nomPressure, nomFlowRate, designPressure, designFlowRate, number, dynamicPressure, hasInverter)
    {
      UpdateState(nomFlowRate);
      CorrectionFactor = nomElectricity / GetElectricConsumption();
    }

    #endregion

    #region インスタンスメソッド

    /// <summary>Updates the state.</summary>
    /// <param name="flowRate">Volumetric flow rate [m³/s].</param>
    public void UpdateState(double flowRate)
    {
      VolumetricFlowRate = flowRate;

      //停止判定
      if (VolumetricFlowRate <= 0)
      {
        ShutOff();
        return;
      }

      //抵抗曲線上の圧力
      double ps = VolumetricFlowRate * VolumetricFlowRate * resistanceCoefficient + ActualHead;

      //インバータ有り
      if (HasInverter)
      {
        updateWithFlowRateAndPressure(VolumetricFlowRate, ps);
        //回転数上限の場合には最大回転数で成り行き計算
        if (1.0 < RotationRatio)
        {
          RotationRatio = 1.0;
          VolumetricFlowRate =
            GetFlowRate(RotationRatio, pressureCoefficient, resistanceCoefficient, ActualHead, 1, DesignFlowRate);
          Pressure = VolumetricFlowRate * VolumetricFlowRate * resistanceCoefficient + ActualHead;
        }
        //回転数下限の場合には最小回転数でダンパ制御
        else if (RotationRatio < MinimumRotationRatio)
          UpdateWithFlowRateAndRotationRatio(VolumetricFlowRate, MinimumRotationRatio);
      }
      //インバータ無し
      else
      {
        //過負荷判定
        UpdateWithFlowRateAndRotationRatio(VolumetricFlowRate, 1.0);
        if (Pressure < ps)
        {
          RotationRatio = 1.0;
          VolumetricFlowRate =
            GetFlowRate(1.0, pressureCoefficient, resistanceCoefficient, ActualHead, 1, DesignFlowRate);
          Pressure = VolumetricFlowRate * VolumetricFlowRate * resistanceCoefficient + ActualHead;
        }
      }
    }

    #endregion

    #region staticメソッド

    /// <summary>Computes characteristic coefficients for a general-purpose fan.</summary>
    /// <param name="flowRate">Air flow rate [m³/s].</param>
    /// <param name="pressure">Total pressure [kPa].</param>
    /// <param name="number">Fan number.</param>
    /// <param name="efficiencyCoef">Output: efficiency characteristic coefficients.</param>
    /// <param name="pressureCoef">Output: pressure characteristic coefficients.</param>
    private static void GetGeneralParameters(double flowRate, double pressure,
      double number, ref double[] efficiencyCoef, ref double[] pressureCoef)
    {
      //最大効率近似係数
      double[][] aj = new double[3][];
      aj[0] = new double[] { 1.397E-01, 1.303E-01, -1.179E-02, 3.219E-04 };
      aj[1] = new double[] { 7.649E-01, -9.105E-02, -5.345E-03, 1.006E-03 };
      aj[2] = new double[] { -7.141E-01, 1.325E-01, -2.639E-03, -5.690E-04 };

      //最大効率[-]の計算
      double[] act = new double[3];
      for (int i = 0; i < aj.Length; i++)
        for (int j = aj[0].Length - 1; 0 <= j; j--)
          act[i] = (number * act[i] + aj[i][j]);
      double maxEff = act[0] + pressure * (act[1] + act[2] * pressure);

      //効率特性
      double m2 = flowRate * flowRate;
      double m3 = m2 * flowRate;
      efficiencyCoef[0] = 0.337 * maxEff / m3;
      efficiencyCoef[1] = -1.700 * maxEff / m2;
      efficiencyCoef[2] = 2.350 * maxEff / flowRate;
      efficiencyCoef[3] = 0;

      //圧力特性//2017.01.05.係数修正 E.Togashi
      pressureCoef[0] = -0.203 * pressure / m2;
      pressureCoef[1] = 0.219 * pressure / flowRate;
      pressureCoef[2] = 0.984 * pressure;
    }

    #endregion

  }

}
