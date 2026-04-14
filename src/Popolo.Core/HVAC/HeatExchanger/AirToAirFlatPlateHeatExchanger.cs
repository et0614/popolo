/* AirToAirFlatPlateHeatExchanger.cs
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

namespace Popolo.Core.HVAC.HeatExchanger
{

  /// <summary>Air-to-air fixed-plate heat exchanger (sensible and total heat recovery).</summary>
  public class AirToAirFlatPlateHeatExchanger : IReadOnlyAirToAirFlatPlateHeatExchanger
  {

    #region 列挙型

    /// <summary>Air flow arrangement.</summary>
    public enum AirFlow
    {
      /// <summary>Counter-flow arrangement.</summary>
      CounterFlow,
      /// <summary>Cross-flow arrangement.</summary>
      CrossFlow
    }

    /// <summary>JIS test condition for initializing heat transfer coefficients.</summary>
    public enum Condition
    {
      /// <summary>JIS B 8628:2003, heating condition.</summary>
      JISB8628_2003_Heating,
      /// <summary>JIS B 8628:2017, heating condition.</summary>
      JISB8628_2017_Heating,
      /// <summary>JIS B 8628:2003, cooling condition.</summary>
      JISB8628_2003_Cooling,
      /// <summary>JIS B 8628:2017, cooling condition.</summary>
      JISB8628_2017_Cooling,
    }

    #endregion

    #region インスタンス変数

    /// <summary>Sensible heat transfer coefficient [kW/K].</summary>
    private double sensibleHeatTransferCoefficient;

    /// <summary>Latent heat transfer coefficient [kg/(kg/kg)].</summary>
    private double latentHeatTransferCoefficient;

    #endregion

    #region プロパティ

    /// <summary>Gets a value indicating whether this is a total heat exchanger (sensible + latent).</summary>
    public bool IsTotalHeatExchanger { get; private set; }

    /// <summary>Gets the supply air volumetric flow rate [m³/h].</summary>
    public double SupplyAirFlowVolume { get; private set; }

    /// <summary>Gets the exhaust air volumetric flow rate [m³/h].</summary>
    public double ExhaustAirFlowVolume { get; private set; }

    /// <summary>Gets the supply air inlet dry-bulb temperature [°C].</summary>
    public double SupplyAirInletDrybulbTemperature { get; private set; }

    /// <summary>Gets the exhaust air inlet dry-bulb temperature [°C].</summary>
    public double ExhaustAirInletDrybulbTemperature { get; private set; }

    /// <summary>Gets the supply air outlet dry-bulb temperature [°C].</summary>
    public double SupplyAirOutletDrybulbTemperature { get; private set; }

    /// <summary>Gets the exhaust air outlet dry-bulb temperature [°C].</summary>
    public double ExhaustAirOutletDrybulbTemperature { get; private set; }

    /// <summary>Gets the supply air inlet humidity ratio [kg/kg].</summary>
    public double SupplyAirInletHumidityRatio { get; private set; }

    /// <summary>Gets the exhaust air inlet humidity ratio [kg/kg].</summary>
    public double ExhaustAirInletHumidityRatio { get; private set; }

    /// <summary>Gets the supply air outlet humidity ratio [kg/kg].</summary>
    public double SupplyAirOutletHumidityRatio { get; private set; }

    /// <summary>Gets the exhaust air outlet humidity ratio [kg/kg].</summary>
    public double ExhaustAirOutletHumidityRatio { get; private set; }

    /// <summary>Gets the sensible heat exchange efficiency [-].</summary>
    public double SensibleEfficiency { get; private set; }

    /// <summary>Gets the latent heat exchange efficiency [-].</summary>
    public double LatentEfficiency { get; private set; }

    /// <summary>Gets the air flow arrangement type.</summary>
    public AirFlow Flow { get; private set; }

    #endregion

    #region コンストラクタ

    /// <summary>Initializes a new instance.</summary>
    /// <param name="supplyAirFlowVolume">Supply air volumetric flow rate [m³/h].</param>
    /// <param name="exhaustAirFlowVolume">Exhaust air volumetric flow rate [m³/h].</param>
    /// <param name="inletSADrybulbTemperature">Supply air inlet dry-bulb temperature [°C].</param>
    /// <param name="inletSAHumidityRatio">Supply air inlet humidity ratio [kg/kg].</param>
    /// <param name="inletEADrybulbTemperature">Exhaust air inlet dry-bulb temperature [°C].</param>
    /// <param name="inletEAHumidityRatio">Exhaust air inlet humidity ratio [kg/kg].</param>
    /// <param name="sensibleEfficiency">Sensible heat exchange efficiency [-].</param>
    /// <param name="latentEfficiency">Latent heat exchange efficiency [-].</param>
    /// <param name="flow">Air flow arrangement type.</param>
    /// <param name="isTotalHeatExchanger">True for a total heat exchanger (sensible + latent); false for sensible only.</param>
    public AirToAirFlatPlateHeatExchanger
      (double supplyAirFlowVolume, double exhaustAirFlowVolume,
      double inletSADrybulbTemperature, double inletSAHumidityRatio,
      double inletEADrybulbTemperature, double inletEAHumidityRatio,
      double sensibleEfficiency, double latentEfficiency,
      AirFlow flow, bool isTotalHeatExchanger)
    {
      Initialize(
        supplyAirFlowVolume, exhaustAirFlowVolume, 
        inletSADrybulbTemperature, inletSAHumidityRatio,
        inletEADrybulbTemperature, inletEAHumidityRatio, 
        sensibleEfficiency, latentEfficiency,
        flow, isTotalHeatExchanger);
    }

    /// <summary>Initializes a new instance.</summary>
    /// <param name="supplyAirFlowVolume">Supply air volumetric flow rate [m³/h].</param>
    /// <param name="exhaustAirFlowVolume">Exhaust air volumetric flow rate [m³/h].</param>
    /// <param name="sensibleEfficiency">Sensible heat exchange efficiency [-].</param>
    /// <param name="latentEfficiency">Latent heat exchange efficiency [-].</param>
    /// <param name="flow">Air flow arrangement type.</param>
    /// <param name="condition">JIS test condition used to compute heat transfer coefficients.</param>
    public AirToAirFlatPlateHeatExchanger
      (double supplyAirFlowVolume, double exhaustAirFlowVolume, double sensibleEfficiency, double latentEfficiency, AirFlow flow, Condition condition):
      this(supplyAirFlowVolume, exhaustAirFlowVolume, sensibleEfficiency, latentEfficiency, flow, condition, false)
    { }

    /// <summary>Initializes a new instance.</summary>
    /// <param name="supplyAirFlowVolume">Supply air volumetric flow rate [m³/h].</param>
    /// <param name="exhaustAirFlowVolume">Exhaust air volumetric flow rate [m³/h].</param>
    /// <param name="sensibleEfficiency">Sensible heat exchange efficiency [-].</param>
    /// <param name="latentOrEnthalpyEfficiency">Latent or enthalpy-based heat exchange efficiency [-].</param>
    /// <param name="flow">Air flow arrangement type.</param>
    /// <param name="condition">JIS test condition used to compute heat transfer coefficients.</param>
    /// <param name="isEnthalpyEfficiency">True if the efficiency is defined as enthalpy-based rather than humidity-ratio-based.</param>
    public AirToAirFlatPlateHeatExchanger
      (double supplyAirFlowVolume, double exhaustAirFlowVolume, double sensibleEfficiency, double latentOrEnthalpyEfficiency, AirFlow flow, Condition condition, bool isEnthalpyEfficiency)
    {
      double saDB, saHmd, eaDB, eaHmd;
      switch (condition)
      {
        case Condition.JISB8628_2003_Cooling:
          saDB = 34.5;
          eaDB = 26.5;
          saHmd = 0.02627;
          eaHmd = 0.01402;
          break;
        case Condition.JISB8628_2003_Heating:
          saDB = 5.0;
          eaDB = 20.5;
          saHmd = 0.00350;
          eaHmd = 0.00894;
          break;
        case Condition.JISB8628_2017_Cooling:
          saDB = 35.0;
          eaDB = 27.0;
          saHmd = 0.02715;
          eaHmd = 0.01178;
          break;
        case Condition.JISB8628_2017_Heating:
          saDB = 5.0;
          eaDB = 20.0;
          saHmd = 0.00387;
          eaHmd = 0.00857;
          break;
        default:
          throw new NotImplementedException();
      }

      //エンタルピー交換効率が与えられた場合には潜熱交換効率に変換
      if (isEnthalpyEfficiency)
      {
        double tsao = saDB - sensibleEfficiency * (saDB - eaDB);
        double saH = MoistAir.GetEnthalpyFromDryBulbTemperatureAndHumidityRatio(saDB, saHmd);
        double eaH = MoistAir.GetEnthalpyFromDryBulbTemperatureAndHumidityRatio(eaDB, eaHmd);
        double hsao = saH - latentOrEnthalpyEfficiency * (saH - eaH);
        double hmd = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndEnthalpy(tsao, hsao);
        latentOrEnthalpyEfficiency = (saHmd - hmd) / (saHmd - eaHmd);
      }
      
      Initialize(supplyAirFlowVolume, exhaustAirFlowVolume, saDB, saHmd, eaDB, eaHmd, sensibleEfficiency, latentOrEnthalpyEfficiency, flow, true);
    }

    /// <summary>Initializes a new instance.</summary>
    /// <param name="supplyAirFlowVolume">Supply air volumetric flow rate [m³/h].</param>
    /// <param name="exhaustAirFlowVolume">Exhaust air volumetric flow rate [m³/h].</param>
    /// <param name="inletSADrybulbTemperature">Supply air inlet dry-bulb temperature [°C].</param>
    /// <param name="inletSAHumidityRatio">Supply air inlet humidity ratio [kg/kg].</param>
    /// <param name="inletEADrybulbTemperature">Exhaust air inlet dry-bulb temperature [°C].</param>
    /// <param name="inletEAHumidityRatio">Exhaust air inlet humidity ratio [kg/kg].</param>
    /// <param name="sensibleEfficiency">Sensible heat exchange efficiency [-].</param>
    /// <param name="latentEfficiency">Latent heat exchange efficiency [-].</param>
    /// <param name="flow">Air flow arrangement type.</param>
    /// <param name="isTotalHeatExchanger">True for a total heat exchanger (sensible + latent); false for sensible only.</param>
    private void Initialize
      (double supplyAirFlowVolume, double exhaustAirFlowVolume,
      double inletSADrybulbTemperature, double inletSAHumidityRatio,
      double inletEADrybulbTemperature, double inletEAHumidityRatio,
      double sensibleEfficiency, double latentEfficiency,
      AirFlow flow, bool isTotalHeatExchanger)
    {
      //機器情報を保存
      IsTotalHeatExchanger = isTotalHeatExchanger;
      Flow = flow;

      //空気の質量流量を計算
      double svSA = MoistAir.GetSpecificVolumeFromDryBulbTemperatureAndHumidityRatio
        (inletSADrybulbTemperature, inletSAHumidityRatio, PhysicsConstants.StandardAtmosphericPressure);
      double svEA = MoistAir.GetSpecificVolumeFromDryBulbTemperatureAndHumidityRatio
        (inletEADrybulbTemperature, inletEAHumidityRatio, PhysicsConstants.StandardAtmosphericPressure);
      double mSA = supplyAirFlowVolume / (3600 * svSA);
      double mEA = exhaustAirFlowVolume / (3600 * svEA);

      //顕熱貫流率を計算
      sensibleHeatTransferCoefficient = GetSensibleHeatTransferCoefficient
        (mSA, mEA, inletSADrybulbTemperature, inletSAHumidityRatio,
        inletEADrybulbTemperature, inletEAHumidityRatio, sensibleEfficiency, flow);

      //全熱交換器の場合には潜熱貫流率[kg/(kg/kg)]を計算
      if (IsTotalHeatExchanger)
      {
        latentHeatTransferCoefficient = GetLatentHeatTransferCoefficient
          (mSA, mEA, inletSAHumidityRatio, inletEAHumidityRatio, latentEfficiency, flow);
      }

      //定格条件で成り行き計算
      UpdateState(supplyAirFlowVolume, exhaustAirFlowVolume, inletSADrybulbTemperature,
        inletSAHumidityRatio, inletEADrybulbTemperature, inletEAHumidityRatio);
    }

    #endregion

    #region インスタンスメソッド

    /// <summary>Updates the outlet conditions from the given inlet conditions.</summary>
    /// <param name="supplyAirFlowVolume">Supply air volumetric flow rate [m³/h].</param>
    /// <param name="exhaustAirFlowVolume">Exhaust air volumetric flow rate [m³/h].</param>
    /// <param name="inletSADrybulbTemperature">Supply air inlet dry-bulb temperature [°C].</param>
    /// <param name="inletSAHumidityRatio">Supply air inlet humidity ratio [kg/kg].</param>
    /// <param name="inletEADrybulbTemperature">Exhaust air inlet dry-bulb temperature [°C].</param>
    /// <param name="inletEAHumidityRatio">Exhaust air inlet humidity ratio [kg/kg].</param>
    public void UpdateState
      (double supplyAirFlowVolume, double exhaustAirFlowVolume,
      double inletSADrybulbTemperature, double inletSAHumidityRatio,
      double inletEADrybulbTemperature, double inletEAHumidityRatio)
    {
      //風量を保存
      SupplyAirFlowVolume = supplyAirFlowVolume;
      ExhaustAirFlowVolume = exhaustAirFlowVolume;

      //入口空気状態を保存
      SupplyAirInletDrybulbTemperature = inletSADrybulbTemperature;
      SupplyAirInletHumidityRatio = inletSAHumidityRatio;
      ExhaustAirInletDrybulbTemperature = inletEADrybulbTemperature;
      ExhaustAirInletHumidityRatio = inletEAHumidityRatio;

      //風量が0の場合
      if (supplyAirFlowVolume <= 0 || exhaustAirFlowVolume <= 0)
      {
        SensibleEfficiency = 0;
        SupplyAirOutletDrybulbTemperature = SupplyAirInletDrybulbTemperature;
        ExhaustAirOutletDrybulbTemperature = ExhaustAirInletDrybulbTemperature;
        SupplyAirOutletHumidityRatio = SupplyAirInletHumidityRatio;
        ExhaustAirOutletHumidityRatio = ExhaustAirInletHumidityRatio;
        return;
      }

      //空気の質量流量を計算
      double svSA = MoistAir.GetSpecificVolumeFromDryBulbTemperatureAndHumidityRatio
        (inletSADrybulbTemperature, inletSAHumidityRatio, PhysicsConstants.StandardAtmosphericPressure);
      double svEA = MoistAir.GetSpecificVolumeFromDryBulbTemperatureAndHumidityRatio
        (inletEADrybulbTemperature, inletEAHumidityRatio, PhysicsConstants.StandardAtmosphericPressure);
      double mSA = supplyAirFlowVolume / (3600 * svSA);
      double mEA = exhaustAirFlowVolume / (3600 * svEA);

      //熱通過有効度[-]を計算
      double effectiveness, mcMin, capacityRate;
      bool isMcMinSA;
      GetSensibleEffectiveness
        (mSA, mEA, inletSADrybulbTemperature, inletSAHumidityRatio,
        inletEADrybulbTemperature, inletEAHumidityRatio, sensibleHeatTransferCoefficient, Flow,
        out effectiveness, out mcMin, out capacityRate, out isMcMinSA);

      //熱交換効率[-]を計算
      double eff2;
      if (isMcMinSA)
      {
        SensibleEfficiency = effectiveness;
        eff2 = effectiveness * capacityRate;
      }
      else
      {
        SensibleEfficiency = effectiveness * capacityRate;
        eff2 = effectiveness;
      }

      //出口空気状態の計算
      SupplyAirOutletDrybulbTemperature =
        SupplyAirInletDrybulbTemperature - SensibleEfficiency *
        (SupplyAirInletDrybulbTemperature - ExhaustAirInletDrybulbTemperature);
      ExhaustAirOutletDrybulbTemperature = ExhaustAirInletDrybulbTemperature -
        eff2 * (ExhaustAirInletDrybulbTemperature - SupplyAirInletDrybulbTemperature);

      //水分交換
      if (IsTotalHeatExchanger)
      {
        //熱通過有効度[-]を計算
        GetLatentEffectiveness
          (mSA, mEA, inletSAHumidityRatio, inletEAHumidityRatio,
          latentHeatTransferCoefficient, Flow,
          out effectiveness, out mcMin, out capacityRate);

        //熱交換効率[-]を計算
        if (mcMin == mSA)
        {
          LatentEfficiency = effectiveness;
          eff2 = effectiveness * capacityRate;
        }
        else
        {
          LatentEfficiency = effectiveness * capacityRate;
          eff2 = effectiveness;
        }

        SupplyAirOutletHumidityRatio = SupplyAirInletHumidityRatio -
          LatentEfficiency * (SupplyAirInletHumidityRatio - ExhaustAirInletHumidityRatio);
        ExhaustAirOutletHumidityRatio = ExhaustAirInletHumidityRatio -
          eff2 * (ExhaustAirInletHumidityRatio - SupplyAirInletHumidityRatio);
      }
      else
      {
        SupplyAirOutletHumidityRatio = SupplyAirInletHumidityRatio;
        ExhaustAirOutletHumidityRatio = ExhaustAirInletHumidityRatio;
      }
    }

    /// <summary>Computes the total heat exchange efficiency [-] from sensible and latent effectivenesses.</summary>
    /// <returns>Total heat exchange efficiency [-].</returns>
    public double GetTotalEfficiency()
    {
      //空気の出入口エンタルピーの計算
      double hSAi = MoistAir.GetEnthalpyFromDryBulbTemperatureAndHumidityRatio
        (SupplyAirInletDrybulbTemperature, SupplyAirInletHumidityRatio);
      double hSAo = MoistAir.GetEnthalpyFromDryBulbTemperatureAndHumidityRatio
        (SupplyAirOutletDrybulbTemperature, SupplyAirOutletHumidityRatio);
      double hEAi = MoistAir.GetEnthalpyFromDryBulbTemperatureAndHumidityRatio
        (ExhaustAirInletDrybulbTemperature, ExhaustAirInletHumidityRatio);

      return (hSAi - hSAo) / (hSAi - hEAi);
    }

    #endregion

    #region staticメソッド

    /// <summary>Computes the sensible heat transfer coefficient [kW/K] from rated conditions.</summary>
    /// <param name="supplyAirMassFlowRate">Supply air mass flow rate [kg/s].</param>
    /// <param name="exhaustAirMassFlowRate">Exhaust air mass flow rate [kg/s].</param>
    /// <param name="supplyAirDrybulbTemperature">Supply air dry-bulb temperature [°C].</param>
    /// <param name="supplyAirHumidityRatio">Supply air humidity ratio [kg/kg].</param>
    /// <param name="exhaustAirDrybulbTemperature">Exhaust air dry-bulb temperature [°C].</param>
    /// <param name="exhaustAirHumitidyRatio">Exhaust air humidity ratio [kg/kg].</param>
    /// <param name="efficiency">Sensible heat exchange efficiency [-].</param>
    /// <param name="flow">Air flow arrangement type.</param>
    /// <returns>Sensible heat transfer coefficient [kW/K].</returns>
    public static double GetSensibleHeatTransferCoefficient
      (double supplyAirMassFlowRate, double exhaustAirMassFlowRate,
      double supplyAirDrybulbTemperature, double supplyAirHumidityRatio,
      double exhaustAirDrybulbTemperature, double exhaustAirHumitidyRatio,
      double efficiency, AirFlow flow)
    {
      //熱容量流量[kW/K]の計算
      double cpSA = MoistAir.GetSpecificHeat(supplyAirHumidityRatio);
      double cpEA = MoistAir.GetSpecificHeat(exhaustAirHumitidyRatio);
      double mcSA = supplyAirMassFlowRate * cpSA;
      double mcEA = exhaustAirMassFlowRate * cpEA;
      double mcMin = Math.Min(mcSA, mcEA);

      //熱容量流量比[-]の計算
      double capacityRatio = mcMin / Math.Max(mcSA, mcEA);

      //熱通過有効度[-]を計算
      double effectiveness;
      if (mcSA < mcEA) effectiveness = efficiency;
      else effectiveness = efficiency / capacityRatio;

      //移動単位数を計算
      HeatExchange.FlowType fType;
      if (flow == AirFlow.CounterFlow) fType = HeatExchange.FlowType.CounterFlow;
      else fType = HeatExchange.FlowType.CrossFlow_BothFluidsUnmixed;
      double ntu = HeatExchange.GetNTU(effectiveness, capacityRatio, fType);

      return ntu * mcMin;
    }

    /// <summary>Computes the latent heat transfer coefficient [kg/(kg/kg)] from rated conditions.</summary>
    /// <param name="supplyAirMassFlowRate">Supply air mass flow rate [kg/s].</param>
    /// <param name="exhaustAirMassFlowRate">Exhaust air mass flow rate [kg/s].</param>
    /// <param name="supplyAirHumidityRatio">Supply air humidity ratio [kg/kg].</param>
    /// <param name="exhaustAirHumitidyRatio">Exhaust air humidity ratio [kg/kg].</param>
    /// <param name="efficiency">Latent heat exchange efficiency [-].</param>
    /// <param name="flow">Air flow arrangement type.</param>
    /// <returns>Latent heat transfer coefficient [kg/(kg/kg)].</returns>
    public static double GetLatentHeatTransferCoefficient
      (double supplyAirMassFlowRate, double exhaustAirMassFlowRate,
      double supplyAirHumidityRatio, double exhaustAirHumitidyRatio,
      double efficiency, AirFlow flow)
    {
      //質量流量比[-]の計算
      double mcMin = Math.Min(supplyAirMassFlowRate, exhaustAirMassFlowRate);
      double massFlowRatio = mcMin / Math.Max
        (supplyAirMassFlowRate, exhaustAirMassFlowRate);

      //熱通過有効度[-]を計算
      double effectiveness;
      if (supplyAirMassFlowRate < exhaustAirMassFlowRate) effectiveness = efficiency;
      else effectiveness = efficiency / massFlowRatio;

      //移動単位数を計算
      HeatExchange.FlowType fType;
      if (flow == AirFlow.CounterFlow) fType = HeatExchange.FlowType.CounterFlow;
      else fType = HeatExchange.FlowType.CrossFlow_BothFluidsUnmixed;
      double ntu = HeatExchange.GetNTU(effectiveness, massFlowRatio, fType);

      return ntu * mcMin;
    }

    /// <summary>Computes the sensible heat transfer effectiveness [-].</summary>
    /// <param name="supplyAirMassFlowRate">Supply air mass flow rate [kg/s].</param>
    /// <param name="exhaustAirMassFlowRate">Exhaust air mass flow rate [kg/s].</param>
    /// <param name="supplyAirDrybulbTemperature">Supply air dry-bulb temperature [°C].</param>
    /// <param name="supplyAirHumidityRatio">Supply air humidity ratio [kg/kg].</param>
    /// <param name="exhaustAirDrybulbTemperature">Exhaust air dry-bulb temperature [°C].</param>
    /// <param name="exhaustAirHumitidyRatio">Exhaust air humidity ratio [kg/kg].</param>
    /// <param name="heatTransferCoefficient">Sensible heat transfer coefficient [kW/K].</param>
    /// <param name="flow">Air flow arrangement type.</param>
    /// <param name="effectiveness">Sensible heat transfer effectiveness [-].</param>
    /// <param name="mcMin">Smaller heat capacity rate [kW/K].</param>
    /// <param name="capacityRatio">Mass flow rate ratio (min/max) [-].</param>
    /// <param name="isMcMinSASide">True if the supply air side has the smaller heat capacity rate.</param>
    public static void GetSensibleEffectiveness
      (double supplyAirMassFlowRate, double exhaustAirMassFlowRate,
      double supplyAirDrybulbTemperature, double supplyAirHumidityRatio,
      double exhaustAirDrybulbTemperature, double exhaustAirHumitidyRatio,
      double heatTransferCoefficient, AirFlow flow, out double effectiveness,
      out double mcMin, out double capacityRatio, out bool isMcMinSASide)
    {
      //熱容量流量[kW/K]の計算
      double cpSA = MoistAir.GetSpecificHeat(supplyAirHumidityRatio);
      double cpEA = MoistAir.GetSpecificHeat(exhaustAirHumitidyRatio);
      double mcSA = supplyAirMassFlowRate * cpSA;
      double mcEA = exhaustAirMassFlowRate * cpEA;
      mcMin = Math.Min(mcSA, mcEA);
      isMcMinSASide = (mcSA == mcMin);

      //熱容量流量比[-]の計算
      capacityRatio = mcMin / Math.Max(mcSA, mcEA);

      //熱通過有効度[-]の計算
      double ntu = heatTransferCoefficient / mcMin;
      HeatExchange.FlowType fType;
      if (flow == AirFlow.CounterFlow) fType = HeatExchange.FlowType.CounterFlow;
      else fType = HeatExchange.FlowType.CrossFlow_BothFluidsUnmixed;
      effectiveness = HeatExchange.GetEffectiveness(ntu, capacityRatio, fType);
    }

    /// <summary>Computes the latent heat transfer effectiveness [-].</summary>
    /// <param name="supplyAirMassFlowRate">Supply air mass flow rate [kg/s].</param>
    /// <param name="exhaustAirMassFlowRate">Exhaust air mass flow rate [kg/s].</param>
    /// <param name="supplyAirHumidityRatio">Supply air humidity ratio [kg/kg].</param>
    /// <param name="exhaustAirHumitidyRatio">Exhaust air humidity ratio [kg/kg].</param>
    /// <param name="heatTransferCoefficient">Latent heat transfer coefficient [kg/(kg/kg)].</param>
    /// <param name="flow">Air flow arrangement type.</param>
    /// <param name="effectiveness">Latent heat transfer effectiveness [-].</param>
    /// <param name="mMin">Smaller mass flow rate [kg/s].</param>
    /// <param name="capacityRatio">Mass flow rate ratio (min/max) [-].</param>
    public static void GetLatentEffectiveness
     (double supplyAirMassFlowRate, double exhaustAirMassFlowRate,
     double supplyAirHumidityRatio, double exhaustAirHumitidyRatio,
     double heatTransferCoefficient, AirFlow flow, out double effectiveness,
     out double mMin, out double capacityRatio)
    {
      //質量流量比[-]の計算
      mMin = Math.Min(supplyAirMassFlowRate, exhaustAirMassFlowRate);
      capacityRatio = mMin / Math.Max(supplyAirMassFlowRate, exhaustAirMassFlowRate);

      //熱通過有効度[-]の計算
      double ntu = heatTransferCoefficient / mMin;
      HeatExchange.FlowType fType;
      if (flow == AirFlow.CounterFlow) fType = HeatExchange.FlowType.CounterFlow;
      else fType = HeatExchange.FlowType.CrossFlow_BothFluidsUnmixed;
      effectiveness = HeatExchange.GetEffectiveness(ntu, capacityRatio, fType);
    }

    #endregion

  }
}
