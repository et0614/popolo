/* HumidifierUnit.cs
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
using Popolo.Core.HVAC.FluidCircuit;
using Popolo.Core.Physics;

namespace Popolo.Core.HVAC.AirSide
{
  /// <summary>
  /// Humidifier unit comprising a fan and a humidifier.
  /// </summary>
  /// <remarks>
  /// <para>
  /// Models a packaged humidification unit that draws air with its own fan
  /// and humidifies it, used independently of an air handling unit.
  /// </para>
  /// <para>
  /// The fan is assumed to be upstream of the humidifier (blow-through):
  /// the inlet air is first heated by the fan energy input, then humidified.
  /// The higher dry-bulb temperature after the fan raises the saturation
  /// humidity ratio and therefore acts in favour of the humidification
  /// capacity.
  /// </para>
  /// </remarks>
  public class HumidifierUnit : IReadOnlyHumidifierUnit
  {

    #region インスタンス変数・プロパティ

    /// <summary>Fan.</summary>
    private readonly CentrifugalFan fan;

    /// <summary>Humidifier.</summary>
    private readonly Humidifier humidifier;

    /// <summary>Gets the fan.</summary>
    public IReadOnlyFluidMachinery Fan { get { return fan; } }

    /// <summary>Gets the humidifier.</summary>
    public IReadOnlyHumidifier Humidifier { get { return humidifier; } }

    /// <summary>Gets or sets the inlet air dry-bulb temperature [°C].</summary>
    public double InletAirTemperature { get; set; }

    /// <summary>Gets or sets the inlet air humidity ratio [kg/kg].</summary>
    public double InletAirHumidityRatio { get; set; }

    /// <inheritdoc />
    public double OutletAirTemperature { get; private set; }

    /// <inheritdoc />
    public double OutletAirHumidityRatio { get; private set; }

    /// <inheritdoc />
    public double AirFlowRate { get; private set; }

    /// <inheritdoc />
    public double WaterConsumption { get { return humidifier.WaterConsumption; } }

    /// <inheritdoc />
    public double SteamConsumption { get { return humidifier.SteamConsumption; } }

    #endregion

    #region コンストラクタ

    /// <summary>Initializes a new instance.</summary>
    /// <param name="humidifier">Humidifier.</param>
    /// <param name="fan">Fan.</param>
    public HumidifierUnit(Humidifier humidifier, CentrifugalFan fan)
    {
      if (humidifier == null)
        throw new PopoloArgumentException("humidifier must not be null.", nameof(humidifier));
      if (fan == null)
        throw new PopoloArgumentException("fan must not be null.", nameof(fan));

      this.humidifier = humidifier;
      this.fan = fan;
    }

    #endregion

    #region インスタンスメソッド

    /// <summary>Sets the air mass flow rate [kg/s].</summary>
    /// <param name="airFlowRate">Air mass flow rate [kg/s].</param>
    public void SetAirFlowRate(double airFlowRate)
    {
      AirFlowRate = Math.Max(0, airFlowRate);
    }

    /// <summary>
    /// Humidifies the air in free-running mode: the humidifier operates at
    /// its current <see cref="AirSide.Humidifier.SaturationEfficiency"/>.
    /// </summary>
    public void Humidify()
    {
      if (AirFlowRate <= 0)
      {
        ShutOff();
        return;
      }

      double tFo = ComputeFanOutletTemperature();
      humidifier.UpdateOutletState(tFo, InletAirHumidityRatio, AirFlowRate);
      OutletAirTemperature = humidifier.OutletAirTemperature;
      OutletAirHumidityRatio = humidifier.OutletAirHumidityRatio;
    }

    /// <summary>
    /// Humidifies the air so that the outlet humidity ratio reaches the
    /// setpoint.
    /// </summary>
    /// <param name="setpointHumidityRatio">Outlet air humidity ratio setpoint [kg/kg].</param>
    /// <returns>
    /// True if the setpoint was achieved; false when the setpoint exceeds
    /// the humidification capacity.
    /// </returns>
    public bool ControlOutletHumidityRatio(double setpointHumidityRatio)
    {
      if (AirFlowRate <= 0)
      {
        ShutOff();
        return false;
      }

      double tFo = ComputeFanOutletTemperature();
      bool suc = humidifier.ControlOutletHumidityRatio(
        tFo, InletAirHumidityRatio, AirFlowRate, setpointHumidityRatio);
      OutletAirTemperature = humidifier.OutletAirTemperature;
      OutletAirHumidityRatio = humidifier.OutletAirHumidityRatio;
      return suc;
    }

    /// <summary>Gets the electric consumption of the fan [kW].</summary>
    /// <returns>Electric consumption of the fan [kW].</returns>
    public double GetElectricConsumption()
    {
      return fan.GetElectricConsumption();
    }

    /// <summary>Shuts off the unit (zero airflow, fan stopped, no humidification).</summary>
    public void ShutOff()
    {
      fan.ShutOff();
      humidifier.ShutOff();
      AirFlowRate = 0.0;
      OutletAirTemperature = InletAirTemperature;
      OutletAirHumidityRatio = InletAirHumidityRatio;
    }

    /// <summary>
    /// Updates the fan state and returns the fan outlet air temperature [°C]
    /// (inlet air temperature + temperature rise by the fan energy input).
    /// </summary>
    private double ComputeFanOutletTemperature()
    {
      fan.UpdateState(AirFlowRate / PhysicsConstants.NominalMoistAirDensity);
      double tRise = fan.GetElectricConsumption()
        / (AirFlowRate * PhysicsConstants.NominalMoistAirIsobaricSpecificHeat);
      return InletAirTemperature + tRise;
    }

    #endregion

  }
}
