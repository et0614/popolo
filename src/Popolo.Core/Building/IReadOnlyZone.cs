/* IReadOnlyZone.cs
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

namespace Popolo.Core.Building
{
  /// <summary>
  /// Represents a read-only view of a thermal zone — a single well-mixed air
  /// volume characterized by one dry-bulb temperature and one humidity ratio.
  /// </summary>
  /// <remarks>
  /// <para>
  /// A thermal zone is the smallest unit of conditioned space in the building
  /// model. The zone solver assumes <b>perfect mixing</b> within the volume:
  /// one <see cref="Temperature"/> and one <see cref="HumidityRatio"/>
  /// represent the whole zone air at any instant, and spatial gradients inside
  /// the zone are neglected. Rooms that require internal stratification should
  /// be modeled as multiple connected zones.
  /// </para>
  /// <para>
  /// A zone participates in four balance equations solved by its enclosing
  /// <see cref="IReadOnlyMultiRoom"/>:
  /// <list type="bullet">
  ///   <item><description><b>Sensible heat balance</b> against surrounding walls / windows
  ///     (see <see cref="GetWallReferences"/> and <see cref="GetWindows"/>),
  ///     ventilation and supply air, and internal heat gains.</description></item>
  ///   <item><description><b>Moisture balance</b> against outdoor air exchange, supply air,
  ///     and internal moisture gains.</description></item>
  ///   <item><description><b>Short-wave radiation balance</b> for solar irradiance that
  ///     enters through windows and is distributed to interior surfaces.</description></item>
  ///   <item><description><b>Long-wave radiation balance</b> between the interior surfaces.</description></item>
  /// </list>
  /// Either temperature or humidity can be held at a setpoint by HVAC control
  /// (<see cref="TemperatureControlled"/>, <see cref="HumidityControlled"/>);
  /// when not controlled, the state floats freely subject to the balance equations.
  /// </para>
  /// <para>
  /// <see cref="HeatCapacity"/> and <see cref="MoistureCapacity"/> represent the
  /// thermal/moisture mass of <i>contents other than air</i> (furniture, goods,
  /// carpets, etc.). The air mass itself is tracked separately in
  /// <see cref="AirMass"/> and derived from the floor area and height supplied
  /// when the zone was created. Using non-zero capacities for contents adds a
  /// time constant that damps rapid transients and better matches measured
  /// behavior in occupied buildings.
  /// </para>
  /// <para>
  /// HVAC capacity limits (<see cref="HeatingCapacity"/>,
  /// <see cref="CoolingCapacity"/>, <see cref="HumidifyingCapacity"/>,
  /// <see cref="DehumidifyingCapacity"/>) bound the heat/moisture the zone can
  /// request in controlled mode: if the setpoint cannot be reached within the
  /// cap, the state overshoots the setpoint by the deficit.
  /// </para>
  /// </remarks>
  public interface IReadOnlyZone
  {
    /// <summary>Gets the name of the zone.</summary>
    string Name { get; }

    /// <summary>Gets the ventilation air flow rate [kg/s].</summary>
    double VentilationRate { get; }

    /// <summary>Gets the supply air flow rate [kg/s].</summary>
    double SupplyAirFlowRate { get; }

    /// <summary>Gets the air mass in the zone [kg].</summary>
    double AirMass { get; }

    /// <summary>Gets the floor area of the zone [m²].</summary>
    double FloorArea { get; }

    /// <summary>Gets the room index to which this zone belongs (-1 if unassigned).</summary>
    int RoomIndex { get; }

    /// <summary>Gets the multi-room system to which this zone belongs.</summary>
    IReadOnlyMultiRoom MultiRoom { get; }

    /// <summary>Gets the sensible heat capacity of objects other than air [J/K].</summary>
    double HeatCapacity { get; }

    /// <summary>Gets the moisture capacity of objects other than air [kg].</summary>
    double MoistureCapacity { get; }

    /// <summary>Gets the dry-bulb temperature of the zone air [°C].</summary>
    double Temperature { get; }

    /// <summary>Gets the sensible heat supply to the zone [W].</summary>
    double HeatSupply { get; }

    /// <summary>Gets a value indicating whether the zone dry-bulb temperature is being controlled.</summary>
    bool TemperatureControlled { get; }

    /// <summary>Gets the supply air temperature [°C].</summary>
    double SupplyAirTemperature { get; }

    /// <summary>Gets the humidity ratio of the zone air [kg/kg].</summary>
    double HumidityRatio { get; }

    /// <summary>Gets the moisture supply to the zone [kg/s].</summary>
    double MoistureSupply { get; }

    /// <summary>Gets a value indicating whether the zone humidity ratio is being controlled.</summary>
    bool HumidityControlled { get; }

    /// <summary>Gets the supply air humidity ratio [kg/kg].</summary>
    double SupplyAirHumidityRatio { get; }

    /// <summary>Gets the maximum heating capacity [W].</summary>
    double HeatingCapacity { get; }

    /// <summary>Gets the maximum cooling capacity [W].</summary>
    double CoolingCapacity { get; }

    /// <summary>Gets the maximum humidifying capacity [kg/s].</summary>
    double HumidifyingCapacity { get; }

    /// <summary>Gets the maximum dehumidifying capacity [kg/s].</summary>
    double DehumidifyingCapacity { get; }

    /// <summary>Gets all heat gain elements assigned to this zone.</summary>
    /// <returns>Array of heat gain elements.</returns>
    IHeatGain[] GetHeatGains();

    /// <summary>Integrates the convective sensible heat gains from all elements [W].</summary>
    /// <returns>Total convective sensible heat gain [W].</returns>
    double IntegrateConvectiveHeatgains();

    /// <summary>Integrates the radiative sensible heat gains from all elements [W].</summary>
    /// <returns>Total radiative sensible heat gain [W].</returns>
    double IntegrateRadiativeHeatGains();

    /// <summary>Integrates the moisture gains from all elements [kg/s].</summary>
    /// <returns>Total moisture generation rate [kg/s].</returns>
    double IntegrateMoistureGains();

    /// <summary>Computes the mean surface temperature weighted by emissivity and area.</summary>
    /// <returns>Emissivity–area weighted mean surface temperature [°C].</returns>
    double GetMeanSurfaceTemperature();

    /// <summary>Gets the total window area facing this zone [m²].</summary>
    /// <returns>Total window area [m²].</returns>
    double GetTotalWindowArea();

    /// <summary>Gets all windows facing this zone.</summary>
    /// <returns>Array of read-only window instances.</returns>
    IReadOnlyWindow[] GetWindows();
    /// <summary>Gets references to all walls facing this zone (wall ID + side flag).</summary>
    WallSurfaceReference[] GetWallReferences();

    /// <summary>Gets the sensible heat input from supply air [W].</summary>
    /// <returns>Supply air sensible heat [W].</returns>
    double GetSupplyAirHeat();

    /// <summary>Gets the moisture input from supply air [kg/s].</summary>
    /// <returns>Supply air moisture [kg/s].</returns>
    double GetSupplyAirMoisture();
  }
}