/* Program.cs
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
using System.Linq;

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

    #region レジストリ

    /// <summary>Registry of all available demos. Add new demos here.</summary>
    private static readonly IDemo[] Demos =
    {
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

    #region ヘルパー

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
