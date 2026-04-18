using System;
using System.Collections.Generic;
using System.Text;

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

    /// <summary>Gets the outdoor unit for cooling mode (condenser).</summary>
    IReadOnlyVRFUnit OutdoorUnit_C { get; }

    /// <summary>Gets the outdoor unit for heating mode (evaporator).</summary>
    IReadOnlyVRFUnit? OutdoorUnit_H { get; }

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

    #region 冷房運転関連のプロパティ

    /// <summary>Gets the nominal cooling capacity [kW].</summary>
    double NominalCoolingCapacity { get; }

    /// <summary>Gets the nominal compression head in cooling mode [kW].</summary>
    double NominalHead_C { get; }

    /// <summary>Gets the pipe resistance coefficient for cooling mode [1/m].</summary>
    double PipeResistanceCoefficient_C { get; }

    /// <summary>Gets the compression head efficiency ratio at the nominal cooling operating point [-].</summary>
    double NominalEfficiency_C { get; }

    /// <summary>Gets coefficient A of the compression head efficiency ratio characteristic curve for cooling [-].</summary>
    double HeadEfficiencyRatioCoefA_C { get; }

    /// <summary>Gets coefficient B of the compression head efficiency ratio characteristic curve for cooling [-].</summary>
    double HeadEfficiencyRatioCoefB_C { get; }

    /// <summary>Gets the electric power consumption at the nominal cooling operating point [kW].</summary>
    double NominalElectricity_C { get; }

    /// <summary>Gets the maximum evaporating temperature in cooling mode [°C].</summary>
    double MaxEvaporatingTemperature { get; }

    /// <summary>Gets the minimum evaporating temperature in cooling mode [°C].</summary>
    /// <remarks>Currently, the temperature cannot be lowered below the nominal value, except in heating mode.</remarks>
    double MinEvaporatingTemperature { get; }

    /// <summary>Gets the target evaporating temperature for free-running calculation [°C].</summary>
    double TargetEvaporatingTemperature { get; set; }

    #endregion

    #region 暖房運転関連のプロパティ

    /// <summary>Gets the nominal heating capacity [kW].</summary>
    double NominalHeatingCapacity { get; }

    /// <summary>Gets the nominal compression head in heating mode [kW].</summary>
    double NominalHead_H { get; }

    /// <summary>Gets the pipe resistance coefficient for heating mode [1/m].</summary>
    double PipeResistanceCoefficient_H { get; }

    /// <summary>Gets the compression head efficiency ratio at the nominal heating operating point [-].</summary>
    double NominalEfficiency_H { get; }

    /// <summary>Gets coefficient A of the compression head efficiency ratio characteristic curve for heating [-].</summary>
    double HeadEfficiencyRatioCoefA_H { get; }

    /// <summary>Gets coefficient B of the compression head efficiency ratio characteristic curve for heating [-].</summary>
    double HeadEfficiencyRatioCoefB_H { get; }

    /// <summary>Gets the electric power consumption at the nominal heating operating point [kW].</summary>
    double NominalElectricity_H { get; }

    /// <summary>Gets the maximum condensing temperature in heating mode [°C].</summary>
    /// <remarks>Currently, the temperature cannot be raised above the nominal value, except in cooling mode.</remarks>
    double MaxCondensingTemperature { get; }

    /// <summary>Gets the minimum condensing temperature in heating mode [°C].</summary>
    double MinCondensingTemperature { get; }

    /// <summary>Gets the target condensing temperature for free-running calculation [°C].</summary>
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
