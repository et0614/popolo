/* PlateHeatExchanger.cs
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
using Popolo.Core.Physics;

namespace Popolo.Core.HVAC.HeatExchanger
{
  /// <summary>Plate heat exchanger (counter-flow, water-to-water).</summary>
  public class PlateHeatExchanger : IReadOnlyPlateHeatExchanger
  {
    
    #region インスタンス変数・プロパティ

    /// <summary>Gets a value indicating whether the heat exchanger is overloaded.</summary>
    public bool IsOverLoad { get; private set; }

    /// <summary>Gets the overall heat transfer coefficient UA [kW/K].</summary>
    public double HeatTransferCoefficient { get; private set; }

    /// <summary>Gets the maximum allowable heat source flow rate [kg/s].</summary>
    public double MaxHeatSourceFlowRate { get; private set; }

    /// <summary>Gets the maximum allowable supply flow rate [kg/s].</summary>
    public double MaxSupplyFlowRate { get; private set; }

    /// <summary>Gets the current heat source flow rate [kg/s].</summary>
    public double HeatSourceFlowRate { get; private set; }

    /// <summary>Gets the current supply flow rate [kg/s].</summary>
    public double SupplyFlowRate { get; private set; }

    /// <summary>Gets the heat source inlet temperature [°C].</summary>
    public double HeatSourceInletTemperature { get; private set; }

    /// <summary>Gets the heat source outlet temperature [°C].</summary>
    public double HeatSourceOutletTemperature { get; private set; }

    /// <summary>Gets the supply temperature [°C].</summary>
    public double SupplyTemperature { get; private set; }

    /// <summary>Gets the return temperature [°C].</summary>
    public double ReturnTemperature { get; private set; }

    /// <summary>Gets or sets the supply temperature setpoint [°C].</summary>
    public double SupplyTemperatureSetpoint { get; set; }

    /// <summary>Gets the heat transfer rate [kW].</summary>
    public double HeatTransfer { get; private set; }

    #endregion

    #region コンストラクタ・インスタンスメソッド

    /// <summary>Initializes a new instance.</summary>
    /// <param name="heatTransfer">Rated heat transfer rate [kW].</param>
    /// <param name="heatsourceTemperature">Heat source temperature [°C].</param>
    /// <param name="heatsourceFlowRate">Heat source flow rate [kg/s].</param>
    /// <param name="supplyTemperature">Supply temperature [°C].</param>
    /// <param name="supplyFlowRate">Supply flow rate [kg/s].</param>
    public PlateHeatExchanger(double heatTransfer,
      double heatsourceTemperature, double heatsourceFlowRate, double supplyTemperature, double supplyFlowRate)
    {
      MaxHeatSourceFlowRate = heatsourceFlowRate;
      MaxSupplyFlowRate = supplyFlowRate;
      HeatSourceInletTemperature = heatsourceTemperature;

      bool isHeating = supplyTemperature < heatsourceTemperature;
      if (isHeating)
      {
        ReturnTemperature = supplyTemperature - heatTransfer / (0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat * supplyFlowRate);
        HeatTransferCoefficient = HeatExchange.GetHeatTransferCoefficient
          (heatsourceTemperature, ReturnTemperature, heatsourceFlowRate * 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat,
          supplyFlowRate * 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat, heatTransfer, HeatExchange.FlowType.CounterFlow);
      }
      else
      {
        ReturnTemperature = supplyTemperature + heatTransfer / (0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat * supplyFlowRate);
        HeatTransferCoefficient = HeatExchange.GetHeatTransferCoefficient
          (ReturnTemperature, heatsourceTemperature, supplyFlowRate * 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat,
          heatsourceFlowRate * 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat, heatTransfer, HeatExchange.FlowType.CounterFlow);
      }
      ShutOff();
    }

    /// <summary>Initializes a new instance.</summary>
    /// <param name="heatTransferCoefficient">Overall heat transfer coefficient UA [kW/K].</param>
    /// <param name="heatsourceFlowRate">Heat source flow rate [kg/s].</param>
    /// <param name="supplyFlowRate">Supply flow rate [kg/s].</param>
    public PlateHeatExchanger(double heatTransferCoefficient, double heatsourceFlowRate, double supplyFlowRate)
    {
      HeatTransferCoefficient = heatTransferCoefficient;
      MaxHeatSourceFlowRate = heatsourceFlowRate;
      MaxSupplyFlowRate = supplyFlowRate;
      ShutOff();
    }

    /// <summary>Updates the outlet state from given inlet conditions (free-running calculation).</summary>
    /// <param name="heatsourceTemperature">Heat source inlet temperature [°C].</param>
    /// <param name="returnTemperature">Return water temperature [°C].</param>
    /// <param name="heatsourceFlowRate">Heat source flow rate [kg/s].</param>
    /// <param name="supplyFlowRate">Supply flow rate [kg/s].</param>
    public void Update
      (double heatsourceTemperature, double returnTemperature, double heatsourceFlowRate, double supplyFlowRate)
    {
      IsOverLoad = false;
      HeatSourceInletTemperature = heatsourceTemperature;
      ReturnTemperature = returnTemperature;
      HeatSourceFlowRate = heatsourceFlowRate;
      SupplyFlowRate = supplyFlowRate;

      //流量0.01%以下の場合は停止
      if (heatsourceFlowRate <= 0.001 * MaxHeatSourceFlowRate || supplyFlowRate <= 0.001 * MaxSupplyFlowRate)
      {
        ShutOff();
        return;
      }

      double sign;
      bool isHeating = returnTemperature < heatsourceTemperature;
      double mchs = HeatSourceFlowRate * 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat;
      double mcsp = SupplyFlowRate * 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat;
      if (isHeating)
      {
        sign = 1;
        HeatTransfer = HeatExchange.GetHeatTransfer(heatsourceTemperature, returnTemperature,
          mchs, mcsp, HeatTransferCoefficient, HeatExchange.FlowType.CounterFlow);
      }
      else
      {
        sign = -1;
        HeatTransfer = HeatExchange.GetHeatTransfer(returnTemperature, heatsourceTemperature,
          mcsp, mchs, HeatTransferCoefficient, HeatExchange.FlowType.CounterFlow);
      }

      //出口水温計算
      SupplyTemperature = ReturnTemperature + sign * HeatTransfer / mcsp;
      HeatSourceOutletTemperature = HeatSourceInletTemperature - sign * HeatTransfer / mchs;
    }

    /// <summary>Controls the supply temperature to the setpoint by adjusting the heat source flow rate.</summary>
    /// <param name="heatsourceTemperature">Heat source inlet temperature [°C].</param>
    /// <param name="returnTemperature">Return water temperature [°C].</param>
    /// <param name="supplyFlowRate">Supply flow rate [kg/s].</param>
    public void ControlSupplyTemperature
      (double heatsourceTemperature, double returnTemperature, double supplyFlowRate)
    {
      HeatSourceInletTemperature = heatsourceTemperature;
      ReturnTemperature = returnTemperature;
      SupplyFlowRate = supplyFlowRate;

      //加熱冷却逆転判定
      bool isHeating = returnTemperature < SupplyTemperatureSetpoint;
      bool isRev = (heatsourceTemperature < returnTemperature && isHeating) ||
        (returnTemperature < heatsourceTemperature && !isHeating);

      //流量0以下または加熱冷却逆転の場合には機器を停止
      if (supplyFlowRate <= 0 || isRev)
      {
        ShutOff();
        return;
      }

      double sign;
      double mcSply = supplyFlowRate * 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat;
      double ql = Math.Abs(returnTemperature - SupplyTemperatureSetpoint) * mcSply;
      //加熱運転
      if (isHeating)
      {
        sign = 1;
        //最大流量での熱交換量計算
        HeatTransfer = HeatExchange.GetHeatTransfer
          (heatsourceTemperature, returnTemperature, MaxHeatSourceFlowRate * 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat, mcSply,
          HeatTransferCoefficient, HeatExchange.FlowType.CounterFlow);
        IsOverLoad = HeatTransfer < ql;
        //過負荷判定
        if (IsOverLoad) HeatSourceFlowRate = MaxHeatSourceFlowRate;
        else
        {
          Roots.ErrorFunction eFnc = delegate (double flow) {
            HeatTransfer = HeatExchange.GetHeatTransfer
            (heatsourceTemperature, returnTemperature, flow * 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat, mcSply,
            HeatTransferCoefficient, HeatExchange.FlowType.CounterFlow);
            return HeatTransfer - ql;
          };
          HeatSourceFlowRate = Roots.Bisection
            (eFnc, 0, MaxHeatSourceFlowRate, -ql, HeatTransfer - ql, 0, MaxHeatSourceFlowRate * 0.001, 20);
        }
      }
      //冷却運転
      else
      {
        sign = -1;
        //最大流量での熱交換量計算
        HeatTransfer = HeatExchange.GetHeatTransfer
          (returnTemperature, heatsourceTemperature, mcSply, MaxHeatSourceFlowRate * 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat,
          HeatTransferCoefficient, HeatExchange.FlowType.CounterFlow);
        IsOverLoad = HeatTransfer < ql;
        //過負荷判定
        if (IsOverLoad) HeatSourceFlowRate = MaxHeatSourceFlowRate;
        else
        {
          Roots.ErrorFunction eFnc = delegate (double flow) {
            HeatTransfer = HeatExchange.GetHeatTransfer
            (returnTemperature, heatsourceTemperature, mcSply, flow * 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat,
            HeatTransferCoefficient, HeatExchange.FlowType.CounterFlow);
            return HeatTransfer - ql;
          };
          HeatSourceFlowRate = Roots.Bisection
            (eFnc, 0, MaxHeatSourceFlowRate, -ql, HeatTransfer - ql, 0, MaxHeatSourceFlowRate * 0.001, 20);
        }
      }

      //出口水温計算
      SupplyTemperature = ReturnTemperature + sign * HeatTransfer / mcSply;
      HeatSourceOutletTemperature = HeatSourceInletTemperature
        - sign * HeatTransfer / (HeatSourceFlowRate * 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat);
    }

    /// <summary>Shuts off the heat exchanger (zero flow, outlet temperature equals inlet).</summary>
    public void ShutOff()
    {
      HeatTransfer = 0;
      SupplyTemperature = ReturnTemperature;
      HeatSourceOutletTemperature = HeatSourceInletTemperature;
      SupplyFlowRate = HeatSourceFlowRate = 0;
    }

    #endregion

  }
}
