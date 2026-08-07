/* VRFUnit.cs
 * 
 * Copyright (C) 2020 E.Togashi
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

namespace Popolo.Core.HVAC.VRF
{
  /// <summary>VRF indoor/outdoor heat exchanger unit (evaporator or condenser).</summary>
  public class VRFUnit : IReadOnlyVRFUnit
  {

    #region Constant declarations

    /// <summary>Overall heat transfer coefficient for the dry coil section [kW/(m²·K)].</summary>
    /// <remarks>
    /// Derived from: air-to-refrigerant surface area ratio = 15,
    /// refrigerant-side HTC = 6 kW/(m²·K), air-side HTC = 0.1 kW/(m²·K), fin efficiency = 0.9.
    /// </remarks>
    public const double HeatTransferCoefficient = 0.074;

    /// <summary>Sublimation latent heat of ice [kJ/kg].</summary>
    private const double SUBLIMINATION_LATENT_HEAT = 2837;

    /// <summary>Isobaric specific heat of ice [kJ/(kg·K)].</summary>
    private const double ICE_ISOBARIC_SPECIFIC_HEAT = 2.090;

    /// <summary>Overall heat transfer coefficient degradation factor due to frosting.</summary>
    /// <remarks>A value of 0.1 gives good agreement with NEDO field measurement tests.</remarks>
    private const double F_PENALTY = 0.6;

    #endregion

    #region Enumeration definitions

    /// <summary>Operating mode of the VRF unit.</summary>
    [Flags]
    public enum Mode
    {
      /// <summary>Shut-off mode.</summary>
      ShutOff = 1,
      /// <summary>Cooling mode.</summary>
      Cooling = 2,
      /// <summary>Heating mode.</summary>
      Heating = 4,
      /// <summary>Thermo-off (standby) mode.</summary>
      ThermoOff = 8
    }

    #endregion

    #region Properties

    /// <summary>Gets the selectable operating modes.</summary>
    public Mode SelectableMode { get; private set; }

    /// <summary>Gets or sets the current operating mode.</summary>
    public Mode CurrentMode { get; set; } = Mode.ShutOff;

    /// <summary>Relative humidity at the dry/wet boundary [%].</summary>
    private double borderRelativeHumidity;

    /// <summary>Gets the nominal cooling capacity [kW] (negative = cooling, positive = heating).</summary>
    public double NominalCoolingCapacity { get; private set; }

    /// <summary>Gets the nominal heating capacity [kW] (positive = heating, negative = cooling).</summary>
    public double NominalHeatingCapacity { get; private set; }

    /// <summary>Gets the evaporator heat transfer surface area [m²].</summary>
    public double EvaporatorSurfaceArea { get; private set; }

    /// <summary>Gets the condenser heat transfer surface area [m²].</summary>
    public double CondenserSurfaceArea { get; private set; }

    /// <summary>Gets the dry heat transfer surface area [m²].</summary>
    public double DrySurfaceArea { get; private set; }

    /// <summary>Gets the wet heat transfer surface area [m²].</summary>
    /// <remarks>Always 0 m² for a condenser unit.</remarks>
    public double WetSurfaceArea { get; private set; }

    /// <summary>Gets the frosted heat transfer surface area [m²].</summary>
    /// <remarks>Always 0 m² for a condenser unit.</remarks>
    public double FrostSurfaceArea
    {
      get { return EvaporatorSurfaceArea - (DrySurfaceArea + WetSurfaceArea); }
    }

    /// <summary>Gets the nominal air mass flow rate [kg/s].</summary>
    public double NominalAirFlowRate { get; private set; }

    /// <summary>Gets or sets the air mass flow rate [kg/s].</summary>
    public double AirFlowRate { get; set; }

    /// <summary>Gets the refrigerant temperature [°C].</summary>
    public double RefrigerantTemperature { get; private set; }

    /// <summary>Gets or sets the relative humidity at the dry/wet boundary [%].</summary>
    public double BorderRelativeHumidity
    {
      get { return borderRelativeHumidity; }
      set
      {
        if (value < 70) throw new PopoloOutOfRangeException(
            nameof(BorderRelativeHumidity), value, 70, null,
            "Dry/wet boundary relative humidity must be >= 70 % to be physically meaningful.");
        borderRelativeHumidity = value;
      }
    }

    /// <summary>Gets or sets the inlet air dry-bulb temperature [°C].</summary>
    public double InletAirTemperature { get; set; }

    /// <summary>Gets or sets the inlet air humidity ratio [kg/kg].</summary>
    public double InletAirHumidityRatio { get; set; }

    /// <summary>Gets the outlet air dry-bulb temperature [°C].</summary>
    public double OutletAirTemperature { get; private set; }

    /// <summary>Gets the outlet air humidity ratio [kg/kg].</summary>
    public double OutletAirHumidityRatio { get; private set; }

    /// <summary>Gets the total heat transfer rate [kW] (positive = heating, negative = cooling).</summary>
    /// <remarks>Thermo-off time ratio is already accounted for.</remarks>
    public double HeatTransfer { get; private set; }

    /// <summary>Gets the sensible heat transfer rate [kW] (positive = heating, negative = cooling).</summary>
    public double SensibleHeatTransfer
    {
      get
      {
        return MoistAir.GetSpecificHeat(InletAirHumidityRatio)
          * (OutletAirTemperature - InletAirTemperature) * AirFlowRate;
      }
    }

    /// <summary>Gets the latent heat transfer rate [kW] (positive = heating, negative = cooling).</summary>
    public double LatentHeatTransfer
    { get { return HeatTransfer - SensibleHeatTransfer; } }

    /// <summary>Gets the defrost load [kW].</summary>
    public double DefrostLoad { get; private set; }

    /// <summary>Gets or sets a value indicating whether to apply water spray to the condenser.</summary>
    public bool UseWaterSpray { get; set; } = false;

    /// <summary>Gets the water consumption rate from condenser water spray [kg/s].</summary>
    public double WaterSupply { get; private set; }

    /// <summary>Temperature reduction effectiveness of water spray [-].</summary>
    /// <remarks>Typical value 0.4–0.5. A value of 0 disables water spray.</remarks>
    private double sprayEffectiveness = 0.5;

    /// <summary>Gets or sets the temperature reduction effectiveness of the condenser water spray [-].</summary>
    /// <remarks>Typical value 0.4–0.5. A value of 0 disables water spray.</remarks>
    public double SprayEffectiveness
    {
      get { return sprayEffectiveness; }
      set { sprayEffectiveness = Math.Max(0, Math.Min(value, 1.0)); }
    }

    /// <summary>Gets the nominal fan electric power in cooling mode [kW].</summary>
    public double NominalCoolingFanElectricity { get; private set; }

    /// <summary>Gets the nominal fan electric power in heating mode [kW].</summary>
    public double NominalHeatingFanElectricity { get; private set; }

    /// <summary>Gets or sets the fan operating rate [-].</summary>
    public double FanOperatingRatio { get; set; }

    /// <summary>Gets the thermo-off time ratio [-].</summary>
    public double ThermoOffTimeRatio { get; private set; }

    /// <summary>Gets or sets the minimum fan electric power rate relative to nominal [-].</summary>
    /// <remarks>
    /// In practice, even at zero load, the fan runs at a minimum rate when the unit is on.
    /// Therefore, the minimum fan electric power rate is defined as a ratio to the nominal power.
    /// </remarks>
    public double MinFanElectricityRatio { get; set; } = 0.0;

    /// <summary>Gets the current fan electric power [kW].</summary>
    public double FanElectricity
    {
      get
      {
        double eRate = IsInverterControlledFan ? AirFlowRate / NominalAirFlowRate : 1.0;
        if (CurrentMode == Mode.Cooling) return NominalCoolingFanElectricity * Math.Max(MinFanElectricityRatio, FanOperatingRatio * eRate);
        else if (CurrentMode == Mode.Heating) return NominalHeatingFanElectricity * Math.Max(MinFanElectricityRatio, FanOperatingRatio * eRate);
        else if (CurrentMode == Mode.ThermoOff) return Math.Max(NominalCoolingFanElectricity, NominalHeatingFanElectricity) * Math.Max(MinFanElectricityRatio, FanOperatingRatio * eRate); //This handling is not ideal
        else return 0;
      }
    }

    /// <summary>Gets or sets the outlet air temperature setpoint [°C].</summary>
    public double OutletAirSetpointTemperature { get; set; }

    /// <summary>Gets or sets the outlet air humidity ratio setpoint [kg/kg].</summary>
    public double OutletAirSetpointHumidityRatio { get; set; }

    /// <summary>Gets or sets a value indicating whether to use humidification (effective in heating mode only).</summary>
    public bool UseHumidifier { get; set; } = false;

    /// <summary>Gets or sets a value indicating whether to stop the fan during thermo-off.</summary>
    public bool ShutoffFanWhenThermoOff { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether the fan uses inverter (speed) control.</summary>
    public bool IsInverterControlledFan { get; set; } = false;

    #endregion

    #region Constructors

    /// <summary>Initializes a new instance as a dedicated evaporator.</summary>
    /// <param name="airFlowRate">Air mass flow rate [kg/s].</param>
    /// <param name="fanElectricity">Fan electric power [kW].</param>
    /// <param name="evpTemperature">Evaporating temperature [°C].</param>
    /// <param name="evpHeatTransfer">Evaporator heat transfer capacity [kW] (negative = cooling).</param>
    /// <param name="evpInletAirTemperature">Evaporator inlet air dry-bulb temperature [°C].</param>
    /// <param name="evpInletAirHumidityRatio">Evaporator inlet air humidity ratio [kg/kg].</param>
    /// <param name="borderRelativeHumidity">Relative humidity at the dry/wet boundary [%].</param>
    public VRFUnit(
      double airFlowRate, double fanElectricity,
      double evpTemperature, double evpHeatTransfer,
      double evpInletAirTemperature, double evpInletAirHumidityRatio, double borderRelativeHumidity)
    {
      if (0 <= evpHeatTransfer) throw new PopoloArgumentException(
        "Evaporator heat transfer must be negative (cooling removes heat from air). "
        + $"Got: {evpHeatTransfer:F3} kW.",
        nameof(evpHeatTransfer));

      //Initialize properties
      NominalAirFlowRate = AirFlowRate = airFlowRate;
      BorderRelativeHumidity = borderRelativeHumidity;
      InletAirTemperature = OutletAirSetpointTemperature = OutletAirTemperature = evpInletAirTemperature;
      InletAirHumidityRatio = OutletAirHumidityRatio = evpInletAirHumidityRatio;
      NominalCoolingCapacity = evpHeatTransfer;
      NominalCoolingFanElectricity = fanElectricity;

      //Initialize heat transfer surface areas
      EvaporatorSurfaceArea = GetEvaporatorSurfaceArea(
        airFlowRate, evpTemperature, evpHeatTransfer,
        evpInletAirTemperature, evpInletAirHumidityRatio, borderRelativeHumidity);

      //Selectable modes: shut-off or evaporator (cooling)
      SelectableMode = Mode.ShutOff | Mode.Cooling;
      CurrentMode = Mode.Cooling;

      ThermoOff();
    }

    /// <summary>Initializes a new instance as a dedicated condenser.</summary>
    /// <param name="airFlowRate">Air mass flow rate [kg/s].</param>
    /// <param name="fanElectricity">Fan electric power [kW].</param>
    /// <param name="cndTemperature">Condensing temperature [°C].</param>
    /// <param name="cndHeatTransfer">Condenser heat transfer capacity [kW] (positive = heating).</param>
    /// <param name="cndInletAirTemperature">Condenser inlet air dry-bulb temperature [°C].</param>
    /// <param name="cndInletAirHumidityRatio">Condenser inlet air humidity ratio [kg/kg].</param>
    public VRFUnit(
      double airFlowRate, double fanElectricity,
      double cndTemperature, double cndHeatTransfer,
      double cndInletAirTemperature, double cndInletAirHumidityRatio)
    {
      if (cndHeatTransfer <= 0) throw new PopoloArgumentException(
        "Condenser heat transfer must be positive (condenser releases heat to air). "
        + $"Got: {cndHeatTransfer:F3} kW.",
        nameof(cndHeatTransfer));

      //Initialize properties
      NominalAirFlowRate = AirFlowRate = airFlowRate;
      InletAirTemperature = OutletAirSetpointTemperature = OutletAirTemperature = cndInletAirTemperature;
      InletAirHumidityRatio = OutletAirHumidityRatio = cndInletAirHumidityRatio;
      NominalHeatingCapacity = cndHeatTransfer;
      NominalHeatingFanElectricity = fanElectricity;

      //Initialize heat transfer surface areas
      CondenserSurfaceArea = GetCondenserSurfaceArea(
        airFlowRate, cndTemperature, cndHeatTransfer,
        cndInletAirTemperature, cndInletAirHumidityRatio);

      //Selectable modes: shut-off or condenser (heating)
      SelectableMode = Mode.ShutOff | Mode.Heating;
      CurrentMode = Mode.Heating;

      ThermoOff();
    }

    /// <summary>Initializes a new instance.</summary>
    /// <param name="airFlowRate">Air mass flow rate [kg/s].</param>
    /// <param name="evpTemperature">Evaporating temperature [°C].</param>
    /// <param name="evpHeatTransfer">Evaporator heat transfer [kW] (negative = cooling).</param>
    /// <param name="evpInletAirTemperature">Inlet air dry-bulb temperature in cooling mode [°C].</param>
    /// <param name="evpInletAirHumidityRatio">Inlet air humidity ratio in cooling mode [kg/kg].</param>
    /// <param name="borderRelativeHumidity">Relative humidity at the dry/wet boundary [%].</param>
    /// <param name="coolingFanElectricity">Fan electric power in cooling mode [kW].</param>
    /// <param name="cndTemperature">Condensing temperature [°C].</param>
    /// <param name="cndHeatTransfer">Condenser heat transfer [kW] (positive = heating).</param>
    /// <param name="cndInletAirTemperature">Inlet air dry-bulb temperature in heating mode [°C].</param>
    /// <param name="cndInletAirHumidityRatio">Inlet air humidity ratio in heating mode [kg/kg].</param>
    /// <param name="heatingFanElectricity">Fan electric power in heating mode [kW].</param>
    public VRFUnit(
      double airFlowRate,
      double evpTemperature, double evpHeatTransfer,
      double evpInletAirTemperature, double evpInletAirHumidityRatio, double borderRelativeHumidity,
      double coolingFanElectricity,
      double cndTemperature, double cndHeatTransfer,
      double cndInletAirTemperature, double cndInletAirHumidityRatio,
      double heatingFanElectricity)
    {
      if (0 <= evpHeatTransfer) throw new PopoloArgumentException(
        "Evaporator heat transfer must be negative (cooling removes heat from air). "
        + $"Got: {evpHeatTransfer:F3} kW.", nameof(evpHeatTransfer));
      if (cndHeatTransfer <= 0) throw new PopoloArgumentException(
        "Condenser heat transfer must be positive (condenser releases heat to air). "
        + $"Got: {cndHeatTransfer:F3} kW.", nameof(cndHeatTransfer));

      //Initialize properties
      NominalAirFlowRate = AirFlowRate = airFlowRate;
      BorderRelativeHumidity = borderRelativeHumidity;
      InletAirTemperature = OutletAirSetpointTemperature = OutletAirTemperature = evpInletAirTemperature;
      InletAirHumidityRatio = OutletAirHumidityRatio = evpInletAirHumidityRatio;
      NominalCoolingCapacity = evpHeatTransfer;
      NominalHeatingCapacity = cndHeatTransfer;
      NominalCoolingFanElectricity = coolingFanElectricity;
      NominalHeatingFanElectricity = heatingFanElectricity;

      //Initialize heat transfer surface areas
      EvaporatorSurfaceArea = GetEvaporatorSurfaceArea(
        airFlowRate, evpTemperature, evpHeatTransfer,
        evpInletAirTemperature, evpInletAirHumidityRatio, borderRelativeHumidity);
      CondenserSurfaceArea = GetCondenserSurfaceArea(
        airFlowRate, cndTemperature, cndHeatTransfer,
        cndInletAirTemperature, cndInletAirHumidityRatio);

      //Selectable modes: shut-off or condenser (heating)
      SelectableMode = Mode.ShutOff | Mode.Heating | Mode.Cooling;
      CurrentMode = Mode.ShutOff;

      ThermoOff();
    }

    #endregion

    #region Static methods

    /// <summary>Computes the evaporator heat transfer surface area [m²].</summary>
    /// <param name="airFlowRate">Air mass flow rate [kg/s].</param>
    /// <param name="evpTemperature">Evaporating temperature [°C].</param>
    /// <param name="heatTransfer">Heat transfer capacity [kW] (negative = cooling).</param>
    /// <param name="inletAirTemperature">Inlet air dry-bulb temperature [°C].</param>
    /// <param name="inletAirHumidityRatio">Inlet air humidity ratio [kg/kg].</param>
    /// <param name="borderRelativeHumidity">Relative humidity at the dry/wet boundary [%].</param>
    /// <returns>Evaporator heat transfer surface area [m²].</returns>
    public static double GetEvaporatorSurfaceArea(
      double airFlowRate,
      double evpTemperature, double heatTransfer,
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
        epsilon = heatTransfer / (mca * (inletAirTemperature - evpTemperature));
        if (1 <= epsilon) throw new PopoloNumericalException(
          "GetEvaporatorSurfaceArea",
          $"NTU-method diverged (epsilon={epsilon:F4} >= 1). "
          + $"Check inlet conditions: Tair={inletAirTemperature:F2}°C, evpTemp={evpTemperature:F2}°C, "
          + $"heatTransfer={heatTransfer:F3} kW.");
        return -Math.Log(1 - epsilon) * mca / HeatTransferCoefficient;
      }
      //Case: heat transfer extends into the wet coil
      epsilon = qD / (mca * (inletAirTemperature - evpTemperature));
      if (1 <= epsilon) throw new PopoloNumericalException(
          "GetEvaporatorSurfaceArea",
          $"NTU-method diverged (epsilon={epsilon:F4} >= 1). "
          + $"Check inlet conditions: Tair={inletAirTemperature:F2}°C, evpTemp={evpTemperature:F2}°C, "
          + $"heatTransfer={heatTransfer:F3} kW.");
      double sD = -Math.Log(1 - epsilon) * mca / HeatTransferCoefficient;

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
          (evpTemperature, 100, PhysicsConstants.StandardAtmosphericPressure);
        double hFB = MoistAir.GetEnthalpyFromDryBulbTemperatureAndRelativeHumidity
          (0, borderRelativeHumidity, PhysicsConstants.StandardAtmosphericPressure);
        qW = (hWB - hFB) * airFlowRate;
        double kW = HeatTransferCoefficient / (0.5 * (cpmaWB + cpmaFB));

        //Case: heat transfer completes within the wet coil
        if (heatTransfer - qD < qW)
        {
          epsilon = (heatTransfer - qD) / (airFlowRate * (hWB - hEvp));
          if (1 <= epsilon) throw new PopoloNumericalException(
          "GetEvaporatorSurfaceArea",
          $"NTU-method diverged (epsilon={epsilon:F4} >= 1). "
          + $"Check inlet conditions: Tair={inletAirTemperature:F2}°C, evpTemp={evpTemperature:F2}°C, "
          + $"heatTransfer={heatTransfer:F3} kW.");
          return -Math.Log(1 - epsilon) * airFlowRate / kW + sD;
        }
        //Case: heat transfer extends into the frosted coil
        epsilon = qW / (airFlowRate * (hWB - hEvp));
        if (1 <= epsilon) throw new PopoloNumericalException(
          "GetEvaporatorSurfaceArea",
          $"NTU-method diverged (epsilon={epsilon:F4} >= 1). "
          + $"Check inlet conditions: Tair={inletAirTemperature:F2}°C, evpTemp={evpTemperature:F2}°C, "
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
      double kF = HeatTransferCoefficient / cpmaFB * F_PENALTY;
      double hdF = MoistAir.GetEnthalpyFromDryBulbTemperatureAndRelativeHumidity
        (tFB, borderRelativeHumidity, PhysicsConstants.StandardAtmosphericPressure);
      double hdEvp = MoistAir.GetEnthalpyFromDryBulbTemperatureAndRelativeHumidity
        (evpTemperature, 100, PhysicsConstants.StandardAtmosphericPressure);
      epsilon = (heatTransfer - qD - qW) / (airFlowRate * (hdF - hdEvp));
      if (1 <= epsilon) throw new PopoloNumericalException(
          "GetEvaporatorSurfaceArea",
          $"NTU-method diverged (epsilon={epsilon:F4} >= 1). "
          + $"Check inlet conditions: Tair={inletAirTemperature:F2}°C, evpTemp={evpTemperature:F2}°C, "
          + $"heatTransfer={heatTransfer:F3} kW.");
      double sF = -Math.Log(1 - epsilon) * airFlowRate / kF;

      return sF + sD + sW;
    }

    /// <summary>Computes the condenser heat transfer surface area [m²].</summary>
    /// <param name="airFlowRate">Air mass flow rate [kg/s].</param>
    /// <param name="condensingTempearture">Condensing temperature [°C].</param>
    /// <param name="heatTransfer">Heat transfer capacity [kW] (positive = heating).</param>
    /// <param name="inletAirTemperature">Inlet air dry-bulb temperature [°C].</param>
    /// <param name="inletAirHumidityRatio">Inlet air humidity ratio [kg/kg].</param>
    /// <returns>Condenser heat transfer surface area [m²].</returns>
    public static double GetCondenserSurfaceArea(
      double airFlowRate,
      double condensingTempearture, double heatTransfer,
      double inletAirTemperature, double inletAirHumidityRatio)
    {
      double cpma = MoistAir.GetSpecificHeat(inletAirHumidityRatio);
      double mca = cpma * airFlowRate;
      double epsilon = heatTransfer / (mca * (condensingTempearture - inletAirTemperature));
      if (1 <= epsilon) throw new PopoloNumericalException(
          "GetCondenserSurfaceArea",
          $"NTU-method diverged (epsilon={epsilon:F4} >= 1). "
          + $"Check inlet conditions: Tair={inletAirTemperature:F2}°C, cndTemp={condensingTempearture:F2}°C, "
          + $"heatTransfer={heatTransfer:F3} kW.");
      return -Math.Log(1 - epsilon) * mca / HeatTransferCoefficient;
    }

    #endregion

    #region Other methods

    /// <summary>Thermo-off (standby) mode.</summary>
    public void ThermoOff()
    {
      //Unless the unit is shut off, the fan keeps idling
      if (CurrentMode == Mode.ShutOff || ShutoffFanWhenThermoOff)
      {
        AirFlowRate = 0.0;
        FanOperatingRatio = 0.0;
      }
      else
      {
        //AirFlowRate = NominalAirFlowRate; //2023.02.10, this resets the air flow rate during the convergence iterations of free-run operation
        FanOperatingRatio = 1.0;
      }

      ThermoOffTimeRatio = 1.0;
      OutletAirTemperature = InletAirTemperature;
      OutletAirHumidityRatio = InletAirHumidityRatio;
      DrySurfaceArea = EvaporatorSurfaceArea;
      WetSurfaceArea = 0;
      HeatTransfer = 0;
      RefrigerantTemperature = InletAirTemperature; //Must be updated properly, since otherwise the refrigerant temperature would be indeterminate, depending on the previous calculation. 2024.10.16
      //RefrigerantTemperature = InletAirTemperature; //Doing this causes a convergence error. 2023.04.11
      DefrostLoad = 0;
      WaterSupply = 0;
    }

    /// <summary>Computes the water consumption rate from water spray [kg/s].</summary>
    /// <param name="inletAirTemperature">Inlet air dry-bulb temperature [°C].</param>
    /// <param name="inletAirHumidityRatio">Inlet air humidity ratio [kg/kg].</param>
    /// <param name="sprayEffectiveness">Water spray temperature reduction effectiveness [-].</param>
    /// <param name="airFlowRate">Air mass flow rate [kg/s].</param>
    private static double GetWaterSupply
      (ref double inletAirTemperature, ref double inletAirHumidityRatio,
      double sprayEffectiveness, double airFlowRate)
    {
      double twb = MoistAir.GetWetBulbTemperatureFromDryBulbTemperatureAndHumidityRatio
          (inletAirTemperature, inletAirHumidityRatio, PhysicsConstants.StandardAtmosphericPressure);
      double ts = MoistAir.GetDryBulbTemperatureFromWetBulbTemperatureAndRelativeHumidity
        (twb, 100, PhysicsConstants.StandardAtmosphericPressure);
      double ws = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndRelativeHumidity
        (ts, 100, PhysicsConstants.StandardAtmosphericPressure);
      inletAirTemperature -= sprayEffectiveness * (inletAirTemperature - ts);
      inletAirHumidityRatio += sprayEffectiveness * (ws - inletAirHumidityRatio);
      return airFlowRate * sprayEffectiveness * (ws - inletAirHumidityRatio);
    }

    #endregion

    #region Free-run state calculation for a given refrigerant temperature

    /// <summary>Updates the unit state based on the refrigerant temperature.</summary>
    /// <param name="refrigerantTemperature">Refrigerant temperature [°C].</param>
    /// <param name="controlOutletAirState">True to control outlet air state using thermo-off time ratio.</param>
    public void UpdateWithRefrigerantTemperature
      (double refrigerantTemperature, bool controlOutletAirState)
    {
      UpdateWithRefrigerantTemperature
        (refrigerantTemperature, AirFlowRate, InletAirTemperature, InletAirHumidityRatio, controlOutletAirState, true);
    }

    /// <summary>Updates the unit state based on the refrigerant temperature.</summary>
    /// <param name="refrigerantTemperature">Refrigerant temperature [°C].</param>
    /// <param name="controlOutletAirState">True to control outlet air state using thermo-off time ratio.</param>
    /// <param name="controlThermoOffWithSensibleHeat">True to control thermo-off time ratio based on sensible heat.</param>
    public void UpdateWithRefrigerantTemperature
      (double refrigerantTemperature, bool controlOutletAirState, bool controlThermoOffWithSensibleHeat)
    {
      UpdateWithRefrigerantTemperature
        (refrigerantTemperature, AirFlowRate, InletAirTemperature, InletAirHumidityRatio, controlOutletAirState, controlThermoOffWithSensibleHeat);
    }

    /// <summary>Updates the unit state based on the refrigerant temperature.</summary>
    /// <param name="refrigerantTemperature">Refrigerant temperature [°C].</param>
    /// <param name="airFlowRate">Air mass flow rate [kg/s].</param>
    /// <param name="inletAirTemperature">Inlet air dry-bulb temperature [°C].</param>
    /// <param name="inletAirHumidityRatio">Inlet air humidity ratio [kg/kg].</param>
    /// <param name="controlOutletAirState">True to control outlet air state using thermo-off time ratio.</param>
    public void UpdateWithRefrigerantTemperature
      (double refrigerantTemperature, double airFlowRate,
      double inletAirTemperature, double inletAirHumidityRatio, bool controlOutletAirState)
    {
      UpdateWithRefrigerantTemperature
        (refrigerantTemperature, airFlowRate, inletAirTemperature, inletAirHumidityRatio, controlOutletAirState, true);
    }

    /// <summary>Updates the unit state based on the refrigerant temperature.</summary>
    /// <param name="refrigerantTemperature">Refrigerant temperature [°C].</param>
    /// <param name="airFlowRate">Air mass flow rate [kg/s].</param>
    /// <param name="inletAirTemperature">Inlet air dry-bulb temperature [°C].</param>
    /// <param name="inletAirHumidityRatio">Inlet air humidity ratio [kg/kg].</param>
    /// <param name="controlOutletAirState">True to control outlet air state using thermo-off time ratio.</param>
    /// <param name="controlThermoOffWithSensibleHeat">True to control thermo-off time ratio based on sensible heat.</param>
    public void UpdateWithRefrigerantTemperature
      (double refrigerantTemperature, double airFlowRate,
      double inletAirTemperature, double inletAirHumidityRatio, bool controlOutletAirState, bool controlThermoOffWithSensibleHeat)
    {
      if (airFlowRate < 0) throw new PopoloOutOfRangeException(
        nameof(airFlowRate), airFlowRate, 0, null, "Air flow rate must be non-negative.");

      //Set properties
      FanOperatingRatio = 1.0;
      RefrigerantTemperature = refrigerantTemperature;
      AirFlowRate = airFlowRate;
      InletAirTemperature = inletAirTemperature;
      InletAirHumidityRatio = inletAirHumidityRatio;

      switch (CurrentMode)
      {
        //Shut-off
        case Mode.ShutOff:
          ThermoOff();
          break;

        //Thermo-off
        case Mode.ThermoOff:
          ThermoOff();
          break;

        //Cooling operation (evaporator)
        case Mode.Cooling:
          if ((SelectableMode & Mode.Cooling) == 0 ||
            inletAirTemperature <= refrigerantTemperature ||
            airFlowRate == 0)
          {
            ThermoOff();
            return;
          }
          GetEvaporatorHeatTransfer(
            refrigerantTemperature, airFlowRate, EvaporatorSurfaceArea, inletAirTemperature,
            inletAirHumidityRatio, borderRelativeHumidity,
            out double ht, out double to, out double wo, out double sd, out double sw, out double dfl);
          OutletAirTemperature = to;
          OutletAirHumidityRatio = wo;
          DrySurfaceArea = sd;
          WetSurfaceArea = sw;
          HeatTransfer = ht;
          DefrostLoad = dfl;
          WaterSupply = 0;

          //Case: outlet air temperature is controlled (adjusted by thermo on/off cycling)
          if (controlOutletAirState)
          {
            double tRate;
            //Control based on sensible heat
            if (controlThermoOffWithSensibleHeat)
              tRate = 1.0 - (InletAirTemperature - OutletAirSetpointTemperature) / (InletAirTemperature - OutletAirTemperature);
            //Control based on total heat (uncommon)
            else
            {
              double inletH = MoistAir.GetEnthalpyFromDryBulbTemperatureAndHumidityRatio(InletAirTemperature, InletAirHumidityRatio);
              double outletH_SP = MoistAir.GetEnthalpyFromDryBulbTemperatureAndHumidityRatio(OutletAirSetpointTemperature, OutletAirSetpointHumidityRatio);
              double outletH = MoistAir.GetEnthalpyFromDryBulbTemperatureAndHumidityRatio(OutletAirTemperature, OutletAirHumidityRatio);
              tRate = 1.0 - (inletH - outletH_SP) / (inletH - outletH);
            }

            ThermoOffTimeRatio = Math.Max(0.0, Math.Min(1.0, tRate));
            //Case: 100% thermo-off
            if (ThermoOffTimeRatio == 1.0) ThermoOff();
            else if (0.0 < ThermoOffTimeRatio) //Adjust with the thermo-off time
            {
              OutletAirTemperature = OutletAirSetpointTemperature;
              OutletAirHumidityRatio = ThermoOffTimeRatio * InletAirHumidityRatio + (1 - ThermoOffTimeRatio) * OutletAirHumidityRatio;
              HeatTransfer *= (1 - ThermoOffTimeRatio);
              DefrostLoad *= (1 - ThermoOffTimeRatio); //This handling may actually be unnecessary.
            }
          }

          break;

        //Heating operation (condenser)
        case Mode.Heating:
          if ((SelectableMode & Mode.Heating) == 0 ||
            (!UseWaterSpray && refrigerantTemperature <= InletAirTemperature) || //Case: refrigerant temperature is too low without water spray
            airFlowRate == 0)
          {
            ThermoOff();
            return;
          }

          double sp = UseWaterSpray ? sprayEffectiveness : 0;
          GetCondenserHeatTransfer(
            refrigerantTemperature, airFlowRate, NominalAirFlowRate, CondenserSurfaceArea,
            inletAirTemperature, inletAirHumidityRatio, sp,
            out double ht2, out double to2, out double wo2, out double ws);
          OutletAirTemperature = to2;
          OutletAirHumidityRatio = wo2;
          HeatTransfer = ht2;
          WaterSupply = ws;
          DrySurfaceArea = EvaporatorSurfaceArea; //Intentionally set to the evaporator surface area here
          WetSurfaceArea = 0;
          DefrostLoad = 0;

          //Case: outlet air temperature is controlled (adjusted by thermo on/off cycling)
          if (controlOutletAirState)
          {
            //When humidifying, change the target outlet temperature
            double newTO = OutletAirSetpointTemperature;
            if (UseHumidifier && InletAirHumidityRatio < OutletAirSetpointHumidityRatio)
            {
              double wbt = MoistAir.GetWetBulbTemperatureFromDryBulbTemperatureAndHumidityRatio
                (OutletAirSetpointTemperature, OutletAirSetpointHumidityRatio, PhysicsConstants.StandardAtmosphericPressure);
              newTO = MoistAir.GetDryBulbTemperatureFromHumidityRatioAndWetBulbTemperature
                (InletAirHumidityRatio, wbt, PhysicsConstants.StandardAtmosphericPressure);
            }

            double tRate = 1.0 - (InletAirTemperature - newTO) / (InletAirTemperature - OutletAirTemperature);
            ThermoOffTimeRatio = Math.Max(0.0, Math.Min(1.0, tRate));
            //Case: 100% thermo-off
            if (ThermoOffTimeRatio == 1.0) ThermoOff();
            else if (0.0 < ThermoOffTimeRatio) //Adjust with the thermo-off time
            {
              //With humidification
              if (UseHumidifier && InletAirHumidityRatio < OutletAirSetpointHumidityRatio)
              {
                OutletAirTemperature = OutletAirSetpointTemperature;
                OutletAirHumidityRatio = OutletAirSetpointHumidityRatio;
              }
              //Without humidification
              else
              {
                OutletAirTemperature = newTO;
                OutletAirHumidityRatio = ThermoOffTimeRatio * InletAirHumidityRatio + (1 - ThermoOffTimeRatio) * OutletAirHumidityRatio;
              }
              HeatTransfer *= (1 - ThermoOffTimeRatio);
            }
            //Under overload with humidification, the humidity ratio setpoint is not reached
            else if (UseHumidifier && InletAirHumidityRatio < OutletAirSetpointHumidityRatio)
            {
              OutletAirTemperature = Math.Min(OutletAirTemperature, OutletAirSetpointTemperature);
              if (OutletAirSetpointTemperature <= OutletAirTemperature)
              {
                double wbt = MoistAir.GetWetBulbTemperatureFromDryBulbTemperatureAndHumidityRatio
                  (OutletAirTemperature, OutletAirHumidityRatio, PhysicsConstants.StandardAtmosphericPressure);
                OutletAirHumidityRatio = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndWetBulbTemperature
                  (OutletAirTemperature, wbt, PhysicsConstants.StandardAtmosphericPressure);
              }
            }
          }

          break;

      }
    }

    /// <summary>Computes the heat transfer rate [kW] (negative = heating, positive = cooling).</summary>
    /// <param name="evpTemperature">Evaporating temperature [°C].</param>
    /// <param name="airFlowRate">Air mass flow rate [kg/s].</param>
    /// <param name="surfaceArea">Heat transfer surface area [m²].</param>
    /// <param name="inletAirTemperature">Inlet air dry-bulb temperature [°C].</param>
    /// <param name="inletAirHumidityRatio">Inlet air humidity ratio [kg/kg].</param>
    /// <param name="borderRelativeHumidity">Relative humidity at the dry/wet boundary [%].</param>
    /// <param name="heatTransfer">Output: heat transfer [kW] (negative = heating, positive = cooling).</param>
    /// <param name="outletAirTemperature">Output: outlet air dry-bulb temperature [°C].</param>
    /// <param name="outletAirHumidityRatio">Output: outlet air humidity ratio [kg/kg].</param>
    /// <param name="sD">Output: dry coil surface area [m²].</param>
    /// <param name="sW">Output: wet coil surface area [m²].</param>
    /// <param name="defrostLoad">Output: defrost load [kW].</param>
    public static void GetEvaporatorHeatTransfer(
      double evpTemperature, double airFlowRate, double surfaceArea,
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
      double epsilonD = qD / (mca * (inletAirTemperature - evpTemperature));
      if (epsilonD <= 1) sD = -Math.Log(1 - epsilonD) * mca / HeatTransferCoefficient;
      else sD = surfaceArea;

      double hWB = MoistAir.GetEnthalpyFromDryBulbTemperatureAndHumidityRatio(tWB, inletAirHumidityRatio);
      double hEvp =
        MoistAir.GetEnthalpyFromDryBulbTemperatureAndRelativeHumidity(evpTemperature, 100, PhysicsConstants.StandardAtmosphericPressure);

      //Case: heat transfer completes within the dry coil alone
      if (surfaceArea <= sD || 1 <= epsilonD || hWB < hEvp)
      {
        sD = surfaceArea;
        sW = 0;
        defrostLoad = 0;
        outletAirHumidityRatio = inletAirHumidityRatio;

        epsilonD = 1 - Math.Exp(-HeatTransferCoefficient * sD / mca);
        qD = epsilonD * mca * (inletAirTemperature - evpTemperature);
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
        double kW = HeatTransferCoefficient / (0.5 * (cpmaWB + cpmaFB));
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
      double kF = HeatTransferCoefficient / cpmaFB * F_PENALTY;
      double hdFB = MoistAir.GetEnthalpyFromDryBulbTemperatureAndRelativeHumidity
        (tFB, borderRelativeHumidity, PhysicsConstants.StandardAtmosphericPressure);
      double hdEvp = MoistAir.GetEnthalpyFromDryBulbTemperatureAndRelativeHumidity
        (evpTemperature, 100, PhysicsConstants.StandardAtmosphericPressure);
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

    /// <summary>Computes the heat transfer rate [kW] (positive = heating, negative = cooling).</summary>
    /// <param name="cndTemperature">Condensing temperature [°C].</param>
    /// <param name="airFlowRate">Air mass flow rate [kg/s].</param>
    /// <param name="nominalAirFlowRate">Nominal air mass flow rate [kg/s].</param>
    /// <param name="surfaceArea">Heat transfer surface area [m²].</param>
    /// <param name="inletAirTemperature">Inlet air dry-bulb temperature [°C].</param>
    /// <param name="inletAirHumidityRatio">Inlet air humidity ratio [kg/kg].</param>
    /// <param name="sprayEffectiveness">Water spray temperature reduction effectiveness [-].</param>
    /// <param name="heatTransfer">Output: heat transfer rate [kW].</param>
    /// <param name="outletAirTemperature">Output: outlet air dry-bulb temperature [°C].</param>
    /// <param name="outletAirHumidityRatio">Output: outlet air humidity ratio [kg/kg].</param>
    /// <param name="waterSupply">Output: water consumption rate [kg/s].</param>
    public static void GetCondenserHeatTransfer
      (double cndTemperature, double airFlowRate, double nominalAirFlowRate, double surfaceArea,
      double inletAirTemperature, double inletAirHumidityRatio, double sprayEffectiveness,
      out double heatTransfer, out double outletAirTemperature,
      out double outletAirHumidityRatio, out double waterSupply)
    {
      //With water spray
      if (0 < sprayEffectiveness)
        waterSupply = GetWaterSupply
         (ref inletAirTemperature, ref inletAirHumidityRatio, sprayEffectiveness, airFlowRate);
      //Without water spray
      else waterSupply = 0;

      //Moist air specific heat [kJ/kgK]
      double cpma = MoistAir.GetSpecificHeat(inletAirHumidityRatio);
      double mca = cpma * airFlowRate;

      double epsilon = 1 - Math.Exp(-HeatTransferCoefficient * surfaceArea / mca);
      double q = epsilon * mca * (cndTemperature - inletAirTemperature);
      outletAirTemperature = inletAirTemperature + q / mca;
      outletAirHumidityRatio = inletAirHumidityRatio;
      heatTransfer = q;
    }

    #endregion

    #region State update based on processed heat load

    /// <summary>Updates the unit state based on the specified heat transfer rate [kW].</summary>
    /// <param name="heatLoad">Heat load [kW] (positive = heating, negative = cooling).</param>
    /// <param name="airFlowRate">Air mass flow rate [kg/s].</param>
    /// <param name="inletAirTemperature">Inlet air dry-bulb temperature [°C].</param>
    /// <param name="inletAirHumidityRatio">Inlet air humidity ratio [kg/kg].</param>
    /// <param name="deductDefrostLoad">True to deduct the defrost load from the heat transfer.</param>
    public void SolveHeatLoad(
      double heatLoad, double airFlowRate,
      double inletAirTemperature, double inletAirHumidityRatio, bool deductDefrostLoad)
    {
      if (airFlowRate < 0) throw new PopoloOutOfRangeException(
        nameof(airFlowRate), airFlowRate, 0, null, "Air flow rate must be non-negative.");

      //Set properties
      FanOperatingRatio = 1.0;
      HeatTransfer = heatLoad;
      AirFlowRate = airFlowRate;
      InletAirTemperature = inletAirTemperature;
      InletAirHumidityRatio = inletAirHumidityRatio;

      switch (CurrentMode)
      {
        //Shut-off
        case Mode.ShutOff:
          ThermoOff();
          break;

        //Thermo-off
        case Mode.ThermoOff:
          ThermoOff();
          break;

        //Cooling operation (evaporator)
        case Mode.Cooling:
          if ((SelectableMode & Mode.Cooling) == 0 ||
            0 <= heatLoad || airFlowRate == 0)
          {
            ThermoOff();
            return;
          }

          GetEvaporatingTemperature(heatLoad, airFlowRate, EvaporatorSurfaceArea,
            inletAirTemperature, inletAirHumidityRatio, borderRelativeHumidity, deductDefrostLoad,
            out double te, out double to, out double wo, out double sd, out double sw, out double dfl);
          OutletAirTemperature = to;
          OutletAirHumidityRatio = wo;
          DrySurfaceArea = sd;
          WetSurfaceArea = sw;
          RefrigerantTemperature = te;
          DefrostLoad = dfl;
          WaterSupply = 0;
          break;

        //Heating operation (condenser)
        case Mode.Heating:
          if ((SelectableMode & Mode.Heating) == 0 ||
            heatLoad <= 0 || airFlowRate == 0)
          {
            ThermoOff();
            return;
          }

          GetCondensingTemperature(heatLoad, airFlowRate, CondenserSurfaceArea,
            inletAirTemperature, inletAirHumidityRatio, (UseWaterSpray ? sprayEffectiveness : 0),
            out double tc, out double to2, out double wo2, out double ws);
          OutletAirTemperature = to2;
          OutletAirHumidityRatio = wo2;
          RefrigerantTemperature = tc;
          WaterSupply = ws;
          DrySurfaceArea = EvaporatorSurfaceArea; //Intentionally set to the evaporator surface area here
          WetSurfaceArea = 0;
          DefrostLoad = 0;
          break;
      }
    }

    /// <summary>Computes the evaporating temperature [°C].</summary>
    /// <param name="heatTransfer">Heat transfer [kW] (negative = cooling).</param>
    /// <param name="airFlowRate">Air mass flow rate [kg/s].</param>
    /// <param name="surfaceArea">Heat transfer surface area [m²].</param>
    /// <param name="inletAirTemperature">Inlet air dry-bulb temperature [°C].</param>
    /// <param name="inletAirHumidityRatio">Inlet air humidity ratio [kg/kg].</param>
    /// <param name="borderRelativeHumidity">Relative humidity at the dry/wet boundary [%].</param>
    /// <param name="deductDefrostLoad">True to deduct the defrost load from the heat transfer.</param>
    /// <param name="evaporatingTemperature">Output: evaporating temperature [°C].</param>
    /// <param name="outletAirTemperature">Output: outlet air dry-bulb temperature [°C].</param>
    /// <param name="outletAirHumidityRatio">Output: outlet air humidity ratio [kg/kg].</param>
    /// <param name="sD">Output: dry coil surface area [m²].</param>
    /// <param name="sW">Output: wet coil surface area [m²].</param>
    /// <param name="defrostLoad">Output: defrost load [kW].</param>
    public static void GetEvaporatingTemperature(
      double heatTransfer, double airFlowRate,
      double surfaceArea, double inletAirTemperature, double inletAirHumidityRatio, double borderRelativeHumidity,
      bool deductDefrostLoad, out double evaporatingTemperature, out double outletAirTemperature,
      out double outletAirHumidityRatio, out double sD, out double sW, out double defrostLoad)
    {
      //Initial guess for the evaporating temperature
      evaporatingTemperature = inletAirTemperature + heatTransfer / (airFlowRate * 1.006);

      Roots.ErrorFunction eFnc = delegate (double eTemp)
      {
        double ht, ot, oa, sd, sw, dl;
        GetEvaporatorHeatTransfer(eTemp, airFlowRate, surfaceArea, inletAirTemperature,
          inletAirHumidityRatio, borderRelativeHumidity, out ht, out ot, out oa, out sd, out sw, out dl);
        if (deductDefrostLoad) return ht - heatTransfer - dl;
        else return ht - heatTransfer;
      };
      try
      {
        evaporatingTemperature = Roots.Brent(evaporatingTemperature - 20, evaporatingTemperature + 5, 0.00001, eFnc);
      }
      catch (Exception ex)
      {
        throw new PopoloNumericalException(
          "UpdateWithRefrigerantTemperature",
          $"Brent solver failed to find evaporating temperature. "
          + $"Tair={inletAirTemperature:F2}°C, hHeat={heatTransfer:F3} kW, surface={surfaceArea:F4} m². "
          + ex.Message, ex);
      }
      double hTransfer;
      GetEvaporatorHeatTransfer(evaporatingTemperature, airFlowRate, surfaceArea,
        inletAirTemperature, inletAirHumidityRatio, borderRelativeHumidity,
        out hTransfer, out outletAirTemperature, out outletAirHumidityRatio, out sD, out sW, out defrostLoad);
    }

    /// <summary>Computes the condensing temperature [°C].</summary>
    /// <param name="heatTransfer">Heat transfer [kW] (positive = heating).</param>
    /// <param name="airFlowRate">Air mass flow rate [kg/s].</param>
    /// <param name="surfaceArea">Heat transfer surface area [m²].</param>
    /// <param name="inletAirTemperature">Inlet air dry-bulb temperature [°C].</param>
    /// <param name="inletAirHumidityRatio">Inlet air humidity ratio [kg/kg].</param>
    /// <param name="sprayEffectiveness">Water spray temperature reduction effectiveness [-].</param>
    /// <param name="condensingTemperature">Output: condensing temperature [°C].</param>
    /// <param name="outletAirTemperature">Output: outlet air dry-bulb temperature [°C].</param>
    /// <param name="outletAirHumidityRatio">Output: outlet air humidity ratio [kg/kg].</param>
    /// <param name="waterSupply">Output: water consumption rate [kg/s].</param>
    public static void GetCondensingTemperature(
      double heatTransfer, double airFlowRate,
      double surfaceArea, double inletAirTemperature, double inletAirHumidityRatio,
      double sprayEffectiveness, out double condensingTemperature,
      out double outletAirTemperature, out double outletAirHumidityRatio,
      out double waterSupply)
    {
      //With water spray
      if (0 < sprayEffectiveness)
        waterSupply = GetWaterSupply
         (ref inletAirTemperature, ref inletAirHumidityRatio, sprayEffectiveness, airFlowRate);
      //Without water spray
      else waterSupply = 0;

      //Moist air specific heat [kJ/kgK]
      double cpma = MoistAir.GetSpecificHeat(inletAirHumidityRatio);
      double mca = cpma * airFlowRate;

      outletAirTemperature = inletAirTemperature + heatTransfer / mca;
      outletAirHumidityRatio = inletAirHumidityRatio;
      double epsilon = 1 - Math.Exp(-HeatTransferCoefficient * surfaceArea / mca);
      condensingTemperature = inletAirTemperature + heatTransfer / (epsilon * mca);
    }

    #endregion

    #region State update based on supply air temperature

    /// <summary>Controls the supply air dry-bulb temperature using the refrigerant temperature.</summary>
    /// <param name="airFlowRate">Air mass flow rate [kg/s].</param>
    /// <param name="inletAirTemperature">Inlet air dry-bulb temperature [°C].</param>
    /// <param name="inletAirHumidityRatio">Inlet air humidity ratio [kg/kg].</param>
    public void ControlOutletAirTemperatureWithRefrigerantTemperature(
      double airFlowRate, double inletAirTemperature, double inletAirHumidityRatio)
    {
      if (airFlowRate < 0) throw new PopoloOutOfRangeException(
        nameof(airFlowRate), airFlowRate, 0, null, "Air flow rate must be non-negative.");

      //Set properties
      FanOperatingRatio = 1.0;
      OutletAirTemperature = OutletAirSetpointTemperature;
      AirFlowRate = airFlowRate;
      InletAirTemperature = inletAirTemperature;
      InletAirHumidityRatio = inletAirHumidityRatio;

      switch (CurrentMode)
      {
        //Shut-off
        case Mode.ShutOff:
          ThermoOff();
          break;

        //Thermo-off
        case Mode.ThermoOff:
          ThermoOff();
          break;

        //Cooling operation (evaporator)
        case Mode.Cooling:
          if ((SelectableMode & Mode.Cooling) == 0 ||
            AirFlowRate == 0 ||
            InletAirTemperature < OutletAirSetpointTemperature ||
            OutletAirSetpointTemperature == InletAirTemperature)
          {
            ThermoOff();
            return;
          }

          ControlOutletAirTemperature(
            OutletAirTemperature, airFlowRate, EvaporatorSurfaceArea,
            inletAirTemperature, inletAirHumidityRatio, borderRelativeHumidity,
            out double te, out double ht, out double wo, out double sd, out double sw, out double dfl);
          HeatTransfer = ht;
          OutletAirHumidityRatio = wo;
          DrySurfaceArea = sd;
          WetSurfaceArea = sw;
          RefrigerantTemperature = te;
          DefrostLoad = dfl;
          WaterSupply = 0;
          break;

        //Heating operation (condenser)
        case Mode.Heating:
          if ((SelectableMode & Mode.Heating) == 0 ||
            AirFlowRate == 0 ||
            OutletAirSetpointTemperature < InletAirTemperature ||
            OutletAirSetpointTemperature == InletAirTemperature)
          {
            ThermoOff();
            return;
          }

          //When humidifying, change the target outlet temperature
          double newTO = OutletAirSetpointTemperature;
          if (UseHumidifier && InletAirHumidityRatio < OutletAirSetpointHumidityRatio)
          {
            double wbt = MoistAir.GetWetBulbTemperatureFromDryBulbTemperatureAndHumidityRatio
              (OutletAirSetpointTemperature, OutletAirSetpointHumidityRatio, PhysicsConstants.StandardAtmosphericPressure);
            newTO = MoistAir.GetDryBulbTemperatureFromHumidityRatioAndWetBulbTemperature
              (InletAirHumidityRatio, wbt, PhysicsConstants.StandardAtmosphericPressure);
          }

          ControlOutletAirTemperature(
            newTO, airFlowRate, CondenserSurfaceArea,
            inletAirTemperature, inletAirHumidityRatio, (UseWaterSpray ? sprayEffectiveness : 0),
            out double tc, out double ht2, out double wo2, out double ws);
          HeatTransfer = ht2;
          if (UseHumidifier && InletAirHumidityRatio < OutletAirSetpointHumidityRatio)
            OutletAirHumidityRatio = OutletAirSetpointHumidityRatio;
          else OutletAirHumidityRatio = wo2;
          RefrigerantTemperature = tc;
          WaterSupply = ws;
          DrySurfaceArea = EvaporatorSurfaceArea; //Intentionally set to the evaporator surface area here
          WetSurfaceArea = 0;
          DefrostLoad = 0;
          break;
      }
    }

    /// <summary>Controls the supply air temperature.</summary>
    /// <param name="outletAirSetpointTemperature">Supply air dry-bulb temperature setpoint [°C].</param>
    /// <param name="airFlowRate">Air mass flow rate [kg/s].</param>
    /// <param name="surfaceArea">Heat transfer surface area [m²].</param>
    /// <param name="inletAirTemperature">Inlet air dry-bulb temperature [°C].</param>
    /// <param name="inletAirHumidityRatio">Inlet air humidity ratio [kg/kg].</param>
    /// <param name="borderRelativeHumidity">Relative humidity at the dry/wet boundary [%].</param>
    /// <param name="evaporatingTemperature">Output: evaporating temperature [°C].</param>
    /// <param name="heatTransfer">Output: heat transfer [kW] (negative = cooling, positive = heating).</param>
    /// <param name="outletAirHumidityRatio">Output: outlet air humidity ratio [kg/kg].</param>
    /// <param name="sD">Output: dry coil surface area [m²].</param>
    /// <param name="sW">Output: wet coil surface area [m²].</param>
    /// <param name="defrostLoad">Output: defrost load [kW].</param>
    public static void ControlOutletAirTemperature(
      double outletAirSetpointTemperature, double airFlowRate,
      double surfaceArea, double inletAirTemperature, double inletAirHumidityRatio, double borderRelativeHumidity,
      out double evaporatingTemperature, out double heatTransfer,
      out double outletAirHumidityRatio, out double sD, out double sW, out double defrostLoad)
    {
      //Initial guess for the evaporating temperature
      evaporatingTemperature = outletAirSetpointTemperature;

      Roots.ErrorFunction eFnc = delegate (double eTemp)
      {
        GetEvaporatorHeatTransfer(eTemp, airFlowRate, surfaceArea, inletAirTemperature,
          inletAirHumidityRatio, borderRelativeHumidity,
          out _, out double ot, out _, out _, out _, out _);
        return ot - outletAirSetpointTemperature;
      };
      try
      {
        evaporatingTemperature = Roots.Brent(evaporatingTemperature - 20, evaporatingTemperature, 0.00001, eFnc);
      }
      catch (Exception ex)
      {
        throw new PopoloNumericalException(
          "ControlOutletAirTemperature",
          $"Brent solver failed to find evaporating temperature for setpoint control. "
          + $"Tair={inletAirTemperature:F2}°C, Tsp={outletAirSetpointTemperature:F2}°C, surface={surfaceArea:F4} m². "
          + ex.Message, ex);
      }
      GetEvaporatorHeatTransfer(evaporatingTemperature, airFlowRate, surfaceArea,
        inletAirTemperature, inletAirHumidityRatio, borderRelativeHumidity,
        out heatTransfer, out _, out outletAirHumidityRatio, out sD, out sW, out defrostLoad);
    }

    /// <summary>Controls the supply air temperature [°C].</summary>
    /// <param name="outletAirSetpointTemperature">Supply air dry-bulb temperature setpoint [°C].</param>
    /// <param name="airFlowRate">Air mass flow rate [kg/s].</param>
    /// <param name="surfaceArea">Heat transfer surface area [m²].</param>
    /// <param name="inletAirTemperature">Inlet air dry-bulb temperature [°C].</param>
    /// <param name="inletAirHumidityRatio">Inlet air humidity ratio [kg/kg].</param>
    /// <param name="sprayEffectiveness">Water spray temperature reduction effectiveness [-].</param>
    /// <param name="condensingTemperature">Output: condensing temperature [°C].</param>
    /// <param name="heatTransfer">Output: heat transfer [kW] (negative = cooling, positive = heating).</param>
    /// <param name="outletAirHumidityRatio">Output: outlet air humidity ratio [kg/kg].</param>
    /// <param name="waterSupply">Output: water consumption rate [kg/s].</param>
    public static void ControlOutletAirTemperature(
      double outletAirSetpointTemperature, double airFlowRate,
      double surfaceArea, double inletAirTemperature, double inletAirHumidityRatio,
      double sprayEffectiveness, out double condensingTemperature,
      out double heatTransfer, out double outletAirHumidityRatio,
      out double waterSupply)
    {
      //With water spray
      if (0 < sprayEffectiveness)
        waterSupply = GetWaterSupply
         (ref inletAirTemperature, ref inletAirHumidityRatio, sprayEffectiveness, airFlowRate);
      //Without water spray
      else waterSupply = 0;

      //Moist air specific heat [kJ/kgK]
      double cpma = MoistAir.GetSpecificHeat(inletAirHumidityRatio);
      double mca = cpma * airFlowRate;

      heatTransfer = (outletAirSetpointTemperature - inletAirTemperature) * mca;
      outletAirHumidityRatio = inletAirHumidityRatio;
      double epsilon = 1 - Math.Exp(-HeatTransferCoefficient * surfaceArea / mca);
      condensingTemperature = inletAirTemperature + heatTransfer / (epsilon * mca);
    }

    #endregion

  }
}