/* AirToRefrigerantCrossFinHeatExchanger.cs
 *
 * Copyright (C) 2026 E.Togashi
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
  /// <summary>Cross-fin (plate-fin-and-tube) air-to-refrigerant coil solved as pure functions.</summary>
  /// <remarks>
  /// The refrigerant side is modelled as an isothermal surface (phase change; heat capacity
  /// rate ratio = 0) and enters the calculation only as a boundary temperature: no refrigerant
  /// property is referenced. Heating (condenser role) and cooling (evaporator role) are
  /// unified: when the refrigerant is hotter than the (possibly spray-precooled) inlet air the
  /// coil heats the air with a single dry section; otherwise the air is cooled through a
  /// dry → wet → frosted section cascade, and the defrost load is evaluated from the frosted
  /// moisture. For an air-to-water coil in which both streams change temperature, use
  /// <see cref="AirToWaterCrossFinHeatExchanger"/> instead.
  /// All methods are stateless; instance state (fan power, thermo-off control, setpoints, …)
  /// belongs to wrapper equipment models such as Popolo.Core.HVAC.VRF.VRFUnit.
  /// The overall heat transfer coefficient of the dry section is caller-supplied so that each
  /// wrapper can use a fixed value or its own air-velocity-dependent correlation; the wet-section
  /// conversion (heat-mass transfer analogy) and the frosting degradation factor are internal.
  /// </remarks>
  public static class AirToRefrigerantCrossFinHeatExchanger
  {

    #region Constant declarations

    /// <summary>Sublimation latent heat of ice [kJ/kg].</summary>
    private const double SUBLIMINATION_LATENT_HEAT = 2837;

    /// <summary>Isobaric specific heat of ice [kJ/(kg·K)].</summary>
    private const double ICE_ISOBARIC_SPECIFIC_HEAT = 2.090;

    /// <summary>Overall heat transfer coefficient degradation factor due to frosting.</summary>
    /// <remarks>A value of 0.1 gives good agreement with NEDO field measurement tests.</remarks>
    private const double F_PENALTY = 0.6;

    /// <summary>Convergence tolerance of the refrigerant temperature [°C].</summary>
    /// <remarks>
    /// 0.001 K is far below any physically meaningful precision of the model;
    /// tightening it further only wastes root-finding iterations.
    /// </remarks>
    private const double REFRIGERANT_TEMPERATURE_TOLERANCE = 0.001;

    #endregion

    #region Unified public methods

    /// <summary>Computes the heat transfer surface area [m²] from a rated operating point.</summary>
    /// <param name="heatTransferCoefficient">Overall heat transfer coefficient of the dry section [kW/(m²·K)].</param>
    /// <param name="airFlowRate">Air mass flow rate [kg/s].</param>
    /// <param name="refrigerantTemperature">Refrigerant (surface) temperature [°C].</param>
    /// <param name="heatTransfer">Heat transfer capacity [kW] (negative = air is cooled, positive = air is heated).</param>
    /// <param name="inletAirTemperature">Inlet air dry-bulb temperature [°C].</param>
    /// <param name="inletAirHumidityRatio">Inlet air humidity ratio [kg/kg].</param>
    /// <param name="borderRelativeHumidity">Relative humidity at the dry/wet boundary [%] (cooling only).</param>
    /// <returns>Heat transfer surface area [m²].</returns>
    public static double GetSurfaceArea(
      double heatTransferCoefficient, double airFlowRate,
      double refrigerantTemperature, double heatTransfer,
      double inletAirTemperature, double inletAirHumidityRatio, double borderRelativeHumidity)
    {
      if (heatTransfer == 0) throw new PopoloArgumentException(
        "Heat transfer must be non-zero (negative = air is cooled, positive = air is heated).",
        nameof(heatTransfer));

      if (heatTransfer < 0)
        return GetCoolingSurfaceArea(heatTransferCoefficient, airFlowRate,
          refrigerantTemperature, heatTransfer, inletAirTemperature, inletAirHumidityRatio, borderRelativeHumidity);
      else
        return GetHeatingSurfaceArea(heatTransferCoefficient, airFlowRate,
          refrigerantTemperature, heatTransfer, inletAirTemperature, inletAirHumidityRatio);
    }

    /// <summary>Computes the heat transfer rate [kW] for a given refrigerant (surface) temperature.</summary>
    /// <remarks>
    /// Water spray (if any) is applied to the inlet air first; the coil then heats the air when
    /// the refrigerant is at or above the sprayed inlet air temperature and cools it otherwise.
    /// </remarks>
    /// <param name="heatTransferCoefficient">Overall heat transfer coefficient of the dry section [kW/(m²·K)].</param>
    /// <param name="refrigerantTemperature">Refrigerant (surface) temperature [°C].</param>
    /// <param name="airFlowRate">Air mass flow rate [kg/s].</param>
    /// <param name="surfaceArea">Heat transfer surface area [m²].</param>
    /// <param name="inletAirTemperature">Inlet air dry-bulb temperature [°C].</param>
    /// <param name="inletAirHumidityRatio">Inlet air humidity ratio [kg/kg].</param>
    /// <param name="borderRelativeHumidity">Relative humidity at the dry/wet boundary [%] (cooling only).</param>
    /// <param name="sprayEffectiveness">Water spray temperature reduction effectiveness [-] (0 disables spray).</param>
    /// <param name="heatTransfer">Output: heat transfer rate [kW] (negative = air is cooled, positive = air is heated).</param>
    /// <param name="outletAirTemperature">Output: outlet air dry-bulb temperature [°C].</param>
    /// <param name="outletAirHumidityRatio">Output: outlet air humidity ratio [kg/kg].</param>
    /// <param name="drySurfaceArea">Output: dry section surface area [m²].</param>
    /// <param name="wetSurfaceArea">Output: wet section surface area [m²] (0 when heating).</param>
    /// <param name="defrostLoad">Output: defrost load [kW] (0 when heating).</param>
    /// <param name="waterSupply">Output: water consumption rate of the spray [kg/s].</param>
    public static void GetHeatTransfer(
      double heatTransferCoefficient, double refrigerantTemperature,
      double airFlowRate, double surfaceArea,
      double inletAirTemperature, double inletAirHumidityRatio, double borderRelativeHumidity,
      double sprayEffectiveness,
      out double heatTransfer, out double outletAirTemperature, out double outletAirHumidityRatio,
      out double drySurfaceArea, out double wetSurfaceArea, out double defrostLoad, out double waterSupply)
    {
      //Apply the water spray to the inlet air first
      if (0 < sprayEffectiveness)
        waterSupply = ApplyWaterSpray
          (ref inletAirTemperature, ref inletAirHumidityRatio, sprayEffectiveness, airFlowRate);
      else waterSupply = 0;

      //Heating (condenser role): single dry section
      if (inletAirTemperature <= refrigerantTemperature)
      {
        GetHeatingHeatTransfer(heatTransferCoefficient, refrigerantTemperature,
          airFlowRate, surfaceArea, inletAirTemperature, inletAirHumidityRatio, 0,
          out heatTransfer, out outletAirTemperature, out outletAirHumidityRatio, out _);
        drySurfaceArea = surfaceArea;
        wetSurfaceArea = 0;
        defrostLoad = 0;
      }
      //Cooling (evaporator role): dry → wet → frosted cascade
      else
      {
        GetCoolingHeatTransfer(heatTransferCoefficient, refrigerantTemperature,
          airFlowRate, surfaceArea, inletAirTemperature, inletAirHumidityRatio, borderRelativeHumidity,
          out heatTransfer, out outletAirTemperature, out outletAirHumidityRatio,
          out drySurfaceArea, out wetSurfaceArea, out defrostLoad);
      }
    }

    /// <summary>Computes the refrigerant (surface) temperature [°C] required to process the given heat transfer.</summary>
    /// <param name="heatTransferCoefficient">Overall heat transfer coefficient of the dry section [kW/(m²·K)].</param>
    /// <param name="heatTransfer">Heat transfer rate [kW] (negative = air is cooled, positive = air is heated).</param>
    /// <param name="airFlowRate">Air mass flow rate [kg/s].</param>
    /// <param name="surfaceArea">Heat transfer surface area [m²].</param>
    /// <param name="inletAirTemperature">Inlet air dry-bulb temperature [°C].</param>
    /// <param name="inletAirHumidityRatio">Inlet air humidity ratio [kg/kg].</param>
    /// <param name="borderRelativeHumidity">Relative humidity at the dry/wet boundary [%] (cooling only).</param>
    /// <param name="deductDefrostLoad">True to deduct the defrost load from the heat transfer (cooling only).</param>
    /// <param name="sprayEffectiveness">Water spray temperature reduction effectiveness [-] (heating only; 0 disables spray).</param>
    /// <param name="refrigerantTemperature">Output: refrigerant (surface) temperature [°C].</param>
    /// <param name="outletAirTemperature">Output: outlet air dry-bulb temperature [°C].</param>
    /// <param name="outletAirHumidityRatio">Output: outlet air humidity ratio [kg/kg].</param>
    /// <param name="drySurfaceArea">Output: dry section surface area [m²].</param>
    /// <param name="wetSurfaceArea">Output: wet section surface area [m²] (0 when heating).</param>
    /// <param name="defrostLoad">Output: defrost load [kW] (0 when heating).</param>
    /// <param name="waterSupply">Output: water consumption rate of the spray [kg/s].</param>
    public static void GetRefrigerantTemperature(
      double heatTransferCoefficient, double heatTransfer,
      double airFlowRate, double surfaceArea,
      double inletAirTemperature, double inletAirHumidityRatio, double borderRelativeHumidity,
      bool deductDefrostLoad, double sprayEffectiveness,
      out double refrigerantTemperature, out double outletAirTemperature, out double outletAirHumidityRatio,
      out double drySurfaceArea, out double wetSurfaceArea, out double defrostLoad, out double waterSupply)
    {
      if (heatTransfer == 0) throw new PopoloArgumentException(
        "Heat transfer must be non-zero (negative = air is cooled, positive = air is heated).",
        nameof(heatTransfer));

      //Cooling (evaporator role)
      if (heatTransfer < 0)
      {
        GetCoolingRefrigerantTemperature(heatTransferCoefficient, heatTransfer,
          airFlowRate, surfaceArea, inletAirTemperature, inletAirHumidityRatio, borderRelativeHumidity,
          deductDefrostLoad, out refrigerantTemperature, out outletAirTemperature,
          out outletAirHumidityRatio, out drySurfaceArea, out wetSurfaceArea, out defrostLoad);
        waterSupply = 0;
      }
      //Heating (condenser role)
      else
      {
        GetHeatingRefrigerantTemperature(heatTransferCoefficient, heatTransfer,
          airFlowRate, surfaceArea, inletAirTemperature, inletAirHumidityRatio, sprayEffectiveness,
          out refrigerantTemperature, out outletAirTemperature, out outletAirHumidityRatio, out waterSupply);
        drySurfaceArea = surfaceArea;
        wetSurfaceArea = 0;
        defrostLoad = 0;
      }
    }

    /// <summary>Computes the refrigerant (surface) temperature [°C] required to reach the outlet air temperature setpoint.</summary>
    /// <param name="heatTransferCoefficient">Overall heat transfer coefficient of the dry section [kW/(m²·K)].</param>
    /// <param name="outletAirSetpointTemperature">Outlet air dry-bulb temperature setpoint [°C].</param>
    /// <param name="airFlowRate">Air mass flow rate [kg/s].</param>
    /// <param name="surfaceArea">Heat transfer surface area [m²].</param>
    /// <param name="inletAirTemperature">Inlet air dry-bulb temperature [°C].</param>
    /// <param name="inletAirHumidityRatio">Inlet air humidity ratio [kg/kg].</param>
    /// <param name="borderRelativeHumidity">Relative humidity at the dry/wet boundary [%] (cooling only).</param>
    /// <param name="sprayEffectiveness">Water spray temperature reduction effectiveness [-] (heating only; 0 disables spray).</param>
    /// <param name="refrigerantTemperature">Output: refrigerant (surface) temperature [°C].</param>
    /// <param name="heatTransfer">Output: heat transfer rate [kW] (negative = air is cooled, positive = air is heated).</param>
    /// <param name="outletAirHumidityRatio">Output: outlet air humidity ratio [kg/kg].</param>
    /// <param name="drySurfaceArea">Output: dry section surface area [m²].</param>
    /// <param name="wetSurfaceArea">Output: wet section surface area [m²] (0 when heating).</param>
    /// <param name="defrostLoad">Output: defrost load [kW] (0 when heating).</param>
    /// <param name="waterSupply">Output: water consumption rate of the spray [kg/s].</param>
    public static void GetRefrigerantTemperatureForOutletAirTemperature(
      double heatTransferCoefficient, double outletAirSetpointTemperature,
      double airFlowRate, double surfaceArea,
      double inletAirTemperature, double inletAirHumidityRatio, double borderRelativeHumidity,
      double sprayEffectiveness,
      out double refrigerantTemperature, out double heatTransfer, out double outletAirHumidityRatio,
      out double drySurfaceArea, out double wetSurfaceArea, out double defrostLoad, out double waterSupply)
    {
      if (outletAirSetpointTemperature == inletAirTemperature) throw new PopoloArgumentException(
        "Outlet air setpoint must differ from the inlet air temperature.",
        nameof(outletAirSetpointTemperature));

      //Cooling (evaporator role)
      if (outletAirSetpointTemperature < inletAirTemperature)
      {
        GetCoolingRefrigerantTemperatureForOutletAirTemperature(heatTransferCoefficient,
          outletAirSetpointTemperature, airFlowRate, surfaceArea,
          inletAirTemperature, inletAirHumidityRatio, borderRelativeHumidity,
          out refrigerantTemperature, out heatTransfer, out outletAirHumidityRatio,
          out drySurfaceArea, out wetSurfaceArea, out defrostLoad);
        waterSupply = 0;
      }
      //Heating (condenser role)
      else
      {
        GetHeatingRefrigerantTemperatureForOutletAirTemperature(heatTransferCoefficient,
          outletAirSetpointTemperature, airFlowRate, surfaceArea,
          inletAirTemperature, inletAirHumidityRatio, sprayEffectiveness,
          out refrigerantTemperature, out heatTransfer, out outletAirHumidityRatio, out waterSupply);
        drySurfaceArea = surfaceArea;
        wetSurfaceArea = 0;
        defrostLoad = 0;
      }
    }

    /// <summary>Applies evaporative water spray to the inlet air and returns the water consumption rate [kg/s].</summary>
    /// <remarks>
    /// The inlet air state is shifted toward its adiabatic saturation point by the given
    /// effectiveness. This is a pre-treatment of the inlet air, independent of the coil solve.
    /// The water consumption equals the moisture gained by the air stream,
    /// airFlowRate × (sprayed − original humidity ratio).
    /// </remarks>
    /// <param name="inletAirTemperature">Inlet air dry-bulb temperature [°C]; replaced by the sprayed state.</param>
    /// <param name="inletAirHumidityRatio">Inlet air humidity ratio [kg/kg]; replaced by the sprayed state.</param>
    /// <param name="sprayEffectiveness">Water spray temperature reduction effectiveness [-].</param>
    /// <param name="airFlowRate">Air mass flow rate [kg/s].</param>
    /// <returns>Water consumption rate [kg/s].</returns>
    public static double ApplyWaterSpray
      (ref double inletAirTemperature, ref double inletAirHumidityRatio,
      double sprayEffectiveness, double airFlowRate)
    {
      double twb = MoistAir.GetWetBulbTemperatureFromDryBulbTemperatureAndHumidityRatio
          (inletAirTemperature, inletAirHumidityRatio, PhysicsConstants.StandardAtmosphericPressure);
      double ts = MoistAir.GetDryBulbTemperatureFromWetBulbTemperatureAndRelativeHumidity
        (twb, 100, PhysicsConstants.StandardAtmosphericPressure);
      double ws = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndRelativeHumidity
        (ts, 100, PhysicsConstants.StandardAtmosphericPressure);
      double dw = sprayEffectiveness * (ws - inletAirHumidityRatio);
      inletAirTemperature -= sprayEffectiveness * (inletAirTemperature - ts);
      inletAirHumidityRatio += dw;
      return airFlowRate * dw;
    }

    #endregion

    #region Cooling (evaporator role) internals

    /// <summary>Computes the surface area [m²] of an air-cooling coil (dry/wet/frosted cascade).</summary>
    /// <param name="heatTransferCoefficient">Overall heat transfer coefficient of the dry section [kW/(m²·K)].</param>
    /// <param name="airFlowRate">Air mass flow rate [kg/s].</param>
    /// <param name="refrigerantTemperature">Evaporating temperature [°C].</param>
    /// <param name="heatTransfer">Heat transfer capacity [kW] (negative = cooling).</param>
    /// <param name="inletAirTemperature">Inlet air dry-bulb temperature [°C].</param>
    /// <param name="inletAirHumidityRatio">Inlet air humidity ratio [kg/kg].</param>
    /// <param name="borderRelativeHumidity">Relative humidity at the dry/wet boundary [%].</param>
    /// <returns>Heat transfer surface area [m²].</returns>
    internal static double GetCoolingSurfaceArea(
      double heatTransferCoefficient, double airFlowRate,
      double refrigerantTemperature, double heatTransfer,
      double inletAirTemperature, double inletAirHumidityRatio, double borderRelativeHumidity)
    {
      double epsilon;
      heatTransfer = -heatTransfer; //Flip sign

      //Determine the dry/wet boundary
      //Supersaturated inlet air (rh > 100) is treated as saturated: the whole coil becomes wet
      double rh = Math.Min(100, MoistAir.GetRelativeHumidityFromDryBulbTemperatureAndHumidityRatio
        (inletAirTemperature, inletAirHumidityRatio, PhysicsConstants.StandardAtmosphericPressure));
      borderRelativeHumidity = Math.Max(rh, borderRelativeHumidity);

      //Compute the moist air specific heat
      double cpmaWB = MoistAir.GetSpecificHeat(inletAirHumidityRatio);

      //Compute the dry coil surface area
      double mca = cpmaWB * airFlowRate;
      double tWB = Math.Min(inletAirTemperature,
        MoistAir.GetDryBulbTemperatureFromHumidityRatioAndRelativeHumidity
        (inletAirHumidityRatio, borderRelativeHumidity, PhysicsConstants.StandardAtmosphericPressure));
      double qD = (inletAirTemperature - tWB) * mca;

      //Case: heat transfer completes within the dry coil
      if (heatTransfer < qD)
      {
        epsilon = heatTransfer / (mca * (inletAirTemperature - refrigerantTemperature));
        if (1 <= epsilon) throw new PopoloNumericalException(
          "GetCoolingSurfaceArea",
          $"NTU-method diverged (epsilon={epsilon:F4} >= 1). "
          + $"Check inlet conditions: Tair={inletAirTemperature:F2}°C, evpTemp={refrigerantTemperature:F2}°C, "
          + $"heatTransfer={heatTransfer:F3} kW.");
        return -Math.Log(1 - epsilon) * mca / heatTransferCoefficient;
      }
      //Case: heat transfer extends into the wet coil
      epsilon = qD / (mca * (inletAirTemperature - refrigerantTemperature));
      if (1 <= epsilon) throw new PopoloNumericalException(
          "GetCoolingSurfaceArea",
          $"NTU-method diverged (epsilon={epsilon:F4} >= 1). "
          + $"Check inlet conditions: Tair={inletAirTemperature:F2}°C, evpTemp={refrigerantTemperature:F2}°C, "
          + $"heatTransfer={heatTransfer:F3} kW.");
      double sD = -Math.Log(1 - epsilon) * mca / heatTransferCoefficient;

      double qW, sW, xFB, tFB, cpmaFB;
      //Case: a wet coil section exists
      if (0 < tWB)
      {
        tFB = 0;
        //Compute the wet coil surface area
        xFB = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndRelativeHumidity
          (0, borderRelativeHumidity, PhysicsConstants.StandardAtmosphericPressure);
        cpmaFB = MoistAir.GetSpecificHeat(xFB);
        double hWB = MoistAir.GetEnthalpyFromDryBulbTemperatureAndHumidityRatio
          (tWB, inletAirHumidityRatio);
        double hEvp = MoistAir.GetEnthalpyFromDryBulbTemperatureAndRelativeHumidity
          (refrigerantTemperature, 100, PhysicsConstants.StandardAtmosphericPressure);
        double hFB = MoistAir.GetEnthalpyFromDryBulbTemperatureAndRelativeHumidity
          (0, borderRelativeHumidity, PhysicsConstants.StandardAtmosphericPressure);
        qW = (hWB - hFB) * airFlowRate;
        double kW = heatTransferCoefficient / (0.5 * (cpmaWB + cpmaFB));

        //Case: heat transfer completes within the wet coil
        if (heatTransfer - qD < qW)
        {
          epsilon = (heatTransfer - qD) / (airFlowRate * (hWB - hEvp));
          if (1 <= epsilon) throw new PopoloNumericalException(
          "GetCoolingSurfaceArea",
          $"NTU-method diverged (epsilon={epsilon:F4} >= 1). "
          + $"Check inlet conditions: Tair={inletAirTemperature:F2}°C, evpTemp={refrigerantTemperature:F2}°C, "
          + $"heatTransfer={heatTransfer:F3} kW.");
          return -Math.Log(1 - epsilon) * airFlowRate / kW + sD;
        }
        //Case: heat transfer extends into the frosted coil
        epsilon = qW / (airFlowRate * (hWB - hEvp));
        if (1 <= epsilon) throw new PopoloNumericalException(
          "GetCoolingSurfaceArea",
          $"NTU-method diverged (epsilon={epsilon:F4} >= 1). "
          + $"Check inlet conditions: Tair={inletAirTemperature:F2}°C, evpTemp={refrigerantTemperature:F2}°C, "
          + $"heatTransfer={heatTransfer:F3} kW.");
        sW = -Math.Log(1 - epsilon) * airFlowRate / kW;
      }
      else
      {
        tFB = tWB;
        xFB = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndRelativeHumidity
          (tWB, borderRelativeHumidity, PhysicsConstants.StandardAtmosphericPressure);
        cpmaFB = MoistAir.GetSpecificHeat(xFB);
        qW = 0;
        sW = 0;
      }

      //Compute the frosted coil surface area
      double kF = heatTransferCoefficient / cpmaFB * F_PENALTY;
      double hdF = MoistAir.GetEnthalpyFromDryBulbTemperatureAndRelativeHumidity
        (tFB, borderRelativeHumidity, PhysicsConstants.StandardAtmosphericPressure);
      double hdEvp = MoistAir.GetEnthalpyFromDryBulbTemperatureAndRelativeHumidity
        (refrigerantTemperature, 100, PhysicsConstants.StandardAtmosphericPressure);
      epsilon = (heatTransfer - qD - qW) / (airFlowRate * (hdF - hdEvp));
      if (1 <= epsilon) throw new PopoloNumericalException(
          "GetCoolingSurfaceArea",
          $"NTU-method diverged (epsilon={epsilon:F4} >= 1). "
          + $"Check inlet conditions: Tair={inletAirTemperature:F2}°C, evpTemp={refrigerantTemperature:F2}°C, "
          + $"heatTransfer={heatTransfer:F3} kW.");
      double sF = -Math.Log(1 - epsilon) * airFlowRate / kF;

      return sF + sD + sW;
    }

    /// <summary>Computes the heat transfer rate [kW] of an air-cooling coil (dry/wet/frosted cascade).</summary>
    /// <param name="heatTransferCoefficient">Overall heat transfer coefficient of the dry section [kW/(m²·K)].</param>
    /// <param name="refrigerantTemperature">Evaporating temperature [°C].</param>
    /// <param name="airFlowRate">Air mass flow rate [kg/s].</param>
    /// <param name="surfaceArea">Heat transfer surface area [m²].</param>
    /// <param name="inletAirTemperature">Inlet air dry-bulb temperature [°C].</param>
    /// <param name="inletAirHumidityRatio">Inlet air humidity ratio [kg/kg].</param>
    /// <param name="borderRelativeHumidity">Relative humidity at the dry/wet boundary [%].</param>
    /// <param name="heatTransfer">Output: heat transfer [kW] (negative = cooling).</param>
    /// <param name="outletAirTemperature">Output: outlet air dry-bulb temperature [°C].</param>
    /// <param name="outletAirHumidityRatio">Output: outlet air humidity ratio [kg/kg].</param>
    /// <param name="sD">Output: dry coil surface area [m²].</param>
    /// <param name="sW">Output: wet coil surface area [m²].</param>
    /// <param name="defrostLoad">Output: defrost load [kW].</param>
    internal static void GetCoolingHeatTransfer(
      double heatTransferCoefficient, double refrigerantTemperature,
      double airFlowRate, double surfaceArea,
      double inletAirTemperature, double inletAirHumidityRatio, double borderRelativeHumidity,
      out double heatTransfer, out double outletAirTemperature, out double outletAirHumidityRatio,
      out double sD, out double sW, out double defrostLoad)
    {
      //Determine the dry/wet boundary
      //Supersaturated inlet air (rh > 100) is treated as saturated: the whole coil becomes wet
      double rh = Math.Min(100, MoistAir.GetRelativeHumidityFromDryBulbTemperatureAndHumidityRatio
        (inletAirTemperature, inletAirHumidityRatio, PhysicsConstants.StandardAtmosphericPressure));
      borderRelativeHumidity = Math.Max(rh, borderRelativeHumidity);
      double tWB = Math.Min(inletAirTemperature,
        MoistAir.GetDryBulbTemperatureFromHumidityRatioAndRelativeHumidity
        (inletAirHumidityRatio, borderRelativeHumidity, PhysicsConstants.StandardAtmosphericPressure));

      //Compute the moist air specific heat [kJ/kgK]
      double cpmaWB = MoistAir.GetSpecificHeat(inletAirHumidityRatio);
      double mca = cpmaWB * airFlowRate;

      //Dry coil calculation
      //Compute the surface area required to cool down to the dew point
      double qD = mca * (inletAirTemperature - tWB);
      double epsilonD = qD / (mca * (inletAirTemperature - refrigerantTemperature));
      if (epsilonD <= 1) sD = -Math.Log(1 - epsilonD) * mca / heatTransferCoefficient;
      else sD = surfaceArea;

      double hWB = MoistAir.GetEnthalpyFromDryBulbTemperatureAndHumidityRatio(tWB, inletAirHumidityRatio);
      double hEvp =
        MoistAir.GetEnthalpyFromDryBulbTemperatureAndRelativeHumidity(refrigerantTemperature, 100, PhysicsConstants.StandardAtmosphericPressure);

      //Case: heat transfer completes within the dry coil alone
      if (surfaceArea <= sD || 1 <= epsilonD || hWB < hEvp)
      {
        sD = surfaceArea;
        sW = 0;
        defrostLoad = 0;
        outletAirHumidityRatio = inletAirHumidityRatio;

        epsilonD = 1 - Math.Exp(-heatTransferCoefficient * sD / mca);
        qD = epsilonD * mca * (inletAirTemperature - refrigerantTemperature);
        outletAirTemperature = inletAirTemperature - qD / mca;
        heatTransfer = -qD;
        return;
      }

      //Case: a wet coil section exists
      double tFB, qW, xFB, cpmaFB;
      if (0 < tWB)
      {
        tFB = 0;
        xFB = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndRelativeHumidity
          (0, borderRelativeHumidity, PhysicsConstants.StandardAtmosphericPressure);
        cpmaFB = MoistAir.GetSpecificHeat(xFB);

        //Compute the surface area required to cool down to the freezing point (0C)
        double hFB = MoistAir.GetEnthalpyFromDryBulbTemperatureAndRelativeHumidity
          (0, borderRelativeHumidity, PhysicsConstants.StandardAtmosphericPressure);

        qW = (hWB - hFB) * airFlowRate;
        double kW = heatTransferCoefficient / (0.5 * (cpmaWB + cpmaFB));
        double epsilonW = qW / (airFlowRate * (hWB - hEvp));
        if (epsilonW <= 1) sW = -Math.Log(1 - epsilonW) * airFlowRate / kW;
        else sW = surfaceArea - sD;

        //Case: heat transfer completes within the wet coil
        if (surfaceArea <= sW + sD || 1 <= epsilonW)
        {
          sW = surfaceArea - sD;
          defrostLoad = 0;

          epsilonW = 1 - Math.Exp(-kW * sW / airFlowRate);
          qW = epsilonW * airFlowRate * (hWB - hEvp);
          double ho2 = hWB - qW / airFlowRate;
          outletAirHumidityRatio = MoistAir.GetHumidityRatioFromEnthalpyAndRelativeHumidity
            (ho2, borderRelativeHumidity, PhysicsConstants.StandardAtmosphericPressure);
          outletAirTemperature = MoistAir.GetDryBulbTemperatureFromHumidityRatioAndEnthalpy
            (outletAirHumidityRatio, ho2);
          heatTransfer = -(qD + qW);
          return;
        }
      }
      else
      {
        qW = 0;
        sW = 0;
        tFB = tWB;
        xFB = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndRelativeHumidity
          (tWB, borderRelativeHumidity, PhysicsConstants.StandardAtmosphericPressure);
        cpmaFB = MoistAir.GetSpecificHeat(xFB);
      }

      //Frosted coil calculation
      double kF = heatTransferCoefficient / cpmaFB * F_PENALTY;
      double hdFB = MoistAir.GetEnthalpyFromDryBulbTemperatureAndRelativeHumidity
        (tFB, borderRelativeHumidity, PhysicsConstants.StandardAtmosphericPressure);
      double hdEvp = MoistAir.GetEnthalpyFromDryBulbTemperatureAndRelativeHumidity
        (refrigerantTemperature, 100, PhysicsConstants.StandardAtmosphericPressure);
      double sF = surfaceArea - sD - sW;
      double epsilonF = 1 - Math.Exp(-kF * sF / airFlowRate);
      double qF = epsilonF * airFlowRate * (hdFB - hdEvp);
      double hdo = hdFB - qF / airFlowRate;

      //Iterate to converge on the outlet air temperature
      double to = tFB;
      double ho = MoistAir.GetEnthalpyFromDryBulbTemperatureAndRelativeHumidity
        (to, borderRelativeHumidity, PhysicsConstants.StandardAtmosphericPressure);
      double err1 = Math.Abs(ho - hdo);
      const double DELTA = 0.001;
      while (0.01 < err1)
      {
        ho = MoistAir.GetEnthalpyFromDryBulbTemperatureAndRelativeHumidity
          (to + DELTA, borderRelativeHumidity, PhysicsConstants.StandardAtmosphericPressure);
        double err2 = Math.Abs(ho - hdo);
        to -= DELTA * err1 / (err2 - err1);
        ho = MoistAir.GetEnthalpyFromDryBulbTemperatureAndRelativeHumidity
          (to, borderRelativeHumidity, PhysicsConstants.StandardAtmosphericPressure);
        err1 = Math.Abs(ho - hdo);
      }
      outletAirTemperature = to;
      outletAirHumidityRatio = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndRelativeHumidity
        (outletAirTemperature, borderRelativeHumidity, PhysicsConstants.StandardAtmosphericPressure);

      //Compute the defrost load
      defrostLoad = airFlowRate * (xFB - outletAirHumidityRatio)
        * (SUBLIMINATION_LATENT_HEAT - ICE_ISOBARIC_SPECIFIC_HEAT * outletAirTemperature);

      //Sum up the heat transfer [kW]
      heatTransfer = -(qD + qW + qF);
    }

    /// <summary>Computes the evaporating temperature [°C] required to process the given cooling heat transfer.</summary>
    /// <param name="heatTransferCoefficient">Overall heat transfer coefficient of the dry section [kW/(m²·K)].</param>
    /// <param name="heatTransfer">Heat transfer [kW] (negative = cooling).</param>
    /// <param name="airFlowRate">Air mass flow rate [kg/s].</param>
    /// <param name="surfaceArea">Heat transfer surface area [m²].</param>
    /// <param name="inletAirTemperature">Inlet air dry-bulb temperature [°C].</param>
    /// <param name="inletAirHumidityRatio">Inlet air humidity ratio [kg/kg].</param>
    /// <param name="borderRelativeHumidity">Relative humidity at the dry/wet boundary [%].</param>
    /// <param name="deductDefrostLoad">True to deduct the defrost load from the heat transfer.</param>
    /// <param name="refrigerantTemperature">Output: evaporating temperature [°C].</param>
    /// <param name="outletAirTemperature">Output: outlet air dry-bulb temperature [°C].</param>
    /// <param name="outletAirHumidityRatio">Output: outlet air humidity ratio [kg/kg].</param>
    /// <param name="sD">Output: dry coil surface area [m²].</param>
    /// <param name="sW">Output: wet coil surface area [m²].</param>
    /// <param name="defrostLoad">Output: defrost load [kW].</param>
    internal static void GetCoolingRefrigerantTemperature(
      double heatTransferCoefficient, double heatTransfer, double airFlowRate,
      double surfaceArea, double inletAirTemperature, double inletAirHumidityRatio, double borderRelativeHumidity,
      bool deductDefrostLoad, out double refrigerantTemperature, out double outletAirTemperature,
      out double outletAirHumidityRatio, out double sD, out double sW, out double defrostLoad)
    {
      //Initial guess for the evaporating temperature
      refrigerantTemperature = inletAirTemperature + heatTransfer / (airFlowRate * 1.006);

      Roots.ErrorFunction eFnc = delegate (double eTemp)
      {
        double ht, ot, oa, sd, sw, dl;
        GetCoolingHeatTransfer(heatTransferCoefficient, eTemp, airFlowRate, surfaceArea, inletAirTemperature,
          inletAirHumidityRatio, borderRelativeHumidity, out ht, out ot, out oa, out sd, out sw, out dl);
        if (deductDefrostLoad) return ht - heatTransfer - dl;
        else return ht - heatTransfer;
      };
      try
      {
        refrigerantTemperature = Roots.Brent(
          refrigerantTemperature - 20, refrigerantTemperature + 5, REFRIGERANT_TEMPERATURE_TOLERANCE, eFnc);
      }
      catch (Exception ex)
      {
        throw new PopoloNumericalException(
          "GetCoolingRefrigerantTemperature",
          $"Brent solver failed to find evaporating temperature. "
          + $"Tair={inletAirTemperature:F2}°C, hHeat={heatTransfer:F3} kW, surface={surfaceArea:F4} m². "
          + ex.Message, ex);
      }
      double hTransfer;
      GetCoolingHeatTransfer(heatTransferCoefficient, refrigerantTemperature, airFlowRate, surfaceArea,
        inletAirTemperature, inletAirHumidityRatio, borderRelativeHumidity,
        out hTransfer, out outletAirTemperature, out outletAirHumidityRatio, out sD, out sW, out defrostLoad);
    }

    /// <summary>Computes the evaporating temperature [°C] required to reach the outlet air temperature setpoint.</summary>
    /// <param name="heatTransferCoefficient">Overall heat transfer coefficient of the dry section [kW/(m²·K)].</param>
    /// <param name="outletAirSetpointTemperature">Outlet air dry-bulb temperature setpoint [°C].</param>
    /// <param name="airFlowRate">Air mass flow rate [kg/s].</param>
    /// <param name="surfaceArea">Heat transfer surface area [m²].</param>
    /// <param name="inletAirTemperature">Inlet air dry-bulb temperature [°C].</param>
    /// <param name="inletAirHumidityRatio">Inlet air humidity ratio [kg/kg].</param>
    /// <param name="borderRelativeHumidity">Relative humidity at the dry/wet boundary [%].</param>
    /// <param name="refrigerantTemperature">Output: evaporating temperature [°C].</param>
    /// <param name="heatTransfer">Output: heat transfer [kW] (negative = cooling).</param>
    /// <param name="outletAirHumidityRatio">Output: outlet air humidity ratio [kg/kg].</param>
    /// <param name="sD">Output: dry coil surface area [m²].</param>
    /// <param name="sW">Output: wet coil surface area [m²].</param>
    /// <param name="defrostLoad">Output: defrost load [kW].</param>
    internal static void GetCoolingRefrigerantTemperatureForOutletAirTemperature(
      double heatTransferCoefficient, double outletAirSetpointTemperature, double airFlowRate,
      double surfaceArea, double inletAirTemperature, double inletAirHumidityRatio, double borderRelativeHumidity,
      out double refrigerantTemperature, out double heatTransfer,
      out double outletAirHumidityRatio, out double sD, out double sW, out double defrostLoad)
    {
      //Initial guess for the evaporating temperature
      refrigerantTemperature = outletAirSetpointTemperature;

      Roots.ErrorFunction eFnc = delegate (double eTemp)
      {
        GetCoolingHeatTransfer(heatTransferCoefficient, eTemp, airFlowRate, surfaceArea, inletAirTemperature,
          inletAirHumidityRatio, borderRelativeHumidity,
          out _, out double ot, out _, out _, out _, out _);
        return ot - outletAirSetpointTemperature;
      };
      try
      {
        refrigerantTemperature = Roots.Brent(
          refrigerantTemperature - 20, refrigerantTemperature, REFRIGERANT_TEMPERATURE_TOLERANCE, eFnc);
      }
      catch (Exception ex)
      {
        throw new PopoloNumericalException(
          "GetCoolingRefrigerantTemperatureForOutletAirTemperature",
          $"Brent solver failed to find evaporating temperature for setpoint control. "
          + $"Tair={inletAirTemperature:F2}°C, Tsp={outletAirSetpointTemperature:F2}°C, surface={surfaceArea:F4} m². "
          + ex.Message, ex);
      }
      GetCoolingHeatTransfer(heatTransferCoefficient, refrigerantTemperature, airFlowRate, surfaceArea,
        inletAirTemperature, inletAirHumidityRatio, borderRelativeHumidity,
        out heatTransfer, out _, out outletAirHumidityRatio, out sD, out sW, out defrostLoad);
    }

    #endregion

    #region Heating (condenser role) internals

    /// <summary>Computes the surface area [m²] of an air-heating coil (single dry section).</summary>
    /// <param name="heatTransferCoefficient">Overall heat transfer coefficient of the dry section [kW/(m²·K)].</param>
    /// <param name="airFlowRate">Air mass flow rate [kg/s].</param>
    /// <param name="refrigerantTemperature">Condensing temperature [°C].</param>
    /// <param name="heatTransfer">Heat transfer capacity [kW] (positive = heating).</param>
    /// <param name="inletAirTemperature">Inlet air dry-bulb temperature [°C].</param>
    /// <param name="inletAirHumidityRatio">Inlet air humidity ratio [kg/kg].</param>
    /// <returns>Heat transfer surface area [m²].</returns>
    internal static double GetHeatingSurfaceArea(
      double heatTransferCoefficient, double airFlowRate,
      double refrigerantTemperature, double heatTransfer,
      double inletAirTemperature, double inletAirHumidityRatio)
    {
      double cpma = MoistAir.GetSpecificHeat(inletAirHumidityRatio);
      double mca = cpma * airFlowRate;
      double epsilon = heatTransfer / (mca * (refrigerantTemperature - inletAirTemperature));
      if (1 <= epsilon) throw new PopoloNumericalException(
          "GetHeatingSurfaceArea",
          $"NTU-method diverged (epsilon={epsilon:F4} >= 1). "
          + $"Check inlet conditions: Tair={inletAirTemperature:F2}°C, cndTemp={refrigerantTemperature:F2}°C, "
          + $"heatTransfer={heatTransfer:F3} kW.");
      return -Math.Log(1 - epsilon) * mca / heatTransferCoefficient;
    }

    /// <summary>Computes the heat transfer rate [kW] of an air-heating coil (single dry section).</summary>
    /// <param name="heatTransferCoefficient">Overall heat transfer coefficient of the dry section [kW/(m²·K)].</param>
    /// <param name="refrigerantTemperature">Condensing temperature [°C].</param>
    /// <param name="airFlowRate">Air mass flow rate [kg/s].</param>
    /// <param name="surfaceArea">Heat transfer surface area [m²].</param>
    /// <param name="inletAirTemperature">Inlet air dry-bulb temperature [°C].</param>
    /// <param name="inletAirHumidityRatio">Inlet air humidity ratio [kg/kg].</param>
    /// <param name="sprayEffectiveness">Water spray temperature reduction effectiveness [-].</param>
    /// <param name="heatTransfer">Output: heat transfer rate [kW] (positive = heating).</param>
    /// <param name="outletAirTemperature">Output: outlet air dry-bulb temperature [°C].</param>
    /// <param name="outletAirHumidityRatio">Output: outlet air humidity ratio [kg/kg].</param>
    /// <param name="waterSupply">Output: water consumption rate [kg/s].</param>
    internal static void GetHeatingHeatTransfer
      (double heatTransferCoefficient, double refrigerantTemperature,
      double airFlowRate, double surfaceArea,
      double inletAirTemperature, double inletAirHumidityRatio, double sprayEffectiveness,
      out double heatTransfer, out double outletAirTemperature,
      out double outletAirHumidityRatio, out double waterSupply)
    {
      //With water spray
      if (0 < sprayEffectiveness)
        waterSupply = ApplyWaterSpray
         (ref inletAirTemperature, ref inletAirHumidityRatio, sprayEffectiveness, airFlowRate);
      //Without water spray
      else waterSupply = 0;

      //Moist air specific heat [kJ/kgK]
      double cpma = MoistAir.GetSpecificHeat(inletAirHumidityRatio);
      double mca = cpma * airFlowRate;

      double epsilon = 1 - Math.Exp(-heatTransferCoefficient * surfaceArea / mca);
      double q = epsilon * mca * (refrigerantTemperature - inletAirTemperature);
      outletAirTemperature = inletAirTemperature + q / mca;
      outletAirHumidityRatio = inletAirHumidityRatio;
      heatTransfer = q;
    }

    /// <summary>Computes the condensing temperature [°C] required to process the given heating heat transfer.</summary>
    /// <param name="heatTransferCoefficient">Overall heat transfer coefficient of the dry section [kW/(m²·K)].</param>
    /// <param name="heatTransfer">Heat transfer [kW] (positive = heating).</param>
    /// <param name="airFlowRate">Air mass flow rate [kg/s].</param>
    /// <param name="surfaceArea">Heat transfer surface area [m²].</param>
    /// <param name="inletAirTemperature">Inlet air dry-bulb temperature [°C].</param>
    /// <param name="inletAirHumidityRatio">Inlet air humidity ratio [kg/kg].</param>
    /// <param name="sprayEffectiveness">Water spray temperature reduction effectiveness [-].</param>
    /// <param name="refrigerantTemperature">Output: condensing temperature [°C].</param>
    /// <param name="outletAirTemperature">Output: outlet air dry-bulb temperature [°C].</param>
    /// <param name="outletAirHumidityRatio">Output: outlet air humidity ratio [kg/kg].</param>
    /// <param name="waterSupply">Output: water consumption rate [kg/s].</param>
    internal static void GetHeatingRefrigerantTemperature(
      double heatTransferCoefficient, double heatTransfer, double airFlowRate,
      double surfaceArea, double inletAirTemperature, double inletAirHumidityRatio,
      double sprayEffectiveness, out double refrigerantTemperature,
      out double outletAirTemperature, out double outletAirHumidityRatio,
      out double waterSupply)
    {
      //With water spray
      if (0 < sprayEffectiveness)
        waterSupply = ApplyWaterSpray
         (ref inletAirTemperature, ref inletAirHumidityRatio, sprayEffectiveness, airFlowRate);
      //Without water spray
      else waterSupply = 0;

      //Moist air specific heat [kJ/kgK]
      double cpma = MoistAir.GetSpecificHeat(inletAirHumidityRatio);
      double mca = cpma * airFlowRate;

      outletAirTemperature = inletAirTemperature + heatTransfer / mca;
      outletAirHumidityRatio = inletAirHumidityRatio;
      double epsilon = 1 - Math.Exp(-heatTransferCoefficient * surfaceArea / mca);
      refrigerantTemperature = inletAirTemperature + heatTransfer / (epsilon * mca);
    }

    /// <summary>Computes the condensing temperature [°C] required to reach the outlet air temperature setpoint.</summary>
    /// <param name="heatTransferCoefficient">Overall heat transfer coefficient of the dry section [kW/(m²·K)].</param>
    /// <param name="outletAirSetpointTemperature">Outlet air dry-bulb temperature setpoint [°C].</param>
    /// <param name="airFlowRate">Air mass flow rate [kg/s].</param>
    /// <param name="surfaceArea">Heat transfer surface area [m²].</param>
    /// <param name="inletAirTemperature">Inlet air dry-bulb temperature [°C].</param>
    /// <param name="inletAirHumidityRatio">Inlet air humidity ratio [kg/kg].</param>
    /// <param name="sprayEffectiveness">Water spray temperature reduction effectiveness [-].</param>
    /// <param name="refrigerantTemperature">Output: condensing temperature [°C].</param>
    /// <param name="heatTransfer">Output: heat transfer [kW] (positive = heating).</param>
    /// <param name="outletAirHumidityRatio">Output: outlet air humidity ratio [kg/kg].</param>
    /// <param name="waterSupply">Output: water consumption rate [kg/s].</param>
    internal static void GetHeatingRefrigerantTemperatureForOutletAirTemperature(
      double heatTransferCoefficient, double outletAirSetpointTemperature, double airFlowRate,
      double surfaceArea, double inletAirTemperature, double inletAirHumidityRatio,
      double sprayEffectiveness, out double refrigerantTemperature,
      out double heatTransfer, out double outletAirHumidityRatio,
      out double waterSupply)
    {
      //With water spray
      if (0 < sprayEffectiveness)
        waterSupply = ApplyWaterSpray
         (ref inletAirTemperature, ref inletAirHumidityRatio, sprayEffectiveness, airFlowRate);
      //Without water spray
      else waterSupply = 0;

      //Moist air specific heat [kJ/kgK]
      double cpma = MoistAir.GetSpecificHeat(inletAirHumidityRatio);
      double mca = cpma * airFlowRate;

      heatTransfer = (outletAirSetpointTemperature - inletAirTemperature) * mca;
      outletAirHumidityRatio = inletAirHumidityRatio;
      double epsilon = 1 - Math.Exp(-heatTransferCoefficient * surfaceArea / mca);
      refrigerantTemperature = inletAirTemperature + heatTransfer / (epsilon * mca);
    }

    #endregion

  }
}
