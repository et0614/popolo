/* SteamTableDemo.cs
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

using Popolo.Core.Physics;

namespace Popolo.Samples.Demos.Core
{
  /// <summary>
  /// Prints a compact saturated-steam table (pressure, enthalpies, latent
  /// heat) across a handful of temperatures.
  /// </summary>
  /// <remarks>
  /// Exercises the <see cref="Water"/> static helpers. Values come from the
  /// Irvine &amp; Liley steam correlations used internally; round-off against a
  /// reference table should be in the fourth significant figure for the
  /// temperature range shown.
  /// </remarks>
  public sealed class SteamTableDemo : IDemo
  {
    public string Name => "physics-steam";
    public string Category => "Core";
    public string Description => "Saturated steam table for a handful of temperatures.";

    public int Run(string[] args)
    {
      Console.WriteLine("Saturated-steam table (Irvine & Liley correlations)");
      Console.WriteLine();
      Console.WriteLine("   T [°C]   P_sat [kPa]   h_f [kJ/kg]   h_g [kJ/kg]   h_fg [kJ/kg]   v_g [m³/kg]");
      Console.WriteLine("  -------  ------------  ------------  ------------  -------------  ------------");

      foreach (double T in new[] { 0.01, 20.0, 40.0, 60.0, 80.0, 100.0, 120.0, 150.0, 200.0, 300.0 })
      {
        double p = Water.GetSaturationPressure(T);
        double hf = Water.GetSaturatedLiquidEnthalpy(T);
        double hg = Water.GetSaturatedVaporEnthalpy(T);
        double hfg = Water.GetVaporizationLatentHeat(T);
        double vg = Water.GetSaturatedVaporSpecificVolume(T, p);
        Console.WriteLine(
          $"   {T,5:F2}   {p,11:F3}   {hf,11:F2}   {hg,11:F2}   {hfg,12:F2}   {vg,11:F5}");
      }

      Console.WriteLine();
      Console.WriteLine("Inverse lookup: T_sat for several saturation pressures");
      Console.WriteLine("   P [kPa]   T_sat [°C]");
      foreach (double p in new[] { 1.0, 10.0, 101.325, 500.0, 1000.0 })
      {
        double T = Water.GetSaturationTemperature(p);
        Console.WriteLine($"   {p,7:F3}   {T,10:F2}");
      }

      Console.WriteLine();
      Console.WriteLine($"Critical point: T = {Water.CriticalTemperature - 273.15:F2} °C, "
                        + $"P = {Water.CriticalPressure:F0} kPa, "
                        + $"h = {Water.CriticalEnthalpy:F1} kJ/kg");

      return 0;
    }
  }
}
