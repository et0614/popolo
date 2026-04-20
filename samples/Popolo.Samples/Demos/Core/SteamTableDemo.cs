/* SteamTableDemo.cs
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
        double vg = Water.GetSaturatedVaporSpecificVolume(T);
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
