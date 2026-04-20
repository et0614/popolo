/* ChillerDemo.cs
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
