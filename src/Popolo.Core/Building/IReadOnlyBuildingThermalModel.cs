/* IReadOnlyBuildingThermalModel.cs
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
using Popolo.Core.Building.Envelope;
using Popolo.Core.Climate;

namespace Popolo.Core.Building
{
    /// <summary>Represents a read-only view of a building thermal load calculation model.</summary>
    public interface IReadOnlyBuildingThermalModel
    {
        /// <summary>Gets the array of multi-room systems.</summary>
        IReadOnlyMultiRooms[] MultiRoom { get; }

        /// <summary>Gets the current simulation date and time.</summary>
        DateTime CurrentDateTime { get; }

        /// <summary>Gets the calculation time step [s].</summary>
        double TimeStep { get; }

        /// <summary>Gets the solar state.</summary>
        IReadOnlySun Sun { get; }

        /// <summary>Gets the outdoor dry-bulb temperature [°C].</summary>
        double OutdoorTemperature { get; }

        /// <summary>Gets the outdoor humidity ratio [kg/kg].</summary>
        double OutdoorHumidityRatio { get; }

        /// <summary>Gets the nocturnal (long-wave) radiation [W/m²].</summary>
        double NocturnalRadiation { get; }

        /// <summary>Gets all wall assemblies in the model across all MultiRooms.</summary>
        /// <returns>Array of read-only wall assemblies.</returns>
        IReadOnlyWall[] GetWalls();

        /// <summary>Gets all zones in the model across all MultiRooms.</summary>
        /// <returns>Array of read-only zones.</returns>
        IReadOnlyZone[] GetZones();

        /// <summary>Gets the breakdown of sensible heat flows into the zone (positive = inflow).</summary>
        /// <param name="mRoomIndex">MultiRooms index.</param>
        /// <param name="zoneIndex">Zone index.</param>
        /// <param name="wallSurfaces">Heat flow from wall and window surfaces [W].</param>
        /// <param name="zoneAirChange">Inter-zone ventilation heat flow [W].</param>
        /// <param name="outdoorAir">Outdoor air heat flow [W].</param>
        /// <param name="supplyAir">Supply air heat flow [W].</param>
        /// <param name="heatGains">Internal heat gains [W].</param>
        /// <param name="heatSupply">HVAC heat supply [W].</param>
        void GetBreakdownOfSensibleHeatFlow(
            int mRoomIndex, int zoneIndex,
            out double wallSurfaces, out double zoneAirChange,
            out double outdoorAir, out double supplyAir,
            out double heatGains, out double heatSupply);

        /// <summary>Gets the convective heat flow from a wall surface [W].</summary>
        /// <param name="mRoomIndex">MultiRooms index.</param>
        /// <param name="wallIndex">Wall index.</param>
        /// <param name="isSideF">True for the F-side surface; false for the B side.</param>
        /// <returns>Convective heat flow [W].</returns>
        double GetWallConvectiveHeatFlow(int mRoomIndex, int wallIndex, bool isSideF);

        /// <summary>Gets the breakdown of moisture flows into the zone (positive = inflow).</summary>
        /// <param name="mRoomIndex">MultiRooms index.</param>
        /// <param name="zoneIndex">Zone index.</param>
        /// <param name="zoneAirChange">Inter-zone ventilation moisture flow [kg/s].</param>
        /// <param name="outdoorAir">Outdoor air moisture flow [kg/s].</param>
        /// <param name="supplyAir">Supply air moisture flow [kg/s].</param>
        /// <param name="moistureGains">Internal moisture gains [kg/s].</param>
        /// <param name="moistureSupply">HVAC moisture supply/removal [kg/s].</param>
        void GetBreakdownOfLatentHeatFlow(
            int mRoomIndex, int zoneIndex,
            out double zoneAirChange,
            out double outdoorAir, out double supplyAir,
            out double moistureGains, out double moistureSupply);
    }
}
