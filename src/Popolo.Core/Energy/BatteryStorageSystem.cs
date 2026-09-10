/* BatteryStorageSystem.cs
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
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
 * General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301, USA.
 */

using System;

namespace Popolo.Core.Energy
{
  /// <summary>
  /// Represents a battery storage system composed of a storage battery and power converters.
  /// </summary>
  /// <remarks>
  /// The system is modeled as a composition of a <see cref="StorageBattery"/> (DC side)
  /// and two <see cref="PowerConverter"/> instances for the charging and discharging
  /// directions. All the powers of this class are the values at the external connection
  /// point of the system (the AC side for an AC coupled system), which is the reference
  /// side of the conversion efficiencies.
  /// The standby power is not included in the charging and discharging calculations:
  /// add <see cref="StandbyPower"/> to the electricity balance of the building
  /// while the system is idle.
  /// References:
  /// - JIS C 4413: Evaluation indices of low-voltage electrical energy storage systems.
  /// - EnergyPlus Engineering Reference: Electric Load Center Distribution Manager.
  /// </remarks>
  public class BatteryStorageSystem : IReadOnlyBatteryStorageSystem
  {

    #region Properties

    /// <summary>Backing field for the standby power.</summary>
    private double _standbyPower = 0;

    /// <summary>Gets the storage battery.</summary>
    public StorageBattery Battery { get; private set; }

    /// <summary>Gets the power converter for the charging direction.</summary>
    public PowerConverter ChargingConverter { get; private set; }

    /// <summary>Gets the power converter for the discharging direction.</summary>
    public PowerConverter DischargingConverter { get; private set; }

    /// <summary>Gets or sets the standby power [W] (clamped to 0 or more).</summary>
    public double StandbyPower
    {
      get => _standbyPower;
      set => _standbyPower = Math.Max(0, value);
    }

    /// <summary>Gets the state of charge [-] of the battery.</summary>
    public double StateOfCharge => Battery.StateOfCharge;

    /// <summary>Gets the storage battery as a read-only view.</summary>
    IReadOnlyStorageBattery IReadOnlyBatteryStorageSystem.Battery => Battery;

    /// <summary>Gets the charging converter as a read-only view.</summary>
    IReadOnlyPowerConverter IReadOnlyBatteryStorageSystem.ChargingConverter => ChargingConverter;

    /// <summary>Gets the discharging converter as a read-only view.</summary>
    IReadOnlyPowerConverter IReadOnlyBatteryStorageSystem.DischargingConverter => DischargingConverter;

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the battery storage system.
    /// </summary>
    /// <remarks>
    /// The same <see cref="PowerConverter"/> instance can be passed to both of the
    /// charging and discharging converters when their characteristics are common.
    /// </remarks>
    /// <param name="battery">Storage battery.</param>
    /// <param name="chargingConverter">Power converter for the charging direction.</param>
    /// <param name="dischargingConverter">Power converter for the discharging direction.</param>
    public BatteryStorageSystem(
        StorageBattery battery,
        PowerConverter chargingConverter, PowerConverter dischargingConverter)
    {
      Battery = battery;
      ChargingConverter = chargingConverter;
      DischargingConverter = dischargingConverter;
    }

    #endregion

    #region Instance methods

    /// <summary>
    /// Charges the system and updates the state of charge.
    /// </summary>
    /// <remarks>
    /// The requested power is limited by the rated power of the charging converter.
    /// When the battery reaches the full state within the time step, the actual power
    /// is back-calculated with the conversion efficiency evaluated at the requested
    /// (pre-clamp) power.
    /// </remarks>
    /// <param name="power">Requested charging power [W] at the external connection point.</param>
    /// <param name="timeStep">Time step [h].</param>
    /// <returns>Actual charging power [W] at the external connection point.</returns>
    public double Charge(double power, double timeStep)
    {
      if (power <= 0 || timeStep <= 0) return 0;

      double systemPower = Math.Min(power, ChargingConverter.RatedPower);
      double efficiency = ChargingConverter.GetEfficiency(systemPower);
      if (efficiency <= 0) return 0;

      double batteryPower = systemPower * efficiency;
      double actualBatteryPower = Battery.Charge(batteryPower, timeStep);
      return actualBatteryPower / efficiency;
    }

    /// <summary>
    /// Discharges the system and updates the state of charge.
    /// </summary>
    /// <remarks>
    /// The requested power is limited by the rated power of the discharging converter.
    /// When the battery becomes empty within the time step, the actual power is
    /// back-calculated with the conversion efficiency evaluated at the requested
    /// (pre-clamp) power.
    /// </remarks>
    /// <param name="power">Requested discharging power [W] at the external connection point.</param>
    /// <param name="timeStep">Time step [h].</param>
    /// <returns>Actual discharging power [W] at the external connection point.</returns>
    public double Discharge(double power, double timeStep)
    {
      if (power <= 0 || timeStep <= 0) return 0;

      double systemPower = Math.Min(power, DischargingConverter.RatedPower);
      double efficiency = DischargingConverter.GetEfficiency(systemPower);
      if (efficiency <= 0) return 0;

      double batteryPower = systemPower / efficiency;
      double actualBatteryPower = Battery.Discharge(batteryPower, timeStep);
      return actualBatteryPower * efficiency;
    }

