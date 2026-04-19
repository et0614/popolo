/* VRFSystem.cs
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
using System.Collections.Generic;

using Popolo.Core.Physics;
using Popolo.Core.Numerics;
using Popolo.Core.Numerics.LinearAlgebra;

namespace Popolo.Core.HVAC.VRF
{

  /// <summary>VRF (Variable Refrigerant Flow) heat pump system.</summary>
  /// <remarks>
  /// Eisuke Togashi and Makoto Satoh,
  /// Development of variable refrigerant flow heat-pump model for annual-energy simulation,
  /// Journal of Building Performance Simulation,
  /// https://doi.org/10.1080/19401493.2021.1986573
  /// </remarks>
  public class VRFSystem : IReadOnlyVRFSystem
  {

    #region 定数宣言

    /// <summary>Temperature conversion constant.</summary>
    private const double KTOC = PhysicsConstants.CelsiusToKelvinOffset;

    #region JIS8616, JIS8615-3条件

    /// <summary>JIS 8615-3 standard outdoor dry-bulb temperature in cooling mode [°C].</summary>
    private const double JIS_OA_DBT_NOM_C = 35;

    /// <summary>JIS 8615-3 standard outdoor wet-bulb temperature in cooling mode [°C].</summary>
    private const double JIS_OA_WBT_NOM_C = 24;

    /// <summary>JIS 8615-3 indoor dry-bulb temperature in cooling mode [°C].</summary>
    private const double JIS_IA_DBT_C = 27;

    /// <summary>JIS 8615-3 indoor wet-bulb temperature in cooling mode [°C].</summary>
    private const double JIS_IA_WBT_C = 19;

    /// <summary>JIS 8616 intermediate outdoor dry-bulb temperature in cooling mode [°C].</summary>
    private const double JIS_OA_DBT_MID_C = 29;

    /// <summary>JIS 8616 intermediate outdoor dry-bulb temperature in cooling mode [°C].</summary>
    private const double JIS_OA_WBT_MID_C = 19;

    /// <summary>JIS 8615-3 standard outdoor dry-bulb temperature in heating mode [°C].</summary>
    private const double JIS_OA_DBT_NOM_H = 7;

    /// <summary>JIS 8615-3 standard outdoor wet-bulb temperature in heating mode [°C].</summary>
    private const double JIS_OA_WBT_NOM_H = 6;

    /// <summary>JIS 8615-3 indoor dry-bulb temperature in heating mode [°C].</summary>
    private const double JIS_IA_DBT_H = 20;

    /// <summary>JIS 8615-3 indoor wet-bulb temperature in heating mode [°C] (approximately 50% RH, as no standard specifies this).</summary>
    private const double JIS_IA_WBT_H = 14;

    #endregion

    /// <summary>Nominal condensing temperature [°C] used as both the reference point for rated-condition performance estimation and the default control target.</summary>
    /// <remarks>
    /// Used for (1) estimating system performance curves at rated JIS operating conditions,
    /// and (2) providing the default value for <see cref="TargetCondensingTemperature"/> and
    /// heating-mode <see cref="ModeParameters.MaxCondensingTemperature"/>.
    /// This value (46 °C) corresponds to the upper bound of the EnergyPlus control range (42–46 °C).
    /// </remarks>
    public const double NominalCondensingTemperature = 46;

    /// <summary>Nominal evaporating temperature [°C] used as both the reference point for rated-condition performance estimation and the default control target.</summary>
    /// <remarks>
    /// Used for (1) estimating system performance curves at rated JIS operating conditions,
    /// and (2) providing the default value for <see cref="TargetEvaporatingTemperature"/> and
    /// cooling-mode <see cref="ModeParameters.MinEvaporatingTemperature"/>.
    /// This value (10 °C) lies within the EnergyPlus control range (3–13 °C),
    /// representing a typical rated cooling operating point.
    /// </remarks>
    public const double NominalEvaporatingTemperature = 10;

    /// <summary>Nominal superheat degree [°C].</summary>
    private const double SUPER_HEAT_NOM = 1;

    /// <summary>Nominal subcooling degree [°C].</summary>
    private const double SUB_COOL_NOM = 1;

    /// <summary>Minimum head efficiency ratio during on/off operation [-].</summary>
    private const double MIN_ER_RATE = 0.20;//NEDO試験によればこのくらいで発停の消費電力が実測に合う

    /// <summary>Minimum compression ratio [-].</summary>
    private const double MIN_COMPRESSION_RATIO = 1.5;

    #endregion

    #region 列挙型定義

    /// <summary>VRF system operating mode.</summary>
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

    #region インスタンス変数・プロパティ

    #region VRFシステム全体

    /// <summary>Gets or sets a value indicating whether thermo-off time is controlled based on sensible heat.</summary>
    /// <remarks>When false, the supply air temperature may deviate from the setpoint, but the total heat transfer matches.</remarks>
    public bool ControlThermoOffWithSensibleHeat { get; set; } = true;

    /// <summary>Gets or sets the operating mode.</summary>
    public Mode CurrentMode { get; set; } = Mode.ShutOff;

    /// <summary>Gets a value indicating whether the system is a gas engine heat pump (GHP).</summary>
    public bool IsGasEngineHeatpump { get { return TotalEfficiencyOfGasEngineHeatpump != 0; } }

    /// <summary>Gets the overall efficiency of the gas engine heat pump [-].</summary>
    public double TotalEfficiencyOfGasEngineHeatpump { get; private set; } = 0.8;

    /// <summary>List of indoor unit heat exchangers.</summary>
    private List<VRFUnit> indoorUnits = new List<VRFUnit>();

    /// <summary>Gets the number of indoor units.</summary>
    public int IndoorUnitCount { get { return indoorUnits.Count; } }

    /// <summary>Gets the list of indoor unit heat exchangers.</summary>
    public IReadOnlyVRFUnit[] IndoorUnits
    { get { return indoorUnits.ToArray(); } }

    /// <summary>Gets the cooling-mode parameters and state (always present).</summary>
    public ModeParameters Cooling { get; } = new ModeParameters
    {
      MaxEvaporatingTemperature = 15,
      MinEvaporatingTemperature = NominalEvaporatingTemperature,
      MaxCondensingTemperature = NominalCondensingTemperature,
      MinCondensingTemperature = 25,
    };

    /// <summary>Gets the heating-mode parameters and state (null for cooling-only systems).</summary>
    public ModeParameters? Heating { get; private set; }

    IReadOnlyModeParameters IReadOnlyVRFSystem.Cooling => Cooling;

    IReadOnlyModeParameters? IReadOnlyVRFSystem.Heating => Heating;

    /// <summary>Gets the compressor electric power consumption [kW].</summary>
    public double CompressorElectricity { get; private set; }

    /// <summary>Gets the compression head [kW].</summary>
    public double CompressionHead { get; private set; }

    /// <summary>Gets the outdoor unit fan electric power [kW].</summary>
    public double OutdoorUnitFanElectricity
    {
      get
      {
        if (CurrentMode == Mode.Cooling) return Cooling.outdoorUnit.FanElectricity * ((double)ActiveOutdoorUnitCount / OutdoorUnitDivisionCount);
        else if (CurrentMode == Mode.Heating && Heating != null) return Heating.outdoorUnit.FanElectricity * ((double)ActiveOutdoorUnitCount / OutdoorUnitDivisionCount);
        else return 0;
      }
    }

    /// <summary>Gets the total indoor unit fan electric power [kW].</summary>
    public double IndoorUnitFanElectricity
    {
      get
      {
        if (CurrentMode == Mode.ShutOff) return 0;
        else
        {
          double ef = 0;
          for (int i = 0; i < indoorUnits.Count; i++) ef += indoorUnits[i].FanElectricity;
          return ef;
        }
      }
    }

    /// <summary>Refrigerant property calculator instance.</summary>
    private Refrigerant refrigerant;

    /// <summary>Gets or sets the superheat degree [°C].</summary>
    public double SuperHeatDegree { get; set; } = SUPER_HEAT_NOM;

    /// <summary>Gets or sets the subcooling degree [°C].</summary>
    public double SubCoolDegree { get; set; } = SUB_COOL_NOM;

    /// <summary>Gets or sets the equivalent pipe length [m].</summary>
    public double PipeLength { get; set; }

    /// <summary>Gets or sets the minimum partial load rate for capacity control [-].</summary>
    /// <remarks>Below this value, capacity is controlled by unit staging or on/off switching.</remarks>
    public double MinimumPartialLoadRatio { get; set; } = 0.15;

    /// <summary>Gets or sets the outdoor air dry-bulb temperature [°C].</summary>
    public double OutdoorAirDryBulbTemperature
    {
      get { return Cooling.outdoorUnit.InletAirTemperature; }
      set
      {
        Cooling.outdoorUnit.InletAirTemperature = value;
        if (Heating != null) Heating.outdoorUnit.InletAirTemperature = value;
      }
    }

    /// <summary>Gets or sets the outdoor air humidity ratio [kg/kg].</summary>
    public double OutdoorAirHumidityRatio
    {
      get { return Cooling.outdoorUnit.InletAirHumidityRatio; }
      set
      {
        Cooling.outdoorUnit.InletAirHumidityRatio = value;
        if (Heating != null) Heating.outdoorUnit.InletAirHumidityRatio = value;
      }
    }

    /// <summary>Gets the condensing pressure [kPa].</summary>
    public double CondensingPressure { get; private set; }

    /// <summary>Gets the condensing temperature [°C].</summary>
    public double CondensingTemperature { get; private set; }

    /// <summary>Gets the evaporating pressure [kPa].</summary>
    public double EvaporatingPressure { get; private set; }

    /// <summary>Gets the evaporating temperature [°C].</summary>
    public double EvaporatingTemperature { get; private set; }

    /// <summary>Gets the compressor inlet pressure [kPa].</summary>
    public double CompressorInletPressure { get; private set; }

    /// <summary>Gets the compressor outlet pressure [kPa].</summary>
    public double CompressorOutletPressure { get; private set; }

    /// <summary>Gets the compression ratio [-].</summary>
    public double CompressionRatio
    { get { return CompressorInletPressure == 0 ? 0 : CompressorOutletPressure / CompressorInletPressure; } }

    /// <summary>Gets the partial load rate [-].</summary>
    public double PartialLoadRatio { get; private set; }

    /// <summary>Gets or sets a value indicating whether water spray is applied to the outdoor unit.</summary>
    public bool UseWaterSpray
    {
      get { return Cooling.outdoorUnit.UseWaterSpray; }
      set { Cooling.outdoorUnit.UseWaterSpray = value; }
    }

    /// <summary>Gets or sets the installation height of indoor units relative to the outdoor unit [m].</summary>
    /// <remarks>A higher outdoor unit position reduces cooling capacity and increases heating capacity; lower position reverses this.</remarks>
    public double IndoorUnitHeight { get; set; }

    /// <summary>Gets a value indicating whether the system is in on/off (bang-bang) operation.</summary>
    public bool IsOnOffOperation
    { get { return PartialLoadRatio < MinimumPartialLoadRatio / OutdoorUnitDivisionCount; } }

    /// <summary>Gets or sets the number of outdoor unit divisions.</summary>
    public int OutdoorUnitDivisionCount
    {
      get { return numberOfOutdoorUnitDivisions; }
      set { numberOfOutdoorUnitDivisions = Math.Max(1, value); }
    }

    /// <summary>Number of outdoor unit divisions.</summary>
    private int numberOfOutdoorUnitDivisions = 1;

    /// <summary>Gets the number of operating outdoor unit modules.</summary>
    public int ActiveOutdoorUnitCount { private set; get; }

    #endregion

    #region 冷房・暖房運転関連のプロパティ

    /// <summary>
    /// Parameters and state specific to one operating mode (cooling or heating).
    /// </summary>
    /// <remarks>
    /// Accessed as <see cref="Cooling"/> for cooling mode and <see cref="Heating"/> for heating mode.
    /// For a cooling-only system, <see cref="Heating"/> is <c>null</c>.
    /// </remarks>
    public sealed class ModeParameters : IReadOnlyModeParameters
    {
      /// <summary>Outdoor unit heat exchanger (internal mutable reference).</summary>
      internal VRFUnit outdoorUnit = null!;

      /// <summary>Gets the outdoor unit (acts as condenser in cooling mode, evaporator in heating mode).</summary>
      public IReadOnlyVRFUnit OutdoorUnit => outdoorUnit;

      /// <summary>Gets the nominal capacity [kW] (negative for cooling, positive for heating).</summary>
      public double NominalCapacity { get; internal set; }

      /// <summary>Gets the nominal compression head [kW].</summary>
      public double NominalHead { get; internal set; }

      /// <summary>Gets the pipe resistance coefficient [1/m].</summary>
      public double PipeResistanceCoefficient { get; internal set; }

      /// <summary>Gets the compression head efficiency ratio at the nominal operating point [-].</summary>
      public double NominalEfficiency { get; internal set; }

      /// <summary>Gets coefficient A of the compression head efficiency ratio characteristic curve [-].</summary>
      public double HeadEfficiencyRatioCoefA { get; internal set; }

      /// <summary>Gets coefficient B of the compression head efficiency ratio characteristic curve [-].</summary>
      public double HeadEfficiencyRatioCoefB { get; internal set; }

      /// <summary>Gets the electric power consumption at the nominal operating point [kW].</summary>
      public double NominalElectricity { get; internal set; }

      /// <summary>Gets or sets the maximum evaporating temperature [°C].</summary>
      /// <remarks>
      /// In cooling mode, this is the primary upper bound for the evaporating temperature.
      /// In heating mode, this is a secondary boundary used when solving the condensing temperature.
      /// </remarks>
      public double MaxEvaporatingTemperature { get; set; }

      /// <summary>Gets or sets the minimum evaporating temperature [°C].</summary>
      /// <remarks>In cooling mode, this is the primary lower bound for the evaporating temperature.</remarks>
      public double MinEvaporatingTemperature { get; set; }

      /// <summary>Gets or sets the maximum condensing temperature [°C].</summary>
      /// <remarks>In heating mode, this is the primary upper bound for the condensing temperature.</remarks>
      public double MaxCondensingTemperature { get; set; }

      /// <summary>Gets or sets the minimum condensing temperature [°C].</summary>
      /// <remarks>
      /// In heating mode, this is the primary lower bound for the condensing temperature.
      /// In cooling mode, this is a secondary boundary used when solving the evaporating temperature.
      /// </remarks>
      public double MinCondensingTemperature { get; set; }
    }

    /// <summary>Gets or sets the target evaporating temperature for free-running calculation [°C].</summary>
    /// <remarks>Used only in cooling mode.</remarks>
    public double TargetEvaporatingTemperature { get; set; } = NominalEvaporatingTemperature;

    /// <summary>Gets or sets the target condensing temperature for free-running calculation [°C].</summary>
    /// <remarks>Used only in heating mode.</remarks>
    public double TargetCondensingTemperature { get; set; } = NominalCondensingTemperature;

    #endregion

    #endregion

    #region コンストラクタ

    /// <summary>Initializes a new instance as a heat-pump (cooling/heating switchable) machine.</summary>
    /// <param name="refrigerant">Refrigerant property calculator.</param>
    /// <param name="coolingOutdoorHexAirFlow">Outdoor unit air mass flow rate in cooling mode [kg/s].</param>
    /// <param name="coolingOutdoorHexFanElectricity">Outdoor unit fan electric power in cooling mode [kW].</param>
    /// <param name="coolingNominalCapacity">Nominal cooling capacity [kW] (negative).</param>
    /// <param name="coolingNominalElectricity">Nominal electric power in cooling mode [kW].</param>
    /// <param name="coolingMidLoadCapacity">Intermediate standard cooling capacity [kW].</param>
    /// <param name="coolingMidLoadElectricity">Electric power at intermediate standard cooling condition [kW].</param>
    /// <param name="coolingMidLoadMidTempCapacity">Intermediate mid-temperature cooling capacity [kW].</param>
    /// <param name="coolingMidLoadMidTempElectricity">Electric power at intermediate mid-temperature cooling condition [kW].</param>
    /// <param name="heatingOutdoorHexAirFlow">Outdoor unit air mass flow rate in heating mode [kg/s].</param>
    /// <param name="heatingOutdoorHexFanElectricity">Outdoor unit fan electric power in heating mode [kW].</param>
    /// <param name="heatingNominalCapacity">Nominal heating capacity [kW] (positive).</param>
    /// <param name="heatingNominalElectricity">Nominal electric power in heating mode [kW].</param>
    /// <param name="heatingMidLoadCapacity">Intermediate standard heating capacity [kW].</param>
    /// <param name="heatingMidLoadElectricity">Electric power at intermediate standard heating condition [kW].</param>
    /// <param name="nominalPipeLength">Nominal pipe length [m].</param>
    /// <param name="coolingLongPipeLength">Pipe length at which the cooling correction factor applies [m].</param>
    /// <param name="coolingPipeCorrectionFactor">Pipe length correction factor for cooling mode [-].</param>
    /// <param name="heatingLongPipeLength">Pipe length at which the heating correction factor applies [m].</param>
    /// <param name="heatingPipeCorrectionFactor">Pipe length correction factor for heating mode [-].</param>
    /// <param name="iHex">Indoor unit used to define rated operating conditions.</param>
    public VRFSystem(
      Refrigerant refrigerant,
      double coolingOutdoorHexAirFlow, double coolingOutdoorHexFanElectricity,
      double coolingNominalCapacity, double coolingNominalElectricity,
      double coolingMidLoadCapacity, double coolingMidLoadElectricity,
      double coolingMidLoadMidTempCapacity, double coolingMidLoadMidTempElectricity,
      double heatingOutdoorHexAirFlow, double heatingOutdoorHexFanElectricity,
      double heatingNominalCapacity, double heatingNominalElectricity,
      double heatingMidLoadCapacity, double heatingMidLoadElectricity,
      double nominalPipeLength,
      double coolingLongPipeLength, double coolingPipeCorrectionFactor,
      double heatingLongPipeLength, double heatingPipeCorrectionFactor,
      VRFUnit iHex) : this(
        refrigerant,
        coolingOutdoorHexAirFlow, coolingOutdoorHexFanElectricity,
        coolingNominalCapacity, coolingNominalElectricity, coolingMidLoadCapacity, coolingMidLoadElectricity, coolingMidLoadMidTempCapacity, coolingMidLoadMidTempElectricity,
        heatingOutdoorHexAirFlow, heatingOutdoorHexFanElectricity,
        heatingNominalCapacity, heatingNominalElectricity, heatingMidLoadCapacity, heatingMidLoadElectricity,
        nominalPipeLength,
        coolingLongPipeLength, coolingPipeCorrectionFactor,
        heatingLongPipeLength, heatingPipeCorrectionFactor,
        iHex, 0.0)
    { }

    /// <summary>Initializes a new instance as a heat-pump (cooling/heating switchable) machine.</summary>
    /// <param name="refrigerant">Refrigerant property calculator.</param>
    /// <param name="coolingOutdoorHexAirFlow">Outdoor unit air mass flow rate in cooling mode [kg/s].</param>
    /// <param name="coolingOutdoorHexFanElectricity">Outdoor unit fan electric power in cooling mode [kW].</param>
    /// <param name="coolingNominalCapacity">Nominal cooling capacity [kW] (negative).</param>
    /// <param name="coolingNominalElectricity">Nominal electric power in cooling mode [kW].</param>
    /// <param name="coolingMidLoadCapacity">Intermediate standard cooling capacity [kW].</param>
    /// <param name="coolingMidLoadElectricity">Electric power at intermediate standard cooling condition [kW].</param>
    /// <param name="heatingOutdoorHexAirFlow">Outdoor unit air mass flow rate in heating mode [kg/s].</param>
    /// <param name="heatingOutdoorHexFanElectricity">Outdoor unit fan electric power in heating mode [kW].</param>
    /// <param name="heatingNominalCapacity">Nominal heating capacity [kW] (positive).</param>
    /// <param name="heatingNominalElectricity">Nominal electric power in heating mode [kW].</param>
    /// <param name="heatingMidLoadCapacity">Intermediate standard heating capacity [kW].</param>
    /// <param name="heatingMidLoadElectricity">Electric power at intermediate standard heating condition [kW].</param>
    /// <param name="nominalPipeLength">Nominal pipe length [m].</param>
    /// <param name="coolingLongPipeLength">Pipe length at which the cooling correction factor applies [m].</param>
    /// <param name="coolingPipeCorrectionFactor">Pipe length correction factor for cooling mode [-].</param>
    /// <param name="heatingLongPipeLength">Pipe length at which the heating correction factor applies [m].</param>
    /// <param name="heatingPipeCorrectionFactor">Pipe length correction factor for heating mode [-].</param>
    /// <param name="iHex">Indoor unit used to define rated operating conditions.</param>
    public VRFSystem(
      Refrigerant refrigerant,
      double coolingOutdoorHexAirFlow, double coolingOutdoorHexFanElectricity,
      double coolingNominalCapacity, double coolingNominalElectricity,
      double coolingMidLoadCapacity, double coolingMidLoadElectricity,
      double heatingOutdoorHexAirFlow, double heatingOutdoorHexFanElectricity,
      double heatingNominalCapacity, double heatingNominalElectricity,
      double heatingMidLoadCapacity, double heatingMidLoadElectricity,
      double nominalPipeLength,
      double coolingLongPipeLength, double coolingPipeCorrectionFactor,
      double heatingLongPipeLength, double heatingPipeCorrectionFactor,
      VRFUnit iHex) : this(
        refrigerant,
        coolingOutdoorHexAirFlow, coolingOutdoorHexFanElectricity,
        coolingNominalCapacity, coolingNominalElectricity, coolingMidLoadCapacity, coolingMidLoadElectricity,
        heatingOutdoorHexAirFlow, heatingOutdoorHexFanElectricity,
        heatingNominalCapacity, heatingNominalElectricity, heatingMidLoadCapacity, heatingMidLoadElectricity,
        nominalPipeLength,
        coolingLongPipeLength, coolingPipeCorrectionFactor,
        heatingLongPipeLength, heatingPipeCorrectionFactor,
        iHex, 0.0)
    { }

    /// <summary>Initializes a new instance as a heat-pump (cooling/heating switchable) machine.</summary>
    /// <param name="refrigerant">Refrigerant property calculator.</param>
    /// <param name="coolingOutdoorHexAirFlow">Outdoor unit air mass flow rate in cooling mode [kg/s].</param>
    /// <param name="coolingOutdoorHexFanElectricity">Outdoor unit fan electric power in cooling mode [kW].</param>
    /// <param name="coolingNominalCapacity">Nominal cooling capacity [kW] (negative).</param>
    /// <param name="coolingNominalElectricity">Nominal electric power in cooling mode [kW].</param>
    /// <param name="coolingMidLoadCapacity">Intermediate standard cooling capacity [kW].</param>
    /// <param name="coolingMidLoadElectricity">Electric power at intermediate standard cooling condition [kW].</param>
    /// <param name="coolingMidLoadMidTempCapacity">Intermediate mid-temperature cooling capacity [kW].</param>
    /// <param name="coolingMidLoadMidTempElectricity">Electric power at intermediate mid-temperature cooling condition [kW].</param>
    /// <param name="heatingOutdoorHexAirFlow">Outdoor unit air mass flow rate in heating mode [kg/s].</param>
    /// <param name="heatingOutdoorHexFanElectricity">Outdoor unit fan electric power in heating mode [kW].</param>
    /// <param name="heatingNominalCapacity">Nominal heating capacity [kW] (positive).</param>
    /// <param name="heatingNominalElectricity">Nominal electric power in heating mode [kW].</param>
    /// <param name="heatingMidLoadCapacity">Intermediate standard heating capacity [kW].</param>
    /// <param name="heatingMidLoadElectricity">Electric power at intermediate standard heating condition [kW].</param>
    /// <param name="nominalPipeLength">Nominal pipe length [m].</param>
    /// <param name="coolingLongPipeLength">Pipe length at which the cooling correction factor applies [m].</param>
    /// <param name="coolingPipeCorrectionFactor">Pipe length correction factor for cooling mode [-].</param>
    /// <param name="heatingLongPipeLength">Pipe length at which the heating correction factor applies [m].</param>
    /// <param name="heatingPipeCorrectionFactor">Pipe length correction factor for heating mode [-].</param>
    /// <param name="iHex">Indoor unit used to define rated operating conditions.</param>
    /// <param name="totalEfficiency">Overall efficiency of the gas engine heat pump [-].</param>
    public VRFSystem(
      Refrigerant refrigerant,
      double coolingOutdoorHexAirFlow, double coolingOutdoorHexFanElectricity,
      double coolingNominalCapacity, double coolingNominalElectricity,
      double coolingMidLoadCapacity, double coolingMidLoadElectricity,
      double coolingMidLoadMidTempCapacity, double coolingMidLoadMidTempElectricity,
      double heatingOutdoorHexAirFlow, double heatingOutdoorHexFanElectricity,
      double heatingNominalCapacity, double heatingNominalElectricity,
      double heatingMidLoadCapacity, double heatingMidLoadElectricity,
      double nominalPipeLength,
      double coolingLongPipeLength, double coolingPipeCorrectionFactor,
      double heatingLongPipeLength, double heatingPipeCorrectionFactor,
      VRFUnit iHex, double totalEfficiency) : this(
        refrigerant, coolingOutdoorHexAirFlow, coolingOutdoorHexFanElectricity,
        coolingNominalCapacity, coolingNominalElectricity,
        coolingMidLoadCapacity, coolingMidLoadElectricity,
        coolingMidLoadMidTempCapacity, coolingMidLoadMidTempElectricity, nominalPipeLength,
        coolingLongPipeLength, coolingPipeCorrectionFactor, iHex)
    {

      InitHeatingModel(
        refrigerant, heatingOutdoorHexAirFlow, heatingOutdoorHexFanElectricity,
        heatingNominalCapacity, heatingNominalElectricity,
        heatingMidLoadCapacity, heatingMidLoadElectricity,
        nominalPipeLength, heatingLongPipeLength, heatingPipeCorrectionFactor,
        iHex, totalEfficiency);
    }

    /// <summary>Initializes a new instance as a heat-pump (cooling/heating switchable) machine.</summary>
    /// <param name="refrigerant">Refrigerant property calculator.</param>
    /// <param name="coolingOutdoorHexAirFlow">Outdoor unit air mass flow rate in cooling mode [kg/s].</param>
    /// <param name="coolingOutdoorHexFanElectricity">Outdoor unit fan electric power in cooling mode [kW].</param>
    /// <param name="coolingNominalCapacity">Nominal cooling capacity [kW] (negative).</param>
    /// <param name="coolingNominalElectricity">Nominal electric power in cooling mode [kW].</param>
    /// <param name="coolingMidLoadCapacity">Intermediate standard cooling capacity [kW].</param>
    /// <param name="coolingMidLoadElectricity">Electric power at intermediate standard cooling condition [kW].</param>
    /// <param name="heatingOutdoorHexAirFlow">Outdoor unit air mass flow rate in heating mode [kg/s].</param>
    /// <param name="heatingOutdoorHexFanElectricity">Outdoor unit fan electric power in heating mode [kW].</param>
    /// <param name="heatingNominalCapacity">Nominal heating capacity [kW] (positive).</param>
    /// <param name="heatingNominalElectricity">Nominal electric power in heating mode [kW].</param>
    /// <param name="heatingMidLoadCapacity">Intermediate standard heating capacity [kW].</param>
    /// <param name="heatingMidLoadElectricity">Electric power at intermediate standard heating condition [kW].</param>
    /// <param name="nominalPipeLength">Nominal pipe length [m].</param>
    /// <param name="coolingLongPipeLength">Pipe length at which the cooling correction factor applies [m].</param>
    /// <param name="coolingPipeCorrectionFactor">Pipe length correction factor for cooling mode [-].</param>
    /// <param name="heatingLongPipeLength">Pipe length at which the heating correction factor applies [m].</param>
    /// <param name="heatingPipeCorrectionFactor">Pipe length correction factor for heating mode [-].</param>
    /// <param name="iHex">Indoor unit used to define rated operating conditions.</param>
    /// <param name="totalEfficiency">Overall efficiency of the gas engine heat pump [-].</param>
    public VRFSystem(
      Refrigerant refrigerant,
      double coolingOutdoorHexAirFlow, double coolingOutdoorHexFanElectricity,
      double coolingNominalCapacity, double coolingNominalElectricity,
      double coolingMidLoadCapacity, double coolingMidLoadElectricity,
      double heatingOutdoorHexAirFlow, double heatingOutdoorHexFanElectricity,
      double heatingNominalCapacity, double heatingNominalElectricity,
      double heatingMidLoadCapacity, double heatingMidLoadElectricity,
      double nominalPipeLength,
      double coolingLongPipeLength, double coolingPipeCorrectionFactor,
      double heatingLongPipeLength, double heatingPipeCorrectionFactor,
      VRFUnit iHex, double totalEfficiency) : this(
        refrigerant, coolingOutdoorHexAirFlow, coolingOutdoorHexFanElectricity,
        coolingNominalCapacity, coolingNominalElectricity,
        coolingMidLoadCapacity, coolingMidLoadElectricity,
        nominalPipeLength, coolingLongPipeLength, coolingPipeCorrectionFactor, iHex)
    {
      InitHeatingModel(
        refrigerant, heatingOutdoorHexAirFlow, heatingOutdoorHexFanElectricity,
        heatingNominalCapacity, heatingNominalElectricity,
        heatingMidLoadCapacity, heatingMidLoadElectricity,
        nominalPipeLength, heatingLongPipeLength, heatingPipeCorrectionFactor,
        iHex, totalEfficiency);
    }

    /// <summary>Initializes the heating mode model parameters.</summary>
    /// <param name="refrigerant">Refrigerant property calculator.</param>
    /// <param name="heatingOutdoorHexAirFlow">Outdoor unit air mass flow rate in heating mode [kg/s].</param>
    /// <param name="heatingOutdoorHexFanElectricity">Outdoor unit fan electric power in heating mode [kW].</param>
    /// <param name="heatingNominalCapacity">Nominal heating capacity [kW] (positive).</param>
    /// <param name="heatingNominalElectricity">Nominal electric power in heating mode [kW].</param>
    /// <param name="heatingMidLoadCapacity">Intermediate standard heating capacity [kW].</param>
    /// <param name="heatingMidLoadElectricity">Electric power at intermediate standard heating condition [kW].</param>
    /// <param name="nominalPipeLength">Nominal pipe length [m].</param>
    /// <param name="heatingLongPipeLength">Pipe length at which the heating correction factor applies [m].</param>
    /// <param name="heatingPipeCorrectionFactor">Pipe length correction factor for heating mode [-].</param>
    /// <param name="iHex">Indoor unit used to define rated operating conditions.</param>
    /// <param name="totalEfficiency">Overall efficiency of the gas engine heat pump [-].</param>
    private void InitHeatingModel(
      Refrigerant refrigerant,
      double heatingOutdoorHexAirFlow, double heatingOutdoorHexFanElectricity,
      double heatingNominalCapacity, double heatingNominalElectricity,
      double heatingMidLoadCapacity, double heatingMidLoadElectricity,
      double nominalPipeLength,
      double heatingLongPipeLength, double heatingPipeCorrectionFactor,
      VRFUnit iHex, double totalEfficiency)
    {
      //プロパティ設定
      var heating = new ModeParameters
      {
        NominalCapacity = heatingNominalCapacity,
        NominalElectricity = heatingNominalElectricity,
        MaxEvaporatingTemperature = 25,
        MinEvaporatingTemperature = NominalEvaporatingTemperature,
        MaxCondensingTemperature = NominalCondensingTemperature,
        MinCondensingTemperature = 40,
      };
      Heating = heating;
      PipeLength = nominalPipeLength;
      TotalEfficiencyOfGasEngineHeatpump = totalEfficiency;

      //暖房定格運転時のパラメータ推定
      double pResist, nomHead;
      EstimateHeatingOutdoorUnitNominalParameters(
        refrigerant, heatingOutdoorHexAirFlow, heatingOutdoorHexFanElectricity,
        heatingNominalCapacity, totalEfficiency * heatingNominalElectricity, nominalPipeLength, heatingLongPipeLength, heatingPipeCorrectionFactor,
        out pResist, out nomHead, out VRFUnit heatingOutdoorUnit);
      heating.outdoorUnit = heatingOutdoorUnit;
      heating.PipeResistanceCoefficient = pResist;
      heating.NominalHead = nomHead;

      //部分負荷時の特性推定
      double midHead;
      EstimateHeatingPartialLoadParameters(
        refrigerant, nominalPipeLength, heating.PipeResistanceCoefficient, heating.NominalHead,
        heatingNominalCapacity, heating.outdoorUnit, iHex, totalEfficiency * heatingMidLoadElectricity, heatingMidLoadCapacity, out midHead);

      double cA, cB;
      MakePartialLoadCharacteristicCurve(
        heating.NominalHead, heatingNominalElectricity, midHead, heatingMidLoadElectricity, out cA, out cB);
      heating.HeadEfficiencyRatioCoefA = cA;
      heating.HeadEfficiencyRatioCoefB = cB;
      heating.NominalEfficiency = heating.NominalHead / heatingNominalElectricity;

      //入口空気をJIS定格標準条件に戻す
      heating.outdoorUnit.InletAirTemperature = JIS_OA_DBT_NOM_H;
      double oHmd = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndWetBulbTemperature(JIS_OA_DBT_NOM_H, JIS_OA_WBT_NOM_H, PhysicsConstants.StandardAtmosphericPressure);
      heating.outdoorUnit.InletAirHumidityRatio = oHmd;
    }

    /// <summary>Initializes a new instance as a cooling-only machine.</summary>
    /// <param name="refrigerant">Refrigerant property calculator.</param>
    /// <param name="coolingOutdoorHexAirFlow">Outdoor unit air mass flow rate in cooling mode [kg/s].</param>
    /// <param name="coolingOutdoorHexFanElectricity">Outdoor unit fan electric power in cooling mode [kW].</param>
    /// <param name="coolingNominalCapacity">Nominal cooling capacity [kW] (negative).</param>
    /// <param name="coolingNominalElectricity">Nominal electric power in cooling mode [kW].</param>
    /// <param name="coolingMidLoadCapacity">Intermediate standard cooling capacity [kW].</param>
    /// <param name="coolingMidLoadElectricity">Electric power at intermediate standard cooling condition [kW].</param>
    /// <param name="nominalPipeLength">Nominal pipe length [m].</param>
    /// <param name="coolingLongPipeLength">Pipe length at which the correction factor applies [m].</param>
    /// <param name="coolingPipeCorrectionFactor">Pipe length correction factor [-].</param>
    /// <param name="iHex">Indoor unit used to define rated operating conditions.</param>
    public VRFSystem(
      Refrigerant refrigerant,
      double coolingOutdoorHexAirFlow, double coolingOutdoorHexFanElectricity,
      double coolingNominalCapacity, double coolingNominalElectricity,
      double coolingMidLoadCapacity, double coolingMidLoadElectricity,
      double nominalPipeLength, double coolingLongPipeLength, double coolingPipeCorrectionFactor,
      VRFUnit iHex)
    {
      //プロパティ設定
      this.refrigerant = refrigerant;
      Cooling.NominalCapacity = coolingNominalCapacity;
      Cooling.NominalElectricity = coolingNominalElectricity;
      PipeLength = nominalPipeLength;

      //定格運転時のパラメータ推定
      double pResist, nomHead;
      EstimateCoolingOutdoorUnitNominalParameters(
        refrigerant, coolingOutdoorHexAirFlow, coolingOutdoorHexFanElectricity,
        coolingNominalCapacity, nominalPipeLength, coolingLongPipeLength, coolingPipeCorrectionFactor,
        out pResist, out nomHead, out VRFUnit coolingOutdoorUnit);
      Cooling.outdoorUnit = coolingOutdoorUnit;
      Cooling.PipeResistanceCoefficient = pResist;
      Cooling.NominalHead = nomHead;

      //部分負荷時の特性推定
      EstimateCoolingPartialLoadParameters(
        refrigerant, nominalPipeLength, Cooling.PipeResistanceCoefficient, Cooling.NominalHead,
        coolingNominalCapacity, Cooling.outdoorUnit, iHex, coolingMidLoadCapacity, out double midHead1);
      MakePartialLoadCharacteristicCurve(
        Cooling.NominalHead, coolingNominalElectricity, midHead1, coolingMidLoadElectricity, out double cA, out double cB);
      Cooling.HeadEfficiencyRatioCoefA = cA;
      Cooling.HeadEfficiencyRatioCoefB = cB;
      Cooling.NominalEfficiency = Cooling.NominalHead / coolingNominalElectricity;

      //入口空気をJIS定格標準条件に戻す
      Cooling.outdoorUnit.InletAirTemperature = JIS_OA_DBT_NOM_C;
      double oHmd = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndWetBulbTemperature(JIS_OA_DBT_NOM_C, JIS_OA_WBT_NOM_C, PhysicsConstants.StandardAtmosphericPressure);
      Cooling.outdoorUnit.InletAirHumidityRatio = oHmd;
    }

    /// <summary>Initializes a new instance as a cooling-only machine.</summary>
    /// <param name="refrigerant">Refrigerant property calculator.</param>
    /// <param name="coolingOutdoorHexAirFlow">Outdoor unit air mass flow rate in cooling mode [kg/s].</param>
    /// <param name="coolingOutdoorHexFanElectricity">Outdoor unit fan electric power in cooling mode [kW].</param>
    /// <param name="coolingNominalCapacity">Nominal cooling capacity [kW] (negative).</param>
    /// <param name="coolingNominalElectricity">Nominal electric power in cooling mode [kW].</param>
    /// <param name="coolingMidLoadCapacity">Intermediate standard cooling capacity [kW].</param>
    /// <param name="coolingMidLoadElectricity">Electric power at intermediate standard cooling condition [kW].</param>
    /// <param name="coolingMidLoadMidTempCapacity">Intermediate mid-temperature cooling capacity [kW].</param>
    /// <param name="coolingMidLoadMidTempElectricity">Electric power at intermediate mid-temperature cooling condition [kW].</param>
    /// <param name="nominalPipeLength">Nominal pipe length [m].</param>
    /// <param name="coolingLongPipeLength">Pipe length at which the correction factor applies [m].</param>
    /// <param name="coolingPipeCorrectionFactor">Pipe length correction factor [-].</param>
    /// <param name="iHex">Indoor unit used to define rated operating conditions.</param>
    public VRFSystem(
      Refrigerant refrigerant,
      double coolingOutdoorHexAirFlow, double coolingOutdoorHexFanElectricity,
      double coolingNominalCapacity, double coolingNominalElectricity,
      double coolingMidLoadCapacity, double coolingMidLoadElectricity,
      double coolingMidLoadMidTempCapacity, double coolingMidLoadMidTempElectricity,
      double nominalPipeLength, double coolingLongPipeLength, double coolingPipeCorrectionFactor,
      VRFUnit iHex)
    {
      //プロパティ設定
      this.refrigerant = refrigerant;
      Cooling.NominalCapacity = coolingNominalCapacity;
      Cooling.NominalElectricity = coolingNominalElectricity;
      PipeLength = nominalPipeLength;

      //定格運転時のパラメータ推定
      double pResist, nomHead;
      EstimateCoolingOutdoorUnitNominalParameters(
        refrigerant, coolingOutdoorHexAirFlow, coolingOutdoorHexFanElectricity,
        coolingNominalCapacity, nominalPipeLength, coolingLongPipeLength, coolingPipeCorrectionFactor,
        out pResist, out nomHead, out VRFUnit coolingOutdoorUnit);
      Cooling.outdoorUnit = coolingOutdoorUnit;
      Cooling.PipeResistanceCoefficient = pResist;
      Cooling.NominalHead = nomHead;

      //部分負荷時の特性推定
      double midHead1, midHead2;
      EstimateCoolingPartialLoadParameters(
        refrigerant, nominalPipeLength, Cooling.PipeResistanceCoefficient, Cooling.NominalHead,
        coolingNominalCapacity, Cooling.outdoorUnit, iHex, coolingMidLoadCapacity, coolingMidLoadMidTempCapacity, out midHead1, out midHead2);
      double cA, cB;
      MakePartialLoadCharacteristicCurve(
        Cooling.NominalHead, coolingNominalElectricity, midHead1, coolingMidLoadElectricity, midHead2, coolingMidLoadMidTempElectricity, out cA, out cB);
      Cooling.HeadEfficiencyRatioCoefA = cA;
      Cooling.HeadEfficiencyRatioCoefB = cB;
      Cooling.NominalEfficiency = Cooling.NominalHead / coolingNominalElectricity;

      //入口空気をJIS定格標準条件に戻す
      Cooling.outdoorUnit.InletAirTemperature = JIS_OA_DBT_NOM_C;
      double oHmd = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndWetBulbTemperature(JIS_OA_DBT_NOM_C, JIS_OA_WBT_NOM_C, PhysicsConstants.StandardAtmosphericPressure);
      Cooling.outdoorUnit.InletAirHumidityRatio = oHmd;
    }

    #endregion

    #region 状態更新処理および制御有り無し共通の補助関数

    /// <summary>Updates the system state for the current conditions and setpoints.</summary>
    /// <param name="controlHeatload">
    /// Whether to control the heat load.
    /// When false, the outlet state is determined by refrigerant temperature, airflow, and thermo-on/off state.
    /// </param>
    public void UpdateState(bool controlHeatload = true)
    {
      //屋外機停止時
      if (CurrentMode == Mode.ShutOff || CurrentMode == Mode.ThermoOff) UpdateNoLoad();
      //冷房時
      else if (CurrentMode == Mode.Cooling)
      {
        if (controlHeatload) UpdateCoolingStateWithControl();
        else UpdateCoolingStateWithoutControl();
      }
      //暖房時
      else
      {
        if (controlHeatload) UpdateHeatingStateWithControl();
        else UpdateHeatingStateWithoutControl();
      }
    }

    /// <summary>Handles the zero-load case (thermo-off or shut-off).</summary>
    private void UpdateNoLoad()
    {
      EvaporatingTemperature = Cooling.MaxEvaporatingTemperature;
      CondensingTemperature = Heating?.MinCondensingTemperature ?? Cooling.MinCondensingTemperature;
      PartialLoadRatio = CompressorElectricity = CompressionHead = 0;
      CompressorInletPressure = CompressorOutletPressure = 0;

      Cooling.outdoorUnit.ThermoOff();
      Heating?.outdoorUnit.ThermoOff();

      //室内機
      for (int i = 0; i < indoorUnits.Count; i++)
        indoorUnits[i].ThermoOff();
    }

    /// <summary>Computes the compression head error [kW] for a given assumed value (used in cooling mode convergence).</summary>
    /// <param name="headAssumption">Assumed compression head [kW].</param>
    /// <param name="coolingLoad">Cooling load [kW] (negative).</param>
    /// <param name="compressorInletEnthalpy">Compressor inlet specific enthalpy [kJ/kg].</param>
    /// <param name="compressorInletDensity">Compressor inlet refrigerant density [kg/m³].</param>
    /// <param name="evaporatingPressure">Evaporating pressure [kPa].</param>
    /// <param name="condensingPressure">Output: condensing pressure [kPa].</param>
    /// <returns>Compression head error [kW].</returns>
    private double CalcCoolingHeadError
      (double headAssumption, double coolingLoad, double compressorInletEnthalpy, double compressorInletDensity,
      double evaporatingPressure, out double condensingPressure)
    {
      //必要凝縮圧力の計算
      double qCnd = -coolingLoad + headAssumption;
      VRFUnit oUnt = Cooling.outdoorUnit;
      oUnt.SolveHeatLoad(qCnd, oUnt.NominalAirFlowRate, oUnt.InletAirTemperature, oUnt.InletAirHumidityRatio, false);

      //凝縮器出口冷媒状態の計算
      refrigerant.GetSaturatedPropertyFromTemperature(oUnt.RefrigerantTemperature + KTOC,
        out double _, out double _, out condensingPressure);
      if (refrigerant.MaxPressure < condensingPressure)
        return condensingPressure - refrigerant.MaxPressure; //冷媒計算可能範囲を上回る場合には過剰分を誤差として出力
      //圧縮比の制限
      refrigerant.GetSaturatedPropertyFromTemperature(Cooling.MinCondensingTemperature + KTOC, out _, out _, out double minCondensingPressure);
      minCondensingPressure = Math.Max(minCondensingPressure, evaporatingPressure * MIN_COMPRESSION_RATIO);
      if (condensingPressure < minCondensingPressure)
      {
        condensingPressure = minCondensingPressure;
        refrigerant.GetSaturatedPropertyFromPressure(condensingPressure, out _, out _, out double cndT);
        oUnt.UpdateWithRefrigerantTemperature
        (cndT - KTOC, oUnt.NominalAirFlowRate, oUnt.InletAirTemperature, oUnt.InletAirHumidityRatio, false);
        oUnt.FanOperatingRatio = qCnd / oUnt.HeatTransfer; //2022.01.14 Debug
      }
      CompressorOutletPressure = condensingPressure;
      refrigerant.GetStateFromPressureAndTemperature(condensingPressure, oUnt.RefrigerantTemperature + KTOC - SubCoolDegree,
        out _, out _, out double oUnitOutletEnthalpy, out _);

      //冷媒循環量の計算
      double mR = -coolingLoad / (compressorInletEnthalpy - oUnitOutletEnthalpy);
      double vR = mR / compressorInletDensity;

      //圧力損失[kPa]の計算
      double dP = Cooling.PipeResistanceCoefficient * mR * vR * PipeLength;
      dP += 0.001 * IndoorUnitHeight * 9.8 * compressorInletDensity;

      //圧縮ヘッド[kW]の計算
      CompressorInletPressure = Math.Max(evaporatingPressure - dP, refrigerant.MinPressure);
      refrigerant.GetStateFromPressureAndEnthalpy(CompressorInletPressure, compressorInletEnthalpy,
        out double tmp, out double rhoVap, out _, out _);
      double kappa = refrigerant.GetSpecificHeatRatioFromTemperatureAndDensity(tmp, rhoVap);
      double kp2 = kappa / (kappa - 1);
      double head2 = kp2 * CompressorInletPressure * (mR / rhoVap) * (Math.Pow(condensingPressure / CompressorInletPressure, 1d / kp2) - 1);

      return head2 - headAssumption;
    }

    /// <summary>Computes the compression head error [kW] for an assumed evaporating temperature (cooling mode convergence).</summary>
    /// <param name="evaporatingTemperature">Evaporating temperature [°C].</param>
    /// <param name="controlIndoorUnits">True to control indoor unit airflow (must not be changed during free-running calculation).</param>
    /// <param name="evaporatingPressure">Output: evaporating pressure [kPa].</param>
    /// <returns>Compression head error [kW]; a positive value means the nominal head is insufficient.</returns>
    private double CalcEvaporatingTemperatureError(double evaporatingTemperature, bool controlIndoorUnits, out double evaporatingPressure)
    {
      //蒸発温度にもとづいて屋内機を更新
      refrigerant.GetSaturatedPropertyFromTemperature(evaporatingTemperature + KTOC, out _, out _, out evaporatingPressure);
      double qEvpSum = 0;
      for (int i = 0; i < indoorUnits.Count; i++)
      {
        VRFUnit unt = indoorUnits[i];
        if (controlIndoorUnits) unt.UpdateWithRefrigerantTemperature
            (evaporatingTemperature, unt.NominalAirFlowRate, unt.InletAirTemperature, unt.InletAirHumidityRatio, true, ControlThermoOffWithSensibleHeat);
        else unt.UpdateWithRefrigerantTemperature(evaporatingTemperature, false);

        qEvpSum += unt.HeatTransfer;
      }
      //蒸発器出口比エンタルピーの計算
      refrigerant.GetStateFromPressureAndTemperature(evaporatingPressure, evaporatingTemperature + KTOC + SuperHeatDegree,
        out _, out double iUnitOutletDensity2, out double iUnitOutletEnthalpy2, out _);

      //必要凝縮圧力の計算
      double qCnd = -qEvpSum + Cooling.NominalHead;
      VRFUnit oUnt = Cooling.outdoorUnit;
      oUnt.SolveHeatLoad(qCnd, oUnt.NominalAirFlowRate, oUnt.InletAirTemperature, oUnt.InletAirHumidityRatio, false);
      //2025.01.19: 臨界点近傍で冷媒物性計算が不安定になることに対応
      if (refrigerant.CriticalTemperature * 0.98 < oUnt.RefrigerantTemperature + KTOC)
        return (oUnt.RefrigerantTemperature + KTOC) - refrigerant.CriticalTemperature * 0.98; //冷媒の計算範囲外に飛び出した場合には、不足分を誤差として出力

      //凝縮器出口冷媒状態の計算
      refrigerant.GetSaturatedPropertyFromTemperature(oUnt.RefrigerantTemperature + KTOC,
        out _, out _, out double cndPressure);
      if (refrigerant.MaxPressure < cndPressure)
        return cndPressure - refrigerant.MaxPressure; //冷媒の計算範囲外に飛び出した場合には、不足分を誤差として出力
      CompressorOutletPressure = cndPressure;
      refrigerant.GetStateFromPressureAndTemperature(CompressorOutletPressure, oUnt.RefrigerantTemperature + KTOC - SubCoolDegree,
        out _, out _, out double oUnitOutletEnthalpy, out _);

      //冷媒循環量の計算
      double mR = -qEvpSum / (iUnitOutletEnthalpy2 - oUnitOutletEnthalpy);
      double vR = mR / iUnitOutletDensity2;

      //圧力損失[kPa]の計算
      double dP = Cooling.PipeResistanceCoefficient * mR * vR * PipeLength;
      dP += 0.001 * IndoorUnitHeight * 9.8 * iUnitOutletDensity2;

      //圧縮ヘッド[kW]の計算
      if (evaporatingPressure - dP < refrigerant.MinPressure)
        return refrigerant.MinPressure - (evaporatingPressure - dP); //冷媒の計算範囲外に飛び出した場合には、不足分を誤差として出力
      CompressorInletPressure = evaporatingPressure - dP;
      refrigerant.GetStateFromPressureAndEnthalpy(CompressorInletPressure, iUnitOutletEnthalpy2,
        out double tmp, out double rhoVap, out _, out _);
      double kappa = refrigerant.GetSpecificHeatRatioFromTemperatureAndDensity(tmp, rhoVap);
      double kp2 = kappa / (kappa - 1);
      double head2 = kp2 * CompressorInletPressure * (mR / rhoVap) * (Math.Pow(CompressorOutletPressure / CompressorInletPressure, 1d / kp2) - 1);

      return head2 - Cooling.NominalHead;
    }

    /// <summary>Computes the electric power [kW] from the compression head [kW] accounting for partial load characteristics.</summary>
    /// <param name="head">Compression head [kW].</param>
    private void CalculateCoolingElectricity(double head)
    {
      PartialLoadRatio = head / Cooling.NominalHead; //システムとしての負荷率

      //ユニットの運転台数と負荷率を確認
      int nOP;
      for (nOP = OutdoorUnitDivisionCount; 1 < nOP; nOP--)
        if ((double)nOP / OutdoorUnitDivisionCount * MinimumPartialLoadRatio < PartialLoadRatio)
          break;
      ActiveOutdoorUnitCount = nOP;
      double plUnit = PartialLoadRatio * OutdoorUnitDivisionCount / ActiveOutdoorUnitCount;

      double eRate;
      //連続運転
      if (MinimumPartialLoadRatio <= plUnit)
        eRate = Cooling.HeadEfficiencyRatioCoefA * plUnit + Cooling.HeadEfficiencyRatioCoefB;
      //発停運転
      else
      {
        double eMin = Cooling.HeadEfficiencyRatioCoefA * MinimumPartialLoadRatio + Cooling.HeadEfficiencyRatioCoefB;
        double rme = eMin * MIN_ER_RATE;
        eRate = (eMin - rme) / MinimumPartialLoadRatio * plUnit + rme;
      }
      CompressionHead = head;
      CompressorElectricity = head / (eRate * Cooling.NominalEfficiency);
    }

    /// <summary>Computes the compression head error [kW] for a given assumed value (used in heating mode convergence).</summary>
    /// <param name="headAssumption">Assumed compression head [kW].</param>
    /// <param name="heatingLoad">Heating load [kW].</param>
    /// <param name="iUnitOutletEnthalpy">Indoor unit outlet specific enthalpy [kJ/kg].</param>
    /// <param name="evaporatingPressure">Output: evaporating pressure [kPa].</param>
    /// <param name="condensingPressure">Output: condensing pressure [kPa].</param>
    /// <returns>Compression head error [kW].</returns>
    private double CalcHeatingHeadError
      (double headAssumption, double heatingLoad, double iUnitOutletEnthalpy,
      out double evaporatingPressure, ref double condensingPressure)
    {
      //廃熱回収を考慮
      double qRcv = 0;
      if (IsGasEngineHeatpump)
      {
        double effHd = CalculateHeatingHeadEfficiency(headAssumption);
        qRcv = headAssumption * (TotalEfficiencyOfGasEngineHeatpump - effHd) / effHd;
      }

      //必要蒸発圧力の計算
      double qEvp = Math.Max(0, heatingLoad - headAssumption);
      VRFUnit oUnt = Heating!.outdoorUnit;
      //屋外機の処理熱量計算時には廃熱回収を差し引く（冷媒循環量計算時はひいては駄目）
      oUnt.SolveHeatLoad(-Math.Max(0, qEvp - qRcv), oUnt.NominalAirFlowRate, oUnt.InletAirTemperature, oUnt.InletAirHumidityRatio, true); //デフロストを反映

      //蒸発器出口冷媒状態の計算
      refrigerant.GetSaturatedPropertyFromTemperature(oUnt.RefrigerantTemperature + KTOC, out _, out _, out evaporatingPressure);
      if (evaporatingPressure < refrigerant.MinPressure)
        return refrigerant.MinPressure - evaporatingPressure; //冷媒計算可能範囲を上回る場合には過剰分を誤差として出力
      //圧縮比の制限
      refrigerant.GetSaturatedPropertyFromTemperature(Heating!.MaxEvaporatingTemperature + KTOC, out _, out _, out double maxEvaporatingPressure);
      maxEvaporatingPressure = Math.Min(maxEvaporatingPressure, condensingPressure / MIN_COMPRESSION_RATIO);
      if (maxEvaporatingPressure < evaporatingPressure)
      {
        evaporatingPressure = maxEvaporatingPressure;
        refrigerant.GetSaturatedPropertyFromPressure(evaporatingPressure, out _, out _, out double evpT);
        oUnt.UpdateWithRefrigerantTemperature
        (evpT - KTOC, oUnt.NominalAirFlowRate, oUnt.InletAirTemperature, oUnt.InletAirHumidityRatio, true);
        oUnt.FanOperatingRatio = oUnt.HeatTransfer == 0 ? 0 : qEvp / oUnt.HeatTransfer; //2022.01.14 Debug
      }
      CompressorInletPressure = evaporatingPressure;
      refrigerant.GetStateFromPressureAndTemperature(evaporatingPressure, oUnt.RefrigerantTemperature + KTOC + SuperHeatDegree,
        out _, out double rhoCmpIn, out double oUnitOutletEnthalpy, out _);

      //冷媒循環量の計算
      double mR = (qEvp + oUnt.DefrostLoad) / (oUnitOutletEnthalpy - iUnitOutletEnthalpy);

      //圧縮機出口状態の計算
      double hCmpOut = oUnitOutletEnthalpy + headAssumption / mR;
      double kappa = refrigerant.GetSpecificHeatRatioFromTemperatureAndDensity(
        oUnt.RefrigerantTemperature + KTOC + SuperHeatDegree, rhoCmpIn);
      CompressorOutletPressure = GetHighPressure(headAssumption, kappa, mR / rhoCmpIn, evaporatingPressure);
      //極低負荷の場合に冷媒圧力と比エンタルピーが以上上昇することを回避
      CompressorOutletPressure = Math.Min(4000, CompressorOutletPressure);
      hCmpOut = Math.Min(500, hCmpOut);
      refrigerant.GetStateFromPressureAndEnthalpy(Math.Min(CompressorOutletPressure, condensingPressure), hCmpOut,
        out _, out double rhoVap, out _, out _); //圧力降下後の冷媒密度で計算

      //凝縮圧力の誤差を出力
      double pCnd2 = CompressorOutletPressure
      - Heating!.PipeResistanceCoefficient * mR * (mR / rhoVap) * PipeLength
      + 0.001 * IndoorUnitHeight * 9.8 * rhoVap;
      return condensingPressure - pCnd2;
    }

    /// <summary>Computes the compression head error [kW] for an assumed condensing temperature (heating mode convergence).</summary>
    /// <param name="condensingTemperature">Assumed condensing temperature [°C].</param>
    /// <param name="heatRecovery">Heat recovery [kW].</param>
    /// <param name="heatingLoad">Heating load [kW].</param>
    /// <param name="condensingPressure">Output: condensing pressure [kPa].</param>
    /// <param name="evaporatingPressure">Output: evaporating pressure [kPa].</param>
    /// <returns>Compression head error [kW]; a positive value means the nominal head is insufficient.</returns>
    private double CalcCondensingTemperatureError
      (double condensingTemperature, double heatRecovery,
      out double heatingLoad, out double condensingPressure, out double evaporatingPressure)
    {
      //凝縮温度にもとづいて屋内機を更新
      refrigerant.GetSaturatedPropertyFromTemperature(condensingTemperature + KTOC,
        out _, out _, out condensingPressure);
      heatingLoad = 0;
      for (int i = 0; i < indoorUnits.Count; i++)
      {
        VRFUnit unt = indoorUnits[i];
        unt.UpdateWithRefrigerantTemperature
        (condensingTemperature, unt.NominalAirFlowRate, unt.InletAirTemperature, unt.InletAirHumidityRatio, false);

        heatingLoad += unt.HeatTransfer;
      }
      //凝縮器出口比エンタルピーの計算
      refrigerant.GetStateFromPressureAndTemperature(condensingPressure, condensingTemperature + KTOC - SubCoolDegree,
        out _, out _, out double iUnitOutletEnthalpy2, out _);

      //必要蒸発圧力の計算
      double qEvp = heatingLoad - Heating!.NominalHead;
      if (qEvp < 0) //ヘッドだけで負荷が賄える場合
      {
        evaporatingPressure = refrigerant.MinPressure;
        return qEvp; //2023.07.28
      }
      VRFUnit oUnt = Heating!.outdoorUnit;
      //屋外機の処理熱量計算時には廃熱回収を差し引く（冷媒循環量計算時は引いては駄目）
      oUnt.SolveHeatLoad(-Math.Max(0, qEvp - heatRecovery), oUnt.NominalAirFlowRate, oUnt.InletAirTemperature, oUnt.InletAirHumidityRatio, true);

      //蒸発器出口冷媒状態の計算
      refrigerant.GetSaturatedPropertyFromTemperature(oUnt.RefrigerantTemperature + KTOC,
        out _, out _, out evaporatingPressure);
      if (evaporatingPressure < refrigerant.MinPressure)
        return refrigerant.MinPressure - evaporatingPressure; //冷媒の計算範囲外に飛び出した場合には、不足分を誤差として出力
      CompressorInletPressure = evaporatingPressure;
      refrigerant.GetStateFromPressureAndTemperature(evaporatingPressure, oUnt.RefrigerantTemperature + KTOC + SuperHeatDegree,
        out _, out double rhoCmpIn, out double oUnitOutletEnthalpy, out _);

      //圧縮機出口比エンタルピー[kJ/kg]の計算
      double mR = (Math.Max(0, qEvp + oUnt.DefrostLoad)) / (oUnitOutletEnthalpy - iUnitOutletEnthalpy2); //2023.04.11 Bugfix:圧縮ヘッドのみで負荷がまかなえてしまう場合に収束計算エラーが発生したため
      double hCmpOut = oUnitOutletEnthalpy + Math.Min(Heating!.NominalHead / mR, oUnitOutletEnthalpy - iUnitOutletEnthalpy2); //2023.04.11: 低負荷時に極めて大きい比エンタルピーになる場合があったため、圧縮機によるh上昇は蒸発器のΔh以下とした。収束計算破綻回避処理である、物理的な意味はない。

      //圧縮機出口圧力[kPa]の計算
      refrigerant.GetStateFromPressureAndEnthalpy(condensingPressure, hCmpOut,
        out _, out double rhoVap, out _, out _);  //圧力降下後の冷媒密度で計算
      CompressorOutletPressure = condensingPressure
      + Heating!.PipeResistanceCoefficient * mR * (mR / rhoVap) * PipeLength
      - 0.001 * IndoorUnitHeight * 9.8 * rhoVap;

      //圧縮ヘッド[kW]の計算
      double kappa = refrigerant.GetSpecificHeatRatioFromTemperatureAndDensity(
        oUnt.RefrigerantTemperature + KTOC + SuperHeatDegree, rhoCmpIn);
      double kp2 = kappa / (kappa - 1);
      double head2 = kp2 * evaporatingPressure * (mR / rhoCmpIn) * (Math.Pow(CompressorOutletPressure / evaporatingPressure, 1d / kp2) - 1);

      return head2 - Heating!.NominalHead;
    }

    /// <summary>Computes the electric power [kW] from the compression head [kW] accounting for partial load characteristics.</summary>
    /// <param name="head">Compression head [kW].</param>
    private void CalculateHeatingElectricity(double head)
    {
      CompressorElectricity = head / CalculateHeatingHeadEfficiency(head);
      CompressionHead = head;
    }

    /// <summary>Computes the head efficiency ratio [-] from the compression head [kW].</summary>
    /// <param name="head">Compression head [kW].</param>
    /// <returns>Compression head efficiency ratio [-].</returns>
    private double CalculateHeatingHeadEfficiency(double head)
    {
      PartialLoadRatio = head / Heating!.NominalHead; //システムとしての負荷率

      //ユニットの運転台数と負荷率を確認
      int nOP;
      for (nOP = OutdoorUnitDivisionCount; 1 < nOP; nOP--)
        if ((double)nOP / OutdoorUnitDivisionCount * MinimumPartialLoadRatio < PartialLoadRatio)
          break;
      ActiveOutdoorUnitCount = nOP;
      double plUnit = PartialLoadRatio * OutdoorUnitDivisionCount / ActiveOutdoorUnitCount;

      double eRate;
      //連続運転
      if (MinimumPartialLoadRatio <= plUnit)
        eRate = Heating!.HeadEfficiencyRatioCoefA * plUnit + Heating!.HeadEfficiencyRatioCoefB;
      //発停運転
      else
      {
        double eMin = Heating!.HeadEfficiencyRatioCoefA * MinimumPartialLoadRatio + Heating!.HeadEfficiencyRatioCoefB;
        double rme = eMin * MIN_ER_RATE;
        eRate = (eMin - rme) / MinimumPartialLoadRatio * plUnit + rme;
      }
      return eRate * Heating!.NominalEfficiency;
    }

    #endregion

    #region 制御有の場合の状態更新処理（熱負荷ないしは給気温度を指定）

    /// <summary>Performs the cooling mode state update with setpoint control.</summary>
    private void UpdateCoolingStateWithControl()
    {
      //屋内機の計算
      double lmtTemp = Cooling.MaxEvaporatingTemperature;
      double qEvpSum = 0;
      double[] rfTemps = new double[indoorUnits.Count];
      for (int i = 0; i < indoorUnits.Count; i++)
      {
        VRFUnit unt = indoorUnits[i];
        unt.ControlOutletAirTemperatureWithRefrigerantTemperature
          (unt.NominalAirFlowRate, unt.InletAirTemperature, unt.InletAirHumidityRatio);
        rfTemps[i] = unt.RefrigerantTemperature;

        lmtTemp = Math.Min(lmtTemp, unt.RefrigerantTemperature);  //最大負荷系統の冷媒温度を保存
        qEvpSum += unt.HeatTransfer;
      }
      lmtTemp = Math.Max(lmtTemp, Cooling.MinEvaporatingTemperature);

      //熱負荷が無い場合にはサーモオフして終了
      if (0 <= qEvpSum)
      {
        UpdateNoLoad();
        return;
      }

      //蒸発器出口冷媒状態の計算
      refrigerant.GetSaturatedPropertyFromTemperature
        (lmtTemp + KTOC, out _, out _, out double evpPressure);
      refrigerant.GetStateFromPressureAndTemperature(evpPressure, lmtTemp + KTOC + SuperHeatDegree,
        out _, out double iUnitOutletDensity, out double iUnitOutletEnthalpy, out _);

      //圧縮ヘッドの収束計算用誤差関数*****************************
      double cndPressure = 0;
      Roots.ErrorFunction eFncHead = delegate (double head)
      {
        return CalcCoolingHeadError
        (head, qEvpSum, iUnitOutletEnthalpy, iUnitOutletDensity, evpPressure, out cndPressure);
      };

      //蒸発温度の収束計算用誤差関数*****************************
      Roots.ErrorFunction eFncEvpTmp = delegate (double evpT)
      {
        return CalcEvaporatingTemperatureError(evpT, true, out evpPressure);
      };

      //過負荷判定
      bool overLoad = 0 < eFncHead(Cooling.NominalHead);
      //過負荷の場合には蒸発温度を収束計算
      if (overLoad)
      {
        //蒸発温度を上げれば定格圧縮動力で冷凍サイクルが形成できる場合
        if (0 <= eFncEvpTmp(lmtTemp))
        {
          Roots.Bisection(eFncEvpTmp, lmtTemp, 30, 0.001, 0.001, 20);
          CompressorElectricity = Cooling.NominalElectricity;
          CompressionHead = Cooling.NominalHead;
          PartialLoadRatio = 1.0;
        }
        //蒸発温度を低くしないと成立しない場合
        else
        {
          //ヘッドを調整して熱量は合わせられる場合
          if (eFncHead(0) < 0)
          {
            double hd = Roots.Bisection(eFncHead, 0, Cooling.NominalHead, 0.001, 0.001, 20);
            eFncHead(hd - 0.001); //大きい側で収束が終わって圧力が過剰に高くなる場合があったための回避処理。良くないプログラム
            CalculateCoolingElectricity(hd);
          }
          //成立しない場合
          else
          {
            eFncHead(Cooling.NominalHead);
            CompressorElectricity = Cooling.NominalElectricity;
            CompressionHead = Cooling.NominalHead;
            PartialLoadRatio = 1.0;
          }
        }
      }
      //軽負荷の場合にはヘッドを収束計算
      else
      {
        double hd = Roots.Bisection(eFncHead, 0, Cooling.NominalHead, 0.001, 0.001, 20);
        eFncHead(hd - 0.001); //大きい側で収束が終わって圧力が過剰に高くなる場合があったための回避処理。良くないプログラム
        CalculateCoolingElectricity(hd);
      }

      //屋内機のサーモオフ時間を調整
      for (int i = 0; i < indoorUnits.Count; i++)
      {
        VRFUnit unt = indoorUnits[i];
        unt.UpdateWithRefrigerantTemperature
          (lmtTemp, unt.NominalAirFlowRate, unt.InletAirTemperature, unt.InletAirHumidityRatio, true, ControlThermoOffWithSensibleHeat);
      }

      //プロパティ書き込み
      EvaporatingPressure = evpPressure;
      CondensingPressure = cndPressure;
      EvaporatingTemperature = indoorUnits[0].RefrigerantTemperature;
      CondensingTemperature = Cooling.outdoorUnit.RefrigerantTemperature;
    }

    /// <summary>Performs the heating mode state update with setpoint control.</summary>
    private void UpdateHeatingStateWithControl()
    {
      //屋内機の計算
      double lmtTemp = Heating!.MinCondensingTemperature;
      double qCndSum = 0;
      double[] rfTemps = new double[indoorUnits.Count];
      for (int i = 0; i < indoorUnits.Count; i++)
      {
        VRFUnit unt = indoorUnits[i];
        unt.ControlOutletAirTemperatureWithRefrigerantTemperature
          (unt.NominalAirFlowRate, unt.InletAirTemperature, unt.InletAirHumidityRatio);
        rfTemps[i] = unt.RefrigerantTemperature;

        lmtTemp = Math.Max(lmtTemp, unt.RefrigerantTemperature);  //最大負荷系統の冷媒温度を保存
        qCndSum += unt.HeatTransfer;
      }
      lmtTemp = Math.Min(Heating!.MaxCondensingTemperature, lmtTemp);

      //熱負荷が無い場合にはサーモオフして終了
      if (qCndSum <= 0)
      {
        UpdateNoLoad();
        return;
      }

      //凝縮器出口冷媒状態の計算
      refrigerant.GetSaturatedPropertyFromTemperature
        (lmtTemp + KTOC, out _, out _, out double cndPressure);
      refrigerant.GetStateFromPressureAndTemperature(cndPressure, lmtTemp + KTOC - SubCoolDegree,
        out _, out _, out double iUnitOutletEnthalpy, out _);

      //定格廃熱回収
      double qRcvN = 0;
      if (IsGasEngineHeatpump)
      {
        double effHd = CalculateHeatingHeadEfficiency(Heating!.NominalHead);
        qRcvN = Heating!.NominalHead * (TotalEfficiencyOfGasEngineHeatpump - effHd) / effHd;
      }

      //圧縮ヘッドの収束計算用誤差関数*****************************
      double evpPressure = 0;
      Roots.ErrorFunction eFncHead = delegate (double head)
      {
        return CalcHeatingHeadError
        (head, qCndSum, iUnitOutletEnthalpy, out evpPressure, ref cndPressure);
      };

      //蒸発圧力の収束計算用誤差関数*****************************
      Roots.ErrorFunction eFncCndTmp = delegate (double cndT)
      {
        return CalcCondensingTemperatureError
        (cndT, qRcvN, out qCndSum, out cndPressure, out evpPressure);
      };

      //過負荷判定
      bool overLoad;
      if (qCndSum < Heating!.NominalHead + qRcvN) overLoad = false;
      else overLoad = 0 < eFncHead(Heating!.NominalHead);
      //過負荷の場合には凝縮温度を収束計算
      if (overLoad)
      {
        //凝縮温度を下げれば定格圧縮動力で冷凍サイクルが形成できる場合
        if (0 <= eFncCndTmp(lmtTemp))
        {
          lmtTemp = Roots.Bisection(eFncCndTmp, 15, lmtTemp, 0.001, 0.001, 20);
          CompressorElectricity = Heating!.NominalElectricity;
          CompressionHead = Heating!.NominalHead;
          PartialLoadRatio = 1.0;
          eFncCndTmp(lmtTemp);
        }
        //凝縮温度を上げないと成立しない場合
        else
        {
          //ヘッドを調整して熱量は合わせられる場合
          if (eFncHead(0) < 0)
          {
            double hd = Roots.Bisection(eFncHead, 0, Math.Min(qCndSum, Heating!.NominalHead), 0.001, 0.001, 20);
            eFncHead(hd - 0.001); //大きい側で収束が終わって圧力が過剰に高くなる場合があったための回避処理。良くないプログラム
            CalculateHeatingElectricity(hd);
          }
          //成立しない場合
          else
          {
            eFncHead(Heating!.NominalHead);
            CompressorElectricity = Heating!.NominalElectricity;
            CompressionHead = Heating!.NominalHead;
            PartialLoadRatio = 1.0;
          }
        }
      }
      //軽負荷の場合にはヘッドを収束計算
      else
      {
        double hd = Roots.Bisection(eFncHead, 0, Math.Min(qCndSum, Heating!.NominalHead), 0.001, 0.001, 20);
        eFncHead(hd - 0.001); //大きい側で収束が終わって圧力が過剰に高くなる場合があったための回避処理。良くないプログラム
        CalculateHeatingElectricity(hd);
      }

      //屋内機のサーモオフ時間を調整
      for (int i = 0; i < indoorUnits.Count; i++)
      {
        VRFUnit unt = indoorUnits[i];
        unt.UpdateWithRefrigerantTemperature
          (lmtTemp, unt.NominalAirFlowRate, unt.InletAirTemperature, unt.InletAirHumidityRatio, true);
      }

      //プロパティ書き込み
      EvaporatingPressure = evpPressure;
      CondensingPressure = cndPressure;
      EvaporatingTemperature = Heating!.outdoorUnit.RefrigerantTemperature;
      CondensingTemperature = indoorUnits[0].RefrigerantTemperature;
    }

    #endregion

    #region 制御無の場合の状態更新処理（冷媒温度、室内機風量、サーモOn/Off状態を指定：成り行き）

    /// <summary>Performs the cooling mode state update in free-running mode.</summary>
    private void UpdateCoolingStateWithoutControl()
    {
      //成り行き計算の場合には勝手にファンを落とさない
      for (int i = 0; i < indoorUnits.Count; i++)
        indoorUnits[i].ShutoffFanWhenThermoOff = false;

      //蒸発温度が制御できると仮定して屋内機処理熱を計算
      double qEvpSum = 0;
      double[] rfTemps = new double[indoorUnits.Count];
      for (int i = 0; i < indoorUnits.Count; i++)
      {
        VRFUnit unt = indoorUnits[i];
        unt.UpdateWithRefrigerantTemperature(TargetEvaporatingTemperature, false);
        qEvpSum += unt.HeatTransfer;
      }

      //熱負荷が無い場合にはサーモオフして終了
      if (0 <= qEvpSum)
      {
        UpdateNoLoad();
        return;
      }

      //蒸発温度が維持できると仮定して蒸発器出口冷媒状態を計算
      refrigerant.GetSaturatedPropertyFromTemperature
        (TargetEvaporatingTemperature + KTOC, out _, out _, out double evpPressure);
      refrigerant.GetStateFromPressureAndTemperature(evpPressure, TargetEvaporatingTemperature + KTOC + SuperHeatDegree,
        out _, out double iUnitOutletDensity, out double iUnitOutletEnthalpy, out _);

      //圧縮ヘッドの収束計算用誤差関数*****************************
      double cndPressure = 0;
      Roots.ErrorFunction eFncHead = delegate (double head)
      {
        return CalcCoolingHeadError
        (head, qEvpSum, iUnitOutletEnthalpy, iUnitOutletDensity, evpPressure, out cndPressure);
      };

      //蒸発温度の収束計算用誤差関数*****************************
      Roots.ErrorFunction eFncEvpTmp = delegate (double evpT)
      {
        return CalcEvaporatingTemperatureError(evpT, false, out evpPressure);
      };

      //過負荷判定
      bool overLoad = 0 < eFncHead(Cooling.NominalHead);
      //過負荷の場合には蒸発温度を収束計算
      if (overLoad)
      {
        if (eFncEvpTmp(Cooling.MinEvaporatingTemperature) < 0) //下限蒸発温度にかかるための過負荷
        {
          double hd = Roots.Bisection(eFncHead, 0, Cooling.NominalHead, 0.001, 0.001, 20);
          eFncHead(hd - 0.001); //大きい側で収束が終わって圧力が過剰に高くなる場合があったための回避処理。良くないプログラム
          CalculateCoolingElectricity(hd);
        }
        else //圧縮機の能力不足による過負荷
        {
          Roots.Bisection(eFncEvpTmp, Cooling.MinEvaporatingTemperature, 30, 0.001, 0.001, 20);
          CompressorElectricity = Cooling.NominalElectricity;
          CompressionHead = Cooling.NominalHead;
          PartialLoadRatio = 1.0;
        }
      }
      //軽負荷の場合にはヘッドを収束計算
      else
      {
        double hd = Roots.Bisection(eFncHead, 0, Cooling.NominalHead, 0.001, 0.001, 20);
        eFncHead(hd - 0.001); //大きい側で収束が終わって圧力が過剰に高くなる場合があったための回避処理。良くないプログラム
        CalculateCoolingElectricity(hd);
      }

      //プロパティ書き込み
      refrigerant.GetSaturatedPropertyFromTemperature(indoorUnits[0].RefrigerantTemperature + KTOC, out _, out _, out evpPressure);
      refrigerant.GetSaturatedPropertyFromTemperature(Cooling.outdoorUnit.RefrigerantTemperature + KTOC, out _, out _, out cndPressure);
      EvaporatingPressure = evpPressure;
      CondensingPressure = cndPressure;
      EvaporatingTemperature = indoorUnits[0].RefrigerantTemperature;
      CondensingTemperature = Cooling.outdoorUnit.RefrigerantTemperature;
    }

    /// <summary>Performs the heating mode state update in free-running mode.</summary>
    private void UpdateHeatingStateWithoutControl()
    {
      //成り行き計算の場合には勝手にファンを落とさない
      for (int i = 0; i < indoorUnits.Count; i++)
        indoorUnits[i].ShutoffFanWhenThermoOff = false;

      //凝縮温度が制御できると仮定して屋内機処理熱を計算
      double qCndSum = 0;
      double[] rfTemps = new double[indoorUnits.Count];
      for (int i = 0; i < indoorUnits.Count; i++)
      {
        VRFUnit unt = indoorUnits[i];
        unt.UpdateWithRefrigerantTemperature(TargetCondensingTemperature, false);
        qCndSum += unt.HeatTransfer;
      }

      //熱負荷が無い場合にはサーモオフして終了
      if (qCndSum <= 0)
      {
        UpdateNoLoad();
        return;
      }

      //凝縮温度が維持できると仮定して凝縮器出口冷媒状態を計算
      refrigerant.GetSaturatedPropertyFromTemperature
        (TargetCondensingTemperature + KTOC, out _, out _, out double cndPressure);
      refrigerant.GetStateFromPressureAndTemperature(cndPressure, TargetCondensingTemperature + KTOC - SubCoolDegree,
        out _, out double iUnitOutletDensity, out double iUnitOutletEnthalpy, out _);

      //定格廃熱回収
      double qRcvN = 0;
      if (IsGasEngineHeatpump)
      {
        double effHd = CalculateHeatingHeadEfficiency(Heating!.NominalHead);
        qRcvN = Heating!.NominalHead * (TotalEfficiencyOfGasEngineHeatpump - effHd) / effHd;
      }

      //圧縮ヘッドの収束計算用誤差関数*****************************
      double evpPressure = 0;
      Roots.ErrorFunction eFncHead = delegate (double head)
      {
        return CalcHeatingHeadError
        (head, qCndSum, iUnitOutletEnthalpy, out evpPressure, ref cndPressure);
      };

      //蒸発圧力の収束計算用誤差関数*****************************
      Roots.ErrorFunction eFncCndTmp = delegate (double cndT)
      {
        return CalcCondensingTemperatureError
        (cndT, qRcvN, out qCndSum, out cndPressure, out evpPressure);
      };

      //過負荷判定
      bool overLoad;
      if (qCndSum < Heating!.NominalHead + qRcvN) overLoad = false;
      else overLoad = 0 < eFncHead(Heating!.NominalHead);
      //過負荷の場合には凝縮温度を収束計算
      if (overLoad)
      {
        if (eFncCndTmp(Heating!.MaxCondensingTemperature) < 0) //凝縮温度上限値を超えることによる過負荷
        {
          double hd = Roots.Bisection(eFncHead, 0, Math.Min(qCndSum, Heating!.NominalHead), 0.001, 0.001, 20);
          eFncHead(hd - 0.001); //大きい側で収束が終わって圧力が過剰に高くなる場合があったための回避処理。良くないプログラム
          CalculateHeatingElectricity(hd);
        }
        else //圧縮機能力が不足することによる過負荷
        {
          Roots.Bisection(eFncCndTmp, 15, Heating!.MaxCondensingTemperature, 0.001, 0.001, 20);
          CompressorElectricity = Heating!.NominalElectricity;
          CompressionHead = Heating!.NominalHead;
          PartialLoadRatio = 1.0;
        }
      }
      //軽負荷の場合にはヘッドを収束計算
      else
      {
        double hd = Roots.Bisection(eFncHead, 0, Math.Min(qCndSum, Heating!.NominalHead), 0.001, 0.001, 20);
        eFncHead(hd - 0.001); //大きい側で収束が終わって圧力が過剰に高くなる場合があったための回避処理。良くないプログラム
        CalculateHeatingElectricity(hd);
      }

      //プロパティ書き込み
      EvaporatingPressure = evpPressure;
      CondensingPressure = cndPressure;
      EvaporatingTemperature = Heating!.outdoorUnit.RefrigerantTemperature;
      CondensingTemperature = indoorUnits[0].RefrigerantTemperature;
    }

    #endregion

    #region 屋内機情報設定

    /// <summary>Sets the supply air temperature setpoint for an indoor unit [°C].</summary>
    /// <param name="indoorUnitIndex">Indoor unit index.</param>
    /// <param name="setpointTemperature">Supply air temperature setpoint [°C].</param>
    public void SetIndoorUnitSetpointTemperature
      (int indoorUnitIndex, double setpointTemperature)
    { indoorUnits[indoorUnitIndex].OutletAirSetpointTemperature = setpointTemperature; }

    /// <summary>Sets the supply air humidity ratio setpoint for an indoor unit [kg/kg].</summary>
    /// <param name="indoorUnitIndex">Indoor unit index.</param>
    /// <param name="setpointHumidityRatio">Supply air humidity ratio setpoint [kg/kg].</param>
    public void SetIndoorUnitSetpointHumidityRatio
      (int indoorUnitIndex, double setpointHumidityRatio)
    { indoorUnits[indoorUnitIndex].OutletAirSetpointHumidityRatio = setpointHumidityRatio; }

    /// <summary>Sets the operating mode of an indoor unit.</summary>
    /// <param name="indoorUnitIndex">Indoor unit index.</param>
    /// <param name="mode">Operating mode.</param>
    public void SetIndoorUnitMode
      (int indoorUnitIndex, VRFUnit.Mode mode)
    { indoorUnits[indoorUnitIndex].CurrentMode = mode; }

    /// <summary>Sets the operating mode of an indoor unit.</summary>
    /// <param name="mode">Operating mode.</param>
    public void SetIndoorUnitMode(VRFUnit.Mode mode)
    {
      for (int i = 0; i < IndoorUnitCount; i++)
        indoorUnits[i].CurrentMode = mode;
    }

    /// <summary>Sets the air flow rate for an indoor unit [kg/s].</summary>
    /// <param name="indoorUnitIndex">Indoor unit index.</param>
    /// <param name="airFlowRate">Air mass flow rate [kg/s].</param>
    public void SetIndoorUnitAirFlowRate
      (int indoorUnitIndex, double airFlowRate)
    { indoorUnits[indoorUnitIndex].AirFlowRate = airFlowRate; }

    /// <summary>Sets the inlet air conditions for an indoor unit.</summary>
    /// <param name="indoorUnitIndex">Indoor unit index.</param>
    /// <param name="inletAirTemperature">Inlet air dry-bulb temperature [°C].</param>
    /// <param name="inletAirHumidityRatio">Inlet air humidity ratio [kg/kg].</param>
    public void SetIndoorUnitInletAirState
      (int indoorUnitIndex, double inletAirTemperature, double inletAirHumidityRatio)
    {
      indoorUnits[indoorUnitIndex].InletAirTemperature = inletAirTemperature;
      indoorUnits[indoorUnitIndex].InletAirHumidityRatio = inletAirHumidityRatio;
    }

    /// <summary>Sets the minimum fan electric power ratio for indoor units [-].</summary>
    /// <param name="minRatio">Minimum electric power consumption ratio [-].</param>
    /// <remarks>
    /// In practice, if the unit is running, the indoor fan rotates at a minimum ratio even at zero load.
    /// Therefore, a minimum indoor fan power rate is defined as a ratio to nominal power for startup accounting.
    /// </remarks>
    public void SetMinFanElectricityRatio(double minRatio)
    {
      for (int i = 0; i < IndoorUnitCount; i++)
        indoorUnits[i].MinFanElectricityRatio = minRatio;
    }

    #endregion

    #region 屋内機接続処理

    /// <summary>Adds one or more indoor units to the system.</summary>
    /// <param name="iHex">Indoor unit to add.</param>
    public void AddIndoorUnit(VRFUnit iHex)
    { indoorUnits.Add(iHex); }

    /// <summary>Adds one or more indoor units to the system.</summary>
    /// <param name="iHexes">List of indoor units to add.</param>
    public void AddIndoorUnit(VRFUnit[] iHexes)
    { indoorUnits.AddRange(iHexes); }

    /// <summary>Removes an indoor unit from the system.</summary>
    /// <param name="iHex">Indoor unit to remove.</param>
    public void RemoveIndoorUnit(VRFUnit iHex)
    { indoorUnits.Remove(iHex); }

    /// <summary>Removes all indoor units from the system.</summary>
    public void ClearIndoorHex()
    { indoorUnits.Clear(); }

    #endregion

    #region 計算結果取得処理

    /// <summary>Gets the total indoor unit heat load [kW] (positive = heating, negative = cooling).</summary>
    /// <returns>Total indoor unit heat load [kW] (positive = heating, negative = cooling).</returns>
    public double GetHeatLoad()
    {
      double hSum = 0;
      foreach (VRFUnit iHex in indoorUnits)
        hSum += iHex.HeatTransfer;
      //デフロスト負荷があれば差し引く
      if (CurrentMode == Mode.Heating && Heating!.outdoorUnit.DefrostLoad != 0)
        hSum = Math.Max(0, hSum - Heating!.outdoorUnit.DefrostLoad);
      return hSum;
    }

    /// <summary>Gets the indoor unit heat load [kW] (positive = heating, negative = cooling).</summary>
    /// <param name="indoorUnitIndex">Indoor unit index.</param>
    /// <returns>Indoor unit heat load [kW] (positive = heating, negative = cooling).</returns>
    public double GetHeatLoad(int indoorUnitIndex)
    {
      //デフロスト負荷があれば負担する
      if (CurrentMode == Mode.Heating && Heating!.outdoorUnit.DefrostLoad != 0)
      {
        double hSum = 0;
        foreach (VRFUnit iHex in indoorUnits)
          hSum += iHex.HeatTransfer;
        double rate = indoorUnits[indoorUnitIndex].HeatTransfer / hSum;
        return Math.Max(0, indoorUnits[indoorUnitIndex].HeatTransfer - Heating!.outdoorUnit.DefrostLoad * rate);
      }
      //なければ室内機負荷をそのまま出力
      else
        return indoorUnits[indoorUnitIndex].HeatTransfer;
    }

    /// <summary>Gets the total system electric power consumption [kW].</summary>
    /// <returns>Total system electric power [kW].</returns>
    /// <remarks>Sum of compressor, outdoor fan, and indoor fan electric power.</remarks>
    public double GetElectricity()
    {
      return CompressorElectricity + IndoorUnitFanElectricity + OutdoorUnitFanElectricity;
    }

    /// <summary>Gets the coefficient of performance [-].</summary>
    /// <returns>COP[-]</returns>
    public double GetCOP()
    {
      double elc = GetElectricity();
      if (elc <= 0) return 0;
      else return Math.Abs(GetHeatLoad()) / elc;
    }

    #endregion

    #region 初期化用のstaticメソッド（冷房運転モデル）

    /// <summary>Estimates the outdoor unit performance parameters for cooling mode from JIS-rated conditions.</summary>
    /// <param name="refrigerant">Refrigerant property calculator.</param>
    /// <param name="oHexAirFlowRate">Outdoor unit air mass flow rate [kg/s].</param>
    /// <param name="fanElectricity">Fan electric power [kW].</param>
    /// <param name="coolingCapacity">Cooling capacity [kW] (negative = cooling).</param>
    /// <param name="nominalPipeLength">Reference (nominal) pipe length [m].</param>
    /// <param name="longPipeLength">Reference pipe length for correction factor [m].</param>
    /// <param name="pipeCorrectionFactor">Pipe length correction factor at the reference length [-].</param>
    /// <param name="pipeResistanceCoefficient">Output: pipe resistance coefficient [1/m].</param>
    /// <param name="nominalHead">Output: nominal compression head [kW].</param>
    /// <param name="outdoorHex">Output: outdoor unit heat exchanger.</param>
    public static void EstimateCoolingOutdoorUnitNominalParameters(
      Refrigerant refrigerant,
      double oHexAirFlowRate, double fanElectricity, double coolingCapacity,
      double nominalPipeLength, double longPipeLength, double pipeCorrectionFactor,
      out double pipeResistanceCoefficient, out double nominalHead,
      out VRFUnit outdoorHex)
    {
      //定格凝縮温度を推定
      double cndTemp = 1.449 * (-coolingCapacity / oHexAirFlowRate) + 36.03;

      //基準条件の蒸発器入口比エンタルピー
      refrigerant.GetSaturatedPropertyFromTemperature(cndTemp + KTOC, out double rhoLiq, out _, out double cndPressureRef);
      double hIHexInRef = refrigerant.GetEnthalpyFromTemperatureAndDensity(cndTemp - SUB_COOL_NOM + KTOC, rhoLiq);

      //配管長補正条件の蒸発器入口比エンタルピー
      double cndTempAdj = JIS_OA_DBT_NOM_C - pipeCorrectionFactor * (JIS_OA_DBT_NOM_C - cndTemp);  //凝縮温度の補正
      double evpTempAdj = JIS_IA_DBT_C - pipeCorrectionFactor * (JIS_IA_DBT_C - NominalEvaporatingTemperature);
      refrigerant.GetSaturatedPropertyFromTemperature(cndTempAdj + KTOC, out rhoLiq, out _, out double cndPressurePC);
      double hIHexInPC = refrigerant.GetEnthalpyFromTemperatureAndDensity(cndTempAdj - SUB_COOL_NOM + KTOC, rhoLiq);

      //圧縮機入口冷媒状態
      double hIHexOutRef;
      refrigerant.GetSaturatedPropertyFromTemperature(NominalEvaporatingTemperature + KTOC, out _, out _, out double evpPressureRef);
      refrigerant.GetStateFromPressureAndTemperature(evpPressureRef, NominalEvaporatingTemperature + KTOC + SUPER_HEAT_NOM, out _, out _, out hIHexOutRef, out _);

      //配管長補正条件の圧縮機入口冷媒状態
      double hIHexOutPC;
      refrigerant.GetSaturatedPropertyFromTemperature(evpTempAdj + KTOC, out _, out _, out double evpPressurePC);
      refrigerant.GetStateFromPressureAndTemperature(evpPressurePC, evpTempAdj + KTOC + SUPER_HEAT_NOM, out _, out double rhoVap, out hIHexOutPC, out _);

      //圧縮機入口冷媒流量
      double mRRef = -coolingCapacity / (hIHexOutRef - hIHexInRef);  //冷媒質量流量_基準[kg/s]
      double mRPC = -coolingCapacity * pipeCorrectionFactor / (hIHexOutPC - hIHexInPC);  //冷媒質量流量_配管長補正[kg/s]
      double rvvRef = mRRef * mRRef / rhoVap * nominalPipeLength;
      double rvvPC = mRPC * mRPC / rhoVap * longPipeLength;

      //配管抵抗係数を収束計算
      double nmHead = 1;
      Roots.ErrorFunction eFnc1 = delegate (double dpCoef)
      {
        //基準条件の圧縮ヘッド
        double pCmpInRef = evpPressureRef - rvvRef * dpCoef;
        refrigerant.GetStateFromPressureAndEnthalpy(pCmpInRef, hIHexOutRef, out double tmp, out rhoVap, out _, out _);
        double kappaRef = refrigerant.GetSpecificHeatRatioFromTemperatureAndDensity(tmp, rhoVap);
        double kp2Ref = kappaRef / (kappaRef - 1);
        nmHead = kp2Ref * pCmpInRef * (mRRef / rhoVap) * (Math.Pow(cndPressureRef / pCmpInRef, 1d / kp2Ref) - 1);

        //配管長補正条件の圧縮ヘッド
        double pCmpInPC = evpPressurePC - rvvPC * dpCoef;
        refrigerant.GetStateFromPressureAndEnthalpy(pCmpInPC, hIHexOutPC, out tmp, out rhoVap, out _, out _);
        double kappaPC = refrigerant.GetSpecificHeatRatioFromTemperatureAndDensity(tmp, rhoVap);
        double kp2PC = kappaPC / (kappaPC - 1);
        double headPC = kp2PC * pCmpInPC * (mRPC / rhoVap) * (Math.Pow(cndPressurePC / pCmpInPC, 1d / kp2PC) - 1);

        return nmHead - headPC;
      };
      pipeResistanceCoefficient = Roots.Newton(eFnc1, 0, 1, 0.01, 0.01, 20);
      nominalHead = nmHead;

      //屋外機伝熱面積
      double oHmd = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndWetBulbTemperature(JIS_OA_DBT_NOM_C, JIS_OA_WBT_NOM_C, PhysicsConstants.StandardAtmosphericPressure);
      outdoorHex = new VRFUnit
        (oHexAirFlowRate, fanElectricity, cndTemp, -coolingCapacity + nmHead, JIS_OA_DBT_NOM_C, oHmd);
      outdoorHex.CurrentMode = VRFUnit.Mode.Heating;
    }

    /// <summary>Estimates the partial load characteristic curve for cooling mode from JIS-rated conditions.</summary>
    /// <param name="refrigerant">Refrigerant property calculator.</param>
    /// <param name="nominalPipeLength">Reference (nominal) pipe length [m].</param>
    /// <param name="pipeResistanceCoefficient">Pipe resistance coefficient [1/m].</param>
    /// <param name="nominalHead">Nominal compression head [kW].</param>
    /// <param name="nominalCapacity">Nominal cooling capacity [kW].</param>
    /// <param name="outdoorHex">Outdoor unit heat exchanger.</param>
    /// <param name="indoorHex">Indoor unit heat exchanger.</param>
    /// <param name="midCapacity1">Intermediate standard-condition cooling capacity [kW] (negative).</param>
    /// <param name="midCapacity2">Intermediate mid-temperature-condition cooling capacity [kW] (negative).</param>
    /// <param name="midHead1">Output: compression head at intermediate standard condition [kW].</param>
    /// <param name="midHead2">Output: compression head at intermediate mid-temperature condition [kW].</param>
    public static void EstimateCoolingPartialLoadParameters(
      Refrigerant refrigerant,
      double nominalPipeLength, double pipeResistanceCoefficient, double nominalHead, double nominalCapacity,
      VRFUnit outdoorHex, VRFUnit indoorHex,
      double midCapacity1, double midCapacity2, out double midHead1, out double midHead2)
    {
      double evpPressure, cndPressure;  //蒸発圧力と凝縮圧力
      double hIHexOut, rhoIHexOut;  //蒸発器出口状態

      double iHmd = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndWetBulbTemperature(JIS_IA_DBT_C, JIS_IA_WBT_C, PhysicsConstants.StandardAtmosphericPressure);
      indoorHex.CurrentMode = VRFUnit.Mode.Cooling;

      //中間標準条件の計算*****************************
      //蒸発器出口状態
      double partialRate = midCapacity1 / nominalCapacity;
      indoorHex.SolveHeatLoad
        (indoorHex.NominalCoolingCapacity * partialRate, indoorHex.NominalAirFlowRate, JIS_IA_DBT_C, iHmd, false);
      refrigerant.GetSaturatedPropertyFromTemperature
        (indoorHex.RefrigerantTemperature + KTOC, out _, out _, out evpPressure);
      refrigerant.GetStateFromPressureAndTemperature
        (evpPressure, indoorHex.RefrigerantTemperature + SUPER_HEAT_NOM + KTOC, out _, out rhoIHexOut, out hIHexOut, out _);

      //圧縮ヘッドを収束計算
      double oHmd = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndWetBulbTemperature(JIS_OA_DBT_NOM_C, JIS_OA_WBT_NOM_C, PhysicsConstants.StandardAtmosphericPressure);
      Roots.ErrorFunction eFnc2 = delegate (double head)
      {
        //凝縮器出口比エンタルピー
        outdoorHex.SolveHeatLoad(-midCapacity1 + head, outdoorHex.NominalAirFlowRate, JIS_OA_DBT_NOM_C, oHmd, false);
        refrigerant.GetSaturatedPropertyFromTemperature(outdoorHex.RefrigerantTemperature + KTOC, out _, out double rhoVap, out cndPressure);
        double hIHexIn;
        refrigerant.GetStateFromPressureAndTemperature
        (cndPressure, outdoorHex.RefrigerantTemperature + KTOC - SUB_COOL_NOM, out _, out _, out hIHexIn, out _);

        //冷媒質量流量
        double mR = -midCapacity1 / (hIHexOut - hIHexIn);

        //圧縮ヘッドの誤差を出力
        double pCmpIn = evpPressure - pipeResistanceCoefficient * nominalPipeLength * mR * mR / rhoIHexOut;
        refrigerant.GetStateFromPressureAndEnthalpy(pCmpIn, hIHexOut, out double tmp, out rhoVap, out _, out _);
        double kappa = refrigerant.GetSpecificHeatRatioFromTemperatureAndDensity(tmp, rhoVap);
        double kp2 = kappa / (kappa - 1);
        return (kp2 * pCmpIn * (mR / rhoVap) * (Math.Pow(cndPressure / pCmpIn, 1d / kp2) - 1)) - head;
      };
      midHead1 = Roots.Brent(0.1 * nominalHead, nominalHead, 0.001, eFnc2);

      //中間中温条件の計算*****************************
      partialRate = midCapacity2 / nominalCapacity;
      indoorHex.SolveHeatLoad
        (indoorHex.NominalCoolingCapacity * partialRate, indoorHex.NominalAirFlowRate, JIS_IA_DBT_C, iHmd, false);
      refrigerant.GetSaturatedPropertyFromTemperature
        (indoorHex.RefrigerantTemperature + KTOC, out _, out _, out evpPressure);
      refrigerant.GetStateFromPressureAndTemperature
        (evpPressure, indoorHex.RefrigerantTemperature + SUPER_HEAT_NOM + KTOC, out _, out rhoIHexOut, out hIHexOut, out _);

      //圧縮ヘッドを収束計算
      oHmd = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndWetBulbTemperature(JIS_OA_DBT_MID_C, JIS_OA_WBT_MID_C, PhysicsConstants.StandardAtmosphericPressure);
      Roots.ErrorFunction eFnc3 = delegate (double head)
      {
        //凝縮器出口比エンタルピー
        outdoorHex.SolveHeatLoad(-midCapacity2 + head, outdoorHex.NominalAirFlowRate, JIS_OA_DBT_MID_C, oHmd, false);
        refrigerant.GetSaturatedPropertyFromTemperature(outdoorHex.RefrigerantTemperature + KTOC, out _, out double rhoVap, out cndPressure);
        double hIHexIn;
        refrigerant.GetStateFromPressureAndTemperature
        (cndPressure, outdoorHex.RefrigerantTemperature + KTOC - SUB_COOL_NOM, out _, out _, out hIHexIn, out _);

        //冷媒質量流量
        double mR = -midCapacity2 / (hIHexOut - hIHexIn);

        //圧縮ヘッドの誤差を出力
        double pCmpIn = evpPressure - pipeResistanceCoefficient * nominalPipeLength * mR * mR / rhoIHexOut;
        refrigerant.GetStateFromPressureAndEnthalpy(pCmpIn, hIHexOut, out double tmp, out rhoVap, out _, out _);
        double kappa = refrigerant.GetSpecificHeatRatioFromTemperatureAndDensity(tmp, rhoVap);
        double kp2 = kappa / (kappa - 1);
        return (kp2 * pCmpIn * (mR / rhoVap) * (Math.Pow(cndPressure / pCmpIn, 1d / kp2) - 1)) - head;
      };
      midHead2 = Roots.Brent(0.1 * nominalHead, nominalHead, 0.001, eFnc3);
    }

    /// <summary>Estimates the partial load characteristic curve for cooling mode from JIS-rated conditions.</summary>
    /// <param name="refrigerant">Refrigerant property calculator.</param>
    /// <param name="nominalPipeLength">Reference (nominal) pipe length [m].</param>
    /// <param name="pipeResistanceCoefficient">Pipe resistance coefficient [1/m].</param>
    /// <param name="nominalHead">Nominal compression head [kW].</param>
    /// <param name="nominalCapacity">Nominal cooling capacity [kW].</param>
    /// <param name="outdoorHex">Outdoor unit heat exchanger.</param>
    /// <param name="indoorHex">Indoor unit heat exchanger.</param>
    /// <param name="midCapacity1">Intermediate standard-condition cooling capacity [kW] (negative).</param>
    /// <param name="midHead1">Output: compression head at intermediate standard condition [kW].</param>
    public static void EstimateCoolingPartialLoadParameters(
      Refrigerant refrigerant,
      double nominalPipeLength, double pipeResistanceCoefficient,
      double nominalHead, double nominalCapacity,
      VRFUnit outdoorHex, VRFUnit indoorHex,
      double midCapacity1, out double midHead1)
    {
      double iHmd = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndWetBulbTemperature(JIS_IA_DBT_C, JIS_IA_WBT_C, PhysicsConstants.StandardAtmosphericPressure);
      indoorHex.CurrentMode = VRFUnit.Mode.Cooling;

      //中間標準条件の計算*****************************
      //蒸発器出口状態
      double partialRate = midCapacity1 / nominalCapacity;
      indoorHex.SolveHeatLoad
        (indoorHex.NominalCoolingCapacity * partialRate, indoorHex.NominalAirFlowRate, JIS_IA_DBT_C, iHmd, false);
      refrigerant.GetSaturatedPropertyFromTemperature
        (indoorHex.RefrigerantTemperature + KTOC, out _, out _, out double evpPressure);
      refrigerant.GetStateFromPressureAndTemperature
        (evpPressure, indoorHex.RefrigerantTemperature + SUPER_HEAT_NOM + KTOC, out _, out double rhoIHexOut, out double hIHexOut, out _);

      //圧縮ヘッドを収束計算
      double oHmd = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndWetBulbTemperature(JIS_OA_DBT_NOM_C, JIS_OA_WBT_NOM_C, PhysicsConstants.StandardAtmosphericPressure);
      Roots.ErrorFunction eFnc2 = delegate (double head)
      {
        //凝縮器出口比エンタルピー
        outdoorHex.SolveHeatLoad(-midCapacity1 + head, outdoorHex.NominalAirFlowRate, JIS_OA_DBT_NOM_C, oHmd, false);
        refrigerant.GetSaturatedPropertyFromTemperature(outdoorHex.RefrigerantTemperature + KTOC, out _, out _, out double cndPressure);
        double hIHexIn;
        refrigerant.GetStateFromPressureAndTemperature
        (cndPressure, outdoorHex.RefrigerantTemperature + KTOC - SUB_COOL_NOM, out _, out _, out hIHexIn, out _);

        //冷媒質量流量
        double mR = -midCapacity1 / (hIHexOut - hIHexIn);

        //圧縮ヘッドの誤差を出力
        double pCmpIn = evpPressure - pipeResistanceCoefficient * nominalPipeLength * mR * mR / rhoIHexOut;
        refrigerant.GetStateFromPressureAndEnthalpy(pCmpIn, hIHexOut, out double tmp, out double rhoVap, out _, out _);
        double kappa = refrigerant.GetSpecificHeatRatioFromTemperatureAndDensity(tmp, rhoVap);
        double kp2 = kappa / (kappa - 1);
        return (kp2 * pCmpIn * (mR / rhoVap) * (Math.Pow(cndPressure / pCmpIn, 1d / kp2) - 1)) - head;
      };
      midHead1 = Roots.Brent(0.1 * nominalHead, nominalHead, 0.001, eFnc2);
    }


    /// <summary>Initializes the indoor unit performance for cooling mode from JIS-rated conditions.</summary>
    /// <param name="iHexAirFlowRate">Indoor unit air mass flow rate [kg/s].</param>
    /// <param name="fanElectricity">Fan electric power [kW].</param>
    /// <param name="coolingCapacity">Cooling capacity [kW] (negative = cooling).</param>
    /// <returns>Initialized indoor unit.</returns>
    public static VRFUnit MakeIndoorUnit_Cooling(
      double iHexAirFlowRate, double fanElectricity, double coolingCapacity)
    {
      return MakeIndoorUnit_Cooling(iHexAirFlowRate, fanElectricity, coolingCapacity, 95);
    }

    /// <summary>Initializes the indoor unit performance for cooling mode from JIS-rated conditions.</summary>
    /// <param name="iHexAirFlowRate">Indoor unit air mass flow rate [kg/s].</param>
    /// <param name="fanElectricity">Fan electric power [kW].</param>
    /// <param name="coolingCapacity">Cooling capacity [kW] (negative = cooling).</param>
    /// <param name="borderRelativeHumidity">Relative humidity at the dry/wet boundary [%].</param>
    /// <returns>Initialized indoor unit.</returns>
    public static VRFUnit MakeIndoorUnit_Cooling(
      double iHexAirFlowRate, double fanElectricity, double coolingCapacity, double borderRelativeHumidity)
    {
      double iHmd = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndWetBulbTemperature
        (JIS_IA_DBT_C, JIS_IA_WBT_C, PhysicsConstants.StandardAtmosphericPressure);
      VRFUnit iUnit = new VRFUnit
        (iHexAirFlowRate, fanElectricity, NominalEvaporatingTemperature, coolingCapacity, JIS_IA_DBT_C, iHmd, borderRelativeHumidity);
      iUnit.CurrentMode = VRFUnit.Mode.Cooling;
      return iUnit;
    }

    /// <summary>Estimates the partial load efficiency ratio characteristic curve coefficients.</summary>
    /// <param name="nominalHead">Nominal compression head [kW].</param>
    /// <param name="nominalElectricity">Nominal electric power consumption [kW].</param>
    /// <param name="midHead1">Compression head at intermediate standard cooling condition [kW].</param>
    /// <param name="midElectricity1">Electric power at intermediate standard cooling condition [kW].</param>
    /// <param name="midHead2">Compression head at intermediate mid-temperature cooling condition [kW].</param>
    /// <param name="midElectricity2">Electric power at intermediate mid-temperature cooling condition [kW].</param>
    /// <param name="coefA">Output: coefficient A.</param>
    /// <param name="coefB">Output: coefficient B.</param>
    public static void MakePartialLoadCharacteristicCurve(
      double nominalHead, double nominalElectricity,
      double midHead1, double midElectricity1,
      double midHead2, double midElectricity2,
      out double coefA, out double coefB)
    {
      double[] y = new double[] {
        1.0,
        (midHead1 / midElectricity1) / (nominalHead / nominalElectricity) ,
        (midHead2 / midElectricity2) / (nominalHead / nominalElectricity) };
      double[] x = new double[] { 1.0, midHead1 / nominalHead, midHead2 / nominalHead };
      LinearAlgebraOperations.FitAxPlusB(x, y, out coefA, out coefB);
    }

    /// <summary>Estimates the partial load efficiency ratio characteristic curve coefficients.</summary>
    /// <param name="nominalHead">Nominal compression head [kW].</param>
    /// <param name="nominalElectricity">Nominal electric power consumption [kW].</param>
    /// <param name="midHead">Compression head at intermediate load condition [kW].</param>
    /// <param name="midElectricity">Electric power at intermediate load condition [kW].</param>
    /// <param name="coefA">Output: coefficient A.</param>
    /// <param name="coefB">Output: coefficient B.</param>
    public static void MakePartialLoadCharacteristicCurve(
      double nominalHead, double nominalElectricity,
      double midHead, double midElectricity,
      out double coefA, out double coefB)
    {
      double[] y = new double[] {
        1.0,
        (midHead / midElectricity) / (nominalHead / nominalElectricity) };
      double[] x = new double[] { 1.0, midHead / nominalHead };
      coefA = (y[0] - y[1]) / (x[0] - x[1]);
      coefB = y[0] - coefA * x[0];
    }

    #endregion

    #region 初期化用のstaticメソッド（暖房運転モデル）

    /// <summary>Estimates the outdoor unit performance parameters for heating mode from JIS-rated conditions.</summary>
    /// <param name="refrigerant">Refrigerant property calculator.</param>
    /// <param name="oHexAirFlowRate">Outdoor unit air mass flow rate [kg/s].</param>
    /// <param name="fanElectricity">Fan electric power [kW].</param>
    /// <param name="heatingCapacity">Heating capacity [kW] (positive = heating).</param>
    /// <param name="totalEnergyRecover">Total energy recovery [kW] (positive for GHP only).</param>
    /// <param name="nominalPipeLength">Reference (nominal) pipe length [m].</param>
    /// <param name="longPipeLength">Reference pipe length for correction factor [m].</param>
    /// <param name="pipeCorrectionFactor">Pipe length correction factor at the reference length [-].</param>
    /// <param name="pipeResistanceCoefficient">Output: pipe resistance coefficient [1/m].</param>
    /// <param name="nominalHead">Output: nominal compression head [kW].</param>
    /// <param name="outdoorHex">Output: outdoor unit heat exchanger.</param>
    public static void EstimateHeatingOutdoorUnitNominalParameters(
      Refrigerant refrigerant,
      double oHexAirFlowRate, double fanElectricity, double heatingCapacity, double totalEnergyRecover,
      double nominalPipeLength, double longPipeLength, double pipeCorrectionFactor,
      out double pipeResistanceCoefficient, out double nominalHead,
      out VRFUnit outdoorHex)
    {
      //定格凝縮温度を推定
      double evpTempRef = -0.34 * (heatingCapacity / oHexAirFlowRate) + 4.091;

      //基準条件の蒸発器入口比エンタルピー
      refrigerant.GetSaturatedPropertyFromTemperature(NominalCondensingTemperature + KTOC, out double rhoLiq, out _, out double cndPressureRef);
      double hIHexInRef = refrigerant.GetEnthalpyFromTemperatureAndDensity(NominalCondensingTemperature - SUB_COOL_NOM + KTOC, rhoLiq);

      //配管長補正条件の蒸発器入口比エンタルピー
      double evpTempAdj = JIS_OA_DBT_NOM_H - pipeCorrectionFactor * (JIS_OA_DBT_NOM_H - evpTempRef);  //蒸発温度の補正
      double cndTempAdj = JIS_IA_DBT_H - pipeCorrectionFactor * (JIS_IA_DBT_H - NominalCondensingTemperature);  //凝縮温度の補正
      refrigerant.GetSaturatedPropertyFromTemperature(cndTempAdj + KTOC, out rhoLiq, out _, out double cndPressurePC);
      double hIHexInPC = refrigerant.GetEnthalpyFromTemperatureAndDensity(cndTempAdj - SUB_COOL_NOM + KTOC, rhoLiq);

      //圧縮機入口冷媒状態
      double hIHexOutRef;
      refrigerant.GetSaturatedPropertyFromTemperature(evpTempRef + KTOC, out _, out _, out double evpPressureRef);
      refrigerant.GetStateFromPressureAndTemperature(evpPressureRef, evpTempRef + SUPER_HEAT_NOM + KTOC, out _, out double rhoCmpInRef, out hIHexOutRef, out _);
      double kappaRef = refrigerant.GetSpecificHeatRatioFromTemperatureAndDensity(evpTempRef + SUPER_HEAT_NOM + KTOC, rhoCmpInRef);
      double kp2Ref = kappaRef / (kappaRef - 1);

      //配管長補正時の圧縮機入口冷媒状態
      double hIHexOutPC;
      refrigerant.GetSaturatedPropertyFromTemperature(evpTempAdj + KTOC, out _, out _, out double evpPressurePC);
      refrigerant.GetStateFromPressureAndTemperature(evpPressurePC, evpTempAdj + SUPER_HEAT_NOM + KTOC, out _, out double rhoCmpInPC, out hIHexOutPC, out _);
      double kappaPC = refrigerant.GetSpecificHeatRatioFromTemperatureAndDensity(evpTempAdj + SUPER_HEAT_NOM + KTOC, rhoCmpInPC);
      double kp2PC = kappaPC / (kappaPC - 1);

      //配管抵抗係数を収束計算
      double pipeCoef = 0;
      Roots.ErrorFunction eFnc1 = delegate (double nmHead)
      {
        //基準条件の配管抵抗係数
        double mRRef = Math.Max(0, heatingCapacity - nmHead) / (hIHexOutRef - hIHexInRef);  //冷媒質量流量_基準[kg/s]
        double hCmpoRef = hIHexOutRef + nmHead / mRRef;
        double pHRef = GetHighPressure(nmHead, kappaRef, mRRef / rhoCmpInRef, evpPressureRef);
        refrigerant.GetStateFromPressureAndEnthalpy(cndPressureRef, hCmpoRef, out _, out double rhoVap, out _, out _); //圧力降下後の冷媒密度で計算
        double volRef = mRRef / rhoVap;
        pipeCoef = (pHRef - cndPressureRef) / (nominalPipeLength * mRRef * volRef);

        //配管長補正条件の配管抵抗係数
        double mRPC = Math.Max(0, heatingCapacity * pipeCorrectionFactor - nmHead) / (hIHexOutPC - hIHexInPC);  //冷媒質量流量_配管長補正[kg/s]
        double hCmpoPC = hIHexOutPC + nmHead / mRPC;
        double pHPC = GetHighPressure(nmHead, kappaPC, mRPC / rhoCmpInPC, evpPressurePC);
        refrigerant.GetStateFromPressureAndEnthalpy(cndPressurePC, hCmpoPC, out _, out rhoVap, out _, out _); //圧力降下後の冷媒密度で計算
        double volPC = mRPC / rhoVap;
        double kpPC = (pHPC - cndPressurePC) / (longPipeLength * mRPC * volPC);

        return pipeCoef - kpPC;
      };
      //COP=6を初期値にして収束計算
      nominalHead = Roots.Newton(eFnc1, heatingCapacity / 6d, 0.001, 0.001, 0.001, 10);
      pipeResistanceCoefficient = pipeCoef;

      //屋外機伝熱面積
      double oHmd = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndWetBulbTemperature(JIS_OA_DBT_NOM_H, JIS_OA_WBT_NOM_H, PhysicsConstants.StandardAtmosphericPressure);
      double nmRcv = Math.Max(0, totalEnergyRecover - nominalHead);
      outdoorHex = new VRFUnit
        (oHexAirFlowRate, fanElectricity, evpTempRef, -(heatingCapacity - nominalHead - nmRcv), JIS_OA_DBT_NOM_H, oHmd, 95);
      outdoorHex.CurrentMode = VRFUnit.Mode.Cooling;
    }

    /// <summary>Estimates the partial load characteristic curve for heating mode from JIS-rated conditions.</summary>
    /// <param name="refrigerant">Refrigerant property calculator.</param>
    /// <param name="nominalPipeLength">Reference (nominal) pipe length [m].</param>
    /// <param name="pipeResistanceCoefficient">Pipe resistance coefficient [1/m].</param>
    /// <param name="nominalHead">Nominal compression head [kW].</param>
    /// <param name="nominalCapacity">Nominal heating capacity [kW].</param>
    /// <param name="outdoorHex">Outdoor unit heat exchanger.</param>
    /// <param name="indoorHex">Indoor unit heat exchanger.</param>
    /// <param name="totalEnergyRecover">Total energy recovery [kW].</param>
    /// <param name="midCapacity">Intermediate standard-condition heating capacity [kW] (positive).</param>
    /// <param name="midHead">Output: compression head at intermediate standard condition [kW].</param>
    public static void EstimateHeatingPartialLoadParameters(
      Refrigerant refrigerant,
      double nominalPipeLength, double pipeResistanceCoefficient, double nominalHead, double nominalCapacity,
      VRFUnit outdoorHex, VRFUnit indoorHex, double totalEnergyRecover,
      double midCapacity, out double midHead)
    {
      indoorHex.CurrentMode = VRFUnit.Mode.Heating;
      double iHmd = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndWetBulbTemperature(JIS_IA_DBT_H, JIS_IA_WBT_H, PhysicsConstants.StandardAtmosphericPressure);

      //中間標準条件の計算*****************************
      //凝縮器出口状態
      double partialRate = midCapacity / nominalCapacity;
      indoorHex.SolveHeatLoad
        (indoorHex.NominalHeatingCapacity * partialRate, indoorHex.NominalAirFlowRate, JIS_IA_DBT_H, iHmd, false);
      refrigerant.GetSaturatedPropertyFromTemperature
        (indoorHex.RefrigerantTemperature + KTOC, out _, out _, out double cndPressure);
      refrigerant.GetStateFromPressureAndTemperature
        (cndPressure, indoorHex.RefrigerantTemperature - SUB_COOL_NOM + KTOC, out _, out _, out double hOHexIn, out _);

      //圧縮ヘッドを収束計算
      double oHmd = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndWetBulbTemperature(JIS_OA_DBT_NOM_H, JIS_OA_WBT_NOM_H, PhysicsConstants.StandardAtmosphericPressure);
      Roots.ErrorFunction eFnc = delegate (double head)
      {
        //廃熱回収
        double qRcv = Math.Max(0, totalEnergyRecover - head);

        //蒸発器出口比エンタルピー
        outdoorHex.SolveHeatLoad(-Math.Max(0, midCapacity - head - qRcv), outdoorHex.NominalAirFlowRate, JIS_OA_DBT_NOM_H, oHmd, false);
        refrigerant.GetSaturatedPropertyFromTemperature(outdoorHex.RefrigerantTemperature + KTOC, out _, out _, out double evpPressure);
        refrigerant.GetStateFromPressureAndTemperature(
          evpPressure, outdoorHex.RefrigerantTemperature + KTOC + SUPER_HEAT_NOM,
          out _, out double rhoVap, out double hOHexOut, out _);
        double kappa = refrigerant.GetSpecificHeatRatioFromTemperatureAndDensity(
          outdoorHex.RefrigerantTemperature + KTOC + SUPER_HEAT_NOM, rhoVap);

        //冷媒質量流量
        double mR = (midCapacity - head) / (hOHexOut - hOHexIn);

        //圧縮ヘッドの誤差を出力
        double pCmpOut = GetHighPressure(head, kappa, mR / rhoVap, evpPressure);
        double hCmpOut = hOHexOut + head / mR;
        refrigerant.GetStateFromPressureAndEnthalpy(cndPressure, hCmpOut, out _, out rhoVap, out _, out _);  //圧力降下後の冷媒密度で計算
        double pIHexIn = pCmpOut - pipeResistanceCoefficient * nominalPipeLength * mR * (mR / rhoVap);
        return pIHexIn - cndPressure;
      };
      midHead = Roots.Newton(eFnc, partialRate * nominalHead, 0.001, 0.001, 0.001, 10);
    }

    /// <summary>Estimates the partial load characteristic curve for heating mode from JIS-rated conditions.</summary>
    /// <param name="refrigerant">Refrigerant property calculator.</param>
    /// <param name="nominalPipeLength">Reference (nominal) pipe length [m].</param>
    /// <param name="pipeResistanceCoefficient">Pipe resistance coefficient [1/m].</param>
    /// <param name="nominalHead">Nominal compression head [kW].</param>
    /// <param name="nominalCapacity">Nominal heating capacity [kW].</param>
    /// <param name="outdoorHex">Outdoor unit heat exchanger.</param>
    /// <param name="indoorHex">Indoor unit heat exchanger.</param>
    /// <param name="midCapacity1">Intermediate standard-condition heating capacity [kW] (positive).</param>
    /// <param name="midCapacity2">Minimum standard-condition heating capacity [kW] (positive).</param>
    /// <param name="midHead1">Output: compression head at intermediate standard condition [kW].</param>
    /// <param name="midHead2">Output: compression head at minimum standard condition [kW].</param>
    /// <remarks>The minimum standard condition does not appear to be mandatory published information under JIS.</remarks>
    public static void EstimateHeatingPartialLoadParameters(
      Refrigerant refrigerant,
      double nominalPipeLength, double pipeResistanceCoefficient, double nominalHead, double nominalCapacity,
      VRFUnit outdoorHex, VRFUnit indoorHex,
      double midCapacity1, double midCapacity2, out double midHead1, out double midHead2)
    {
      double iHmd = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndWetBulbTemperature(JIS_IA_DBT_H, JIS_IA_WBT_H, PhysicsConstants.StandardAtmosphericPressure);

      midHead1 = midHead2 = 0;
      for (int css = 0; css < 2; css++)
      {
        //中間標準条件の計算*****************************
        //凝縮器出口状態
        double partialRate = (css == 0 ? midCapacity1 : midCapacity2) / nominalCapacity;
        indoorHex.SolveHeatLoad
          (indoorHex.NominalHeatingCapacity * partialRate, indoorHex.NominalAirFlowRate, JIS_IA_DBT_H, iHmd, false);
        refrigerant.GetSaturatedPropertyFromTemperature
          (indoorHex.RefrigerantTemperature + KTOC, out _, out _, out double cndPressure);
        refrigerant.GetStateFromPressureAndTemperature
          (cndPressure, indoorHex.RefrigerantTemperature - SUB_COOL_NOM + KTOC, out _, out _, out double hOHexIn, out _);

        //圧縮ヘッドを収束計算
        double oHmd = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndWetBulbTemperature(JIS_OA_DBT_NOM_H, JIS_OA_WBT_NOM_H, PhysicsConstants.StandardAtmosphericPressure);
        Roots.ErrorFunction eFnc2 = delegate (double head)
        {
          //凝縮器出口比エンタルピー
          outdoorHex.SolveHeatLoad(-(midCapacity1 - head), outdoorHex.NominalAirFlowRate, JIS_OA_DBT_NOM_H, oHmd, false);
          refrigerant.GetSaturatedPropertyFromTemperature(outdoorHex.RefrigerantTemperature + KTOC, out _, out double rhoVap, out double evpPressure);
          refrigerant.GetStateFromPressureAndTemperature(
            evpPressure, outdoorHex.RefrigerantTemperature + KTOC + SUPER_HEAT_NOM,
            out _, out rhoVap, out double hOHexOut, out _);
          double kappa = refrigerant.GetSpecificHeatRatioFromTemperatureAndDensity(
            outdoorHex.RefrigerantTemperature + KTOC + SUPER_HEAT_NOM, rhoVap);

          //冷媒質量流量
          double mR = (midCapacity1 - head) / (hOHexOut - hOHexIn);

          //圧縮ヘッドの誤差を出力
          double pCmpOut = GetHighPressure(head, kappa, mR / rhoVap, evpPressure);
          double hCmpOut = hOHexOut + head / mR;
          refrigerant.GetStateFromPressureAndEnthalpy(cndPressure, hCmpOut, out _, out rhoVap, out _, out _);  //圧力降下後の冷媒密度で計算
          double pIHexIn = pCmpOut - pipeResistanceCoefficient * nominalPipeLength * mR * (mR / rhoVap);
          return pIHexIn - cndPressure;
        };
        if (css == 0)
          midHead1 = Roots.Brent(0.1 * nominalHead, 1.3 * partialRate * nominalHead, 0.001, eFnc2);
        else
          midHead2 = Roots.Brent(0.1 * nominalHead, 1.3 * partialRate * nominalHead, 0.001, eFnc2);
      }
    }

    /// <summary>Initializes the indoor unit performance from JIS-rated conditions.</summary>
    /// <param name="iHexAirFlowRate">Indoor unit air mass flow rate [kg/s].</param>
    /// <param name="coolingFanElectricity">Fan electric power in cooling mode [kW].</param>
    /// <param name="coolingCapacity">Cooling capacity [kW] (negative = cooling).</param>
    /// <param name="heatingFanElectricity">Fan electric power in heating mode [kW].</param>
    /// <param name="heatingCapacity">Heating capacity [kW] (positive = heating).</param>
    /// <returns>Initialized indoor unit.</returns>
    public static VRFUnit MakeIndoorUnit(
      double iHexAirFlowRate,
      double coolingFanElectricity, double coolingCapacity,
      double heatingFanElectricity, double heatingCapacity)
    {
      return MakeIndoorUnit(
        iHexAirFlowRate,
        coolingFanElectricity, coolingCapacity, 95,
        heatingFanElectricity, heatingCapacity);
    }

    /// <summary>Initializes the indoor unit performance from JIS-rated conditions.</summary>
    /// <param name="iHexAirFlowRate">Indoor unit air mass flow rate [kg/s].</param>
    /// <param name="coolingFanElectricity">Fan electric power in cooling mode [kW].</param>
    /// <param name="coolingCapacity">Cooling capacity [kW] (negative = cooling).</param>
    /// <param name="borderRelativeHumidity">Relative humidity at the dry/wet boundary [%].</param>
    /// <param name="heatingFanElectricity">Fan electric power in heating mode [kW].</param>
    /// <param name="heatingCapacity">Heating capacity [kW] (positive = heating).</param>
    /// <returns>Initialized indoor unit.</returns>
    public static VRFUnit MakeIndoorUnit(
      double iHexAirFlowRate,
      double coolingFanElectricity, double coolingCapacity, double borderRelativeHumidity,
      double heatingFanElectricity, double heatingCapacity)
    {
      double coolingIndoorHumidityRatio = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndWetBulbTemperature
        (JIS_IA_DBT_C, JIS_IA_WBT_C, PhysicsConstants.StandardAtmosphericPressure);
      double heatingIndoorHumidityRatio = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndWetBulbTemperature
        (JIS_IA_DBT_H, JIS_IA_WBT_H, PhysicsConstants.StandardAtmosphericPressure);
      VRFUnit iUnit = new VRFUnit
        (iHexAirFlowRate,
        NominalEvaporatingTemperature, coolingCapacity, JIS_IA_DBT_C, coolingIndoorHumidityRatio, borderRelativeHumidity, coolingFanElectricity,
        NominalCondensingTemperature, heatingCapacity, JIS_IA_DBT_H, heatingIndoorHumidityRatio, heatingFanElectricity);
      iUnit.CurrentMode = VRFUnit.Mode.Cooling;
      return iUnit;
    }

    /// <summary>Initializes the indoor unit performance for heating mode from JIS-rated conditions.</summary>
    /// <param name="iHexAirFlowRate">Indoor unit air mass flow rate [kg/s].</param>
    /// <param name="fanElectricity">Fan electric power [kW].</param>
    /// <param name="heatingCapacity">Heating capacity [kW] (positive = heating).</param>
    /// <returns>Initialized indoor unit.</returns>
    public static VRFUnit MakeIndoorUnit_Heating(
      double iHexAirFlowRate, double fanElectricity, double heatingCapacity)
    {
      double iHmd = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndWetBulbTemperature
        (JIS_IA_DBT_H, JIS_IA_WBT_H, PhysicsConstants.StandardAtmosphericPressure);
      VRFUnit iUnit = new VRFUnit
        (iHexAirFlowRate, fanElectricity, NominalCondensingTemperature, heatingCapacity, JIS_IA_DBT_H, iHmd);
      iUnit.CurrentMode = VRFUnit.Mode.Heating;
      return iUnit;
    }

    /// <summary>Computes the high-side pressure [kPa].</summary>
    /// <param name="head">Compression head [kW].</param>
    /// <param name="kappa">Specific heat ratio (isentropic exponent) [-].</param>
    /// <param name="volume">Refrigerant volumetric flow rate [m³/s].</param>
    /// <param name="lowPressure">Low-side pressure [kPa].</param>
    /// <returns>High-side pressure [kPa].</returns>
    private static double GetHighPressure
      (double head, double kappa, double volume, double lowPressure)
    {
      const double MAXP = 4000;

      double kp2 = kappa / (kappa - 1);
      double hd = kp2 * lowPressure * volume * (Math.Pow(MAXP / lowPressure, 1d / kp2) - 1);
      if (hd < head) return MAXP;

      Roots.ErrorFunction eFnc = delegate (double pH)
      { return head - kp2 * lowPressure * volume * (Math.Pow(pH / lowPressure, 1d / kp2) - 1); };
      return Roots.Newton(eFnc, lowPressure + 1, 0.001, 0.001, 0.001, 10);
    }

    #endregion

  }
}