/* IHeatGain.cs
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

namespace Popolo.Core.Building
{
  /// <summary>Represents a heat gain element that contributes sensible heat and moisture to a thermal zone.</summary>
  public interface IHeatGain
  {
    /// <summary>Gets the convective component of the sensible heat gain [W].</summary>
    /// <param name="zone">The zone to which this heat gain element belongs.</param>
    /// <returns>Convective sensible heat gain [W].</returns>
    double GetConvectiveHeatGain(IReadOnlyZone zone);

    /// <summary>Gets the radiative component of the sensible heat gain [W].</summary>
    /// <param name="zone">The zone to which this heat gain element belongs.</param>
    /// <returns>Radiative sensible heat gain [W].</returns>
    double GetRadiativeHeatGain(IReadOnlyZone zone);

    /// <summary>Gets the moisture generation rate [kg/s].</summary>
    /// <param name="zone">The zone to which this heat gain element belongs.</param>
    /// <returns>Moisture generation rate [kg/s].</returns>
    double GetMoistureGain(IReadOnlyZone zone);
  }
}
