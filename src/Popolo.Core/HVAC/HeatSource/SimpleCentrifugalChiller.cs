/* SimpleCompressionRefrigerator.cs
 * 
 * Copyright (C) 2015 E.Togashi
 * 
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 2 of the License, or (at
 * your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful, but
 * WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 675 Mass Ave, Cambridge, MA 02139, USA.
 */

using System;

using Popolo.Core.Physics;

namespace Popolo.Core.HVAC.HeatSource
{
  /// <summary>Simplified centrifugal chiller based on characteristic equations.</summary>
  public class SimpleCentrifugalChiller : ICentrifugalChiller, IReadOnlyCentrifugalChiller
  {

    #region Properties and instance variables

    /// <summary>Chiller characteristic coefficients.</summary>
    private readonly double[] coef;

    /// <summary>Theoretical (Carnot) COP [-].</summary>
    private readonly double theoreticalCOP;

    /// <summary>Gets a value indicating whether the chiller has an inverter drive.</summary>
    public bool HasInverter { get; private set; }

    /// <summary>Gets or sets a value indicating whether the chiller is operating.</summary>
    public bool IsOperating { get; set; }

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

    /// <summary>Gets the chilled water flow rate [kg/s].</summary>
    public double ChilledWaterFlowRate { get; private set; }

    /// <summary>Gets the cooling water flow rate [kg/s].</summary>
    public double CoolingWaterFlowRate { get; private set; }

    /// <summary>Gets the nominal cooling capacity [kW].</summary>
    public double NominalCapacity { get; private set; }

    /// <summary>Gets the nominal power input [kW].</summary>
    public double NominalInput { get; private set; }

    /// <summary>Gets the nominal COP [-].</summary>
    public double NominalCOP { get; private set; }

    /// <summary>Gets the minimum partial load ratio for capacity control [-].</summary>
    public double MinimumPartialLoadRatio { get; private set; }

    /// <summary>Gets the electric power consumption [kW].</summary>
    public double ElectricConsumption { get; private set; }

    /// <summary>Gets the cooling load [kW].</summary>
    public double CoolingLoad { get; private set; }

    /// <summary>Gets the coefficient of performance [-].</summary>
    public double COP { get { return (ElectricConsumption == 0) ? 0 : CoolingLoad / ElectricConsumption; } }

    /// <summary>Gets the maximum chilled water flow rate [kg/s].</summary>
    public double MaxChilledWaterFlowRate { get; private set; }

    /// <summary>Gets or sets the minimum chilled water flow rate ratio [-].</summary>
    public double MinChilledWaterFlowRatio { get; set; } = 0.4;

    /// <summary>Gets a value indicating whether the chiller is overloaded.</summary>
    public bool IsOverLoad { get; private set; }

    #endregion

    #region Constructors

    /// <summary>Initializes a new instance from rated conditions.</summary>
    /// <param name="nominalInput">Nominal power input [kW].</param>
    /// <param name="minimumPartialLoadRatio">Minimum partial load ratio for capacity control [-].</param>
    /// <param name="chilledWaterInletTemperature">Chilled water inlet temperature [°C].</param>
    /// <param name="chilledWaterOutletTemperature">Chilled water outlet temperature [°C].</param>
    /// <param name="coolingWaterOutletTemperature">Cooling water outlet temperature [°C].</param>
    /// <param name="chilledWaterFlowRate">Chilled water mass flow rate [kg/s].</param>
    /// <param name="hasInverter">True if the chiller has an inverter drive.</param>
    public SimpleCentrifugalChiller
      (double nominalInput, double minimumPartialLoadRatio, double chilledWaterInletTemperature,
      double chilledWaterOutletTemperature, double coolingWaterOutletTemperature,
      double chilledWaterFlowRate, bool hasInverter)
    {
      this.NominalCapacity = chilledWaterFlowRate * 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat
        * (chilledWaterInletTemperature - chilledWaterOutletTemperature);
      this.MaxChilledWaterFlowRate = chilledWaterFlowRate;
      this.NominalInput = nominalInput;
      this.MinimumPartialLoadRatio = minimumPartialLoadRatio;
      this.HasInverter = hasInverter;
      this.theoreticalCOP = PhysicsConstants.ToKelvin(chilledWaterOutletTemperature)
        / (coolingWaterOutletTemperature - chilledWaterOutletTemperature);
      this.NominalCOP = NominalCapacity / NominalInput;
      this.ChilledWaterOutletSetpointTemperature = chilledWaterOutletTemperature;

      //Characteristic coefficients for inverter driven chillers
      if (HasInverter) coef = new double[] { 4.425e-4, -2.532e-1, 4.256e-1, 3.098e-1, 6.910e-2, 4.482e-1 };
      //Characteristic coefficients for constant speed chillers
      else coef = new double[] { -4.964e-4, 4.865e-1, 5.053e-1, 1.781e-1, -1.923e-1, 2.292e-2 };
    }

