/* IReadOnlyMultiRoom.cs
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

using Popolo.Core.Building.Envelope;
using Popolo.Core.Climate;
using System;

namespace Popolo.Core.Building
{
  /// <summary>
  /// Represents a group of rooms that are solved as a single coupled system
  /// within one time step.
  /// </summary>
  /// <remarks>
  /// <para>
  /// A <see cref="IReadOnlyMultiRoom"/> defines the boundary of <b>tight thermal coupling</b>.
  /// All rooms contained in the same multi-room are solved simultaneously
  /// within each time step, including heat conduction through shared walls and
  /// inter-zone air flow. Their temperatures are obtained as the solution of a
  /// single linear system.
  /// </para>
  /// <para>
  /// In contrast, multiple multi-rooms belonging to the same
  /// <see cref="IReadOnlyBuildingThermalModel"/> are <b>loosely coupled</b>: they
  /// exchange boundary conditions with a one-time-step lag, rather than being
  /// solved together.
  /// </para>
  /// <para>
  /// Use a single multi-room when parts of the building are thermally
  /// interdependent. Split into multiple multi-rooms when weak coupling
  /// (one-step lag) is acceptable, to reduce the linear-system size and enable
  /// parallel computation.
  /// </para>
  /// </remarks>
  public interface IReadOnlyMultiRoom
  {
    /// <summary>Gets the calculation time step [s].</summary>
    double TimeStep { get; }

    /// <summary>Gets a value indicating whether coupled heat and moisture transfer is solved.</summary>
    bool SolveMoistureTransferSimultaneously { get; }

    /// <summary>Gets the current simulation date and time.</summary>
    DateTime CurrentDateTime { get; }

    /// <summary>Gets the solar state.</summary>
    IReadOnlySun Sun { get; }

    /// <summary>Gets the total number of rooms.</summary>
    int RoomCount { get; }

    /// <summary>Gets the total number of zones.</summary>
    int ZoneCount { get; }

    /// <summary>Gets the array of zones.</summary>
    IReadOnlyZone[] Zones { get; }

    /// <summary>Gets the array of wall assemblies.</summary>
    IReadOnlyWall[] Walls { get; }

    /// <summary>Gets the array of window assemblies.</summary>
    IReadOnlyWindow[] Windows { get; }

    /// <summary>Gets the outdoor dry-bulb temperature [°C].</summary>
    double OutdoorTemperature { get; }

    /// <summary>Gets the outdoor humidity ratio [kg/kg].</summary>
    double OutdoorHumidityRatio { get; }

    /// <summary>Gets the nocturnal (long-wave) radiation [W/m²].</summary>
    double NocturnalRadiation { get; }

    /// <summary>Gets the ground surface albedo [-].</summary>
    double Albedo { get; }

    /// <summary>Gets or sets a value indicating whether tilted-surface solar irradiance is provided directly.</summary>
    bool IsSolarIrradianceGiven { set; }

    /// <summary>Gets the air flow rate from zone 1 to zone 2 [kg/s].</summary>
    /// <param name="zoneIndex1">Source zone index.</param>
    /// <param name="zoneIndex2">Destination zone index.</param>
    /// <returns>Air flow rate [kg/s].</returns>
    double GetAirFlow(int zoneIndex1, int zoneIndex2);

    /// <summary>Gets the air flow rate from zone 1 to zone 2 [kg/s].</summary>
    /// <param name="zone1">Source zone.</param>
    /// <param name="zone2">Destination zone.</param>
    /// <returns>Air flow rate [kg/s].</returns>
    double GetAirFlow(IReadOnlyZone zone1, IReadOnlyZone zone2);

    /// <summary>Gets the breakdown of sensible heat flows into the zone (positive = inflow).</summary>
    /// <param name="zoneIndex">Zone index.</param>
    /// <param name="wallSurfaces">Heat flow from wall and window surfaces [W].</param>
    /// <param name="zoneAirChange">Inter-zone ventilation heat flow [W].</param>
    /// <param name="outdoorAir">Outdoor air heat flow [W].</param>
    /// <param name="supplyAir">Supply air heat flow [W].</param>
    /// <param name="heatGains">Internal heat gains [W].</param>
    /// <param name="heatSupply">HVAC heat supply [W].</param>
    void GetBreakdownOfSensibleHeatFlow(
        int zoneIndex,
        out double wallSurfaces, out double zoneAirChange,
        out double outdoorAir, out double supplyAir,
        out double heatGains, out double heatSupply);

    /// <summary>Gets the convective heat flow from a wall surface [W].</summary>
    /// <param name="wallIndex">Wall index.</param>
    /// <param name="isSideF">True for the F side; false for the B side.</param>
    /// <returns>Convective heat flow [W].</returns>
    double GetWallConvectiveHeatFlow(int wallIndex, bool isSideF);

    /// <summary>Gets the breakdown of moisture flows into the zone (positive = inflow).</summary>
    /// <param name="zoneIndex">Zone index.</param>
    /// <param name="zoneAirChange">Inter-zone ventilation moisture flow [kg/s].</param>
    /// <param name="outdoorAir">Outdoor air moisture flow [kg/s].</param>
    /// <param name="supplyAir">Supply air moisture flow [kg/s].</param>
    /// <param name="moistureGains">Internal moisture gains [kg/s].</param>
    /// <param name="moistureSupply">HVAC moisture supply/removal [kg/s].</param>
    void GetBreakdownOfLatentHeatFlow(
        int zoneIndex,
        out double zoneAirChange,
        out double outdoorAir, out double supplyAir,
        out double moistureGains, out double moistureSupply);
  }
}
