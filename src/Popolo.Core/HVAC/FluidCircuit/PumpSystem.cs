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

using Popolo.Core.Exceptions;

namespace Popolo.Core.HVAC.FluidCircuit
{
  /// <summary>Represents a pump system with staged operation.</summary>
  public class PumpSystem : IReadOnlyPumpSystem
  {

    #region Instance variables and properties

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
    public int ActivePumpCount { get; private set; }

    /// <summary>Gets the number of pumps [units].</summary>
    public int PumpCount { get; private set; }

    /// <summary>Gets the actual head [kPa].</summary>
    public double ActualHead { get; private set; }

    /// <summary>Gets or sets the pressure setpoint [kPa].</summary>
    public double PressureSetpoint
    {
      get { return pump.PressureSetpoint; }
      set { pump.PressureSetpoint = value; }
    }

    #endregion

    #region Constructors

    /// <summary>Initializes a new instance.</summary>
    /// <param name="pump">Centrifugal pump.</param>
    /// <param name="designPressure">Design pressure [kPa].</param>
    /// <param name="designFlowRate">Design flow rate [m³/s].</param>
    /// <param name="actualHead">Actual head [kPa].</param>
    /// <param name="pumpCount">Number of pumps.</param>
    public PumpSystem
      (CentrifugalPump pump, double designPressure, double designFlowRate, double actualHead, int pumpCount)
    {
      this.pump = pump;
      PumpCount = pumpCount;
      PressureSetpoint = designPressure;
      ActualHead = actualHead;

      //Compute the resistance coefficient [kPa/(m3/s)^2]
      resistanceCoefficient = (designPressure - actualHead) / (designFlowRate * designFlowRate);
    }

    #endregion

    #region Instance methods

    /// <summary>Updates the state.</summary>
    public void UpdateState()
    {
      //Determine the number of operating pumps
      ActivePumpCount = GetActivePumpCount(TotalFlowRate);
      if (ActivePumpCount == 0)
      {
        ShutOff();
        return;
      }
      //Overload case
      if (PumpCount < ActivePumpCount)
      {
        ActivePumpCount = PumpCount;
        double tf;
        pump.updateWithResistanceAndRotationRatio(1.0, resistanceCoefficient, ActualHead, PumpCount, out tf);
        TotalFlowRate = tf;
        BypassFlowRate = 0;
      }
      else
      {
        double vFlow = TotalFlowRate / ActivePumpCount;
        //Minimum discharge pressure control:
        if (Pump.Control == CentrifugalPump.ControlMethod.MinPressure)
        {
          double pressure = TotalFlowRate * TotalFlowRate * resistanceCoefficient + ActualHead;
          pump.updateWithFlowRateAndPressure(vFlow, pressure);
          //Below the minimum rotational speed ratio [-]
          if (Pump.RotationRatio < Pump.MinRotationRatio)
          {
            double tf;
            pump.updateWithResistanceAndRotationRatio
              (Pump.MinRotationRatio, resistanceCoefficient, ActualHead, ActivePumpCount, out tf);
          }
        }
        //Constant discharge pressure control or bypass control
        else pump.UpdateState(vFlow);

        BypassFlowRate = (Pump.VolumetricFlowRate * ActivePumpCount) - TotalFlowRate;
      }
    }

    /// <summary>Shuts off the machine.</summary>
    public void ShutOff()
    {
      pump.ShutOff();
      BypassFlowRate = 0;
      TotalFlowRate = 0;
      ActivePumpCount = 0;
    }

    /// <summary>Gets the power consumption [kW].</summary>
    public double GetElectricConsumption()
    { return Pump.GetElectricConsumption() * ActivePumpCount; }

    #endregion

    #region Static methods

    /// <summary>Computes the required number of operating pumps.</summary>
    /// <param name="flowRate">Required water flow rate [m³/s].</param>
    /// <returns>Required number of operating units.</returns>
    private int GetActivePumpCount(double flowRate)
    {
      if (flowRate <= 0) return 0;

      //Minimum discharge pressure control: check by adding one unit at a time
      if (Pump.Control == CentrifugalPump.ControlMethod.MinPressure)
      {
        double r2 = flowRate * flowRate;
        double ps = resistanceCoefficient * r2 + ActualHead;
        //Verify that the shutoff head exceeds the required head
        pump.UpdateWithFlowRateAndRotationRatio(0, 1.0);
        if (pump.Pressure < ps) throw new PopoloInvalidOperationException(
          $"Pump shutoff head ({pump.Pressure} kPa) is less than required static head ({ps} kPa). "
          + "Pump is undersized for the specified flow and resistance.");
        int opNum = 1;
        while (true)
        {
          //Check whether the flow rate is sufficient at the maximum rotational speed ratio
          double tf;
          pump.updateWithResistanceAndRotationRatio(1.0, resistanceCoefficient, ActualHead, opNum, out tf);
          if (flowRate < tf) break;
          else opNum++;
          if (50 < opNum) throw new PopoloNumericalException(
            "PumpSystem.GetActivePumpCount",
            $"Failed to determine operating pump count within 50 units; required flow rate may be too large.");
        }
        return opNum;
      }
      //For constant discharge pressure control or bypass control, split the flow rate evenly among the units
      else
      {
        pump.updateWithRotationRatioAndPressure(1.0, PressureSetpoint);
        return (int)Math.Ceiling(flowRate / Pump.VolumetricFlowRate);
      }
    }

    #endregion

  }

}
