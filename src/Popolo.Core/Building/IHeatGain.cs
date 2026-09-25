/* IHeatGain.cs
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

namespace Popolo.Core.Building
{
  /// <summary>
  /// Represents an internal heat gain element that contributes sensible heat and
  /// moisture to a thermal zone.
  /// </summary>
  /// <remarks>
  /// <para>
  /// Heat gain elements model sources that release energy or moisture inside a
  /// zone — for example, occupants, lighting, and office equipment. Each element
  /// reports three quantities evaluated against the zone it belongs to:
  /// the convective sensible heat gain, the radiative sensible heat gain, and
  /// the moisture generation rate.
  /// </para>
  /// <para>
  /// The convective component is added directly to the zone air heat balance,
  /// while the radiative component is distributed to surrounding surfaces
  /// (walls, windows) through the short-wave radiation balance. The moisture
  /// component feeds the zone humidity ratio balance.
  /// </para>
  /// <para>
  /// Each method receives the zone as an argument so that implementations can
  /// make the heat gain a function of zone state (e.g., temperature-dependent
  /// equipment loads). Implementations that return constant values can ignore
  /// the <see cref="IReadOnlyZone"/> parameter — see
  /// <see cref="SimpleHeatGain"/>.
  /// </para>
  /// </remarks>
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
