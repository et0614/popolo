/* SimpleHeatGain.cs
 * 
 * Copyright (C) 2016 E.Togashi
 * 
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 2 of the License, or (at
 * your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful, but
 * WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 675 Mass Ave, Cambridge, MA 02139, USA.
 */

using System;

namespace Popolo.Core.Building
{
  /// <inheritdoc cref="IHeatGain"/>
  /// <remarks>
  /// <para>
  /// <see cref="SimpleHeatGain"/> is the most basic implementation of
  /// <see cref="IHeatGain"/>, holding constant values for the convective sensible
  /// heat, radiative sensible heat, and moisture generation rate. Use it when the
  /// heat gain is known a priori and does not depend on zone state (typical for
  /// schedule-driven loads).
  /// </para>
  /// <para>
  /// Because the returned values do not depend on zone state, the
  /// <c>IReadOnlyZone</c> argument is ignored by all three Get* methods and is
  /// accepted only to satisfy the <see cref="IHeatGain"/> contract.
  /// </para>
  /// </remarks>
  public class SimpleHeatGain : IHeatGain
  {
    /// <summary>Gets or sets the convective sensible heat gain [W].</summary>
    public double ConvectiveHeatGain { get; set; }

    /// <summary>Gets or sets the radiative sensible heat gain [W].</summary>
    public double RadiativeHeatGain { get; set; }

    /// <summary>Gets or sets the moisture generation rate [kg/s].</summary>
    public double MoistureGain { get; set; }

    /// <summary>Gets the convective sensible heat gain [W].</summary>
    /// <param name="zone">Not used; included for interface compatibility.</param>
    /// <returns>Convective sensible heat gain [W].</returns>
    public double GetConvectiveHeatGain(IReadOnlyZone zone) { return ConvectiveHeatGain; }

    /// <summary>Gets the radiative sensible heat gain [W].</summary>
    /// <param name="zone">Not used; included for interface compatibility.</param>
    /// <returns>Radiative sensible heat gain [W].</returns>
    public double GetRadiativeHeatGain(IReadOnlyZone zone) { return RadiativeHeatGain; }

    /// <summary>Gets the moisture generation rate [kg/s].</summary>
    /// <param name="zone">Not used; included for interface compatibility.</param>
    /// <returns>Moisture generation rate [kg/s].</returns>
    public double GetMoistureGain(IReadOnlyZone zone) { return MoistureGain; }

    /// <summary>Initializes a new instance with specified heat gain values.</summary>
    /// <param name="convectiveHeatGain">Convective sensible heat gain [W].</param>
    /// <param name="radiativeHeatGain">Radiative sensible heat gain [W].</param>
    /// <param name="moistureGain">Moisture generation rate [kg/s].</param>
    public SimpleHeatGain(double convectiveHeatGain, double radiativeHeatGain, double moistureGain)
    {
      ConvectiveHeatGain = convectiveHeatGain;
      RadiativeHeatGain = radiativeHeatGain;
      MoistureGain = moistureGain;
    }
  }
}
