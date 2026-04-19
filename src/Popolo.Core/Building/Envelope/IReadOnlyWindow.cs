/* IReadOnlyWindow.cs
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

using Popolo.Core.Climate;

namespace Popolo.Core.Building.Envelope
{
  /// <summary>
  /// Represents a read-only view of a window assembly composed of multiple
  /// glazing layers and optional interior shading devices.
  /// </summary>
  /// <remarks>
  /// <para>
  /// A window resolves solar radiation as the combined effect of glazing
  /// transmittance / reflectance / absorptance, air-gap thermal resistances,
  /// and any <see cref="IShadingDevice"/> installed between (or next to) the
  /// glazing layers. The glazing optical properties can depend on the angle
  /// of incidence for direct solar radiation, while diffuse properties are
  /// integrated from the same angular coefficients.
  /// </para>
  /// <para>
  /// F and B denote the two opposing sides, matching <see cref="IReadOnlyWall"/>:
  /// for a typical exterior window, F is the outdoor side and B is the indoor
  /// side. The interface therefore reports a separate family of properties
  /// for each direction of solar incidence:
  /// <list type="bullet">
  ///   <item><description><b>Incident</b> (from outdoors): <see cref="DirectSolarIncidentTransmittance"/>,
  ///     <see cref="DiffuseSolarIncidentTransmittance"/>, and their reflectance / absorptance pairs.</description></item>
  ///   <item><description><b>Lost</b> (from indoors): <see cref="DiffuseSolarLostTransmittance"/> and its
  ///     reflectance / absorptance counterparts (used for re-radiation from the zone).</description></item>
  /// </list>
  /// </para>
  /// <para>
  /// Exterior shading such as overhangs or fins is modeled separately by a
  /// <see cref="SunShade"/> attached to the window surface; this is orthogonal
  /// to the interior <see cref="IShadingDevice"/> stack accessed via
  /// <see cref="GetShadingDevice"/>.
  /// </para>
  /// </remarks>
  public interface IReadOnlyWindow
  {
    /// <summary>Gets the window surface area [m²].</summary>
    double Area { get; }

    /// <summary>Gets the tilted surface orientation of the outdoor-facing side.</summary>
    IReadOnlyIncline OutsideIncline { get; }

    /// <summary>Gets the total transmittance for direct solar irradiance from outdoors [-].</summary>
    double DirectSolarIncidentTransmittance { get; }

    /// <summary>Gets the total reflectance for direct solar irradiance from outdoors [-].</summary>
    double DirectSolarIncidentReflectance { get; }

    /// <summary>Gets the absorbed solar heat gain coefficient for direct irradiance from outdoors [-].</summary>
    double DirectSolarIncidentAbsorptance { get; }

    /// <summary>Gets the total transmittance for diffuse solar irradiance from outdoors [-].</summary>
    double DiffuseSolarIncidentTransmittance { get; }

    /// <summary>Gets the total reflectance for diffuse solar irradiance from outdoors [-].</summary>
    double DiffuseSolarIncidentReflectance { get; }

    /// <summary>Gets the absorbed solar heat gain coefficient for diffuse irradiance from outdoors [-].</summary>
    double DiffuseSolarIncidentAbsorptance { get; }

    /// <summary>Gets the total transmittance for diffuse solar irradiance from indoors [-].</summary>
    double DiffuseSolarLostTransmittance { get; }

    /// <summary>Gets the total reflectance for diffuse solar irradiance from indoors [-].</summary>
    double DiffuseSolarLostReflectance { get; }

    /// <summary>Gets the absorbed solar heat gain coefficient for diffuse irradiance from indoors [-].</summary>
    double DiffuseSolarLostAbsorptance { get; }

    /// <summary>Gets the number of glazing layers.</summary>
    int GlazingCount { get; }

    /// <summary>Gets the exterior solar shading device.</summary>
    SunShade SunShade { get; }

    /// <summary>Gets the sol-air temperature on the F side (outdoor) [°C].</summary>
    double SolAirTemperatureF { get; }

    /// <summary>Gets the sol-air temperature on the B side (indoor) [°C].</summary>
    double SolAirTemperatureB { get; }

    /// <summary>Gets the convective heat transfer coefficient on the F side (outdoor) [W/(m²·K)].</summary>
    double ConvectiveCoefficientF { get; }

    /// <summary>Gets the convective heat transfer coefficient on the B side (indoor) [W/(m²·K)].</summary>
    double ConvectiveCoefficientB { get; }

    /// <summary>Gets the radiative heat transfer coefficient on the F side (outdoor) [W/(m²·K)].</summary>
    double RadiativeCoefficientF { get; }

    /// <summary>Gets the radiative heat transfer coefficient on the B side (indoor) [W/(m²·K)].</summary>
    double RadiativeCoefficientB { get; }

    /// <summary>Gets the combined heat transfer coefficient on the F side (outdoor) [W/(m²·K)].</summary>
    double FilmCoefficientF { get; }

    /// <summary>Gets the combined heat transfer coefficient on the B side (indoor) [W/(m²·K)].</summary>
    double FilmCoefficientB { get; }

    /// <summary>Gets the short-wave (solar) emissivity on the F side (outdoor) [-].</summary>
    double ShortWaveEmissivityF { get; }

    /// <summary>Gets the long-wave (thermal) emissivity on the F side (outdoor) [-].</summary>
    double LongWaveEmissivityF { get; }

    /// <summary>Gets the short-wave (solar) emissivity on the B side (indoor) [-].</summary>
    double ShortWaveEmissivityB { get; }

    /// <summary>Gets the long-wave (thermal) emissivity on the B side (indoor) [-].</summary>
    double LongWaveEmissivityB { get; }

    /// <summary>Gets the surface temperature on the F side (outdoor) [°C].</summary>
    double SurfaceTemperatureF { get; }

    /// <summary>Gets the surface temperature on the B side (indoor) [°C].</summary>
    double SurfaceTemperatureB { get; }

    /// <summary>Gets the shading device at the specified layer position.</summary>
    /// <param name="number">Layer index (0 = outdoor side, N+1 = indoor side).</param>
    /// <returns>The shading device at the specified position.</returns>
    IShadingDevice GetShadingDevice(int number);

    /// <summary>Gets the transmittance of the specified glazing layer [-].</summary>
    /// <param name="glazingIndex">Glazing layer index (0, 1, 2, ...).</param>
    /// <param name="isSideF">True for the F (outdoor) side; false for the B (indoor) side.</param>
    /// <returns>Transmittance [-].</returns>
    double GetGlazingTransmittance(int glazingIndex, bool isSideF);

    /// <summary>Gets the reflectance of the specified glazing layer [-].</summary>
    /// <param name="glazingIndex">Glazing layer index (0, 1, 2, ...).</param>
    /// <param name="isSideF">True for the F (outdoor) side; false for the B (indoor) side.</param>
    /// <returns>Reflectance [-].</returns>
    double GetGlazingReflectance(int glazingIndex, bool isSideF);

    /// <summary>Gets the thermal resistance of the specified glazing layer [m²·K/W].</summary>
    /// <param name="glazingIndex">Glazing layer index.</param>
    /// <returns>Thermal resistance [m²·K/W].</returns>
    double GetGlassResistance(int glazingIndex);

    /// <summary>Gets the thermal resistance of the air gap to the right of the specified glazing layer [m²·K/W].</summary>
    /// <param name="glazingIndex">Glazing layer index (the air gap to its indoor side is returned).</param>
    /// <returns>Air gap thermal resistance [m²·K/W].</returns>
    double GetAirGapResistance(int glazingIndex);

  }
}