    #endregion

    #region Public methods

    /// <summary>Shuts off the chiller.</summary>
    public void ShutOff()
    {
      ChilledWaterOutletTemperature = ChilledWaterInletTemperature;
      CoolingWaterOutletTemperature = CoolingWaterInletTemperature;
      CoolingLoad = ChilledWaterFlowRate = CoolingWaterFlowRate = 0;
      ElectricConsumption = 0;
    }

    /// <summary>Updates the chiller state for the given inlet conditions.</summary>
    /// <param name="coolingWaterInletTemperature">Cooling water inlet temperature [°C].</param>
    /// <param name="chilledWaterInletTemperature">Chilled water inlet temperature [°C].</param>
    /// <param name="coolingWaterFlowRate">Cooling water mass flow rate [kg/s].</param>
    /// <param name="chilledWaterFlowRate">Chilled water mass flow rate [kg/s].</param>
    public void Update
      (double coolingWaterInletTemperature, double chilledWaterInletTemperature,
      double coolingWaterFlowRate, double chilledWaterFlowRate)
    {
      //Store state values
      this.ChilledWaterInletTemperature = chilledWaterInletTemperature;
      this.CoolingWaterInletTemperature = coolingWaterInletTemperature;
      this.ChilledWaterFlowRate = chilledWaterFlowRate;
      this.CoolingWaterFlowRate = coolingWaterFlowRate;

      //Shut off if not operating
      if (!IsOperating)
      {
        ShutOff();
        return;
      }

      //Compute the heat capacity rates
      double mcch = 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat * ChilledWaterFlowRate;
      double mccd = 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat * CoolingWaterFlowRate;

      //Compute the load [kW]
      CoolingLoad = mcch * (ChilledWaterInletTemperature - ChilledWaterOutletSetpointTemperature);

      //Correct the outlet temperature when overloaded
      IsOverLoad = NominalCapacity < CoolingLoad;
      if (IsOverLoad)
      {
        CoolingLoad = NominalCapacity;
        ChilledWaterOutletTemperature = ChilledWaterInletTemperature - CoolingLoad / mcch;
      }
      else ChilledWaterOutletTemperature = ChilledWaterOutletSetpointTemperature;

      //Compute the electric consumption at the lower limit when below the minimum capacity control ratio
      double load = Math.Max(MinimumPartialLoadRatio * NominalCapacity, CoolingLoad);

      //Compute the cooling water outlet temperature
      double tcho = PhysicsConstants.ToKelvin(ChilledWaterOutletTemperature);
      double tcdi = PhysicsConstants.ToKelvin(coolingWaterInletTemperature);
      double pL = load / NominalCapacity;
      double dcd = (coef[2] + coef[3] * pL) * theoreticalCOP;
      double ecd = coef[0] + pL * (coef[1] + coef[5] * pL);
      double acd = coef[4] * theoreticalCOP * theoreticalCOP;
      double bcd = tcho * (-2 * acd + dcd - (tcho * mccd) / NominalInput);
      double ccd = tcho * tcho * (acd - dcd + ecd + (load + tcdi * mccd) / NominalInput);

      //Quadratic formula
      CoolingWaterOutletTemperature = PhysicsConstants.ToCelsius((-bcd - Math.Sqrt(bcd * bcd - 4 * acd * ccd)) / (2d * acd));

      //Compute the electric consumption
      double tCop = tcho / (CoolingWaterOutletTemperature - ChilledWaterOutletTemperature);
      ElectricConsumption = NominalInput * ((acd / tCop + dcd) / tCop + ecd);
    }

    #endregion

  }
}
