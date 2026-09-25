/* CrossFinHeatExchanger.cs
 *
 * Copyright (C) 2014 E.Togashi
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;

namespace Popolo.Core.HVAC.HeatExchanger
{
  /// <summary>Obsolete alias of <see cref="AirToWaterCrossFinHeatExchanger"/>.</summary>
  /// <remarks>
  /// The class was renamed to make the medium pair explicit: this coil exchanges heat between
  /// moist air and water (both streams change temperature). For an air-to-refrigerant coil in
  /// which the refrigerant side is an isothermal (phase-change) surface, see
  /// <see cref="AirToRefrigerantCrossFinHeatExchanger"/>.
  /// </remarks>
  [Obsolete("Renamed to AirToWaterCrossFinHeatExchanger. This alias will be removed in a future major version.")]
  public class CrossFinHeatExchanger : AirToWaterCrossFinHeatExchanger, IReadOnlyCrossFinHeatExchanger
  {

    /// <summary>Initializes a new instance using the detailed geometric coil model.</summary>
    public CrossFinHeatExchanger(double depth, double width, double height, int rowCount, int columnCount,
      double finPitch, double finThickness, double thermalConductivity, double innerDiameter,
      double outerDiameter, double ratedAirFlowRate, double ratedInletAirTemperature,
      double ratedInletAirHumidityRatio, double borderRelativeHumidity, double ratedWaterFlowRate,
      double maxWaterFlowRate, double ratedInletWaterTemperature, WaterFlowType flowType,
      double heatTransfer, bool useCorrectionFactor)
      : base(depth, width, height, rowCount, columnCount, finPitch, finThickness, thermalConductivity,
          innerDiameter, outerDiameter, ratedAirFlowRate, ratedInletAirTemperature,
          ratedInletAirHumidityRatio, borderRelativeHumidity, ratedWaterFlowRate, maxWaterFlowRate,
          ratedInletWaterTemperature, flowType, heatTransfer, useCorrectionFactor)
    { }

    /// <summary>Initializes a new instance using the detailed model with automatic UA estimation.</summary>
    public CrossFinHeatExchanger(
      double width, double height, int rowCount, int columnount, double ratedAirFlowRate,
      double ratedInletAirTemperature, double ratedInletAirHumidityRatio, double borderRelativeHumidity,
      double ratedWaterFlowRate, double maxWaterFlowRate, double ratedInletWaterTemperature,
      WaterFlowType flowType, double heatTransfer, bool useCorrectionFactor)
      : base(width, height, rowCount, columnount, ratedAirFlowRate,
          ratedInletAirTemperature, ratedInletAirHumidityRatio, borderRelativeHumidity,
          ratedWaterFlowRate, maxWaterFlowRate, ratedInletWaterTemperature,
          flowType, heatTransfer, useCorrectionFactor)
    { }

    /// <summary>Initializes a new instance using the detailed geometric coil model.</summary>
    public CrossFinHeatExchanger(double depth, double width, double height, int rowCount, int columnCount,
      double finPitch, double finThickness, double thermalConductivity, double innerDiameter,
      double outerDiameter, double ratedAirFlowRate, double ratedInletAirTemperature,
      double ratedInletAirHumidityRatio, double borderRelativeHumidity, double ratedWaterFlowRate,
      double maxWaterFlowRate, double ratedInletWaterTemperature, double flowFactor, double heatTransfer,
      bool useCorrectionFactor)
      : base(depth, width, height, rowCount, columnCount, finPitch, finThickness, thermalConductivity,
          innerDiameter, outerDiameter, ratedAirFlowRate, ratedInletAirTemperature,
          ratedInletAirHumidityRatio, borderRelativeHumidity, ratedWaterFlowRate, maxWaterFlowRate,
          ratedInletWaterTemperature, flowFactor, heatTransfer, useCorrectionFactor)
    { }

    /// <summary>Initializes a new instance using the simplified coil model.</summary>
    public CrossFinHeatExchanger(double ratedAirFlowRate, double ratedVelocity,
      double ratedInletAirTemperature, double ratedInletAirHumidityRatio, double borderRelativeHumidity,
      double ratedWaterFlowRate, double ratedWaterSpeed, double maxWaterFlowRate,
      double ratedInletWaterTemperature, double heatTransfer)
      : base(ratedAirFlowRate, ratedVelocity,
          ratedInletAirTemperature, ratedInletAirHumidityRatio, borderRelativeHumidity,
          ratedWaterFlowRate, ratedWaterSpeed, maxWaterFlowRate,
          ratedInletWaterTemperature, heatTransfer)
    { }

  }
}
