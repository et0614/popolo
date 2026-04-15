/* HotWaterAbsorptionChiller.cs
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

using Popolo.Core.Numerics;
using Popolo.Core.Physics;

namespace Popolo.Core.HVAC.HeatSource
{
  /// <summary>Hot-water-driven LiBr absorption chiller.</summary>
  public class HotWaterAbsorptionChiller
  {

    #region 定数宣言

    /// <summary>Desorption temperature approach between hot water and solution [°C].</summary>
    private const double DESORB_TEMPERATURE_APPROACH = 2;


    #endregion

    #region プロパティ・インスタンス変数

    /// <summary>Evaporator overall heat transfer conductance [kW/K].</summary>
    private double evaporatorKA;

    /// <summary>Condenser overall heat transfer conductance [kW/K].</summary>
    private double condensorKA;

    /// <summary>Desorber overall heat transfer conductance [kW/K].</summary>
    private double desorborKA;

    /// <summary>Solution heat exchanger overall heat transfer conductance [kW/K].</summary>
    private double solutionHexKA;

    /// <summary>Minimum chilled water flow rate ratio [-].</summary>
    private double chilledWaterMinimumFLowRatio = 0.4;

    /// <summary>Minimum cooling water flow rate ratio [-].</summary>
    private double coolingWaterMinimumFLowRatio = 0.4;

    /// <summary>Minimum hot water flow rate ratio [-].</summary>
    private double hotWaterMinimumFLowRatio = 0.4;

    /// <summary>Nominal solution (refrigerant) flow rate [kg/s].</summary>
    private double nominalSolutionFlowRate;

    /// <summary>Heat loss rate relative to hot water input [-].</summary>
    private double heatLossRate = 0.0;

    /// <summary>Gets the chilled water outlet temperature [°C].</summary>
    public double ChilledWaterOutletTemperature { get; private set; }

    /// <summary>Gets or sets the chilled water outlet temperature setpoint [°C].</summary>
    public double ChilledWaterOutletSetPointTemperature { get; set; }

    /// <summary>Gets the chilled water inlet temperature [°C].</summary>
    public double ChilledWaterInletTemperature { get; private set; }

    /// <summary>Gets the cooling water outlet temperature [°C].</summary>
    public double CoolingWaterOutletTemperature { get; private set; }

    /// <summary>Gets the cooling water inlet temperature [°C].</summary>
    public double CoolingWaterInletTemperature { get; private set; }

    /// <summary>Gets the hot water outlet temperature [°C].</summary>
    public double HotWaterOutletTemperature { get; private set; }

    /// <summary>Gets the hot water inlet temperature [°C].</summary>
    public double HotWaterInletTemperature { get; private set; }

    /// <summary>Gets the current chilled water flow rate [kg/s].</summary>
    public double ChilledWaterFlowRate { get; private set; }

    /// <summary>Gets the current cooling water flow rate [kg/s].</summary>
    public double CoolingWaterFlowRate { get; private set; }

    /// <summary>Gets the current hot water flow rate [kg/s].</summary>
    public double HotWaterFlowRate { get; private set; }

    /// <summary>Gets the nominal chilled water flow rate [kg/s].</summary>
    public double NominalChilledWaterFlowRate { get; private set; }

    /// <summary>Gets the nominal cooling water flow rate [kg/s].</summary>
    public double NominalCoolingWaterFlowRate { get; private set; }

    /// <summary>Gets the nominal hot water flow rate [kg/s].</summary>
    public double NominalHotWaterFlowRate { get; private set; }

    /// <summary>Gets the nominal cooling capacity [kW].</summary>
    public double NominalCapacity { get; private set; }

    /// <summary>Gets or sets the minimum chilled water flow rate ratio [-].</summary>
    public double ChilledWaterMinimumFLowRatio
    {
      get { return chilledWaterMinimumFLowRatio; }
      private set { chilledWaterMinimumFLowRatio = Math.Min(1, Math.Max(0.4, value)); }
    }

    /// <summary>Gets or sets the minimum cooling water flow rate ratio [-].</summary>
    public double CoolingWaterMinimumFLowRatio
    {
      get { return coolingWaterMinimumFLowRatio; }
      private set { coolingWaterMinimumFLowRatio = Math.Min(1, Math.Max(0.4, value)); }
    }

    /// <summary>Gets or sets the minimum hot water flow rate ratio [-].</summary>
    public double HotWaterMinimumFLowRatio
    {
      get { return hotWaterMinimumFLowRatio; }
      private set { hotWaterMinimumFLowRatio = Math.Min(1, Math.Max(0.4, value)); }
    }

    /// <summary>Gets the current cooling load [kW].</summary>
    public double CoolingLoad { get; private set; }

    /// <summary>Gets the current COP [-].</summary>
    public double COP
    {
      get
      {
        if (HotWaterFlowRate == 0) return 0;
        else return CoolingLoad / (HotWaterFlowRate * 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat
            * (HotWaterInletTemperature - HotWaterOutletTemperature));
      }
    }

    #endregion

    #region コンストラクタ

    /// <summary>Initializes a new instance from rated operating conditions.</summary>
    /// <param name="chilledWaterInletTemperature">Chilled water inlet temperature [°C].</param>
    /// <param name="chilledWaterOutletTemperature">Chilled water outlet temperature [°C].</param>
    /// <param name="chilledWaterFlowRate">Chilled water mass flow rate [kg/s].</param>
    /// <param name="coolingWaterInletTemperature">Cooling water inlet temperature [°C].</param>
    /// <param name="coolingWaterOutletTemperature">Cooling water outlet temperature [°C].</param>
    /// <param name="coolingWaterFlowRate">Cooling water mass flow rate [kg/s].</param>
    /// <param name="hotWaterInletTemperature">Hot water inlet temperature [°C].</param>
    /// <param name="hotWaterOutletTemperature">Hot water outlet temperature [°C].</param>
    /// <param name="hotWaterFlowRate">Hot water mass flow rate [kg/s].</param>
    public HotWaterAbsorptionChiller
      (double chilledWaterInletTemperature, double chilledWaterOutletTemperature, double chilledWaterFlowRate,
      double coolingWaterInletTemperature, double coolingWaterOutletTemperature, double coolingWaterFlowRate,
      double hotWaterInletTemperature, double hotWaterOutletTemperature, double hotWaterFlowRate)
    {
      //伝熱係数を計算
      double dsvHL;
      AbsorptionRefrigerationCycle.GetHeatTransferCoefficients
        (chilledWaterInletTemperature, chilledWaterOutletTemperature, chilledWaterFlowRate,
        coolingWaterInletTemperature, coolingWaterOutletTemperature, coolingWaterFlowRate,
        hotWaterInletTemperature, hotWaterFlowRate, DESORB_TEMPERATURE_APPROACH, out evaporatorKA,
        out condensorKA, out desorborKA, out solutionHexKA, out nominalSolutionFlowRate, out dsvHL);

      //熱損失率[-]を計算    
      double qHW = (hotWaterInletTemperature - hotWaterOutletTemperature) * hotWaterFlowRate * 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat;
      heatLossRate = (qHW - dsvHL) / qHW;

      this.ChilledWaterOutletSetPointTemperature = chilledWaterOutletTemperature;
      this.CoolingWaterInletTemperature = coolingWaterInletTemperature;
      this.ChilledWaterInletTemperature = chilledWaterInletTemperature;
      this.HotWaterInletTemperature = hotWaterInletTemperature;
      this.NominalCoolingWaterFlowRate = coolingWaterFlowRate;
      this.NominalChilledWaterFlowRate = chilledWaterFlowRate;
      this.NominalHotWaterFlowRate = hotWaterFlowRate;
      this.NominalCapacity = (chilledWaterInletTemperature - chilledWaterOutletTemperature)
        * chilledWaterFlowRate * (0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat);

      //機器を停止
      ShutOff();
    }

    #endregion

    #region publicメソッド

    /// <summary>Updates the chiller state for the given inlet conditions.</summary>
    /// <param name="chilledWaterInletTemperature">Chilled water inlet temperature [°C].</param>
    /// <param name="chilledWaterFlowRate">Chilled water mass flow rate [kg/s].</param>
    /// <param name="coolingWaterInletTemperature">Cooling water inlet temperature [°C].</param>
    /// <param name="coolingWaterFlowRate">Cooling water mass flow rate [kg/s].</param>
    /// <param name="hotWaterInletTemperature">Hot water inlet temperature [°C].</param>
    /// <param name="hotWaterFlowRate">Hot water mass flow rate [kg/s].</param>
    public void Update
      (double chilledWaterInletTemperature, double chilledWaterFlowRate, double coolingWaterInletTemperature,
      double coolingWaterFlowRate, double hotWaterInletTemperature, double hotWaterFlowRate)
    {
      //状態値を保存
      this.ChilledWaterInletTemperature = chilledWaterInletTemperature;
      this.CoolingWaterInletTemperature = coolingWaterInletTemperature;
      this.HotWaterInletTemperature = hotWaterInletTemperature;
      double rch = chilledWaterFlowRate / NominalChilledWaterFlowRate;
      this.ChilledWaterFlowRate =
        Math.Max(ChilledWaterMinimumFLowRatio, Math.Min(1, rch)) * NominalChilledWaterFlowRate;
      double rcd = coolingWaterFlowRate / NominalCoolingWaterFlowRate;
      this.CoolingWaterFlowRate =
        Math.Max(CoolingWaterMinimumFLowRatio, Math.Min(1, rcd)) * NominalCoolingWaterFlowRate;
      double rht = hotWaterFlowRate / NominalHotWaterFlowRate;
      this.HotWaterFlowRate = Math.Max(HotWaterMinimumFLowRatio, Math.Min(1, rht)) * NominalHotWaterFlowRate;

      //冷却運転
      if (ChilledWaterOutletSetPointTemperature < chilledWaterInletTemperature)
      {
        double cho, cdo, ho;
        //成り行きの出口状態を計算
        AbsorptionRefrigerationCycle.GetOutletTemperatures
          (ChilledWaterInletTemperature, ChilledWaterFlowRate, CoolingWaterInletTemperature,
          CoolingWaterFlowRate, HotWaterInletTemperature, HotWaterFlowRate, evaporatorKA, condensorKA,
          desorborKA, solutionHexKA, nominalSolutionFlowRate, out cho, out cdo, out ho);

        //処理可能の場合
        if (cho < ChilledWaterOutletSetPointTemperature)
        {
          cho = ChilledWaterOutletSetPointTemperature;
          AbsorptionRefrigerationCycle.GetOutletTemperatures
            (ChilledWaterInletTemperature, ChilledWaterFlowRate, CoolingWaterInletTemperature,
            CoolingWaterFlowRate, HotWaterInletTemperature, HotWaterFlowRate, evaporatorKA, condensorKA,
            desorborKA, solutionHexKA, nominalSolutionFlowRate, cho, out cdo, out ho);
        }

        //出口状態設定
        ChilledWaterOutletTemperature = cho;
        CoolingWaterOutletTemperature = cdo;
        HotWaterOutletTemperature = (ho - heatLossRate * HotWaterInletTemperature) / (1 - heatLossRate);
        //処理熱量計算
        CoolingLoad = (ChilledWaterInletTemperature - ChilledWaterOutletTemperature)
          * 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat * chilledWaterFlowRate;
      }
      //運転停止
      else ShutOff();
    }

    /// <summary>Shuts off the chiller.</summary>
    public void ShutOff()
    {
      ChilledWaterOutletTemperature = ChilledWaterInletTemperature;
      CoolingWaterOutletTemperature = CoolingWaterInletTemperature;
      HotWaterOutletTemperature = HotWaterInletTemperature;
      ChilledWaterFlowRate = CoolingWaterFlowRate = HotWaterFlowRate = 0;
      CoolingLoad = 0;
    }

    #endregion

  }
}