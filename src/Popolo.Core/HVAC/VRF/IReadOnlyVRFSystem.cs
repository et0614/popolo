/* IReadOnlyVRFSystem.cs
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

namespace Popolo.Core.HVAC.VRF
{
  /// <summary>Read-only view of a VRF system.</summary>
  public interface IReadOnlyVRFSystem
  {

    #region VRFシステム全体のプロパティ

    /// <summary>Gets the current operating mode.</summary>
    VRFSystem.Mode CurrentMode { get; }

    /// <summary>Gets a value indicating whether the system is a gas engine heat pump (GHP).</summary>
    bool IsGasEngineHeatpump { get; }

    /// <summary>Gets the overall efficiency of the gas engine heat pump [-].</summary>
    double TotalEfficiencyOfGasEngineHeatpump { get; }

    /// <summary>Gets the number of indoor units.</summary>
    int IndoorUnitCount { get; }

    /// <summary>Gets the list of indoor unit heat exchangers.</summary>
    IReadOnlyVRFUnit[] IndoorUnits { get; }

    /// <summary>Gets the cooling-mode parameters and state (always present).</summary>
    IReadOnlyModeParameters Cooling { get; }

    /// <summary>Gets the heating-mode parameters and state (null for cooling-only systems).</summary>
    IReadOnlyModeParameters? Heating { get; }

    /// <summary>Gets the compressor electric power consumption [kW].</summary>
    double CompressorElectricity { get; }

    /// <summary>Gets the outdoor unit fan electric power [kW].</summary>
    double OutdoorUnitFanElectricity { get; }

    /// <summary>Gets the total indoor unit fan electric power [kW].</summary>
    double IndoorUnitFanElectricity { get; }

    /// <summary>Gets the superheat degree [°C].</summary>
    double SuperHeatDegree { get; }

    /// <summary>Gets the subcooling degree [°C].</summary>
    double SubCoolDegree { get; }

    /// <summary>Gets the equivalent pipe length [m].</summary>
    double PipeLength { get; }

    /// <summary>Gets the minimum partial load ratio for capacity control [-].</summary>
    double MinimumPartialLoadRatio { get; }

    /// <summary>Gets the outdoor air dry-bulb temperature [°C].</summary>
    double OutdoorAirDryBulbTemperature { get; }

    /// <summary>Gets the outdoor air humidity ratio [kg/kg].</summary>
    double OutdoorAirHumidityRatio { get; }

    /// <summary>Gets the condensing pressure [kPa].</summary>
    double CondensingPressure { get; }

    /// <summary>Gets the condensing temperature [°C].</summary>
    double CondensingTemperature { get; }

    /// <summary>Gets the evaporating pressure [kPa].</summary>
    double EvaporatingPressure { get; }

    /// <summary>Gets the evaporating temperature [°C].</summary>
    double EvaporatingTemperature { get; }

    /// <summary>Gets the compressor inlet pressure [kPa].</summary>
    double CompressorInletPressure { get; }

    /// <summary>Gets the compressor outlet pressure [kPa].</summary>
    double CompressorOutletPressure { get; }

    /// <summary>Gets the compression ratio [-].</summary>
    double CompressionRatio { get; }

    /// <summary>Gets the partial load ratio [-].</summary>
    double PartialLoadRatio { get; }

    /// <summary>Gets a value indicating whether water spray is applied to the outdoor unit.</summary>
    bool UseWaterSpray { get; }

    /// <summary>Gets the installation height of indoor units relative to the outdoor unit [m].</summary>
    /// <remarks>A higher outdoor unit position reduces cooling capacity and increases heating capacity; lower position reverses this.</remarks>
    double IndoorUnitHeight { get; }

    /// <summary>Gets a value indicating whether the system is in on/off (bang-bang) operation.</summary>
    bool IsOnOffOperation { get; }

    #endregion

    #region 冷房・暖房運転の温度制限プロパティ

    /// <summary>Gets the target evaporating temperature for free-running calculation [°C].</summary>
    /// <remarks>Used only in cooling mode.</remarks>
    double TargetEvaporatingTemperature { get; set; }

    /// <summary>Gets the target condensing temperature for free-running calculation [°C].</summary>
    /// <remarks>Used only in heating mode.</remarks>
    double TargetCondensingTemperature { get; }

    #endregion

    #region 計算結果取得処理

    /// <summary>Gets the total indoor unit heat load [kW] (positive = heating, negative = cooling).</summary>
    /// <returns>Total indoor unit heat load [kW] (positive = heating, negative = cooling).</returns>
    double GetHeatLoad();

    /// <summary>Gets the coefficient of performance [-].</summary>
    /// <returns>COP[-]</returns>
    double GetCOP();

    #endregion

  }
}