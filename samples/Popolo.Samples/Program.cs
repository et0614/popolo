/* Program.cs
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
using System.Linq;

using Popolo.Samples.Demos.Core;
using Popolo.Samples.Demos.IO;
using Popolo.Samples.Demos.Webpro;

namespace Popolo.Samples
{
  /// <summary>
  /// Entry point for the Popolo samples runner.
  /// </summary>
  /// <remarks>
  /// <para>
  /// This program bundles all Popolo samples (Core / IO / Webpro) into a
  /// single executable. Users select a specific demo by name on the command
  /// line; run without arguments (or with <c>list</c>) to see what is
  /// available.
  /// </para>
  /// <para>
  /// Usage:
  /// </para>
  /// <code>
  /// dotnet run --project samples/Popolo.Samples -- list
  /// dotnet run --project samples/Popolo.Samples -- &lt;demo-name&gt; [demo args]
  /// </code>
  /// </remarks>
  public static class Program
  {

    #region Registry

    /// <summary>Registry of all available demos. Add new demos here.</summary>
    private static readonly IDemo[] Demos =
    {
      // Core — physics / climate / numerics
      new MoistAirDemo(),
      new SteamTableDemo(),
      new SunPositionDemo(),
      new InclineIrradianceDemo(),
      new OdeSolverDemo(),
      new RegressionDemo(),

      // Core — envelope / HVAC / comfort
      new WallResponseDemo(),
      new ChillerDemo(),
      new PmvDemo(),
      new TanabeDemo(),
      new VRFNEDOTestDemo(),

      // IO
      new WeatherSummaryDemo(),
      new JsonRoundTripDemo(),

      // Webpro
      new AnnualSimulationDemo(),
    };

    #endregion

    #region Main

    public static int Main(string[] args)
    {
      if (args.Length == 0 || args[0] is "list" or "--list" or "-l" or "help" or "--help" or "-h")
      {
        PrintListing();
        return 0;
      }

      string demoName = args[0];
      var demo = Demos.FirstOrDefault(d =>
        string.Equals(d.Name, demoName, StringComparison.OrdinalIgnoreCase));

      if (demo is null)
      {
        Console.Error.WriteLine($"Unknown demo: '{demoName}'.");
        Console.Error.WriteLine();
        PrintListing();
        return 1;
      }

      // 2 番目以降の引数を demo に渡す
      var demoArgs = args.Skip(1).ToArray();
      try
      {
        return demo.Run(demoArgs);
      }
      catch (Exception ex)
      {
        Console.Error.WriteLine($"Demo '{demoName}' threw an exception: {ex.Message}");
        Console.Error.WriteLine(ex.StackTrace);
        return 2;
      }
    }

    #endregion

    #region Helpers

    private static void PrintListing()
    {
      Console.WriteLine("Popolo samples runner");
      Console.WriteLine();
      Console.WriteLine("Usage:");
      Console.WriteLine("  dotnet run --project samples/Popolo.Samples -- <demo-name> [demo args]");
      Console.WriteLine("  dotnet run --project samples/Popolo.Samples -- list");
      Console.WriteLine();
      Console.WriteLine("Available demos:");

      foreach (var group in Demos.GroupBy(d => d.Category).OrderBy(g => g.Key))
      {
        Console.WriteLine();
        Console.WriteLine($"  [{group.Key}]");
        foreach (var demo in group.OrderBy(d => d.Name))
          Console.WriteLine($"    {demo.Name,-24} {demo.Description}");
      }
    }

    #endregion
  }
}
