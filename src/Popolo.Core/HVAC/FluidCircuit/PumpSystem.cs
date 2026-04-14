/* PumpSystem.cs
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
  /// <summary>Represents a pump system with staged operation.</summary>
  public class PumpSystem : IReadOnlyPumpSystem
  {

    #region インスタンス変数・プロパティ

    /// <summary>Represents a centrifugal pump.</summary>
    private CentrifugalPump pump;

    /// <summary>Gets the centrifugal pump.</summary>
    public IReadOnlyCentrifugalPump Pump { get { return pump; } }

    /// <summary>Resistance coefficient [kPa/(m³/s)²].</summary>
    private double resistanceCoefficient;

    /// <summary>Gets or sets the total flow rate [m³/s].</summary>
    public double TotalFlowRate { get; set; }

    /// <summary>Gets the bypass flow rate [m³/s].</summary>
    public double BypassFlowRate { get; private set; }

    /// <summary>Gets the number of operating units [units].</summary>
    public int OperatingNumber { get; private set; }

    /// <summary>Gets the number of pumps [units].</summary>
    public int PumpNumber { get; private set; }

    /// <summary>Gets the actual head [kPa].</summary>
    public double ActualHead { get; private set; }

    /// <summary>Gets or sets the pressure setpoint [kPa].</summary>
    public double PressureSetpoint
    {
      get { return pump.PressureSetpoint; }
      set { pump.PressureSetpoint = value; }
    }

    #endregion

    #region コンストラクタ

    /// <summary>Initializes a new instance.</summary>
    /// <param name="pump">Centrifugal pump.</param>
    /// <param name="designPressure">Design pressure [kPa].</param>
    /// <param name="designFlowRate">Design flow rate [m³/s].</param>
    /// <param name="actualHead">Actual head [kPa].</param>
    /// <param name="pumpNumber">Number of pumps.</param>
    public PumpSystem
      (CentrifugalPump pump, double designPressure, double designFlowRate, double actualHead, int pumpNumber)
    {
      this.pump = pump;
      PumpNumber = pumpNumber;
      PressureSetpoint = designPressure;
      ActualHead = actualHead;

      //抵抗係数[kPa/(m3/s)^2]を計算
      resistanceCoefficient = (designPressure - actualHead) / (designFlowRate * designFlowRate);
    }

    #endregion

    #region インスタンスメソッド

    /// <summary>Updates the state.</summary>
    public void UpdateState()
    {
      //運転台数を確定
      OperatingNumber = GetOperatingNumber(TotalFlowRate);
      if (OperatingNumber == 0)
      {
        ShutOff();
        return;
      }
      //過負荷の場合
      if (PumpNumber < OperatingNumber)
      {
        OperatingNumber = PumpNumber;
        double tf;
        pump.updateWithResistanceAndRotationRatio(1.0, resistanceCoefficient, ActualHead, PumpNumber, out tf);
        TotalFlowRate = tf;
        BypassFlowRate = 0;
      }
      else
      {
        double vFlow = TotalFlowRate / OperatingNumber;
        //最小吐出圧制御の場合：
        if (Pump.Control == CentrifugalPump.ControlMethod.MinimumPressure)
        {
          double pressure = TotalFlowRate * TotalFlowRate * resistanceCoefficient + ActualHead;
          pump.updateWithFlowRateAndPressure(vFlow, pressure);
          //最小回転数比[-]未満の場合
          if (Pump.RotationRatio < Pump.MinimumRotationRatio)
          {
            double tf;
            pump.updateWithResistanceAndRotationRatio
              (Pump.MinimumRotationRatio, resistanceCoefficient, ActualHead, OperatingNumber, out tf);
          }
        }
        //吐出圧一定制御・バイパス制御の場合
        else pump.UpdateState(vFlow);

        BypassFlowRate = (Pump.VolumetricFlowRate * OperatingNumber) - TotalFlowRate;
      }
    }

    /// <summary>Shuts off the machine.</summary>
    public void ShutOff()
    {
      pump.ShutOff();
      BypassFlowRate = 0;
      TotalFlowRate = 0;
      OperatingNumber = 0;
    }

    /// <summary>Gets the power consumption [kW].</summary>
    public double GetElectricConsumption()
    { return Pump.GetElectricConsumption() * OperatingNumber; }

    #endregion

    #region staticメソッド

    /// <summary>Computes the required number of operating pumps.</summary>
    /// <param name="flowRate">Required water flow rate [m³/s].</param>
    /// <returns>Required number of operating units.</returns>
    private int GetOperatingNumber(double flowRate)
    {
      if (flowRate <= 0) return 0;

      //最小吐出圧制御の場合：1台ずつ増やして確認
      if (Pump.Control == CentrifugalPump.ControlMethod.MinimumPressure)
      {
        double r2 = flowRate * flowRate;
        double ps = resistanceCoefficient * r2 + ActualHead;
        //締切揚程が必要揚程を上回ることの確認
        pump.UpdateWithFlowRateAndRotationRatio(0, 1.0);
        if (pump.Pressure < ps) throw new Exception("Pump Pressure Error");
        int opNum = 1;
        while (true)
        {
          //最大回転数比で水量が足りるか確認
          double tf;
          pump.updateWithResistanceAndRotationRatio(1.0, resistanceCoefficient, ActualHead, opNum, out tf);
          if (flowRate < tf) break;
          else opNum++;
          if (50 < opNum) throw new Exception("Pump Number Error");
        }
        return opNum;
      }
      //吐出圧一定制御・バイパス制御の場合には台数で流量均等分割
      else
      {
        pump.updateWithRotationRatioAndPressure(1.0, PressureSetpoint);
        return (int)Math.Ceiling(flowRate / Pump.VolumetricFlowRate);
      }
    }

    #endregion

  }

}
