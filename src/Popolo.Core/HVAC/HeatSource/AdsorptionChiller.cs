/* AdsorptionChiller.cs
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

using Popolo.Core.Physics;
using Popolo.Core.Numerics;

namespace Popolo.Core.HVAC.HeatSource
{
  /// <summary>Adsorption chiller.</summary>
  public class AdsorptionChiller
  {

    #region 定数宣言

    /// <summary>Water content during adsorption at rated conditions [kg/kg].</summary>
    private const double NOM_AD0 = 0.04;

    /// <summary>Water content during desorption at rated conditions [kg/kg].</summary>
    private const double NOM_DS0 = 0.16;

    /// <summary>Maximum water content (saturation) [kg/kg].</summary>
    private const double MAX_W = 0.16;

    /// <summary>Minimum water content [kg/kg].</summary>
    private const double MIN_W = 0.04;

    /// <summary>Heat transfer effectiveness of the adsorption reactor [-].</summary>
    private const double EPSILON_DS = 0.9;
    private const double EPSILON_AD = 0.9;


    /// <summary>Heat recovery effectiveness [-].</summary>
    private const double EPSILON_RCV = 0.3;

    /// <summary>Latent heat of vaporisation approximation coefficients.</summary>
    private const double CGAM_A = -2.4564;
    private const double CGAM_B = 3.1753e3;

    /// <summary>Linear approximation coefficients for adsorbent temperature, water content, and saturation temperature.</summary>
    private readonly double[] ATWS0 = new double[] { -2.6797e-4, 2.2301e-1, -3.0832e1 };
    private readonly double[] ATWS1 = new double[] { 7.6246e-5, 9.3167e-1, -6.3175 };

    #endregion

    #region インスタンス変数・プロパティ

    /// <summary>Cycle time ratio relative to the rated cycle.</summary>
    private double ctRate = 1.0;

    /// <summary>Evaporator overall heat transfer conductance [kW/K].</summary>
    private double evaporatorKA;

    /// <summary>Condenser overall heat transfer conductance [kW/K].</summary>
    private double condenserKA;

    /// <summary>Minimum chilled water flow rate ratio [-].</summary>
    private double chilledWaterMinFlowRatio = 0.4;

    /// <summary>Minimum cooling water flow rate ratio [-].</summary>
    private double coolingWaterMinFlowRatio = 0.4;

    /// <summary>Minimum hot water flow rate ratio [-].</summary>
    private double hotWaterMinFlowRatio = 0.4;

    /// <summary>Heat loss rate [-].</summary>
    private double heatLossRate = 0.0;

    /// <summary>Adsorbent mass rate relative to cycle period [kg/s].</summary>
    private double massPerCycle;

    /// <summary>Heat capacity of the adsorber/desorber per unit adsorbent mass [kJ/(kg·K)].</summary>
    private double heatCapacity;

    /// <summary>Gets or sets the cycle time ratio relative to the rated cycle [-].</summary>
    public double CyclingTimeRatio
    {
      get { return ctRate; }
      set { ctRate = Math.Max(0.3, value); }
    }

    /// <summary>Gets the chilled water outlet temperature [°C].</summary>
    public double ChilledWaterOutletTemperature { get; private set; }

    /// <summary>Gets or sets the chilled water outlet temperature setpoint [°C].</summary>
    public double ChilledWaterOutletSetpointTemperature { get; set; }

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

    /// <summary>Gets the chilled water flow rate [kg/s].</summary>
    public double ChilledWaterFlowRate { get; private set; }

    /// <summary>Gets the cooling water flow rate [kg/s].</summary>
    public double CoolingWaterFlowRate { get; private set; }

    /// <summary>Gets the hot water flow rate [kg/s].</summary>
    public double HotWaterFlowRate { get; private set; }

    /// <summary>Gets the nominal chilled water flow rate [kg/s].</summary>
    public double NominalChilledWaterFlowRate { get; private set; }

    /// <summary>Gets the nominal cooling water flow rate [kg/s].</summary>
    public double NominalCoolingWaterFlowRate { get; private set; }

    /// <summary>Gets the nominal hot water flow rate [kg/s].</summary>
    public double NominalHotWaterFlowRate { get; private set; }

    /// <summary>Gets the nominal cooling capacity [kW].</summary>
    public double NominalCapacity { get; private set; }

    /// <summary>Gets the nominal COP [-].</summary>
    public double NominalCOP { get; private set; }

    /// <summary>Gets or sets the minimum chilled water flow rate ratio [-].</summary>
    public double ChilledWaterMinFlowRatio
    {
      get { return chilledWaterMinFlowRatio; }
      private set { chilledWaterMinFlowRatio = Math.Min(1, Math.Max(0.4, value)); }
    }

    /// <summary>Gets or sets the minimum cooling water flow rate ratio [-].</summary>
    public double CoolingWaterMinFlowRatio
    {
      get { return coolingWaterMinFlowRatio; }
      private set { coolingWaterMinFlowRatio = Math.Min(1, Math.Max(0.4, value)); }
    }

    /// <summary>Gets or sets the minimum hot water flow rate ratio [-].</summary>
    public double HotWaterMinFlowRatio
    {
      get { return hotWaterMinFlowRatio; }
      private set { hotWaterMinFlowRatio = Math.Min(1, Math.Max(0.4, value)); }
    }

    /// <summary>Gets the current cooling load [kW].</summary>
    public double CoolingLoad { get; private set; }

    /// <summary>Gets the current COP [-].</summary>
    public double COP
    {
      get
      {
        if (HotWaterFlowRate == 0) return 0;
        else
          return CoolingLoad / (HotWaterFlowRate * 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat
            * (HotWaterInletTemperature - HotWaterOutletTemperature));
      }
    }

    /// <summary>Gets or sets the adsorption temperature [°C].</summary>
    public double AdsorptionTemperature { get; private set; }

    /// <summary>Gets or sets the desorption temperature [°C].</summary>
    public double DesorptionTemperature { get; private set; }

    /// <summary>Gets or sets the water content during adsorption [kg/kg].</summary>
    public double WaterContent_Adsorption { get; private set; }

    /// <summary>Gets or sets the water content during desorption [kg/kg].</summary>
    public double WaterContent_Desorption { get; private set; }

    /// <summary>Gets the operating time ratio [-].</summary>
    public double OperatingTimeRatio { get; private set; }

    #endregion

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
    public AdsorptionChiller
      (double chilledWaterInletTemperature, double chilledWaterOutletTemperature,
      double chilledWaterFlowRate, double coolingWaterInletTemperature,
      double coolingWaterOutletTemperature, double coolingWaterFlowRate,
      double hotWaterInletTemperature, double hotWaterOutletTemperature,
      double hotWaterFlowRate)
    {
      NominalChilledWaterFlowRate = chilledWaterFlowRate;
      NominalCoolingWaterFlowRate = coolingWaterFlowRate;
      NominalHotWaterFlowRate = hotWaterFlowRate;
      ChilledWaterOutletSetpointTemperature = chilledWaterOutletTemperature;
      CyclingTimeRatio = 1.0;

      //絶対温度に変換
      double tHwi = PhysicsConstants.ToKelvin(hotWaterInletTemperature);
      double tCDwi = PhysicsConstants.ToKelvin(coolingWaterInletTemperature);
      double tCHwi = PhysicsConstants.ToKelvin(chilledWaterInletTemperature);
      double tCDwo = PhysicsConstants.ToKelvin(coolingWaterOutletTemperature);
      double tHwo = PhysicsConstants.ToKelvin(hotWaterOutletTemperature);
      double tCHwo = PhysicsConstants.ToKelvin(chilledWaterOutletTemperature);
      double tDS = tHwi - (1 - heatLossRate) * (tHwi - tHwo) / EPSILON_DS;

      //熱容量流量の計算
      double mcH = 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat * hotWaterFlowRate;
      double mcCD = 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat * coolingWaterFlowRate;
      double mcCH = 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat * chilledWaterFlowRate;

      //熱収支の確認：熱損失率の計算
      double qCDW = mcCD * (tCDwo - tCDwi);
      double qHW = mcH * (tHwi - tHwo);
      double qEvp = mcCH * (tCHwi - tCHwo);
      double qDS = qCDW - qEvp;
      heatLossRate = Math.Max(0, 1 - qDS / qHW); //熱収支を調整
      NominalCapacity = qEvp;
      NominalCOP = qEvp / qHW;

      //吸着器・脱着器処理熱量の計算
      double gamTDS = (CGAM_A * tDS + CGAM_B);
      double bb = (EPSILON_AD - 1) * tCDwi - tCDwo + CGAM_B / CGAM_A * EPSILON_AD;
      double cc = (qEvp * EPSILON_AD * gamTDS) / (mcCD * CGAM_A)
        - (EPSILON_AD - 1) * tCDwi * tCDwo - CGAM_B / CGAM_A * EPSILON_AD * tCDwo;
      double tADwo = 0.5 * (-bb - Math.Sqrt(bb * bb - 4 * cc));

      double qAD = (tADwo - tCDwi) * mcCD;
      double tAD = qAD / (mcCD * EPSILON_AD) + tCDwi;
      double qCnd = (tCDwo - tADwo) * mcCD;

      //蒸発器の伝熱係数
      double atws0 = tAD * (tAD * ATWS0[0] + ATWS0[1]) + ATWS0[2];
      double atws1 = tAD * (tAD * ATWS1[0] + ATWS1[1]) + ATWS1[2];
      double awad = mcCH * atws0 / (CGAM_A * tAD + CGAM_B);
      double bwad = (tCHwi - atws1) / atws0;
      double ademt = -Math.Log((NOM_DS0 - bwad) / (NOM_AD0 - bwad));
      double aveWad = bwad - 1d / ademt * (NOM_AD0 - bwad) * (Math.Exp(-ademt) - 1);
      double aveEvp = atws0 * aveWad + atws1;
      double epsEvp = qEvp / (mcCH * (tCHwi - aveEvp));
      evaporatorKA = -Math.Log(1 - Math.Min(0.99, epsEvp)) * mcCH;

      //凝縮器の熱通過有効度[-]
      atws0 = tDS * (tDS * ATWS0[0] + ATWS0[1]) + ATWS0[2];
      atws1 = tDS * (tDS * ATWS1[0] + ATWS1[1]) + ATWS1[2];
      double awds = mcCD * atws0 / (CGAM_A * tDS + CGAM_B);
      double bwds = (tADwo - atws1) / atws0;
      double dsemt = -Math.Log((NOM_AD0 - bwds) / (NOM_DS0 - bwds));
      double aveWds = bwds - 1d / dsemt * (NOM_DS0 - bwds) * (Math.Exp(-dsemt) - 1);
      double aveCnd = atws0 * aveWds + atws1;
      double epsCnd = qCnd / (mcCD * (aveCnd - tADwo));
      condenserKA = -Math.Log(1 - Math.Min(0.99, epsCnd)) * mcCD;

      //周期あたりの吸着材質量[kg/sec]
      double mpc1 = awad * epsEvp / ademt;
      double mpc2 = awds * epsCnd / dsemt;
      massPerCycle = Math.Min(mpc1, mpc2);

      //吸着反応器の熱容量
      double qloss = qAD - qEvp;
      heatCapacity = qloss / (massPerCycle * (1 - EPSILON_RCV) * (tDS - tAD));
    }

    /// <summary>Updates the chiller state in free-running mode (no outlet temperature control).</summary>
    /// <param name="chilledWaterInletTemperature">Chilled water inlet temperature [°C].</param>
    /// <param name="chilledWaterFlowRate">Chilled water mass flow rate [kg/s].</param>
    /// <param name="coolingWaterInletTemperature">Cooling water inlet temperature [°C].</param>
    /// <param name="coolingWaterFlowRate">Cooling water mass flow rate [kg/s].</param>
    /// <param name="hotWaterInletTemperature">Hot water inlet temperature [°C].</param>
    /// <param name="hotWaterFlowRate">Hot water mass flow rate [kg/s].</param>
    public void Update
      (double chilledWaterInletTemperature, double chilledWaterFlowRate,
      double coolingWaterInletTemperature, double coolingWaterFlowRate,
      double hotWaterInletTemperature, double hotWaterFlowRate)
    {
      //状態値を保存
      this.ChilledWaterInletTemperature = chilledWaterInletTemperature;
      this.CoolingWaterInletTemperature = coolingWaterInletTemperature;
      this.HotWaterInletTemperature = hotWaterInletTemperature;

      //停止判定
      if (chilledWaterFlowRate <= 0 || coolingWaterFlowRate <= 0 || hotWaterFlowRate <= 0
        || hotWaterInletTemperature < coolingWaterInletTemperature || coolingWaterInletTemperature < chilledWaterInletTemperature)
      {
        ShutOff();
        return;
      }

      //流量調整
      double rch = chilledWaterFlowRate / NominalChilledWaterFlowRate;
      this.ChilledWaterFlowRate = Math.Max(ChilledWaterMinFlowRatio,
        Math.Min(1.4, rch)) * NominalChilledWaterFlowRate;
      double rcd = coolingWaterFlowRate / NominalCoolingWaterFlowRate;
      this.CoolingWaterFlowRate = Math.Max(CoolingWaterMinFlowRatio,
        Math.Min(1.4, rcd)) * NominalCoolingWaterFlowRate;
      double rht = hotWaterFlowRate / NominalHotWaterFlowRate;
      this.HotWaterFlowRate = Math.Max(HotWaterMinFlowRatio,
        Math.Min(1.4, rht)) * NominalHotWaterFlowRate;

      //冷却運転
      if (ChilledWaterOutletSetpointTemperature < chilledWaterInletTemperature)
      {
        //絶対温度に変換
        double thwi = PhysicsConstants.ToKelvin(HotWaterInletTemperature);
        double tcwi = PhysicsConstants.ToKelvin(CoolingWaterInletTemperature);
        double tchwi = PhysicsConstants.ToKelvin(ChilledWaterInletTemperature);

        //熱容量流量の計算
        double mcH = 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat * HotWaterFlowRate;
        double mcCD = 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat * CoolingWaterFlowRate;
        double mcCH = 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat * ChilledWaterFlowRate;
        double epsEvp = 1 - Math.Exp(-evaporatorKA / mcCH);
        double epsCnd = 1 - Math.Exp(-condenserKA / mcCD);

        //サイクル時間補正
        double mperc = massPerCycle / CyclingTimeRatio;

        //吸着温度を収束計算
        double qEVP, tDS, tAD, qAD, qDS, qCND, wad0, wadt, tADwo;
        qEVP = tDS = tAD = qAD = qDS = qCND = wad0 = wadt = tADwo = 0;
        Roots.ErrorFunction eFnc = delegate (double cop)
        {
          //熱源全体の熱収支式から脱着器投入熱を収束計算
          double gamAD, gamDS;
          gamAD = gamDS = 0;
          Roots.ErrorFunction eFnc1 = delegate (double thwo)
          {
            double qHW = mcH * (thwi - thwo);
            qDS = qHW * (1 - heatLossRate);
            qEVP = cop * qHW;
            tDS = thwi - (1 - heatLossRate) * (thwi - thwo) / EPSILON_DS;
            double mtcap = mperc * (1 - EPSILON_RCV) * heatCapacity;
            tAD = (qEVP + EPSILON_AD * mcCD * tcwi + mtcap * tDS) / (EPSILON_AD * mcCD + mtcap);
            gamDS = CGAM_A * tDS + CGAM_B;
            gamAD = CGAM_A * tAD + CGAM_B;
            qCND = qEVP * (gamDS / gamAD);
            qAD = EPSILON_AD * mcCD * (tAD - tcwi);
            return (qCND + qAD) - (qDS + qEVP);
          };
          double tDSwo = Roots.Newton(eFnc1, thwi - 1, 0.00001, 0.00001, 0.0001, 20);
          tADwo = tcwi + EPSILON_AD * (tAD - tcwi);

          //吸着器入口含水率を収束計算
          double atws0ad = tAD * (tAD * ATWS0[0] + ATWS0[1]) + ATWS0[2];
          double atws1ad = tAD * (tAD * ATWS1[0] + ATWS1[1]) + ATWS1[2];
          double awad = mcCH * atws0ad / gamAD;
          double bwad = (tchwi - atws1ad) / atws0ad;
          double atws0ds = tDS * (tDS * ATWS0[0] + ATWS0[1]) + ATWS0[2];
          double atws1ds = tDS * (tDS * ATWS1[0] + ATWS1[1]) + ATWS1[2];
          double awds = mcCD * atws0ds / gamDS;
          double bwds = (tADwo - atws1ds) / atws0ds;
          Roots.ErrorFunction eFnc2 = delegate (double ws)
          {
            wadt = Math.Max(ws, bwad + Math.Exp(-awad * epsEvp / mperc) * (ws - bwad));
            if (MAX_W < wadt) CorrectAdsorption(ref wadt, tAD, atws0ad, atws1ad);
            wad0 = Math.Min(wadt, (bwds + Math.Exp(-awds * epsCnd / mperc) * (wadt - bwds)));
            if (wad0 < MIN_W) CorrectAdsorption(ref wad0, tDS, atws0ds, atws1ds);
            return wad0 - ws;
          };
          wad0 = Roots.NewtonBisection(eFnc2, MIN_W, 0.00001, 0.00001, 0.0001, 20);

          return (qEVP / (gamAD * mperc)) - (wadt - wad0);
        };
        if (0.001 < Math.Abs(eFnc(0.0)))
          Roots.Bisection(eFnc, 0.0, 0.8, 0.001, 0.0001, 20);
        //Roots.NewtonBisection(eFnc, 0.45, 0.001, 0.001, 0.0001, 20);  //ニュートン法だと稀に飛ぶ

        ChilledWaterOutletTemperature = ChilledWaterInletTemperature - qEVP / mcCH;
        HotWaterOutletTemperature = HotWaterInletTemperature - qDS / (1 - heatLossRate) / mcH;
        CoolingWaterOutletTemperature = CoolingWaterInletTemperature + (qCND + qAD) / mcCD;
        CoolingLoad = qEVP;
        WaterContent_Desorption = wad0;
        WaterContent_Adsorption = wadt;
        OperatingTimeRatio = 1.0;
      }
      //運転停止
      else ShutOff();
    }

    /// <summary>Updates the chiller state (controls outlet temperature to setpoint).</summary>
    /// <param name="chilledWaterInletTemperature">Chilled water inlet temperature [°C].</param>
    /// <param name="chilledWaterFlowRate">Chilled water mass flow rate [kg/s].</param>
    /// <param name="coolingWaterInletTemperature">Cooling water inlet temperature [°C].</param>
    /// <param name="coolingWaterFlowRate">Cooling water mass flow rate [kg/s].</param>
    /// <param name="hotWaterInletTemperature">Hot water inlet temperature [°C].</param>
    /// <param name="hotWaterFlowRate">Hot water mass flow rate [kg/s].</param>
    /// <param name="controlOutletTemperature">True to control outlet temperature to the setpoint.</param>
    public void Update
      (double chilledWaterInletTemperature, double chilledWaterFlowRate,
      double coolingWaterInletTemperature, double coolingWaterFlowRate,
      double hotWaterInletTemperature, double hotWaterFlowRate, bool controlOutletTemperature)
    {
      //成り行き運転
      Update
        (chilledWaterInletTemperature, chilledWaterFlowRate,
        coolingWaterInletTemperature, coolingWaterFlowRate,
        hotWaterInletTemperature, hotWaterFlowRate);

      //出口温度を制御しない場合は終了
      if (!controlOutletTemperature || (ChilledWaterOutletTemperature == ChilledWaterInletTemperature)) return;

      //理想的な発停を前提に出口温度を制御
      OperatingTimeRatio = (ChilledWaterOutletSetpointTemperature - ChilledWaterInletTemperature)
        / (ChilledWaterOutletTemperature - ChilledWaterInletTemperature);
      //過負荷の場合には制御不可能：成り行き運転
      if (1.0 <= OperatingTimeRatio) return;
      ChilledWaterOutletTemperature = ChilledWaterOutletSetpointTemperature;
      HotWaterOutletTemperature = HotWaterInletTemperature * (1 - OperatingTimeRatio) + HotWaterOutletTemperature * OperatingTimeRatio;
      CoolingWaterOutletTemperature = CoolingWaterInletTemperature * (1 - OperatingTimeRatio) + CoolingWaterOutletTemperature * OperatingTimeRatio;
      CoolingLoad *= OperatingTimeRatio;
    }

    private void CorrectAdsorption
      (ref double adsorption, double temperature, double atws0, double atws1)
    {
      if (MAX_W < adsorption || adsorption < MIN_W)
      {
        double tws = atws0 * adsorption + atws1;
        double rp = Water.GetSaturationPressure(PhysicsConstants.ToCelsius(tws)) / Water.GetSaturationPressure(PhysicsConstants.ToCelsius(temperature));
        adsorption = GetAdsorption(temperature, Math.Min(rp, 1.0));
      }
    }

    /// <summary>Shuts off the chiller.</summary>
    public void ShutOff()
    {
      ChilledWaterOutletTemperature = ChilledWaterInletTemperature;
      CoolingWaterOutletTemperature = CoolingWaterInletTemperature;
      HotWaterOutletTemperature = HotWaterInletTemperature;
      ChilledWaterFlowRate = CoolingWaterFlowRate = HotWaterFlowRate = 0;
      CoolingLoad = 0;
      OperatingTimeRatio = 0.0;
    }

    /// <summary>Computes the equilibrium water content [kg/kg] for the given adsorbent temperature and relative pressure.</summary>
    /// <param name="temperature">Adsorbent temperature [K].</param>
    /// <param name="relativePressure">Relative pressure [kPa].</param>
    /// <returns>Equilibrium water content [kg/kg].</returns>
    private static double GetAdsorption
      (double temperature, double relativePressure)
    {
      double aw0 = temperature * (2.6540e-6 * temperature - 1.9572e-3) + 5.3561e-1;
      double aw1 = temperature * (-4.0912e-3 * temperature + 2.7430) - 5.5037e2;
      double aw2 = temperature * (7.2946e-4 * temperature - 2.9366e-1) + 5.1854e1;
      double aw3 = temperature * (-3.8458e-6 * temperature + 2.1582e-3) - 2.5674e-1;
      return aw0 / (1 + Math.Exp(aw1 * relativePressure + aw2)) + aw3 * relativePressure;
    }

  }
}