/* FlowNotchTable.cs
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
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301, USA.
 */

using System.Collections.Generic;

using Popolo.Core.Exceptions;

namespace Popolo.Core.HVAC.FluidCircuit
{
  /// <summary>
  /// Ordered table of discrete flow notches (e.g., extra-high / high /
  /// low / extra-low) used by fluid machinery with stepped speed control.
  /// </summary>
  /// <remarks>
  /// Notches are ordered by ascending flow rate, which gives
  /// <see cref="TryRaise"/> / <see cref="TryLower"/> their meaning. The
  /// number of steps and their names are free so that any catalog
  /// (強/中/弱, 特強/強/弱/微弱, ...) can be represented.
  /// </remarks>
  internal sealed class FlowNotchTable
  {

    #region Instance variables and properties

    /// <summary>Notches ordered by ascending flow rate.</summary>
    private readonly List<(string name, double flowRate)> notches = new List<(string, double)>();

    /// <summary>Gets the number of notches.</summary>
    public int Count { get { return notches.Count; } }

    /// <summary>Gets the current notch index (-1 when not operating on a notch).</summary>
    public int CurrentIndex { get; private set; } = -1;

    /// <summary>Gets the current notch name (empty when not operating on a notch).</summary>
    public string CurrentName
    { get { return CurrentIndex < 0 ? string.Empty : notches[CurrentIndex].name; } }

    #endregion

    #region Instance methods

    /// <summary>Replaces the notch table.</summary>
    /// <param name="flowNotches">Notches (name and volumetric flow rate [m³/s]) in ascending flow order.</param>
    /// <exception cref="PopoloArgumentException">
    /// Thrown when no notch is given, when a name is empty or duplicated, or
    /// when the flow rates are not positive and strictly ascending.
    /// </exception>
    public void SetNotches((string name, double flowRate)[] flowNotches)
    {
      if (flowNotches == null || flowNotches.Length == 0)
        throw new PopoloArgumentException(
          "At least one notch must be given.", nameof(flowNotches));

      for (int i = 0; i < flowNotches.Length; i++)
      {
        if (string.IsNullOrEmpty(flowNotches[i].name))
          throw new PopoloArgumentException(
            $"The name of notch {i} is empty.", nameof(flowNotches));
        if (flowNotches[i].flowRate <= 0)
          throw new PopoloArgumentException(
            $"The flow rate of notch '{flowNotches[i].name}' must be positive.",
            nameof(flowNotches));
        if (0 < i && flowNotches[i].flowRate <= flowNotches[i - 1].flowRate)
          throw new PopoloArgumentException(
            "The flow rates must be given in strictly ascending order "
            + $"(notch '{flowNotches[i].name}').", nameof(flowNotches));
        for (int j = 0; j < i; j++)
          if (flowNotches[j].name == flowNotches[i].name)
            throw new PopoloArgumentException(
              $"The notch name '{flowNotches[i].name}' is duplicated.", nameof(flowNotches));
      }

      notches.Clear();
      notches.AddRange(flowNotches);
      CurrentIndex = -1;
    }

    /// <summary>Selects a notch and returns its flow rate [m³/s].</summary>
    /// <param name="notchIndex">Notch index.</param>
    /// <returns>Flow rate of the notch [m³/s].</returns>
    /// <exception cref="PopoloArgumentException">Thrown when no notch table has been set.</exception>
    /// <exception cref="PopoloOutOfRangeException">Thrown when the index is out of range.</exception>
    public double Select(int notchIndex)
    {
      ValidateIndex(notchIndex);
      CurrentIndex = notchIndex;
      return notches[notchIndex].flowRate;
    }

    /// <summary>Selects a notch by name and returns its flow rate [m³/s].</summary>
    /// <param name="notchName">Notch name.</param>
    /// <returns>Flow rate of the notch [m³/s].</returns>
    /// <exception cref="PopoloArgumentException">
    /// Thrown when no notch table has been set or the name is not found.
    /// </exception>
    public double Select(string notchName)
    {
      ValidateHasNotches();
      for (int i = 0; i < notches.Count; i++)
        if (notches[i].name == notchName) return Select(i);
      throw new PopoloArgumentException(
        $"The notch '{notchName}' is not found.", nameof(notchName));
    }

    /// <summary>
    /// Steps up to the next notch. When not operating on a notch, the lowest
    /// notch is selected.
    /// </summary>
    /// <param name="flowRate">Flow rate of the selected notch [m³/s].</param>
    /// <returns>False when the notch is already at the maximum (no change).</returns>
    /// <exception cref="PopoloArgumentException">Thrown when no notch table has been set.</exception>
    public bool TryRaise(out double flowRate)
    {
      ValidateHasNotches();
      if (CurrentIndex == Count - 1)
      {
        flowRate = notches[CurrentIndex].flowRate;
        return false;
      }
      flowRate = Select(CurrentIndex < 0 ? 0 : CurrentIndex + 1);
      return true;
    }

    /// <summary>Steps down to the previous notch.</summary>
    /// <param name="flowRate">Flow rate of the selected notch [m³/s].</param>
    /// <returns>
    /// False when the notch is already at the minimum or the machine is not
    /// operating on a notch (no change).
    /// </returns>
    /// <exception cref="PopoloArgumentException">Thrown when no notch table has been set.</exception>
    public bool TryLower(out double flowRate)
    {
      ValidateHasNotches();
      if (CurrentIndex <= 0)
      {
        flowRate = CurrentIndex < 0 ? 0.0 : notches[0].flowRate;
        return false;
      }
      flowRate = Select(CurrentIndex - 1);
      return true;
    }

    /// <summary>Marks the machine as not operating on a notch (continuous flow or shut off).</summary>
    public void Invalidate()
    {
      CurrentIndex = -1;
    }

    /// <summary>Gets the name of the specified notch.</summary>
    /// <param name="notchIndex">Notch index.</param>
    /// <returns>Name of the notch.</returns>
    public string GetName(int notchIndex)
    {
      ValidateIndex(notchIndex);
      return notches[notchIndex].name;
    }

    /// <summary>Gets the flow rate [m³/s] of the specified notch.</summary>
    /// <param name="notchIndex">Notch index.</param>
    /// <returns>Flow rate of the notch [m³/s].</returns>
    public double GetFlowRate(int notchIndex)
    {
      ValidateIndex(notchIndex);
      return notches[notchIndex].flowRate;
    }

    /// <summary>Throws when no notch table has been set.</summary>
    private void ValidateHasNotches()
    {
      if (notches.Count == 0)
        throw new PopoloArgumentException(
          "No flow notches have been set. Call SetFlowNotches first.", "notchIndex");
    }

    /// <summary>Throws when the index is out of range.</summary>
    /// <param name="notchIndex">Notch index.</param>
    private void ValidateIndex(int notchIndex)
    {
      ValidateHasNotches();
      if (notchIndex < 0 || Count <= notchIndex)
        throw new PopoloOutOfRangeException(nameof(notchIndex), notchIndex, 0, Count - 1,
          "Notch index is out of range.");
    }

    #endregion

  }
}
