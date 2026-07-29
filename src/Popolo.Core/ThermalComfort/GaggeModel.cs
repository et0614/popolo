/* GaggeModel.cs
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

namespace Popolo.Core.ThermalComfort
{
  /// <summary>Implements the Gagge two-node thermoregulatory model for computing skin temperature,
  /// core temperature, SET*, and related heat loss components.</summary>
  /// <remarks>
  /// Based on: Gagge et al. (1986). A standard predictive index of human response to the thermal environment.
  /// ASHRAE Transactions, 92(2), 709–731.
  /// </remarks>
  public class GaggeModel : IReadOnlyGaggeModel
  {

    #region Constant declarations

    /// <summary>Clothing area factor coefficient [-].</summary>
    private const double K_CLO = 0.25;

    /// <summary>Clothing moisture permeability index [K/kPa].</summary>
    private const double I_CLS = 0.45;

    /// <summary>Conversion factor from metabolic rate [met] to heat flux [W/m²].</summary>
    private const double CONVERT_MET_TO_W = 58.2;

    #endregion

    #region Instance variables

    /// <summary>Gets the age of the occupant [years].</summary>
    public uint Age { private set; get; }

    /// <summary>Gets the height [m].</summary>
    public double Height { private set; get; }

    /// <summary>Gets the weight [kg].</summary>
    public double Weight { private set; get; }

    /// <summary>Gets the basal metabolic rate [W/m²].</summary>
    public double BasalMetabolism { private set; get; }

    /// <summary>Gets the skin temperature [°C].</summary>
    public double SkinTemperature { private set; get; } = 33;

    /// <summary>Gets the core (rectal) temperature [°C].</summary>
    public double CoreTemperature { private set; get; } = 35;

    /// <summary>Gets the mean body temperature [°C].</summary>
    public double BodyTemperature { private set; get; } = 34;

    /// <summary>Gets the clothing surface temperature [°C].</summary>
    public double ClothTemperature { private set; get; } = 25;

    /// <summary>Gets the sensible heat loss from skin [W/m²].</summary>
    public double SensibleHeatLossFromSkin { private set; get; }

    /// <summary>Gets the latent heat loss from skin [W/m²].</summary>
    public double LatentHeatLossFromSkin { private set; get; }

    /// <summary>Gets the sensible heat loss by respiration [W/m²].</summary>
    public double SensibleHeatLossByRespiration { private set; get; }

    /// <summary>Gets the latent heat loss by respiration [W/m²].</summary>
    public double LatentHeatLossByRespiration { private set; get; }

    /// <summary>Gets the mean skin wettedness [-].</summary>
    public double Wettedness { private set; get; }

    /// <summary>Gets the body surface area (Du Bois) [m²].</summary>
    public double BodySurface { private set; get; }

    /// <summary>Gets the normal skin blood flow rate [mL/(m²·s)].</summary>
    public double NormalBloodFlow { private set; get; }

    #endregion

    #region Constructors and instance methods

    /// <summary>Initializes a new instance of the Gagge two-node model.</summary>
    /// <param name="age">Age [years].</param>
    /// <param name="isMale">True for male; false for female.</param>
    /// <param name="height">Height [m].</param>
    /// <param name="weight">Weight [kg].</param>
    public GaggeModel(uint age, bool isMale, double height, double weight)
    {
      this.Age = age;
      this.Height = height;
      this.Weight = weight;
      this.BodySurface = 0.202 * Math.Pow(weight, 0.425) * Math.Pow(height, 0.725);

      double rCI = 1.66 + age * (-3.48e-2 + age * (2.42e-4 + age * (5.16e-6 - age * 6.22e-8)));
      this.NormalBloodFlow = 1.75 * rCI * BodySurface / 1.8;
      this.BasalMetabolism = (0.1238 + 2.34 * Height + 0.0481 * Weight - 0.0138 * Age - 0.5473 * (isMale ? 1 : 2)) / (0.0864 * BodySurface);
    }

    /// <summary>Updates the thermoregulatory state for one time step.</summary>
    /// <param name="timeStep">Time step [s].</param>
    /// <param name="dryBulbTemperature">Dry-bulb temperature [°C].</param>
    /// <param name="meanRadiantTemperature">Mean radiant temperature [°C].</param>
    /// <param name="relativeHumidity">Relative humidity [%].</param>
    /// <param name="velocity">Relative air velocity [m/s].</param>
    /// <param name="clothing">Clothing insulation [clo].</param>
    /// <param name="metabolicRate">Metabolic rate [met].</param>
    /// <param name="externalWork">External mechanical work [W/m²].</param>
    /// <param name="atmosphericPressure">Atmospheric pressure [kPa].</param>
    public void UpdateState
      (double timeStep, double dryBulbTemperature, double meanRadiantTemperature, double relativeHumidity, 
      double velocity, double clothing, double metabolicRate, double externalWork, double atmosphericPressure)
    {
      //Constant declarations
      const double C_SWEATING = 47.2;         //Sweating coefficient [mg/(m2 s K)]
      const double C_VASODILATATION = 55.6;   //Vasodilation coefficient [L/(m2 s K)]
      const double C_VASOCONSTRICTION = 0.1;  //Vasoconstriction coefficient [1/K]
      const double SETPOINT_SKIN = 33.7;      //Skin setpoint temperature [C]
      const double SETPOINT_CORE = 36.8;      //Core setpoint temperature [C]
      const double SETPOINT_BODY = 36.49;     //Body temperature setpoint [C]
      const double CRITICAL_WETTEDNESS = 0.85;//Maximum skin wettedness [-]

      //Scale metabolic rate linearly, taking basal metabolism as 0.7 met
      double metab = BasalMetabolism * metabolicRate / 0.7;
      
      //Compute water vapor partial pressure [kPa]
      double pa = relativeHumidity / 100 * Water.GetSaturationPressure(dryBulbTemperature);
      //Compute clothing thermal resistance [m2K/W]
      double rcl = 0.155 * clothing;
      //Compute clothing area factor [-]
      double clothRate = 1d + K_CLO * clothing;

      //Compute convective heat transfer coefficient (governed by metabolic rate or air velocity)
      double chcv = 8.6 * Math.Pow(Math.Max(0.15, velocity), 0.53);
      double chcm = 5.66 * Math.Pow(Math.Max(0, metab / CONVERT_MET_TO_W - 0.85), 0.39);
      double convectiveHTransCoef = Math.Max(chcv, chcm);

      //Update with a time interval of at most 1 min
      while (true)
      {
        double tStep = Math.Min(timeStep, 60);
        if (timeStep < 60) tStep = timeStep;
        else tStep = 60;

        //Iteratively solve for clothing surface temperature
        double operatingTemp, ra;
        while (true)
        {
          double ctOld = ClothTemperature;
          //Compute radiative heat transfer coefficient [W/(m2K)]
          double hr = 4d * PhysicsConstants.StefanBoltzmannConstant * 0.72
            * Math.Pow(PhysicsConstants.ToKelvin((ClothTemperature + meanRadiantTemperature) / 2d), 3);
          //Compute combined heat transfer coefficient [W/(m2K)]
          double hcr = hr + convectiveHTransCoef;
          //Compute sensible heat resistance of air layer [(m2K)/W]
          ra = 1 / (clothRate * hcr);
          //Compute operative temperature [C]
          operatingTemp = (hr * meanRadiantTemperature + convectiveHTransCoef * dryBulbTemperature) / hcr;
          //Compute clothing temperature [C]
          ClothTemperature = (ra * SkinTemperature + rcl * operatingTemp) / (ra + rcl);
          //Converged when clothing temperature change is below 0.01 C
          if (Math.Abs(ctOld - ClothTemperature) < 0.01) break;
        }

        //Compute control signals/////////////////////////////////////////////////////////
        double sDil = Math.Max(0, CoreTemperature - SETPOINT_CORE);
        double sStr = Math.Max(0, SETPOINT_SKIN - SkinTemperature);
        double sSw1 = Math.Max(0, BodyTemperature - SETPOINT_BODY);
        double sSw2 = Math.Max(0, SkinTemperature - SETPOINT_SKIN);
        double sShv1 = Math.Max(0, SETPOINT_SKIN - SkinTemperature);
        double sShv2 = Math.Max(0, SETPOINT_CORE - CoreTemperature);

        //Compute skin blood flow rate [L/(m2 s)]
        double skinBloodFlow = (NormalBloodFlow + C_VASODILATATION * sDil) / (1d + C_VASOCONSTRICTION * sStr);
        skinBloodFlow = Math.Max(Math.Min(skinBloodFlow, 25), 0.139);
        //Update the skin/core mass ratio
        double alpha = 0.0417737 + 0.2069953 / (skinBloodFlow + 0.1626158);

        //Compute evaporative heat loss due to regulatory sweating [W/m2]
        double msw = C_SWEATING * sSw1 * Math.Exp(sSw2 / 10.7);
        double esw = 2.501 * msw;

        //Compute evaporative heat loss due to insensible perspiration [W/m2]
        double lewis = 0.0555 * PhysicsConstants.ToKelvin(SkinTemperature);
        double latentHTransCoef = 1 / (rcl / (I_CLS * lewis) + 1d / (clothRate * convectiveHTransCoef * lewis));
        double emax = latentHTransCoef * (Water.GetSaturationPressure(SkinTemperature) - pa);
        double wSw = esw / emax;
        double eb = 0.06 * (emax - esw);
        double wB = eb / emax;
        Wettedness = wSw + wB;
        LatentHeatLossFromSkin = esw + eb;

        //Correct evaporative heat loss etc. when skin wettedness exceeds the upper limit
        if (CRITICAL_WETTEDNESS < Wettedness)
        {
          Wettedness = CRITICAL_WETTEDNESS;
          wSw = (CRITICAL_WETTEDNESS - 0.06) / 0.94;
          esw = wSw * emax;
          eb = 0.06 * (1 - wSw) * emax;
          LatentHeatLossFromSkin = esw + eb;
        }
        //Case where skin surface is below the dew point
        if (emax < 0)
        {
          eb = 0;
          esw = 0;
          wB = CRITICAL_WETTEDNESS;
          wSw = CRITICAL_WETTEDNESS;
          LatentHeatLossFromSkin = emax;
        }

        //Compute shivering heat production [W/m2]
        double mshiv = 19.4 * sShv1 * sShv2;
        //Metabolic rate is the sum of basal metabolism and shivering heat production
        double metabolism = metab + mshiv;

        //End of control signal computation/////////////////////////////////////////////////////////

        //Compute sensible heat loss from skin [W/(m2K)]
        SensibleHeatLossFromSkin = (SkinTemperature - operatingTemp) / (ra + rcl);
        //Compute heat flow from core to skin [W/m2]
        double hfcs = (CoreTemperature - SkinTemperature) * (5.28 + 3.842 * skinBloodFlow);

        //Compute heat loss by respiration [W/m2]
        LatentHeatLossByRespiration = metabolism * 0.017251 * (5.8662 - pa);
        SensibleHeatLossByRespiration = metabolism * 0.0014 * (34 - dryBulbTemperature);

        //Compute heat flows into core and skin [W/m2]
        double scr = metabolism - hfcs - LatentHeatLossByRespiration - SensibleHeatLossByRespiration - externalWork;
        double ssk = hfcs - SensibleHeatLossFromSkin - LatentHeatLossFromSkin;
        //Compute heat capacities of core and skin [J/K]
        double tccr = 3492 * (1 - alpha) * Weight;
        double tcsk = 3492 * alpha * Weight;
        //Compute rates of temperature change of core and skin [K/s]
        double dtcr = (scr * BodySurface) / tccr;
        double dtsk = (ssk * BodySurface) / tcsk;
        //Update core and skin temperatures
        CoreTemperature = CoreTemperature + dtcr * tStep;
        SkinTemperature = SkinTemperature + dtsk * tStep;

        //Compute mean body temperature as weighted average
        BodyTemperature = alpha * SkinTemperature + (1 - alpha) * CoreTemperature;

        //Update clothing temperature
        ClothTemperature = (ra * SkinTemperature + rcl * operatingTemp) / (ra + rcl);

        if (timeStep < 60) return;
        else timeStep -= tStep;
      }
    }

    #endregion

    #region Static methods

    /// <summary>Computes the steady-state thermoregulatory conditions.</summary>
    /// <param name="dryBulbTemperature">Dry-bulb temperature [°C].</param>
    /// <param name="meanRadiantTemperature">Mean radiant temperature [°C].</param>
    /// <param name="relativeHumidity">Relative humidity [%].</param>
    /// <param name="velocity">Relative air velocity [m/s].</param>
    /// <param name="clothing">Clothing insulation [clo].</param>
    /// <param name="basalMetabolism">Basal metabolic rate [W/m²].</param>
    /// <param name="externalWork">External mechanical work [W/m²].</param>
    /// <param name="skinTemperature">Output: skin temperature [°C].</param>
    /// <param name="coreTemperature">Output: core temperature [°C].</param>
    /// <param name="bodyTemperature">Output: mean body temperature [°C].</param>
    /// <param name="clothTemperature">Output: clothing surface temperature [°C].</param>
    /// <param name="sensibleHFSkin">Output: sensible heat loss from skin [W/m²].</param>
    /// <param name="latentHFSkin">Output: latent heat loss from skin [W/m²].</param>
    /// <param name="sensibleRespiration">Output: sensible heat loss by respiration [W/m²].</param>
    /// <param name="latentRespiration">Output: latent heat loss by respiration [W/m²].</param>
    /// <param name="wettedness">Output: mean skin wettedness [-].</param>
    public static void GetSteadyState
      (double dryBulbTemperature, double meanRadiantTemperature, double relativeHumidity, double velocity, 
      double clothing, double basalMetabolism, double externalWork, 
      out double skinTemperature, out double coreTemperature, out double bodyTemperature, 
      out double clothTemperature, out double sensibleHFSkin, out double latentHFSkin, 
      out double sensibleRespiration, out double latentRespiration, out double wettedness)
    {
      //Constant declarations
      const double WEIGHT = 70d;              //Standard body weight [kg]
      const double BODY_SURFACE = 1.8;        //Standard body surface area [m2]
      const double C_SWEATING = 47.2;         //Sweating coefficient [mg/(m2 s K)]
      const double C_VASODILATATION = 55.6;   //Vasodilation coefficient [L/(m2 s K)]
      const double C_VASOCONSTRICTION = 0.1;  //Vasoconstriction coefficient [1/K]
      const double SETPOINT_SKIN = 33.7;      //Skin setpoint temperature [C]
      const double SETPOINT_CORE = 36.8;      //Core setpoint temperature [C]
      const double SETPOINT_BODY = 36.49;     //Body temperature setpoint [C]
      const double NORMAL_BLOOD_FLOW = 1.75;  //Skin blood flow rate at normal conditions [mL/(m2 s)]
      const double CRITICAL_WETTEDNESS = 0.85;//Maximum skin wettedness [-]

      //Compute water vapor partial pressure [kPa]
      double pa = relativeHumidity / 100 * Water.GetSaturationPressure(dryBulbTemperature);
      //Compute clothing thermal resistance [m2K/W]
      double rcl = 0.155 * clothing;
      //Compute clothing area factor [-]
      double clothRate = 1d + K_CLO * clothing;

      //Set initial values
      sensibleHFSkin = sensibleRespiration = latentRespiration = wettedness = 0;
      skinTemperature = SETPOINT_SKIN;
      coreTemperature = SETPOINT_CORE;
      bodyTemperature = SETPOINT_BODY;
      double skinBloodFlow = NORMAL_BLOOD_FLOW;
      double alpha = 0.1;
      latentHFSkin = 0.1 * basalMetabolism;
      double metabolism = basalMetabolism;   //Shivering heat production is assumed to be 0

      //Compute convective heat transfer coefficient (governed by metabolic rate or air velocity)
      double chcv = 8.6 * Math.Pow(Math.Max(0.15, velocity), 0.53);
      double chcm = 5.66 * Math.Pow(Math.Max(0, basalMetabolism / CONVERT_MET_TO_W - 0.85), 0.39);
      double convectiveHTransCoef = Math.Max(chcv, chcm);

      //Iterate for 60 min with Δt = 1 min
      const int DELTA_T = 1;
      clothTemperature = (skinTemperature + dryBulbTemperature) / 2d;
      for (int tim = 0; tim < 60; tim += DELTA_T)
      {
        //Iteratively solve for clothing surface temperature
        double operatingTemp, ra;
        while (true)
        {
          double ctOld = clothTemperature;
          //Compute radiative heat transfer coefficient [W/(m2K)]
          double hr = 4d * PhysicsConstants.StefanBoltzmannConstant * 0.72 
            * Math.Pow(PhysicsConstants.ToKelvin((clothTemperature + meanRadiantTemperature) / 2d), 3);
          //Compute combined heat transfer coefficient [W/(m2K)]
          double hcr = hr + convectiveHTransCoef;
          //Compute sensible heat resistance of air layer [(m2K)/W]
          ra = 1 / (clothRate * hcr);
          //Compute operative temperature [C]
          operatingTemp = (hr * meanRadiantTemperature + convectiveHTransCoef * dryBulbTemperature) / hcr;
          //Compute clothing temperature [C]
          clothTemperature = (ra * skinTemperature + rcl * operatingTemp) / (ra + rcl);
          //Converged when clothing temperature change is below 0.01 C
          if (Math.Abs(ctOld - clothTemperature) < 0.01) break;
        }

        //Compute sensible heat loss from skin [W/(m2K)]
        sensibleHFSkin = (skinTemperature - operatingTemp) / (ra + rcl);
        //Compute heat flow from core to skin [W/m2]
        double hfcs = (coreTemperature - skinTemperature) * (5.28 + 3.842 * skinBloodFlow);

        //Compute heat loss by respiration [W/m2]
        latentRespiration = metabolism * 0.017251 * (5.8662 - pa);
        sensibleRespiration = metabolism * 0.0014 * (34 - dryBulbTemperature);

        //Compute heat flows into core and skin [W/m2]
        double scr = metabolism - hfcs - latentRespiration - sensibleRespiration - externalWork;
        double ssk = hfcs - sensibleHFSkin - latentHFSkin;
        //Compute heat capacities of core and skin [J/K]
        double tccr = 3492 * (1 - alpha) * WEIGHT;
        double tcsk = 3492 * alpha * WEIGHT;
        //Compute rates of temperature change of core and skin [K/s]
        double dtcr = (scr * BODY_SURFACE) / tccr;
        double dtsk = (ssk * BODY_SURFACE) / tcsk;
        //Update core and skin temperatures
        coreTemperature = coreTemperature + dtcr * (DELTA_T * 60);
        skinTemperature = skinTemperature + dtsk * (DELTA_T * 60);

        //Compute mean body temperature as weighted average
        bodyTemperature = alpha * skinTemperature + (1 - alpha) * coreTemperature;

        //Compute control signals
        double sDil = Math.Max(0, coreTemperature - SETPOINT_CORE);
        double sStr = Math.Max(0, SETPOINT_SKIN - skinTemperature);
        double sSw1 = Math.Max(0, bodyTemperature - SETPOINT_BODY);
        double sSw2 = Math.Max(0, skinTemperature - SETPOINT_SKIN);
        double sShv1 = Math.Max(0, SETPOINT_SKIN - skinTemperature);
        double sShv2 = Math.Max(0, SETPOINT_CORE - coreTemperature);

        //Compute skin blood flow rate [L/(m2 s)]
        skinBloodFlow = (NORMAL_BLOOD_FLOW + C_VASODILATATION * sDil) / (1d + C_VASOCONSTRICTION * sStr);
        skinBloodFlow = Math.Max(Math.Min(skinBloodFlow, 25), 0.139);
        //Update the skin/core mass ratio
        alpha = 0.0417737 + 0.2069953 / (skinBloodFlow + 0.1626158);

        //Compute evaporative heat loss due to regulatory sweating [W/m2]
        double msw = C_SWEATING * sSw1 * Math.Exp(sSw2 / 10.7);
        double esw = 2.501 * msw;

        //Compute evaporative heat loss due to insensible perspiration [W/m2]
        double lewis = 0.0555 * (PhysicsConstants.ToKelvin(skinTemperature));
        double latentHTransCoef = 1 / (rcl / (I_CLS * lewis) + 1d / (clothRate * convectiveHTransCoef * lewis));
        double emax = latentHTransCoef * (Water.GetSaturationPressure(skinTemperature) - pa);
        double wSw = esw / emax;
        double eb = 0.06 * (emax - esw);
        double wB = eb / emax;
        wettedness = wSw + wB;
        latentHFSkin = esw + eb;

        //Correct evaporative heat loss etc. when skin wettedness exceeds the upper limit
        if (CRITICAL_WETTEDNESS < wettedness)
        {
          wettedness = CRITICAL_WETTEDNESS;
          wSw = (CRITICAL_WETTEDNESS - 0.06) / 0.94;
          esw = wSw * emax;
          eb = 0.06 * (1 - wSw) * emax;
          latentHFSkin = esw + eb;
        }
        //Case where skin surface is below the dew point
        if (emax < 0) latentHFSkin = emax;

        //Compute shivering heat production [W/m2]
        double mshiv = 19.4 * sShv1 * sShv2;
        //Metabolic rate is the sum of basal metabolism and shivering heat production
        metabolism = basalMetabolism + mshiv;

        //Update clothing temperature
        clothTemperature = (ra * skinTemperature + rcl * operatingTemp) / (ra + rcl);
      }
    }

    /// <summary>Computes the steady-state thermoregulatory conditions with body dimension adjustment.</summary>
    /// <param name="age">Age [years].</param>
    /// <param name="height">Height [m].</param>
    /// <param name="weight">Weight [kg].</param>
    /// <param name="dryBulbTemperature">Dry-bulb temperature [°C].</param>
    /// <param name="meanRadiantTemperature">Mean radiant temperature [°C].</param>
    /// <param name="relativeHumidity">Relative humidity [%].</param>
    /// <param name="velocity">Relative air velocity [m/s].</param>
    /// <param name="clothing">Clothing insulation [clo].</param>
    /// <param name="basalMetabolism">Basal metabolic rate [W/m²].</param>
    /// <param name="externalWork">External mechanical work [W/m²].</param>
    /// <param name="skinTemperature">Output: skin temperature [°C].</param>
    /// <param name="coreTemperature">Output: core temperature [°C].</param>
    /// <param name="bodyTemperature">Output: mean body temperature [°C].</param>
    /// <param name="clothTemperature">Output: clothing surface temperature [°C].</param>
    /// <param name="sensibleHFSkin">Output: sensible heat loss from skin [W/m²].</param>
    /// <param name="latentHFSkin">Output: latent heat loss from skin [W/m²].</param>
    /// <param name="sensibleRespiration">Output: sensible heat loss by respiration [W/m²].</param>
    /// <param name="latentRespiration">Output: latent heat loss by respiration [W/m²].</param>
    /// <param name="wettedness">Output: mean skin wettedness [-].</param>
    public static void GetSteadyState
      (int age, double height, double weight, 
      double dryBulbTemperature, double meanRadiantTemperature, double relativeHumidity, double velocity,
      double clothing, double basalMetabolism, double externalWork,
      out double skinTemperature, out double coreTemperature, out double bodyTemperature,
      out double clothTemperature, out double sensibleHFSkin, out double latentHFSkin,
      out double sensibleRespiration, out double latentRespiration, out double wettedness)
    {
      //Constant declarations
      const double C_SWEATING = 47.2;         //Sweating coefficient [mg/(m2 s K)]
      const double C_VASODILATATION = 55.6;   //Vasodilation coefficient [L/(m2 s K)]
      const double C_VASOCONSTRICTION = 0.1;  //Vasoconstriction coefficient [1/K]
      const double SETPOINT_SKIN = 33.7;      //Skin setpoint temperature [C]
      const double SETPOINT_CORE = 36.8;      //Core setpoint temperature [C]
      const double SETPOINT_BODY = 36.49;     //Body temperature setpoint [C]
      const double CRITICAL_WETTEDNESS = 0.85;//Maximum skin wettedness [-]

      //Compute body surface area and blood flow reflecting body dimensions
      double bodySurface = 0.202 * Math.Pow(weight, 0.425) * Math.Pow(height, 0.725);
      double normalBloodFlow = 3.14 + age * (-6.55e-2 + age * (4.55e-4 + age * (9.71e-6 - 1.17e-7 * age)));

      //Compute water vapor partial pressure [kPa]
      double pa = relativeHumidity / 100 * Water.GetSaturationPressure(dryBulbTemperature);
      //Compute clothing thermal resistance [m2K/W]
      double rcl = 0.155 * clothing;
      //Compute clothing area factor [-]
      double clothRate = 1d + K_CLO * clothing;

      //Set initial values
      sensibleHFSkin = sensibleRespiration = latentRespiration = wettedness = 0;
      skinTemperature = SETPOINT_SKIN;
      coreTemperature = SETPOINT_CORE;
      bodyTemperature = SETPOINT_BODY;
      double skinBloodFlow = normalBloodFlow;
      double alpha = 0.1;
      latentHFSkin = 0.1 * basalMetabolism;
      double metabolism = basalMetabolism;   //Shivering heat production is assumed to be 0

      //Compute convective heat transfer coefficient (governed by metabolic rate or air velocity)
      double chcv = 8.6 * Math.Pow(Math.Max(0.15, velocity), 0.53);
      double chcm = 5.66 * Math.Pow(Math.Max(0, basalMetabolism / CONVERT_MET_TO_W - 0.85), 0.39);
      double convectiveHTransCoef = Math.Max(chcv, chcm);

      //Iterate for 60 min with Δt = 1 min
      const int DELTA_T = 1;
      clothTemperature = (skinTemperature + dryBulbTemperature) / 2d;
      for (int tim = 0; tim < 60; tim += DELTA_T)
      {
        //Iteratively solve for clothing surface temperature
        double operatingTemp, ra;
        while (true)
        {
          double ctOld = clothTemperature;
          //Compute radiative heat transfer coefficient [W/(m2K)]
          double hr = 4d * PhysicsConstants.StefanBoltzmannConstant * 0.72
            * Math.Pow(PhysicsConstants.ToKelvin((clothTemperature + meanRadiantTemperature) / 2d), 3);
          //Compute combined heat transfer coefficient [W/(m2K)]
          double hcr = hr + convectiveHTransCoef;
          //Compute sensible heat resistance of air layer [(m2K)/W]
          ra = 1 / (clothRate * hcr);
          //Compute operative temperature [C]
          operatingTemp = (hr * meanRadiantTemperature + convectiveHTransCoef * dryBulbTemperature) / hcr;
          //Compute clothing temperature [C]
          clothTemperature = (ra * skinTemperature + rcl * operatingTemp) / (ra + rcl);
          //Converged when clothing temperature change is below 0.01 C
          if (Math.Abs(ctOld - clothTemperature) < 0.01) break;
        }

        //Compute sensible heat loss from skin [W/(m2K)]
        sensibleHFSkin = (skinTemperature - operatingTemp) / (ra + rcl);
        //Compute heat flow from core to skin [W/m2]
        double hfcs = (coreTemperature - skinTemperature) * (5.28 + 3.842 * skinBloodFlow);

        //Compute heat loss by respiration [W/m2]
        latentRespiration = metabolism * 0.017251 * (5.8662 - pa);
        sensibleRespiration = metabolism * 0.0014 * (34 - dryBulbTemperature);

        //Compute heat flows into core and skin [W/m2]
        double scr = metabolism - hfcs - latentRespiration - sensibleRespiration - externalWork;
        double ssk = hfcs - sensibleHFSkin - latentHFSkin;
        //Compute heat capacities of core and skin [J/K]
        double tccr = 3492 * (1 - alpha) * weight;
        double tcsk = 3492 * alpha * weight;
        //Compute rates of temperature change of core and skin [K/s]
        double dtcr = (scr * bodySurface) / tccr;
        double dtsk = (ssk * bodySurface) / tcsk;
        //Update core and skin temperatures
        coreTemperature = coreTemperature + dtcr * (DELTA_T * 60);
        skinTemperature = skinTemperature + dtsk * (DELTA_T * 60);

        //Compute mean body temperature as weighted average
        bodyTemperature = alpha * skinTemperature + (1 - alpha) * coreTemperature;

        //Compute control signals
        double sDil = Math.Max(0, coreTemperature - SETPOINT_CORE);
        double sStr = Math.Max(0, SETPOINT_SKIN - skinTemperature);
        double sSw1 = Math.Max(0, bodyTemperature - SETPOINT_BODY);
        double sSw2 = Math.Max(0, skinTemperature - SETPOINT_SKIN);
        double sShv1 = Math.Max(0, SETPOINT_SKIN - skinTemperature);
        double sShv2 = Math.Max(0, SETPOINT_CORE - coreTemperature);

        //Compute skin blood flow rate [L/(m2 s)]
        skinBloodFlow = (normalBloodFlow + C_VASODILATATION * sDil) / (1d + C_VASOCONSTRICTION * sStr);
        skinBloodFlow = Math.Max(Math.Min(skinBloodFlow, 25), 0.139);
        //Update the skin/core mass ratio
        alpha = 0.0417737 + 0.2069953 / (skinBloodFlow + 0.1626158);

        //Compute evaporative heat loss due to regulatory sweating [W/m2]
        double msw = C_SWEATING * sSw1 * Math.Exp(sSw2 / 10.7);
        double esw = 2.501 * msw;

        //Compute evaporative heat loss due to insensible perspiration [W/m2]
        double lewis = 0.0555 * (PhysicsConstants.ToKelvin(skinTemperature));
        double latentHTransCoef = 1 / (rcl / (I_CLS * lewis) + 1d / (clothRate * convectiveHTransCoef * lewis));
        double emax = latentHTransCoef * (Water.GetSaturationPressure(skinTemperature) - pa);
        double wSw = esw / emax;
        double eb = 0.06 * (emax - esw);
        double wB = eb / emax;
        wettedness = wSw + wB;
        latentHFSkin = esw + eb;

        //Correct evaporative heat loss etc. when skin wettedness exceeds the upper limit
        if (CRITICAL_WETTEDNESS < wettedness)
        {
          wettedness = CRITICAL_WETTEDNESS;
          wSw = (CRITICAL_WETTEDNESS - 0.06) / 0.94;
          esw = wSw * emax;
          eb = 0.06 * (1 - wSw) * emax;
          latentHFSkin = esw + eb;
        }
        //Case where skin surface is below the dew point
        if (emax < 0) latentHFSkin = emax;

        //Compute shivering heat production [W/m2]
        double mshiv = 19.4 * sShv1 * sShv2;
        //Metabolic rate is the sum of basal metabolism and shivering heat production
        metabolism = basalMetabolism + mshiv;

        //Update clothing temperature
        clothTemperature = (ra * skinTemperature + rcl * operatingTemp) / (ra + rcl);
      }
    }

    /// <summary>Computes SET* [°C] directly from ambient conditions.</summary>
    /// <param name="dryBulbTemperature">Dry-bulb temperature [°C].</param>
    /// <param name="meanRadiantTemperature">Mean radiant temperature [°C].</param>
    /// <param name="velocity">Relative air velocity [m/s].</param>
    /// <param name="relativeHumidity">Relative humidity [%].</param>
    /// <param name="clothing">Clothing insulation [clo].</param>
    /// <param name="basalMetabolism">Basal metabolic rate [W/m²].</param>
    /// <param name="externalWork">External mechanical work [W/m²].</param>
    /// <returns>SET*[C]</returns>
    public static double GetSETStarFromAmbientCondition
      (double dryBulbTemperature, double meanRadiantTemperature, double relativeHumidity, double velocity, 
      double clothing, double basalMetabolism, double externalWork)
    {
      double st, ct, bt, clt, ss, ls, sr, lr, wd;
      GetSteadyState(dryBulbTemperature, meanRadiantTemperature, relativeHumidity,
        velocity, clothing, basalMetabolism, externalWork,
        out st, out ct, out bt, out clt, out ss, out ls, out sr, out lr, out wd);
      return GetSETStar(meanRadiantTemperature, basalMetabolism, externalWork, clt, st, ss, ls, wd);
    }

    /// <summary>Computes the Standard Effective Temperature (SET*) [°C].</summary>
    /// <param name="meanRadiantTemperature">Mean radiant temperature [°C].</param>
    /// <param name="basalMetabolism">Basal metabolic rate [W/m²].</param>
    /// <param name="externalWork">External mechanical work [W/m²].</param>
    /// <param name="clothTemperature">Clothing surface temperature [°C].</param>
    /// <param name="skinTemperature">Skin temperature [°C].</param>
    /// <param name="sensibleHFSkin">Sensible heat loss from skin [W/m²].</param>
    /// <param name="latentHFSkin">Latent heat loss from skin [W/m²].</param>
    /// <param name="wettedness">Mean skin wettedness [-].</param>
    /// <returns>SET*[C]</returns>
    public static double GetSETStar
      (double meanRadiantTemperature, double basalMetabolism, double externalWork, double clothTemperature,
      double skinTemperature, double sensibleHFSkin, double latentHFSkin, double wettedness)
    {
      //Compute radiative heat transfer coefficient [W/(m2K)]
      double radiativeHTransCoef = 4d * PhysicsConstants.StefanBoltzmannConstant * 0.72
        * Math.Pow(PhysicsConstants.ToKelvin((clothTemperature + meanRadiantTemperature) / 2d), 3);
      //Compute convective heat transfer coefficient [W/(m2 K)]
      double convectiveHTransCoef = 5.66 * Math.Pow(Math.Max(0, basalMetabolism / CONVERT_MET_TO_W - 0.85), 0.39);
      convectiveHTransCoef = Math.Max(convectiveHTransCoef, 3d);

      //Compute standard clothing insulation [clo]
      double sClothing = 1.3264 / ((basalMetabolism - externalWork) / CONVERT_MET_TO_W + 0.7383) - 0.0953;

      //Compute clothing thermal resistance [m2K/W]
      double rcl = 0.155 * sClothing;
      //Compute clothing area factor [-]
      double clothRate = 1d + K_CLO * sClothing;

      //Compute latent heat transfer coefficient [W/(m2 K)]
      double lewis = 0.0555 * (PhysicsConstants.ToKelvin(skinTemperature));
      double latentHTransCoef = 1 / (rcl / (I_CLS * lewis) + 1d / (clothRate * convectiveHTransCoef * lewis));

      //Compute sensible heat transfer coefficient [W/(m2K)]
      double hcr = radiativeHTransCoef + convectiveHTransCoef;
      double sensibleHTransCoef = 1 / (1 / (clothRate * hcr) + rcl);

      //Compute SET* iteratively
      double hfSkin = sensibleHFSkin + latentHFSkin;
      double psk = Water.GetSaturationPressure(skinTemperature);
      Roots.ErrorFunction eFnc = delegate (double setStar)
      {
        return hfSkin - sensibleHTransCoef * (skinTemperature - setStar)
        - wettedness * latentHTransCoef * (psk - Water.GetSaturationPressure(setStar) * 0.5);
      };
      return Roots.Newton(eFnc, 26, 1e-3, 1e-3, 1e-3, 20);
    }

    #endregion

  }

}
