/* AbsorptionRefrigerationCycle.cs
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
using Popolo.Core.HVAC.HeatExchanger;

namespace Popolo.Core.HVAC.HeatSource
{
  /// <summary>Provides static methods for single-effect and double-effect absorption refrigeration cycle calculations.</summary>
  public static class AbsorptionRefrigerationCycle
  {

    #region Constant declarations


    /// <summary>Nominal evaporating temperature [°C].</summary>
    /// <remarks>A lower limit of approximately 5°C applies due to vacuum maintenance requirements.</remarks>
    public const double NominalEvaporatingTemperature = 5;

    /// <summary>Nominal condensing temperature [°C].</summary>
    public const double NominalCondensingTemperature = 40;

    /// <summary>Nominal desorption temperature (solution side) [°C].</summary>
    /// <remarks>An upper limit of approximately 160°C applies due to mild-steel corrosion concerns.</remarks>
    public const double NominalDesorberLiquidTemperature = 155;

    /// <summary>Nominal desorption temperature (saturated vapour side) [°C].</summary>
    /// <remarks>To avoid pressure vessel requirements, keeping pressure below atmospheric gives approximately 98°C.</remarks>
    public const double NominalDesorberVaporTemperature = 98;
    
    /// <summary>Heat loss fraction relative to the high-temperature desorber heat input.</summary>
    private const double HeatLossFraction = 0.05;

    #endregion

    #region Single-effect absorption refrigeration cycle methods

    /// <summary>Computes the overall heat transfer conductances [kW/K] from rated operating conditions.</summary>
    /// <param name="chWaterITemperature">Chilled water inlet temperature [°C].</param>
    /// <param name="chWaterOTemperature">Chilled water outlet temperature [°C].</param>
    /// <param name="chWaterFlowRate">Chilled water mass flow rate [kg/s].</param>
    /// <param name="cdWaterITemperature">Cooling water inlet temperature [°C].</param>
    /// <param name="cdWaterOTemperature">Cooling water outlet temperature [°C].</param>
    /// <param name="cdWaterFlowRate">Cooling water mass flow rate [kg/s].</param>
    /// <param name="htWaterITemperature">Hot water inlet temperature [°C].</param>
    /// <param name="hotWaterFlowRate">Hot water mass flow rate [kg/s].</param>
    /// <param name="dsbTemperatureApploach">Desorption temperature approach [°C].</param>
    /// <param name="evaporatorKA">Evaporator overall heat transfer conductance [kW/K].</param>
    /// <param name="condenserKA">Condenser (absorber) overall heat transfer conductance [kW/K].</param>
    /// <param name="desorborKA">Desorber overall heat transfer conductance [kW/K].</param>
    /// <param name="hexKA">Solution heat exchanger overall heat transfer conductance [kW/K].</param>
    /// <param name="solFlowRate">Dilute solution circulation rate [kg/s].</param>
    /// <param name="desorbHeat">High-temperature desorber heat input [kW].</param>
    public static void GetHeatTransferCoefficients
      (double chWaterITemperature, double chWaterOTemperature, double chWaterFlowRate, double cdWaterITemperature,
      double cdWaterOTemperature, double cdWaterFlowRate, double htWaterITemperature, double hotWaterFlowRate, 
      double dsbTemperatureApploach, out double evaporatorKA, out double condenserKA, out double desorborKA,
      out double hexKA, out double solFlowRate, out double desorbHeat)
    {
      //Heat transfer coefficients KA of the condenser (absorber) and the evaporator [kW/K]
      evaporatorKA = GetRefrigerantHexKA(chWaterITemperature, chWaterOTemperature, chWaterFlowRate, NominalEvaporatingTemperature);
      condenserKA = GetRefrigerantHexKA(cdWaterITemperature, cdWaterOTemperature, cdWaterFlowRate, NominalCondensingTemperature);

      //Heat input to the regenerator [kW]
      double qE = chWaterFlowRate * 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat * (chWaterITemperature - chWaterOTemperature);
      double qCDAB = cdWaterFlowRate * 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat * (cdWaterOTemperature - cdWaterITemperature);
      desorbHeat = qCDAB - qE;
      double hotWaterOutletTemperature =
        htWaterITemperature - desorbHeat / (0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat * hotWaterFlowRate);

      //Solution states at the regenerator and absorber outlets
      LithiumBromide lbDo = LithiumBromide.MakeFromLiquidTemperatureAndVaporTemperature
        (PhysicsConstants.ToKelvin(htWaterITemperature - dsbTemperatureApploach), PhysicsConstants.ToKelvin(NominalCondensingTemperature));
      LithiumBromide lbAo = LithiumBromide.MakeFromLiquidTemperatureAndVaporTemperature
        (PhysicsConstants.ToKelvin(NominalCondensingTemperature), PhysicsConstants.ToKelvin(NominalEvaporatingTemperature));

      //Solution circulation ratio [-]
      double aW = lbDo.MassFraction / (lbDo.MassFraction - lbAo.MassFraction);

      //Specific enthalpies of the refrigerant [kJ/kg]
      double hRVDo = Water.GetSaturatedVaporEnthalpy(NominalCondensingTemperature);
      double hRLEi = Water.GetSaturatedLiquidEnthalpy(NominalCondensingTemperature);
      double hRVEo = Water.GetSaturatedVaporEnthalpy(NominalEvaporatingTemperature);

      //Circulation rates of the refrigerant and the solution [kg/s]
      double mR = qE / (hRVEo - hRLEi);
      solFlowRate = mR * aW;
      double mAi = solFlowRate - mR;

      //Heat transfer coefficient KA of the solution heat exchanger [kW/K]
      double hSDi = (mR * hRVDo + lbDo.Enthalpy * mAi - desorbHeat) / solFlowRate;
      double qX = (hSDi - lbAo.Enthalpy) * solFlowRate;
      hexKA = HeatExchange.GetHeatTransferCoefficient(lbDo.LiquidTemperature, lbAo.LiquidTemperature, 
        lbDo.SpecificHeat * mAi, lbAo.SpecificHeat * solFlowRate, qX, HeatExchange.FlowType.CounterFlow);

      //Heat transfer coefficient KA of the regenerator [kW/K]
      LithiumBromide lbDi2 = LithiumBromide.MakeFromEnthalpyAndVaporTemperature(hSDi, PhysicsConstants.ToKelvin(NominalCondensingTemperature));
      double cp = GetSolutionAverageSpecificHeat(lbDi2, lbDo);
      double mcHW = hotWaterFlowRate * 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat;
      double mcSL = solFlowRate * cp;
      double mcMin = Math.Min(mcHW, mcSL);
      double mcMax = Math.Max(mcHW, mcSL);
      double effectiveness = desorbHeat / (mcMin * (htWaterITemperature - (PhysicsConstants.ToCelsius(lbDi2.LiquidTemperature))));
      desorborKA = HeatExchange.GetNTU(effectiveness, mcMin / mcMax, HeatExchange.FlowType.CounterFlow) * mcMin;
    }

    /// <summary>Computes the outlet temperatures in free-running mode.</summary>
    /// <param name="chWaterITemperature">Chilled water inlet temperature [°C].</param>
    /// <param name="chWaterFlowRate">Chilled water mass flow rate [kg/s].</param>
    /// <param name="cdWaterITemperature">Cooling water inlet temperature [°C].</param>
    /// <param name="cdWaterFlowRate">Cooling water mass flow rate [kg/s].</param>
    /// <param name="htWaterITemperature">Hot water inlet temperature [°C].</param>
    /// <param name="htWaterFlowRate">Hot water mass flow rate [kg/s].</param>
    /// <param name="evaporatorKA">Evaporator overall heat transfer conductance [kW/K].</param>
    /// <param name="condenserKA">Condenser overall heat transfer conductance [kW/K].</param>
    /// <param name="desorborKA">Desorber overall heat transfer conductance [kW/K].</param>
    /// <param name="hexKA">Solution heat exchanger overall heat transfer conductance [kW/K].</param>
    /// <param name="solFlowRate">Solution mass flow rate [kg/s].</param>
    /// <param name="chWaterOTemperature">Output: chilled water outlet temperature [°C].</param>
    /// <param name="cdWaterOTemperature">Output: cooling water outlet temperature [°C].</param>
    /// <param name="htWaterOTemperature">Output: hot water outlet temperature [°C].</param>
    public static void GetOutletTemperatures
      (double chWaterITemperature, double chWaterFlowRate, double cdWaterITemperature, 
      double cdWaterFlowRate, double htWaterITemperature, double htWaterFlowRate, double evaporatorKA, 
      double condenserKA, double desorborKA, double hexKA, double solFlowRate,
      out double chWaterOTemperature, out double cdWaterOTemperature, out double htWaterOTemperature)
    {
      double tcdo = 0;
      double tho = 0;
      Minimization.MinimizeFunction mFnc = delegate (double tcho)
      {
        return GetError(chWaterITemperature, chWaterFlowRate, cdWaterITemperature, cdWaterFlowRate,
          htWaterITemperature, htWaterFlowRate, evaporatorKA, condenserKA, desorborKA, hexKA, solFlowRate, tcho, 
          out tcdo, out tho);
      };

      chWaterOTemperature = NominalEvaporatingTemperature + 0.001;
      Minimization.GoldenSection(ref chWaterOTemperature, chWaterITemperature - 0.001, mFnc);
      cdWaterOTemperature = tcdo;
      htWaterOTemperature = tho;
    }

    /// <summary>Computes the outlet temperatures for the specified chilled water outlet temperature.</summary>
    /// <param name="chWaterITemperature">Chilled water inlet temperature [°C].</param>
    /// <param name="chWaterFlowRate">Chilled water mass flow rate [kg/s].</param>
    /// <param name="cdWaterITemperature">Cooling water inlet temperature [°C].</param>
    /// <param name="cdWaterFlowRate">Cooling water mass flow rate [kg/s].</param>
    /// <param name="htWaterITemperature">Hot water inlet temperature [°C].</param>
    /// <param name="htWaterFlowRate">Hot water mass flow rate [kg/s].</param>
    /// <param name="evaporatorKA">Evaporator overall heat transfer conductance [kW/K].</param>
    /// <param name="condenserKA">Condenser overall heat transfer conductance [kW/K].</param>
    /// <param name="desorborKA">Desorber overall heat transfer conductance [kW/K].</param>
    /// <param name="hexKA">Solution heat exchanger overall heat transfer conductance [kW/K].</param>
    /// <param name="solFlowRate">Solution mass flow rate [kg/s].</param>
    /// <param name="chWaterOTemperatureSP">Chilled water outlet temperature setpoint [°C].</param>
    /// <param name="cdWaterOTemperature">Output: cooling water outlet temperature [°C].</param>
    /// <param name="htWaterOTemperature">Output: hot water outlet temperature [°C].</param>
    public static void GetOutletTemperatures(double chWaterITemperature, double chWaterFlowRate,
      double cdWaterITemperature, double cdWaterFlowRate, double htWaterITemperature, double htWaterFlowRate,
      double evaporatorKA, double condenserKA, double desorborKA, double hexKA, double solFlowRate,
      double chWaterOTemperatureSP, out double cdWaterOTemperature, out double htWaterOTemperature)
    {
      double tcdo = 0;
      double tho = 0;
      Minimization.MinimizeFunction mFnc = delegate (double hwr)
      {
        return GetError(chWaterITemperature, chWaterFlowRate, cdWaterITemperature, cdWaterFlowRate,
          htWaterITemperature, htWaterFlowRate * hwr, evaporatorKA, condenserKA, desorborKA, hexKA,
          solFlowRate, chWaterOTemperatureSP, out tcdo, out tho);
      };

      double hwRatio = 0.01;
      Minimization.GoldenSection(ref hwRatio, 1.0, mFnc);
      cdWaterOTemperature = tcdo;
      htWaterOTemperature = tho * hwRatio + htWaterITemperature * (1 - hwRatio);
    }

    /// <summary>Error function for the single-effect absorption refrigeration cycle.</summary>
    /// <param name="chWaterITemperature">Chilled water inlet temperature [°C].</param>
    /// <param name="chWaterFlowRate">Chilled water mass flow rate [kg/s].</param>
    /// <param name="cdWaterITemperature">Cooling water inlet temperature [°C].</param>
    /// <param name="cdWaterFlowRate">Cooling water mass flow rate [kg/s].</param>
    /// <param name="htWaterITemperature">Hot water inlet temperature [°C].</param>
    /// <param name="htWaterFlowRate">Hot water mass flow rate [kg/s].</param>
    /// <param name="evaporatorKA">Evaporator overall heat transfer conductance [kW/K].</param>
    /// <param name="condenserKA">Condenser overall heat transfer conductance [kW/K].</param>
    /// <param name="desorborKA">Desorber overall heat transfer conductance [kW/K].</param>
    /// <param name="hexKA">Solution heat exchanger overall heat transfer conductance [kW/K].</param>
    /// <param name="solFlowRate">Solution mass flow rate [kg/s].</param>
    /// <param name="chWaterOTemperature">Chilled water outlet temperature [°C].</param>
    /// <param name="cdWaterOTemperature">Output: cooling water outlet temperature [°C].</param>
    /// <param name="htWaterOTemperature">Output: hot water outlet temperature [°C].</param>
    /// <returns>Single-effect absorption cycle error value.</returns>
    private static double GetError(double chWaterITemperature, double chWaterFlowRate,
      double cdWaterITemperature, double cdWaterFlowRate, double htWaterITemperature,
      double htWaterFlowRate, double evaporatorKA, double condenserKA, double desorborKA, double hexKA,
      double solFlowRate, double chWaterOTemperature,
      out double cdWaterOTemperature, out double htWaterOTemperature)
    {
      //Compute the evaporating temperature
      double qE = chWaterFlowRate * 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat * (chWaterITemperature - chWaterOTemperature);
      double evaporatingTemperature = GetRefrigerantTemperature
        (chWaterITemperature, chWaterOTemperature, chWaterFlowRate, evaporatorKA);
      double hRVEo = Water.GetSaturatedVaporEnthalpy(evaporatingTemperature);

      LithiumBromide lbAo = null!;
      LithiumBromide lbDo = null!;
      double qX = 0;
      double condensingTemperature = 0;
      double tcdo = 0;
      Roots.ErrorFunction eFnc = delegate (double dsvH)
      {
        //Condensing temperature and specific enthalpies
        double qCDAB = qE + dsvH;
        tcdo = cdWaterITemperature + qCDAB / (cdWaterFlowRate * 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat);
        condensingTemperature = GetRefrigerantTemperature
        (cdWaterITemperature, tcdo, cdWaterFlowRate, condenserKA);
        double hRVDo = Water.GetSaturatedVaporEnthalpy(condensingTemperature);
        double hRLCDo = Water.GetSaturatedLiquidEnthalpy(condensingTemperature);

        //Refrigerant flow rate and solution circulation ratio [-]
        double mR = qE / (hRVEo - hRLCDo);
        double aW = solFlowRate / mR;
        double mSAi = solFlowRate - mR;

        //Solution states at the absorber and regenerator outlets
        lbAo = LithiumBromide.MakeFromLiquidTemperatureAndVaporTemperature
        (PhysicsConstants.ToKelvin(condensingTemperature), PhysicsConstants.ToKelvin(evaporatingTemperature));
        lbDo = LithiumBromide.MakeFromVaporTemperatureAndMassFraction
        (PhysicsConstants.ToKelvin(condensingTemperature), aW / (aW - 1) * lbAo.MassFraction);

        //Heat processed by the solution heat exchanger based on the cooling water heat
        double qAB = qCDAB - mR * (hRVDo - hRLCDo);
        double hSAi = lbAo.Enthalpy + (qAB - mR * (hRVEo - lbAo.Enthalpy)) / mSAi;
        qX = (lbDo.Enthalpy - hSAi) * mSAi;

        //Heat processed based on the solution heat exchanger heat transfer coefficient
        double qX2 = HeatExchange.GetHeatTransfer
        (lbDo.LiquidTemperature, lbAo.LiquidTemperature, lbDo.SpecificHeat * mSAi,
        lbAo.SpecificHeat * solFlowRate, hexKA, HeatExchange.FlowType.CounterFlow);

        return qX - qX2;
      };

      //Iterate to solve the heat input
      double desorbHeat = qE / 0.75;
      desorbHeat = Roots.Newton(eFnc, desorbHeat, 0.001, 0.0001, desorbHeat * 0.001, 20);

      //Compute the cooling water and hot water outlet temperatures
      cdWaterOTemperature = tcdo;
      htWaterOTemperature = htWaterITemperature - desorbHeat / (htWaterFlowRate * 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat);

      //Compute the desorption temperature required by the regenerator
      LithiumBromide lbDi = LithiumBromide.MakeFromEnthalpyAndMassFraction
        (lbAo.Enthalpy + qX / solFlowRate, lbAo.MassFraction);
      LithiumBromide lbDi2 = LithiumBromide.MakeFromEnthalpyAndVaporTemperature
        (lbDi.Enthalpy, PhysicsConstants.ToKelvin(condensingTemperature));
      double desorbTemp = GetDesorbTemperature
        (desorborKA, desorbHeat, htWaterFlowRate, lbDi2, lbDo, solFlowRate);

      //If the hot water inlet temperature is below the required temperature
      if (0 < desorbTemp - (PhysicsConstants.ToKelvin(htWaterITemperature)))
        return desorbTemp - (PhysicsConstants.ToKelvin(htWaterITemperature));
      //Otherwise compute the surplus hot water flow rate
      else
        return htWaterFlowRate - GetHotWaterFlowRate
          (desorborKA, desorbHeat, PhysicsConstants.ToKelvin(htWaterITemperature), lbDi2, lbDo, solFlowRate);
    }

    #endregion

    #region Double-effect absorption refrigeration cycle methods

    /// <summary>Computes the overall heat transfer conductances [kW/K] from rated operating conditions.</summary>
    /// <param name="chWaterITemperature">Chilled water inlet temperature [°C].</param>
    /// <param name="chWaterOTemperature">Chilled water outlet temperature [°C].</param>
    /// <param name="cdWaterITemperature">Cooling water inlet temperature [°C].</param>
    /// <param name="cdWaterOTemperature">Cooling water outlet temperature [°C].</param>
    /// <param name="chWaterFlowRate">Chilled water mass flow rate [kg/s].</param>
    /// <param name="cdWaterFlowRate">Cooling water mass flow rate [kg/s].</param>
    /// <param name="evaporatorKA">Evaporator overall heat transfer conductance [kW/K].</param>
    /// <param name="condenserKA">Condenser (absorber) overall heat transfer conductance [kW/K].</param>
    /// <param name="lowDesorborKA">Low-temperature desorber overall heat transfer conductance [kW/K].</param>
    /// <param name="lHexKA">Low-temperature solution heat exchanger conductance [kW/K].</param>
    /// <param name="solFlowRate">Dilute solution circulation rate [kg/s].</param>
    /// <param name="desorbHeat">High-temperature desorber heat input [kW].</param>
    public static void GetHeatTransferCoefficients
      (double chWaterITemperature, double chWaterOTemperature, double chWaterFlowRate,
      double cdWaterITemperature, double cdWaterOTemperature, double cdWaterFlowRate,
      out double evaporatorKA, out double condenserKA, out double lowDesorborKA, 
      out double lHexKA, out double solFlowRate, out double desorbHeat)
    {
      //Heat transfer coefficients KA of the condenser (absorber) and the evaporator [kW/K]
      evaporatorKA = GetRefrigerantHexKA(chWaterITemperature, chWaterOTemperature, chWaterFlowRate, NominalEvaporatingTemperature);
      condenserKA = GetRefrigerantHexKA(cdWaterITemperature, cdWaterOTemperature, cdWaterFlowRate, NominalCondensingTemperature);

      //Compute the heat input to the regenerator [kW]
      double qE = chWaterFlowRate * 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat * (chWaterITemperature - chWaterOTemperature);
      double qCDAB = cdWaterFlowRate * 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat * (cdWaterOTemperature - cdWaterITemperature);
      double qD = desorbHeat = (qCDAB - qE) / (1 - HeatLossFraction);

      //Compute the solution states
      LithiumBromide lbHDo = LithiumBromide.MakeFromLiquidTemperatureAndVaporTemperature
        (PhysicsConstants.ToKelvin(NominalDesorberLiquidTemperature), PhysicsConstants.ToKelvin(NominalDesorberVaporTemperature));
      LithiumBromide lbAo = LithiumBromide.MakeFromLiquidTemperatureAndVaporTemperature
        (PhysicsConstants.ToKelvin(NominalCondensingTemperature), PhysicsConstants.ToKelvin(NominalEvaporatingTemperature));
      LithiumBromide lbLDo = LithiumBromide.MakeFromVaporTemperatureAndMassFraction
        (PhysicsConstants.ToKelvin(NominalCondensingTemperature), lbHDo.MassFraction);

      //Solution circulation ratio [-]
      double aW = lbHDo.MassFraction / (lbHDo.MassFraction - lbAo.MassFraction);

      //Compute the refrigerant specific enthalpies [kJ/kg]
      double hRVHDo = Water.GetSaturatedVaporEnthalpy(NominalDesorberVaporTemperature);
      double hRLHDo = Water.GetSaturatedLiquidEnthalpy(NominalDesorberVaporTemperature);
      double hRVLDo = Water.GetSaturatedVaporEnthalpy(NominalCondensingTemperature);
      double hRLEi = Water.GetSaturatedLiquidEnthalpy(NominalCondensingTemperature);
      double hRVEo = Water.GetSaturatedVaporEnthalpy(NominalEvaporatingTemperature);

      //Refrigerant circulation rate [kg/s]
      double mR = qE / (hRVEo - hRLEi);
      solFlowRate = mR * aW;

      //Variables holding solution states
      LithiumBromide lbLDi = null!;  //solution at the low-temperature regenerator inlet
      LithiumBromide lbAi = null!;  //solution at the low-temperature heat exchanger outlet
      double mRH = 0;  //high-temperature side refrigerant flow rate [kg/s]
      double mRL = 0;  //low-temperature side refrigerant flow rate [kg/s]

      //Define the error function
      Roots.ErrorFunction eFnc = delegate (double rhgRate)
      {
        //Compute the refrigerant flow rates [kg/s]
        mRH = mR * rhgRate;
        mRL = mR - mRH;
        double mSAo = mR * aW;
        double mSAi = mSAo - mR;

        //Heat processed by the condenser and the absorber [kW]
        double qCD = (hRLHDo - hRLEi) * mRH + (hRVLDo - hRLEi) * mRL - qD * HeatLossFraction;
        double qAB = qCDAB - qCD;

        //Solution at the low-temperature solution heat exchanger outlet
        double hSAi = lbAo.Enthalpy + (qAB - mR * (hRVEo - lbAo.Enthalpy)) / mSAi;
        lbAi = LithiumBromide.MakeFromEnthalpyAndMassFraction(hSAi, lbHDo.MassFraction);

        //Solution at the low-temperature regenerator inlet
        double hLDi = lbAo.Enthalpy + (lbLDo.Enthalpy - lbAi.Enthalpy) * mSAi / mSAo;
        lbLDi = LithiumBromide.MakeFromEnthalpyAndMassFraction(hLDi, lbAo.MassFraction);

        //Heat input to the low-temperature regenerator
        double qLD1 = (hRVHDo - hRLHDo) * mRH;
        double qLD2 = hRVLDo * mRL + lbLDo.Enthalpy * (mRL * (aW - 1)) - hLDi * (mRL * aW);

        return qLD1 - qLD2;
      };

      //Iterate to solve the solution distribution ratio [-]
      double rRatio = Roots.Newton(eFnc, 0.5, 0.001, 0.0001, 0.0001, 20);

      //Compute the heat transfer coefficient KA of the low-temperature regenerator [kW/K]
      LithiumBromide lbLDi2 = LithiumBromide.MakeFromEnthalpyAndVaporTemperature
        (lbLDi.Enthalpy, PhysicsConstants.ToKelvin(NominalCondensingTemperature));
      double cp = GetSolutionAverageSpecificHeat(lbLDi2, lbLDo);
      double effectiveness = (lbLDo.LiquidTemperature - lbLDi2.LiquidTemperature) 
        / (PhysicsConstants.ToKelvin(NominalDesorberVaporTemperature) - lbLDi2.LiquidTemperature);
      lowDesorborKA = -Math.Log(1 - effectiveness) * (cp * (mRL * aW));

      //Compute the heat transfer coefficient KA of the solution heat exchanger [kW/K]
      double qLX = (lbLDo.Enthalpy - lbAi.Enthalpy) * mR * (aW - 1);
      double mcH = lbLDo.SpecificHeat * mR * (aW - 1);
      double mcC = lbAo.SpecificHeat * mR * aW;
      lHexKA = HeatExchange.GetHeatTransferCoefficient
        (lbLDo.LiquidTemperature, lbAo.LiquidTemperature, mcC, mcH, qLX, HeatExchange.FlowType.CounterFlow);
    }

    /// <summary>Computes the chilled water outlet temperature [°C].</summary>
    /// <param name="chWaterITemperature">Chilled water inlet temperature [°C].</param>
    /// <param name="chWaterFlowRate">Chilled water mass flow rate [kg/s].</param>
    /// <param name="cdWaterITemperature">Cooling water inlet temperature [°C].</param>
    /// <param name="cdWaterFlowRate">Cooling water mass flow rate [kg/s].</param>
    /// <param name="evaporatorKA">Evaporator overall heat transfer conductance [kW/K].</param>
    /// <param name="condenserKA">Condenser overall heat transfer conductance [kW/K].</param>
    /// <param name="lowDesorborKA">Low-temperature desorber overall heat transfer conductance [kW/K].</param>
    /// <param name="lHexKA">Low-temperature solution heat exchanger conductance [kW/K].</param>
    /// <param name="solFlowRate">Solution mass flow rate [kg/s].</param>
    /// <param name="desorbHeat">High-temperature desorber heat input [kW].</param>
    /// <param name="cdWaterOTemperature">Output: cooling water outlet temperature [°C].</param>
    /// <param name="dsbTemperature">Output: high-temperature desorber temperature [°C].</param>
    /// <param name="evpTemperature">Output: evaporating temperature [°C].</param>
    /// <param name="cndTemperature">Output: condensing temperature [°C].</param>
    /// <param name="thinMFraction">Output: dilute solution mass fraction [-].</param>
    /// <param name="thickMFraction">Output: concentrated solution mass fraction [-].</param>
    /// <returns>Chilled water outlet temperature [°C].</returns>
    public static double GetChilledWaterOutletTemperature
      (double chWaterITemperature, double chWaterFlowRate, double cdWaterITemperature, double cdWaterFlowRate,
      double evaporatorKA, double condenserKA, double lowDesorborKA, double lHexKA, double solFlowRate,
      double desorbHeat, out double cdWaterOTemperature, out double dsbTemperature, out double evpTemperature, 
      out double cndTemperature, out double thinMFraction, out double thickMFraction)
    {
      double dtCH, dtCD;
      AdjustRange(ref chWaterITemperature, ref cdWaterITemperature, out dtCH, out dtCD);

      double tdsv, tcnd, tevp, tcdo, wth, wtk;
      tdsv = tcnd = tevp = tcdo = wth = wtk = 0;
      Roots.ErrorFunction eFnc = delegate (double tcho)
      {

        return GetError(chWaterITemperature, chWaterFlowRate, cdWaterITemperature, cdWaterFlowRate, 
          evaporatorKA, condenserKA, lowDesorborKA, lHexKA, desorbHeat, solFlowRate, tcho,
          out tcdo, out tdsv, out tevp, out tcnd, out wth, out wtk);
      };

      //Iterate to solve the chilled water outlet temperature
      double chilledWaterOutletTemperature = Roots.Newton(eFnc, NominalEvaporatingTemperature + 0.01, 0.001, 0.0001, 0.01, 20);
      dsbTemperature = tdsv;
      thinMFraction = wth;
      thickMFraction = wtk;
      cdWaterOTemperature = tcdo + dtCD;
      evpTemperature = tevp + dtCH;
      cndTemperature = tcnd + dtCD;

      return chilledWaterOutletTemperature + dtCH;
    }

    /// <summary>Computes the heat input to the high-temperature desorber [kW].</summary>
    /// <param name="chWaterITemperature">Chilled water inlet temperature [°C].</param>
    /// <param name="chWaterFlowRate">Chilled water mass flow rate [kg/s].</param>
    /// <param name="cdWaterITemperature">Cooling water inlet temperature [°C].</param>
    /// <param name="cdWaterFlowRate">Cooling water mass flow rate [kg/s].</param>
    /// <param name="evaporatorKA">Evaporator overall heat transfer conductance [kW/K].</param>
    /// <param name="condenserKA">Condenser overall heat transfer conductance [kW/K].</param>
    /// <param name="lowDesorborKA">Low-temperature desorber overall heat transfer conductance [kW/K].</param>
    /// <param name="lHexKA">Low-temperature solution heat exchanger conductance [kW/K].</param>
    /// <param name="solFlowRate">Solution mass flow rate [kg/s].</param>
    /// <param name="chWaterOTemperature">Chilled water outlet temperature setpoint [°C].</param>
    /// <param name="cdWaterOTemperature">Output: cooling water outlet temperature [°C].</param>
    /// <param name="dsbTemperature">Output: high-temperature desorber temperature [°C].</param>
    /// <param name="evpTemperature">Output: evaporating temperature [°C].</param>
    /// <param name="cndTemperature">Output: condensing temperature [°C].</param>
    /// <param name="thinMFraction">Output: dilute solution mass fraction [-].</param>
    /// <param name="thickMFraction">Output: concentrated solution mass fraction [-].</param>
    /// <returns>High-temperature desorber heat input [kW].</returns>
    public static double GetDesorbHeat
      (double chWaterITemperature, double chWaterFlowRate, double cdWaterITemperature, double cdWaterFlowRate,
      double evaporatorKA, double condenserKA, double lowDesorborKA, double lHexKA, double solFlowRate,
      double chWaterOTemperature, out double cdWaterOTemperature, out double dsbTemperature,
      out double evpTemperature, out double cndTemperature, out double thinMFraction, out double thickMFraction)
    {
      double dtCH, dtCD;
      AdjustRange(ref chWaterITemperature, ref cdWaterITemperature, out dtCH, out dtCD);
      chWaterOTemperature += dtCH;

      double tdsv, tcnd, tevp, tcdo, wth, wtk;
      tdsv = tcnd = tevp = tcdo = wth = wtk = 0;
      Roots.ErrorFunction eFnc = delegate (double dsvH)
      {
        return GetError
        (chWaterITemperature, chWaterFlowRate, cdWaterITemperature, cdWaterFlowRate, evaporatorKA, condenserKA,
        lowDesorborKA, lHexKA, dsvH, solFlowRate, chWaterOTemperature,
        out tcdo, out tdsv, out tevp, out tcnd, out wth, out wtk);
      };

      //Iterate to solve the heat input to the regenerator
      double qE = chWaterFlowRate * 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat * (chWaterITemperature - chWaterOTemperature);
      double desorbHeat = qE / 1.4;
      desorbHeat = Roots.Newton(eFnc, desorbHeat, 0.001, 0.0001, desorbHeat * 0.001, 20);
      dsbTemperature = tdsv;
      thinMFraction = wth;
      thickMFraction = wtk;
      cdWaterOTemperature = tcdo + dtCD;
      evpTemperature = tevp + dtCH;
      cndTemperature = tcnd + dtCD;

      return desorbHeat;
    }

    /// <summary>Adjusts chilled water and cooling water temperatures to satisfy energy balance constraints.</summary>
    /// <param name="tCHi">Chilled water inlet temperature [°C].</param>
    /// <param name="tCDi">Cooling water inlet temperature [°C].</param>
    /// <param name="dtCH">Output: chilled water temperature adjustment.</param>
    /// <param name="dtCD">Output: cooling water temperature adjustment.</param>
    private static void AdjustRange(ref double tCHi, ref double tCDi, out double dtCH, out double dtCD)
    {
      dtCH = dtCD = 0;
      if (tCHi < 3)
      {
        dtCH = tCHi - 5;
        tCHi = 5;
      }
      else if (18 < tCHi)
      {
        dtCH = tCHi - 18;
        tCHi = 18;
      }
      if (tCDi < 20)
      {
        dtCD = tCDi - 20;
        tCDi = 20;
      }
      else if (37 < tCDi)
      {
        dtCD = tCDi - 37;
        tCDi = 37;
      }
    }

    /// <summary>Error function for the double-effect absorption refrigeration cycle.</summary>
    /// <param name="chWaterITemperature">Chilled water inlet temperature [°C].</param>
    /// <param name="chWaterFlowRate">Chilled water mass flow rate [kg/s].</param>
    /// <param name="cdWaterITemperature">Cooling water inlet temperature [°C].</param>
    /// <param name="cdWaterFlowRate">Cooling water mass flow rate [kg/s].</param>
    /// <param name="evaporatorKA">Evaporator overall heat transfer conductance [kW/K].</param>
    /// <param name="condenserKA">Condenser overall heat transfer conductance [kW/K].</param>
    /// <param name="lowDesorborKA">Low-temperature desorber overall heat transfer conductance [kW/K].</param>
    /// <param name="lHexKA">Low-temperature solution heat exchanger conductance [kW/K].</param>
    /// <param name="desorbHeat">High-temperature desorber heat input [kW].</param>
    /// <param name="solFlowRate">Solution mass flow rate [kg/s].</param>
    /// <param name="chWaterOTemperature">Chilled water outlet temperature [°C].</param>
    /// <param name="cdWaterOTemperature">Output: cooling water outlet temperature [°C].</param>
    /// <param name="dsbTemperature">Output: high-temperature desorber temperature [°C].</param>
    /// <param name="evpTemperature">Output: evaporating temperature [°C].</param>
    /// <param name="cndTemperature">Output: condensing temperature [°C].</param>
    /// <param name="thinMFraction">Output: dilute solution mass fraction [-].</param>
    /// <param name="thickMFraction">Output: concentrated solution mass fraction [-].</param>
    /// <returns>Double-effect absorption cycle error value.</returns>
    private static double GetError
      (double chWaterITemperature, double chWaterFlowRate, double cdWaterITemperature, double cdWaterFlowRate, 
      double evaporatorKA, double condenserKA, double lowDesorborKA, double lHexKA, double desorbHeat,
      double solFlowRate, double chWaterOTemperature, out double cdWaterOTemperature, out double dsbTemperature,
      out double evpTemperature, out double cndTemperature, out double thinMFraction, out double thickMFraction)
    {
      //Cooling water outlet temperature
      double qE = chWaterFlowRate * 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat * (chWaterITemperature - chWaterOTemperature);
      double qCDAB = qE + desorbHeat / (1 + HeatLossFraction);
      cdWaterOTemperature = cdWaterITemperature + qCDAB / (cdWaterFlowRate * 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat);

      //Evaporating and condensing temperatures
      evpTemperature = GetRefrigerantTemperature
        (chWaterITemperature, chWaterOTemperature, chWaterFlowRate, evaporatorKA);
      cndTemperature = GetRefrigerantTemperature
        (cdWaterITemperature, cdWaterOTemperature, cdWaterFlowRate, condenserKA);

      //Refrigerant enthalpies at the condensing and evaporating temperatures
      double hRVLDo = Water.GetSaturatedVaporEnthalpy(cndTemperature);
      double hRLLDo = Water.GetSaturatedLiquidEnthalpy(cndTemperature);
      double hRVEo = Water.GetSaturatedVaporEnthalpy(evpTemperature);

      //Refrigerant flow rate and solution circulation ratio [-]
      double mR = qE / (hRVEo - hRLLDo);
      double aW = solFlowRate / mR;

      LithiumBromide lbAo = LithiumBromide.MakeFromLiquidTemperatureAndVaporTemperature
        (PhysicsConstants.ToKelvin(cndTemperature), PhysicsConstants.ToKelvin(evpTemperature));
      LithiumBromide lbLDo = LithiumBromide.MakeFromVaporTemperatureAndMassFraction
        (PhysicsConstants.ToKelvin(cndTemperature), aW / (aW - 1) * lbAo.MassFraction);

      double mSAo = solFlowRate;
      double mSAi = solFlowRate - mR;
      double tDesorb = 0;
      double tcndK = PhysicsConstants.ToKelvin(cndTemperature);
      Roots.ErrorFunction eFnc = delegate (double rRatio)
      {
        //Refrigerant and solution flow rates
        double mRH = mR * rRatio;
        double mRL = mR - mRH;
        double mSLDi = mSAo * (1 - rRatio);
        double mSLDo = mSLDi - mRL;

        //Desorption temperature of the low-temperature regenerator
        double qLX = HeatExchange.GetHeatTransfer
        (lbLDo.LiquidTemperature, lbAo.LiquidTemperature, lbLDo.SpecificHeat * mSAi,
        lbAo.SpecificHeat * mSAo, lHexKA, HeatExchange.FlowType.CounterFlow);
        LithiumBromide lbLDi = LithiumBromide.MakeFromEnthalpyAndMassFraction
        (lbAo.Enthalpy + qLX / mSAo, lbAo.MassFraction);
        LithiumBromide lbLDi2 = LithiumBromide.MakeFromEnthalpyAndVaporTemperature(lbLDi.Enthalpy, tcndK);
        tDesorb = GetDesorbTemperature(lowDesorborKA, mSLDi, lbLDi2, lbLDo);

        //Heat processed by the low-temperature regenerator 1 [kW]
        double hRVHD = Water.GetSaturatedVaporEnthalpy(PhysicsConstants.ToCelsius(tDesorb));
        double hRLHD = Water.GetSaturatedLiquidEnthalpy(PhysicsConstants.ToCelsius(tDesorb));
        double qLD1 = (hRVHD - hRLHD) * mRH;
        //Heat processed by the low-temperature regenerator 2 [kW]
        double qLD2 = hRVLDo * mRL + lbLDo.Enthalpy * mSLDo - lbLDi.Enthalpy * mSLDi;

        //Heat removed by the cooling water
        qCDAB = mRH * (hRLHD - hRLLDo) + mRL * (hRVLDo - hRLLDo)
        + mR * (hRVEo - lbAo.Enthalpy) + mSAi * (lbLDo.Enthalpy - lbAo.Enthalpy) - qLX;

        return qLD1 - qLD2;
      };

      //Iterate to solve the solution distribution ratio
      Roots.Newton(eFnc, 0.5, 0.001, 0.0001, 0.0001, 20);

      //Output the desorption temperature and solution mass fractions
      dsbTemperature = PhysicsConstants.ToCelsius(
        LithiumBromide.GetLiquidTemperatureFromVaporTemperatureAndMassFraction(tDesorb, lbLDo.MassFraction));
      thinMFraction = lbAo.MassFraction;
      thickMFraction = lbLDo.MassFraction;

      return (qCDAB - qE) - desorbHeat;
    }

    #endregion

    #region Evaporator and condenser methods

    /// <summary>Computes the evaporating/condensing temperature [°C].</summary>
    /// <param name="iwTemperature">Inlet water temperature [°C].</param>
    /// <param name="owTemperature">Outlet water temperature [°C].</param>
    /// <param name="wFlowRate">Water mass flow rate [kg/s].</param>
    /// <param name="heatTransferCoefficient">Evaporator/condenser overall heat transfer conductance [kW/K].</param>
    /// <returns>Evaporating/condensing temperature [°C].</returns>
    private static double GetRefrigerantTemperature
      (double iwTemperature, double owTemperature, double wFlowRate, double heatTransferCoefficient)
    {
      double ntu = heatTransferCoefficient / (wFlowRate * 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat);
      double effectiveness = 1 - Math.Exp(-ntu);
      return iwTemperature - (iwTemperature - owTemperature) / effectiveness;
    }

    /// <summary>Computes the overall heat transfer conductance of the evaporator/condenser [kW/K].</summary>
    /// <param name="iwTemperature">Inlet water temperature [°C].</param>
    /// <param name="owTemperature">Outlet water temperature [°C].</param>
    /// <param name="wFlowRate">Water mass flow rate [kg/s].</param>
    /// <param name="rTemperature">Evaporating/condensing temperature [°C].</param>
    /// <returns>Evaporator/condenser overall heat transfer conductance [kW/K].</returns>
    private static double GetRefrigerantHexKA
      (double iwTemperature, double owTemperature, double wFlowRate, double rTemperature)
    {
      double effectiveness = (iwTemperature - owTemperature) / (iwTemperature - rTemperature);
      return -Math.Log(1 - effectiveness) * wFlowRate * 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat;
    }

    #endregion

    #region Regenerator methods

    /// <summary>Computes the desorption temperature [K].</summary>
    /// <param name="desorborKA">Desorber overall heat transfer conductance [kW/K].</param>
    /// <param name="slFlowRate">Solution mass flow rate [kg/s].</param>
    /// <param name="iSolution">Inlet solution state.</param>
    /// <param name="oSolution">Outlet solution state.</param>
    /// <returns>Desorption temperature [K].</returns>
    private static double GetDesorbTemperature
      (double desorborKA, double slFlowRate, LithiumBromide iSolution, LithiumBromide oSolution)
    {
      double cp = GetSolutionAverageSpecificHeat(iSolution, oSolution);
      double effectiveness = 1 - Math.Exp(-desorborKA / (cp * slFlowRate));
      return (oSolution.LiquidTemperature - iSolution.LiquidTemperature) / effectiveness + iSolution.LiquidTemperature;
    }

    /// <summary>Computes the desorption temperature [K].</summary>
    /// <param name="desorborKA">Desorber overall heat transfer conductance [kW/K].</param>
    /// <param name="desorbHeat">Desorber heat input [kW].</param>
    /// <param name="hwFlowRate">Hot water flow rate [kg/s].</param>
    /// <param name="iSolution">Inlet solution state.</param>
    /// <param name="oSolution">Outlet solution state.</param>
    /// <param name="slFlowRate">Solution mass flow rate [kg/s].</param>
    /// <returns>Desorption temperature [K].</returns>
    private static double GetDesorbTemperature
      (double desorborKA, double desorbHeat, double hwFlowRate, 
      LithiumBromide iSolution, LithiumBromide oSolution, double slFlowRate)
    {
      double cp = GetSolutionAverageSpecificHeat(iSolution, oSolution);
      double mcHW = hwFlowRate * 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat;
      double mcSL = slFlowRate * cp;
      double mcMin = Math.Min(mcHW, mcSL);
      double mcMax = Math.Max(mcHW, mcSL); 
      double effectiveness = HeatExchange.GetEffectiveness
        (desorborKA / mcMin, mcMin / mcMax, HeatExchange.FlowType.CounterFlow);
      return desorbHeat / (mcMin * effectiveness) + iSolution.LiquidTemperature;
    }

    /// <summary>Computes the hot water flow rate [kg/s] required to meet the specified conditions.</summary>
    /// <param name="desorborKA">Desorber overall heat transfer conductance [kW/K].</param>
    /// <param name="desorbHeat">Desorber heat input [kW].</param>
    /// <param name="hwITemperature">Desorption temperature [K].</param>
    /// <param name="iSolution">Inlet solution state.</param>
    /// <param name="oSolution">Outlet solution state.</param>
    /// <param name="slFlowRate">Solution mass flow rate [kg/s].</param>
    /// <returns>Required hot water flow rate [kg/s].</returns>
    private static double GetHotWaterFlowRate
      (double desorborKA, double desorbHeat, double hwITemperature,
      LithiumBromide iSolution, LithiumBromide oSolution, double slFlowRate)
    {
      Roots.ErrorFunction eFnc = delegate (double hwf)
      {
        return GetDesorbTemperature
        (desorborKA, desorbHeat, hwf, iSolution, oSolution, slFlowRate) - hwITemperature;
      };
      return Roots.Newton(eFnc, 0.0001, 0.001, 0.001, 0.0001, 20);
    }

    /// <summary>Computes the specific heat [kJ/(kg·K)] of the solution mixture relative to solution 1.</summary>
    /// <param name="sol1">Solution 1 state.</param>
    /// <param name="sol2">Solution 2 state (partially evaporated).</param>
    /// <returns>Specific heat of the solution mixture relative to solution 1 [kJ/(kg·K)].</returns>
    private static double GetSolutionAverageSpecificHeat(LithiumBromide sol1, LithiumBromide sol2)
    {
      double hw = Water.GetSaturatedVaporEnthalpy(PhysicsConstants.ToCelsius(sol1.VaporTemperature));
      double slRate = sol1.MassFraction / sol2.MassFraction;
      double hco = sol2.Enthalpy * slRate + hw * (1 - slRate);
      return (hco - sol1.Enthalpy) / (sol2.LiquidTemperature - sol1.LiquidTemperature);
    }

    #endregion

  }
}
