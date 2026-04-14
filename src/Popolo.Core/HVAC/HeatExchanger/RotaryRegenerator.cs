/* RotaryRegenerator.cs
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
  /// <summary>Rotary heat regenerator (enthalpy wheel or sensible wheel).</summary>
  public class RotaryRegenerator : IReadOnlyRotaryRegenerator
  {

    #region 定数宣言



    /// <summary>Assumes equal SA and EA flow-path cross-section ratio (1:1).</summary>
    private const double SA_AREA_RATE = 0.5;

    /// <summary>Perimeter per frontal area [m/m²].</summary>
    private const double PERUNIT = 5.76e-3 / (0.196e-6 + 2.584e-6);

    /// <summary>Equivalent hydraulic diameter of the matrix [m].</summary>
    private const double E_DIAMETER = 1.795e-3;

    /// <summary>Flow area to frontal area ratio [-].</summary>
    private const double AIRSTREAM_RATE = 2.584e-6 / (0.196e-6 + 2.584e-6);

    #endregion

    #region インスタンス変数

    /// <summary>Thermal conductivity of the matrix material [W/(m·K)].</summary>
    private double thermalConductivity;

    /// <summary>Rotor frontal area [m²].</summary>
    private double frontalArea;

    /// <summary>Flow passage cross-sectional area [m²].</summary>
    private double airStreamArea;

    /// <summary>Matrix cross-sectional area [m²].</summary>
    private double matrixArea;

    /// <summary>Heat transfer surface area [m²].</summary>
    private double heatTransferArea;

    /// <summary>Rotary heat capacity rate of the matrix [W/K].</summary>
    private double matrixHeatCapacity;

    /// <summary>Nominal heat transfer effectiveness [-].</summary>
    private double nominalEpsilon;

    #endregion

    #region プロパティ・インスタンス変数

    /// <summary>Gets a value indicating whether the detailed geometric model is used.</summary>
    /// <remarks>Simplified model: effectiveness is constant regardless of flow rate.</remarks>
    public bool IsDetailedModel { get; private set; }

    /// <summary>Gets a value indicating whether this is a total (enthalpy) heat exchanger.</summary>
    public bool IsDesiccantWheel { get; private set; }

    /// <summary>Gets the rotor diameter [m].</summary>
    public double Diameter { get; private set; }

    /// <summary>Gets the rotor depth [m].</summary>
    public double Depth { get; private set; }

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

    /// <summary>Gets the supply-air-side heat exchange efficiency εSA [-].</summary>
    public double Efficiency { get; private set; }

    /// <summary>Gets or sets the nominal power consumption [kW].</summary>
    public double NominalElectricity { get; set; }

    /// <summary>Gets the current power consumption [kW].</summary>
    public double Electricity { get; private set; }

    #endregion

    #region コンストラクタ

    /// <summary>Initializes a new instance.</summary>
    /// <param name="nominalEpsilon">Nominal heat transfer effectiveness [-].</param>
    /// <param name="isDesiccantWheel">True for a desiccant (total heat) wheel; false for a sensible wheel.</param>
    /// <param name="nominalElectricity">Nominal power consumption [kW].</param>
    /// <remarks>Simplified model: uses a fixed effectiveness independent of flow rate.</remarks>
    public RotaryRegenerator(double nominalEpsilon, bool isDesiccantWheel, double nominalElectricity)
    {
      IsDetailedModel = false;
      this.nominalEpsilon = nominalEpsilon;
      IsDesiccantWheel = isDesiccantWheel;
      NominalElectricity = nominalElectricity;
    }

    /// <summary>Initializes a new instance.</summary>
    /// <param name="efficiency">Nominal supply-air-side heat exchange efficiency εSA [-].</param>
    /// <param name="diameter">Rotor diameter [m].</param>
    /// <param name="depth">Rotor depth [m].</param>
    /// <param name="thermalConductivity">Thermal conductivity of the matrix material [W/(m·K)].</param>
    /// <param name="saFlowVolume">Supply air volumetric flow rate [m³/h].</param>
    /// <param name="eaFlowVolume">Exhaust air volumetric flow rate [m³/h].</param>
    /// <param name="isDesiccantWheel">True for a desiccant (total heat) wheel; false for a sensible wheel.</param>
    /// <param name="inletSADrybulbTemperature">Supply air inlet dry-bulb temperature [°C].</param>
    /// <param name="inletSAHumidityRatio">Supply air inlet humidity ratio [kg/kg].</param>
    /// <param name="inletEADrybulbTemperature">Exhaust air inlet dry-bulb temperature [°C].</param>
    /// <param name="inletEAHumidityRatio">Exhaust air inlet humidity ratio [kg/kg].</param>
    /// <param name="nominalElectricity">Nominal power consumption [kW].</param>
    public RotaryRegenerator
      (double efficiency, double diameter, double depth, double thermalConductivity, double saFlowVolume,
      double eaFlowVolume, bool isDesiccantWheel, double inletSADrybulbTemperature, double inletSAHumidityRatio,
      double inletEADrybulbTemperature, double inletEAHumidityRatio, double nominalElectricity)
    {
      Initialize(efficiency, diameter, depth, thermalConductivity, saFlowVolume, eaFlowVolume,
        isDesiccantWheel, inletSADrybulbTemperature, inletSAHumidityRatio, inletEADrybulbTemperature,
        inletEAHumidityRatio, nominalElectricity);
    }

    /// <summary>Initializes a new instance.</summary>
    /// <param name="depth">Rotor depth [m].</param>
    /// <param name="saFlowVolume">Supply air volumetric flow rate [m³/h].</param>
    /// <param name="eaFlowVolume">Exhaust air volumetric flow rate [m³/h].</param>
    /// <param name="isDesiccantWheel">True for a desiccant (total heat) wheel; false for a sensible wheel.</param>
    /// <param name="inletSADrybulbTemperature">Supply air inlet dry-bulb temperature [°C].</param>
    /// <param name="inletSAHumidityRatio">Supply air inlet humidity ratio [kg/kg].</param>
    /// <param name="inletEADrybulbTemperature">Exhaust air inlet dry-bulb temperature [°C].</param>
    /// <param name="inletEAHumidityRatio">Exhaust air inlet humidity ratio [kg/kg].</param>
    public RotaryRegenerator
      (double depth, double saFlowVolume, double eaFlowVolume, bool isDesiccantWheel,
      double inletSADrybulbTemperature, double inletSAHumidityRatio,
      double inletEADrybulbTemperature, double inletEAHumidityRatio)
    {
      const double VEL = 3.0;
      double diameter = 2 * Math.Sqrt(saFlowVolume / 3600 / (VEL * 0.433 * Math.PI));
      double mr = saFlowVolume / eaFlowVolume;
      double eff = 1.0884 - 0.0608 * VEL + mr * ((0.0623 + 0.0022 * VEL) + mr * (-0.3093 + mr * 0.0752));
      double nomElec = 1e-5 * saFlowVolume + 0.09;
      IsDesiccantWheel = isDesiccantWheel;

      Initialize
        (eff, diameter, depth, 210, saFlowVolume, eaFlowVolume, IsDesiccantWheel, inletSADrybulbTemperature,
        inletSAHumidityRatio, inletEADrybulbTemperature, inletEAHumidityRatio, nomElec);
    }

    /// <summary>Initializes a new instance.</summary>
    /// <param name="efficiency">Nominal supply-air-side heat exchange efficiency εSA [-].</param>
    /// <param name="diameter">Rotor diameter [m].</param>
    /// <param name="depth">Rotor depth [m].</param>
    /// <param name="thermalConductivity">Thermal conductivity of the matrix material [W/(m·K)].</param>
    /// <param name="saFlowVolume">Supply air volumetric flow rate [m³/h].</param>
    /// <param name="eaFlowVolume">Exhaust air volumetric flow rate [m³/h].</param>
    /// <param name="isDesiccantWheel">True for a desiccant (total heat) wheel; false for a sensible wheel.</param>
    /// <param name="inletSADrybulbTemperature">Supply air inlet dry-bulb temperature [°C].</param>
    /// <param name="inletSAHumidityRatio">Supply air inlet humidity ratio [kg/kg].</param>
    /// <param name="inletEADrybulbTemperature">Exhaust air inlet dry-bulb temperature [°C].</param>
    /// <param name="inletEAHumidityRatio">Exhaust air inlet humidity ratio [kg/kg].</param>
    /// <param name="nominalElectricity">Nominal power consumption [kW].</param>
    private void Initialize
      (double efficiency, double diameter, double depth, double thermalConductivity,
      double saFlowVolume, double eaFlowVolume, bool isDesiccantWheel,
      double inletSADrybulbTemperature, double inletSAHumidityRatio,
      double inletEADrybulbTemperature, double inletEAHumidityRatio, double nominalElectricity)
    {
      IsDetailedModel = true;

      //幾何学形状を保存・計算
      Depth = depth;
      Diameter = diameter;
      GetRotaryGeometrics
        (diameter, depth, out frontalArea, out airStreamArea, out matrixArea, out heatTransferArea);

      //マトリクスの熱伝導率[W/(mK)]を保存して熱容量を逆算
      this.thermalConductivity = thermalConductivity;
      matrixHeatCapacity = GetMatrixHeatCapacity
        (efficiency, frontalArea, airStreamArea, matrixArea, heatTransferArea, depth, thermalConductivity,
        saFlowVolume, eaFlowVolume, inletSADrybulbTemperature, inletSAHumidityRatio,
        inletEADrybulbTemperature, inletEAHumidityRatio);

      //ローターの種類と消費電力を保存
      IsDesiccantWheel = isDesiccantWheel;
      NominalElectricity = nominalElectricity;

      //定格条件で成り行き計算
      UpdateState(saFlowVolume, eaFlowVolume, 1.0, inletSADrybulbTemperature, inletSAHumidityRatio,
        inletEADrybulbTemperature, inletEAHumidityRatio);
    }

    #endregion

    #region 成り行き計算処理（インスタンスメソッド）

    /// <summary>Computes the outlet states for the given inlet conditions (free-running).</summary>
    /// <param name="supplyAirFlowVolume">Supply air volumetric flow rate [m³/h].</param>
    /// <param name="exhaustAirFlowVolume">Exhaust air volumetric flow rate [m³/h].</param>
    /// <param name="rotatingRate">Rotor rotation rate ratio [-].</param>
    /// <param name="inletSADrybulbTemperature">Supply air inlet dry-bulb temperature [°C].</param>
    /// <param name="inletSAHumidityRatio">Supply air inlet humidity ratio [kg/kg].</param>
    /// <param name="inletEADrybulbTemperature">Exhaust air inlet dry-bulb temperature [°C].</param>
    /// <param name="inletEAHumidityRatio">Exhaust air inlet humidity ratio [kg/kg].</param>
    public void UpdateState
      (double supplyAirFlowVolume, double exhaustAirFlowVolume, double rotatingRate,
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

      //消費電力は回転率に比例
      Electricity = NominalElectricity * rotatingRate;

      //風量・回転数が0の場合
      if (supplyAirFlowVolume <= 0 || exhaustAirFlowVolume <= 0 || rotatingRate <= 0)
      {
        ShutOff();
        return;
      }

      double effSA, effEA;
      if (IsDetailedModel)
      {
        GetEfficiency_Detailed
          (supplyAirFlowVolume, exhaustAirFlowVolume, rotatingRate,
          inletSADrybulbTemperature, inletSAHumidityRatio,
          inletEADrybulbTemperature, inletEAHumidityRatio, out effSA, out effEA);
      }
      else
      {
        GetEfficiency_Simplified
          (supplyAirFlowVolume, exhaustAirFlowVolume, rotatingRate,
          inletSADrybulbTemperature, inletSAHumidityRatio,
          inletEADrybulbTemperature, inletEAHumidityRatio, out effSA, out effEA);
      }
      Efficiency = effSA;

      //出口空気状態の計算
      SupplyAirOutletDrybulbTemperature = SupplyAirInletDrybulbTemperature -
        effSA * (SupplyAirInletDrybulbTemperature - ExhaustAirInletDrybulbTemperature);
      ExhaustAirOutletDrybulbTemperature = ExhaustAirInletDrybulbTemperature -
        effEA * (ExhaustAirInletDrybulbTemperature - SupplyAirInletDrybulbTemperature);
      //水分交換
      if (IsDesiccantWheel)
      {
        SupplyAirOutletHumidityRatio = SupplyAirInletHumidityRatio -
          effSA * (SupplyAirInletHumidityRatio - ExhaustAirInletHumidityRatio);
        ExhaustAirOutletHumidityRatio = ExhaustAirInletHumidityRatio -
          effEA * (ExhaustAirInletHumidityRatio - SupplyAirInletHumidityRatio);
      }
      else
      {
        SupplyAirOutletHumidityRatio = SupplyAirInletHumidityRatio;
        ExhaustAirOutletHumidityRatio = ExhaustAirInletHumidityRatio;
      }
    }

    /// <summary>Computes the heat exchange efficiency [-] using the detailed model.</summary>
    /// <param name="supplyAirFlowVolume">Supply air volumetric flow rate [m³/h].</param>
    /// <param name="exhaustAirFlowVolume">Exhaust air volumetric flow rate [m³/h].</param>
    /// <param name="rotatingRate">Rotor rotation rate ratio [-].</param>
    /// <param name="inletSADrybulbTemperature">Supply air inlet dry-bulb temperature [°C].</param>
    /// <param name="inletSAHumidityRatio">Supply air inlet humidity ratio [kg/kg].</param>
    /// <param name="inletEADrybulbTemperature">Exhaust air inlet dry-bulb temperature [°C].</param>
    /// <param name="inletEAHumidityRatio">Exhaust air inlet humidity ratio [kg/kg].</param>
    /// <param name="effSA">Output: supply-air-side heat exchange efficiency [-].</param>
    /// <param name="effEA">Output: exhaust-air-side heat exchange efficiency [-].</param>
    private void GetEfficiency_Detailed
      (double supplyAirFlowVolume, double exhaustAirFlowVolume, double rotatingRate,
      double inletSADrybulbTemperature, double inletSAHumidityRatio,
      double inletEADrybulbTemperature, double inletEAHumidityRatio, out double effSA, out double effEA)
    {
      //無次元数を計算
      double ntu0m, rmc, cLambda, mcMin;
      bool isMcMinSASide;
      GetDimensionLessVariables
        (frontalArea, airStreamArea, matrixArea, heatTransferArea, Depth,
        thermalConductivity, supplyAirFlowVolume, exhaustAirFlowVolume,
        inletSADrybulbTemperature, inletSAHumidityRatio, inletEADrybulbTemperature, inletEAHumidityRatio,
        out mcMin, out rmc, out ntu0m, out cLambda, out isMcMinSASide);

      //回転数制御の効果を計算
      double rr = (matrixHeatCapacity * rotatingRate) / mcMin;

      //熱通過有効度[-]を計算
      double eff = GetEffectiveness
        (mcMin, rmc, ntu0m, cLambda, rr, supplyAirFlowVolume, exhaustAirFlowVolume,
        inletSADrybulbTemperature, inletSAHumidityRatio, inletEADrybulbTemperature, inletEAHumidityRatio);

      //熱交換効率[-]を計算
      if (isMcMinSASide)
      {
        effSA = eff;
        effEA = eff * rmc;
      }
      else
      {
        effSA = eff * rmc;
        effEA = eff;
      }
    }

    /// <summary>Computes the heat exchange efficiency [-] using the simplified model.</summary>
    /// <param name="supplyAirFlowVolume">Supply air volumetric flow rate [m³/h].</param>
    /// <param name="exhaustAirFlowVolume">Exhaust air volumetric flow rate [m³/h].</param>
    /// <param name="rotatingRate">Rotor rotation rate ratio [-].</param>
    /// <param name="inletSADrybulbTemperature">Supply air inlet dry-bulb temperature [°C].</param>
    /// <param name="inletSAHumidityRatio">Supply air inlet humidity ratio [kg/kg].</param>
    /// <param name="inletEADrybulbTemperature">Exhaust air inlet dry-bulb temperature [°C].</param>
    /// <param name="inletEAHumidityRatio">Exhaust air inlet humidity ratio [kg/kg].</param>
    /// <param name="effSA">Output: supply-air-side heat exchange efficiency [-].</param>
    /// <param name="effEA">Output: exhaust-air-side heat exchange efficiency [-].</param>
    private void GetEfficiency_Simplified
      (double supplyAirFlowVolume, double exhaustAirFlowVolume, double rotatingRate,
      double inletSADrybulbTemperature, double inletSAHumidityRatio,
      double inletEADrybulbTemperature, double inletEAHumidityRatio, out double effSA, out double effEA)
    {
      //熱容量流量比[-]の計算
      double svSA = MoistAir.GetSpecificVolumeFromDryBulbTemperatureAndHumidityRatio
        (inletSADrybulbTemperature, inletSAHumidityRatio, PhysicsConstants.StandardAtmosphericPressure);
      double svEA = MoistAir.GetSpecificVolumeFromDryBulbTemperatureAndHumidityRatio
        (inletEADrybulbTemperature, inletEAHumidityRatio, PhysicsConstants.StandardAtmosphericPressure);
      double mSA = supplyAirFlowVolume / 3600d / svSA;
      double mEA = exhaustAirFlowVolume / 3600d / svEA;
      double mcSA = mSA * MoistAir.GetSpecificHeat(inletSAHumidityRatio) * 1000;
      double mcEA = mEA * MoistAir.GetSpecificHeat(inletEAHumidityRatio) * 1000;
      double mcMin = Math.Min(mcSA, mcEA);
      double mcMax = Math.Max(mcSA, mcEA);
      double rmc = mcMin / mcMax;

      if (mcSA == mcMin)
      {
        effSA = nominalEpsilon * rotatingRate;
        effEA = nominalEpsilon * rotatingRate * rmc;
      }
      else
      {
        effSA = nominalEpsilon * rotatingRate * rmc;
        effEA = nominalEpsilon * rotatingRate;
      }
    }

    #endregion

    #region 出口状態制御処理（インスタンスメソッド）

    /// <summary>Controls the supply air outlet temperature to the given setpoint.</summary>
    /// <param name="supplyAirFlowVolume">Supply air volumetric flow rate [m³/h].</param>
    /// <param name="exhaustAirFlowVolume">Exhaust air volumetric flow rate [m³/h].</param>
    /// <param name="rotatingRate">Rotor rotation rate ratio [-].</param>
    /// <param name="inletSADrybulbTemperature">Supply air inlet dry-bulb temperature [°C].</param>
    /// <param name="inletSAHumidityRatio">Supply air inlet humidity ratio [kg/kg].</param>
    /// <param name="inletEADrybulbTemperature">Exhaust air inlet dry-bulb temperature [°C].</param>
    /// <param name="inletEAHumidityRatio">Exhaust air inlet humidity ratio [kg/kg].</param>
    /// <param name="outletSADrybulbTemperatureSP">Supply air outlet dry-bulb temperature setpoint [°C].</param>
    /// <returns>True if the setpoint is achievable; false if overloaded.</returns>
    public bool ControlOutletTemperature(
      double supplyAirFlowVolume, double exhaustAirFlowVolume, double rotatingRate,
      double inletSADrybulbTemperature, double inletSAHumidityRatio,
      double inletEADrybulbTemperature, double inletEAHumidityRatio, double outletSADrybulbTemperatureSP)
    {
      //成り行き計算実行
      UpdateState
        (supplyAirFlowVolume, exhaustAirFlowVolume, rotatingRate,
        inletSADrybulbTemperature, inletSAHumidityRatio, inletEADrybulbTemperature, inletEAHumidityRatio);

      //冷却運転か否か
      bool cl = SupplyAirOutletDrybulbTemperature < SupplyAirInletDrybulbTemperature;

      //熱交換が無駄な場合は停止
      if ((cl && (SupplyAirInletDrybulbTemperature < outletSADrybulbTemperatureSP)) ||
        (!cl && (outletSADrybulbTemperatureSP < SupplyAirInletDrybulbTemperature)))
      {
        ShutOff();
        return false;
      }

      //処理可能な場合には消費電力と出口条件を修正
      bool canSolve =
        (cl && SupplyAirOutletDrybulbTemperature <= outletSADrybulbTemperatureSP
        && outletSADrybulbTemperatureSP <= SupplyAirInletDrybulbTemperature) ||
        (!cl && outletSADrybulbTemperatureSP < SupplyAirOutletDrybulbTemperature
        && SupplyAirInletDrybulbTemperature <= outletSADrybulbTemperatureSP);
      if (canSolve)
      {
        double rRun = (1d / Efficiency) * (outletSADrybulbTemperatureSP - SupplyAirInletDrybulbTemperature)
          / (ExhaustAirInletDrybulbTemperature - SupplyAirInletDrybulbTemperature);
        Electricity *= rRun;
        SupplyAirOutletDrybulbTemperature = outletSADrybulbTemperatureSP;
        SupplyAirOutletHumidityRatio =
          rRun * SupplyAirOutletHumidityRatio + (1 - rRun) * SupplyAirInletHumidityRatio;
      }
      return canSolve;
    }

    /// <summary>Controls the supply air outlet humidity ratio to the given setpoint.</summary>
    /// <param name="supplyAirFlowVolume">Supply air volumetric flow rate [m³/h].</param>
    /// <param name="exhaustAirFlowVolume">Exhaust air volumetric flow rate [m³/h].</param>
    /// <param name="rotatingRate">Rotor rotation rate ratio [-].</param>
    /// <param name="inletSADrybulbTemperature">Supply air inlet dry-bulb temperature [°C].</param>
    /// <param name="inletSAHumidityRatio">Supply air inlet humidity ratio [kg/kg].</param>
    /// <param name="inletEADrybulbTemperature">Exhaust air inlet dry-bulb temperature [°C].</param>
    /// <param name="inletEAHumidityRatio">Exhaust air inlet humidity ratio [kg/kg].</param>
    /// <param name="outletSAHumidityRatioSP">Supply air outlet humidity ratio setpoint [kg/kg].</param>
    /// <returns>True if the setpoint is achievable; false if overloaded.</returns>
    public bool ControlOutletHumidity(
      double supplyAirFlowVolume, double exhaustAirFlowVolume, double rotatingRate,
      double inletSADrybulbTemperature, double inletSAHumidityRatio,
      double inletEADrybulbTemperature, double inletEAHumidityRatio, double outletSAHumidityRatioSP)
    {
      //成り行き計算実行
      UpdateState
        (supplyAirFlowVolume, exhaustAirFlowVolume, rotatingRate,
        inletSADrybulbTemperature, inletSAHumidityRatio,
        inletEADrybulbTemperature, inletEAHumidityRatio);

      //除湿運転か否か
      bool cl = SupplyAirOutletHumidityRatio < SupplyAirInletHumidityRatio;

      //熱交換が無駄な場合は停止
      if ((cl && (SupplyAirInletHumidityRatio < outletSAHumidityRatioSP)) ||
        (!cl && (outletSAHumidityRatioSP < SupplyAirInletHumidityRatio)))
      {
        ShutOff();
        return false;
      }

      //制御可能な場合には消費電力と出口条件を修正
      bool canSolve =
        (cl && SupplyAirOutletHumidityRatio <= outletSAHumidityRatioSP
        && outletSAHumidityRatioSP <= SupplyAirInletHumidityRatio) ||
        (!cl && outletSAHumidityRatioSP <= SupplyAirOutletHumidityRatio
        && SupplyAirInletHumidityRatio <= outletSAHumidityRatioSP);
      if (canSolve)
      {
        double rRun = (1d / Efficiency) *
          (outletSAHumidityRatioSP - SupplyAirInletHumidityRatio) /
          (ExhaustAirInletHumidityRatio - SupplyAirInletHumidityRatio);
        Electricity *= rRun;
        SupplyAirOutletDrybulbTemperature = rRun * SupplyAirOutletDrybulbTemperature
          + (1 - rRun) * SupplyAirInletDrybulbTemperature;
        SupplyAirOutletHumidityRatio = outletSAHumidityRatioSP;
      }
      return canSolve;
    }

    /// <summary>Controls the supply air outlet specific enthalpy to the given setpoint.</summary>
    /// <param name="supplyAirFlowVolume">Supply air volumetric flow rate [m³/h].</param>
    /// <param name="exhaustAirFlowVolume">Exhaust air volumetric flow rate [m³/h].</param>
    /// <param name="rotatingRate">Rotor rotation rate ratio [-].</param>
    /// <param name="inletSADrybulbTemperature">Supply air inlet dry-bulb temperature [°C].</param>
    /// <param name="inletSAHumidityRatio">Supply air inlet humidity ratio [kg/kg].</param>
    /// <param name="inletEADrybulbTemperature">Exhaust air inlet dry-bulb temperature [°C].</param>
    /// <param name="inletEAHumidityRatio">Exhaust air inlet humidity ratio [kg/kg].</param>
    /// <param name="outletSAEnthalpySP">Supply air outlet specific enthalpy setpoint [kJ/kg].</param>
    /// <returns>True if the setpoint is achievable; false if overloaded.</returns>
    public bool ControlOutletEnthalpy(
      double supplyAirFlowVolume, double exhaustAirFlowVolume, double rotatingRate,
      double inletSADrybulbTemperature, double inletSAHumidityRatio,
      double inletEADrybulbTemperature, double inletEAHumidityRatio, double outletSAEnthalpySP)
    {
      //成り行き計算実行
      UpdateState
        (supplyAirFlowVolume, exhaustAirFlowVolume, rotatingRate,
        inletSADrybulbTemperature, inletSAHumidityRatio, inletEADrybulbTemperature, inletEAHumidityRatio);

      //冷却除湿運転か否か
      double hSAi = MoistAir.GetEnthalpyFromDryBulbTemperatureAndHumidityRatio
        (SupplyAirInletDrybulbTemperature, SupplyAirInletHumidityRatio);
      double hSAo = MoistAir.GetEnthalpyFromDryBulbTemperatureAndHumidityRatio
        (SupplyAirOutletDrybulbTemperature, SupplyAirOutletHumidityRatio);
      bool cl = hSAo < hSAi;

      //熱交換が無駄な場合は停止
      if ((cl && (hSAi < outletSAEnthalpySP)) || (!cl && (outletSAEnthalpySP < hSAi)))
      {
        ShutOff();
        return false;
      }

      //制御可能な場合には消費電力と出口条件を修正
      bool canSolve =
        (cl && hSAo <= outletSAEnthalpySP && outletSAEnthalpySP <= hSAi) ||
        (!cl && outletSAEnthalpySP <= hSAo && hSAi <= outletSAEnthalpySP);
      if (canSolve)
      {
        double ctw = 1.805
          * (ExhaustAirInletDrybulbTemperature - SupplyAirInletDrybulbTemperature)
          * (ExhaustAirInletHumidityRatio - SupplyAirInletHumidityRatio);
        double aRun = Efficiency * Efficiency * ctw;
        double hEAi = MoistAir.GetEnthalpyFromDryBulbTemperatureAndHumidityRatio
          (ExhaustAirInletDrybulbTemperature, ExhaustAirInletHumidityRatio);
        double bRun = Efficiency * ((hEAi - hSAi) - ctw);
        double cRun = hSAi - outletSAEnthalpySP;
        double bf = Math.Sqrt(bRun * bRun - 4 * aRun * cRun);
        double rRun = (-bRun + bf) / (2 * aRun);
        if (rRun < 0 || 1 < rRun) rRun = (-bRun - bf) / (2 * aRun);
        Electricity *= rRun;
        SupplyAirOutletDrybulbTemperature = rRun * SupplyAirOutletDrybulbTemperature
          + (1 - rRun) * SupplyAirInletDrybulbTemperature;
        SupplyAirOutletHumidityRatio = rRun * SupplyAirOutletHumidityRatio
          + (1 - rRun) * SupplyAirInletHumidityRatio;
      }
      return canSolve;
    }

    #endregion

    #region その他インスタンスメソッド

    /// <summary>Computes the heat recovery rate [kW].</summary>
    /// <returns>Heat recovery rate [kW] (positive = supply air heated).</returns>
    public double GetHeatRecovery()
    {
      if (SupplyAirFlowVolume == 0 || ExhaustAirFlowVolume == 0) return 0;

      double hSAo = MoistAir.GetEnthalpyFromDryBulbTemperatureAndHumidityRatio
        (SupplyAirOutletDrybulbTemperature, SupplyAirOutletHumidityRatio);
      double hSAi = MoistAir.GetEnthalpyFromDryBulbTemperatureAndHumidityRatio
        (SupplyAirInletDrybulbTemperature, SupplyAirInletHumidityRatio);
      return SupplyAirFlowVolume * PhysicsConstants.NominalMoistAirDensity / 3600 * (hSAo - hSAi);
    }

    /// <summary>Shuts off the regenerator.</summary>
    public void ShutOff()
    {
      Efficiency = 0;
      SupplyAirOutletDrybulbTemperature = SupplyAirInletDrybulbTemperature;
      ExhaustAirOutletDrybulbTemperature = ExhaustAirInletDrybulbTemperature;
      SupplyAirOutletHumidityRatio = SupplyAirInletHumidityRatio;
      ExhaustAirOutletHumidityRatio = ExhaustAirInletHumidityRatio;
      SupplyAirFlowVolume = ExhaustAirFlowVolume = 0;
      Electricity = 0;
    }

    #endregion

    #region staticメソッド

    /// <summary>Computes the rotary heat capacity rate of the matrix [W/K].</summary>
    /// <param name="efficiency">Supply-air-side heat exchange efficiency εSA [-].</param>
    /// <param name="frontalArea">Rotor frontal area [m²].</param>
    /// <param name="airStreamArea">Air stream cross-sectional area [m²].</param>
    /// <param name="matrixArea">Matrix cross-sectional area [m²].</param>
    /// <param name="heatTransferArea">Heat transfer surface area [m²].</param>
    /// <param name="length">Rotor depth [m].</param>
    /// <param name="thermalConductivity">Thermal conductivity of the matrix material [W/(m·K)].</param>
    /// <param name="supplyAirFlowVolume">Supply air volumetric flow rate [m³/h].</param>
    /// <param name="exhaustAirFlowVolume">Exhaust air volumetric flow rate [m³/h].</param>
    /// <param name="inletSADrybulbTemperature">Supply air inlet dry-bulb temperature [°C].</param>
    /// <param name="inletSAHumidityRatio">Supply air inlet humidity ratio [kg/kg].</param>
    /// <param name="inletEADrybulbTemperature">Exhaust air inlet dry-bulb temperature [°C].</param>
    /// <param name="inletEAHumidityRatio">Exhaust air inlet humidity ratio [kg/kg].</param>
    /// <returns>Rotary heat capacity rate [W/K].</returns>
    public static double GetMatrixHeatCapacity
      (double efficiency, double frontalArea, double airStreamArea, double matrixArea,
      double heatTransferArea, double length, double thermalConductivity, double supplyAirFlowVolume,
      double exhaustAirFlowVolume, double inletSADrybulbTemperature, double inletSAHumidityRatio,
      double inletEADrybulbTemperature, double inletEAHumidityRatio)
    {
      //無次元数を取得
      double ntu0m, rmc, cLambda, mcMin;
      bool isMcMinSASide;
      GetDimensionLessVariables
        (frontalArea, airStreamArea, matrixArea, heatTransferArea, length, thermalConductivity,
        supplyAirFlowVolume, exhaustAirFlowVolume, inletSADrybulbTemperature, inletSAHumidityRatio,
        inletEADrybulbTemperature, inletEAHumidityRatio,
        out mcMin, out rmc, out ntu0m, out cLambda, out isMcMinSASide);

      double effectiveness;
      if (isMcMinSASide) effectiveness = efficiency;
      else effectiveness = efficiency / rmc;

      double rr;
      if (0.99 < rmc)
      {
        //熱容量流量がほぼ等しい場合
        double ee = ntu0m / (1 + ntu0m);
        rr = effectiveness / ee / (1 - cLambda);
        if (1 <= rr) rr = 100;
        else rr = Math.Pow(1 / (9 * (1 - rr)), 1 / 1.93);
      }
      else
      {
        //熱容量流量が異なる場合
        double em = 2 * rmc * Math.Log((1 - effectiveness) / (1 - effectiveness * rmc));
        em = em / (em + rmc * rmc - 1);
        double rm = em * ((1 + ntu0m) / ntu0m) / (1 - cLambda / (2 - rmc));
        if (1 <= rm) rm = 100;
        else rm = Math.Pow(1 / (9 * (1 - rm)), 1 / 1.93);
        rr = rm * (1 + rmc) / (2 * rmc);
      }
      return rr * mcMin;
    }

    /// <summary>Computes the heat transfer effectiveness [-].</summary>
    /// <param name="mcMin">Minimum heat capacity rate [W/K].</param>
    /// <param name="rmc">Heat capacity rate ratio (Cmin/Cmax) [-].</param>
    /// <param name="ntu0m">Modified number of transfer units [-].</param>
    /// <param name="cLambda">Longitudinal heat conduction correction factor [-].</param>
    /// <param name="rr">Rotary heat capacity rate ratio Cr* [-].</param>
    /// <param name="supplyAirFlowVolume">Supply air volumetric flow rate [m³/h].</param>
    /// <param name="exhaustAirFlowVolume">Exhaust air volumetric flow rate [m³/h].</param>
    /// <param name="inletSADrybulbTemperature">Supply air inlet dry-bulb temperature [°C].</param>
    /// <param name="inletSAHumidityRatio">Supply air inlet humidity ratio [kg/kg].</param>
    /// <param name="inletEADrybulbTemperature">Exhaust air inlet dry-bulb temperature [°C].</param>
    /// <param name="inletEAHumidityRatio">Exhaust air inlet humidity ratio [kg/kg].</param>
    /// <returns>Heat transfer effectiveness [-].</returns>
    public static double GetEffectiveness
      (double mcMin, double rmc, double ntu0m, double cLambda, double rr, double supplyAirFlowVolume,
      double exhaustAirFlowVolume, double inletSADrybulbTemperature, double inletSAHumidityRatio,
      double inletEADrybulbTemperature, double inletEAHumidityRatio)
    {
      double e;
      if (0.99 < rmc)
      {
        //熱容量流量がほぼ等しい場合
        double ee = ntu0m / (1 + ntu0m);
        e = ee * (1 - 1 / (9 * Math.Pow(rr, 1.93))) * (1 - cLambda);
      }
      else
      {
        //熱容量流量が異なる場合
        double rrm = 2 * rr * rmc / (1 + rmc);
        double em = ntu0m / (1 + ntu0m) * (1 - 1 / (9 * Math.Pow(rrm, 1.93)));
        em *= 1 - cLambda / (2 - rmc);
        e = Math.Exp(em * (rmc * rmc - 1) / (2 * rmc * (1 - em)));
        e = (1 - e) / (1 - rmc * e);
      }
      return Math.Min(1, Math.Max(e, 0));
    }

    /// <summary>Computes the dimensionless parameters (NTU, Cr*, Cmin/Cmax).</summary>
    /// <param name="frontalArea">Rotor frontal area [m²].</param>
    /// <param name="airStreamArea">Air stream cross-sectional area [m²].</param>
    /// <param name="matrixArea">Matrix cross-sectional area [m²].</param>
    /// <param name="heatTransferArea">Heat transfer surface area [m²].</param>
    /// <param name="length">Rotor depth [m].</param>
    /// <param name="thermalConductivity">Thermal conductivity of the matrix material [W/(m·K)].</param>
    /// <param name="saFlowVolume">Supply air volumetric flow rate [m³/h].</param>
    /// <param name="eaFlowVolume">Exhaust air volumetric flow rate [m³/h].</param>
    /// <param name="inletSADrybulbTemperature">Supply air inlet dry-bulb temperature [°C].</param>
    /// <param name="inletSAHumidityRatio">Supply air inlet humidity ratio [kg/kg].</param>
    /// <param name="inletEADrybulbTemperature">Exhaust air inlet dry-bulb temperature [°C].</param>
    /// <param name="inletEAHumidityRatio">Exhaust air inlet humidity ratio [kg/kg].</param>
    /// <param name="mcMin">Minimum heat capacity rate [W/K].</param>
    /// <param name="rmc">Heat capacity rate ratio (Cmin/Cmax) [-].</param>
    /// <param name="ntu0m">Modified number of transfer units [-].</param>
    /// <param name="cLambda">Longitudinal heat conduction correction factor [-].</param>
    /// <param name="isMcMinSASide">True if the supply air side has the smaller heat capacity rate.</param>
    public static void GetDimensionLessVariables
      (double frontalArea, double airStreamArea, double matrixArea, double heatTransferArea, double length,
      double thermalConductivity, double saFlowVolume, double eaFlowVolume, double inletSADrybulbTemperature,
      double inletSAHumidityRatio, double inletEADrybulbTemperature, double inletEAHumidityRatio,
      out double mcMin, out double rmc, out double ntu0m, out double cLambda, out bool isMcMinSASide)
    {
      //熱容量流量比[-]の計算
      double svSA = MoistAir.GetSpecificVolumeFromDryBulbTemperatureAndHumidityRatio
        (inletSADrybulbTemperature, inletSAHumidityRatio, PhysicsConstants.StandardAtmosphericPressure);
      double svEA = MoistAir.GetSpecificVolumeFromDryBulbTemperatureAndHumidityRatio
        (inletEADrybulbTemperature, inletEAHumidityRatio, PhysicsConstants.StandardAtmosphericPressure);
      double mSA = saFlowVolume / 3600d / svSA;
      double mEA = eaFlowVolume / 3600d / svEA;
      double mcSA = mSA * MoistAir.GetSpecificHeat(inletSAHumidityRatio) * 1000;
      double mcEA = mEA * MoistAir.GetSpecificHeat(inletEAHumidityRatio) * 1000;
      mcMin = Math.Min(mcSA, mcEA);
      double mcMax = Math.Max(mcSA, mcEA);
      rmc = mcMin / mcMax;
      isMcMinSASide = (mcSA == mcMin);

      //風速[m/s]の計算
      double vSA = saFlowVolume / 3600d / (airStreamArea * SA_AREA_RATE);
      double vEA = eaFlowVolume / 3600d / (airStreamArea * (1 - SA_AREA_RATE));
      //対流熱伝達率[W/(m2K)]の計算
      double ed25 = 1d / Math.Pow(E_DIAMETER, 0.25);
      double alphaSA = (4.13 + 0.195 * inletSADrybulbTemperature / 100) * Math.Pow(vSA, 0.75) * ed25;
      double alphaEA = (4.13 + 0.195 * inletEADrybulbTemperature / 100) * Math.Pow(vEA, 0.75) * ed25;
      //修正移動単位数[-]の計算
      double ha = 1 / (alphaSA * heatTransferArea * SA_AREA_RATE)
        + 1 / (alphaEA * heatTransferArea * (1 - SA_AREA_RATE));
      double ntu0 = 1 / mcMin / ha;
      ntu0m = (2 * ntu0 * rmc) / (1 + rmc);

      //流路方向の熱伝導補正係数[-]の計算
      double lambda = (thermalConductivity * matrixArea) / (length * mcMin);
      double phi = Math.Sqrt((lambda * ntu0m) / (1 + lambda * ntu0m));
      phi = phi * Math.Tanh(ntu0m / phi);
      cLambda = 1 / (1 + ntu0m * (1 + lambda * phi) / (1 + lambda * ntu0m)) - 1 / (1 + ntu0m);
    }

    /// <summary>Computes the rotor geometry (surface areas, equivalent diameter, etc.).</summary>
    /// <param name="diameter">Rotor diameter [m].</param>
    /// <param name="length">Rotor depth [m].</param>
    /// <param name="frontalArea">Rotor frontal area [m²].</param>
    /// <param name="airStreamArea">Air stream cross-sectional area [m²].</param>
    /// <param name="matrixArea">Matrix cross-sectional area [m²].</param>
    /// <param name="heatTransferArea">Heat transfer surface area [m²].</param>
    public static void GetRotaryGeometrics
      (double diameter, double length, out double frontalArea, out double airStreamArea,
      out double matrixArea, out double heatTransferArea)
    {
      //見付面積[m2]の計算
      frontalArea = diameter * diameter * Math.PI / 4;
      //流路面積[m2]の計算
      airStreamArea = frontalArea * AIRSTREAM_RATE;
      //マトリクス面積[m2]の計算
      matrixArea = frontalArea - airStreamArea;
      //伝熱面積[m2]の計算
      heatTransferArea = frontalArea * PERUNIT * length;
    }

    #endregion

  }
}
