/* InclineIrradianceDemo.cs
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

using Popolo.Core.Climate;

namespace Popolo.Samples.Demos.Core
{
  /// <summary>
  /// Prints tilted-surface solar irradiance for several orientations given a
  /// prescribed solar state, illustrating the role of <see cref="Incline"/>.
  /// </summary>
  /// <remarks>
  /// Demonstrates how a single <see cref="Sun"/> state (DNI / DHI / altitude /
  /// azimuth) maps to different surface irradiance values depending on the
  /// surface orientation and tilt. Ground albedo is fixed at 0.2 (typical
  /// green / paved ground).
  /// </remarks>
  public sealed class InclineIrradianceDemo : IDemo
  {
    public string Name => "climate-incline";
    public string Category => "Core";
    public string Description => "Tilted-surface irradiance for several orientations.";

    public int Run(string[] args)
    {
      const double albedo = 0.2;

      // Representative summer solar state at Tokyo, around solar noon.
      var sun = new Sun(Sun.City.Tokyo);
      sun.Update(new DateTime(2026, 6, 21, 12, 0, 0));
      // Prescribe DNI and DHI; compute GHI from them for consistency.
      sun.DirectNormalRadiation = 850;       // typical clear-sky DNI
      sun.DiffuseHorizontalRadiation = 130;  // typical clear-sky DHI
      sun.GlobalHorizontalRadiation =
        sun.DirectNormalRadiation * Math.Sin(sun.Altitude)
        + sun.DiffuseHorizontalRadiation;

      double altDeg = sun.Altitude * 180.0 / Math.PI;
      double aziDeg = sun.Azimuth * 180.0 / Math.PI;

      Console.WriteLine("Tilted-surface solar irradiance at Tokyo");
      Console.WriteLine($"  Date/time       : {sun.CurrentDateTime:yyyy-MM-dd HH:mm}");
      Console.WriteLine($"  Solar altitude  : {altDeg:F2} °");
      Console.WriteLine($"  Solar azimuth   : {aziDeg:F2} ° (south = 0, east = neg)");
      Console.WriteLine($"  DNI             : {sun.DirectNormalRadiation:F1} W/m²");
      Console.WriteLine($"  DHI             : {sun.DiffuseHorizontalRadiation:F1} W/m²");
      Console.WriteLine($"  GHI             : {sun.GlobalHorizontalRadiation:F1} W/m²");
      Console.WriteLine($"  Ground albedo   : {albedo}");
      Console.WriteLine();

      Console.WriteLine("  Orientation        Tilt        cosθ    Direct [W/m²]   Diffuse [W/m²]    Total [W/m²]");
      Console.WriteLine("  -----------------  ---------  -------  --------------  ---------------  ------------");

      foreach (var row in Surfaces())
      {
        var inc = new Incline(row.azimuth, row.tilt);
        double cosTheta = inc.GetDirectSolarRadiationRatio(sun);
        double dir = inc.GetDirectSolarIrradiance(sun);
        double dif = inc.GetDiffuseSolarIrradiance(sun, albedo);
        double tot = inc.GetSolarIrradiance(sun, albedo);
        Console.WriteLine(
          $"  {row.label,-17}  {row.tiltLabel,-9}  {cosTheta,6:F3}  "
          + $"{dir,14:F1}  {dif,15:F1}  {tot,12:F1}");
      }

      return 0;
    }

    /// <summary>Surface descriptions to display, using Incline conventions (south = 0 rad, east = negative, tilt 0 = horizontal up, π/2 = vertical).</summary>
    private static System.Collections.Generic.IEnumerable<(string label, string tiltLabel, double azimuth, double tilt)> Surfaces()
    {
      const double deg = Math.PI / 180.0;

      yield return ("Horizontal (roof)", "0°",     0.0,             0.0);
      yield return ("South, vertical",   "90°",    0.0,             90 * deg);
      yield return ("East, vertical",    "90°",   -90 * deg,        90 * deg);
      yield return ("West, vertical",    "90°",    90 * deg,        90 * deg);
      yield return ("North, vertical",   "90°",    180 * deg,       90 * deg);
      yield return ("South, 30° tilt",   "30°",    0.0,             30 * deg);
    }
  }
}
