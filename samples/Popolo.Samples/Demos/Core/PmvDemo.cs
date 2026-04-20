/* PmvDemo.cs
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

using Popolo.Core.ThermalComfort;

namespace Popolo.Samples.Demos.Core
{
  /// <summary>
  /// Computes Fanger's PMV and PPD across a handful of representative indoor
  /// conditions and prints a summary table.
  /// </summary>
  /// <remarks>
  /// Uses the static helpers <see cref="FangerModel.GetPMV"/> and
  /// <see cref="FangerModel.GetPPD"/>. Also demonstrates the activity-level
  /// lookup via <see cref="FangerModel.GetMet"/>.
  /// </remarks>
  public sealed class PmvDemo : IDemo
  {
    public string Name => "comfort-pmv";
    public string Category => "Core";
    public string Description => "PMV / PPD across several indoor conditions (Fanger).";

    public int Run(string[] args)
    {
      // Reference activity levels for the clothed, seated-office scenario.
      double metSeated = FangerModel.GetMet(FangerModel.MetabolicActivity.OfficeSeatedReadingWriting);
      double cloSummer = 0.5;   // summer business-casual
      double cloWinter = 1.0;   // winter business
      double velocity = 0.1;    // still indoor air

      Console.WriteLine("PMV / PPD — seated office worker, v = 0.1 m/s, Tr = Ta");
      Console.WriteLine($"  Summer clothing: {cloSummer} clo    Winter clothing: {cloWinter} clo");
      Console.WriteLine($"  Metabolism      : {metSeated:F2} met ({FangerModel.MetabolicActivity.OfficeSeatedReadingWriting})");
      Console.WriteLine();
      Console.WriteLine("  Season    Tdb [°C]   RH [%]     PMV    PPD [%]   Sensation");
      Console.WriteLine("  --------  ---------  --------  ------  --------  ---------------");

      foreach (var (label, T, RH, clo) in Cases(cloSummer, cloWinter))
      {
        double pmv = FangerModel.GetPMV(
          dryBulbTemperature: T, meanRadiantTemperature: T,
          relativeHumidity: RH, relativeAirVelocity: velocity,
          clothing: clo, metabolicRate: metSeated, externalWork: 0);
        double ppd = FangerModel.GetPPD(pmv);
        Console.WriteLine(
          $"  {label,-8}  {T,7:F1}    {RH,5:F0}     {pmv,5:F2}   {ppd,6:F1}    {Sensation(pmv)}");
      }

      // Inverse: what dry-bulb temperature yields PMV = 0 (thermal neutrality)?
      Console.WriteLine();
      Console.WriteLine("Inverse lookup — dry-bulb temperature for PMV = 0 (thermal neutrality):");
      foreach (double clo in new[] { 0.5, 1.0 })
      {
        double tNeutral = FangerModel.GetDryBulbTemperature(
          pmv: 0.0, meanRadiantTemperature: 24.0, relativeHumidity: 50.0,
          relativeAirVelocity: velocity, clothing: clo,
          metabolicRate: metSeated, externalWork: 0);
        Console.WriteLine($"  clo = {clo} → Tdb = {tNeutral:F2} °C (with Tr = 24 °C, RH 50 %)");
      }

      return 0;
    }

    private static string Sensation(double pmv)
    {
      return pmv switch
      {
        < -2.5 => "cold",
        < -1.5 => "cool",
        < -0.5 => "slightly cool",
        < 0.5  => "neutral",
        < 1.5  => "slightly warm",
        < 2.5  => "warm",
        _      => "hot",
      };
    }

    private static System.Collections.Generic.IEnumerable<(string label, double T, double RH, double clo)> Cases(
      double cloSummer, double cloWinter)
    {
      // Winter
      yield return ("Winter",  18, 40, cloWinter);
      yield return ("Winter",  22, 40, cloWinter);
      yield return ("Winter",  24, 40, cloWinter);
      // Summer
      yield return ("Summer",  24, 55, cloSummer);
      yield return ("Summer",  26, 55, cloSummer);
      yield return ("Summer",  28, 60, cloSummer);
      yield return ("Summer",  30, 60, cloSummer);
    }
  }
}
