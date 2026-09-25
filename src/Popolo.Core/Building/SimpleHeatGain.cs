/* SimpleHeatGain.cs
 * 
 * Copyright (C) 2016 E.Togashi
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
