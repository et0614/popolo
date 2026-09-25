/* ChillerDemo.cs
 *
 * Copyright (C) 2026 E.Togashi
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

using Popolo.Core.HVAC.HeatSource;
using Popolo.Core.Physics;

namespace Popolo.Samples.Demos.Core
{
  /// <summary>
  /// Evaluates a simplified centrifugal chiller (constant-speed vs. inverter)
  /// across a handful of operating points and prints COP and power input.
  /// </summary>
  /// <remarks>
  /// Builds two <see cref="SimpleCentrifugalChiller"/> instances with
  /// identical rated conditions (500 kW cooling, 5 K chilled-water ΔT,
  /// 37 °C cooling-water supply). One is constant-speed, the other has an
  /// inverter. Each is run at three partial-load ratios (40, 70, 100 %) and
  /// two cooling-water inlet temperatures (32, 26 °C).
  /// </remarks>
  public sealed class ChillerDemo : IDemo
  {
    public string Name => "hvac-chiller";
    public string Category => "Core";
    public string Description => "SimpleCentrifugalChiller COP at several part-load / ambient points.";

    public int Run(string[] args)
    {
      // Rated conditions: 500 kW cooling, CHW 12→7 °C, CW supply 37 °C.
      const double ratedCapacity = 500.0;                      // [kW]
      const double chwReturn = 12.0;                            // [°C]
      const double chwSupply = 7.0;                             // [°C]
      const double cwRatedLeave = 37.0;                         // [°C]
      double cp = PhysicsConstants.NominalWaterIsobaricSpecificHeat / 1000.0; // [kJ/(kg·K)]
      double chwRatedFlow = ratedCapacity / (cp * (chwReturn - chwSupply));
      double cwRatedFlow = ratedCapacity * 1.25 / (cp * (cwRatedLeave - 32.0));
      double ratedInput = ratedCapacity / 6.0;                  // assumed COP = 6 at rated

      var fixedSpeed = new SimpleCentrifugalChiller(
        nominalInput: ratedInput, minimumPartialLoadRatio: 0.25,
        chilledWaterInletTemperature: chwReturn,
        chilledWaterOutletTemperature: chwSupply,
        coolingWaterOutletTemperature: cwRatedLeave,
        chilledWaterFlowRate: chwRatedFlow,
        hasInverter: false);

      var inverter = new SimpleCentrifugalChiller(
        nominalInput: ratedInput, minimumPartialLoadRatio: 0.25,
        chilledWaterInletTemperature: chwReturn,
        chilledWaterOutletTemperature: chwSupply,
        coolingWaterOutletTemperature: cwRatedLeave,
        chilledWaterFlowRate: chwRatedFlow,
        hasInverter: true);

      Console.WriteLine($"Rated: {ratedCapacity:F0} kW cooling, CHW {chwReturn}→{chwSupply} °C, "
                        + $"CW supply 32 °C, nominal COP ≈ {ratedCapacity / ratedInput:F1}");
      Console.WriteLine();
      Console.WriteLine("   Type       PLR    CW_in [°C]   Load [kW]   Power [kW]    COP");
      Console.WriteLine("  ---------  ------  ----------  ----------  ----------  -------");

      double[] plrs = { 1.00, 0.70, 0.40 };
      double[] cwIns = { 32.0, 26.0 };
      foreach (double cwIn in cwIns)
      {
        foreach (double plr in plrs)
        {
          RunCase("Constant", fixedSpeed, plr, cwIn, chwRatedFlow, cwRatedFlow);
          RunCase("Inverter", inverter,   plr, cwIn, chwRatedFlow, cwRatedFlow);
        }
      }

      return 0;
    }

    private static void RunCase(
      string label, SimpleCentrifugalChiller chiller, double plr,
      double cwInletTemp, double chwRatedFlow, double cwFlow)
    {
      // Partial-load target: keep the CHW supply setpoint but reduce CHW flow proportionally.
      double load = plr * chiller.NominalCapacity;
      double cp = PhysicsConstants.NominalWaterIsobaricSpecificHeat / 1000.0;
      double chwReturn =
        chiller.ChilledWaterOutletSetpointTemperature + load / (cp * chwRatedFlow);

      chiller.IsOperating = true;
      chiller.Update(
        coolingWaterInletTemperature: cwInletTemp,
        chilledWaterInletTemperature: chwReturn,
        coolingWaterFlowRate: cwFlow,
        chilledWaterFlowRate: chwRatedFlow);

      Console.WriteLine(
        $"   {label,-9}  {plr,5:F2}   {cwInletTemp,9:F1}   "
        + $"{chiller.CoolingLoad,9:F1}   {chiller.ElectricConsumption,9:F1}   {chiller.COP,6:F2}");
    }
  }
}
