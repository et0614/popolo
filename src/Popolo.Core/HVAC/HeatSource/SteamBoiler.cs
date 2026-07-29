/* SteamBoiler.cs
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

namespace Popolo.Core.HVAC.HeatSource
{
  /// <summary>Steam boiler.</summary>
  public class SteamBoiler
  {

    #region Properties and instance variables

    /// <summary>Excess air ratio [-].</summary>
    private double airRatio = 1.1;

    /// <summary>Heat loss coefficient [kW/K].</summary>
    private double heatLossCoefficient = 0.0;

    /// <summary>Nominal steam temperature [°C].</summary>
    private double nominalSteamTemperature;

    /// <summary>Nominal flue gas temperature [°C].</summary>
    private double nominalSmokeTemperature;

    /// <summary>Gets or sets the primary energy conversion factor for electricity [MJ/kWh].</summary>
    public double PrimaryEnergyFactor { get; set; } = 9.76;

    /// <summary>Gets the fuel type.</summary>
    public Boiler.Fuel Fuel { get; private set; }

    /// <summary>Gets or sets the steam pressure [kPa].</summary>
    public double SteamPressure { get; set; }

    /// <summary>Gets the steam temperature [°C] corresponding to the current steam pressure.</summary>
    public double SteamTemperature
    { get { return Water.GetSaturationTemperature(SteamPressure); } }

    /// <summary>Gets the feed water inlet temperature [°C].</summary>
    public double InletWaterTemperature { get; private set; }

    /// <summary>Gets the current steam flow rate [kg/s].</summary>
    public double SteamFlowRate { get; private set; }

    /// <summary>Gets the nominal heating capacity [kW].</summary>
    public double NominalCapacity { get; private set; }

    /// <summary>Gets the nominal fuel consumption rate [kg/s or Nm³/s].</summary>
    public double NominalFuelConsumption { get; private set; }

    /// <summary>Gets the electric power consumption [kW].</summary>
    public double ElectricConsumption { get; private set; }

    /// <summary>Gets the current fuel consumption rate [kg/s or Nm³/s].</summary>
    public double FuelConsumption { get; private set; }

    /// <summary>Gets the current heat output [kW].</summary>
    public double HeatLoad { get; private set; }

    /// <summary>Gets or sets the ambient temperature [°C].</summary>
    public double AmbientTemperature { get; set; }

    /// <summary>Gets or sets the excess air ratio [-] (clamped to 1.0–1.5).</summary>
    public double AirRatio
    {
      get { return airRatio; }
      set { airRatio = Math.Max(1.0, Math.Min(1.5, value)); }
    }

    /// <summary>Gets the coefficient of performance (primary energy basis) [-].</summary>
    public double COP
    {
      get
      {
        if (HeatLoad == 0) return 0;
        else return 0.001 * HeatLoad / (ElectricConsumption * PrimaryEnergyFactor
            + FuelConsumption * Boiler.GetHeatingValue(Fuel, true));
      }
    }

    #endregion

    #region Constructors

    /// <summary>Initializes a new instance from rated operating conditions.</summary>
    /// <param name="inletWaterTemperature">Feed water inlet temperature [°C].</param>
    /// <param name="steamPressure">Steam pressure [kPa].</param>
    /// <param name="steamFlowRate">Steam flow rate [kg/s].</param>
    /// <param name="fuelConsumption">Fuel consumption rate [kg/s or Nm³/s].</param>
    /// <param name="electricConsumption">Electric power consumption [kW].</param>
    /// <param name="ambientTemperature">Ambient temperature [°C].</param>
    /// <param name="airRatio">Excess air ratio [-].</param>
    /// <param name="fuel">Fuel type.</param>
    /// <param name="smokeTemperature">Flue gas temperature [°C].</param>
    public SteamBoiler(double inletWaterTemperature, double steamPressure, double steamFlowRate,
      double fuelConsumption, double electricConsumption, double ambientTemperature, double airRatio,
      Boiler.Fuel fuel, double smokeTemperature)
    {
      //Store properties
      Fuel = fuel;
      AirRatio = airRatio;
      nominalSmokeTemperature = smokeTemperature;
      NominalFuelConsumption = fuelConsumption;
      ElectricConsumption = electricConsumption;
      AmbientTemperature = ambientTemperature;
      SteamPressure = steamPressure;
      SteamFlowRate = steamFlowRate;
      nominalSteamTemperature = Water.GetSaturationTemperature(steamPressure);

      double ts = Water.GetSaturationTemperature(steamPressure);
      double hs = Water.GetSaturatedVaporEnthalpy(ts);
      double hw = Water.GetSaturatedLiquidEnthalpy(inletWaterTemperature);
      NominalCapacity = steamFlowRate * (hs - hw);

      //Compute the heat loss coefficient
      heatLossCoefficient = Boiler.GetHeatLossCoefficient
        (NominalCapacity, ts, AmbientTemperature, fuel, nominalSmokeTemperature, AirRatio,
        NominalFuelConsumption, AmbientTemperature);

      //Shut off the boiler
      ShutOff();
    }

    #endregion

    #region Public methods

    /// <summary>Updates the boiler state for the given feed water and steam flow rate.</summary>
    /// <param name="inletWaterTemperature">Feed water inlet temperature [°C].</param>
    /// <param name="steamFlowRate">Steam flow rate [kg/s].</param>
    public void Update(double inletWaterTemperature, double steamFlowRate)
    {
      //Store input conditions
      InletWaterTemperature = inletWaterTemperature;
      SteamFlowRate = steamFlowRate;

      //Shut off when flow rate is zero or setpoint pressure < saturation pressure at the inlet water temperature
      double pwi = Water.GetSaturationPressure(InletWaterTemperature);
      if (SteamFlowRate <= 0)
      {
        ShutOff();
        return;
      }

      //Compute the fuel consumption
      double ts = Water.GetSaturationTemperature(SteamPressure);
      double hs = Water.GetSaturatedVaporEnthalpy(ts);
      double hw = Water.GetSaturatedLiquidEnthalpy(inletWaterTemperature);
      double hl = steamFlowRate * (hs - hw);
      FuelConsumption = Boiler.GetFuelConsumption(hl, ts, AmbientTemperature, Fuel,
        nominalSmokeTemperature, airRatio, heatLossCoefficient, AmbientTemperature, nominalSteamTemperature);

      //Adjust the steam flow rate above when overloaded
      if (NominalFuelConsumption < FuelConsumption)
      {
        FuelConsumption = NominalFuelConsumption;
        double sf;
        Boiler.GetSteamFlowRate(InletWaterTemperature, SteamPressure,
          NominalFuelConsumption, AmbientTemperature, Fuel, nominalSmokeTemperature,
          AirRatio, heatLossCoefficient, AmbientTemperature, nominalSteamTemperature, out sf, out hl);
        SteamFlowRate = sf;
      }
      HeatLoad = hl;
    }

    /// <summary>Shuts off the boiler.</summary>
    public void ShutOff()
    {
      SteamPressure = PhysicsConstants.StandardAtmosphericPressure;
      ElectricConsumption = SteamFlowRate = HeatLoad = FuelConsumption = 0;
    }

    #endregion

  }
}
