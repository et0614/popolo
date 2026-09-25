/* IDemo.cs
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
