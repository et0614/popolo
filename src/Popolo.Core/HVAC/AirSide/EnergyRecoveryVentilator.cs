/* EnergyRecoveryVentilator.cs
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
using Popolo.Core.HVAC.HeatExchanger;
using Popolo.Core.Physics;

namespace Popolo.Core.HVAC.AirSide
{
  /// <summary>
  /// Energy recovery ventilator (ERV): a packaged ventilation unit
  /// comprising an air-to-air fixed-plate heat exchanger, a supply air fan,
  /// and an exhaust air fan.
  /// </summary>
  /// <remarks>
  /// <para>
  /// Outdoor air is drawn by the supply air fan, exchanges sensible (and,
  /// for a total heat exchanger, latent) heat with the exhaust air stream,
  /// and is supplied to the room. Return air is drawn by the exhaust air fan
  /// and discharged outdoors. When the heat exchanger is sensible-only, the
  /// unit corresponds to a heat recovery ventilator (HRV).
  /// </para>
  /// <para>
  /// Fan heat is treated in the same way as in
  /// <see cref="AirHandlingUnit"/>: the temperature rise by the supply air
  /// fan is added downstream of the heat exchanger, and the heat of the
  /// exhaust air fan is discharged outdoors and therefore ignored.
  /// </para>
  /// </remarks>
  public class EnergyRecoveryVentilator : IReadOnlyEnergyRecoveryVentilator
  {

    #region Instance variables and properties

    /// <summary>Air-to-air fixed-plate heat exchanger.</summary>
    private readonly AirToAirFlatPlateHeatExchanger hex;

    /// <summary>Supply air fan.</summary>
    private readonly CentrifugalFan saFan;

    /// <summary>Exhaust air fan.</summary>
    private readonly CentrifugalFan eaFan;

    /// <summary>Gets the air-to-air fixed-plate heat exchanger.</summary>
    public IReadOnlyAirToAirFlatPlateHeatExchanger HeatExchanger { get { return hex; } }

    /// <summary>Gets the supply air fan.</summary>
    public IReadOnlyFluidMachinery SupplyAirFan { get { return saFan; } }

    /// <summary>Gets the exhaust air fan.</summary>
    public IReadOnlyFluidMachinery ExhaustAirFan { get { return eaFan; } }

    /// <summary>
    /// Gets or sets a value indicating whether to bypass the heat exchanger
    /// (normal ventilation without heat recovery).
    /// </summary>
    public bool BypassHeatExchanger { get; set; }

    /// <summary>Gets or sets the outdoor air dry-bulb temperature [°C].</summary>
    public double OATemperature { get; set; }

    /// <summary>Gets or sets the outdoor air humidity ratio [kg/kg].</summary>
    public double OAHumidityRatio { get; set; }

    /// <summary>Gets or sets the return air dry-bulb temperature [°C].</summary>
    public double RATemperature { get; set; }

    /// <summary>Gets or sets the return air humidity ratio [kg/kg].</summary>
    public double RAHumidityRatio { get; set; }

    /// <inheritdoc />
    public double SAFlowRate { get; private set; }

    /// <inheritdoc />
    public double EAFlowRate { get; private set; }

    /// <inheritdoc />
    public double SATemperature { get; private set; }

    /// <inheritdoc />
    public double SAHumidityRatio { get; private set; }

    /// <inheritdoc />
    public double EATemperature { get; private set; }

    /// <inheritdoc />
    public double EAHumidityRatio { get; private set; }

    /// <inheritdoc />
    public int NotchCount { get { return saFan.NotchCount; } }

    /// <inheritdoc />
    public int CurrentNotchIndex { get { return saFan.CurrentNotchIndex; } }

    /// <inheritdoc />
    public string CurrentNotchName { get { return saFan.CurrentNotchName; } }

    #endregion

    #region Constructors

    /// <summary>Initializes a new instance.</summary>
    /// <param name="heatExchanger">Air-to-air fixed-plate heat exchanger.</param>
    /// <param name="saFan">Supply air fan.</param>
    /// <param name="eaFan">Exhaust air fan.</param>
    public EnergyRecoveryVentilator
      (AirToAirFlatPlateHeatExchanger heatExchanger, CentrifugalFan saFan, CentrifugalFan eaFan)
    {
      if (heatExchanger == null)
        throw new PopoloArgumentException("heatExchanger must not be null.", nameof(heatExchanger));
      if (saFan == null)
        throw new PopoloArgumentException("saFan must not be null.", nameof(saFan));
      if (eaFan == null)
        throw new PopoloArgumentException("eaFan must not be null.", nameof(eaFan));

      this.hex = heatExchanger;
      this.saFan = saFan;
      this.eaFan = eaFan;
    }

    #endregion

    #region Instance methods

    /// <summary>Sets the supply and exhaust air flow rates [kg/s].</summary>
    /// <param name="saFlowRate">Supply air flow rate [kg/s].</param>
    /// <param name="eaFlowRate">Exhaust air flow rate [kg/s].</param>
    /// <remarks>The fans leave notch operation (<see cref="CurrentNotchIndex"/> becomes -1).</remarks>
    public void SetAirFlowRate(double saFlowRate, double eaFlowRate)
    {
      SAFlowRate = Math.Max(0, saFlowRate);
      EAFlowRate = Math.Max(0, eaFlowRate);
      saFan.InvalidateNotch();
      eaFan.InvalidateNotch();
    }

    /// <summary>Operates the unit at the specified fan notch.</summary>
    /// <param name="notchIndex">Notch index (ascending air flow order).</param>
    /// <remarks>
    /// The notch tables are those of the supply and exhaust air fans
    /// (<see cref="FluidMachinery.SetFlowNotches"/>); configure both fans
    /// with the same number of notches before use. The notch index is
    /// applied to both fans and the air mass flow rates follow each fan's
    /// own notch flow: giving the fans different flow rates per notch models
    /// designs where the supply exceeds the exhaust (e.g., SA 500 CMH and
    /// EA 450 CMH, the 50 CMH difference leaving through a toilet exhaust).
    /// </remarks>
    public void SetNotch(int notchIndex)
    {
      saFan.SetNotch(notchIndex);
      eaFan.SetNotch(notchIndex);
      ApplyFanNotchFlows();
    }

    /// <summary>Operates the unit at the specified fan notch.</summary>
    /// <param name="notchName">Notch name (of the supply air fan's table).</param>
    /// <remarks>
    /// The name is resolved on the supply air fan's notch table and the
    /// exhaust air fan follows by index. Giving both fans the same notch
    /// names is recommended for consistent monitoring.
    /// </remarks>
    public void SetNotch(string notchName)
    {
      saFan.SetNotch(notchName);
      eaFan.SetNotch(saFan.CurrentNotchIndex);
      ApplyFanNotchFlows();
    }

    /// <summary>
    /// Steps up the air flow to the next fan notch. When the unit is not
    /// operating on a notch, the lowest notch is selected.
    /// </summary>
    /// <returns>False when the notch is already at the maximum (no change).</returns>
    public bool RaiseNotch()
    {
      bool changed = saFan.RaiseNotch();
      eaFan.SetNotch(saFan.CurrentNotchIndex);
      ApplyFanNotchFlows();
      return changed;
    }

    /// <summary>Steps down the air flow to the previous fan notch.</summary>
    /// <returns>
    /// False when the notch is already at the minimum or the unit is not
    /// operating on a notch (no change).
    /// </returns>
    public bool LowerNotch()
    {
      if (!saFan.LowerNotch()) return false;
      eaFan.SetNotch(saFan.CurrentNotchIndex);
      ApplyFanNotchFlows();
      return true;
    }

    /// <summary>Applies the fans' notch flow rates to the air mass flow rates of the unit.</summary>
    private void ApplyFanNotchFlows()
    {
      SAFlowRate = saFan.VolumetricFlowRate * PhysicsConstants.NominalMoistAirDensity;
      EAFlowRate = eaFan.VolumetricFlowRate * PhysicsConstants.NominalMoistAirDensity;
    }

    /// <summary>
    /// Operates the ventilator: exchanges heat between the outdoor and the
    /// exhaust air streams and computes the supply air state.
    /// </summary>
    /// <remarks>
    /// When <see cref="BypassHeatExchanger"/> is set or the exhaust air flow
    /// is zero, no heat is recovered and the supply air is the outdoor air
    /// plus the supply air fan temperature rise.
    /// </remarks>
    public void Ventilate()
    {
      //Shut off when the SA flow rate is 0
      if (SAFlowRate <= 0)
      {
        ShutOff();
        return;
      }

      //Compute the fan temperature rise
      saFan.UpdateState(SAFlowRate / PhysicsConstants.NominalMoistAirDensity);
      eaFan.UpdateState(EAFlowRate / PhysicsConstants.NominalMoistAirDensity);
      //Electric consumption is in [kW] and specific heat in [J/(kg·K)], so multiply by 0.001 to make the units consistent
      double tRise = saFan.GetElectricConsumption()
        / (SAFlowRate * 0.001 * PhysicsConstants.NominalMoistAirIsobaricSpecificHeat);

      if (!BypassHeatExchanger && 0 < EAFlowRate)
      {
        //Total heat exchange
        const double cf = 3600 / PhysicsConstants.NominalMoistAirDensity;
        hex.UpdateState(SAFlowRate * cf, EAFlowRate * cf,
          OATemperature, OAHumidityRatio, RATemperature, RAHumidityRatio);

        //Add the supply fan temperature rise after the heat exchange. Exhaust fan heat is ignored as it is discharged outdoors
        SATemperature = hex.SupplyAirOutletDryBulbTemperature + tRise;
        SAHumidityRatio = hex.SupplyAirOutletHumidityRatio;
        EATemperature = hex.ExhaustAirOutletDryBulbTemperature;
        EAHumidityRatio = hex.ExhaustAirOutletHumidityRatio;
      }
      else
      {
        //Bypass operation (no heat recovery)
        hex.UpdateState(0, 0, OATemperature, OAHumidityRatio, RATemperature, RAHumidityRatio);
        SATemperature = OATemperature + tRise;
        SAHumidityRatio = OAHumidityRatio;
        EATemperature = RATemperature;
        EAHumidityRatio = RAHumidityRatio;
      }
    }

    /// <summary>Gets the electric consumption of the supply and exhaust air fans [kW].</summary>
    /// <returns>Electric consumption of the supply and exhaust air fans [kW].</returns>
    public double GetElectricConsumption()
    {
      return saFan.GetElectricConsumption() + eaFan.GetElectricConsumption();
    }

    /// <summary>Shuts off the ventilator (zero airflow, fans stopped).</summary>
    public void ShutOff()
    {
      saFan.ShutOff();
      eaFan.ShutOff();
      SAFlowRate = 0.0;
      EAFlowRate = 0.0;
      hex.UpdateState(0, 0, OATemperature, OAHumidityRatio, RATemperature, RAHumidityRatio);
      SATemperature = OATemperature;
      SAHumidityRatio = OAHumidityRatio;
      EATemperature = RATemperature;
      EAHumidityRatio = RAHumidityRatio;
    }

    #endregion

  }
}
