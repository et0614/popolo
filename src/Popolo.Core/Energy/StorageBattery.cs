/* StorageBattery.cs
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
  /// Represents a storage battery (DC side) and provides charging and discharging calculations.
  /// </summary>
  /// <remarks>
  /// The state of charge is updated by an energy balance model. The charging and
  /// discharging efficiencies are expressed as a linear function of the C-rate:
  /// eta_ch = eta_coul * (1 - k * c), eta_dis = 1 - k * c,
  /// which is the first order approximation of the ohmic (I^2 R) loss inside the battery.
  /// The coulombic efficiency eta_coul represents the charge lost to side reactions,
  /// which occurs on the charging side only (about 90 % for lead-acid batteries and
  /// nearly 100 % for lithium-ion batteries).
  /// All powers are DC side values at the battery terminals. Conversion losses of the
  /// power converter and standby power of the storage system are out of the scope of
  /// this class (see <see cref="BatteryStorageSystem"/>).
  /// References:
  /// - JIS C 4413: Evaluation indices of low-voltage electrical energy storage systems.
  /// - EnergyPlus Engineering Reference: Electric Load Center Distribution Manager.
  /// </remarks>
  public class StorageBattery : IReadOnlyStorageBattery
  {

    #region Enumerations

    /// <summary>
    /// Specifies the battery chemistry type.
    /// </summary>
    public enum ChemistryType
    {
      /// <summary>Lead-acid battery.</summary>
      LeadAcid,
      /// <summary>Lithium-ion battery.</summary>
      LithiumIon
    }

    #endregion

    #region Properties

    /// <summary>Backing field for the coulombic efficiency.</summary>
    private double _coulombicEfficiency;

    /// <summary>Backing field for the loss coefficient.</summary>
    private double _lossCoefficient = 0.1;

    /// <summary>Backing field for the stored energy.</summary>
    private double _storedEnergy = 0;

    /// <summary>
    /// Gets the nominal capacity [Wh] (rated capacity of the battery cells,
    /// used as the reference of the C-rate).
    /// </summary>
    public double NominalCapacity { get; private set; }

    /// <summary>
    /// Gets the maximum storable energy [Wh] (usable capacity within the state of
    /// charge window enforced by the battery management system).
    /// </summary>
    public double MaximumStorableEnergy { get; private set; }

    /// <summary>Gets the battery chemistry type.</summary>
    public ChemistryType Chemistry { get; private set; }

    /// <summary>Gets or sets the stored energy [Wh] (clamped to [0, MaximumStorableEnergy]).</summary>
    public double StoredEnergy
    {
      get => _storedEnergy;
      set => _storedEnergy = Math.Max(0, Math.Min(MaximumStorableEnergy, value));
    }

    /// <summary>Gets the state of charge [-] (ratio of the stored energy to the maximum storable energy).</summary>
    public double StateOfCharge => _storedEnergy / MaximumStorableEnergy;

    /// <summary>Gets or sets the coulombic efficiency [-] (clamped to [0, 1]).</summary>
    public double CoulombicEfficiency
    {
      get => _coulombicEfficiency;
      set => _coulombicEfficiency = Math.Max(0, Math.Min(1, value));
    }

    /// <summary>
    /// Gets or sets the rate dependent loss coefficient k [h] (clamped to 0 or more).
    /// </summary>
    public double LossCoefficient
    {
      get => _lossCoefficient;
      set => _lossCoefficient = Math.Max(0, value);
    }

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the storage battery.
    /// </summary>
    /// <param name="nominalCapacity">Nominal capacity [Wh].</param>
    /// <param name="maximumStorableEnergy">Maximum storable energy [Wh].</param>
    /// <param name="chemistry">Battery chemistry type.</param>
    public StorageBattery(
        double nominalCapacity, double maximumStorableEnergy, ChemistryType chemistry)
    {
      NominalCapacity = nominalCapacity;
      MaximumStorableEnergy = maximumStorableEnergy;
      Chemistry = chemistry;
      _coulombicEfficiency = chemistry == ChemistryType.LeadAcid ? 0.9 : 1.0;
    }

    #endregion

    #region Instance methods

    /// <summary>
    /// Gets the charging efficiency [-] at the specified charging power.
    /// </summary>
    /// <param name="power">Charging power [W] at the battery terminals.</param>
    /// <returns>Charging efficiency [-]</returns>
    public double GetChargingEfficiency(double power)
    {
      double cRate = Math.Max(0, power) / NominalCapacity;
      return _coulombicEfficiency * Math.Max(0, 1.0 - _lossCoefficient * cRate);
    }

    /// <summary>
    /// Gets the discharging efficiency [-] at the specified discharging power.
    /// </summary>
    /// <param name="power">Discharging power [W] at the battery terminals.</param>
    /// <returns>Discharging efficiency [-]</returns>
    public double GetDischargingEfficiency(double power)
    {
      double cRate = Math.Max(0, power) / NominalCapacity;
      return Math.Max(0, 1.0 - _lossCoefficient * cRate);
    }

    /// <summary>
    /// Charges the battery and updates the state of charge.
    /// </summary>
    /// <remarks>
    /// When the battery reaches the full state within the time step, the actual
    /// charging power is back-calculated so that the energy balance holds.
    /// The efficiency is evaluated with the requested (pre-clamp) power.
    /// </remarks>
    /// <param name="power">Requested charging power [W] at the battery terminals.</param>
    /// <param name="timeStep">Time step [h].</param>
    /// <returns>Actual charging power [W] at the battery terminals.</returns>
    public double Charge(double power, double timeStep)
    {
      if (power <= 0 || timeStep <= 0) return 0;

      double efficiency = GetChargingEfficiency(power);
      if (efficiency <= 0) return 0;

      double storedDelta = power * efficiency * timeStep;
      double room = MaximumStorableEnergy - _storedEnergy;
      if (storedDelta < room)
      {
        _storedEnergy += storedDelta;
        return power;
      }
      else
      {
        _storedEnergy = MaximumStorableEnergy;
        return room / (efficiency * timeStep);
      }
    }

    /// <summary>
    /// Discharges the battery and updates the state of charge.
    /// </summary>
    /// <remarks>
    /// When the battery becomes empty within the time step, the actual discharging
    /// power is back-calculated so that the energy balance holds.
    /// The efficiency is evaluated with the requested (pre-clamp) power.
    /// </remarks>
    /// <param name="power">Requested discharging power [W] at the battery terminals.</param>
    /// <param name="timeStep">Time step [h].</param>
    /// <returns>Actual discharging power [W] at the battery terminals.</returns>
    public double Discharge(double power, double timeStep)
    {
      if (power <= 0 || timeStep <= 0) return 0;

      double efficiency = GetDischargingEfficiency(power);
      if (efficiency <= 0) return 0;

      double storedDelta = power / efficiency * timeStep;
      if (storedDelta < _storedEnergy)
      {
        _storedEnergy -= storedDelta;
        return power;
      }
      else
      {
        double actualPower = _storedEnergy * efficiency / timeStep;
        _storedEnergy = 0;
        return actualPower;
      }
    }

    #endregion

    #region Static methods

    /// <summary>
    /// Estimates the loss coefficient k [h] from the round trip efficiency of the battery.
    /// </summary>
    /// <remarks>
    /// Solves 0 = k^2 * cch * cdis - k * (cch + cdis) + 1 - eta_rt / eta_coul
    /// and returns the physically valid (smaller) root. The round trip efficiency
    /// must be the DC side value of the battery alone: when only the total efficiency
    /// of the storage system (AC to AC) is available, divide it by the conversion
    /// efficiencies of the power converter beforehand.
    /// </remarks>
    /// <param name="chargeCRate">C-rate [1/h] at which the charging efficiency was measured.</param>
    /// <param name="dischargeCRate">C-rate [1/h] at which the discharging efficiency was measured.</param>
    /// <param name="roundTripEfficiency">Round trip (charge x discharge) efficiency [-] of the battery alone.</param>
    /// <param name="coulombicEfficiency">Coulombic efficiency [-].</param>
    /// <returns>Loss coefficient k [h]</returns>
    public static double EstimateLossCoefficient(
        double chargeCRate, double dischargeCRate,
        double roundTripEfficiency, double coulombicEfficiency)
    {
      double constant = 1.0 - roundTripEfficiency / coulombicEfficiency;
      if (constant <= 0) return 0;

      double a = chargeCRate * dischargeCRate;
      double b = chargeCRate + dischargeCRate;
      if (a <= 0) return constant / b;

      //Smaller root of the quadratic equation (the larger root makes the efficiency negative)
      return (b - Math.Sqrt(b * b - 4 * a * constant)) / (2 * a);
    }

    #endregion

  }

  /// <summary>
  /// Represents a read-only view of a storage battery.
  /// </summary>
  public interface IReadOnlyStorageBattery
  {
    /// <summary>Gets the nominal capacity [Wh].</summary>
    double NominalCapacity { get; }

    /// <summary>Gets the maximum storable energy [Wh].</summary>
    double MaximumStorableEnergy { get; }

    /// <summary>Gets the battery chemistry type.</summary>
    StorageBattery.ChemistryType Chemistry { get; }

    /// <summary>Gets the stored energy [Wh].</summary>
    double StoredEnergy { get; }

    /// <summary>Gets the state of charge [-].</summary>
    double StateOfCharge { get; }

    /// <summary>Gets the coulombic efficiency [-].</summary>
    double CoulombicEfficiency { get; }

    /// <summary>Gets the rate dependent loss coefficient k [h].</summary>
    double LossCoefficient { get; }

    /// <summary>Gets the charging efficiency [-] at the specified charging power [W].</summary>
    /// <param name="power">Charging power [W] at the battery terminals.</param>
    /// <returns>Charging efficiency [-]</returns>
    double GetChargingEfficiency(double power);

    /// <summary>Gets the discharging efficiency [-] at the specified discharging power [W].</summary>
    /// <param name="power">Discharging power [W] at the battery terminals.</param>
    /// <returns>Discharging efficiency [-]</returns>
    double GetDischargingEfficiency(double power);
  }

}
