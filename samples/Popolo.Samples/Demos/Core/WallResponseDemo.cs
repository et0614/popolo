/* WallResponseDemo.cs
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

using Popolo.Core.Building.Envelope;

namespace Popolo.Samples.Demos.Core
{
  /// <summary>
  /// Applies a sinusoidal 24-hour sol-air temperature to one side of a
  /// three-layer exterior wall and prints the resulting outdoor- and
  /// indoor-facing surface temperatures.
  /// </summary>
  /// <remarks>
  /// Demonstrates <see cref="Wall"/>'s implicit-Euler step solver under
  /// periodic boundary conditions.
  /// The outdoor sol-air temperature varies as 30 ± 10 °C with its peak at
  /// 14:00. The indoor side is held at 25 °C. Film coefficients are set to
  /// common default values. Two full 24-hour cycles are simulated so the
  /// wall reaches periodic steady state; only the second cycle is printed.
  /// </remarks>
  public sealed class WallResponseDemo : IDemo
  {
    public string Name => "building-wall";
    public string Category => "Core";
    public string Description => "Multi-layer wall response to a 24 h sinusoidal sol-air temperature.";

    public int Run(string[] args)
    {
      // Build a simple exterior wall: mortar + reinforced concrete + glass wool + plaster board.
      var layers = new WallLayer[]
      {
        new WallLayer(WallLayer.Material.Mortar,                  0.020),
        new WallLayer(WallLayer.Material.ReinforcedConcrete,      0.150),
        new WallLayer(WallLayer.Material.GlassWoolInsulation_24K, 0.050),
        new WallLayer(WallLayer.Material.PlasterBoard,             0.012),
      };

      var wall = new Wall(area: 1.0, layers);
      wall.TimeStep = 3600;   // 1 hour
      wall.ConvectiveCoefficientF = 18.0;   // outdoor convective [W/(m²·K)]
      wall.RadiativeCoefficientF  = 5.5;
      wall.ConvectiveCoefficientB = 3.5;    // indoor convective
      wall.RadiativeCoefficientB  = 5.0;

      // Reset to a uniform temperature consistent with the indoor boundary.
      wall.Initialize(25.0);

      // Print U-value as a steady-state context clue.
      double totalR = 1.0 / wall.FilmCoefficientF;
      foreach (var layer in layers) totalR += layer.Thickness / layer.ThermalConductivity;
      totalR += 1.0 / wall.FilmCoefficientB;
      Console.WriteLine($"Wall: {layers.Length} layers, steady-state U ≈ {1.0 / totalR:F3} W/(m²·K)");
      Console.WriteLine();
      Console.WriteLine("Outdoor sol-air temp: 30 ± 10 °C, peak at 14:00.  Indoor side: 25 °C.");
      Console.WriteLine();

      // Run two full 24-hour cycles; only print the second.
      for (int cycle = 0; cycle < 2; cycle++)
      {
        if (cycle == 1)
        {
          Console.WriteLine("   Hour   T_solair [°C]   T_surf_F [°C]   T_surf_B [°C]   q_F [W/m²]");
          Console.WriteLine("  -----  --------------  --------------  --------------  -----------");
        }

        for (int h = 0; h < 24; h++)
        {
          double t_solair = 30.0 + 10.0 * Math.Cos((h - 14) * Math.PI / 12.0);
          wall.SolAirTemperatureF = t_solair;
          wall.SolAirTemperatureB = 25.0;
          wall.Update();

          if (cycle == 1)
          {
            double qF = wall.GetSurfaceHeatTransfer(isSideF: true);
            Console.WriteLine(
              $"   {h,2}:00   {t_solair,14:F2}   {wall.SurfaceTemperatureF,14:F2}   "
              + $"{wall.SurfaceTemperatureB,14:F2}   {qF,11:F2}");
          }
        }
      }

      return 0;
    }
  }
}
