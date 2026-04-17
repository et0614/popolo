/* CrossFinHeatExchanger.cs
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
using Popolo.Core.Numerics;

namespace Popolo.Core.HVAC.HeatExchanger
{
  /// <summary>Cross-fin (plate-fin-and-tube) heat exchanger for air-water coils.</summary>
  public class CrossFinHeatExchanger : IReadOnlyCrossFinHeatExchanger
  {

    #region 定数宣言

    /// <summary>Approximate density of moist air [kg/m³].</summary>
    private const double AIR_SPECIFIC_WEIGHT = 1.2;



    #endregion

    #region 列挙型定義

    /// <summary>Water flow circuit type.</summary>
    public enum WaterFlowType
    {
      /// <summary>Half-flow circuit.</summary>
      HalfFlow,
      /// <summary>Single-flow circuit.</summary>
      SingleFlow,
      /// <summary>Double-flow circuit.</summary>
      DoubleFlow,
      /// <summary>Triple-flow circuit.</summary>
      TripleFlow
    }

    #endregion

    #region インスタンス変数

    /// <summary>True if the detailed geometric model is used.</summary>
    private readonly bool isDetailedModel;

    /// <summary>Coil specification for the detailed model.</summary>
    private readonly double airWaterSurfaceRatio, coreArea, equivalentFinRadius, equivalentDiameter,
      waterPath, finThickness, thermalConductivity, innerDiameter, outerDiameter;

    /// <summary>Coil specification for the simplified model.</summary>
    private readonly double ratedVelocity, ratedWaterSpeed;

    /// <summary>Heat transfer degradation factor [-].</summary>
    private double degradationFactor = 1.0;

    #endregion

    #region プロパティ

    /// <summary>Gets the relative humidity at the dry/wet boundary [%].</summary>
    public double BorderRelativeHumidity { get; private set; }

    /// <summary>Gets the maximum water flow rate [kg/s].</summary>
    public double MaxWaterFlowRate { get; private set; }

    /// <summary>Gets the nominal water flow rate [kg/s].</summary>
    public double RatedWaterFlowRate { get; private set; }

    /// <summary>Gets the current water flow rate [kg/s].</summary>
    public double WaterFlowRate { get; private set; }

    /// <summary>Gets the current air flow rate [kg/s].</summary>
    public double AirFlowRate { get; private set; }

    /// <summary>Gets the nominal air flow rate [kg/s].</summary>
    public double RatedAirFlowRate { get; private set; }

    /// <summary>Gets the inlet air dry-bulb temperature [°C].</summary>
    public double InletAirTemperature { get; private set; }

    /// <summary>Gets the inlet air humidity ratio [kg/kg].</summary>
    public double InletAirHumidityRatio { get; private set; }

    /// <summary>Gets the outlet air dry-bulb temperature [°C].</summary>
    public double OutletAirTemperature { get; private set; }

    /// <summary>Gets the outlet air humidity ratio [kg/kg].</summary>
    public double OutletAirHumidityRatio { get; private set; }

    /// <summary>Gets the inlet water temperature [°C].</summary>
    public double InletWaterTemperature { get; private set; }

    /// <summary>Gets the outlet water temperature [°C].</summary>
    public double OutletWaterTemperature { get; private set; }

    /// <summary>Gets the heat transfer surface area [m²].</summary>
    public double SurfaceArea { get; private set; }

    /// <summary>Gets the dry coil fraction [-].</summary>
    public double DryRate { get; private set; }

    /// <summary>Gets the overall heat transfer coefficient for the dry coil [kW/(m²·K)].</summary>
    public double DryHeatTransferCoefficient { get; private set; }

    /// <summary>Gets the overall heat transfer coefficient for the wet coil [kW/(m²·(kJ/kg))].</summary>
    public double WetHeatTransferCoefficient { get; private set; }

    /// <summary>Gets or sets the surface area correction factor [-].</summary>
    public double CorrectionFactor { get; set; }

    /// <summary>Gets the heat transfer rate [kW] from the water-side perspective.</summary>
    /// <remarks>
    /// Computed as (OutletWaterTemperature - InletWaterTemperature) × WaterFlowRate × cp.
    /// <list type="bullet">
    ///   <item>
    ///     <description>
    ///       <b>Cooling coil</b>: water is heated by the air → positive value.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <description>
    ///       <b>Heating coil</b>: water is cooled by releasing heat to the air → negative value.
    ///     </description>
    ///   </item>
    /// </list>
    /// To obtain the magnitude of heating delivered to the air, use
    /// <c>Math.Abs(HeatTransfer)</c>.
    /// </remarks>
    public double HeatTransfer
    {
      get
      {
        return (OutletWaterTemperature - InletWaterTemperature) * WaterFlowRate * 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat;
      }
    }

    /// <summary>Gets or sets the heat transfer degradation factor [-].</summary>
    public double DegradationFactor
    {
      get { return degradationFactor; }
      set { degradationFactor = Math.Max(0.0001, Math.Min(1.0, value)); }
    }

    #endregion

    #region コンストラクタ

    /// <summary>Initializes a new instance using the detailed geometric coil model.</summary>
    /// <param name="depth">Coil depth [m].</param>
    /// <param name="width">Coil width [m].</param>
    /// <param name="height">Coil height [m].</param>
    /// <param name="rowNumber">Number of tube columns (perpendicular to air flow).</param>
    /// <param name="columnNumber">Number of tube rows in the air-flow direction.</param>
    /// <param name="finPitch">Fin pitch [m].</param>
    /// <param name="finThickness">Fin thickness [m].</param>
    /// <param name="thermalConductivity">Thermal conductivity of the fin material [W/(m·K)].</param>
    /// <param name="innerDiameter">Tube inner diameter [m].</param>
    /// <param name="outerDiameter">Tube outer diameter [m].</param>
    /// <param name="ratedAirFlowRate">Nominal air mass flow rate [kg/s].</param>
    /// <param name="ratedInletAirTemperature">Nominal inlet air dry-bulb temperature [°C].</param>
    /// <param name="ratedInletAirHumidityRatio">Nominal inlet air humidity ratio [kg/kg].</param>
    /// <param name="borderRelativeHumidity">Relative humidity at the dry/wet coil boundary [%].</param>
    /// <param name="ratedWaterFlowRate">Nominal water flow rate [kg/s].</param>
    /// <param name="maxWaterFlowRate">Maximum water flow rate [kg/s].</param>
    /// <param name="ratedInletWaterTemperature">Nominal inlet water temperature [°C].</param>
    /// <param name="flowType">Water flow circuit type.</param>
    /// <param name="heatTransfer">Rated heat transfer capacity [kW].</param>
    /// <param name="useCorrectionFactor">True to apply the correction factor to the heat transfer coefficients.</param>
    public CrossFinHeatExchanger(double depth, double width, double height, int rowNumber, int columnNumber,
      double finPitch, double finThickness, double thermalConductivity, double innerDiameter,
      double outerDiameter, double ratedAirFlowRate, double ratedInletAirTemperature,
      double ratedInletAirHumidityRatio, double borderRelativeHumidity, double ratedWaterFlowRate,
      double maxWaterFlowRate, double ratedInletWaterTemperature, WaterFlowType flowType,
      double heatTransfer, bool useCorrectionFactor)
      : this(depth, width, height, rowNumber, columnNumber, finPitch, finThickness, thermalConductivity,
          innerDiameter, outerDiameter, ratedAirFlowRate, ratedInletAirTemperature,
          ratedInletAirHumidityRatio, borderRelativeHumidity, ratedWaterFlowRate, maxWaterFlowRate,
          ratedInletWaterTemperature, GetFlowFactor(flowType), heatTransfer, useCorrectionFactor)
    { }

    /// <summary>Initializes a new instance using the detailed model with automatic UA estimation.</summary>
    /// <param name="width">Coil width [m].</param>
    /// <param name="height">Coil height [m].</param>
    /// <param name="rowNumber">Number of tube columns (perpendicular to air flow).</param>
    /// <param name="columnNumber">Number of tube rows in the air-flow direction.</param>
    /// <param name="ratedAirFlowRate">Nominal air mass flow rate [kg/s].</param>
    /// <param name="ratedInletAirTemperature">Nominal inlet air dry-bulb temperature [°C].</param>
    /// <param name="ratedInletAirHumidityRatio">Nominal inlet air humidity ratio [kg/kg].</param>
    /// <param name="borderRelativeHumidity">Relative humidity at the dry/wet coil boundary [%].</param>
    /// <param name="ratedWaterFlowRate">Nominal water flow rate [kg/s].</param>
    /// <param name="maxWaterFlowRate">Maximum water flow rate [kg/s].</param>
    /// <param name="ratedInletWaterTemperature">Nominal inlet water temperature [°C].</param>
    /// <param name="flowType">Water flow circuit type.</param>
    /// <param name="heatTransfer">Rated heat transfer capacity [kW].</param>
    /// <param name="useCorrectionFactor">True to apply the correction factor to the heat transfer coefficients.</param>
    public CrossFinHeatExchanger(
      double width, double height, int rowNumber, int columnNumber, double ratedAirFlowRate,
      double ratedInletAirTemperature, double ratedInletAirHumidityRatio, double borderRelativeHumidity,
      double ratedWaterFlowRate, double maxWaterFlowRate, double ratedInletWaterTemperature,
      WaterFlowType flowType, double heatTransfer, bool useCorrectionFactor)
      : this(rowNumber * 0.0329, width, height, rowNumber, columnNumber, 0.0029, 0.0002, 237, 0.0146, 0.0158,
          ratedAirFlowRate, ratedInletAirTemperature, ratedInletAirHumidityRatio, borderRelativeHumidity,
          ratedWaterFlowRate, maxWaterFlowRate, ratedInletWaterTemperature, GetFlowFactor(flowType),
          heatTransfer, useCorrectionFactor)
    { }

    /// <summary>Computes the flow factor from the water flow circuit type.</summary>
    /// <param name="wType">Water flow circuit type.</param>
    /// <returns>Flow factor [-].</returns>
    private static double GetFlowFactor(WaterFlowType wType)
    {
      if (wType == WaterFlowType.HalfFlow) return 0.5;
      else if (wType == WaterFlowType.SingleFlow) return 1.0;
      else if (wType == WaterFlowType.DoubleFlow) return 2.0;
      else return 3.0;
    }

    /// <summary>Initializes a new instance using the detailed geometric coil model.</summary>
    /// <param name="depth">Coil depth [m].</param>
    /// <param name="width">Coil width [m].</param>
    /// <param name="height">Coil height [m].</param>
    /// <param name="rowNumber">Number of tube columns (perpendicular to air flow).</param>
    /// <param name="columnNumber">Number of tube rows in the air-flow direction.</param>
    /// <param name="finPitch">Fin pitch [m].</param>
    /// <param name="finThickness">Fin thickness [m].</param>
    /// <param name="thermalConductivity">Thermal conductivity of the fin material [W/(m·K)].</param>
    /// <param name="innerDiameter">Tube inner diameter [m].</param>
    /// <param name="outerDiameter">Tube outer diameter [m].</param>
    /// <param name="ratedAirFlowRate">Nominal air mass flow rate [kg/s].</param>
    /// <param name="ratedInletAirTemperature">Nominal inlet air dry-bulb temperature [°C].</param>
    /// <param name="ratedInletAirHumidityRatio">Nominal inlet air humidity ratio [kg/kg].</param>
    /// <param name="borderRelativeHumidity">Relative humidity at the dry/wet coil boundary [%].</param>
    /// <param name="ratedWaterFlowRate">Nominal water flow rate [kg/s].</param>
    /// <param name="maxWaterFlowRate">Maximum water flow rate [kg/s].</param>
    /// <param name="ratedInletWaterTemperature">Nominal inlet water temperature [°C].</param>
    /// <param name="flowFactor">Flow factor [-].</param>
    /// <param name="heatTransfer">Rated heat transfer capacity [kW].</param>
    /// <param name="useCorrectionFactor">True to apply the correction factor to the heat transfer coefficients.</param>
    public CrossFinHeatExchanger(double depth, double width, double height, int rowNumber, int columnNumber,
      double finPitch, double finThickness, double thermalConductivity, double innerDiameter,
      double outerDiameter, double ratedAirFlowRate, double ratedInletAirTemperature,
      double ratedInletAirHumidityRatio, double borderRelativeHumidity, double ratedWaterFlowRate,
      double maxWaterFlowRate, double ratedInletWaterTemperature, double flowFactor, double heatTransfer,
      bool useCorrectionFactor)
    {
      //詳細モデルによる初期化
      isDetailedModel = true;

      //コイルの幾何学形状を計算
      double asr, car, eqr, eqd, asa;
      GetGeometricCompfigulation(depth, width, height, rowNumber, columnNumber, finPitch, finThickness,
        innerDiameter, outerDiameter, out asr, out car, out eqr, out eqd, out asa);

      //コイル仕様を保存
      this.airWaterSurfaceRatio = asr;
      this.coreArea = car;
      this.equivalentFinRadius = eqr;
      this.equivalentDiameter = eqd;
      this.waterPath = flowFactor * columnNumber;
      this.finThickness = finThickness;
      this.thermalConductivity = thermalConductivity;
      this.innerDiameter = innerDiameter;
      this.outerDiameter = outerDiameter;

      //その他のコイル仕様を保存
      this.RatedAirFlowRate = ratedAirFlowRate;
      this.RatedWaterFlowRate = ratedWaterFlowRate;
      this.MaxWaterFlowRate = maxWaterFlowRate;

      //乾湿境界での空気の相対湿度を保存
      this.BorderRelativeHumidity = borderRelativeHumidity;

      if (useCorrectionFactor)
      {
        //熱貫流率を計算する
        double kd, kw;
        GetHeatTransferCoefficient(airWaterSurfaceRatio, coreArea, equivalentFinRadius, equivalentDiameter,
          waterPath, finThickness, thermalConductivity, innerDiameter, outerDiameter, RatedAirFlowRate,
          ratedInletAirTemperature, ratedInletAirHumidityRatio, borderRelativeHumidity, RatedWaterFlowRate,
          ratedInletWaterTemperature, out kd, out kw);
        DryHeatTransferCoefficient = kd;
        WetHeatTransferCoefficient = kw;

        //伝熱面積[m2]を取得する
        SurfaceArea = GetSurfaceArea(ratedInletAirTemperature, ratedInletAirHumidityRatio,
          borderRelativeHumidity, ratedInletWaterTemperature, ratedAirFlowRate, ratedWaterFlowRate,
          heatTransfer, kd, kw);
        CorrectionFactor = SurfaceArea / (asa * rowNumber);
      }
      else
      {
        SurfaceArea = asa * rowNumber;
        CorrectionFactor = 1.0d;
      }
    }

    /// <summary>Initializes a new instance using the simplified coil model.</summary>
    /// <param name="ratedAirFlowRate">Nominal air mass flow rate [kg/s].</param>
    /// <param name="ratedVelocity">Nominal face velocity [m/s].</param>
    /// <param name="ratedInletAirTemperature">Nominal inlet air dry-bulb temperature [°C].</param>
    /// <param name="ratedInletAirHumidityRatio">Nominal inlet air humidity ratio [kg/kg].</param>
    /// <param name="borderRelativeHumidity">Dry/wet boundary relative humidity [%].</param>
    /// <param name="ratedWaterFlowRate">Nominal water flow rate [kg/s].</param>
    /// <param name="ratedWaterSpeed">Nominal water velocity inside tubes [m/s].</param>
    /// <param name="maxWaterFlowRate">Maximum water flow rate [kg/s].</param>
    /// <param name="ratedInletWaterTemperature">Nominal inlet water temperature [°C].</param>
    /// <param name="heatTransfer">Rated heat transfer capacity [kW].</param>
    public CrossFinHeatExchanger(double ratedAirFlowRate, double ratedVelocity,
      double ratedInletAirTemperature, double ratedInletAirHumidityRatio, double borderRelativeHumidity,
      double ratedWaterFlowRate, double ratedWaterSpeed, double maxWaterFlowRate,
      double ratedInletWaterTemperature, double heatTransfer)
    {
      //簡易モデルによる初期化
      isDetailedModel = false;

      //簡易モデルのコイル仕様を保存
      this.ratedVelocity = ratedVelocity;
      this.ratedWaterSpeed = ratedWaterSpeed;

      //その他のコイル仕様を保存
      this.RatedAirFlowRate = ratedAirFlowRate;
      this.RatedWaterFlowRate = ratedWaterFlowRate;
      this.MaxWaterFlowRate = maxWaterFlowRate;

      //乾湿境界での空気の相対湿度を保存
      this.BorderRelativeHumidity = borderRelativeHumidity;

      //熱貫流率を計算する
      double kd, kw;
      GetHeatTransferCoefficient(ratedWaterSpeed, ratedVelocity, out kd, out kw);
      DryHeatTransferCoefficient = kd;
      WetHeatTransferCoefficient = kw;

      //伝熱面積[m2]を取得する
      SurfaceArea = GetSurfaceArea(ratedInletAirTemperature, ratedInletAirHumidityRatio,
        borderRelativeHumidity, ratedInletWaterTemperature, ratedAirFlowRate, ratedWaterFlowRate,
        heatTransfer, kd, kw);
    }

    #endregion

    #region インスタンスメソッド

    /// <summary>Computes the outlet air and water states for the given inlet conditions.</summary>
    /// <param name="inletAirTemperature">Inlet air dry-bulb temperature [°C].</param>
    /// <param name="inletAirHumidityRatio">Inlet air humidity ratio [kg/kg].</param>
    /// <param name="inletWaterTemperature">Inlet water temperature [°C].</param>
    /// <param name="airFlowRate">Air mass flow rate [kg/s].</param>
    /// <param name="waterFlowRate">Water flow rate [kg/s].</param>
    public void UpdateOutletState(double inletAirTemperature, double inletAirHumidityRatio,
      double inletWaterTemperature, double airFlowRate, double waterFlowRate)
    {
      //入力値を保存
      InletAirTemperature = inletAirTemperature;
      InletAirHumidityRatio = inletAirHumidityRatio;
      InletWaterTemperature = inletWaterTemperature;
      AirFlowRate = airFlowRate;
      WaterFlowRate = waterFlowRate;

      //熱媒が流れていない場合
      if (AirFlowRate <= 0 || WaterFlowRate <= 0)
      {
        OutletAirTemperature = InletAirTemperature;
        OutletAirHumidityRatio = InletAirHumidityRatio;
        OutletWaterTemperature = InletWaterTemperature;
        DryRate = 1.0;
        return;
      }

      //熱貫流率を計算
      if (isDetailedModel)
      {
        //詳細モデル
        double kd, kw;
        GetHeatTransferCoefficient(airWaterSurfaceRatio, coreArea, equivalentFinRadius, equivalentDiameter,
          waterPath, finThickness, thermalConductivity, innerDiameter, outerDiameter, AirFlowRate,
          InletAirTemperature, InletAirHumidityRatio, BorderRelativeHumidity, WaterFlowRate,
          InletWaterTemperature, out kd, out kw);
        DryHeatTransferCoefficient = kd;
        WetHeatTransferCoefficient = kw;
      }
      else
      {
        //簡易モデル
        double kd, kw;
        //水速と風速を計算//風量と水量に比例
        double velocity = (AirFlowRate / RatedAirFlowRate) * ratedVelocity;
        double waterSpeed = (WaterFlowRate / RatedWaterFlowRate) * ratedWaterSpeed;
        GetHeatTransferCoefficient(waterSpeed, velocity, out kd, out kw);
        DryHeatTransferCoefficient = kd;
        WetHeatTransferCoefficient = kw;
      }

      //出口状態を計算
      double ta, xa, tw, dr;
      GetOutletState(InletAirTemperature, InletAirHumidityRatio, BorderRelativeHumidity,
        InletWaterTemperature, AirFlowRate, WaterFlowRate, DryHeatTransferCoefficient,
        WetHeatTransferCoefficient, SurfaceArea * DegradationFactor, out ta, out xa, out tw, out dr);
      OutletAirTemperature = ta;
      OutletAirHumidityRatio = xa;
      OutletWaterTemperature = tw;
      DryRate = dr;
    }

    /// <summary>Controls the outlet air temperature to the given setpoint by adjusting the water flow rate.</summary>
    /// <param name="inletAirTemperature">Inlet air dry-bulb temperature [°C].</param>
    /// <param name="inletAirHumidityRatio">Inlet air humidity ratio [kg/kg].</param>
    /// <param name="inletWaterTemperature">Inlet water temperature [°C].</param>
    /// <param name="airFlowRate">Air mass flow rate [kg/s].</param>
    /// <param name="outletAirTemperatureSetpoint">Outlet air dry-bulb temperature setpoint [°C].</param>
    /// <returns>True if control to the setpoint is achievable; false if overloaded.</returns>
    public bool ControlOutletAirTemperature(double inletAirTemperature, double inletAirHumidityRatio,
      double inletWaterTemperature, double airFlowRate, double outletAirTemperatureSetpoint)
    {
      //入力値を保存
      InletAirTemperature = inletAirTemperature;
      InletAirHumidityRatio = inletAirHumidityRatio;
      InletWaterTemperature = inletWaterTemperature;
      AirFlowRate = airFlowRate;

      //冷却・加熱の判定
      bool isCooling = (inletWaterTemperature < inletAirTemperature);

      //冷却・加熱不要の場合
      if (isCooling && inletAirTemperature < outletAirTemperatureSetpoint + 1e-3 ||
        !isCooling && outletAirTemperatureSetpoint < inletAirTemperature + 1e-3)
      {
        ShutOff();
        return false;
      }

      //最大水量で成り行き出口温度を計算
      UpdateOutletState
        (InletAirTemperature, InletAirHumidityRatio, InletWaterTemperature, AirFlowRate, MaxWaterFlowRate);

      //過負荷の場合には最大能力での成り行き状態を出力
      if ((isCooling && (outletAirTemperatureSetpoint < OutletAirTemperature))
        || (!isCooling && (OutletAirTemperature < outletAirTemperatureSetpoint)))
        return false;

      //負荷が処理可能な場合は水量をBrent法で収束計算
      //誤差関数を定義
      Roots.ErrorFunction eFnc = delegate (double wFlow)
      {
        UpdateOutletState
        (InletAirTemperature, InletAirHumidityRatio, InletWaterTemperature, AirFlowRate, wFlow);
        return outletAirTemperatureSetpoint - OutletAirTemperature;
      };
      double wf = Roots.Brent(0, MaxWaterFlowRate, MaxWaterFlowRate * 0.001, eFnc);
      UpdateOutletState(InletAirTemperature, InletAirHumidityRatio, InletWaterTemperature, AirFlowRate, wf);
      OutletAirTemperature = outletAirTemperatureSetpoint;
      return true;
    }

    /// <summary>Shuts off the heat exchanger.</summary>
    public void ShutOff()
    {
      OutletAirTemperature = InletAirTemperature;
      OutletAirHumidityRatio = InletAirHumidityRatio;
      OutletWaterTemperature = InletWaterTemperature;
      WaterFlowRate = 0;
      DryRate = 1;
    }

    #endregion

    #region staticメソッド

    /// <summary>Computes the outlet air and water states for the given inlet conditions.</summary>
    /// <param name="inletAirTemperature">Inlet air dry-bulb temperature [°C].</param>
    /// <param name="inletAirHumidityRatio">Inlet air humidity ratio [kg/kg].</param>
    /// <param name="borderRelativeHumidity">Relative humidity at the dry/wet coil boundary [%].</param>
    /// <param name="inletWaterTemperature">Inlet water temperature [°C].</param>
    /// <param name="airFlowRate">Air mass flow rate [kg/s].</param>
    /// <param name="waterFlowRate">Water flow rate [kg/s].</param>
    /// <param name="dryHeatTransferCoefficient">Overall heat transfer coefficient for the dry section [kW/(m²·K)].</param>
    /// <param name="wetHeatTransferCoefficient">Overall heat transfer coefficient for the wet section [kW/(m²·(kJ/kg))].</param>
    /// <param name="surfaceArea">Surface area [m²].</param>
    /// <param name="outletAirTemperature">Output: outlet air dry-bulb temperature [°C].</param>
    /// <param name="outletAirHumidityRatio">Output: outlet air humidity ratio [kg/kg].</param>
    /// <param name="outletWaterTemperature">Output: outlet water temperature [°C].</param>
    /// <param name="dryRate">Output: dry coil fraction [-].</param>
    public static void GetOutletState
      (double inletAirTemperature, double inletAirHumidityRatio, double borderRelativeHumidity,
      double inletWaterTemperature, double airFlowRate, double waterFlowRate,
      double dryHeatTransferCoefficient, double wetHeatTransferCoefficient, double surfaceArea,
      out double outletAirTemperature, out double outletAirHumidityRatio,
      out double outletWaterTemperature, out double dryRate)
    {
      //熱媒流量が0の場合は出口状態=入口状態
      if (airFlowRate <= 0 || waterFlowRate <= 0 ||
        inletWaterTemperature == inletAirTemperature)
      {
        outletAirTemperature = inletAirTemperature;
        outletAirHumidityRatio = inletAirHumidityRatio;
        outletWaterTemperature = inletWaterTemperature;
        dryRate = 1;
        return;
      }

      //水と湿り空気の熱容量流量[kW/s]の計算
      double cpma = MoistAir.GetSpecificHeat(inletAirHumidityRatio);
      double mca = airFlowRate * cpma;
      double mcw = waterFlowRate * 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat;

      //加熱コイルの場合
      if (inletAirTemperature < inletWaterTemperature)
      {
        dryRate = 1.0;

        double mcMin = Math.Min(mcw, mca);
        double mcMax = Math.Max(mcw, mca);
        double ntu = dryHeatTransferCoefficient * surfaceArea / mcMin;

        //対向流の熱通過有効度[-]の計算
        double eff = HeatExchange.GetEffectiveness(ntu, mcMin / mcMax, HeatExchange.FlowType.CounterFlow);

        //交換熱量・出口状態を計算
        double q = eff * mcMin * (inletWaterTemperature - inletAirTemperature);
        outletAirTemperature = inletAirTemperature + q / mca;
        outletWaterTemperature = inletWaterTemperature - q / mcw;
        outletAirHumidityRatio = inletAirHumidityRatio;
      }
      //冷却コイルの場合
      else
      {
        //乾湿境界での空気温度[C]の計算
        double ba = MoistAir.GetDryBulbTemperatureFromHumidityRatioAndRelativeHumidity
          (inletAirHumidityRatio, borderRelativeHumidity, PhysicsConstants.StandardAtmosphericPressure);

        //入口空気のエンタルピー[kJ/kg]の計算
        double iAirEnthalpy = MoistAir.GetEnthalpyFromDryBulbTemperatureAndHumidityRatio
          (inletAirTemperature, inletAirHumidityRatio);

        //エンタルピー近似係数の計算
        double a, b;
        GetSaturationEnthalpyCoefficients(inletWaterTemperature, out a, out b);

        double xd = 1 / mca;
        double yd = -1 / mcw;
        double xw = 1 / airFlowRate;
        double yw = -a / mcw;

        double v2, v3, v4, bWaterTemp, bAirTemp;
        v2 = v3 = v4 = bWaterTemp = bAirTemp = 0;

        Roots.ErrorFunction eFnc = delegate (double dRate)
        {
          double zd = Math.Exp(dryHeatTransferCoefficient * surfaceArea * dRate * (xd + yd));
          double wd = zd * xd + yd;
          double v1 = xd * (zd - 1) / wd;
          v2 = zd * (xd + yd) / wd;

          double zw = Math.Exp(wetHeatTransferCoefficient * surfaceArea * (1 - dRate) * (xw + yw));
          double ww = zw * xw + yw;
          v3 = (xw + yw) / ww;
          v4 = xw * (zw - 1) / ww;
          double v5 = zw * (xw + yw) / ww;
          double v6 = yw * (1 - zw) / ww / a;

          //乾湿境界の水温[C]の計算
          bWaterTemp = (v5 * inletWaterTemperature
          + v6 * (iAirEnthalpy - v1 * cpma * inletAirTemperature - b)) / (1 - v1 * v6 * cpma);
          //乾湿境界での空気状態の計算
          bAirTemp = inletAirTemperature - v1 * (inletAirTemperature - bWaterTemp);

          //誤差の評価
          return ba - bAirTemp;
        };
        //結露が生じる場合には乾きコイル面積比を収束計算
        dryRate = 1.0;
        if (0 < eFnc(dryRate)) dryRate = Roots.Brent(0, 1, 0.0001, eFnc);

        //出口水温[C]の計算
        outletWaterTemperature = inletAirTemperature - v2 * (inletAirTemperature - bWaterTemp);
        double bAirEnthalpy = cpma * (bAirTemp - inletAirTemperature) + iAirEnthalpy;
        //出口空気状態の計算
        double iWaterEnthalpy = a * inletWaterTemperature + b;
        double oAirEnthalpy = v3 * bAirEnthalpy + v4 * iWaterEnthalpy;
        if (dryRate < 1.0)
          outletAirHumidityRatio = MoistAir.GetHumidityRatioFromEnthalpyAndRelativeHumidity
            (oAirEnthalpy, borderRelativeHumidity, PhysicsConstants.StandardAtmosphericPressure);
        else outletAirHumidityRatio = inletAirHumidityRatio;
        outletAirTemperature = MoistAir.GetDryBulbTemperatureFromHumidityRatioAndEnthalpy
          (outletAirHumidityRatio, oAirEnthalpy);
      }
    }

    /// <summary>Computes the required water flow rate [kg/s] to achieve the target outlet air temperature.</summary>
    /// <param name="inletAirTemperature">Inlet air dry-bulb temperature [°C].</param>
    /// <param name="inletAirHumidityRatio">Inlet air humidity ratio [kg/kg].</param>
    /// <param name="borderRelativeHumidity">Relative humidity at the dry/wet coil boundary [%].</param>
    /// <param name="inletWaterTemperature">Inlet water temperature [°C].</param>
    /// <param name="velocity">Air face velocity [m/s].</param>
    /// <param name="ratedWaterSpeed">Nominal water velocity inside tubes [m/s].</param>
    /// <param name="airFlowRate">Air mass flow rate [kg/s].</param>
    /// <param name="ratedWaterFlowRate">Nominal water flow rate [kg/s].</param>
    /// <param name="maxWaterFlowRate">Maximum water flow rate [kg/s].</param>
    /// <param name="surfaceArea">Heat transfer surface area [m²].</param>
    /// <param name="outletAirTemperatureSetpoint">Outlet air dry-bulb temperature setpoint [°C].</param>
    /// <returns>Required water flow rate [kg/s].</returns>
    public static double GetWaterFlowRate(
      double inletAirTemperature, double inletAirHumidityRatio,
      double borderRelativeHumidity, double inletWaterTemperature,
      double velocity, double ratedWaterSpeed,
      double airFlowRate, double ratedWaterFlowRate, double maxWaterFlowRate,
      double surfaceArea, double outletAirTemperatureSetpoint)
    {
      //冷却・加熱の判定
      bool isCooling = (inletWaterTemperature < inletAirTemperature);

      //冷却・加熱不要の場合
      if (isCooling && inletAirTemperature < outletAirTemperatureSetpoint
        || !isCooling && outletAirTemperatureSetpoint < inletAirTemperature) return 0;

      double wc = ratedWaterSpeed / ratedWaterFlowRate;

      //最大水量で成り行き出口温度を計算
      double oat, oah, owt, dr, kd, kw;
      GetHeatTransferCoefficient(wc * maxWaterFlowRate, velocity, out kd, out kw);
      GetOutletState(inletAirTemperature, inletAirHumidityRatio, borderRelativeHumidity,
        inletWaterTemperature, airFlowRate, maxWaterFlowRate, kd, kw, surfaceArea,
        out oat, out oah, out owt, out dr);

      //過負荷の場合には最大水量を出力
      if ((isCooling && (outletAirTemperatureSetpoint < oat))
        || (!isCooling && (oat < outletAirTemperatureSetpoint)))
        return maxWaterFlowRate;

      //負荷が処理可能な場合は水量をBrent法で収束計算
      //誤差関数を定義
      Roots.ErrorFunction eFnc = delegate (double wFlow)
      {
        GetHeatTransferCoefficient(wc * wFlow, velocity, out kd, out kw);
        GetOutletState(inletAirTemperature, inletAirHumidityRatio, borderRelativeHumidity,
          inletWaterTemperature, airFlowRate, wFlow, kd, kw, surfaceArea,
          out oat, out oah, out owt, out dr);
        return outletAirTemperatureSetpoint - oat;
      };
      return Roots.Brent(0, maxWaterFlowRate, 0.01, eFnc);
    }

    /// <summary>Computes the required water flow rate [kg/s] to achieve the target outlet air temperature.</summary>
    /// <param name="airWaterSurfaceRatio">Air-side to water-side surface area ratio [-].</param>
    /// <param name="coreArea">Coil face area [m²].</param>
    /// <param name="equivalentFinRadius">Equivalent annular fin outer radius [m].</param>
    /// <param name="equivalentDiameter">Equivalent hydraulic diameter [m].</param>
    /// <param name="waterPath">Number of parallel water flow paths [-].</param>
    /// <param name="finThickness">Fin thickness [m].</param>
    /// <param name="thermalConductivity">Thermal conductivity of the fin material [W/(m·K)].</param>
    /// <param name="innerDiameter">Tube inner diameter [m].</param>
    /// <param name="outerDiameter">Tube outer diameter [m].</param>
    /// <param name="airFlowRate">Air mass flow rate [kg/s].</param>
    /// <param name="inletAirTemperature">Inlet air dry-bulb temperature [°C].</param>
    /// <param name="inletAirHumidityRatio">Inlet air humidity ratio [kg/kg].</param>
    /// <param name="borderRelativeHumidity">Relative humidity at the dry/wet coil boundary [%].</param>
    /// <param name="waterFlowRate">Water flow rate [kg/s].</param>
    /// <param name="inletWaterTemperature">Inlet water temperature [°C].</param>
    /// <param name="maxWaterFlowRate">Maximum water flow rate [kg/s].</param>
    /// <param name="surfaceArea">Heat transfer surface area [m²].</param>
    /// <param name="outletAirTemperatureSetpoint">Outlet air dry-bulb temperature setpoint [°C].</param>
    /// <returns>Required water flow rate [kg/s].</returns>
    public static double GetWaterFlowRate(
      double airWaterSurfaceRatio, double coreArea, double equivalentFinRadius,
      double equivalentDiameter, double waterPath, double finThickness,
      double thermalConductivity, double innerDiameter, double outerDiameter,
      double airFlowRate, double inletAirTemperature, double inletAirHumidityRatio,
      double borderRelativeHumidity, double waterFlowRate, double inletWaterTemperature,
      double maxWaterFlowRate, double surfaceArea, double outletAirTemperatureSetpoint)
    {
      //冷却・加熱の判定
      bool isCooling = (inletWaterTemperature < inletAirTemperature);

      //冷却・加熱不要の場合
      if (isCooling && inletAirTemperature < outletAirTemperatureSetpoint
        || !isCooling && outletAirTemperatureSetpoint < inletAirTemperature) return 0;

      //最大水量で成り行き出口温度を計算
      double oat, oah, owt, dr, kd, kw;
      GetHeatTransferCoefficient(airWaterSurfaceRatio, coreArea, equivalentFinRadius,
        equivalentDiameter, waterPath, finThickness, thermalConductivity,
        innerDiameter, outerDiameter, airFlowRate, inletAirHumidityRatio,
        inletAirHumidityRatio, borderRelativeHumidity, maxWaterFlowRate,
        inletWaterTemperature, out kd, out kw);
      GetOutletState(inletAirTemperature, inletAirHumidityRatio,
        borderRelativeHumidity, inletWaterTemperature,
        airFlowRate, maxWaterFlowRate, kd, kw, surfaceArea,
        out oat, out oah, out owt, out dr);

      //過負荷の場合には最大水量を出力
      if ((isCooling && (outletAirTemperatureSetpoint < oat))
        || (!isCooling && (oat < outletAirTemperatureSetpoint)))
        return maxWaterFlowRate;

      //負荷が処理可能な場合は水量をBrent法で収束計算
      //誤差関数を定義
      Roots.ErrorFunction eFnc = delegate (double wFlow)
      {
        GetHeatTransferCoefficient(airWaterSurfaceRatio, coreArea, equivalentFinRadius,
          equivalentDiameter, waterPath, finThickness, thermalConductivity,
          innerDiameter, outerDiameter, airFlowRate, inletAirHumidityRatio,
          inletAirHumidityRatio, borderRelativeHumidity, wFlow,
          inletWaterTemperature, out kd, out kw);
        GetOutletState(inletAirTemperature, inletAirHumidityRatio, borderRelativeHumidity,
          inletWaterTemperature, airFlowRate, wFlow, kd, kw, surfaceArea,
          out oat, out oah, out owt, out dr);
        return outletAirTemperatureSetpoint - oat;
      };
      return Roots.Brent(0, maxWaterFlowRate, 0.01, eFnc);
    }

    /// <summary>Computes the linearisation coefficients for saturation enthalpy as a function of dry-bulb temperature.</summary>
    /// <param name="drybulbTemperature">Dry-bulb temperature [°C].</param>
    /// <param name="a">Coefficient for the dry-bulb temperature term.</param>
    /// <param name="b">Intercept of the linearised saturation enthalpy equation.</param>
    private static void GetSaturationEnthalpyCoefficients
      (double drybulbTemperature, out double a, out double b)
    {
      const double DELTA = 0.001;
      double hws1 = MoistAir.GetSaturationEnthalpyFromDryBulbTemperature
        (drybulbTemperature, PhysicsConstants.StandardAtmosphericPressure);
      double hws2 = MoistAir.GetSaturationEnthalpyFromDryBulbTemperature
        (drybulbTemperature + DELTA, PhysicsConstants.StandardAtmosphericPressure);
      a = (hws2 - hws1) / DELTA;
      b = hws1 - a * drybulbTemperature;
    }

    /// <summary>Computes the air-side heat transfer surface area [m²].</summary>
    /// <param name="inletAirTemperature">Inlet air dry-bulb temperature [°C].</param>
    /// <param name="inletAirHumidityRatio">Inlet air humidity ratio [kg/kg].</param>
    /// <param name="borderRelativeHumidity">Dry/wet boundary relative humidity [%].</param>
    /// <param name="inletWaterTemperature">Inlet water temperature [°C].</param>
    /// <param name="airFlowRate">Air mass flow rate [kg/s].</param>
    /// <param name="waterFlowRate">Water flow rate [kg/s].</param>
    /// <param name="heatTransfer">Heat transfer capacity [kW].</param>
    /// <param name="dryHeatTransferCoefficient">Overall heat transfer coefficient for the dry section [kW/(m²·K)].</param>
    /// <param name="wetHeatTransferCoefficient">Overall heat transfer coefficient for the wet section [kW/(m²·(kJ/kg))].</param>
    /// <returns>Air-side heat transfer surface area [m²].</returns>
    public static double GetSurfaceArea
      (double inletAirTemperature, double inletAirHumidityRatio, double borderRelativeHumidity,
      double inletWaterTemperature, double airFlowRate, double waterFlowRate, double heatTransfer,
      double dryHeatTransferCoefficient, double wetHeatTransferCoefficient)
    {

      //水と湿り空気の熱容量流量[kW/s]の計算
      double cpma = MoistAir.GetSpecificHeat(inletAirHumidityRatio);
      double mca = airFlowRate * cpma;
      double mcw = waterFlowRate * 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat;

      //加熱コイルの場合
      if (inletAirTemperature < inletWaterTemperature)
      {
        //NTU値の計算
        double mcMin = Math.Min(mcw, mca);
        double mcMax = Math.Max(mcw, mca);

        //熱通過有効度[-]の計算
        double eff = heatTransfer / mcMin / (inletWaterTemperature - inletAirTemperature);
        double ntu = HeatExchange.GetNTU(eff, mcMin / mcMax, HeatExchange.FlowType.CounterFlow);

        return ntu * mcMin / dryHeatTransferCoefficient;
      }
      //冷却コイルの場合
      else
      {
        //冷水出口温度[C]の計算
        double oWaterTemp = inletWaterTemperature + heatTransfer / mcw;

        //空気出入口エンタルピー[kJ/kg]の計算
        double iAirEnthalpy = MoistAir.GetEnthalpyFromDryBulbTemperatureAndHumidityRatio
          (inletAirTemperature, inletAirHumidityRatio);
        double oAirEnthalpy = iAirEnthalpy - heatTransfer / airFlowRate;

        //コイルの乾湿境界の計算
        double oAirHumidRatio = MoistAir.GetHumidityRatioFromEnthalpyAndRelativeHumidity
          (oAirEnthalpy, borderRelativeHumidity, PhysicsConstants.StandardAtmosphericPressure);

        //乾きコイルのみの場合
        if (inletAirHumidityRatio < oAirHumidRatio)
        {
          double oAirTemp = inletAirTemperature - heatTransfer / mca;
          double d1 = inletAirTemperature - oWaterTemp;
          double d2 = oAirTemp - inletWaterTemperature;
          double lmtd = (d1 - d2) / Math.Log(d1 / d2);
          return heatTransfer / lmtd / dryHeatTransferCoefficient;
        }
        //乾き+湿りコイルの場合
        else
        {
          //境界点での湿り空気状態の計算
          double bAirTemp =
            MoistAir.GetDryBulbTemperatureFromHumidityRatioAndRelativeHumidity
            (inletAirHumidityRatio, borderRelativeHumidity, PhysicsConstants.StandardAtmosphericPressure);
          double bAirEnthalpy =
            MoistAir.GetEnthalpyFromDryBulbTemperatureAndHumidityRatio(bAirTemp, inletAirHumidityRatio);

          //境界点での水温[C]の計算
          double htWet = (bAirEnthalpy - oAirEnthalpy) * airFlowRate;
          double bWaterTemp = inletWaterTemperature + htWet / mcw;

          //水温と等しい温度の空気の飽和エンタルピー[kJ/(kg)]の計算
          double iWaterEnthalpy =
            MoistAir.GetSaturationEnthalpyFromDryBulbTemperature(inletWaterTemperature, PhysicsConstants.StandardAtmosphericPressure);
          double bWaterEnthalpy =
            MoistAir.GetSaturationEnthalpyFromDryBulbTemperature(bWaterTemp, PhysicsConstants.StandardAtmosphericPressure);

          //乾きコイルの表面積[m2]の計算
          double dt1 = inletAirTemperature - oWaterTemp;
          double dt2 = bAirTemp - bWaterTemp;
          double lmtd = (dt1 - dt2) / Math.Log(dt1 / dt2);
          double sd = (heatTransfer - htWet) / lmtd / dryHeatTransferCoefficient;

          double dh1 = bAirEnthalpy - bWaterEnthalpy;
          double dh2 = oAirEnthalpy - iWaterEnthalpy;
          double lmhd = (dh1 - dh2) / Math.Log(dh1 / dh2);
          double sw = htWet / lmhd / wetHeatTransferCoefficient;

          return sd + sw;
        }
      }
    }

    /// <summary>Computes the overall heat transfer coefficients (dry and wet).</summary>
    /// <param name="waterSpeed">Water velocity inside tubes [m/s].</param>
    /// <param name="velocity">Face velocity [m/s].</param>
    /// <param name="dryHeatTransferCoefficient">Overall heat transfer coefficient for the dry section [kW/(m²·K)].</param>
    /// <param name="wetHeatTransferCoefficient">Overall heat transfer coefficient for the wet section [kW/(m²·(kJ/kg))].</param>
    public static void GetHeatTransferCoefficient
      (double waterSpeed, double velocity,
      out double dryHeatTransferCoefficient, out double wetHeatTransferCoefficient)
    {
      dryHeatTransferCoefficient =
        1 / (4.72 + 4.91 * Math.Pow(waterSpeed, -0.8) + 26.7 * Math.Pow(velocity, -0.64));
      wetHeatTransferCoefficient =
        1 / (10.044 + 10.44 * Math.Pow(waterSpeed, -0.8) + 39.6 * Math.Pow(velocity, -0.64));
    }

    /// <summary>Computes the coil geometry (surface areas, diameters, fin efficiency).</summary>
    /// <param name="depth">Coil depth [m].</param>
    /// <param name="width">Coil width [m].</param>
    /// <param name="height">Coil height [m].</param>
    /// <param name="rowNumber">Number of tube columns (perpendicular to air flow).</param>
    /// <param name="columnNumber">Number of tube rows in the air-flow direction.</param>
    /// <param name="finPitch">Fin pitch [m].</param>
    /// <param name="finThickness">Fin thickness [m].</param>
    /// <param name="innerDiameter">Tube inner diameter [m].</param>
    /// <param name="outerDiameter">Tube outer diameter [m].</param>
    /// <param name="airWaterSurfaceRatio">Air-side to water-side surface area ratio [-].</param>
    /// <param name="coreArea">Coil face area [m²].</param>
    /// <param name="equivalentFinRadius">Equivalent annular fin outer radius [m].</param>
    /// <param name="equivalentDiameter">Equivalent hydraulic diameter [m].</param>
    /// <param name="surfaceArea">Air-side heat transfer surface area [m²].</param>
    public static void GetGeometricCompfigulation
      (double depth, double width, double height, int rowNumber, int columnNumber,
      double finPitch, double finThickness, double innerDiameter, double outerDiameter,
      out double airWaterSurfaceRatio, out double coreArea, out double equivalentFinRadius,
      out double equivalentDiameter, out double surfaceArea)
    {
      //空気側伝熱面積[m2]の計算
      double sf = 2 * (height * depth / rowNumber 
        - outerDiameter * outerDiameter / 4 * Math.PI * columnNumber) * width / finPitch;
      double sto = outerDiameter * Math.PI * columnNumber * width * (1 - finThickness / finPitch);
      surfaceArea = sf + sto;

      //水側伝熱面積[m2]の計算
      double wSurface = innerDiameter * Math.PI * columnNumber * width;

      //空気側・水側伝熱面積比[-]の計算
      airWaterSurfaceRatio = surfaceArea / wSurface;

      //コア面積[m2]の計算
      coreArea = (width * height - outerDiameter * width * columnNumber) * (1 - finThickness / finPitch);

      //環状フィンの相当半径[m]の計算
      equivalentFinRadius = Math.Sqrt((depth / rowNumber) * (height / columnNumber) / Math.PI);

      //等価直径[m]の計算
      equivalentDiameter = 4 * coreArea / (surfaceArea * rowNumber / depth);
    }

    /// <summary>Computes the overall heat transfer coefficients (dry and wet).</summary>
    /// <param name="airWaterSurfaceRatio">Air-side to water-side surface area ratio [-].</param>
    /// <param name="coreArea">Coil face area [m²].</param>
    /// <param name="equivalentFinRadius">Equivalent annular fin outer radius [m].</param>
    /// <param name="equivalentDiameter">Equivalent hydraulic diameter [m].</param>
    /// <param name="waterPath">Number of parallel water flow paths [-].</param>
    /// <param name="finThickness">Fin thickness [m].</param>
    /// <param name="thermalConductivity">Thermal conductivity of the fin material [W/(m·K)].</param>
    /// <param name="innerDiameter">Tube inner diameter [m].</param>
    /// <param name="outerDiameter">Tube outer diameter [m].</param>
    /// <param name="airFlowRate">Air mass flow rate [kg/s].</param>
    /// <param name="inletAirTemperature">Inlet air dry-bulb temperature [°C].</param>
    /// <param name="inletAirHumidityRatio">Inlet air humidity ratio [kg/kg].</param>
    /// <param name="borderRelativeHumidity">Relative humidity at the dry/wet coil boundary [%].</param>
    /// <param name="waterFlowRate">Water flow rate [kg/s].</param>
    /// <param name="inletWaterTemperature">Inlet water temperature [°C].</param>
    /// <param name="dryHeatTransferCoefficient">Overall heat transfer coefficient for the dry section [kW/(m²·K)].</param>
    /// <param name="wetHeatTransferCoefficient">Overall heat transfer coefficient for the wet section [kW/(m²·(kJ/kg))].</param>
    public static void GetHeatTransferCoefficient
      (double airWaterSurfaceRatio, double coreArea, double equivalentFinRadius,
      double equivalentDiameter, double waterPath, double finThickness,
      double thermalConductivity, double innerDiameter, double outerDiameter,
      double airFlowRate, double inletAirTemperature, double inletAirHumidityRatio,
      double borderRelativeHumidity, double waterFlowRate, double inletWaterTemperature,
      out double dryHeatTransferCoefficient, out double wetHeatTransferCoefficient)
    {
      //湿り空気物性の計算//比熱[kJ/kgK]・動粘性係数[m2/s]・熱伝導率[W/(mK)]
      //比体積[kg/m3]・拡散係数[m2/s]
      double cpma = MoistAir.GetSpecificHeat(inletAirHumidityRatio);
      double dVis = MoistAir.GetDynamicViscosity
        (inletAirTemperature, inletAirHumidityRatio, PhysicsConstants.StandardAtmosphericPressure);
      double tCond = MoistAir.GetThermalConductivity(inletAirTemperature);
      double sVol = MoistAir.GetSpecificVolumeFromDryBulbTemperatureAndHumidityRatio
        (inletAirTemperature, inletAirHumidityRatio, PhysicsConstants.StandardAtmosphericPressure);
      double difc = MoistAir.GetThermalDiffusivity
        (inletAirTemperature, inletAirHumidityRatio, PhysicsConstants.StandardAtmosphericPressure);

      //実風速の計算[m/s]
      double coreVelocity = airFlowRate * AIR_SPECIFIC_WEIGHT / coreArea;

      //レイノルズ数[-]の計算
      double re = coreVelocity * equivalentDiameter / dVis;

      //水側の対流熱伝達率[W/(m2K)]の計算
      double wfCoefficient = FluidCircuit.WaterPipe.GetInsideHeatTransferCoefficient
        (inletWaterTemperature, innerDiameter, waterFlowRate / waterPath);

      //乾き部分の計算////
      //空気側対流熱伝達率[W/(m2K)]の計算
      double afd = 0.129 * tCond / equivalentDiameter * Math.Pow(re, 0.64);

      //フィン効率[-]の計算
      double fEfficiencyD = HeatExchange.GetCircularFinEfficiency
        (outerDiameter / 2, equivalentFinRadius, finThickness, afd, thermalConductivity);

      //熱貫流率[kW/(m2K)]の計算
      dryHeatTransferCoefficient = 0.001 / (airWaterSurfaceRatio / wfCoefficient 
        + 1 / (afd * (fEfficiencyD + 1 / airWaterSurfaceRatio)));

      //湿り部分の計算////
      //フィン表面の物質移動係数[W/(m2(kJ/kg))]の計算
      double kf = 37.2 * difc / (sVol * equivalentDiameter) * Math.Pow(re, 0.8);

      //エンタルピー近似係数の計算
      double a, b;
      GetSaturationEnthalpyCoefficients(inletWaterTemperature, out a, out b);

      //フィン効率[-]の計算
      double lewis = 3.19 * Math.Pow(re, -0.16);
      double afw = a / (cpma * lewis) * afd;
      double fEfficiencyW = HeatExchange.GetCircularFinEfficiency
        (outerDiameter / 2, equivalentFinRadius, finThickness, afw, thermalConductivity);

      //熱貫流率[kW/(m2(kJ/kg))]の計算
      wetHeatTransferCoefficient = 0.001 / (a * airWaterSurfaceRatio / wfCoefficient 
        + 1 / (kf * (fEfficiencyW + 1 / airWaterSurfaceRatio)));
    }

    #endregion

  }
}
