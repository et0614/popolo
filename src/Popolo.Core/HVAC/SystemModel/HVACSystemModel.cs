/* HVACSystemModel.cs
 * 
 * Copyright (C) 2016 E.Togashi
 * 
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 2 of the License, or (at
 * your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful, but
 * WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 675 Mass Ave, Cambridge, MA 02139, USA.
 */

using System;

using Popolo.Core.Climate;
using Popolo.Core.Building;
using Popolo.Core.Numerics;
using Popolo.Core.Physics;

namespace Popolo.Core.HVAC.SystemModel
{
  /// <summary>Coupled building, heat source, and air-conditioning system model.</summary>
  public class HVACSystemModel
  {

    #region 定数宣言


    #endregion

    #region インスタンス変数・プロパティ
    
    /// <summary>Multiplier for each air-conditioning system branch.</summary>
    private int[] acFactor;

    /// <summary>Gets the current date and time.</summary>
    public DateTime CurrentDateTime { get; private set; }

    /// <summary>Simulation time step [s].</summary>
    private double timeStep = 3600;

    /// <summary>Gets or sets the simulation time step [s].</summary>
    public double TimeStep
    {
      get { return timeStep; }
      set
      {
        timeStep = value;
        bModel.TimeStep = value;
        foreach (IAirConditioningSystemModel acs in acModels) acs.TimeStep = value;
        hsModel.TimeStep = value;
      }
    }

    /// <summary>Building thermal load calculation model.</summary>
    private BuildingThermalModel bModel;

    /// <summary>List of secondary air-conditioning system models.</summary>
    private IAirConditioningSystemModel[] acModels;

    /// <summary>Heat source system model.</summary>
    private HeatSourceSystemModel hsModel;

    /// <summary>Gets the building thermal load calculation model.</summary>
    public IReadOnlyBuildingThermalModel BuildingThermalModel { get { return bModel; } }

    /// <summary>Gets the list of secondary air-conditioning system models.</summary>
    public IReadOnlyAirConditioningSystemModel[] AirConditioningSystemModel { get { return acModels; } }

    /// <summary>Gets the heat source system model.</summary>
    public IReadOnlyHeatSourceSystemModel HeatSourceSystemModel { get { return hsModel; } }

    /// <summary>Gets or sets the hot water lower temperature limit [°C].</summary>
    public double HotWaterLowerLimitTemperature { get; set; } = 35;

    /// <summary>Gets or sets the hot water supply temperature setpoint [°C].</summary>
    public double HotWaterSupplyTemperatureSetpoint
    {
      get { return hsModel.HotWaterSupplyTemperatureSetpoint; }
      set { hsModel.HotWaterSupplyTemperatureSetpoint = value; }
    }

    /// <summary>Gets the hot water supply temperature [°C].</summary>
    public double HotWaterSupplyTemperature { get; private set; } = 45;

    /// <summary>Gets the hot water return temperature [°C].</summary>
    public double HotWaterReturnTemperature { get; private set; } = 40;

    /// <summary>Gets the hot water flow rate [kg/s].</summary>
    public double HotWaterFlowRate { get; private set; }

    /// <summary>Gets or sets the chilled water upper temperature limit [°C].</summary>
    public double ChilledWaterUpperLimitTemperature { get; set; } = 15;

    /// <summary>Gets or sets the chilled water supply temperature setpoint [°C].</summary>
    public double ChilledWaterSupplyTemperatureSetpoint
    {
      get { return hsModel.ChilledWaterSupplyTemperatureSetpoint; }
      set { hsModel.ChilledWaterSupplyTemperatureSetpoint = value; }
    }

    /// <summary>Gets the chilled water supply temperature [°C].</summary>
    public double ChilledWaterSupplyTemperature { get; private set; } = 7;

    /// <summary>Gets the chilled water return temperature [°C].</summary>
    public double ChilledWaterReturnTemperature { get; private set; } = 12;

