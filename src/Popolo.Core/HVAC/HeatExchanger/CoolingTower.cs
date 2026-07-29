/* CoolingTower.cs
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

using Popolo.Core.Exceptions;
using Popolo.Core.Physics;
using Popolo.Core.Numerics;

namespace Popolo.Core.HVAC.HeatExchanger
{
  /// <summary>Cooling tower model (counter-flow or cross-flow evaporative type).</summary>
  public class CoolingTower : IReadOnlyCoolingTower
  {

    #region Constant declarations

    /// <summary>Cooling tower characteristic coefficient c [-].</summary>
    private const double EXP_N = -0.6;

    #endregion

    #region Enumeration definitions

    /// <summary>Air flow direction type.</summary>
    public enum AirFlowDirection
    {
      /// <summary>Counter-flow arrangement.</summary>
      CounterFlow,
      /// <summary>Cross-flow arrangement.</summary>
      CrossFlow
    }

    #endregion

    #region Instance variables and properties

    /// <summary>Cooling tower characteristic coefficient c [-].</summary>
    private double coefC;

    /// <summary>Drift water rate (fraction of circulating water lost as drift) [-].</summary>
    private double driftWaterRate = 0.002;

    /// <summary>Concentration ratio of the circulating water [-].</summary>
    private double concentrationRatio = 4;

    /// <summary>Gets or sets the circulating water flow rate [kg/s].</summary>
    public double WaterFlowRate { get; set; }

    /// <summary>Gets the maximum allowable water flow rate [kg/s].</summary>
    public double MaxWaterFlowRate { get; private set; }

    /// <summary>Gets the current air flow rate [kg/s].</summary>
    public double AirFlowRate { get; private set; }

    /// <summary>Gets the maximum air flow rate [kg/s].</summary>
    public double MaxAirFlowRate { get; private set; }

    /// <summary>Gets the outdoor wet-bulb temperature [°C].</summary>
    public double OutdoorWetBulbTemperature { get; private set; } = 27;

    /// <summary>Gets the outdoor humidity ratio [kg/kg].</summary>
    public double OutdoorHumidityRatio { get; private set; } = 0.0195;

    /// <summary>Gets the inlet water temperature [°C].</summary>
    public double InletWaterTemperature { get; private set; }

    /// <summary>Gets the outlet water temperature [°C].</summary>
    public double OutletWaterTemperature { get; private set; }

    /// <summary>Gets or sets the outlet water temperature setpoint [°C].</summary>
    public double OutletWaterSetpointTemperature { get; set; }

    /// <summary>Gets the heat rejection rate [kW].</summary>
    public double HeatRejection { get; private set; }

    /// <summary>Gets or sets the concentration ratio [-].</summary>
    public double ConcentrationRatio
    {
      get { return concentrationRatio; }
      set { concentrationRatio = Math.Max(0, value); }
    }

    /// <summary>Gets or sets the drift water rate [-].</summary>
    public double DriftWaterRate
    {
      get { return driftWaterRate; }
      set { driftWaterRate = Math.Min(1, Math.Max(0, value)); }
    }

    /// <summary>True if the fan has an inverter drive.</summary>
    public bool HasInverter { get; private set; }

    /// <summary>Gets the air flow direction type.</summary>
    public AirFlowDirection AirFlowType { get; private set; }

    /// <summary>Gets the nominal (rated) fan power consumption [kW].</summary>
    public double NominalPowerConsumption { get; private set; }

    /// <summary>Gets the fan power consumption [kW].</summary>
    public double ElectricConsumption { get; private set; }

    /// <summary>Gets the water consumption rate due to evaporation [kg/s].</summary>
    public double EvaporationWater { get; private set; }

    /// <summary>Gets the water consumption rate due to drift [kg/s].</summary>
    public double DriftWater { get; private set; }

    /// <summary>Gets the water consumption rate due to blowdown [kg/s].</summary>
    public double BlowDownWater { get; private set; }

    /// <summary>Gets the total water consumption rate (evaporation + drift + blowdown) [kg/s].</summary>
    public double WaterConsumption
    { get { return EvaporationWater + DriftWater + BlowDownWater; } }

    /// <summary>Gets a value indicating whether the cooling tower is overloaded.</summary>
    public bool IsOverLoad { get; private set; }

    /// <summary>Gets or sets the minimum inverter rotation ratio [-].</summary>
    public double MinRotationRatio { get; set; } = 0.4;

    /// <summary>Gets the current inverter rotation ratio [-].</summary>
    public double RotationRatio
    { get { return Math.Max(MinRotationRatio, AirFlowRate / MaxAirFlowRate); } }

    #endregion

    #region Constructors

    /// <summary>Initializes a new instance.</summary>
    /// <param name="inletWaterTemperature">Inlet water temperature [°C].</param>
    /// <param name="outletWaterTemperature">Outlet water temperature [°C].</param>
    /// <param name="wetBulbTemperature">Outdoor wet-bulb temperature [°C].</param>
    /// <param name="waterFlowRate">Cooling water mass flow rate [kg/s].</param>
    /// <param name="airFlowRate">Air mass flow rate [kg/s].</param>
    /// <param name="airFlowType">Air flow direction type.</param>
    /// <param name="powerConsumption">Fan power consumption [kW].</param>
    /// <param name="hasInverter">True if the fan has an inverter drive.</param>
    public CoolingTower
      (double inletWaterTemperature, double outletWaterTemperature, double wetBulbTemperature, double waterFlowRate,
      double airFlowRate, AirFlowDirection airFlowType, double powerConsumption, bool hasInverter)
    {
      //Store nominal conditions
      OutletWaterSetpointTemperature = outletWaterTemperature;
      MaxAirFlowRate = AirFlowRate = airFlowRate;
      MaxWaterFlowRate = WaterFlowRate = waterFlowRate;
      HasInverter = hasInverter;
      AirFlowType = airFlowType;
      NominalPowerConsumption = powerConsumption;

      //Initialize the characteristic coefficient c [-] from nominal conditions
      coefC = GetCoolingTowerCoefficient
        (inletWaterTemperature, outletWaterTemperature, wetBulbTemperature, waterFlowRate, airFlowRate, airFlowType);

      //Initialize outlet state
      Update(inletWaterTemperature, false);
    }

    /// <summary>Initializes a new instance.</summary>
    /// <param name="inletWaterTemperature">Inlet water temperature [°C].</param>
    /// <param name="outletWaterTemperature">Outlet water temperature [°C].</param>
    /// <param name="wetBulbTemperature">Outdoor wet-bulb temperature [°C].</param>
    /// <param name="waterFlowRate">Cooling water mass flow rate [kg/s].</param>
    /// <param name="airFlowType">Air flow direction type.</param>
    /// <param name="hasInverter">True if the fan has an inverter drive.</param>
    public CoolingTower
      (double inletWaterTemperature, double outletWaterTemperature, double wetBulbTemperature,
      double waterFlowRate, AirFlowDirection airFlowType, bool hasInverter)
      : this(inletWaterTemperature, outletWaterTemperature, wetBulbTemperature,
          waterFlowRate, waterFlowRate * 0.8, airFlowType,
         GetFanPower((inletWaterTemperature - outletWaterTemperature) * waterFlowRate * 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat),
         hasInverter)
    { }

    /// <summary>Estimates the nominal fan power [kW] from the nominal cooling capacity [kW].</summary>
    /// <param name="load">Rated cooling capacity [kW].</param>
    /// <returns>Estimated nominal fan power [kW].</returns>
    private static double GetFanPower(double load)
    {
      if (load < 30) return 0.2;
      else if (load < 35) return 0.4;
      else if (load < 100) return 0.75;
      else if (load < 210) return 1.5;
      else if (load < 350) return 2.2;
      else if (load < 680) return 3.7;
      else if (load < 1020) return 5.5;
      else if (load < 1130) return 7.5;
      else throw new PopoloOutOfRangeException(
        nameof(load), load, 0.0, 1130.0);
    }

    #endregion

    #region Instance methods

    /// <summary>Sets the outdoor air conditions.</summary>
    /// <param name="wetBulbTemperature">Wet-bulb temperature [°C].</param>
    /// <param name="humidityRatio">Air humidity ratio [kg/kg].</param>
    public void SetOutdoorAirState(double wetBulbTemperature, double humidityRatio)
    {
      OutdoorWetBulbTemperature = wetBulbTemperature;
      OutdoorHumidityRatio = humidityRatio;
    }

    /// <summary>Updates the heat exchange calculation.</summary>
    /// <param name="inletWaterTemperature">Inlet water temperature [°C].</param>
    /// <param name="airFlowRate">Air mass flow rate [kg/s].</param>
    private void UpdateHeatExchange(double inletWaterTemperature, double airFlowRate)
    {
      //Set input conditions
      InletWaterTemperature = inletWaterTemperature;
      AirFlowRate = airFlowRate;

      //Compute cooling water outlet temperature from heat rejection//2017.03.01 DEBUG
      if (AirFlowRate <= 0 || WaterFlowRate <= 0)
      {
        HeatRejection = 0;
        OutletWaterTemperature = InletWaterTemperature;
      }
      else
      {
        HeatRejection = GetHeatRejection
          (InletWaterTemperature, OutdoorWetBulbTemperature, WaterFlowRate, AirFlowRate, coefC, AirFlowType);
        OutletWaterTemperature = InletWaterTemperature - HeatRejection / (0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat * WaterFlowRate);
      }
    }

    /// <summary>Updates fan power consumption and water consumption.</summary>
    private void UpdateElectricyAndWater()
    {
      if (HasInverter) ElectricConsumption = GetPowerConsumptionWithInverter(AirFlowRate, MaxAirFlowRate, NominalPowerConsumption, MinRotationRatio);
      else ElectricConsumption = GetPowerConsumptionWithOutInverter(AirFlowRate, MaxAirFlowRate, NominalPowerConsumption);
      double ew, dw, bw;
      double hOA = MoistAir.GetEnthalpyFromHumidityRatioAndWetBulbTemperature
        (OutdoorHumidityRatio, OutdoorWetBulbTemperature, PhysicsConstants.StandardAtmosphericPressure);
      GetMakeupWater(HeatRejection, hOA, OutdoorHumidityRatio, WaterFlowRate,
        AirFlowRate, MaxAirFlowRate, DriftWaterRate, ConcentrationRatio, out ew, out dw, out bw);
      EvaporationWater = ew;
      DriftWater = dw;
      BlowDownWater = bw;
    }

    /// <summary>Updates the cooling tower state to meet the outlet water temperature setpoint.</summary>
    /// <param name="inletWaterTemperature">Inlet water temperature [°C].</param>
    /// <param name="airFlowRate">Air mass flow rate [kg/s].</param>
    public void Update(double inletWaterTemperature, double airFlowRate)
    {
      IsOverLoad = false;
      UpdateHeatExchange(inletWaterTemperature, airFlowRate);
      UpdateElectricyAndWater();
    }

    /// <summary>Updates the cooling tower state to meet the outlet water temperature setpoint.</summary>
    /// <param name="inletWaterTemperature">Inlet water temperature [°C].</param>
    /// <param name="controlOutletWaterTemperature">True to control outlet water temperature to the setpoint.</param>
    public void Update(double inletWaterTemperature, bool controlOutletWaterTemperature)
    {
      //Set input conditions
      InletWaterTemperature = inletWaterTemperature;

      //When controlling the outlet temperature, adjust the air flow rate
      if (controlOutletWaterTemperature)
      {
        if (InletWaterTemperature <= OutletWaterSetpointTemperature) ShutOff();
        else
        {
          bool oload;
          double af = GetAirFlowRate
            (inletWaterTemperature, OutletWaterSetpointTemperature, OutdoorWetBulbTemperature,
            WaterFlowRate, MaxAirFlowRate, coefC, AirFlowType, out oload);
          IsOverLoad = oload;
          UpdateHeatExchange(inletWaterTemperature, af);
        }
      }
      else Update(inletWaterTemperature, MaxAirFlowRate);

      UpdateElectricyAndWater();
    }

    /// <summary>Computes the cooling water inlet and outlet temperatures that satisfy the given heat rejection rate.</summary>
    /// <param name="heatRejection">[kW]</param>
    public void UpdateFromHeatRejection(double heatRejection)
    {
      const double MIN_TEMP = 10;
      const double MAX_TEMP = 50;

      IsOverLoad = false;

      //Return if the heat exchange is excessive at the minimum water temperature
      UpdateHeatExchange(MIN_TEMP, AirFlowRate);
      if (heatRejection < HeatRejection) return;

      //Return if the heat exchange is insufficient at the maximum water temperature
      UpdateHeatExchange(MAX_TEMP, AirFlowRate);
      if (HeatRejection < heatRejection)
      {
        IsOverLoad = true;
        return;
      }

      Roots.ErrorFunction eFnc = delegate (double tIn)
      {
        UpdateHeatExchange(tIn, AirFlowRate);
        return Math.Abs(heatRejection - HeatRejection);
      };
      Roots.NewtonBisection(eFnc, 37, 1e-4, 1e-3, 1e-3, 20);
    }

    /// <summary>Shuts off the cooling tower (zero heat rejection, zero power).</summary>
    public void ShutOff()
    {
      OutletWaterTemperature = InletWaterTemperature;
      HeatRejection = ElectricConsumption = 0;
      EvaporationWater = DriftWater = BlowDownWater = 0;
    }

    #endregion

    #region Static methods

    /// <summary>Computes the cooling tower characteristic coefficient c [-] from operating conditions.</summary>
    /// <param name="inletWaterTemperature">Cooling water inlet temperature [°C].</param>
    /// <param name="outletWaterTemperature">Cooling water outlet temperature [°C].</param>
    /// <param name="wetBulbTemperature">Inlet air wet-bulb temperature [°C].</param>
    /// <param name="waterFlowRate">Cooling water mass flow rate [kg/s].</param>
    /// <param name="airFlowRate">Air mass flow rate [kg/s].</param>
    /// <param name="airFlowType">Air flow direction type.</param>
    /// <returns>Cooling tower characteristic coefficient c [-].</returns>
    public static double GetCoolingTowerCoefficient
      (double inletWaterTemperature, double outletWaterTemperature, double wetBulbTemperature,
      double waterFlowRate, double airFlowRate, AirFlowDirection airFlowType)
    {
      //Compute heat exchange rate [kW]
      double capacity = (inletWaterTemperature - outletWaterTemperature) * waterFlowRate * 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat;

      //Compute saturated air enthalpy at the cooling water temperature
      double hswi = MoistAir.GetEnthalpyFromDryBulbTemperatureAndRelativeHumidity
        (inletWaterTemperature, 100, PhysicsConstants.StandardAtmosphericPressure);
      double hswo = MoistAir.GetEnthalpyFromDryBulbTemperatureAndRelativeHumidity
        (outletWaterTemperature, 100, PhysicsConstants.StandardAtmosphericPressure);
      //Compute saturated enthalpy of the inlet air
      double hai = MoistAir.GetEnthalpyFromWetBulbTemperatureAndRelativeHumidity
        (wetBulbTemperature, 100, PhysicsConstants.StandardAtmosphericPressure);

      //Compute heat capacity rate ratio using the average specific heat
      double cs = (hswi - hswo) / (inletWaterTemperature - outletWaterTemperature);
      double mwc = waterFlowRate * 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat / cs;
      double mMin = Math.Min(mwc, airFlowRate);
      double mMax = Math.Max(mwc, airFlowRate);
      double rmc = mMin / mMax;

      //Compute heat transfer effectiveness and NTU
      double epsilon = capacity / mMin / (hswi - hai);
      double ntuMin;
      HeatExchange.FlowType aft;
      if (airFlowType == AirFlowDirection.CrossFlow) aft = HeatExchange.FlowType.CrossFlow_BothFluidsUnmixed;
      else aft = HeatExchange.FlowType.CounterFlow;
      ntuMin = HeatExchange.GetNTU(epsilon, rmc, aft);

      //Compute NTUw
      double ntuW;
      if (mwc == mMin) ntuW = ntuMin;
      else ntuW = ntuMin * mwc / airFlowRate;

      //Return the cooling tower characteristic coefficient c [-]
      return ntuW / Math.Pow(waterFlowRate / airFlowRate, EXP_N);
    }

    /// <summary>Computes the heat rejection rate [kW] from the given conditions.</summary>
    /// <param name="inletWaterTemperature">Cooling water inlet temperature [°C].</param>
    /// <param name="wetBulbTemperature">Inlet air wet-bulb temperature [°C].</param>
    /// <param name="waterFlowRate">Cooling water mass flow rate [kg/s].</param>
    /// <param name="airFlowRate">Air mass flow rate [kg/s].</param>
    /// <param name="coolingTowerCoefficient">Cooling tower characteristic coefficient c [-].</param>
    /// <param name="airFlowType">Air flow direction type.</param>
    /// <returns>Heat rejection rate [kW].</returns>
    public static double GetHeatRejection
      (double inletWaterTemperature, double wetBulbTemperature, double waterFlowRate, double airFlowRate,
      double coolingTowerCoefficient, AirFlowDirection airFlowType)
    {
      //Compute saturated air enthalpy at the cooling water temperature
      double hswi = MoistAir.GetEnthalpyFromDryBulbTemperatureAndRelativeHumidity
        (inletWaterTemperature, 100, PhysicsConstants.StandardAtmosphericPressure);
      //Compute saturated enthalpy of the inlet air
      double hai = MoistAir.GetEnthalpyFromWetBulbTemperatureAndRelativeHumidity
        (wetBulbTemperature, 100, PhysicsConstants.StandardAtmosphericPressure);
      //Compute NTUw
      double ntuw = coolingTowerCoefficient * Math.Pow(waterFlowRate / airFlowRate, EXP_N);

      //Define error function
      Roots.ErrorFunction eFnc = delegate (double waterOutletTemperature)
      {
        //Compute average specific heat
        double hswo = MoistAir.GetEnthalpyFromDryBulbTemperatureAndRelativeHumidity
        (waterOutletTemperature, 100, PhysicsConstants.StandardAtmosphericPressure);
        double cs = (hswi - hswo) / (inletWaterTemperature - waterOutletTemperature);

        //Compute equivalent cooling water flow rate
        double mwc = waterFlowRate * 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat / cs;

        //Compute heat transfer effectiveness
        double ntu;
        if (airFlowRate < mwc) ntu = ntuw * (mwc / airFlowRate);
        else ntu = ntuw;
        HeatExchange.FlowType ft;
        if (airFlowType == AirFlowDirection.CounterFlow) ft = HeatExchange.FlowType.CounterFlow;
        else ft = HeatExchange.FlowType.CrossFlow_BothFluidsUnmixed;
        double epsilon = HeatExchange.GetEffectiveness
        (ntu, Math.Min(airFlowRate, mwc) / Math.Max(airFlowRate, mwc), ft);

        //Compute heat rejection and evaluate the error
        double hReject = epsilon * (hswi - hai) * Math.Min(airFlowRate, mwc);
        return hReject - (inletWaterTemperature - waterOutletTemperature) * waterFlowRate * 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat;
      };

      //Iterate to find the cooling water outlet temperature
      double wOutT = inletWaterTemperature - 1;
      wOutT = Roots.Newton(eFnc, wOutT, 0.0001, 0.01, 0.001, 10);

      return (inletWaterTemperature - wOutT) * waterFlowRate * 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat;
    }

    /// <summary>Computes the required air flow rate [kg/s] to achieve the target outlet water temperature.</summary>
    /// <param name="inletWaterTemperature">Cooling water inlet temperature [°C].</param>
    /// <param name="outletWaterTemperature">Cooling water outlet temperature [°C].</param>
    /// <param name="wetBulbTemperature">Inlet air wet-bulb temperature [°C].</param>
    /// <param name="waterFlowRate">Cooling water mass flow rate [kg/s].</param>
    /// <param name="maxAirFlowRate">Maximum air mass flow rate [kg/s].</param>
    /// <param name="coolingTowerCoefficient">Cooling tower characteristic coefficient c [-].</param>
    /// <param name="airFlow">Air flow direction type.</param>
    /// <param name="isOverLoad">True if the tower is operating at maximum capacity.</param>
    /// <returns>Heat rejection rate [kW].</returns>
    public static double GetAirFlowRate
      (double inletWaterTemperature, double outletWaterTemperature, double wetBulbTemperature,
      double waterFlowRate, double maxAirFlowRate, double coolingTowerCoefficient,
      AirFlowDirection airFlow, out bool isOverLoad)
    {
      //Compute saturated air enthalpy at the cooling water temperature
      double hswi = MoistAir.GetEnthalpyFromDryBulbTemperatureAndRelativeHumidity
        (inletWaterTemperature, 100, PhysicsConstants.StandardAtmosphericPressure);
      double hswo = MoistAir.GetEnthalpyFromDryBulbTemperatureAndRelativeHumidity
        (outletWaterTemperature, 100, PhysicsConstants.StandardAtmosphericPressure);
      //Saturated enthalpy of the inlet air
      double hai = MoistAir.GetEnthalpyFromWetBulbTemperatureAndRelativeHumidity
        (wetBulbTemperature, 100, PhysicsConstants.StandardAtmosphericPressure);

      //Set air flow to zero if the water would be heated instead
      if (hswi < hai)
      {
        isOverLoad = true;
        return 0;
      }

      //Compute effectiveness times mass flow rate: εm
      double emmi = waterFlowRate * 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat
        * (inletWaterTemperature - outletWaterTemperature) / (hswi - hai);

      //Compute equivalent cooling water flow rate using the average specific heat
      double cs = (hswi - hswo) / (inletWaterTemperature - outletWaterTemperature);
      double mwc = waterFlowRate * 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat / cs;

      //Define error function
      Roots.ErrorFunction eFnc = delegate (double afRate)
      {
        //Compute NTU
        double ntuw = coolingTowerCoefficient * Math.Pow(waterFlowRate / afRate, EXP_N);
        double ntu;
        if (afRate < mwc) ntu = ntuw * (mwc / afRate);
        else ntu = ntuw;

        //Compute heat transfer effectiveness
        double mmin = Math.Min(mwc, afRate);
        double mmax = Math.Max(mwc, afRate);
        HeatExchange.FlowType ft;
        if (airFlow == AirFlowDirection.CounterFlow) ft = HeatExchange.FlowType.CounterFlow;
        else ft = HeatExchange.FlowType.CrossFlow_BothFluidsUnmixed;
        double epsilon = HeatExchange.GetEffectiveness(ntu, mmin / mmax, ft);

        return emmi - epsilon * mmin;
      };

      //If the air flow is insufficient, return the maximum air flow rate
      isOverLoad = 0 < eFnc(maxAirFlowRate);
      if (isOverLoad) return maxAirFlowRate;

      //If the heat exchange is still excessive at 0.1% air flow ratio, use 0.1%
      isOverLoad = 0 < eFnc(0.001 * maxAirFlowRate);
      if (!isOverLoad) return 0.001 * maxAirFlowRate;

      //Iterate with the bisection method//error ratio below 0.1%
      isOverLoad = false;
      return Roots.Bisection(eFnc, 0.001 * maxAirFlowRate, maxAirFlowRate, 0.001, 0.001 * maxAirFlowRate, 20);
    }

    /// <summary>Computes the water consumption rates (evaporation, drift, and blowdown) [kg/s].</summary>
    /// <param name="heatRejection">Heat rejection rate [kW].</param>
    /// <param name="airEnthalpy">Inlet air enthalpy [kJ/kg].</param>
    /// <param name="airHumidityRatio">Inlet air humidity ratio [kg/kg].</param>
    /// <param name="waterFlowRate">Cooling water mass flow rate [kg/s].</param>
    /// <param name="airFlowRate">Air mass flow rate [kg/s].</param>
    /// <param name="nominalAirFlowRate">Nominal air mass flow rate [kg/s].</param>
    /// <param name="driftWaterRate">Drift water rate [-].</param>
    /// <param name="concentrationRatio">Concentration ratio [-].</param>
    /// <param name="evaporationWater">Output: evaporation water consumption rate [kg/s].</param>
    /// <param name="driftWater">Output: drift water consumption rate [kg/s].</param>
    /// <param name="blowDownWater">Output: blowdown water consumption rate [kg/s].</param>
    public static void GetMakeupWater
      (double heatRejection, double airEnthalpy, double airHumidityRatio, double waterFlowRate,
      double airFlowRate, double nominalAirFlowRate, double driftWaterRate, double concentrationRatio,
      out double evaporationWater, out double driftWater, out double blowDownWater)
    {
      //Compute evaporation water from the heat rejection
      double outletHRatio;
      if (heatRejection <= 0 || airFlowRate <= 0) outletHRatio = 0;
      else outletHRatio = MoistAir.GetHumidityRatioFromEnthalpyAndRelativeHumidity
        (airEnthalpy + heatRejection / airFlowRate, 100, PhysicsConstants.StandardAtmosphericPressure);
      evaporationWater = airFlowRate * (outletHRatio - airHumidityRatio);

      //Compute drift water from the circulating flow rate
      driftWater = waterFlowRate * driftWaterRate * (airFlowRate / nominalAirFlowRate);

      //Adjust blowdown water so that it does not fall below 0 kg/s
      blowDownWater = Math.Max(0, evaporationWater / (concentrationRatio - 1) - driftWater);
    }

    /// <summary>Computes the fan power consumption [kW] without inverter control (on/off).</summary>
    /// <param name="airFlowRate">Air mass flow rate [kg/s].</param>
    /// <param name="nominalAirFlowRate">Nominal air mass flow rate [kg/s].</param>
    /// <param name="nominalPowerConsumption">Nominal fan power consumption [kW].</param>
    /// <returns>Fan power consumption [kW].</returns>
    public static double GetPowerConsumptionWithOutInverter
      (double airFlowRate, double nominalAirFlowRate, double nominalPowerConsumption)
    { return nominalPowerConsumption * airFlowRate / nominalAirFlowRate; }

    /// <summary>Computes the fan power consumption [kW] with inverter control.</summary>
    /// <param name="airFlowRate">Air mass flow rate [kg/s].</param>
    /// <param name="nominalAirFlowRate">Nominal air mass flow rate [kg/s].</param>
    /// <param name="nominalPowerConsumption">Nominal fan power consumption [kW].</param>
    /// <param name="minFrequency">Minimum inverter rotation ratio [-].</param>
    /// <returns>Fan power consumption [kW].</returns>
    public static double GetPowerConsumptionWithInverter
      (double airFlowRate, double nominalAirFlowRate, double nominalPowerConsumption, double minFrequency)
    {
      //Within the rotation speed control range
      if (minFrequency <= airFlowRate / nominalAirFlowRate)
        return nominalPowerConsumption * Math.Pow(airFlowRate / nominalAirFlowRate, 3);
      else
      {
        //Air flow and power consumption at the inverter lower limit
        double el = nominalPowerConsumption * Math.Pow(minFrequency, 3);
        double af = nominalAirFlowRate * minFrequency;

        return el * airFlowRate / af;
      }
    }

    #endregion

  }
}