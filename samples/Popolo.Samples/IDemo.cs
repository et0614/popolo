/* IDemo.cs
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

namespace Popolo.Samples
{
  /// <summary>
  /// Contract implemented by every sample demo in this project.
  /// </summary>
  /// <remarks>
  /// <para>
  /// Each demo lives under <c>Demos/&lt;category&gt;/</c> and is instantiated
  /// once in <see cref="Program"/>. To add a new sample:
  /// </para>
  /// <list type="number">
  ///   <item><description>Create a class that implements <see cref="IDemo"/>.</description></item>
  ///   <item><description>Add an instance to the <c>Demos</c> array in <see cref="Program"/>.</description></item>
  /// </list>
  /// <para>
  /// The <see cref="Name"/> is what users type on the command line, so it
  /// should be short, lowercase, and hyphen-separated (e.g. <c>"webpro-annual"</c>).
  /// </para>
  /// </remarks>
  public interface IDemo
  {
    /// <summary>Short kebab-case identifier used on the command line (e.g. "webpro-annual").</summary>
    string Name { get; }

    /// <summary>One-line description shown in the demo listing.</summary>
    string Description { get; }

    /// <summary>Category label (e.g. "Core", "IO", "Webpro") used to group demos in listings.</summary>
    string Category { get; }

    /// <summary>Runs the demo with its own arguments (the command-line tail after the demo name).</summary>
    /// <param name="args">Demo-specific arguments.</param>
    /// <returns>Process exit code (0 = success).</returns>
    int Run(string[] args);
  }
}
