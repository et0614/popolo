/* IReadOnlyOpticalLayeredEnvelope.cs
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
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
 * General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 675 Mass Ave, Cambridge, MA 02139, USA.
 */

using Popolo.Core.Numerics.LinearAlgebra;

namespace Popolo.Core.Building.Envelope
{
  /// <summary>
  /// Read-only view of any layered envelope component (wall, window, or
  /// future translucent wall) — exposes the F/B side state and matrix-solver
  /// outputs uniformly without requiring the caller to know the concrete
  /// type.
  /// </summary>
  /// <remarks>
  /// <para>
  /// Parent of <see cref="IReadOnlyWall"/> and <see cref="IReadOnlyWindow"/>.
  /// Code that just needs to inspect surface temperatures, film coefficients,
  /// or sol-air boundary state can take an
  /// <see cref="IReadOnlyOpticalLayeredEnvelope"/> and treat both walls and
  /// windows uniformly. Subtype-specific properties (e.g.
  /// <see cref="IReadOnlyWall.ComputeMoistureTransfer"/>,
  /// <see cref="IReadOnlyWindow.OutsideIncline"/>) remain on the more
  /// specific interfaces.
  /// </para>
  /// <para>
  /// F and B denote the two opposing sides of the component. By convention
  /// F is the outdoor-facing side; the user can place a <see cref="Wall"/>
  /// with B outdoors via the explicit-side overload of
  /// <c>MultiRoom.SetOutsideEnvelope</c>, but a <see cref="Window"/> always
  /// has F as its outdoor face (its per-layer optical model is built around
  /// that orientation).
  /// </para>
  /// </remarks>
  public interface IReadOnlyOpticalLayeredEnvelope
  {
    /// <summary>Gets the surface area [m²].</summary>
    double Area { get; }

    /// <summary>Gets the number of nodes in the implicit-Euler matrix network.</summary>
    int NodeCount { get; }

    /// <summary>Gets the calculation time step [s].</summary>
    double TimeStep { get; }

    /// <summary>Gets the nodal temperature distribution [°C].</summary>
    IVector Temperatures { get; }

    /// <summary>Gets the combined heat transfer coefficient on the F side [W/(m²·K)].</summary>
    double FilmCoefficientF { get; }

    /// <summary>Gets the convective heat transfer coefficient on the F side [W/(m²·K)].</summary>
    double ConvectiveCoefficientF { get; }

    /// <summary>Gets the radiative heat transfer coefficient on the F side [W/(m²·K)].</summary>
    double RadiativeCoefficientF { get; }

    /// <summary>Gets the long-wave (thermal) emissivity on the F side [-].</summary>
    double LongWaveEmissivityF { get; }

    /// <summary>Gets the sol-air temperature on the F side [°C].</summary>
    double SolAirTemperatureF { get; }

    /// <summary>Gets the surface temperature on the F side [°C].</summary>
    double SurfaceTemperatureF { get; }

    /// <summary>Gets the combined heat transfer coefficient on the B side [W/(m²·K)].</summary>
    double FilmCoefficientB { get; }

    /// <summary>Gets the convective heat transfer coefficient on the B side [W/(m²·K)].</summary>
    double ConvectiveCoefficientB { get; }

    /// <summary>Gets the radiative heat transfer coefficient on the B side [W/(m²·K)].</summary>
    double RadiativeCoefficientB { get; }

    /// <summary>Gets the long-wave (thermal) emissivity on the B side [-].</summary>
    double LongWaveEmissivityB { get; }

    /// <summary>Gets the sol-air temperature on the B side [°C].</summary>
    double SolAirTemperatureB { get; }

    /// <summary>Gets the surface temperature on the B side [°C].</summary>
    double SurfaceTemperatureB { get; }

    /// <summary>Gets the short-wave (solar) absorptance on the F side [-].</summary>
    double ShortWaveAbsorptanceF { get; }

    /// <summary>Gets the short-wave (solar) absorptance on the B side [-].</summary>
    double ShortWaveAbsorptanceB { get; }

    /// <summary>Gets the humidity ratio on the F side [kg/kg]. 0 for components without coupled moisture transport.</summary>
    double HumidityRatioF { get; }

    /// <summary>Gets the humidity ratio on the B side [kg/kg]. 0 for components without coupled moisture transport.</summary>
    double HumidityRatioB { get; }

    /// <summary>Gets the moisture transfer coefficient on the F side [(kg/s)/((kg/kg)·m²)]. 0 for components without coupled moisture transport.</summary>
    double MoistureCoefficientF { get; }

    /// <summary>Gets the moisture transfer coefficient on the B side [(kg/s)/((kg/kg)·m²)]. 0 for components without coupled moisture transport.</summary>
    double MoistureCoefficientB { get; }
  }
}
