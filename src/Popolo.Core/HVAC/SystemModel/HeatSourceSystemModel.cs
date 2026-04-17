/* HeatSourceSystemModel.cs
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

using Popolo.Core.HVAC.FluidCircuit;
using Popolo.Core.Physics;

namespace Popolo.Core.HVAC.SystemModel
{
  /// <summary>Primary heat source system.</summary>
  public class HeatSourceSystemModel : IReadOnlyHeatSourceSystemModel
  {

    #region 定数宣言


    #endregion

    #region 列挙型定義

    /// <summary>Operating mode.</summary>
    [Flags]
    public enum OperatingMode
    {
      /// <summary>Shut-off.</summary>
      ShutOff = 0,
      /// <summary>Heating mode.</summary>
      Heating = 1,
      /// <summary>Cooling mode.</summary>
      Cooling = 2,
      /// <summary>Heating and cooling mode.</summary>
      HeatingAndCooling = 4
    }

    #endregion

    #region インスタンス変数・プロパティ

    /// <summary>Heat source sub-system.</summary>
    private IHeatSourceSubSystem[] subSystems;

    /// <summary>Secondary water pump.</summary>
    private PumpSystem? chilledWaterPumps, hotWaterPumps;

    /// <summary>Operating priority order.</summary>
    private int[] opRankC, opRankH;

    /// <summary>Gets or sets the chilled water supply temperature setpoint [°C].</summary>
    public double ChilledWaterSupplyTemperatureSetpoint { get; set; } = 7;

    /// <summary>Gets the chilled water supply temperature [°C].</summary>
    public double ChilledWaterSupplyTemperature { get; private set; } = 7;

    /// <summary>Gets the chilled water return temperature [°C].</summary>
    public double ChilledWaterReturnTemperature { get; private set; } = 12;

    /// <summary>Gets the chilled water flow rate [kg/s].</summary>
    public double ChilledWaterFlowRate { get; private set; }

    /// <summary>Gets the chilled water bypass flow rate [kg/s].</summary>
    public double ChilledWaterBypassFlowRate { get; private set; }

    /// <summary>Gets a value indicating whether the chilled water supply is overloaded.</summary>
    public bool IsOverLoad_C { get; private set; }

    /// <summary>Gets or sets the hot water supply temperature setpoint [°C].</summary>
    public double HotWaterSupplyTemperatureSetpoint { get; set; } = 45;

    /// <summary>Gets the hot water supply temperature [°C].</summary>
    public double HotWaterSupplyTemperature { get; private set; } = 45;

    /// <summary>Gets the hot water return temperature [°C].</summary>
    public double HotWaterReturnTemperature { get; private set; } = 40;

    /// <summary>Gets the hot water flow rate [kg/s].</summary>
    public double HotWaterFlowRate { get; private set; }

    /// <summary>Gets the hot water bypass flow rate [kg/s].</summary>
    public double HotWaterBypassFlowRate { get; private set; }

    /// <summary>Gets a value indicating whether the hot water supply is overloaded.</summary>
    public bool IsOverLoad_H { get; private set; }

    /// <summary>True if a secondary pump system is used.</summary>
    public bool IsSecondaryPumpSystem { get { return chilledWaterPumps != null; } }

    /// <summary>Gets the chilled water secondary pump system.</summary>
    public IReadOnlyPumpSystem? ChilledWaterPumpSystem { get { return chilledWaterPumps; } }

    /// <summary>Gets the hot water secondary pump system.</summary>
    public IReadOnlyPumpSystem? HotWaterPumpSystem { get { return hotWaterPumps; } }

    /// <summary>Gets or sets the outdoor air conditions.</summary>
    public IReadOnlyMoistAir OutdoorAir { get; set; } = new MoistAir(35, 0.0195);

    /// <summary>Gets or sets the current date and time.</summary>
    public DateTime CurrentDateTime { get; set; }

    /// <summary>Gets or sets the simulation time step [s].</summary>
    public double TimeStep { get; set; } = 3600;

    /// <summary>Gets or sets the piping heat loss rate [-].</summary>
    public double PipeHeatLossRate { get; set; } = 0.08;

    #endregion

    #region コンストラクタ

    /// <summary>Initializes a new instance (dual-pump system).</summary>
    /// <param name="subSystems">List of heat source sub-systems.</param>
    /// <param name="chilledWaterPumps">Chilled water secondary pumps.</param>
    /// <param name="hotWaterPumps">Hot water secondary pumps.</param>
    public HeatSourceSystemModel
      (IHeatSourceSubSystem[] subSystems, PumpSystem chilledWaterPumps, PumpSystem hotWaterPumps)
    {
      this.subSystems = subSystems;
      this.chilledWaterPumps = chilledWaterPumps;
      this.hotWaterPumps = hotWaterPumps;
      opRankC = new int[subSystems.Length];
      opRankH = new int[subSystems.Length];
    }

    /// <summary>Initializes a new instance (single-pump system).</summary>
    /// <param name="subSystems">List of heat source sub-systems.</param>
    public HeatSourceSystemModel(IHeatSourceSubSystem[] subSystems)
    {
      this.subSystems = subSystems;
      opRankC = new int[subSystems.Length];
      opRankH = new int[subSystems.Length];
    }

    #endregion

    #region インスタンスメソッド

    /// <summary>Forecasts the future state.</summary>
    /// <param name="chilledWaterFlowRate">Chilled water flow rate [kg/s].</param>
    /// <param name="chilledWaterReturnTemperature">Chilled water return temperature [°C].</param>
    /// <param name="hotWaterFlowRate">Hot water flow rate [kg/s].</param>
    /// <param name="hotWaterReturnTemperature">Hot water return temperature [°C].</param>
    public void ForecastSupplyWaterTemperature
      (double chilledWaterFlowRate, double chilledWaterReturnTemperature,
      double hotWaterFlowRate, double hotWaterReturnTemperature)
    {
      ChilledWaterFlowRate = chilledWaterFlowRate;
      HotWaterFlowRate = hotWaterFlowRate;
      ChilledWaterReturnTemperature = chilledWaterReturnTemperature
        - (ChilledWaterSupplyTemperatureSetpoint - chilledWaterReturnTemperature) * PipeHeatLossRate;
      HotWaterReturnTemperature = hotWaterReturnTemperature
        - (HotWaterSupplyTemperatureSetpoint - hotWaterReturnTemperature) * PipeHeatLossRate;

      //日時・冷温水往温度・外気条件を設定
      for (int i = 0; i < subSystems.Length; i++)
      {
        subSystems[i].TimeStep = TimeStep;
        subSystems[i].CurrentDateTime = CurrentDateTime;
        subSystems[i].OutdoorAir = OutdoorAir;
        subSystems[i].ChilledWaterSupplyTemperatureSetpoint = ChilledWaterSupplyTemperatureSetpoint;
        subSystems[i].HotWaterSupplyTemperatureSetpoint = HotWaterSupplyTemperatureSetpoint;
      }

      //2次ポンプによる昇温を反映
      double tcwi = chilledWaterReturnTemperature;
      double thwi = hotWaterReturnTemperature;
      if (IsSecondaryPumpSystem)
      {
        if (chilledWaterFlowRate != 0)
        {
          chilledWaterPumps!.TotalFlowRate = 0.001 * chilledWaterFlowRate;
          chilledWaterPumps.UpdateState();
          tcwi += chilledWaterPumps.GetElectricConsumption() / (chilledWaterFlowRate * 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat);
        }
        else chilledWaterPumps!.ShutOff();
        if (hotWaterFlowRate != 0)
        {
          hotWaterPumps!.TotalFlowRate = 0.001 * hotWaterFlowRate;
          hotWaterPumps.UpdateState();
          thwi += hotWaterPumps.GetElectricConsumption() / (hotWaterFlowRate * 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat);
        }
        else hotWaterPumps!.ShutOff();
      }

      //負荷流量に応じて最低運転台数を計算
      int cStage, hStage;
      double fcsum = 0; //冷却運転
      if (chilledWaterFlowRate <= 0) cStage = 0;
      else
      {
        for (cStage = 1; cStage <= opRankC.Length; cStage++)
        {
          for (int j = 0; j < subSystems.Length; j++)
            if (opRankC[j] == cStage && IsModeAdapts(j, true)) fcsum += subSystems[j].MaxChilledWaterFlowRate;
          if (chilledWaterFlowRate < fcsum) break;
        }
      }
      double fhsum = 0; //加熱運転
      if (hotWaterFlowRate <= 0) hStage = 0;
      else
      {
        for (hStage = 1; hStage <= opRankH.Length; hStage++)
        {
          for (int j = 0; j < subSystems.Length; j++)
            if (opRankH[j] == hStage && IsModeAdapts(j, false)) fhsum += subSystems[j].MaxHotWaterFlowRate;
          if (hotWaterFlowRate < fhsum) break;
        }
      }

      //過負荷系統が無くなるまで増段
      double bypsC = 0;
      double bypsH = 0;
      while (true)
      {
        IsOverLoad_C = false;
        IsOverLoad_H = false;

        //最大絞り流量比を計算
        double minMaxC = 0.0;
        double minMaxH = 0.0;
        for (int i = 0; i < subSystems.Length; i++)
        {
          if (opRankC[i] <= cStage && IsModeAdapts(i, true))
            minMaxC = Math.Max(minMaxC, subSystems[i].MinChilledWaterFlowRatio);
          if (opRankH[i] <= hStage && IsModeAdapts(i, false))
            minMaxH = Math.Max(minMaxH, subSystems[i].MinHotWaterFlowRatio);
        }

        //バイパス流量を考慮して熱源入口水温を計算
        double cpl, hpl;
        if (chilledWaterFlowRate == 0 || fcsum == 0) cpl = 0;
        else
        {
          cpl = chilledWaterFlowRate / fcsum;
          bypsC = Math.Max(0, minMaxC - cpl);
          double tcwi2 = (tcwi * cpl + ChilledWaterSupplyTemperatureSetpoint * bypsC) / (cpl + bypsC);
          foreach (IHeatSourceSubSystem hss in subSystems) hss.ChilledWaterReturnTemperature = tcwi2;
          cpl = Math.Max(minMaxC, cpl);
        }
        if (hotWaterFlowRate == 0 || fhsum == 0) hpl = 0;
        else
        {
          hpl = hotWaterFlowRate / fhsum;
          bypsH = Math.Max(0, minMaxH - hpl);
          double thwi2 = (thwi * hpl + HotWaterSupplyTemperatureSetpoint * bypsH) / (hpl + bypsH);
          foreach (IHeatSourceSubSystem hss in subSystems) hss.HotWaterReturnTemperature = thwi2;
          hpl = Math.Max(minMaxH, hpl);
        }

        //過負荷系統が無いか確認
        for (int i = 0; i < subSystems.Length; i++)
        {
          double cf, hf;
          cf = hf = 0;
          if (opRankC[i] <= cStage && IsModeAdapts(i, true)) cf = subSystems[i].MaxChilledWaterFlowRate * cpl;
          if (opRankH[i] <= hStage && IsModeAdapts(i, false)) hf = subSystems[i].MaxHotWaterFlowRate * hpl;
          subSystems[i].ForecastSupplyWaterTemperature(cf, hf);

          if (subSystems[i].IsOverLoad_C) IsOverLoad_C = true;
          if (subSystems[i].IsOverLoad_H) IsOverLoad_H = true;
        }
        int maxStage = opRankC.Length;
        if ((!IsOverLoad_C || maxStage <= cStage) && (!IsOverLoad_H || maxStage <= hStage)) break;

        //増段処理
        if (IsOverLoad_C && cStage != maxStage)
        {
          cStage++;
          for (int j = 0; j < subSystems.Length; j++)
            if (opRankC[j] == cStage && IsModeAdapts(j, true)) fcsum += subSystems[j].MaxChilledWaterFlowRate;
        }
        if (IsOverLoad_H && hStage != maxStage)
        {
          hStage++;
          for (int j = 0; j < subSystems.Length; j++)
            if (opRankH[j] == hStage && IsModeAdapts(j, false)) fhsum += subSystems[j].MaxHotWaterFlowRate;
        }
      }
      ChilledWaterBypassFlowRate = bypsC * fcsum;
      HotWaterBypassFlowRate = bypsH * fhsum;

      //冷水出口温度を計算
      if (IsOverLoad_C)
      {
        ChilledWaterSupplyTemperature = 0;
        double cwSum = 0;
        foreach (IHeatSourceSubSystem ss in subSystems)
        {
          ChilledWaterSupplyTemperature += ss.ChilledWaterSupplyTemperature * ss.ChilledWaterFlowRate;
          cwSum += ss.ChilledWaterFlowRate;
        }
        ChilledWaterSupplyTemperature /= cwSum;
      }
      else ChilledWaterSupplyTemperature = ChilledWaterSupplyTemperatureSetpoint;

      //温水出口温度を計算
      if (IsOverLoad_H)
      {
        HotWaterSupplyTemperature = 0;
        double hwSum = 0;
        foreach (IHeatSourceSubSystem ss in subSystems)
        {
          HotWaterSupplyTemperature += ss.HotWaterSupplyTemperature * ss.HotWaterFlowRate;
          hwSum += ss.HotWaterFlowRate;
        }
        HotWaterSupplyTemperature /= hwSum;
      }
      else HotWaterSupplyTemperature = HotWaterSupplyTemperatureSetpoint;
    }

    /// <summary>Fixes (commits) the forecast state as the current state.</summary>
    public void FixState() { foreach (IHeatSourceSubSystem hs in subSystems) hs.FixState(); }

    /// <summary>Sets the number of active stages for cooling operation.</summary>
    /// <param name="subSystemIndex">Sub-system index.</param>
    /// <param name="stage">Stage number (1, 2, 3, ...).</param>
    public void SetChillingOperationSequence(int subSystemIndex, int stage) { opRankC[subSystemIndex] = stage; }

    /// <summary>Sets the number of active stages for heating operation.</summary>
    /// <param name="subSystemIndex">Sub-system index.</param>
    /// <param name="stage">Stage number (1, 2, 3, ...).</param>
    public void SetHeatingOperationSequence(int subSystemIndex, int stage) { opRankH[subSystemIndex] = stage; }

    /// <summary>Sets the operating mode.</summary>
    /// <param name="subSystemIndex">Sub-system index.</param>
    /// <param name="mode">Operating mode.</param>
    public void SetOperatingMode(int subSystemIndex, OperatingMode mode) { subSystems[subSystemIndex].Mode = mode; }

    /// <summary>Gets a value indicating whether the given mode matches the current mode.</summary>
    /// <param name="subsystemNumber">Sub-system index.</param>
    /// <param name="cooling">True for cooling mode; false for heating mode.</param>
    /// <returns>True if the mode matches; false otherwise.</returns>
    private bool IsModeAdapts(int subsystemNumber, bool cooling)
    {
      IHeatSourceSubSystem ss = subSystems[subsystemNumber];
      if (ss.Mode == OperatingMode.ShutOff) return false;
      if (cooling && ss.Mode == OperatingMode.Heating) return false;
      if (!cooling && ss.Mode == OperatingMode.Cooling) return false;
      return true;
    }

    #endregion

  }
}
