/* CentrifugalInverterChiller.cs
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

using Popolo.Core.Physics;
using Popolo.Core.Numerics;

namespace Popolo.Core.HVAC.HeatSource
{
  /// <summary>Detailed inverter-driven centrifugal chiller with compressor characteristic equations.</summary>
  public class DetailedCentrifugalInverterChiller : ICentrifugalChiller, IReadOnlyCentrifugalChiller
  {

    #region プロパティ・インスタンス変数

    /// <summary>Evaporator overall heat transfer coefficient [kW/K].</summary>
    private double evaporatorHeatTransferCoefficient;

    /// <summary>Condenser overall heat transfer coefficient [kW/K].</summary>
    private double condenserHeatTransferCoefficient;

    /// <summary>Nominal adiabatic compression head [kW].</summary>
    private double nominalHead;

    /// <summary>Nominal refrigerant volumetric flow rate [m³/s].</summary>
    private double nominalFlowVolume;

    /// <summary>Gets the model parameters.</summary>
    public Parameters ModelParameters { get; private set; }

    /// <summary>True if the compressor has an inverter drive.</summary>
    public bool HasInverter { get { return true; } }

    /// <summary>Gets or sets a value indicating whether the chiller is operating.</summary>
    public bool IsOperating { get; set; }

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

    /// <summary>Gets the chilled water flow rate [kg/s].</summary>
    public double ChilledWaterFlowRate { get; private set; }

    /// <summary>Gets the cooling water flow rate [kg/s].</summary>
    public double CoolingWaterFlowRate { get; private set; }

    /// <summary>Gets the nominal cooling capacity [kW].</summary>
    public double NominalCapacity { get; private set; }

    /// <summary>Gets the nominal power input [kW].</summary>
    public double NominalInput { get; private set; }

    /// <summary>Gets the nominal COP [-].</summary>
    public double NominalCOP { get { return NominalCapacity / NominalInput; } }

    /// <summary>Gets the minimum partial load ratio for capacity control [-].</summary>
    public double MinimumPartialLoadRatio { get; private set; }

    /// <summary>Gets the electric power consumption [kW].</summary>
    public double ElectricConsumption { get; private set; }

    /// <summary>Gets the cooling load [kW].</summary>
    public double CoolingLoad { get; private set; }

    /// <summary>Gets the refrigerant superheat degree [K].</summary>
    public double SuperHeatDegree { get; private set; }

    /// <summary>Gets the coefficient of performance [-].</summary>
    public double COP { get { return (ElectricConsumption == 0) ? 0 : CoolingLoad / ElectricConsumption; } }

    /// <summary>Gets the maximum chilled water flow rate [kg/s].</summary>
    public double MaxChilledWaterFlowRate { get; private set; }

    /// <summary>Gets or sets the minimum chilled water flow rate ratio [-].</summary>
    public double MinChilledWaterFlowRatio { get; set; } = 0.4;

    /// <summary>Gets a value indicating whether the chiller is overloaded.</summary>
    public bool IsOverLoad { get; private set; }

    #endregion

    #region コンストラクタ

    /// <summary>Initializes a new instance from rated conditions.</summary>
    /// <param name="nominalInput">Nominal power input [kW].</param>
    /// <param name="minimumPartialLoadRatio">Minimum partial load ratio [-].</param>
    /// <param name="chilledWaterInletTemperature">Chilled water inlet temperature [°C].</param>
    /// <param name="chilledWaterOutletTemperature">Chilled water outlet temperature [°C].</param>
    /// <param name="coolingWaterInletTemperature">Cooling water inlet temperature [°C].</param>
    /// <param name="chilledWaterFlowRate">Chilled water mass flow rate [kg/s].</param>
    /// <param name="coolingWaterFlowRate">Cooling water mass flow rate [kg/s].</param>
    /// <param name="modelParameters">Model parameters.</param>
    public DetailedCentrifugalInverterChiller
      (double nominalInput, double minimumPartialLoadRatio,
      double chilledWaterInletTemperature, double chilledWaterOutletTemperature,
      double coolingWaterInletTemperature, double chilledWaterFlowRate, double coolingWaterFlowRate, Parameters modelParameters)
    {
      ModelParameters = modelParameters;

      //定格能力などを保存
      this.CoolingWaterFlowRate = coolingWaterFlowRate;
      this.ChilledWaterFlowRate = chilledWaterFlowRate;
      this.MaxChilledWaterFlowRate = chilledWaterFlowRate;
      double mccd = CoolingWaterFlowRate * 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat;
      double mcch = ChilledWaterFlowRate * 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat;
      this.ChilledWaterOutletSetPointTemperature = chilledWaterOutletTemperature;
      this.CoolingWaterInletTemperature = coolingWaterInletTemperature;
      this.ChilledWaterInletTemperature = chilledWaterInletTemperature;
      this.NominalCapacity = mcch * (chilledWaterInletTemperature - chilledWaterOutletTemperature);
      this.NominalInput = nominalInput;
      this.MinimumPartialLoadRatio = minimumPartialLoadRatio;

      //蒸発器の熱伝達率[kW/K]を計算
      double tEvp = ChilledWaterOutletSetPointTemperature - 2;
      double dt1 = ChilledWaterInletTemperature - tEvp;
      double dt2 = ChilledWaterOutletSetPointTemperature - tEvp;
      double lmtd = (dt1 - dt2) / Math.Log(dt1 / dt2);
      evaporatorHeatTransferCoefficient = NominalCapacity / lmtd;

      //凝縮器の熱伝達率[kW/K]を計算
      double qcd = NominalCapacity + NominalInput;
      CoolingWaterOutletTemperature = CoolingWaterInletTemperature + qcd / mccd;
      double tCnd = CoolingWaterOutletTemperature + 1;
      dt1 = tCnd - CoolingWaterInletTemperature;
      dt2 = tCnd - CoolingWaterOutletTemperature;
      lmtd = (dt1 - dt2) / Math.Log(dt1 / dt2);
      condenserHeatTransferCoefficient = qcd / lmtd;

      //定格断熱圧縮仕事[kW]と冷媒流量[m3/s]を計算      
      GetHeadAndFlowVolume(
        qcd, NominalCapacity, CoolingWaterInletTemperature, ChilledWaterInletTemperature,
        condenserHeatTransferCoefficient, evaporatorHeatTransferCoefficient, mccd, mcch,
        out nominalHead, out nominalFlowVolume);
    }

    /// <summary>Initializes a new instance from rated conditions.</summary>
    /// <param name="nominalInput">Nominal power input [kW].</param>
    /// <param name="minimumPartialLoadRatio">Minimum partial load ratio [-].</param>
    /// <param name="chilledWaterInletTemperature">Chilled water inlet temperature [°C].</param>
    /// <param name="chilledWaterOutletTemperature">Chilled water outlet temperature [°C].</param>
    /// <param name="coolingWaterInletTemperature">Cooling water inlet temperature [°C].</param>
    /// <param name="chilledWaterFlowRate">Chilled water mass flow rate [kg/s].</param>
    /// <param name="coolingWaterFlowRate">Cooling water mass flow rate [kg/s].</param>
    public DetailedCentrifugalInverterChiller
      (double nominalInput, double minimumPartialLoadRatio,
      double chilledWaterInletTemperature, double chilledWaterOutletTemperature,
      double coolingWaterInletTemperature, double chilledWaterFlowRate, double coolingWaterFlowRate) :
        this(nominalInput, minimumPartialLoadRatio, chilledWaterInletTemperature, chilledWaterOutletTemperature,
          coolingWaterInletTemperature, chilledWaterFlowRate, coolingWaterFlowRate, new Parameters())
    { }

    #endregion    

    #region publicメソッド

    /// <summary>Updates the chiller state for the given inlet conditions.</summary>
    /// <param name="coolingWaterInletTemperature">Cooling water inlet temperature [°C].</param>
    /// <param name="chilledWaterInletTemperature">Chilled water inlet temperature [°C].</param>
    /// <param name="coolingWaterFlowRate">Cooling water mass flow rate [kg/s].</param>
    /// <param name="chilledWaterFlowRate">Chilled water mass flow rate [kg/s].</param>
    public void Update
      (double coolingWaterInletTemperature, double chilledWaterInletTemperature,
      double coolingWaterFlowRate, double chilledWaterFlowRate)
    {
      //状態値を保存
      this.ChilledWaterInletTemperature = chilledWaterInletTemperature;
      this.CoolingWaterInletTemperature = coolingWaterInletTemperature;
      this.ChilledWaterFlowRate = chilledWaterFlowRate;
      this.CoolingWaterFlowRate = coolingWaterFlowRate;

      //非稼働ならば停止処理
      if (!IsOperating)
      {
        ShutOff();
        return;
      }

      //熱容量流量[kW/K]を計算
      double mccd = CoolingWaterFlowRate * 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat;
      double mcch = ChilledWaterFlowRate * 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat;

      //必要能力[kW]を計算
      CoolingLoad = mcch * (ChilledWaterInletTemperature - ChilledWaterOutletSetPointTemperature);
      double partialLoad = Math.Max(MinimumPartialLoadRatio, CoolingLoad / NominalCapacity);

      //過負荷判定
      double head, volume;
      GetHeadAndFlowVolume(
        CoolingLoad + NominalInput, CoolingLoad, CoolingWaterInletTemperature, ChilledWaterInletTemperature,
        condenserHeatTransferCoefficient, evaporatorHeatTransferCoefficient, mccd, mcch,
        out head, out volume);
      ElectricConsumption = GetElectricity(head, volume, partialLoad);

      //最大能力範囲内:消費電力を収束計算
      if (ElectricConsumption < NominalInput)
      {
        IsOverLoad = false;
        Roots.ErrorFunction eFnc = delegate (double econs)
        {
          GetHeadAndFlowVolume(CoolingLoad + econs, CoolingLoad, CoolingWaterInletTemperature,
            ChilledWaterInletTemperature, condenserHeatTransferCoefficient, evaporatorHeatTransferCoefficient,
            mccd, mcch, out head, out volume);
          return GetElectricity(head, volume, partialLoad) - econs;
        };
        double eTol = NominalInput * 1e-4;  //定格消費電力の0.01%までの誤差を許容
        ElectricConsumption = Roots.Newton(eFnc, ElectricConsumption, 1e-4, eTol, eTol, 10);
        ChilledWaterOutletTemperature = ChilledWaterOutletSetPointTemperature;
      }
      //過負荷//冷却能力を収束計算
      else
      {
        IsOverLoad = true;
        Roots.ErrorFunction eFnc = delegate (double cLoad)
        {
          GetHeadAndFlowVolume(cLoad + NominalInput, cLoad, CoolingWaterInletTemperature,
            ChilledWaterInletTemperature, condenserHeatTransferCoefficient, evaporatorHeatTransferCoefficient,
            mccd, mcch, out head, out volume);
          return GetElectricity(head, volume, partialLoad) - NominalInput;
        };
        CoolingLoad = Roots.Newton(eFnc, NominalCapacity, 1e-4, NominalInput * 1e-4, NominalCapacity * 1e-4, 10);
        ChilledWaterOutletTemperature = ChilledWaterInletTemperature - CoolingLoad / mcch;
      }
      //冷却水温度を計算
      CoolingWaterOutletTemperature = coolingWaterInletTemperature + (CoolingLoad + ElectricConsumption) / mccd;
    }

    /// <summary>Shuts off the chiller.</summary>
    public void ShutOff()
    {
      ChilledWaterOutletTemperature = ChilledWaterInletTemperature;
      CoolingWaterOutletTemperature = CoolingWaterInletTemperature;
      CoolingLoad = ChilledWaterFlowRate = CoolingWaterFlowRate = 0;
      ElectricConsumption = 0;
    }

    #endregion

    #region privateメソッド

    /// <summary>Computes the electric power consumption [kW].</summary>
    /// <param name="head">Adiabatic compression head [kW].</param>
    /// <param name="volume">Refrigerant volumetric flow rate [m³/s].</param>
    /// <param name="partialLoad">Partial load ratio [-].</param>
    /// <returns>Electric power consumption [kW].</returns>
    private double GetElectricity(double head, double volume, double partialLoad)
    {
      double rvR = volume / ((Math.Sqrt(head / nominalHead) * ModelParameters.a_Had + ModelParameters.b_Had) * nominalFlowVolume);
      return head * (ModelParameters.c_Rv + rvR * (ModelParameters.b_Rv + rvR * ModelParameters.a_Rv))
        + NominalInput * (partialLoad * ModelParameters.a_pl + ModelParameters.b_pl);
    }

    /// <summary>Computes the adiabatic compression head [kW] and refrigerant volume flow rate [m³/s].</summary>
    /// <param name="qcd">Condenser heat transfer rate [kW].</param>
    /// <param name="qch">Evaporator heat transfer rate [kW].</param>
    /// <param name="tcdi">Cooling water inlet temperature [°C].</param>
    /// <param name="tchi">Chilled water inlet temperature [°C].</param>
    /// <param name="kacd">Condenser overall heat transfer coefficient [kW/K].</param>
    /// <param name="kach">Evaporator overall heat transfer coefficient [kW/K].</param>
    /// <param name="mccd">Condenser heat capacity rate [kW/K].</param>
    /// <param name="mcch">Evaporator heat capacity rate [kW/K].</param>
    /// <param name="head">Output: adiabatic compression head [kW].</param>
    /// <param name="volume">Output: refrigerant volumetric flow rate [m³/s].</param>
    private static void GetHeadAndFlowVolume
      (double qcd, double qch, double tcdi, double tchi, double kacd, double kach,
      double mccd, double mcch, out double head, out double volume)
    {
      //冷媒はR134a
      Refrigerant r134a = new Refrigerant(Refrigerant.Fluid.R134a);

      //凝縮温度・圧力を計算
      double tCnd = tcdi + qcd / ((1 - Math.Exp(-kacd / mccd)) * mccd);
      double dlCnd, dvCnd, pCnd;
      r134a.GetSaturatedPropertyFromTemperature(PhysicsConstants.ToKelvin(tCnd), out dlCnd, out dvCnd, out pCnd);
      double hoCnd = r134a.GetEnthalpyFromTemperatureAndDensity(PhysicsConstants.ToKelvin(tCnd), dlCnd);

      //蒸発温度・圧力を計算
      double tEvp = tchi - qch / ((1 - Math.Exp(-kach / mcch)) * mcch);
      double dlEvp, dvEvp, pEvp;
      r134a.GetSaturatedPropertyFromTemperature(PhysicsConstants.ToKelvin(tEvp), out dlEvp, out dvEvp, out pEvp);
      double hiCmp1 = r134a.GetEnthalpyFromTemperatureAndDensity(PhysicsConstants.ToKelvin(tEvp), dvEvp);

      //中間圧力を計算
      double pMid = Math.Sqrt(pEvp * pCnd);
      double dlMid, dvMid, tMid;
      r134a.GetSaturatedPropertyFromPressure(pMid, out dlMid, out dvMid, out tMid);
      double hsvMid = r134a.GetEnthalpyFromTemperatureAndDensity(tMid, dvMid);

      //蒸発器冷媒流量を計算
      double hiEvp = r134a.GetEnthalpyFromTemperatureAndDensity(tMid, dlMid);
      double mRevp = qch / (hiCmp1 - hiEvp);
      volume = mRevp / dvEvp;

      //1段目羽根車の断熱ヘッド[kW]と出口比エンタルピー[kJ/kg]を計算
      double kappa = r134a.GetSpecificHeatRatioFromTemperatureAndDensity(PhysicsConstants.ToKelvin(tEvp), dvEvp);
      kappa = (kappa - 1) / kappa;
      double head1 = volume / kappa * pEvp * (Math.Pow(pMid / pEvp, kappa) - 1);
      double hoCmp1 = hiCmp1 + head1 / mRevp;

      //2段目羽根車断熱ヘッド[kW]を計算
      double mRe = (hoCnd - hiEvp) / (hsvMid - hoCnd) * mRevp;
      double hiCmp2 = (hoCmp1 * mRevp + hsvMid * mRe) / (mRevp + mRe);
      double tiCmp2, diCmp2, siCmp2, uiCmp2;
      r134a.GetStateFromPressureAndEnthalpy(pMid, hiCmp2, out tiCmp2, out diCmp2, out siCmp2, out uiCmp2);
      double vRCmp2 = (mRevp + mRe) / diCmp2;
      kappa = r134a.GetSpecificHeatRatioFromTemperatureAndDensity(PhysicsConstants.ToKelvin(tiCmp2), diCmp2);
      kappa = (kappa - 1) / kappa;
      double head2 = vRCmp2 / kappa * pMid * (Math.Pow(pCnd / pMid, kappa) - 1);

      //圧縮機入力[kW]の計算
      head = head1 + head2;
    }

    #endregion

    #region パラメータ保持用のインナークラス定義

    /// <summary>Model parameters for the detailed centrifugal inverter chiller.</summary>
    public class Parameters
    {
      /// <summary>Coefficient a for estimating normalised refrigerant volume flow from normalised adiabatic head.</summary>
      public double a_Had { get; } = 0.8553;

      /// <summary>Coefficient b for estimating normalised refrigerant volume flow from normalised adiabatic head.</summary>
      public double b_Had { get; } = 0.0590;

      /// <summary>Coefficient a for estimating adiabatic head efficiency from normalised refrigerant volume flow.</summary>
      public double a_Rv { get; } = 1.8515;


      /// <summary>Coefficient b for estimating adiabatic head efficiency from normalised refrigerant volume flow.</summary>
      public double b_Rv { get; } = -3.9452;


      /// <summary>Coefficient c for estimating adiabatic head efficiency from normalised refrigerant volume flow.</summary>
      public double c_Rv { get; } = 3.4178;


      /// <summary>Coefficient a for estimating auxiliary power consumption from partial load ratio.</summary>
      public double a_pl { get; } = -0.0945;

      /// <summary>Coefficient b for estimating auxiliary power consumption from partial load ratio.</summary>
      public double b_pl { get; } = 0.0438;

      /// <summary>Initializes a new instance from rated conditions.</summary>
      public Parameters() { }

      /// <summary>Initializes a new instance from rated conditions.</summary>
      /// <param name="a_Had">Coefficient a for normalised volume flow from normalised adiabatic head.</param>
      /// <param name="b_Had">Coefficient b for normalised volume flow from normalised adiabatic head.</param>
      /// <param name="a_Rv">Coefficient a for adiabatic head efficiency from normalised volume flow.</param>
      /// <param name="b_Rv">Coefficient b for adiabatic head efficiency from normalised volume flow.</param>
      /// <param name="c_Rv">Coefficient c for adiabatic head efficiency from normalised volume flow.</param>
      /// <param name="a_pl">Coefficient a for auxiliary power from partial load ratio.</param>
      /// <param name="b_pl">Coefficient b for auxiliary power from partial load ratio.</param>
      public Parameters(
          double a_Had, double b_Had, double a_Rv, double b_Rv,
          double c_Rv, double a_pl, double b_pl)
      {
        this.a_Had = a_Had;
        this.b_Had = b_Had;
        this.a_Rv = a_Rv;
        this.b_Rv = b_Rv;
        this.c_Rv = c_Rv;
        this.a_pl = a_pl;
        this.b_pl = b_pl;
      }
    }

    #endregion

  }
}