    /// <summary>Gets the chilled water flow rate [kg/s].</summary>
    public double ChilledWaterFlowRate { get; private set; }

    /// <summary>Gets the unmet heating load [kW].</summary>
    public double RemainingHeatingLoad { get; private set; }

    /// <summary>Gets the unmet cooling load [kW].</summary>
    public double RemainingCoolingLoad { get; private set; }

    #endregion

    #region コンストラクタ

    /// <summary>Initializes a new instance.</summary>
    /// <param name="bModel">Building thermal load calculation model.</param>
    /// <param name="acModels">List of secondary air-conditioning system models.</param>
    /// <param name="hsModel">Heat source system model.</param>
    public HVACSystemModel
      (BuildingThermalModel bModel, IAirConditioningSystemModel[] acModels, HeatSourceSystemModel hsModel)
    {
      this.bModel = bModel;
      this.acModels = acModels;
      this.hsModel = hsModel;
      acFactor = new int[acModels.Length];
      for (int i = 0; i < acFactor.Length; i++) acFactor[i] = 1;
    }

    #endregion

    #region インスタンスメソッド

    /// <summary>Updates the system state for the current time step.</summary>
    public void Update()
    {
      //未処理負荷初期化
      RemainingCoolingLoad = RemainingHeatingLoad = 0;

      //二次側からの還温度を計算
      CalcReturnWaterState(ChilledWaterSupplyTemperatureSetpoint, HotWaterSupplyTemperatureSetpoint);

      //一次側過負荷判定
      hsModel.ForecastSupplyWaterTemperature
        (ChilledWaterFlowRate, ChilledWaterReturnTemperature, HotWaterFlowRate, HotWaterReturnTemperature);

      //冷却能力不足の場合
      if (hsModel.IsOverLoad_C)
      {
        //往温度上限値で計算
        CalcReturnWaterState(ChilledWaterUpperLimitTemperature, HotWaterSupplyTemperatureSetpoint);
        hsModel.ForecastSupplyWaterTemperature
          (ChilledWaterFlowRate, ChilledWaterReturnTemperature, HotWaterFlowRate, HotWaterReturnTemperature);

        //未処理負荷発生
        if (ChilledWaterUpperLimitTemperature < hsModel.ChilledWaterSupplyTemperature)
          RemainingCoolingLoad = ChilledWaterFlowRate * 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat *
            (hsModel.ChilledWaterSupplyTemperature - ChilledWaterUpperLimitTemperature);
        //往温度上昇
        else
        {
          Roots.ErrorFunction eFnc = delegate (double tcSply)
          {
            CalcReturnWaterState(tcSply, HotWaterSupplyTemperatureSetpoint);
            hsModel.ForecastSupplyWaterTemperature
            (ChilledWaterFlowRate, ChilledWaterReturnTemperature, HotWaterFlowRate, HotWaterReturnTemperature);
            return tcSply - hsModel.ChilledWaterSupplyTemperature;
          };
          ChilledWaterSupplyTemperature = Roots.Bisection
            (eFnc, ChilledWaterSupplyTemperatureSetpoint, ChilledWaterUpperLimitTemperature, 0.01, 0.01, 10);
        }
      }
      else ChilledWaterSupplyTemperature = ChilledWaterSupplyTemperatureSetpoint;

      //加熱能力不足の場合
      if (hsModel.IsOverLoad_H)
      {
        //往温度下限値で計算
        CalcReturnWaterState(ChilledWaterSupplyTemperature, HotWaterLowerLimitTemperature);
        hsModel.ForecastSupplyWaterTemperature
          (ChilledWaterFlowRate, ChilledWaterReturnTemperature, HotWaterFlowRate, HotWaterReturnTemperature);

        //未処理負荷発生
        if (hsModel.HotWaterSupplyTemperature < HotWaterLowerLimitTemperature)
          RemainingHeatingLoad = HotWaterFlowRate * 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat *
            (HotWaterLowerLimitTemperature - hsModel.HotWaterSupplyTemperature);
        //往温度低下
        else
        {
          Roots.ErrorFunction eFnc = delegate (double tcSply)
          {
            CalcReturnWaterState(ChilledWaterSupplyTemperature, tcSply);
            hsModel.ForecastSupplyWaterTemperature
            (ChilledWaterFlowRate, ChilledWaterReturnTemperature, HotWaterFlowRate, HotWaterReturnTemperature);
            return tcSply - hsModel.HotWaterSupplyTemperature;
          };
          HotWaterSupplyTemperature = Roots.Bisection
            (eFnc, HotWaterLowerLimitTemperature, HotWaterSupplyTemperatureSetpoint, 0.01, 0.01, 10);
        }
      }
      else HotWaterSupplyTemperature = HotWaterSupplyTemperatureSetpoint;

      //状態確定
      hsModel.FixState();
      foreach (IAirConditioningSystemModel ac in acModels) ac.FixState();
      bModel.FixState();
    }

