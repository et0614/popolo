/* WaterHeatPump.cs
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

using Popolo.Core.Physics;

namespace Popolo.Core.HVAC.HeatSource
{
  /// <summary>Water-source heat pump (model reference: https://doi.org/10.3130/aije.82.453).</summary>
  public class WaterHeatPump : IReadOnlyWaterHeatPump
  {

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

    #region 定数宣言

    /// <summary>Minimum partial load ratio.</summary>
    private const double MIN_PLOAD = 0.5;

    /// <summary>Minimum COP at very low partial load.</summary>
    private const double MIN_COP = 0.5;

    /// <summary>Power input ratio characteristic coefficients for cooling mode.</summary>
    private readonly double[] cAc = new double[5];
   
    /// <summary>Power input ratio characteristic coefficients for heating mode.</summary>
    private readonly double[] cAh = new double[5];

    /// <summary>Maximum capacity ratio characteristic coefficients for cooling mode.</summary>
    private readonly double[] cBc = new double[] { 0.0313, -0.0100 };

    /// <summary>Maximum capacity ratio characteristic coefficients for heating mode.</summary>
    private readonly double[] cBh = new double[] { 0.0247, -0.0023 };

    #endregion
    
    #region プロパティ
    
    /// <summary>Gets the current operating mode.</summary>
    public OperatingMode Mode { get; private set; }

    /// <summary>Gets a value indicating whether the unit is overloaded.</summary>
    public bool IsOverLoad { get; private set; }

    /// <summary>Gets the nominal cooling capacity [kW].</summary>
    public double NominalCoolingCapacity { get; private set; }

    /// <summary>Gets the nominal chilled water mass flow rate [kg/s].</summary>
    public double NominalChilledWaterFlowRate { get; private set; }

    /// <summary>Gets the nominal cooling water mass flow rate [kg/s].</summary>
    public double NominalCoolingWaterFlowRate { get; private set; }

    /// <summary>Gets the nominal power consumption in cooling mode [kW].</summary>
    public double NominalCoolingEnergyConsumption { get; private set; }

    /// <summary>Gets the nominal heating capacity [kW].</summary>
    public double NominalHeatingCapacity { get; private set; }

    /// <summary>Gets the nominal hot water mass flow rate [kg/s].</summary>
    public double NominalHotWaterFlowRate { get; private set; }

    /// <summary>Gets the nominal heat-source water mass flow rate [kg/s].</summary>
    public double NominalHeatSourceWaterFlowRate { get; private set; }

    /// <summary>Gets the nominal power consumption in heating mode [kW].</summary>
    public double NominalHeatingEnergyConsumption { get; private set; }

    /// <summary>Gets the maximum cooling capacity [kW] at current conditions.</summary>
    public double MaxCoolingCapacity { get; private set; }

    /// <summary>Gets the cooling load [kW].</summary>
    public double CoolingLoad { get; private set; }

    /// <summary>Gets the chilled water mass flow rate [kg/s].</summary>
    public double ChilledWaterFlowRate { get; private set; }

    /// <summary>Gets the cooling water mass flow rate [kg/s].</summary>
    public double CoolingWaterFlowRate { get; private set; }

    /// <summary>Gets or sets the chilled water outlet temperature setpoint [°C].</summary>
    public double ChilledWaterSetpoint { get; set; }

    /// <summary>Gets the chilled water inlet temperature [°C].</summary>
    public double ChilledWaterInletTemperature { get; private set; }

    /// <summary>Gets the chilled water outlet temperature [°C].</summary>
    public double ChilledWaterOutletTemperature { get; private set; }

    /// <summary>Gets the cooling water inlet temperature [°C].</summary>
    public double CoolingWaterInletTemperature { get; private set; }

    /// <summary>Gets the cooling water outlet temperature [°C].</summary>
    public double CoolingWaterOutletTemperature { get; private set; }

    /// <summary>Gets the maximum heating capacity [kW] at current conditions.</summary>
    public double MaxHeatingCapacity { get; private set; }

    /// <summary>Gets the heating load [kW].</summary>
    public double HeatingLoad { get; private set; }

    /// <summary>Gets the hot water mass flow rate [kg/s].</summary>
    public double HotWaterFlowRate { get; private set; }

    /// <summary>Gets the heat-source water mass flow rate [kg/s].</summary>
    public double HeatSourceWaterFlowRate { get; private set; }

    /// <summary>Gets or sets the hot water outlet temperature setpoint [°C].</summary>
    public double HotWaterSetpoint { get; set; }

    /// <summary>Gets the hot water inlet temperature [°C].</summary>
    public double HotWaterInletTemperature { get; private set; }

    /// <summary>Gets the hot water outlet temperature [°C].</summary>
    public double HotWaterOutletTemperature { get; private set; }

    /// <summary>Gets the heat-source water inlet temperature [°C].</summary>
    public double HeatSourceWaterInletTemperature { get; private set; }

    /// <summary>Gets the heat-source water outlet temperature [°C].</summary>
    public double HeatSourceWaterOutletTemperature { get; private set; }

    /// <summary>Gets the current electric power consumption [kW].</summary>
    public double EnergyConsumption { get; private set; }

    /// <summary>Gets the coefficient of performance [-].</summary>
    public double COP
    {
      get
      {
        if (Mode == OperatingMode.Cooling) return CoolingLoad / EnergyConsumption;
        else if (Mode == OperatingMode.Heating) return HeatingLoad / EnergyConsumption;
        else return 0;
      }
    }

    #endregion

    #region インスタンス変数

    /// <summary>Theoretical COP under rated cooling conditions.</summary>
    private readonly double copN_C;

    /// <summary>Theoretical COP under rated heating conditions.</summary>
    private readonly double copN_H;

    /// <summary>Rated chilled water outlet temperature [K].</summary>
    private readonly double tEvpoN_C;

    /// <summary>Rated cooling water outlet temperature [K].</summary>
    private readonly double tCndoN_C;

    /// <summary>Rated heat-source water outlet temperature [K].</summary>
    private readonly double tEvpoN_H;

    /// <summary>Rated hot water outlet temperature [K].</summary>
    private readonly double tCndoN_H;

    #endregion

    #region コンストラクタ

    /// <summary>Initializes a new instance from rated cooling conditions.</summary>
    /// <param name="coolingCapacity">Nominal cooling capacity [kW].</param>
    /// <param name="chilledWaterFlowRate">Chilled water mass flow rate [kg/s].</param>
    /// <param name="coolingWaterFlowRate">Cooling water mass flow rate [kg/s].</param>
    /// <param name="chilledWaterOutletTemperature">Chilled water outlet temperature [°C].</param>
    /// <param name="coolingWaterInletTemperature">Cooling water inlet temperature [°C].</param>
    /// <param name="coolingEnergyConsumption">Nominal power consumption in cooling mode [kW].</param>
    /// <param name="heatingCapacity">Nominal heating capacity [kW].</param>
    /// <param name="hotWaterFlowRate">Hot water mass flow rate [kg/s].</param>
    /// <param name="heatSourceWaterFlowRate">Heat-source water mass flow rate [kg/s].</param>
    /// <param name="hotWaterOutletTemperature">Hot water outlet temperature [°C].</param>
    /// <param name="heatSourceWaterInletTemperature">Heat-source water inlet temperature [°C].</param>
    /// <param name="heatingEnergyConsumption">Nominal power consumption in heating mode [kW].</param>
    public WaterHeatPump
      (double coolingCapacity, double chilledWaterFlowRate, double coolingWaterFlowRate,
      double chilledWaterOutletTemperature, double coolingWaterInletTemperature, double coolingEnergyConsumption,
      double heatingCapacity, double hotWaterFlowRate, double heatSourceWaterFlowRate,
      double hotWaterOutletTemperature, double heatSourceWaterInletTemperature, double heatingEnergyConsumption)
    {
      //冷却能力関連の情報初期化
      NominalCoolingCapacity = coolingCapacity;
      NominalCoolingEnergyConsumption = coolingEnergyConsumption;
      NominalChilledWaterFlowRate = chilledWaterFlowRate;
      NominalCoolingWaterFlowRate = coolingWaterFlowRate;
      ChilledWaterSetpoint = ChilledWaterInletTemperature = ChilledWaterOutletTemperature = chilledWaterOutletTemperature;
      tEvpoN_C = PhysicsConstants.ToKelvin(chilledWaterOutletTemperature);
      tCndoN_C = (coolingCapacity + coolingEnergyConsumption) / (0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat * coolingWaterFlowRate)
        + PhysicsConstants.ToKelvin(coolingWaterInletTemperature);
      copN_C = tEvpoN_C / (tCndoN_C - tEvpoN_C);

      //加熱能力関連の情報初期化
      NominalHeatingCapacity = heatingCapacity;
      NominalHeatingEnergyConsumption = heatingEnergyConsumption;
      NominalHotWaterFlowRate = hotWaterFlowRate;
      NominalHeatSourceWaterFlowRate = heatSourceWaterFlowRate;
      HotWaterSetpoint = HotWaterInletTemperature = HotWaterOutletTemperature = hotWaterOutletTemperature;
      tEvpoN_H = - (heatingCapacity - heatingEnergyConsumption) / (0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat * heatSourceWaterFlowRate)
        + PhysicsConstants.ToKelvin(heatSourceWaterInletTemperature); 
      tCndoN_H = PhysicsConstants.ToKelvin(hotWaterOutletTemperature);
      copN_H = tCndoN_H / (tCndoN_H - tEvpoN_H);

      //特性係数初期化
      double[] asc = new double[] { -0.7970, 1.0536, -0.2289, 1.2119, -0.2397 };
      double[] ash = new double[] { -0.0685, 0.1965, -0.0150, 1.0006, -0.1136 };
      double copC = NominalCoolingCapacity / NominalCoolingEnergyConsumption;
      double copH = NominalHeatingCapacity / NominalHeatingEnergyConsumption;
      for (int i = 0; i < asc.Length; i++)
      {
        cAc[i] = asc[i] / copC;
        cAh[i] = ash[i] / copH;
      }

      ShutOff();
    }

    #endregion

    #region publicメソッド

    /// <summary>Updates the chiller state for cooling operation.</summary>
    /// <param name="chilledWaterFlowRate">Chilled water mass flow rate [kg/s].</param>
    /// <param name="coolingWaterFlowRate">Cooling water mass flow rate [kg/s].</param>
    /// <param name="chilledWaterInletTemperature">Chilled water inlet temperature [°C].</param>
    /// <param name="coolingWaterInletTemperature">Cooling water inlet temperature [°C].</param>
    public void CoolWater
      (double chilledWaterFlowRate, double coolingWaterFlowRate,
      double chilledWaterInletTemperature, double coolingWaterInletTemperature)
    {
      //負荷が無ければ停止
      double mcSpy = 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat * chilledWaterFlowRate;
      CoolingLoad = mcSpy * (chilledWaterInletTemperature - ChilledWaterSetpoint);
      if (CoolingLoad <= 0)
      {
        ShutOff();
        return;
      }

      //冷却水出口温度を計算
      double mcSce = 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat * coolingWaterFlowRate;
      double tScei = PhysicsConstants.ToKelvin(coolingWaterInletTemperature);
      double tSpyi = PhysicsConstants.ToKelvin(chilledWaterInletTemperature);
      double tSpyo = PhysicsConstants.ToKelvin(ChilledWaterSetpoint);

      double tSceo, maxQSpy;
      bool isOverLoad;
      CalcOutletTemperature
        (1, copN_C, tScei, tSpyi, ref tSpyo, tCndoN_C, tEvpoN_C, mcSce, mcSpy, CoolingLoad,
        NominalCoolingCapacity, cBc[1], cBc[0], cAc, out tSceo, out maxQSpy, out isOverLoad);
      if (isOverLoad) CoolingLoad = maxQSpy;

      //状態設定
      Mode = OperatingMode.Cooling;
      IsOverLoad = isOverLoad;
      ChilledWaterFlowRate = chilledWaterFlowRate;
      CoolingWaterFlowRate = coolingWaterFlowRate;
      ChilledWaterInletTemperature = chilledWaterInletTemperature;
      CoolingWaterInletTemperature = coolingWaterInletTemperature;
      MaxCoolingCapacity = maxQSpy;
      ChilledWaterOutletTemperature = PhysicsConstants.ToCelsius(tSpyo);
      CoolingWaterOutletTemperature = PhysicsConstants.ToCelsius(tSceo);
      EnergyConsumption = (mcSce * Math.Abs(tSceo - tScei)) - CoolingLoad;
      //極低負荷時のCOP補正
      double pLoad = CoolingLoad / MaxCoolingCapacity;
      //double pLoad = CoolingLoad / NominalCoolingCapacity; //論文中では上の定義にしたが、発停時に熱源水温度の違いが評価されないため、こちらも有効
      if (pLoad < MIN_PLOAD)
      {
        double minCOP = CoolingLoad / EnergyConsumption;
        double adjCOP = MIN_COP + pLoad * (minCOP - MIN_COP) / MIN_PLOAD;
        EnergyConsumption = CoolingLoad / adjCOP;
      }

      //加熱関連の情報初期化
      MaxHeatingCapacity = NominalHeatingCapacity;
      HeatingLoad = 0;
      HeatSourceWaterFlowRate = HotWaterFlowRate = 0.0;
      HeatSourceWaterOutletTemperature = HeatSourceWaterInletTemperature;
      HotWaterOutletTemperature = HotWaterInletTemperature;
    }

    /// <summary>Updates the heat pump state for heating operation.</summary>
    /// <param name="hotWaterFlowRate">Hot water mass flow rate [kg/s].</param>
    /// <param name="heatSourceWaterFlowRate">Heat-source water mass flow rate [kg/s].</param>
    /// <param name="hotWaterInletTemperature">Hot water inlet temperature [°C].</param>
    /// <param name="heatSourceWaterInletTemperature">Heat-source water inlet temperature [°C].</param>
    public void HeatWater
      (double hotWaterFlowRate, double heatSourceWaterFlowRate,
      double hotWaterInletTemperature, double heatSourceWaterInletTemperature)
    {
      //負荷が無ければ停止
      double mcSpy = 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat * hotWaterFlowRate;
      HeatingLoad = mcSpy * (HotWaterSetpoint - hotWaterInletTemperature);
      if (HeatingLoad <= 0)
      {
        ShutOff();
        return;
      }

      //冷却水出口温度を計算
      double mcSce = 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat * heatSourceWaterFlowRate;
      double tScei = PhysicsConstants.ToKelvin(heatSourceWaterInletTemperature);
      double tSpyi = PhysicsConstants.ToKelvin(hotWaterInletTemperature);
      double tSpyo = PhysicsConstants.ToKelvin(HotWaterSetpoint);

      double tSceo, maxQSpy;
      bool isOverLoad;
      CalcOutletTemperature
        (-1, copN_H, tScei, tSpyi, ref tSpyo, tEvpoN_H, tCndoN_H, mcSce, mcSpy, HeatingLoad,
        NominalHeatingCapacity, cBh[0], cBh[1], cAh, out tSceo, out maxQSpy, out isOverLoad);
      if (isOverLoad) HeatingLoad = maxQSpy;

      //状態設定
      Mode = OperatingMode.Heating;
      IsOverLoad = isOverLoad;
      HotWaterFlowRate = hotWaterFlowRate;
      HeatSourceWaterFlowRate = heatSourceWaterFlowRate;
      HotWaterInletTemperature = hotWaterInletTemperature;
      HeatSourceWaterInletTemperature = heatSourceWaterInletTemperature;
      MaxHeatingCapacity = maxQSpy;
      HotWaterOutletTemperature = PhysicsConstants.ToCelsius(tSpyo);
      HeatSourceWaterOutletTemperature = PhysicsConstants.ToCelsius(tSceo);
      EnergyConsumption = HeatingLoad - (mcSce * Math.Abs(tSceo - tScei));
      //極低負荷時のCOP補正
      double pLoad = HeatingLoad / MaxHeatingCapacity;
      //double pLoad = HeatingLoad / NominalHeatingCapacity;  //論文中では上の定義にしたが、発停時に熱源水温度の違いが評価されないため、こちらも有効
      if (pLoad < MIN_PLOAD)
      {
        double minCOP = HeatingLoad / EnergyConsumption;
        double adjCOP = MIN_COP + pLoad * (minCOP - MIN_COP) / MIN_PLOAD;
        EnergyConsumption = HeatingLoad / adjCOP;
      }

      //冷却関連の情報初期化
      MaxCoolingCapacity = NominalCoolingCapacity;
      CoolingLoad = 0;
      CoolingWaterFlowRate = ChilledWaterFlowRate = 0.0;
      CoolingWaterOutletTemperature = CoolingWaterInletTemperature;
      ChilledWaterOutletTemperature = ChilledWaterInletTemperature;
    }
    
    /// <summary>Computes the heat-source water outlet temperature [K].</summary>
    /// <param name="cf">Operation sign: +1 for cooling, -1 for heating.</param>
    /// <param name="copTN">Theoretical COP under rated conditions.</param>
    /// <param name="tScei">Heat-source water inlet temperature [K].</param>
    /// <param name="tSpyi">Supply-side inlet water temperature [K].</param>
    /// <param name="tSpyo">Supply-side outlet water temperature [K].</param>
    /// <param name="tSceoN">Heat-source water outlet temperature under rated conditions [K].</param>
    /// <param name="tSpyoN">Supply-side outlet water temperature under rated conditions [K].</param>
    /// <param name="mcSce">Heat-source water heat capacity rate [kW/K].</param>
    /// <param name="mcSpy">Supply-side heat capacity rate [kW/K].</param>
    /// <param name="qSpy">Supply-side heat transfer rate [kW].</param>
    /// <param name="qSpyN">Supply-side heat transfer rate under rated conditions [kW].</param>
    /// <param name="bSce">Maximum capacity correction coefficient 1 (heat-source water temperature).</param>
    /// <param name="bSpy">Maximum capacity correction coefficient 2 (supply-side water temperature).</param>
    /// <param name="alpha">Characteristic coefficient.</param>
    /// <param name="tSceo">Output: heat-source water outlet temperature [K].</param>
    /// <param name="maxQSpy">Output: maximum supply-side capacity [kW].</param>
    /// <param name="isOverLoad">Output: true if overloaded.</param>
    private static void CalcOutletTemperature
      (int cf, double copTN, double tScei, double tSpyi, ref double tSpyo, double tSceoN, double tSpyoN,
      double mcSce, double mcSpy, double qSpy, double qSpyN, double bSce, double bSpy, double[] alpha,
      out double tSceo, out double maxQSpy, out bool isOverLoad)
    {
      //冷却水出口温度を計算
      double dsp = qSpyN / qSpy * (bSpy * (tSpyo - tSpyoN) - bSce * tSceoN + 1.0);
      double esp = qSpyN / qSpy * bSce;
      double fsp = cf * copTN / tSpyo;
      double asp = esp * (alpha[2] * esp + alpha[4] * fsp);
      double bsp = esp * (alpha[1] + 2 * alpha[2] * dsp) + alpha[3] * fsp
        + alpha[4] * (dsp * fsp - cf * copTN * esp) - mcSce / qSpy;
      double csp = alpha[0] + dsp * (alpha[1] + alpha[2] * dsp) 
        - cf * copTN * (alpha[3] + alpha[4] * dsp) + (cf * qSpy + mcSce * tScei) / qSpy;
      //解の存在確認
      bool isNanoLoad;
      double disc = bsp * bsp - 4 * asp * csp;
      if (0 < disc)
      {
        tSceo = 0.5 * (-bsp - Math.Sqrt(bsp * bsp - 4 * asp * csp)) / asp;
        maxQSpy = qSpyN * (1.0 + bSpy * (tSpyo - tSpyoN) + bSce * (tSceo - tSceoN));
        isOverLoad = maxQSpy < qSpy;  //過負荷判定
        isNanoLoad = (qSpy / maxQSpy) < MIN_PLOAD;
      }
      //極低負荷の場合
      else
      {
        tSceo = tScei;
        maxQSpy = qSpyN;
        isOverLoad = false;
        isNanoLoad = true;
      }

      //過負荷の場合
      if (isOverLoad)
      {
        double dff = -(cf * mcSpy + bSpy * qSpyN) / (bSce * qSpyN);
        double eff = 1d / bSce * (cf * mcSpy * tSpyi / qSpyN - 1.0 + bSpy * tSpyoN) + tSceoN;
        double fff = (alpha[3] + alpha[4]) * copTN;
        double gff = alpha[0] + alpha[1] + alpha[2] + cf;
        double bf = fff * (1 - dff);
        double aff = (bf - cf * gff) * mcSpy - dff * mcSce;
        double bff = (tSpyi * (-bf + cf * gff) - fff * eff) * mcSpy + (tScei - eff) * mcSce;
        double cff = fff * eff * mcSpy * tSpyi;
        tSpyo = 0.5 * (-bff - Math.Sqrt(bff * bff - 4 * aff * cff)) / aff;
        tSceo = dff * tSpyo + eff;
        maxQSpy = mcSpy * Math.Abs(tSpyi - tSpyo);
      }
      //極低負荷の場合
      else if (isNanoLoad)
      {
        double pl = 1d / MIN_PLOAD;
        double aplm = copTN * (alpha[3] + alpha[4] * pl);
        tSceo = (qSpy * (alpha[0] + (alpha[1] + alpha[2] * pl) * pl + cf * (1 - aplm)) + mcSce * tScei) / (mcSce - cf * qSpy * aplm / tSpyo);
        maxQSpy = qSpyN * (1.0 + bSpy * (tSpyo - tSpyoN) + bSce * (tSceo - tSceoN));
      }
    }

    /// <summary>Shuts off the heat pump.</summary>
    public void ShutOff()
    {
      Mode = OperatingMode.ShutOff;
      EnergyConsumption = 0.0;
      //冷却関連の情報初期化
      MaxCoolingCapacity = NominalCoolingCapacity;
      CoolingLoad = 0;
      CoolingWaterFlowRate = ChilledWaterFlowRate = 0.0;
      CoolingWaterOutletTemperature = CoolingWaterInletTemperature;
      ChilledWaterOutletTemperature = ChilledWaterInletTemperature;
      //加熱関連の情報初期化
      MaxHeatingCapacity = NominalHeatingCapacity;
      HeatingLoad = 0;
      HeatSourceWaterFlowRate = HotWaterFlowRate = 0.0;
      HeatSourceWaterOutletTemperature = HeatSourceWaterInletTemperature;
      HotWaterOutletTemperature = HotWaterInletTemperature;
    }

    #endregion

  }
}
