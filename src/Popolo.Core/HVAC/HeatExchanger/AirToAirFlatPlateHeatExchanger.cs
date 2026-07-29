/* AirToAirFlatPlateHeatExchanger.cs
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

namespace Popolo.Core.HVAC.HeatExchanger
{

  /// <summary>Air-to-air fixed-plate heat exchanger (sensible and total heat recovery).</summary>
  public class AirToAirFlatPlateHeatExchanger : IReadOnlyAirToAirFlatPlateHeatExchanger
  {

    #region Enumerations

    /// <summary>Air flow arrangement.</summary>
    public enum AirFlow
    {
      /// <summary>Counter-flow arrangement.</summary>
      CounterFlow,
      /// <summary>Cross-flow arrangement.</summary>
      CrossFlow
    }

    /// <summary>JIS test condition for initializing heat transfer coefficients.</summary>
    public enum Condition
    {
      /// <summary>JIS B 8628:2003, heating condition.</summary>
      JISB8628_2003_Heating,
      /// <summary>JIS B 8628:2017, heating condition.</summary>
      JISB8628_2017_Heating,
      /// <summary>JIS B 8628:2003, cooling condition.</summary>
      JISB8628_2003_Cooling,
      /// <summary>JIS B 8628:2017, cooling condition.</summary>
      JISB8628_2017_Cooling,
    }

    #endregion

    #region Instance variables

    /// <summary>Sensible heat transfer coefficient [kW/K].</summary>
    private double sensibleHeatTransferCoefficient;

    /// <summary>Latent heat transfer coefficient [kg/(kg/kg)].</summary>
    private double latentHeatTransferCoefficient;

    /// <summary>
    /// Sensible heat transfer coefficient for cooling-side operation [kW/K].
    /// Null unless the exchanger was initialized from both the heating and
    /// the cooling test conditions.
    /// </summary>
    private double? coolingSensibleHeatTransferCoefficient = null;

    /// <summary>
    /// Latent heat transfer coefficient for cooling-side operation
    /// [kg/(kg/kg)]. Null unless the exchanger was initialized from both the
    /// heating and the cooling test conditions.
    /// </summary>
    private double? coolingLatentHeatTransferCoefficient = null;

    #endregion

    #region Properties

    /// <summary>Gets a value indicating whether this is a total heat exchanger (sensible + latent).</summary>
    public bool IsTotalHeatExchanger { get; private set; }

    /// <summary>Gets the supply air volumetric flow rate [m³/h].</summary>
    public double SupplyAirFlowVolume { get; private set; }

    /// <summary>Gets the exhaust air volumetric flow rate [m³/h].</summary>
    public double ExhaustAirFlowVolume { get; private set; }

    /// <summary>Gets the supply air inlet dry-bulb temperature [°C].</summary>
    public double SupplyAirInletDryBulbTemperature { get; private set; }

    /// <summary>Gets the exhaust air inlet dry-bulb temperature [°C].</summary>
    public double ExhaustAirInletDryBulbTemperature { get; private set; }

    /// <summary>Gets the supply air outlet dry-bulb temperature [°C].</summary>
    public double SupplyAirOutletDryBulbTemperature { get; private set; }

    /// <summary>Gets the exhaust air outlet dry-bulb temperature [°C].</summary>
    public double ExhaustAirOutletDryBulbTemperature { get; private set; }

    /// <summary>Gets the supply air inlet humidity ratio [kg/kg].</summary>
    public double SupplyAirInletHumidityRatio { get; private set; }

    /// <summary>Gets the exhaust air inlet humidity ratio [kg/kg].</summary>
    public double ExhaustAirInletHumidityRatio { get; private set; }

    /// <summary>Gets the supply air outlet humidity ratio [kg/kg].</summary>
    public double SupplyAirOutletHumidityRatio { get; private set; }

    /// <summary>Gets the exhaust air outlet humidity ratio [kg/kg].</summary>
    public double ExhaustAirOutletHumidityRatio { get; private set; }

    /// <summary>Gets the sensible heat exchange efficiency [-].</summary>
    public double SensibleEfficiency { get; private set; }

    /// <summary>Gets the latent heat exchange efficiency [-].</summary>
    public double LatentEfficiency { get; private set; }

    /// <summary>Gets the air flow arrangement type.</summary>
    public AirFlow Flow { get; private set; }

    #endregion

    #region Constructors

    /// <summary>Initializes a new instance.</summary>
    /// <param name="supplyAirFlowVolume">Supply air volumetric flow rate [m³/h].</param>
    /// <param name="exhaustAirFlowVolume">Exhaust air volumetric flow rate [m³/h].</param>
    /// <param name="inletSADryBulbTemperature">Supply air inlet dry-bulb temperature [°C].</param>
    /// <param name="inletSAHumidityRatio">Supply air inlet humidity ratio [kg/kg].</param>
    /// <param name="inletEADryBulbTemperature">Exhaust air inlet dry-bulb temperature [°C].</param>
    /// <param name="inletEAHumidityRatio">Exhaust air inlet humidity ratio [kg/kg].</param>
    /// <param name="sensibleEfficiency">Sensible heat exchange efficiency [-].</param>
    /// <param name="latentEfficiency">Latent heat exchange efficiency [-].</param>
    /// <param name="flow">Air flow arrangement type.</param>
    /// <param name="isTotalHeatExchanger">True for a total heat exchanger (sensible + latent); false for sensible only.</param>
    public AirToAirFlatPlateHeatExchanger
      (double supplyAirFlowVolume, double exhaustAirFlowVolume,
      double inletSADryBulbTemperature, double inletSAHumidityRatio,
      double inletEADryBulbTemperature, double inletEAHumidityRatio,
      double sensibleEfficiency, double latentEfficiency,
      AirFlow flow, bool isTotalHeatExchanger)
    {
      Initialize(
        supplyAirFlowVolume, exhaustAirFlowVolume, 
        inletSADryBulbTemperature, inletSAHumidityRatio,
        inletEADryBulbTemperature, inletEAHumidityRatio, 
        sensibleEfficiency, latentEfficiency,
        flow, isTotalHeatExchanger);
    }

    /// <summary>Initializes a new instance.</summary>
    /// <param name="supplyAirFlowVolume">Supply air volumetric flow rate [m³/h].</param>
    /// <param name="exhaustAirFlowVolume">Exhaust air volumetric flow rate [m³/h].</param>
    /// <param name="sensibleEfficiency">Sensible heat exchange efficiency [-].</param>
    /// <param name="latentEfficiency">Latent heat exchange efficiency [-].</param>
    /// <param name="flow">Air flow arrangement type.</param>
    /// <param name="condition">JIS test condition used to compute heat transfer coefficients.</param>
    public AirToAirFlatPlateHeatExchanger
      (double supplyAirFlowVolume, double exhaustAirFlowVolume, double sensibleEfficiency, double latentEfficiency, AirFlow flow, Condition condition):
      this(supplyAirFlowVolume, exhaustAirFlowVolume, sensibleEfficiency, latentEfficiency, flow, condition, false)
    { }

    /// <summary>Initializes a new instance.</summary>
    /// <param name="supplyAirFlowVolume">Supply air volumetric flow rate [m³/h].</param>
    /// <param name="exhaustAirFlowVolume">Exhaust air volumetric flow rate [m³/h].</param>
    /// <param name="sensibleEfficiency">Sensible heat exchange efficiency [-].</param>
    /// <param name="latentOrEnthalpyEfficiency">Latent or enthalpy-based heat exchange efficiency [-].</param>
    /// <param name="flow">Air flow arrangement type.</param>
    /// <param name="condition">JIS test condition used to compute heat transfer coefficients.</param>
    /// <param name="isEnthalpyEfficiency">True if the efficiency is defined as enthalpy-based rather than humidity-ratio-based.</param>
    public AirToAirFlatPlateHeatExchanger
      (double supplyAirFlowVolume, double exhaustAirFlowVolume, double sensibleEfficiency, double latentOrEnthalpyEfficiency, AirFlow flow, Condition condition, bool isEnthalpyEfficiency)
    {
      GetConditionState(condition, out double saDB, out double saHmd, out double eaDB, out double eaHmd);

      //If an enthalpy exchange efficiency is given, convert it to a latent heat exchange efficiency
      if (isEnthalpyEfficiency)
        latentOrEnthalpyEfficiency = ConvertEnthalpyToLatentEfficiency
          (sensibleEfficiency, latentOrEnthalpyEfficiency, saDB, saHmd, eaDB, eaHmd);

      Initialize(supplyAirFlowVolume, exhaustAirFlowVolume, saDB, saHmd, eaDB, eaHmd, sensibleEfficiency, latentOrEnthalpyEfficiency, flow, true);
    }

    /// <summary>
    /// Initializes a new instance of a total heat exchanger from both the
    /// heating and the cooling JIS test conditions.
    /// </summary>
    /// <remarks>
    /// Catalog efficiencies generally differ between the heating and the
    /// cooling test conditions, especially on the latent side. This
    /// constructor computes one pair of heat transfer coefficients per
    /// condition; <see cref="UpdateState"/> selects the pair according to the
    /// operating direction (the cooling pair when the supply air inlet is
    /// warmer than the exhaust air inlet, the heating pair otherwise).
    /// </remarks>
    /// <param name="supplyAirFlowVolume">Supply air volumetric flow rate [m³/h].</param>
    /// <param name="exhaustAirFlowVolume">Exhaust air volumetric flow rate [m³/h].</param>
    /// <param name="sensibleEfficiencyHeating">Sensible heat exchange efficiency at the heating test condition [-].</param>
    /// <param name="latentOrEnthalpyEfficiencyHeating">Latent or enthalpy-based heat exchange efficiency at the heating test condition [-].</param>
    /// <param name="heatingCondition">JIS heating test condition.</param>
    /// <param name="sensibleEfficiencyCooling">Sensible heat exchange efficiency at the cooling test condition [-].</param>
    /// <param name="latentOrEnthalpyEfficiencyCooling">Latent or enthalpy-based heat exchange efficiency at the cooling test condition [-].</param>
    /// <param name="coolingCondition">JIS cooling test condition.</param>
    /// <param name="flow">Air flow arrangement type.</param>
    /// <param name="isEnthalpyEfficiency">True if the efficiencies are defined as enthalpy-based rather than humidity-ratio-based.</param>
    public AirToAirFlatPlateHeatExchanger
      (double supplyAirFlowVolume, double exhaustAirFlowVolume,
      double sensibleEfficiencyHeating, double latentOrEnthalpyEfficiencyHeating, Condition heatingCondition,
      double sensibleEfficiencyCooling, double latentOrEnthalpyEfficiencyCooling, Condition coolingCondition,
      AirFlow flow, bool isEnthalpyEfficiency)
    {
      if (heatingCondition != Condition.JISB8628_2003_Heating
        && heatingCondition != Condition.JISB8628_2017_Heating)
        throw new PopoloArgumentException(
          $"heatingCondition must be a heating test condition. Got: {heatingCondition}.",
          nameof(heatingCondition));
      if (coolingCondition != Condition.JISB8628_2003_Cooling
        && coolingCondition != Condition.JISB8628_2017_Cooling)
        throw new PopoloArgumentException(
          $"coolingCondition must be a cooling test condition. Got: {coolingCondition}.",
          nameof(coolingCondition));

      //Compute and store the heat transfer coefficients for the cooling condition
      GetConditionState(coolingCondition, out double saDB, out double saHmd, out double eaDB, out double eaHmd);
      double latEffC = isEnthalpyEfficiency
        ? ConvertEnthalpyToLatentEfficiency
          (sensibleEfficiencyCooling, latentOrEnthalpyEfficiencyCooling, saDB, saHmd, eaDB, eaHmd)
        : latentOrEnthalpyEfficiencyCooling;
      double svSA = MoistAir.GetSpecificVolumeFromDryBulbTemperatureAndHumidityRatio
        (saDB, saHmd, PhysicsConstants.StandardAtmosphericPressure);
      double svEA = MoistAir.GetSpecificVolumeFromDryBulbTemperatureAndHumidityRatio
        (eaDB, eaHmd, PhysicsConstants.StandardAtmosphericPressure);
      double mSA = supplyAirFlowVolume / (3600 * svSA);
      double mEA = exhaustAirFlowVolume / (3600 * svEA);
      coolingSensibleHeatTransferCoefficient = GetSensibleHeatTransferCoefficient
        (mSA, mEA, saDB, saHmd, eaDB, eaHmd, sensibleEfficiencyCooling, flow);
      coolingLatentHeatTransferCoefficient = GetLatentHeatTransferCoefficient
        (mSA, mEA, saHmd, eaHmd, latEffC, flow);

      //Compute the heat transfer coefficients for the heating condition and initialize (the nominal free-running calculation also uses the heating condition)
      GetConditionState(heatingCondition, out saDB, out saHmd, out eaDB, out eaHmd);
      double latEffH = isEnthalpyEfficiency
        ? ConvertEnthalpyToLatentEfficiency
          (sensibleEfficiencyHeating, latentOrEnthalpyEfficiencyHeating, saDB, saHmd, eaDB, eaHmd)
        : latentOrEnthalpyEfficiencyHeating;
      Initialize(supplyAirFlowVolume, exhaustAirFlowVolume, saDB, saHmd, eaDB, eaHmd,
        sensibleEfficiencyHeating, latEffH, flow, true);
    }

    /// <summary>Returns the inlet air states of the specified JIS test condition.</summary>
    /// <param name="condition">JIS test condition.</param>
    /// <param name="saDB">Supply air inlet dry-bulb temperature [°C].</param>
    /// <param name="saHmd">Supply air inlet humidity ratio [kg/kg].</param>
    /// <param name="eaDB">Exhaust air inlet dry-bulb temperature [°C].</param>
    /// <param name="eaHmd">Exhaust air inlet humidity ratio [kg/kg].</param>
    private static void GetConditionState
      (Condition condition, out double saDB, out double saHmd, out double eaDB, out double eaHmd)
    {
      switch (condition)
      {
        case Condition.JISB8628_2003_Cooling:
          saDB = 34.5;
          eaDB = 26.5;
          saHmd = 0.02627;
          eaHmd = 0.01402;
          break;
        case Condition.JISB8628_2003_Heating:
          saDB = 5.0;
          eaDB = 20.5;
          saHmd = 0.00350;
          eaHmd = 0.00894;
          break;
        case Condition.JISB8628_2017_Cooling:
          saDB = 35.0;
          eaDB = 27.0;
          saHmd = 0.02715;
          eaHmd = 0.01178;
          break;
        case Condition.JISB8628_2017_Heating:
          saDB = 5.0;
          eaDB = 20.0;
          saHmd = 0.00387;
          eaHmd = 0.00857;
          break;
        default:
          throw new PopoloArgumentException(
            $"Unsupported test condition: {condition}.", nameof(condition));
      }
    }

    /// <summary>
    /// Converts an enthalpy-based exchange efficiency into a
    /// humidity-ratio-based (latent) exchange efficiency at the given
    /// rating condition.
    /// </summary>
    private static double ConvertEnthalpyToLatentEfficiency
      (double sensibleEfficiency, double enthalpyEfficiency,
      double saDB, double saHmd, double eaDB, double eaHmd)
    {
      double tsao = saDB - sensibleEfficiency * (saDB - eaDB);
      double saH = MoistAir.GetEnthalpyFromDryBulbTemperatureAndHumidityRatio(saDB, saHmd);
      double eaH = MoistAir.GetEnthalpyFromDryBulbTemperatureAndHumidityRatio(eaDB, eaHmd);
      double hsao = saH - enthalpyEfficiency * (saH - eaH);
      double hmd = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndEnthalpy(tsao, hsao);
      return (saHmd - hmd) / (saHmd - eaHmd);
    }

    /// <summary>Initializes a new instance.</summary>
    /// <param name="supplyAirFlowVolume">Supply air volumetric flow rate [m³/h].</param>
    /// <param name="exhaustAirFlowVolume">Exhaust air volumetric flow rate [m³/h].</param>
    /// <param name="inletSADryBulbTemperature">Supply air inlet dry-bulb temperature [°C].</param>
    /// <param name="inletSAHumidityRatio">Supply air inlet humidity ratio [kg/kg].</param>
    /// <param name="inletEADryBulbTemperature">Exhaust air inlet dry-bulb temperature [°C].</param>
    /// <param name="inletEAHumidityRatio">Exhaust air inlet humidity ratio [kg/kg].</param>
    /// <param name="sensibleEfficiency">Sensible heat exchange efficiency [-].</param>
    /// <param name="latentEfficiency">Latent heat exchange efficiency [-].</param>
    /// <param name="flow">Air flow arrangement type.</param>
    /// <param name="isTotalHeatExchanger">True for a total heat exchanger (sensible + latent); false for sensible only.</param>
    private void Initialize
      (double supplyAirFlowVolume, double exhaustAirFlowVolume,
      double inletSADryBulbTemperature, double inletSAHumidityRatio,
      double inletEADryBulbTemperature, double inletEAHumidityRatio,
      double sensibleEfficiency, double latentEfficiency,
      AirFlow flow, bool isTotalHeatExchanger)
    {
      //Store equipment information
      IsTotalHeatExchanger = isTotalHeatExchanger;
      Flow = flow;

      //Compute air mass flow rates
      double svSA = MoistAir.GetSpecificVolumeFromDryBulbTemperatureAndHumidityRatio
        (inletSADryBulbTemperature, inletSAHumidityRatio, PhysicsConstants.StandardAtmosphericPressure);
      double svEA = MoistAir.GetSpecificVolumeFromDryBulbTemperatureAndHumidityRatio
        (inletEADryBulbTemperature, inletEAHumidityRatio, PhysicsConstants.StandardAtmosphericPressure);
      double mSA = supplyAirFlowVolume / (3600 * svSA);
      double mEA = exhaustAirFlowVolume / (3600 * svEA);

      //Compute sensible heat transfer coefficient
      sensibleHeatTransferCoefficient = GetSensibleHeatTransferCoefficient
        (mSA, mEA, inletSADryBulbTemperature, inletSAHumidityRatio,
        inletEADryBulbTemperature, inletEAHumidityRatio, sensibleEfficiency, flow);

      //For a total heat exchanger, compute the latent heat transfer coefficient [kg/(kg/kg)]
      if (IsTotalHeatExchanger)
      {
        latentHeatTransferCoefficient = GetLatentHeatTransferCoefficient
          (mSA, mEA, inletSAHumidityRatio, inletEAHumidityRatio, latentEfficiency, flow);
      }

      //Run a free-running calculation at nominal conditions
      UpdateState(supplyAirFlowVolume, exhaustAirFlowVolume, inletSADryBulbTemperature,
        inletSAHumidityRatio, inletEADryBulbTemperature, inletEAHumidityRatio);
    }

    #endregion

    #region Instance methods

    /// <summary>Updates the outlet conditions from the given inlet conditions.</summary>
    /// <param name="supplyAirFlowVolume">Supply air volumetric flow rate [m³/h].</param>
    /// <param name="exhaustAirFlowVolume">Exhaust air volumetric flow rate [m³/h].</param>
    /// <param name="inletSADryBulbTemperature">Supply air inlet dry-bulb temperature [°C].</param>
    /// <param name="inletSAHumidityRatio">Supply air inlet humidity ratio [kg/kg].</param>
    /// <param name="inletEADryBulbTemperature">Exhaust air inlet dry-bulb temperature [°C].</param>
    /// <param name="inletEAHumidityRatio">Exhaust air inlet humidity ratio [kg/kg].</param>
    public void UpdateState
      (double supplyAirFlowVolume, double exhaustAirFlowVolume,
      double inletSADryBulbTemperature, double inletSAHumidityRatio,
      double inletEADryBulbTemperature, double inletEAHumidityRatio)
    {
      //Store air flow rates
      SupplyAirFlowVolume = supplyAirFlowVolume;
      ExhaustAirFlowVolume = exhaustAirFlowVolume;

      //Store inlet air states
      SupplyAirInletDryBulbTemperature = inletSADryBulbTemperature;
      SupplyAirInletHumidityRatio = inletSAHumidityRatio;
      ExhaustAirInletDryBulbTemperature = inletEADryBulbTemperature;
      ExhaustAirInletHumidityRatio = inletEAHumidityRatio;

      //Case of zero air flow
      if (supplyAirFlowVolume <= 0 || exhaustAirFlowVolume <= 0)
      {
        SensibleEfficiency = 0;
        SupplyAirOutletDryBulbTemperature = SupplyAirInletDryBulbTemperature;
        ExhaustAirOutletDryBulbTemperature = ExhaustAirInletDryBulbTemperature;
        SupplyAirOutletHumidityRatio = SupplyAirInletHumidityRatio;
        ExhaustAirOutletHumidityRatio = ExhaustAirInletHumidityRatio;
        return;
      }

      //Compute air mass flow rates
      double svSA = MoistAir.GetSpecificVolumeFromDryBulbTemperatureAndHumidityRatio
        (inletSADryBulbTemperature, inletSAHumidityRatio, PhysicsConstants.StandardAtmosphericPressure);
      double svEA = MoistAir.GetSpecificVolumeFromDryBulbTemperatureAndHumidityRatio
        (inletEADryBulbTemperature, inletEAHumidityRatio, PhysicsConstants.StandardAtmosphericPressure);
      double mSA = supplyAirFlowVolume / (3600 * svSA);
      double mEA = exhaustAirFlowVolume / (3600 * svEA);

      //Select the heat transfer coefficients to use (with two-condition initialization, switch between heating and cooling pairs by operating direction)
      double sensKA = sensibleHeatTransferCoefficient;
      double latKA = latentHeatTransferCoefficient;
      if (coolingSensibleHeatTransferCoefficient.HasValue
        && inletEADryBulbTemperature < inletSADryBulbTemperature)
      {
        sensKA = coolingSensibleHeatTransferCoefficient.Value;
        latKA = coolingLatentHeatTransferCoefficient ?? latKA;
      }

      //Compute heat transfer effectiveness [-]
      double effectiveness, mcMin, capacityRate;
      bool isMcMinSA;
      GetSensibleEffectiveness
        (mSA, mEA, inletSADryBulbTemperature, inletSAHumidityRatio,
        inletEADryBulbTemperature, inletEAHumidityRatio, sensKA, Flow,
        out effectiveness, out mcMin, out capacityRate, out isMcMinSA);

      //Compute heat exchange efficiency [-]
      double eff2;
      if (isMcMinSA)
      {
        SensibleEfficiency = effectiveness;
        eff2 = effectiveness * capacityRate;
      }
      else
      {
        SensibleEfficiency = effectiveness * capacityRate;
        eff2 = effectiveness;
      }

      //Compute outlet air states
      SupplyAirOutletDryBulbTemperature =
        SupplyAirInletDryBulbTemperature - SensibleEfficiency *
        (SupplyAirInletDryBulbTemperature - ExhaustAirInletDryBulbTemperature);
      ExhaustAirOutletDryBulbTemperature = ExhaustAirInletDryBulbTemperature -
        eff2 * (ExhaustAirInletDryBulbTemperature - SupplyAirInletDryBulbTemperature);

      //Moisture exchange
      if (IsTotalHeatExchanger)
      {
        //Compute heat transfer effectiveness [-]
        GetLatentEffectiveness
          (mSA, mEA, inletSAHumidityRatio, inletEAHumidityRatio,
          latKA, Flow,
          out effectiveness, out mcMin, out capacityRate);

        //Compute heat exchange efficiency [-]
        if (mcMin == mSA)
        {
          LatentEfficiency = effectiveness;
          eff2 = effectiveness * capacityRate;
        }
        else
        {
          LatentEfficiency = effectiveness * capacityRate;
          eff2 = effectiveness;
        }

        SupplyAirOutletHumidityRatio = SupplyAirInletHumidityRatio -
          LatentEfficiency * (SupplyAirInletHumidityRatio - ExhaustAirInletHumidityRatio);
        ExhaustAirOutletHumidityRatio = ExhaustAirInletHumidityRatio -
          eff2 * (ExhaustAirInletHumidityRatio - SupplyAirInletHumidityRatio);
      }
      else
      {
        SupplyAirOutletHumidityRatio = SupplyAirInletHumidityRatio;
        ExhaustAirOutletHumidityRatio = ExhaustAirInletHumidityRatio;
      }
    }

    /// <summary>Computes the total heat exchange efficiency [-] from sensible and latent effectivenesses.</summary>
    /// <returns>Total heat exchange efficiency [-].</returns>
    public double GetTotalEfficiency()
    {
      //Compute air inlet and outlet enthalpies
      double hSAi = MoistAir.GetEnthalpyFromDryBulbTemperatureAndHumidityRatio
        (SupplyAirInletDryBulbTemperature, SupplyAirInletHumidityRatio);
      double hSAo = MoistAir.GetEnthalpyFromDryBulbTemperatureAndHumidityRatio
        (SupplyAirOutletDryBulbTemperature, SupplyAirOutletHumidityRatio);
      double hEAi = MoistAir.GetEnthalpyFromDryBulbTemperatureAndHumidityRatio
        (ExhaustAirInletDryBulbTemperature, ExhaustAirInletHumidityRatio);

      return (hSAi - hSAo) / (hSAi - hEAi);
    }

    #endregion

    #region Static methods

    /// <summary>Computes the sensible heat transfer coefficient [kW/K] from rated conditions.</summary>
    /// <param name="supplyAirMassFlowRate">Supply air mass flow rate [kg/s].</param>
    /// <param name="exhaustAirMassFlowRate">Exhaust air mass flow rate [kg/s].</param>
    /// <param name="supplyAirDryBulbTemperature">Supply air dry-bulb temperature [°C].</param>
    /// <param name="supplyAirHumidityRatio">Supply air humidity ratio [kg/kg].</param>
    /// <param name="exhaustAirDryBulbTemperature">Exhaust air dry-bulb temperature [°C].</param>
    /// <param name="exhaustAirHumitidyRatio">Exhaust air humidity ratio [kg/kg].</param>
    /// <param name="efficiency">Sensible heat exchange efficiency [-].</param>
    /// <param name="flow">Air flow arrangement type.</param>
    /// <returns>Sensible heat transfer coefficient [kW/K].</returns>
    public static double GetSensibleHeatTransferCoefficient
      (double supplyAirMassFlowRate, double exhaustAirMassFlowRate,
      double supplyAirDryBulbTemperature, double supplyAirHumidityRatio,
      double exhaustAirDryBulbTemperature, double exhaustAirHumitidyRatio,
      double efficiency, AirFlow flow)
    {
      //Compute heat capacity rates [kW/K]
      double cpSA = MoistAir.GetSpecificHeat(supplyAirHumidityRatio);
      double cpEA = MoistAir.GetSpecificHeat(exhaustAirHumitidyRatio);
      double mcSA = supplyAirMassFlowRate * cpSA;
      double mcEA = exhaustAirMassFlowRate * cpEA;
      double mcMin = Math.Min(mcSA, mcEA);

      //Compute heat capacity rate ratio [-]
      double capacityRatio = mcMin / Math.Max(mcSA, mcEA);

      //Compute heat transfer effectiveness [-]
      double effectiveness;
      if (mcSA < mcEA) effectiveness = efficiency;
      else effectiveness = efficiency / capacityRatio;

      //Compute number of transfer units
      HeatExchange.FlowType fType;
      if (flow == AirFlow.CounterFlow) fType = HeatExchange.FlowType.CounterFlow;
      else fType = HeatExchange.FlowType.CrossFlow_BothFluidsUnmixed;
      double ntu = HeatExchange.GetNTU(effectiveness, capacityRatio, fType);

      return ntu * mcMin;
    }

    /// <summary>Computes the latent heat transfer coefficient [kg/(kg/kg)] from rated conditions.</summary>
    /// <param name="supplyAirMassFlowRate">Supply air mass flow rate [kg/s].</param>
    /// <param name="exhaustAirMassFlowRate">Exhaust air mass flow rate [kg/s].</param>
    /// <param name="supplyAirHumidityRatio">Supply air humidity ratio [kg/kg].</param>
    /// <param name="exhaustAirHumitidyRatio">Exhaust air humidity ratio [kg/kg].</param>
    /// <param name="efficiency">Latent heat exchange efficiency [-].</param>
    /// <param name="flow">Air flow arrangement type.</param>
    /// <returns>Latent heat transfer coefficient [kg/(kg/kg)].</returns>
    public static double GetLatentHeatTransferCoefficient
      (double supplyAirMassFlowRate, double exhaustAirMassFlowRate,
      double supplyAirHumidityRatio, double exhaustAirHumitidyRatio,
      double efficiency, AirFlow flow)
    {
      //Compute mass flow rate ratio [-]
      double mcMin = Math.Min(supplyAirMassFlowRate, exhaustAirMassFlowRate);
      double massFlowRatio = mcMin / Math.Max
        (supplyAirMassFlowRate, exhaustAirMassFlowRate);

      //Compute heat transfer effectiveness [-]
      double effectiveness;
      if (supplyAirMassFlowRate < exhaustAirMassFlowRate) effectiveness = efficiency;
      else effectiveness = efficiency / massFlowRatio;

      //Compute number of transfer units
      HeatExchange.FlowType fType;
      if (flow == AirFlow.CounterFlow) fType = HeatExchange.FlowType.CounterFlow;
      else fType = HeatExchange.FlowType.CrossFlow_BothFluidsUnmixed;
      double ntu = HeatExchange.GetNTU(effectiveness, massFlowRatio, fType);

      return ntu * mcMin;
    }

    /// <summary>Computes the sensible heat transfer effectiveness [-].</summary>
    /// <param name="supplyAirMassFlowRate">Supply air mass flow rate [kg/s].</param>
    /// <param name="exhaustAirMassFlowRate">Exhaust air mass flow rate [kg/s].</param>
    /// <param name="supplyAirDryBulbTemperature">Supply air dry-bulb temperature [°C].</param>
    /// <param name="supplyAirHumidityRatio">Supply air humidity ratio [kg/kg].</param>
    /// <param name="exhaustAirDryBulbTemperature">Exhaust air dry-bulb temperature [°C].</param>
    /// <param name="exhaustAirHumitidyRatio">Exhaust air humidity ratio [kg/kg].</param>
    /// <param name="heatTransferCoefficient">Sensible heat transfer coefficient [kW/K].</param>
    /// <param name="flow">Air flow arrangement type.</param>
    /// <param name="effectiveness">Sensible heat transfer effectiveness [-].</param>
    /// <param name="mcMin">Smaller heat capacity rate [kW/K].</param>
    /// <param name="capacityRatio">Mass flow rate ratio (min/max) [-].</param>
    /// <param name="isMcMinSASide">True if the supply air side has the smaller heat capacity rate.</param>
    public static void GetSensibleEffectiveness
      (double supplyAirMassFlowRate, double exhaustAirMassFlowRate,
      double supplyAirDryBulbTemperature, double supplyAirHumidityRatio,
      double exhaustAirDryBulbTemperature, double exhaustAirHumitidyRatio,
      double heatTransferCoefficient, AirFlow flow, out double effectiveness,
      out double mcMin, out double capacityRatio, out bool isMcMinSASide)
    {
      //Compute heat capacity rates [kW/K]
      double cpSA = MoistAir.GetSpecificHeat(supplyAirHumidityRatio);
      double cpEA = MoistAir.GetSpecificHeat(exhaustAirHumitidyRatio);
      double mcSA = supplyAirMassFlowRate * cpSA;
      double mcEA = exhaustAirMassFlowRate * cpEA;
      mcMin = Math.Min(mcSA, mcEA);
      isMcMinSASide = (mcSA == mcMin);

      //Compute heat capacity rate ratio [-]
      capacityRatio = mcMin / Math.Max(mcSA, mcEA);

      //Compute heat transfer effectiveness [-]
      double ntu = heatTransferCoefficient / mcMin;
      HeatExchange.FlowType fType;
      if (flow == AirFlow.CounterFlow) fType = HeatExchange.FlowType.CounterFlow;
      else fType = HeatExchange.FlowType.CrossFlow_BothFluidsUnmixed;
      effectiveness = HeatExchange.GetEffectiveness(ntu, capacityRatio, fType);
    }

    /// <summary>Computes the latent heat transfer effectiveness [-].</summary>
    /// <param name="supplyAirMassFlowRate">Supply air mass flow rate [kg/s].</param>
    /// <param name="exhaustAirMassFlowRate">Exhaust air mass flow rate [kg/s].</param>
    /// <param name="supplyAirHumidityRatio">Supply air humidity ratio [kg/kg].</param>
    /// <param name="exhaustAirHumitidyRatio">Exhaust air humidity ratio [kg/kg].</param>
    /// <param name="heatTransferCoefficient">Latent heat transfer coefficient [kg/(kg/kg)].</param>
    /// <param name="flow">Air flow arrangement type.</param>
    /// <param name="effectiveness">Latent heat transfer effectiveness [-].</param>
    /// <param name="mMin">Smaller mass flow rate [kg/s].</param>
    /// <param name="capacityRatio">Mass flow rate ratio (min/max) [-].</param>
    public static void GetLatentEffectiveness
     (double supplyAirMassFlowRate, double exhaustAirMassFlowRate,
     double supplyAirHumidityRatio, double exhaustAirHumitidyRatio,
     double heatTransferCoefficient, AirFlow flow, out double effectiveness,
     out double mMin, out double capacityRatio)
    {
      //Compute mass flow rate ratio [-]
      mMin = Math.Min(supplyAirMassFlowRate, exhaustAirMassFlowRate);
      capacityRatio = mMin / Math.Max(supplyAirMassFlowRate, exhaustAirMassFlowRate);

      //Compute heat transfer effectiveness [-]
      double ntu = heatTransferCoefficient / mMin;
      HeatExchange.FlowType fType;
      if (flow == AirFlow.CounterFlow) fType = HeatExchange.FlowType.CounterFlow;
      else fType = HeatExchange.FlowType.CrossFlow_BothFluidsUnmixed;
      effectiveness = HeatExchange.GetEffectiveness(ntu, capacityRatio, fType);
    }

    #endregion

  }
}