    #endregion

    #region Static methods

    /// <summary>
    /// Creates a battery storage system from catalog values.
    /// </summary>
    /// <remarks>
    /// The catalog round trip efficiency is assumed to be the total efficiency of the
    /// system measured at the external connection point (AC to AC), as defined in
    /// JIS C 4413. The loss coefficient of the battery is calibrated by stripping the
    /// conversion losses evaluated at the measurement condition, and the maximum
    /// storable energy is back-calculated from the initial effective capacity.
    /// When the C-rate of the measurement exceeds the rated power of the converter,
    /// the actual measurement condition is limited by the converter rating: both the
    /// C-rate and the load ratio are adjusted accordingly.
    /// </remarks>
    /// <param name="nominalCapacity">Nominal capacity [Wh] of the battery.</param>
    /// <param name="initialEffectiveCapacity">Initial effective capacity [Wh] (dischargeable energy measured at the external connection point).</param>
    /// <param name="roundTripEfficiency">Round trip efficiency [-] of the system (external connection point basis).</param>
    /// <param name="chargeCRate">C-rate [1/h] of the charging in the catalog measurement (e.g. 0.3).</param>
    /// <param name="dischargeCRate">C-rate [1/h] of the discharging in the catalog measurement (e.g. 0.4).</param>
    /// <param name="chemistry">Battery chemistry type.</param>
    /// <param name="ratedChargingPower">Rated charging power [W] at the external connection point.</param>
    /// <param name="ratedDischargingPower">Rated discharging power [W] at the external connection point.</param>
    /// <returns>Battery storage system with the calibrated parameters.</returns>
    public static BatteryStorageSystem CreateFromCatalog(
        double nominalCapacity, double initialEffectiveCapacity,
        double roundTripEfficiency, double chargeCRate, double dischargeCRate,
        StorageBattery.ChemistryType chemistry,
        double ratedChargingPower, double ratedDischargingPower)
    {
      PowerConverter chargingConverter = new PowerConverter(ratedChargingPower);
      PowerConverter dischargingConverter = new PowerConverter(ratedDischargingPower);

      //Measurement condition: the C-rate is limited by the converter rating
      double cCharge = Math.Min(chargeCRate, ratedChargingPower / nominalCapacity);
      double cDischarge = Math.Min(dischargeCRate, ratedDischargingPower / nominalCapacity);
      double pCharge = Math.Min(1, cCharge * nominalCapacity / ratedChargingPower);
      double pDischarge = Math.Min(1, cDischarge * nominalCapacity / ratedDischargingPower);

      //Strip the conversion losses to obtain the round trip efficiency of the battery alone
      double convChargeEfficiency = chargingConverter.GetEfficiencyAtLoadRatio(pCharge);
      double convDischargeEfficiency = dischargingConverter.GetEfficiencyAtLoadRatio(pDischarge);
      double batteryRoundTrip =
          roundTripEfficiency / (convChargeEfficiency * convDischargeEfficiency);

      double coulombicEfficiency =
          chemistry == StorageBattery.ChemistryType.LeadAcid ? 0.9 : 1.0;
      double lossCoefficient = StorageBattery.EstimateLossCoefficient(
          cCharge, cDischarge, batteryRoundTrip, coulombicEfficiency);

      //Back-calculate the maximum storable energy from the initial effective capacity
      double batteryDischargeEfficiency = 1.0 - lossCoefficient * cDischarge;
      double maximumStorableEnergy = initialEffectiveCapacity
          / (batteryDischargeEfficiency * convDischargeEfficiency);

      StorageBattery battery =
          new StorageBattery(nominalCapacity, maximumStorableEnergy, chemistry)
          { LossCoefficient = lossCoefficient };

      return new BatteryStorageSystem(battery, chargingConverter, dischargingConverter);
    }

    #endregion

  }

  /// <summary>
  /// Represents a read-only view of a battery storage system.
  /// </summary>
  public interface IReadOnlyBatteryStorageSystem
  {
    /// <summary>Gets the storage battery.</summary>
    IReadOnlyStorageBattery Battery { get; }

    /// <summary>Gets the power converter for the charging direction.</summary>
    IReadOnlyPowerConverter ChargingConverter { get; }

    /// <summary>Gets the power converter for the discharging direction.</summary>
    IReadOnlyPowerConverter DischargingConverter { get; }

    /// <summary>Gets the standby power [W].</summary>
    double StandbyPower { get; }

    /// <summary>Gets the state of charge [-] of the battery.</summary>
    double StateOfCharge { get; }
  }

}
