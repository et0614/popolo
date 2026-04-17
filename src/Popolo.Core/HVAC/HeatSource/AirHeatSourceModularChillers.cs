/* AirHeatSourceHeatPump.cs
 * 
 * Copyright (C) 2015 E.Togashi
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

using Popolo.Core.Numerics;
using Popolo.Core.Physics;

namespace Popolo.Core.HVAC.HeatSource
{
  /// <summary>Air-heat-source modular chiller/heat-pump unit.</summary>
  public class AirHeatSourceModularChillers: IReadOnlyAirHeatSourceModularChillers
  {

    #region 定数宣言


    #endregion

    #region 列挙型定義

    /// <summary>Operating mode.</summary>
    public enum OperatingMode
    {
      /// <summary>Heating mode.</summary>
      Heating,
      /// <summary>Cooling mode.</summary>
      Cooling,
      /// <summary>Shut-off mode.</summary>
      ShutOff
    }

    #endregion

    #region 特性係数

    /// <summary>Maximum capacity ratio approximation coefficients for cooling mode.</summary>
    private readonly double[] aMax_C = new double[] { -4.1557e-04, 1.0518e-01, 1.5448e-01, -3.8823e01 };

    /// <summary>Maximum capacity ratio approximation coefficients for heating mode.</summary>
    private readonly double[] aMax_H = new double[] { 9.2525e-06, 1.7117e-02, -6.7223e-03, -2.4744 };

    /// <summary>COP ratio approximation coefficients for cooling mode.</summary>
    private readonly double[] aCop_C = new double[] { 0.60824, -2.4486, 2.8067 };

    /// <summary>COP ratio approximation coefficients for heating mode.</summary>
    private readonly double[] aCop_H = new double[] { 0.088163, -1.3885, 2.2981 };

    /// <summary>Partial load characteristic coefficients.</summary>
    private readonly double[] a_PL = new double[] { -1.4118, 1.3611, 1.0507 };

    #endregion

    #region インスタンス変数・プロパティ

    /// <summary>Nominal theoretical (Carnot) COP [-].</summary>
    private readonly double copFLRT_C, copFLRT_H;

    /// <summary>Air mass flow rate per unit [kg/s].</summary>
    private double coolingAirFlowRate, heatingAirFlowRate;

    /// <summary>Auxiliary electric power consumption [kW].</summary>
    private double auxElec;
    
    /// <summary>Gets or sets the operating mode.</summary>
    public OperatingMode Mode { get; set; }

    /// <summary>Gets or sets a value indicating whether to maximise operating efficiency by adjusting the number of active units.</summary>
    public bool MaximizeEfficiency { get; set; } = true;

    /// <summary>Gets the total number of modules.</summary>
    public int NumberOfUnits { get; private set; }

    /// <summary>Gets the number of currently operating units.</summary>
    public int OperatingNumber { get; private set; }

    /// <summary>Gets a value indicating whether the unit is a heat-pump model (supports both heating and cooling).</summary>
    public bool IsHeatPumpModel { get; private set; }

    /// <summary>Gets the nominal cooling capacity per unit [kW].</summary>
    public double NominalCoolingCapacity { get; private set; }

    /// <summary>Gets the nominal cooling COP [-].</summary>
    public double NominalCoolingCOP { get; private set; }

    /// <summary>Gets the nominal heating capacity per unit [kW].</summary>
    public double NominalHeatingCapacity { get; private set; }

    /// <summary>Gets the nominal heating COP [-].</summary>
    public double NominalHeatingCOP { get; private set; }

    /// <summary>Gets the water outlet temperature [°C].</summary>
    public double WaterOutletTemperature { get; private set; }

    /// <summary>Gets or sets the water outlet temperature setpoint [°C].</summary>
    public double WaterOutletSetPointTemperature { get; set; }

    /// <summary>Gets the water inlet temperature [°C].</summary>
    public double WaterInletTemperature { get; private set; }

    /// <summary>Gets the water flow rate per unit [kg/s].</summary>
    public double WaterFlowRate { get; private set; }

    /// <summary>Gets the air mass flow rate per unit [kg/s].</summary>
    public double AirFlowRate
    {
      get
      {
        if (Mode == OperatingMode.Cooling) return coolingAirFlowRate;
        else if (Mode == OperatingMode.Heating) return heatingAirFlowRate;
        else return 0;
      }
    }

    /// <summary>Gets the electric power consumption per unit [kW].</summary>
    public double ElectricConsumption { get; private set; }

    /// <summary>Gets the auxiliary electric power consumption per unit [kW].</summary>
    public double AuxiliaryElectricConsumption { get; private set; }

    /// <summary>Gets the ambient air dry-bulb temperature [°C].</summary>
    public double AmbientTemperature { get; private set; }

    /// <summary>Gets the coefficient of performance [-].</summary>
    public double COP
    {
      get
      {
        if (ElectricConsumption != 0)
          return Math.Abs(WaterOutletTemperature - WaterInletTemperature)
            * 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat * WaterFlowRate / (ElectricConsumption * OperatingNumber);
        else return 0;
      }
    }

    /// <summary>Gets the total cooling output [kW].</summary>
    public double CoolingLoad
    {
      get
      {
        if (Mode == OperatingMode.Cooling)
          return (WaterInletTemperature - WaterOutletTemperature) * 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat * WaterFlowRate;
        else return 0;
      }
    }

    /// <summary>Gets the total heating output [kW].</summary>
    public double HeatingLoad
    {
      get
      {
        if (Mode == OperatingMode.Heating)
          return (WaterOutletTemperature - WaterInletTemperature) * 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat * WaterFlowRate;
        else return 0;
      }
    }

    /// <summary>Gets the maximum chilled water flow rate [kg/s].</summary>
    public double MaxChilledWaterFlowRate { get; private set; }

    /// <summary>Gets the maximum hot water flow rate [kg/s].</summary>
    public double MaxHotWaterFlowRate { get; private set; }

    /// <summary>Gets the minimum chilled water flow rate [kg/s].</summary>
    public double MinChilledWaterFlowRate { get; private set; }

    /// <summary>Gets the minimum hot water flow rate [kg/s].</summary>
    public double MinHotWaterFlowRate { get; private set; }

    /// <summary>Gets a value indicating whether the unit is overloaded.</summary>
    public bool IsOverLoad { get; private set; }

    /// <summary>Gets or sets the minimum partial load rate [-].</summary>
    public double MinimumPartialLoadRate { get; set; } = 0.2;

    #endregion

    #region コンストラクタ

    /// <summary>Initializes a new instance from rated conditions.</summary>
    /// <param name="coolingCapacity">Nominal cooling capacity per unit [kW].</param>
    /// <param name="chilledWaterOutletTemperature">Chilled water outlet temperature [°C].</param>
    /// <param name="chilledWaterFlowRate">Chilled water flow rate per unit [kg/s].</param>
    /// <param name="coolingAirTemperature">Cooling-mode ambient air temperature [°C].</param>
    /// <param name="coolingAirFlowRate">Cooling-mode air mass flow rate per unit [kg/s].</param>
    /// <param name="coolingElectricity">Cooling-mode electric power consumption per unit [kW].</param>
    /// <param name="heatingCapacity">Nominal heating capacity per unit [kW].</param>
    /// <param name="hotWaterOutletTemperature">Hot water outlet temperature [°C].</param>
    /// <param name="hotWaterFlowRate">Hot water flow rate per unit [kg/s].</param>
    /// <param name="heatingAirTemperature">Heating-mode ambient air temperature [°C].</param>
    /// <param name="heatingAirFlowRate">Heating-mode air mass flow rate per unit [kg/s].</param>
    /// <param name="heatingElectricity">Heating-mode electric power consumption per unit [kW].</param>
    /// <param name="numberOfUnits">Number of modules.</param>
    /// <param name="auxiliaryElectricConsumption">Auxiliary electric power consumption per unit [kW].</param>
    public AirHeatSourceModularChillers(
      double coolingCapacity, double chilledWaterOutletTemperature, double chilledWaterFlowRate, 
      double coolingAirTemperature, double coolingAirFlowRate, double coolingElectricity,
      double heatingCapacity, double hotWaterOutletTemperature, double hotWaterFlowRate, 
      double heatingAirTemperature, double heatingAirFlowRate, double heatingElectricity,
      int numberOfUnits, double auxiliaryElectricConsumption)
    {
      IsHeatPumpModel = true;

      this.coolingAirFlowRate = coolingAirFlowRate;
      this.heatingAirFlowRate = heatingAirFlowRate;
      this.NominalCoolingCapacity = coolingCapacity;
      this.NominalHeatingCapacity = heatingCapacity;
      this.NumberOfUnits = numberOfUnits;
      this.auxElec = auxiliaryElectricConsumption;
      this.MaxChilledWaterFlowRate = chilledWaterFlowRate * numberOfUnits;
      this.MaxHotWaterFlowRate = hotWaterFlowRate * numberOfUnits;
      this.MinChilledWaterFlowRate = chilledWaterFlowRate * 0.4;
      this.MinHotWaterFlowRate = hotWaterFlowRate * 0.4;

      //冷房運転COPの計算
      double mcw = 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat * chilledWaterFlowRate;
      double mcma = (1.005 + 1.846 * 0.020) * coolingAirFlowRate;
      double tao = coolingAirTemperature + (coolingElectricity + coolingCapacity) / mcma;
      copFLRT_C = (chilledWaterOutletTemperature + 273.15) / (tao - chilledWaterOutletTemperature);
      NominalCoolingCOP = coolingCapacity / coolingElectricity;

      //暖房運転COPの計算
      mcw = 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat * hotWaterFlowRate;
      mcma = (1.005 + 1.846 * 0.002) * heatingAirFlowRate;
      tao = heatingAirTemperature + (heatingElectricity - heatingCapacity) / mcma;
      copFLRT_H = (hotWaterOutletTemperature + 273.15) / (hotWaterOutletTemperature - tao);
      NominalHeatingCOP = heatingCapacity / heatingElectricity;

      //停止させる
      ShutOff();
    }

    /// <summary>Initializes a new instance from rated conditions.</summary>
    /// <param name="coolingCapacity">Nominal cooling capacity per unit [kW].</param>
    /// <param name="chilledWaterOutletTemperature">Chilled water outlet temperature [°C].</param>
    /// <param name="chilledWaterFlowRate">Chilled water flow rate per unit [kg/s].</param>
    /// <param name="coolingAirTemperature">Cooling-mode ambient air temperature [°C].</param>
    /// <param name="coolingAirFlowRate">Cooling-mode air mass flow rate per unit [kg/s].</param>
    /// <param name="coolingElectricity">Cooling-mode electric power consumption per unit [kW].</param>
    /// <param name="numberOfUnits">Number of modules.</param>
    /// <param name="auxiliaryElectricConsumption">Auxiliary electric power consumption per unit [kW].</param>
    public AirHeatSourceModularChillers(
      double coolingCapacity, double chilledWaterOutletTemperature, double chilledWaterFlowRate,
      double coolingAirTemperature, double coolingAirFlowRate, double coolingElectricity,
      int numberOfUnits, double auxiliaryElectricConsumption)
    {
      IsHeatPumpModel = false;

      this.coolingAirFlowRate = coolingAirFlowRate;
      this.NominalCoolingCapacity = coolingCapacity;      
      this.NumberOfUnits = numberOfUnits;
      this.auxElec = auxiliaryElectricConsumption;
      this.heatingAirFlowRate = this.NominalHeatingCapacity = 0;

      //冷房運転COPの計算
      double mcw = 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat * chilledWaterFlowRate;
      double mcma = (1.005 + 1.846 * 0.020) * coolingAirFlowRate;
      double tao = coolingAirTemperature + (coolingElectricity + coolingCapacity) / mcma;
      copFLRT_C = (chilledWaterOutletTemperature + 273.15) / (tao - chilledWaterOutletTemperature);
      NominalCoolingCOP = coolingCapacity / coolingElectricity;

      //停止させる
      ShutOff();
    }

    #endregion

    #region 機器の停止処理

    /// <summary>Shuts off all units.</summary>
    public void ShutOff()
    {
      Mode = OperatingMode.ShutOff;
      StayIdle();
    }

    /// <summary>Shuts off all units.</summary>
    private void StayIdle()
    {
      WaterOutletTemperature = WaterInletTemperature;
      ElectricConsumption = 0;
      AuxiliaryElectricConsumption = 0;
      OperatingNumber = 0;
    }

    #endregion

    #region 最大能力計算処理

    /// <summary>Computes the maximum capacity per unit [kW] at the given conditions.</summary>
    /// <param name="waterOutletTemperature">Water outlet temperature [°C].</param>
    /// <param name="ambientTemperature">Ambient air dry-bulb temperature [°C].</param>
    /// <returns>Maximum capacity per unit [kW].</returns>
    public double GetMaxCapacity(double waterOutletTemperature, double ambientTemperature)
    {
      if (Mode == OperatingMode.ShutOff) return 0;
      else if (Mode == OperatingMode.Cooling)
      {
        double two = Math.Min(15, Math.Max(3, waterOutletTemperature)) + 273.15;
        double tai = Math.Min(43, Math.Max(20, ambientTemperature)) + 273.15;
        return NominalCoolingCapacity * (tai * (two * aMax_C[0] + aMax_C[1]) + two * aMax_C[2] + aMax_C[3]);
      }
      else
      {
        double two = Math.Min(55, Math.Max(35, waterOutletTemperature)) + 273.15;
        double tai = Math.Min(21, Math.Max(-15, ambientTemperature)) + 273.15;
        return NominalHeatingCapacity * (tai * (two * aMax_H[0] + aMax_H[1]) + two * aMax_H[2] + aMax_H[3]);
      }
    }

    #endregion

    #region 運転台数計算処理

    /// <summary>Computes the number of units to operate for the given load.</summary>
    /// <param name="load">Required load [kW].</param>
    /// <param name="mCap">Maximum capacity per unit [kW].</param>
    /// <returns>Number of units to operate.</returns>
    private int GetOperatingNumber(double load, double mCap)
    {
      //部分負荷運転により機器効率を最大化させる場合
      if (MaximizeEfficiency)
      {
        double optCap = mCap * (-0.5 * a_PL[1] / a_PL[0]);
        int optNum = (int)Math.Floor(load / optCap);
        if (NumberOfUnits <= optNum) return NumberOfUnits;
        else
        {
          double plf1 = GetPartialLoadFactor(load / (optNum * mCap));
          double plf2 = GetPartialLoadFactor(load / ((optNum + 1) * mCap));
          if (plf1 < plf2) return (optNum + 1);
          else return optNum;
        }
      }
      //最大負荷で運転時間を最小化させる場合（機器寿命重視）
      else return (int)Math.Min(Math.Ceiling(load / mCap), NumberOfUnits);
    }

    /// <summary>Computes the partial load efficiency factor [-].</summary>
    /// <param name="partialLoad">Partial load ratio [-].</param>
    /// <returns>Partial load efficiency factor [-].</returns>
    private double GetPartialLoadFactor(double partialLoad)
    {
      double pl = Math.Max(MinimumPartialLoadRate, partialLoad);
      double plf = pl * (a_PL[0] * pl + a_PL[1]) + a_PL[2];
      if (partialLoad < MinimumPartialLoadRate) return plf * (partialLoad / MinimumPartialLoadRate);
      else return plf;
    }

    #endregion

    #region 状態更新処理

    /// <summary>Updates the unit state for the given inlet conditions and ambient temperature.</summary>
    /// <param name="waterInletTemperature">Water inlet temperature [°C].</param>
    /// <param name="waterFlowRate">Water flow rate [kg/s].</param>
    /// <param name="ambientTemperature">Ambient air dry-bulb temperature [°C].</param>
    public void Update(double waterInletTemperature, double waterFlowRate, double ambientTemperature)
    {
      //状態値を保存
      this.WaterInletTemperature = waterInletTemperature;
      this.WaterFlowRate = waterFlowRate;
      this.AmbientTemperature = ambientTemperature;

      //必要能力を計算
      double load = 0;  //ShutOffの場合には0になる
      double mcw = WaterFlowRate * 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat;
      if (Mode == OperatingMode.Cooling)
        load = mcw * (WaterInletTemperature - WaterOutletSetPointTemperature);
      else if (Mode == OperatingMode.Heating && IsHeatPumpModel)
        load = mcw * (WaterOutletSetPointTemperature - WaterInletTemperature);

      //負荷0ならば停止処理
      if (load <= 0)
      {
        StayIdle();
        return;
      }

      //過負荷判定//最適運転台数を計算する
      double cap = GetMaxCapacity(WaterOutletSetPointTemperature, AmbientTemperature);
      OperatingNumber = GetOperatingNumber(load, cap);
      double qLD = load / OperatingNumber;
      IsOverLoad = cap < qLD;
      if (IsOverLoad) qLD = cap;

      //冷暖切替係数
      double sg, copFLRT, copFLR, mcma;
      double[] aCOP;
      if (Mode == OperatingMode.Cooling)
      {
        sg = -1;
        copFLRT = copFLRT_C;
        copFLR = NominalCoolingCOP;
        aCOP = aCop_C;
        mcma = AirFlowRate * (1.005 + 1.846 * 0.020); //夏季絶対湿度は20g/kgとする
      }
      else
      {
        sg = 1;
        copFLRT = copFLRT_H;
        copFLR = NominalHeatingCOP;
        aCOP = aCop_H;
        mcma = AirFlowRate * (1.005 + 1.846 * 0.002); //冬季絶対湿度は2g/kgとする
      }

      //3次方程式の係数計算
      WaterOutletTemperature = WaterInletTemperature + sg * qLD * OperatingNumber / mcw;
      double abf0 = copFLRT / (WaterOutletTemperature + 273.15);
      double abf1 = 2 * aCOP[0] * copFLRT + sg * aCOP[1];
      double abf2 = copFLRT * (aCOP[0] * copFLRT + sg * aCOP[1]) + aCOP[2];
      double abf3 = mcma * (AmbientTemperature + 273.15) - sg * qLD;
      double[] aTau = new double[4];
      aTau[0] = abf0 * abf0 * aCOP[0] * mcma;
      aTau[1] = -abf0 * (mcma * abf1 + abf0 * aCOP[0] * abf3);
      aTau[2] = abf0 * abf1 * abf3 + mcma * abf2;
      aTau[3] = - abf2 * abf3 - qLD / (GetPartialLoadFactor(qLD / cap) * NominalCoolingCOP);

      double x1, x2, x3, tao;
      bool hasMS;
      CubicEquation.Solve(aTau, out x1, out x2, out x3, out hasMS);
      if (hasMS)
      {
        double dt;
        if (Mode == OperatingMode.Cooling) dt = qLD / NominalCoolingCOP;
        else dt = qLD / NominalHeatingCOP;
        double tao2 = AmbientTemperature + (dt - sg * qLD) / mcma + 273.15;
        double x1er = Math.Abs(tao2 - x1);
        double x2er = Math.Abs(tao2 - x2);
        double x3er = Math.Abs(tao2 - x3);
        if (x1er < x2er && x1er < x3er) tao = x1 - 273.15;
        else if (x2er < x3er) tao = x2 - 273.15;
        else tao = x3 - 273.15;
      }
      else tao = x1 - 273.15;

      //出力設定
      ElectricConsumption = mcma * (tao - AmbientTemperature) + sg * qLD;
      AuxiliaryElectricConsumption = auxElec;
    }
    
    #endregion

  }
}