    /// <summary>Computes the return water temperatures [°C] from the secondary system loads.</summary>
    /// <param name="tcwSP">Chilled water supply temperature setpoint [°C].</param>
    /// <param name="thwSP">Hot water supply temperature setpoint [°C].</param>
    private void CalcReturnWaterState(double tcwSP, double thwSP)
    {
      ChilledWaterFlowRate = HotWaterFlowRate = ChilledWaterReturnTemperature = HotWaterReturnTemperature = 0;      
      for (int i = 0; i < acModels.Length; i++)
      {
        IAirConditioningSystemModel acm = acModels[i];
        acm.ForecastReturnWaterTemperature(tcwSP, thwSP);
        ChilledWaterReturnTemperature += acm.ChilledWaterReturnTemperature * acm.ChilledWaterFlowRate * acFactor[i];
        HotWaterReturnTemperature += acm.HotWaterReturnTemperature * acm.HotWaterFlowRate * acFactor[i];
        ChilledWaterFlowRate += acm.ChilledWaterFlowRate * acFactor[i];
        HotWaterFlowRate += acm.HotWaterFlowRate * acFactor[i];
      }
      if (ChilledWaterFlowRate == 0) ChilledWaterReturnTemperature = tcwSP;
      else ChilledWaterReturnTemperature /= ChilledWaterFlowRate;
      if (HotWaterFlowRate == 0) HotWaterReturnTemperature = thwSP;
      else HotWaterReturnTemperature /= HotWaterFlowRate;
    }

    /// <summary>Updates the outdoor air conditions and propagates them to all sub-systems.</summary>
    /// <param name="dTime">Current date and time.</param>
    /// <param name="sun">Solar radiation model.</param>
    /// <param name="temperature">Outdoor air dry-bulb temperature [°C].</param>
    /// <param name="humidityRatio">Outdoor air humidity ratio [kg/kg].</param>
    /// <param name="nocRadiation">Nocturnal radiation [W/m²].</param>
    public void UpdateOutdoorCondition
      (DateTime dTime, IReadOnlySun sun, double temperature, double humidityRatio, double nocRadiation)
    {
      MoistAir outdoorAir = new MoistAir(temperature, humidityRatio);
      CurrentDateTime = dTime;
      bModel.UpdateOutdoorCondition(dTime, sun, temperature, humidityRatio, nocRadiation);
      foreach (IAirConditioningSystemModel acs in acModels)
      {
        acs.CurrentDateTime = dTime;
        acs.OutdoorAir = outdoorAir;
      }
      hsModel.CurrentDateTime = dTime;
      hsModel.OutdoorAir = outdoorAir;
    }

    /// <summary>Sets the multiplier for the specified air-conditioning system branch.</summary>
    /// <param name="airConditioningSystemIndex">Air-conditioning system branch index.</param>
    /// <param name="factor">Multiplier [-].</param>
    public void SetACFactor(int airConditioningSystemIndex, int factor)
    { acFactor[airConditioningSystemIndex] = factor; }

    #endregion

  }
}
