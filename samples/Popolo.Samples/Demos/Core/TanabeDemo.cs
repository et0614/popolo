/* TanabeDemo.cs
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
  /// Tracks skin and core temperatures of the Tanabe multi-node body model
  /// over a 60-minute step-down from a warm room (30 °C) to a cool room (22 °C).
  /// </summary>
  /// <remarks>
  /// Exercises <see cref="TanabeMultiNodeModel"/> transient response. The
  /// subject starts thermally equilibrated at 30 °C / 50 %RH, steps into a
  /// 22 °C / 50 %RH environment at t = 0, and skin/core temperatures are
  /// reported every 10 minutes. Clothing is uniform light indoor (0.5 clo).
  /// </remarks>
  public sealed class TanabeDemo : IDemo
  {
    public string Name => "comfort-tanabe";
    public string Category => "Core";
    public string Description => "Tanabe 65-node body model response to a warm→cool step.";

    public int Run(string[] args)
    {
      // Standard adult male, standing, 0.5 clo uniform clothing.
      var body = new TanabeMultiNodeModel();
      body.SetMetabolicRate(1.0);                       // 1.0 met (resting)
      foreach (TanabeMultiNodeModel.Node n in (TanabeMultiNodeModel.Node[])Enum.GetValues(typeof(TanabeMultiNodeModel.Node)))
        body.SetClothingIndex(n, 0.5);

      // Soak at the warm start to let the body equilibrate.
      body.InitializeTemperature(36.0);
      body.UpdateBoundary(velocity: 0.1, meanRadiantTemperature: 30.0, dryBulbTemperature: 30.0, relativeHumidity: 50.0);
      for (int i = 0; i < 60; i++)     // 60 × 60 s = 1 h soak
        body.Update(60.0);

      Console.WriteLine("Tanabe 65-node body model — step from 30 °C to 22 °C at t = 0");
      Console.WriteLine("Activity: 1.0 met (resting), clothing: 0.5 clo uniform");
      Console.WriteLine();
      PrintHeader();
      PrintRow(0, body);

      // Switch to cool environment and step 60 minutes, reporting every 10 min.
      body.UpdateBoundary(velocity: 0.1, meanRadiantTemperature: 22.0, dryBulbTemperature: 22.0, relativeHumidity: 50.0);
      for (int minute = 1; minute <= 60; minute++)
      {
        body.Update(60.0);
        if (minute % 10 == 0) PrintRow(minute, body);
      }

      return 0;
    }

    private static void PrintHeader()
    {
      Console.WriteLine("   t [min]   Tcore [°C]   Tskin_avg [°C]   Tskin_head [°C]   Tskin_hand [°C]");
      Console.WriteLine("  --------  ------------  ---------------  ----------------  ----------------");
    }

    private static void PrintRow(int minute, TanabeMultiNodeModel body)
    {
      double tcore = body.CentralBloodTemperature;
      double tskinAvg = body.GetAverageSkinTemperature();
      double tskinHead = body.GetTemperature(TanabeMultiNodeModel.Node.Head, TanabeMultiNodeModel.Layer.Skin);
      double tskinHand = body.GetTemperature(TanabeMultiNodeModel.Node.LeftHand, TanabeMultiNodeModel.Layer.Skin);
      Console.WriteLine(
        $"   {minute,6}   {tcore,10:F2}   {tskinAvg,13:F2}   {tskinHead,14:F2}   {tskinHand,14:F2}");
    }
  }
}
