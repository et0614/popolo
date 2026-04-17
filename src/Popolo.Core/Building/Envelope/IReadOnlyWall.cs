/* IReadOnlyWall.cs
 * 
 * Copyright (C) 2026 E.Togashi
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

using Popolo.Core.Numerics.LinearAlgebra;

namespace Popolo.Core.Building.Envelope
{
  /// <summary>Represents a read-only view of a wall or floor assembly.</summary>
  /// <remarks>
  /// F and B denote the two opposing sides of the wall.
  /// For external walls, F is conventionally the outdoor-facing side.
  /// </remarks>
  public interface IReadOnlyWall
  {
    /// <summary>Gets the wall ID.</summary>
    int ID { get; }

    /// <summary>Gets a value indicating whether moisture transfer is solved.</summary>
    bool ComputeMoistureTransfer { get; }

    /// <summary>Gets the number of nodes in the finite difference model.</summary>
    int NodeCount { get; }

    /// <summary>Gets the temperature distribution vector [°C].</summary>
    IVector Temperatures { get; }

    /// <summary>Gets the humidity ratio distribution vector [kg/kg].</summary>
    IVector Humidities { get; }

    /// <summary>Gets the wall surface area [m²].</summary>
    double Area { get; }

    /// <summary>Gets the calculation time step [s].</summary>
    double TimeStep { get; }

    /// <summary>Gets the combined heat transfer coefficient on the F side [W/(m²·K)].</summary>
    double FilmCoefficientF { get; }

    /// <summary>Gets the convective heat transfer coefficient on the F side [W/(m²·K)].</summary>
    double ConvectiveCoefficientF { get; }

    /// <summary>Gets the radiative heat transfer coefficient on the F side [W/(m²·K)].</summary>
    double RadiativeCoefficientF { get; }

    /// <summary>Gets the moisture transfer coefficient on the F side [(kg/s)/((kg/kg)·m²)].</summary>
    double MoistureCoefficientF { get; }

    /// <summary>Gets the short-wave (solar) absorptance on the F side [-].</summary>
    double ShortWaveAbsorptanceF { get; }

    /// <summary>Gets the long-wave (thermal) emissivity on the F side [-].</summary>
    double LongWaveEmissivityF { get; }

    /// <summary>Gets the sol-air temperature on the F side [°C].</summary>
    double SolAirTemperatureF { get; }

    /// <summary>Gets the humidity ratio on the F side [kg/kg].</summary>
    double HumidityRatioF { get; }

    /// <summary>Gets the combined heat transfer coefficient on the B side [W/(m²·K)].</summary>
    double FilmCoefficientB { get; }

    /// <summary>Gets the convective heat transfer coefficient on the B side [W/(m²·K)].</summary>
    double ConvectiveCoefficientB { get; }

    /// <summary>Gets the radiative heat transfer coefficient on the B side [W/(m²·K)].</summary>
    double RadiativeCoefficientB { get; }

    /// <summary>Gets the moisture transfer coefficient on the B side [(kg/s)/((kg/kg)·m²)].</summary>
    double MoistureCoefficientB { get; }

    /// <summary>Gets the short-wave (solar) absorptance on the B side [-].</summary>
    double ShortWaveAbsorptanceB { get; }

    /// <summary>Gets the long-wave (thermal) emissivity on the B side [-].</summary>
    double LongWaveEmissivityB { get; }

    /// <summary>Gets the sol-air temperature on the B side [°C].</summary>
    double SolAirTemperatureB { get; }

    /// <summary>Gets the humidity ratio on the B side [kg/kg].</summary>
    double HumidityRatioB { get; }

    /// <summary>Gets the buried pipe at the specified node.</summary>
    /// <param name="node">The node index at which the pipe is embedded.</param>
    /// <returns>The buried pipe, or null if no pipe is embedded at that node.</returns>
    IReadOnlyBuriedPipe GetPipe(int node);

    /// <summary>Gets the heat transfer rate from the buried pipe at the specified node [W].</summary>
    /// <param name="mIndex">Node index.</param>
    /// <returns>Heat transfer rate from the pipe [W].</returns>
    double GetHeatTransferFromPipe(int mIndex);

    /// <summary>Gets the outlet water temperature of the buried pipe at the specified node [°C].</summary>
    /// <param name="mIndex">Node index.</param>
    /// <returns>Outlet water temperature [°C].</returns>
    double GetOutletWaterTemperature(int mIndex);

    /// <summary>Gets the wall layer array.</summary>
    IReadOnlyWallLayer[] Layers { get; }

    /// <summary>Gets the surface heat flux [W/m²]. Positive values indicate heat absorption.</summary>
    /// <param name="isSideF">True for the F side; false for the B side.</param>
    /// <returns>Surface heat flux [W/m²].</returns>
    double GetSurfaceHeatTransfer(bool isSideF);


    /// <summary>Gets the surface temperature on the F side [°C].</summary>
    double SurfaceTemperatureF { get; }

    /// <summary>Gets the surface temperature on the B side [°C].</summary>
    double SurfaceTemperatureB { get; }
  }
}