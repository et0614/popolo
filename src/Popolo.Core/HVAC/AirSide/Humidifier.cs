/* Humidifier.cs
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

namespace Popolo.Core.HVAC.AirSide
{
  /// <summary>
  /// Air humidifier model based on saturation efficiency.
  /// </summary>
  /// <remarks>
  /// <para>
  /// The outlet humidity ratio is computed from the saturation efficiency
  /// η_hmS as <c>W_out = (1 − η) W_in + η W_s</c>, where <c>W_s</c> is the
  /// saturation humidity ratio of the process. For water humidification
  /// (wetted media, atomizing, ultrasonic) the process is adiabatic: the air
  /// state moves along a constant-enthalpy line and <c>W_s</c> is evaluated
  /// at the inlet enthalpy. For steam humidification the air state moves
  /// along a constant dry-bulb temperature line and <c>W_s</c> is evaluated
  /// at the inlet dry-bulb temperature.
  /// </para>
  /// <para>
  /// Not all of the supplied water is used effectively; the ratio of
  /// humidification mass to feed water mass is expressed by
  /// <see cref="WaterSupplyCoefficient"/> (η_hmW).
  /// </para>
  /// </remarks>
  public class Humidifier : IReadOnlyHumidifier
  {

    #region Enumeration definitions

    /// <summary>Humidification method.</summary>
    public enum HumidifierType
    {
      /// <summary>Steam humidification.</summary>
      Steam,
      /// <summary>Evaporative (drip) humidification.</summary>
      WettedMedia,
      /// <summary>Water spray humidification.</summary>
      Atomizing,
      /// <summary>Ultrasonic humidification.</summary>
      Ultrasonic,
    }

    #endregion

    #region Instance variables and properties

    /// <inheritdoc />
    public HumidifierType Type { get; }

    /// <inheritdoc />
    public bool IsAdiabatic { get { return Type != HumidifierType.Steam; } }

    /// <inheritdoc />
    public double MaxSaturationEfficiency { get; set; }

    /// <inheritdoc />
    public double SaturationEfficiency { get; set; }

    /// <inheritdoc />
    public double WaterSupplyCoefficient { get; set; }

    /// <inheritdoc />
    public double InletAirTemperature { get; private set; }

    /// <inheritdoc />
    public double InletAirHumidityRatio { get; private set; }

    /// <inheritdoc />
    public double OutletAirTemperature { get; private set; }

    /// <inheritdoc />
    public double OutletAirHumidityRatio { get; private set; }

    /// <inheritdoc />
    public double AirFlowRate { get; private set; }

    /// <inheritdoc />
    public double WaterConsumption { get; private set; }

    /// <inheritdoc />
    public double SteamConsumption { get; private set; }

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new instance with typical maximum saturation efficiency
    /// and water supply coefficient for the specified humidification method.
    /// </summary>
    /// <param name="type">Humidification method.</param>
    /// <remarks>
    /// Defaults: steam = (1.0, 0.9), wetted media = (0.8, 0.5),
    /// ultrasonic = (0.5, 0.9), atomizing = (0.3, 0.4).
    /// </remarks>
    public Humidifier(HumidifierType type)
    {
      Type = type;
      switch (type)
      {
        case HumidifierType.Steam:
          MaxSaturationEfficiency = 1.0;
          WaterSupplyCoefficient = 0.9;
          break;
        case HumidifierType.WettedMedia:
          MaxSaturationEfficiency = 0.8;
          WaterSupplyCoefficient = 0.5;
          break;
        case HumidifierType.Ultrasonic:
          MaxSaturationEfficiency = 0.5;
          WaterSupplyCoefficient = 0.9;
          break;
        case HumidifierType.Atomizing:
          MaxSaturationEfficiency = 0.3;
          WaterSupplyCoefficient = 0.4;
          break;
        default:
          throw new PopoloNotImplementedException(
            $"Humidifier type '{type}' is not supported.");
      }
    }

    /// <summary>
    /// Initializes a new instance with explicit maximum saturation efficiency
    /// and water supply coefficient.
    /// </summary>
    /// <param name="type">Humidification method.</param>
    /// <param name="maxSaturationEfficiency">Maximum saturation efficiency [-].</param>
    /// <param name="waterSupplyCoefficient">Effective water use ratio of the supplied water [-].</param>
    public Humidifier(HumidifierType type,
      double maxSaturationEfficiency, double waterSupplyCoefficient)
    {
      if (maxSaturationEfficiency <= 0 || 1 < maxSaturationEfficiency)
        throw new PopoloArgumentException(
          "maxSaturationEfficiency must be in (0, 1].", nameof(maxSaturationEfficiency));
      if (waterSupplyCoefficient <= 0 || 1 < waterSupplyCoefficient)
        throw new PopoloArgumentException(
          "waterSupplyCoefficient must be in (0, 1].", nameof(waterSupplyCoefficient));

      Type = type;
      MaxSaturationEfficiency = maxSaturationEfficiency;
      WaterSupplyCoefficient = waterSupplyCoefficient;
    }

    #endregion

    #region Instance methods

    /// <summary>
    /// Computes the outlet air state with the current
    /// <see cref="SaturationEfficiency"/> (free-running operation).
    /// </summary>
    /// <param name="inletAirTemperature">Inlet air dry-bulb temperature [°C].</param>
    /// <param name="inletAirHumidityRatio">Inlet air humidity ratio [kg/kg].</param>
    /// <param name="airFlowRate">Air mass flow rate [kg/s].</param>
    public void UpdateOutletState(
      double inletAirTemperature, double inletAirHumidityRatio, double airFlowRate)
    {
      if (airFlowRate <= 0)
      {
        ShutOff();
        return;
      }

      double eff = Math.Min(Math.Max(0, SaturationEfficiency), MaxSaturationEfficiency);
      ComputeOutletState(inletAirTemperature, inletAirHumidityRatio, airFlowRate, eff);
    }

    /// <summary>
    /// Adjusts the saturation efficiency so that the outlet humidity ratio
    /// reaches the setpoint, then computes the outlet air state.
    /// </summary>
    /// <param name="inletAirTemperature">Inlet air dry-bulb temperature [°C].</param>
    /// <param name="inletAirHumidityRatio">Inlet air humidity ratio [kg/kg].</param>
    /// <param name="airFlowRate">Air mass flow rate [kg/s].</param>
    /// <param name="setpointHumidityRatio">Outlet air humidity ratio setpoint [kg/kg].</param>
    /// <returns>
    /// True if the setpoint was achieved; false when the setpoint exceeds the
    /// humidification capacity limited by <see cref="MaxSaturationEfficiency"/>.
    /// </returns>
    public bool ControlOutletHumidityRatio(
      double inletAirTemperature, double inletAirHumidityRatio,
      double airFlowRate, double setpointHumidityRatio)
    {
      if (airFlowRate <= 0)
      {
        ShutOff();
        return false;
      }

      double satW = GetProcessSaturationHumidityRatio(inletAirTemperature, inletAirHumidityRatio);

      //Back-calculate the required saturation efficiency (0 when no humidification is needed or the air is saturated)
      double eff = 0.0;
      if (inletAirHumidityRatio < setpointHumidityRatio && inletAirHumidityRatio < satW)
        eff = (setpointHumidityRatio - inletAirHumidityRatio) / (satW - inletAirHumidityRatio);
      SaturationEfficiency = Math.Min(eff, MaxSaturationEfficiency);

      ComputeOutletState(inletAirTemperature, inletAirHumidityRatio, airFlowRate, SaturationEfficiency, satW);
      return eff <= MaxSaturationEfficiency;
    }

    /// <summary>Shuts off the humidifier (zero airflow, no humidification).</summary>
    public void ShutOff()
    {
      AirFlowRate = 0.0;
      SaturationEfficiency = 0.0;
      WaterConsumption = 0.0;
      SteamConsumption = 0.0;
      OutletAirTemperature = InletAirTemperature;
      OutletAirHumidityRatio = InletAirHumidityRatio;
    }

    /// <summary>
    /// Returns the saturation humidity ratio [kg/kg] of the humidification
    /// process: at constant enthalpy for water humidification, at constant
    /// dry-bulb temperature for steam humidification.
    /// </summary>
    private double GetProcessSaturationHumidityRatio(
      double inletAirTemperature, double inletAirHumidityRatio)
    {
      if (IsAdiabatic)
      {
        double hIn = MoistAir.GetEnthalpyFromDryBulbTemperatureAndHumidityRatio(
          inletAirTemperature, inletAirHumidityRatio);
        return MoistAir.GetSaturationHumidityRatioFromEnthalpy(
          hIn, PhysicsConstants.StandardAtmosphericPressure);
      }
      else
      {
        return MoistAir.GetSaturationHumidityRatioFromDryBulbTemperature(
          inletAirTemperature, PhysicsConstants.StandardAtmosphericPressure);
      }
    }

    /// <summary>Computes and stores the outlet state for the given saturation efficiency.</summary>
    private void ComputeOutletState(
      double inletAirTemperature, double inletAirHumidityRatio,
      double airFlowRate, double saturationEfficiency, double? processSatW = null)
    {
      InletAirTemperature = inletAirTemperature;
      InletAirHumidityRatio = inletAirHumidityRatio;
      AirFlowRate = airFlowRate;

      double satW = processSatW ??
        GetProcessSaturationHumidityRatio(inletAirTemperature, inletAirHumidityRatio);

      //Equation 26.5: W_out = (1 - η)W_in + ηW_s
      double wOut = Math.Max(inletAirHumidityRatio,
        (1 - saturationEfficiency) * inletAirHumidityRatio + saturationEfficiency * satW);
      OutletAirHumidityRatio = wOut;

      //Water humidification moves along a constant specific enthalpy line; steam humidification along a constant dry-bulb temperature line
      if (IsAdiabatic)
      {
        double hIn = MoistAir.GetEnthalpyFromDryBulbTemperatureAndHumidityRatio(
          inletAirTemperature, inletAirHumidityRatio);
        OutletAirTemperature =
          MoistAir.GetDryBulbTemperatureFromHumidityRatioAndEnthalpy(wOut, hIn);
      }
      else OutletAirTemperature = inletAirTemperature;

      //Water supply and steam consumption (Equation 26.6)
      double supply = (wOut - inletAirHumidityRatio) * airFlowRate / WaterSupplyCoefficient;
      if (IsAdiabatic)
      {
        WaterConsumption = supply;
        SteamConsumption = 0.0;
      }
      else
      {
        WaterConsumption = 0.0;
        SteamConsumption = supply;
      }
    }

    #endregion

  }
}
