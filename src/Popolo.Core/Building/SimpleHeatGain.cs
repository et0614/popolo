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
  /// <summary>A simple heat gain element with fixed convective, radiative, and moisture values.</summary>
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
