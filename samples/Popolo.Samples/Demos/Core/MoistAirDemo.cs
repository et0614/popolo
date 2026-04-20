/* MoistAirDemo.cs
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
  /// Prints psychrometric properties of moist air at a few representative
  /// temperature / humidity combinations.
  /// </summary>
  /// <remarks>
  /// Exercises <see cref="MoistAir"/> at standard atmospheric pressure. For each
  /// sample row, dry-bulb temperature and humidity ratio are given and the
  /// corresponding enthalpy, wet-bulb temperature, relative humidity, and
  /// specific volume are printed. A few static helpers
  /// (<see cref="MoistAir.GetEnthalpyFromDryBulbTemperatureAndHumidityRatio"/>,
  /// <see cref="MoistAir.GetAtmosphericPressure"/>) are also demonstrated.
  /// </remarks>
  public sealed class MoistAirDemo : IDemo
  {
    public string Name => "physics-moist-air";
    public string Category => "Core";
    public string Description => "Psychrometric properties at a few T/w pairs (stdatm).";

    public int Run(string[] args)
    {
      Console.WriteLine("Moist air properties at standard atmosphere (101.325 kPa)");
      Console.WriteLine();
      Console.WriteLine("   Tdb [°C]   w [kg/kg]    Twb [°C]   RH [%]    h [kJ/kg]   v [m³/kg]");
      Console.WriteLine("  ---------  ----------  ----------  -------  -----------  ----------");

      var cases = new (double T, double w)[]
      {
        (20.0, 0.0075),   // mild winter supply
        (22.0, 0.0090),   // comfortable
        (26.0, 0.0105),   // summer cooling design
        (30.0, 0.0180),   // hot & humid
        (35.0, 0.0200),   // peak summer outdoor
      };

      foreach (var (T, w) in cases)
      {
        var air = new MoistAir(T, w);
        Console.WriteLine(
          $"   {T,7:F1}   {w,10:F4}   {air.WetBulbTemperature,8:F2}   "
          + $"{air.RelativeHumidity,6:F1}   {air.Enthalpy,10:F2}   {air.SpecificVolume,9:F4}");
      }

      // Altitude dependence via the static helper
      Console.WriteLine();
      Console.WriteLine("Atmospheric pressure vs elevation (static helper):");
      Console.WriteLine("   Elevation [m]   Pressure [kPa]");
      foreach (int elev in new[] { 0, 500, 1000, 2000, 3776 /* Mt. Fuji */ })
      {
        double p = MoistAir.GetAtmosphericPressure(elev);
        Console.WriteLine($"   {elev,13}   {p,14:F3}");
      }

      // Enthalpy of a known state via the static converter
      Console.WriteLine();
      double h = MoistAir.GetEnthalpyFromDryBulbTemperatureAndHumidityRatio(26.0, 0.0105);
      Console.WriteLine($"Check: h(26°C, 0.0105 kg/kg) via static helper = {h:F2} kJ/kg");

      return 0;
    }
  }
}
