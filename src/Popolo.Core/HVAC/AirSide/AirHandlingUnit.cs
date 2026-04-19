/* AirHandlingUnit.cs
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
using Popolo.Core.HVAC.HeatExchanger;
using Popolo.Core.HVAC.FluidCircuit;
using Popolo.Core.Physics;

namespace Popolo.Core.HVAC.AirSide
{
  /// <summary>Air handling unit (AHU) comprising cooling coil, heating coil, humidifier, supply/return fans, and optional heat recovery wheel.</summary>
  public class AirHandlingUnit : IReadOnlyAirHandlingUnit
  {

    #region 列挙型定義

    /// <summary>Outdoor air economiser control mode.</summary>
    public enum OutdoorAirCoolingControl
    {
      /// <summary>No humidifier.</summary>
      None,
      /// <summary>Control based on dry-bulb temperature.</summary>
      DryBulbTemperature,
      /// <summary>Control based on humidity ratio.</summary>
      HumidityRatio,
      /// <summary>Control based on specific enthalpy.</summary>
      Enthalpy
    }

    /// <summary>Humidifier type.</summary>
    public enum HumidifierType
    {
      /// <summary>No humidifier.</summary>
      None,
      /// <summary>Steam humidification.</summary>
      Steam,
      /// <summary>Evaporative (drip) humidification.</summary>
      WettedMedia,
      /// <summary>Water spray humidification.</summary>
      Atomizing,
      /// <summary>Ultrasonic humidification.</summary>
      Ultrasonic,
    }

    #endregion

    #region インスタンス変数・プロパティ

    /// <summary>Cooling/dehumidifying coil.</summary>
    private CrossFinHeatExchanger cCoil;

    /// <summary>Heating coil.</summary>
    private CrossFinHeatExchanger hCoil;

    /// <summary>Supply air fan.</summary>
    private CentrifugalFan saFan;

    /// <summary>Return air fan.</summary>
    private CentrifugalFan raFan;

    /// <summary>Rotary heat recovery wheel.</summary>
    private RotaryRegenerator? regen = null;

    /// <summary>Gets or sets the outdoor air economiser control mode.</summary>
    public OutdoorAirCoolingControl OutdoorAirCooling
    { get; set; } = OutdoorAirCoolingControl.None;

    /// <summary>Gets the humidifier type.</summary>
    public HumidifierType Humidifier { get; private set; }

    /// <summary>Gets the cooling coil.</summary>
    public IReadOnlyCrossFinHeatExchanger CoolingCoil { get { return cCoil; } }

    /// <summary>Gets the heating coil.</summary>
    public IReadOnlyCrossFinHeatExchanger HeatingCoil { get { return hCoil; } }

    /// <summary>Gets the supply air fan.</summary>
    public IReadOnlyFluidMachinery SupplyAirFan { get { return saFan; } }

    /// <summary>Gets the return air fan.</summary>
    public IReadOnlyFluidMachinery ReturnAirFan { get { return raFan; } }

    /// <summary>Gets the rotary heat recovery wheel.</summary>
    public IReadOnlyRotaryRegenerator? Regenerator { get { return regen; } }

    /// <summary>Gets or sets the duct heat loss rate [-].</summary>
    public double DuctHeatLossRate { get; set; } = 0.08;

    /// <summary>Gets or sets a value indicating whether to bypass the heat recovery wheel.</summary>
    public bool BypassRegenerator { get; set; }

    /// <summary>Gets the maximum outdoor air flow rate [kg/s].</summary>
    public double MaxOAFlowRate { get; private set; }

    /// <summary>Gets the minimum outdoor air flow rate [kg/s].</summary>
    public double MinOAFlowRate { get; private set; }

    /// <summary>Gets the outdoor air intake flow rate [kg/s].</summary>
    public double OAFlowRate { get; private set; }

    /// <summary>Gets the return air flow rate [kg/s].</summary>
    public double RAFlowRate { get; private set; }

    /// <summary>Gets the supply air flow rate [kg/s].</summary>
    public double SAFlowRate { get; private set; }

    /// <summary>Gets the exhaust air flow rate [kg/s].</summary>
    public double EAFlowRate { get; private set; }

    /// <summary>Gets or sets the return air dry-bulb temperature [°C].</summary>
    public double RATemperature { get; set; }

    /// <summary>Gets or sets the return air humidity ratio [kg/kg].</summary>
    public double RAHumidityRatio { get; set; }

    /// <summary>Gets or sets the outdoor air dry-bulb temperature [°C].</summary>
    public double OATemperature { get; set; }

    /// <summary>Gets or sets the outdoor air humidity ratio [kg/kg].</summary>
    public double OAHumidityRatio { get; set; }

    /// <summary>Gets the supply air dry-bulb temperature [°C].</summary>
    public double SATemperature { get; private set; }

    /// <summary>Gets the supply air humidity ratio [kg/kg].</summary>
    public double SAHumidityRatio { get; private set; }

    /// <summary>Gets or sets the chilled water inlet temperature [°C].</summary>
    public double ChilledWaterInletTemperature { get; set; }

    /// <summary>Gets or sets the hot water inlet temperature [°C].</summary>
    public double HotWaterInletTemperature { get; set; }

    /// <summary>Gets the water consumption rate for humidification [kg/s].</summary>
    public double WaterConsumption { get; private set; }

    /// <summary>Gets the steam consumption rate for humidification [kg/s].</summary>
    public double SteamConsumption { get; private set; }

    /// <summary>Gets or sets the water supply efficiency of the humidifier [-].</summary>
    public double WaterSupplyCoefficient { get; set; }

    /// <summary>Gets or sets the maximum humidifier saturation efficiency [-].</summary>
    public double MaxSaturationEfficiency { get; set; }

    /// <summary>Gets or sets the humidifier saturation efficiency [-].</summary>
    public double SaturationEfficiency { get; set; }

    /// <summary>Gets or sets a value indicating whether to minimise airflow even at the cost of over-heating or over-cooling.</summary>
    public bool MinimizeAirFlow { get; set; } = true;

    /// <summary>Gets or sets the supply air upper temperature limit in cooling mode [°C].</summary>
    public double UpperTemperatureLimit_C { get; set; } = 19;

    /// <summary>Gets or sets the supply air lower temperature limit in cooling mode [°C].</summary>
    public double LowerTemperatureLimit_C { get; set; } = 13;

    /// <summary>Gets or sets the supply air upper temperature limit in heating mode [°C].</summary>
    public double UpperTemperatureLimit_H { get; set; } = 37;

    /// <summary>Gets or sets the supply air lower temperature limit in heating mode [°C].</summary>
    public double LowerTemperatureLimit_H { get; set; } = 27;

    #endregion

    #region コンストラクタ

    /// <summary>Initializes a new instance.</summary>
    /// <param name="cCoil">Cooling/dehumidifying coil.</param>
    /// <param name="hCoil">Heating coil.</param>
    /// <param name="humidType">Humidifier type.</param>
    /// <param name="saFan">Supply air fan.</param>
    /// <param name="raFan">Return air fan.</param>
    public AirHandlingUnit
      (CrossFinHeatExchanger cCoil, CrossFinHeatExchanger hCoil,
      HumidifierType humidType, CentrifugalFan saFan, CentrifugalFan raFan) :
      this(cCoil, hCoil, humidType, saFan, raFan, null)
    { }

    /// <summary>Initializes a new instance.</summary>
    /// <param name="cCoil">Cooling/dehumidifying coil.</param>
    /// <param name="hCoil">Heating coil.</param>
    /// <param name="humidType">Humidifier type.</param>
    /// <param name="saFan">Supply air fan.</param>
    /// <param name="raFan">Return air fan.</param>
    /// <param name="regenerator">Rotary heat recovery wheel.</param>
    public AirHandlingUnit
      (CrossFinHeatExchanger cCoil, CrossFinHeatExchanger hCoil, HumidifierType humidType,
      CentrifugalFan saFan, CentrifugalFan raFan, RotaryRegenerator? regenerator)
    {
      this.cCoil = cCoil;
      this.hCoil = hCoil;
      this.Humidifier = humidType;
      this.saFan = saFan;
      this.raFan = raFan;
      this.regen = regenerator;

      switch (humidType)
      {
        case HumidifierType.WettedMedia:
          WaterSupplyCoefficient = 0.5;
          MaxSaturationEfficiency = 0.8;
          WaterSupplyCoefficient = 0.5;
          break;
        case HumidifierType.Steam:
          WaterSupplyCoefficient = 0.9;
          MaxSaturationEfficiency = 1.0;
          WaterSupplyCoefficient = 0.9;
          break;
        case HumidifierType.Ultrasonic:
          WaterSupplyCoefficient = 0.9;
          MaxSaturationEfficiency = 0.5;
          WaterSupplyCoefficient = 0.9;
          break;
        case HumidifierType.Atomizing:
          WaterSupplyCoefficient = 0.4;
          MaxSaturationEfficiency = 0.3;
          WaterSupplyCoefficient = 0.4;
          break;
      }
    }

    #endregion

    #region 冷却運転

    /// <summary>Cools the supply air in free-running mode (no outlet temperature control).</summary>
    public void CoolAir() { CoolAir_Internal(false, 0, 0); }
    
    /// <summary>Cools the supply air with outlet temperature control.</summary>
    /// <param name="setpointTemperature">Supply air temperature setpoint [°C].</param>
    /// <param name="setpointHumidity">Supply air humidity ratio setpoint [kg/kg].</param>
    /// <returns>True if control to the setpoint was achieved.</returns>
    /// <remarks>The humidity ratio setpoint is used for economiser control. Not required when temperature-based control is used.</remarks>
    public bool CoolAir(double setpointTemperature, double setpointHumidity)
    { return CoolAir_Internal(true, setpointTemperature, setpointHumidity); }

    /// <summary>Cools and dehumidifies the supply air.</summary>
    /// <param name="controlOutletTemp">True to control the AHU outlet temperature.</param>
    /// <param name="spTemp">Supply air temperature setpoint [°C].</param>
    /// <param name="spHumid">Supply air humidity ratio setpoint [kg/kg].</param>
    /// <returns>True if control to the setpoint was achieved.</returns>
    /// <remarks>The humidity ratio setpoint is used for economiser control. Not required when temperature-based control is used.</remarks>
    private bool CoolAir_Internal(bool controlOutletTemp, double spTemp, double spHumid)
    {
      //SA風量が0の場合は停止処理（全外気でRA風量=0はありえる）
      if (SAFlowRate <= 0)
      {
        ShutOff();
        return false;
      }      

      //ファン昇温を計算
      saFan.UpdateState(SAFlowRate / PhysicsConstants.NominalMoistAirDensity);
      raFan.UpdateState(RAFlowRate / PhysicsConstants.NominalMoistAirDensity);
      double tRise = saFan.GetElectricConsumption() / (SAFlowRate * PhysicsConstants.NominalMoistAirIsobaricSpecificHeat);

      //ダクト熱損失とファン昇温を考慮して出口温度設定を補正
      double sp2 = Math.Max(LowerTemperatureLimit_C, spTemp);
      double tdCo = (sp2 - RATemperature * DuctHeatLossRate) / (1 - DuctHeatLossRate) - tRise;

      //外気冷房を反映した外気量調整//成り行き計算の場合は外気量は所与
      if (controlOutletTemp)
      {
        double mOA = MinOAFlowRate;
        if (OutdoorAirCooling == OutdoorAirCoolingControl.DryBulbTemperature)
        {
          if (tdCo < OATemperature && OATemperature < RATemperature)
            mOA = Math.Min(MaxOAFlowRate, SAFlowRate);
          else mOA = RAFlowRate * (RATemperature - tdCo) / (tdCo - OATemperature);
        }
        else if (OutdoorAirCooling == OutdoorAirCoolingControl.HumidityRatio)
        {
          if (spHumid < OAHumidityRatio && OAHumidityRatio < RAHumidityRatio)
            mOA = Math.Min(MaxOAFlowRate, SAFlowRate);
          else mOA = RAFlowRate * (RAHumidityRatio - spHumid) / (spHumid - OAHumidityRatio);
        }
        else if (OutdoorAirCooling == OutdoorAirCoolingControl.Enthalpy)
        {
          double hAHUi =
            MoistAir.GetEnthalpyFromDryBulbTemperatureAndHumidityRatio(RATemperature, RAHumidityRatio);
          double hAHUo =
            MoistAir.GetEnthalpyFromDryBulbTemperatureAndHumidityRatio(tdCo, spHumid);
          double hOA =
            MoistAir.GetEnthalpyFromDryBulbTemperatureAndHumidityRatio(OATemperature, OAHumidityRatio);
          if (hAHUo < hOA && hOA < hAHUi) mOA = Math.Min(MaxOAFlowRate, SAFlowRate);
          else mOA = RAFlowRate * (hAHUi - hAHUo) / (hAHUo - hOA);
        } 
        OAFlowRate = Math.Max(MinOAFlowRate, Math.Min(MaxOAFlowRate, mOA));
        EAFlowRate = RAFlowRate + OAFlowRate - SAFlowRate;
      }

      //全熱交換器による熱回収
      double tdOA = OATemperature;
      double hrOA = OAHumidityRatio;
      if (regen != null && !BypassRegenerator && 0 < OAFlowRate && 0 < EAFlowRate)
      {
        double hRtn = 
          MoistAir.GetEnthalpyFromDryBulbTemperatureAndHumidityRatio(RATemperature, RAHumidityRatio);
        double hOA =
          MoistAir.GetEnthalpyFromDryBulbTemperatureAndHumidityRatio(OATemperature, OAHumidityRatio);
        if (hRtn < hOA)
        {
          const double cf = 3600 / PhysicsConstants.NominalMoistAirDensity;
          //成り行き計算を試行
          regen.UpdateState(OAFlowRate * cf, EAFlowRate * cf, 1.0, tdOA, hrOA, RATemperature, RAHumidityRatio);
          //過剰処理の場合には給気温度を制御
          if (controlOutletTemp && regen.SupplyAirOutletDryBulbTemperature < tdCo)
            regen.ControlOutletTemperature
              (OAFlowRate * cf, EAFlowRate * cf, 1.0, tdOA, hrOA, RATemperature, RAHumidityRatio, tdCo);
          tdOA = regen.SupplyAirOutletDryBulbTemperature;
          hrOA = regen.SupplyAirOutletHumidityRatio;
        }
        else regen.ShutOff();
      }

      //OAとRAを混合して冷却
      double mr = OAFlowRate / (OAFlowRate + RAFlowRate);
      double tdCi = tdOA * mr + RATemperature * (1 - mr);
      double hrCi = hrOA * mr + RAHumidityRatio * (1 - mr);
      if (controlOutletTemp)
        cCoil.ControlOutletAirTemperature(tdCi, hrCi, ChilledWaterInletTemperature, SAFlowRate, tdCo);
      else
        cCoil.UpdateOutletState(tdCi, hrCi, ChilledWaterInletTemperature, SAFlowRate, cCoil.WaterFlowRate);
      hCoil.ShutOff();
      WaterConsumption = SteamConsumption = 0.0;

      //SAファンによる昇温とダクト熱損失効果を反映
      SATemperature = cCoil.OutletAirTemperature + tRise;
      SAHumidityRatio = cCoil.OutletAirHumidityRatio;
      ComputeDuctLoss();

      return -1e-4 < spTemp - SATemperature;
    }

    /// <summary>Computes the heat removed by outdoor air economiser cooling [kW].</summary>
    /// <returns>Heat removed by outdoor air economiser cooling [kW].</returns>
    public double GetOutdoorCoolingHeat()
    {
      double hSA = MoistAir.GetEnthalpyFromDryBulbTemperatureAndHumidityRatio
        (cCoil.InletAirTemperature, cCoil.InletAirHumidityRatio);
      double hOA = MoistAir.GetEnthalpyFromDryBulbTemperatureAndHumidityRatio
        (OATemperature, OAHumidityRatio);
      return (OAFlowRate - MinOAFlowRate) * (hSA - hOA);
    }

    /// <summary>Applies duct heat loss to the supply air state.</summary>
    private void ComputeDuctLoss()
    {
      double dl1 = 1 - DuctHeatLossRate;
      SATemperature = SATemperature * dl1 + RATemperature * DuctHeatLossRate;
      SAHumidityRatio = SAHumidityRatio * dl1 + RAHumidityRatio * DuctHeatLossRate;
    }

    #endregion

    #region 加熱運転

    /// <summary>Heats and humidifies the supply air in free-running mode.</summary>
    public void HeatAir() { HeatAir_Internal(false, 0, 0); } //これだと蒸気加湿の場合に全く加湿されない!!! DEBUG

    /// <summary>Heats and humidifies the supply air with outlet temperature/humidity control.</summary>
    /// <param name="setpointTemperature">Outlet air temperature setpoint [°C].</param>
    /// <param name="setpointHumidity">Outlet air humidity ratio setpoint [kg/kg].</param>
    /// <returns>True if heating to the setpoint was achieved.</returns>
    public bool HeatAir(double setpointTemperature, double setpointHumidity)
    { return HeatAir_Internal(true, setpointTemperature, setpointHumidity); }

    /// <summary>Heats and humidifies the supply air.</summary>
    /// <param name="controlOutletState">True to control the supply air outlet state.</param>
    /// <param name="spTemp">Supply air temperature setpoint [°C].</param>
    /// <param name="spHumid">Supply air humidity ratio setpoint [kg/kg].</param>
    /// <returns>True if control to the setpoint was achieved.</returns>
    private bool HeatAir_Internal(bool controlOutletState, double spTemp, double spHumid)
    {
      //SA風量が0の場合は停止処理（全外気でRA風量=0はありえる）
      if (SAFlowRate <= 0)
      {
        ShutOff();
        return false;
      }

      //ファン昇温を計算
      saFan.UpdateState(SAFlowRate / PhysicsConstants.NominalMoistAirDensity);
      raFan.UpdateState(RAFlowRate / PhysicsConstants.NominalMoistAirDensity);
      double tRise = saFan.GetElectricConsumption() / (SAFlowRate * PhysicsConstants.NominalMoistAirIsobaricSpecificHeat);

      //ダクト熱損失とファン昇温を考慮して出口温湿度設定を補正
      double sp2 = Math.Min(UpperTemperatureLimit_H, spTemp);
      double tdCo = (sp2 - RATemperature * DuctHeatLossRate) / (1 - DuctHeatLossRate) - tRise;
      double wAHUo = (spHumid - RAHumidityRatio * DuctHeatLossRate) / (1 - DuctHeatLossRate);
      double hCo = MoistAir.GetEnthalpyFromDryBulbTemperatureAndHumidityRatio(tdCo, wAHUo);

      //外気量は最小値
      //double mOA = MinOAFlowRate;
      OAFlowRate = MinOAFlowRate;
      EAFlowRate = RAFlowRate + OAFlowRate - SAFlowRate;

      //全熱交換器による熱回収
      double tdOA = OATemperature;
      double hrOA = OAHumidityRatio;
      if (regen != null && !BypassRegenerator && 0 < OAFlowRate && 0 < EAFlowRate)
      {
        double hRtn =
          MoistAir.GetEnthalpyFromDryBulbTemperatureAndHumidityRatio(RATemperature, RAHumidityRatio);
        double hOA =
          MoistAir.GetEnthalpyFromDryBulbTemperatureAndHumidityRatio(OATemperature, OAHumidityRatio);
        if (hOA < hRtn)
        {
          const double cf = 3600 / PhysicsConstants.NominalMoistAirDensity;
          //成り行き計算実行
          regen.UpdateState(OAFlowRate * cf, EAFlowRate * cf, 1.0, tdOA, hrOA, RATemperature, RAHumidityRatio);
          //過剰処理の場合には給気比エンタルピーを制御
          double hRego = MoistAir.GetEnthalpyFromDryBulbTemperatureAndHumidityRatio
            (regen.SupplyAirOutletDryBulbTemperature, regen.SupplyAirOutletHumidityRatio);
          if (controlOutletState && hCo < hRego)
            regen.ControlOutletEnthalpy
              (OAFlowRate * cf, EAFlowRate * cf, 1.0, tdOA, hrOA, RATemperature, RAHumidityRatio, hCo);
          tdOA = regen.SupplyAirOutletDryBulbTemperature;
          hrOA = regen.SupplyAirOutletHumidityRatio;
        }
        else regen.ShutOff();
      }

      //OAとRAを混合
      double mr = OAFlowRate / (OAFlowRate + RAFlowRate);
      double tdCi = tdOA * mr + RATemperature * (1 - mr);
      double hrCi = hrOA * mr + RAHumidityRatio * (1 - mr);

      //水加湿の場合には温水コイル出口温度を比エンタルピーで調整
      if (hrCi < wAHUo &&
        (Humidifier != HumidifierType.None && Humidifier != HumidifierType.Steam))
        tdCo = MoistAir.GetDryBulbTemperatureFromHumidityRatioAndEnthalpy(hrCi, hCo);

      //加熱
      if (controlOutletState)
        hCoil.ControlOutletAirTemperature(tdCi, hrCi, HotWaterInletTemperature, SAFlowRate, tdCo);
      else
        hCoil.UpdateOutletState(tdCi, hrCi, HotWaterInletTemperature, SAFlowRate, hCoil.WaterFlowRate);
      tdCo = hCoil.OutletAirTemperature;
      cCoil.ShutOff();

      //加湿
      double tSFi;
      tSFi = hCoil.OutletAirTemperature;
      WaterConsumption = SteamConsumption = 0.0;
      //水加湿
      if (Humidifier != HumidifierType.None && Humidifier != HumidifierType.Steam)
      {
        double hSFi =
          MoistAir.GetEnthalpyFromDryBulbTemperatureAndHumidityRatio(tdCo, hCoil.OutletAirHumidityRatio);
        double satW = MoistAir.GetSaturationHumidityRatioFromEnthalpy(hSFi, PhysicsConstants.StandardAtmosphericPressure);
        if (controlOutletState)
        {
          if (hCoil.OutletAirHumidityRatio < wAHUo)
          {
            double maxW = (1 - MaxSaturationEfficiency)
            * hCoil.OutletAirHumidityRatio + MaxSaturationEfficiency * satW;
            wAHUo = Math.Min(maxW, wAHUo);
            SaturationEfficiency = (wAHUo - hCoil.OutletAirHumidityRatio) / (satW - hCoil.OutletAirHumidityRatio);
          }
          else
          {
            wAHUo = hCoil.OutletAirHumidityRatio;
            SaturationEfficiency = 0.0;
          }
        }
        else wAHUo = (1 - SaturationEfficiency) * hCoil.OutletAirHumidityRatio + SaturationEfficiency * satW;
        tSFi = MoistAir.GetDryBulbTemperatureFromHumidityRatioAndEnthalpy(wAHUo, hSFi);
        WaterConsumption = (wAHUo - hCoil.OutletAirHumidityRatio) * SAFlowRate / WaterSupplyCoefficient;
      }
      //蒸気加湿//最大飽和効率=100%
      else if (Humidifier == HumidifierType.Steam)
      {
        double satW = MoistAir.GetSaturationHumidityRatioFromDryBulbTemperature
          (hCoil.OutletAirTemperature, PhysicsConstants.StandardAtmosphericPressure);
        wAHUo = Math.Min(satW, wAHUo);
        SteamConsumption = (wAHUo - hCoil.OutletAirHumidityRatio) * SAFlowRate / WaterSupplyCoefficient;
      }

      //SAファンによる昇温とダクト熱損失効果を反映
      SATemperature = tSFi + tRise;
      SAHumidityRatio = wAHUo;
      ComputeDuctLoss();

      return -1e-4 < SATemperature - spTemp;
    }

    #endregion

    #region 換気運転

    /// <summary>Operates the AHU in ventilation mode (no heating/cooling, outdoor air only).</summary>
    public void Ventilate()
    {
      //SA風量が0の場合は停止処理（全外気でRA風量=0はありえる）
      if (SAFlowRate <= 0)
      {
        ShutOff();
        return;
      }

      //ファン昇温とダクト熱損失の計算
      saFan.UpdateState(SAFlowRate / PhysicsConstants.NominalMoistAirDensity);
      raFan.UpdateState(SAFlowRate / PhysicsConstants.NominalMoistAirDensity);
      double tRise = saFan.GetElectricConsumption() / (SAFlowRate * PhysicsConstants.NominalMoistAirIsobaricSpecificHeat);
      SATemperature = RATemperature + tRise * (1 - DuctHeatLossRate);
      SAHumidityRatio = RAHumidityRatio;
    }

    #endregion

    #region その他インスタンスメソッド

    /// <summary>Sets the supply and return air flow rates [kg/s].</summary>
    /// <param name="raFlowRate">Return air flow rate [kg/s].</param>
    /// <param name="saFlowRate">Supply air flow rate [kg/s].</param>
    public void SetAirFlowRate(double raFlowRate, double saFlowRate)
    {
      RAFlowRate = Math.Max(0, raFlowRate);
      SAFlowRate = Math.Max(0, saFlowRate);
      EAFlowRate = RAFlowRate + OAFlowRate - SAFlowRate;
    }

    /// <summary>Sets the outdoor air flow rate range.</summary>
    /// <param name="minOAFlow">Minimum outdoor air flow rate [kg/s].</param>
    /// <param name="maxOAFlow">Maximum outdoor air flow rate [kg/s].</param>
    public void SetOutdoorAirFlowRange(double minOAFlow, double maxOAFlow)
    {
      MinOAFlowRate = minOAFlow;
      MaxOAFlowRate = maxOAFlow;
      OAFlowRate = Math.Min(Math.Max(OAFlowRate, MinOAFlowRate), MaxOAFlowRate);
      EAFlowRate = RAFlowRate + OAFlowRate - SAFlowRate;
    }    

    /// <summary>Shuts off the AHU (zero airflow, fans stopped).</summary>
    public void ShutOff()
    {
      cCoil.ShutOff();
      hCoil.ShutOff();
      saFan.ShutOff();
      raFan.ShutOff();
      if (regen != null) regen.ShutOff();
      WaterConsumption = 0.0;
      OAFlowRate = 0;
      SetAirFlowRate(0, 0);
    }
    
    /// <summary>Computes the VAV airflow rates for each zone.</summary>
    /// <param name="isCooling">True for cooling mode; false for heating mode.</param>
    /// <param name="supplyHumiditySP">Supply air humidity ratio setpoint [kg/kg] (used in heating mode).</param>
    /// <param name="isVAVShutOff">Array of VAV shut-off flags per zone.</param>
    /// <param name="zoneTemps">Zone dry-bulb temperatures [°C].</param>
    /// <param name="zoneHumids">Zone humidity ratios [kg/kg].</param>
    /// <param name="zoneSLoads">Zone sensible heat loads [kW] (positive = heating required).</param>
    /// <param name="minSAFlow">Minimum supply air flow rate [kg/s].</param>
    /// <param name="maxSAFlow">Maximum supply air flow rate [kg/s].</param>
    /// <param name="maxRAFlow">Maximum return air flow rate [kg/s].</param>
    /// <param name="success">True if the control target was achieved.</param>
    /// <returns>VAV airflow rates per zone [kg/s].</returns>
    public double[] OptimizeVAV
      (bool isCooling, double supplyHumiditySP, bool[] isVAVShutOff, double[] zoneTemps, double[] zoneHumids, 
      double[] zoneSLoads, double[] minSAFlow, double[] maxSAFlow, double[] maxRAFlow, out bool success)
    {
      //AHU吹出温度の上下限値を計算
      double tAHUoMin, tAHUoMax;
      if (isCooling)
      {
        if (MinimizeAirFlow) tAHUoMin = 60;
        else tAHUoMin = -10;
        tAHUoMax = 60;
      }
      else
      {
        if (MinimizeAirFlow) tAHUoMin = -10;
        else tAHUoMin = 60;
        tAHUoMax = -10;
      }
      for (int i = 0; i < zoneTemps.Length; i++)
      {
        if (!isVAVShutOff[i])
        {
          //最大風量での給気温度とAHU吹出温度
          double tMax = zoneTemps[i] + zoneSLoads[i] / (PhysicsConstants.NominalMoistAirIsobaricSpecificHeat * maxSAFlow[i]);
          if (isCooling) tAHUoMax = Math.Min(tAHUoMax, tMax);
          else tAHUoMax = Math.Max(tAHUoMax, tMax);
          //最小風量での給気温度とAHU吹出温度          
          if (0 < minSAFlow[i])
          {
            double tMin = zoneTemps[i] + zoneSLoads[i] / (PhysicsConstants.NominalMoistAirIsobaricSpecificHeat * minSAFlow[i]);
            //過冷却と過加熱が生じても風量を最小化する
            if (MinimizeAirFlow)
            {
              if (isCooling) tAHUoMin = Math.Min(tAHUoMin, tMin);
              else tAHUoMin = Math.Max(tAHUoMin, tMin);
            }
            //過冷却と過加熱を発生させないように風量を絞る
            else
            {
              if (isCooling) tAHUoMin = Math.Max(tAHUoMin, tMin);
              else tAHUoMin = Math.Min(tAHUoMin, tMin);
            }
          }
        }
      }
      //給気制御範囲内に納める
      if (isCooling)
      {
        tAHUoMin = Math.Max(LowerTemperatureLimit_C, Math.Min(UpperTemperatureLimit_C, tAHUoMin));
        tAHUoMax = Math.Max(LowerTemperatureLimit_C, Math.Min(UpperTemperatureLimit_C, tAHUoMax));
      }
      else
      {
        tAHUoMin = Math.Max(LowerTemperatureLimit_H, Math.Min(UpperTemperatureLimit_H, tAHUoMin));
        tAHUoMax = Math.Max(LowerTemperatureLimit_H, Math.Min(UpperTemperatureLimit_H, tAHUoMax));
      }

      //評価関数を定義
      double mSAMax = 0;
      for (int i = 0; i < maxSAFlow.Length; i++) mSAMax += maxSAFlow[i];
      double[] mSAn = new double[zoneTemps.Length];
      bool suc = true;
      bool overLoad = true;
      Minimization.MinimizeFunction mFnc = delegate (double tAHUo)
      {
        //還気条件を計算
        double mSAsum = 0;
        double mRAsum = 0;
        RATemperature = 0;
        RAHumidityRatio = 0;
        suc = true;
        for (int i = 0; i < zoneTemps.Length; i++)
        {
          if (!isVAVShutOff[i])
          {
            if (tAHUo == zoneTemps[i]) mSAn[i] = 0;
            else mSAn[i] = zoneSLoads[i] / (PhysicsConstants.NominalMoistAirIsobaricSpecificHeat * (tAHUo - zoneTemps[i]));  //DEBUG
            if (1e-10 < minSAFlow[i] - mSAn[i] || 1e-10 < mSAn[i] - maxSAFlow[i]) suc = false;
            mSAn[i] = Math.Min(maxSAFlow[i], Math.Max(minSAFlow[i], mSAn[i]));
            double mRAn = mSAn[i] - (maxSAFlow[i] - maxRAFlow[i]);
            mSAsum += mSAn[i];
            mRAsum += mRAn;
            RATemperature += mRAn * zoneTemps[i];
            RAHumidityRatio += mRAn * zoneHumids[i];
          }
          else mSAn[i] = 0;
        }
        if (1e-7 < mRAsum)
        {
          RATemperature /= mRAsum;
          RAHumidityRatio /= mRAsum;
        }
        SetAirFlowRate(mRAsum, mSAsum);

        //誤差を評価
        if (isCooling) overLoad = !CoolAir(tAHUo, supplyHumiditySP);
        else
        {
          //必要な湿度を補正
          double bf = SAFlowRate / mSAMax;
          double hsp = RAHumidityRatio * (1 - bf) + supplyHumiditySP * bf;
          overLoad = !HeatAir(tAHUo, hsp);
        }
        if (overLoad)
        {
          double err = Math.Abs(SATemperature - tAHUo);
          if (isCooling) return RATemperature + err;
          else return -RATemperature + err;
        }
        else
        {
          if (isCooling) return SATemperature;
          else return -SATemperature;
        }
      };

      //最大風量で計算
      mFnc(tAHUoMax);
      //冷暖逆転の場合には最小風量
      if ((isCooling && RATemperature < tAHUoMax) || (!isCooling && tAHUoMax < RATemperature)) mFnc(tAHUoMin);
      //最大風量で過負荷の場合には最大風量
      else if (!overLoad)
      {
        //最小風量でAHUが過負荷になるならば処理可能な風量まで修正
        mFnc(tAHUoMin);
        if (overLoad)
        {
          if (isCooling) Minimization.GoldenSection(ref tAHUoMin, tAHUoMax, mFnc);
          else Minimization.GoldenSection(ref tAHUoMax, tAHUoMin, mFnc);
        }
      }
      success = suc;
      return mSAn;
    }

    #endregion

  }

}
