/* Pump.cs
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

namespace Popolo.Core.HVAC.FluidCircuit
{
  /// <summary>Represents a centrifugal pump.</summary>
  public class CentrifugalPump : FluidMachinery, IReadOnlyCentrifugalPump
  {

    #region インスタンス変数・プロパティ

    /// <summary>Gets the bypass flow rate [m³/s].</summary>
    public double BypassFlowRate { get; private set; }

    /// <summary>Gets the flow control method.</summary>
    public ControlMethod Control { get; private set; }

    /// <summary>Gets or sets the pressure setpoint [kPa].</summary>
    public double PressureSetpoint { get; set; }

    #endregion

    #region 列挙型定義

    /// <summary>Flow control method for a pump system.</summary>
    public enum ControlMethod
    {
      /// <summary>Constant discharge pressure control (bypass).</summary>
      ConstantPressureWithBypass,
      /// <summary>Constant discharge pressure control (inverter).</summary>
      ConstantPressureWithInverter,
      /// <summary>Minimum discharge pressure control.</summary>
      MinimumPressure
    }

    #endregion

    #region コンストラクタ

    /// <summary>Initializes a new instance.</summary>
    /// <param name="nomPressure">Nominal head [kPa].</param>
    /// <param name="nomFlowRate">Nominal water flow rate [m³/s].</param>
    /// <param name="designPressure">Design head [kPa].</param>
    /// <param name="designFlowRate">Design water flow rate [m³/s].</param>
    /// <param name="control">Flow control method.</param>
    /// <param name="actualHead">Actual head [kPa].</param>
    public CentrifugalPump(double nomPressure, double nomFlowRate, double designPressure,
      double designFlowRate, ControlMethod control, double actualHead) :
       base(nomPressure, nomFlowRate, designPressure, designFlowRate, actualHead,
         control != ControlMethod.ConstantPressureWithBypass)
    {
      //制御方式を保存
      Control = control;

      //効率・圧力特性係数を計算
      efficiencyCoefficient = new double[3];
      pressureCoefficient = new double[3];
      GetGeneralParameters(nomFlowRate, nomPressure, ref efficiencyCoefficient, ref pressureCoefficient);
      PressureSetpoint = designPressure;
    }

    /// <summary>Initializes a new instance.</summary>
    /// <param name="nomPressure">Nominal head [kPa].</param>
    /// <param name="nomFlowRate">Nominal water flow rate [m³/s].</param>
    /// <param name="designPressure">Design head [kPa].</param>
    /// <param name="designFlowRate">Design water flow rate [m³/s].</param>
    /// <param name="nomElectricity">Nominal power consumption [kW].</param>
    /// <param name="control">Flow control method.</param>
    /// <param name="actualHead">Actual head [kPa].</param>
    public CentrifugalPump(double nomPressure, double nomFlowRate, double designPressure,
      double designFlowRate, ControlMethod control, double nomElectricity, double actualHead) :
       this(nomPressure, nomFlowRate, designPressure, designFlowRate, control, actualHead)
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
      //停止判定
      VolumetricFlowRate = flowRate;
      if (VolumetricFlowRate <= 0)
      {
        ShutOff();
        return;
      }

      //バイパスによる吐出圧一定制御の場合：最大回転数・設計圧力で状態更新
      if (Control == ControlMethod.ConstantPressureWithBypass)
      {
        updateWithRotationRatioAndPressure(1.0, PressureSetpoint);
        BypassFlowRate = VolumetricFlowRate - flowRate;
      }
      //INVによる吐出圧一定制御の場合：必要流量・圧力を満たすように回転数を調整
      else if (Control == ControlMethod.ConstantPressureWithInverter)
      {
        updateWithFlowRateAndPressure(flowRate, PressureSetpoint);
        //最小回転数比[-]未満の場合は最小回転数にしてバイパス流量を計算
        if (RotationRatio < MinimumRotationRatio)
        {
          updateWithRotationRatioAndPressure(MinimumRotationRatio, PressureSetpoint);
          BypassFlowRate = VolumetricFlowRate - flowRate;
        }
      }
      //INVによる最小吐出圧制御の場合：抵抗曲線に合う圧力と流量で計算
      else
      {
        double ps = flowRate * flowRate * resistanceCoefficient + ActualHead;
        updateWithFlowRateAndPressure(flowRate, ps);
        //最小回転数比[-]未満の場合は最小回転数にしてバイパス流量を計算
        if (RotationRatio < MinimumRotationRatio)
        {
          updateWithResistanceAndRotationRatio(MinimumRotationRatio);
          BypassFlowRate = VolumetricFlowRate - flowRate;
        }
      }
    }

    /// <summary>Computes the flow rate from the rotation speed.</summary>
    /// <param name="rotationRatio">Rotation speed ratio [-].</param>
    public void UpdateStateWithRotationRatio(double rotationRatio)
    {
      updateWithResistanceAndRotationRatio
        (Math.Min(1, Math.Max(MinimumRotationRatio, rotationRatio)));
    }

    /// <summary>Shuts off the machine.</summary>
    public override void ShutOff()
    {
      base.ShutOff();
      BypassFlowRate = 0;
    }

    #endregion

    #region staticメソッド

    /// <summary>Computes characteristic coefficients for a general-purpose pump.</summary>
    /// <param name="flowRate">Design flow rate [m³/s].</param>
    /// <param name="pressure">Design head [kPa].</param>
    /// <param name="efficiencyCoef">Output: efficiency characteristic coefficients.</param>
    /// <param name="pressureCoef">Output: pressure characteristic coefficients.</param>
    private static void GetGeneralParameters(double flowRate, double pressure,
      ref double[] efficiencyCoef, ref double[] pressureCoef)
    {
      double vf2 = flowRate * flowRate;

      //効率特性
      double lnm = Math.Log(flowRate);
      double maxEff = -0.01618 * lnm * lnm - 0.05662 * lnm + 0.78179;
      efficiencyCoef[0] = -0.929 * maxEff / vf2;
      efficiencyCoef[1] = 1.795 * maxEff / flowRate;
      efficiencyCoef[2] = 0.109 * maxEff;

      //圧力特性
      pressureCoef[0] = -0.395 * pressure / vf2;
      pressureCoef[1] = 0.096 * pressure / flowRate;
      pressureCoef[2] = 1.294 * pressure;
    }

    #endregion

  }

}
