/* Sky.cs
 *
 * Copyright (C) 2008 E.Togashi
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

using System;
using Popolo.Core.Physics;

namespace Popolo.Core.Climate
{
  /// <summary>
  /// Provides static methods for long-wave sky radiation exchange and related
  /// atmospheric estimates used by the building thermal model.
  /// </summary>
  /// <remarks>
  /// <para>
  /// In an outdoor heat balance, every surface facing the sky loses heat to
  /// the cold upper atmosphere (nocturnal radiation) and gains heat from its
  /// own downwelling infrared emission (atmospheric radiation). This class
  /// computes both sides of that exchange from three common weather inputs:
  /// the outdoor dry-bulb temperature, the cloud cover on a 0–10 scale, and
  /// the water-vapor partial pressure of the ambient air.
  /// </para>
  /// <para>
  /// The sky emissivity under clear-sky conditions is approximated by
  /// <see cref="GetSkyEmissivity"/> as an increasing function of water-vapor
  /// partial pressure; cloud cover then shifts the sky toward a near-black-body
  /// emitter. Results feed
  /// <see cref="Building.IReadOnlyBuildingThermalModel.NocturnalRadiation"/>
  /// and, ultimately, the sol-air temperature on each exterior surface.
  /// </para>
  /// <para>
  /// <see cref="GetPrecipitableWater"/> provides a separate utility used
  /// primarily for atmospheric transmissivity calculations (see
  /// <see cref="Sun"/>).
  /// </para>
  /// <para>
  /// References:
  /// <list type="bullet">
  ///   <item><description>Shukuya, M., "Light and Heat in the Architectural Environment — Numerical Approaches," Maruzen, 1993, p. 20.</description></item>
  ///   <item><description>Udagawa, M., "Air Conditioning Calculations with Personal Computers," 1986.</description></item>
  /// </list>
  /// </para>
  /// </remarks>
  public static class Sky
  {

    #region 放射関連

    /// <summary>
    /// Gets the nocturnal (outgoing longwave) radiation [W/m²].
    /// </summary>
    /// <param name="temperature">Outdoor dry-bulb temperature [°C]</param>
    /// <param name="cloudCover">Cloud cover [-] (0: clear, 10: overcast)</param>
    /// <param name="waterVaporPartialPressure">Water vapor partial pressure [kPa]</param>
    /// <returns>Nocturnal radiation [W/m²]</returns>
    public static double GetNocturnalRadiation(
        double temperature, int cloudCover, double waterVaporPartialPressure)
    {
      double br = GetSkyEmissivity(waterVaporPartialPressure);
      return (1.0 - 0.062 * cloudCover) * (1.0 - br)
          * BlackBodyRadiation(temperature);
    }

    /// <summary>
    /// Gets the atmospheric (downwelling longwave) radiation from the sky [W/m²].
    /// </summary>
    /// <param name="temperature">Outdoor dry-bulb temperature [°C]</param>
    /// <param name="cloudCover">Cloud cover [-] (0: clear, 10: overcast)</param>
    /// <param name="waterVaporPartialPressure">Water vapor partial pressure [kPa]</param>
    /// <returns>Atmospheric infrared radiation [W/m²]</returns>
    public static double GetInfraredRadiationFromSky(
        double temperature, int cloudCover, double waterVaporPartialPressure)
    {
      double br = GetSkyEmissivity(waterVaporPartialPressure);
      return ((1.0 - 0.062 * cloudCover) * br + 0.062 * cloudCover)
          * BlackBodyRadiation(temperature);
    }

    /// <summary>
    /// Gets the cloud cover [-] from the atmospheric radiation, temperature,
    /// and water vapor partial pressure.
    /// </summary>
    /// <param name="infraredRadFromSky">Atmospheric infrared radiation [W/m²]</param>
    /// <param name="temperature">Outdoor dry-bulb temperature [°C]</param>
    /// <param name="waterVaporPartialPressure">Water vapor partial pressure [kPa]</param>
    /// <returns>Cloud cover [-] (0 to 10, integer)</returns>
    public static int GetCloudCover(
        double infraredRadFromSky, double temperature, double waterVaporPartialPressure)
    {
      double br = GetSkyEmissivity(waterVaporPartialPressure);
      double bf = BlackBodyRadiation(temperature);
      double cc = (br * bf - infraredRadFromSky) / ((br - 1.0) * bf) / 0.062;
      return (int)Math.Max(0, Math.Min(10, cc));
    }

    /// <summary>
    /// Gets the sky emissivity [-] from the water vapor partial pressure [kPa].
    /// </summary>
    /// <param name="waterVaporPartialPressure">Water vapor partial pressure [kPa]</param>
    /// <returns>Sky emissivity [-]</returns>
    public static double GetSkyEmissivity(double waterVaporPartialPressure)
        => 0.526 + 0.209 * Math.Sqrt(waterVaporPartialPressure);

    /// <summary>Computes the black-body radiation σT⁴ [W/m²].</summary>
    private static double BlackBodyRadiation(double temperature)
        => PhysicsConstants.StefanBoltzmannConstant
           * Math.Pow(PhysicsConstants.ToKelvin(temperature), 4);

    #endregion

    #region 降水量関連

    /// <summary>
    /// Estimates the precipitable water [mm] from the elevation and dew point temperature.
    /// </summary>
    /// <param name="elevation">Elevation above sea level [m]</param>
    /// <param name="dewpointTemperature">Dew point temperature [°C]</param>
    /// <returns>Precipitable water [mm]</returns>
    /// <remarks>
    /// Kondo, J.: An empirical formula to estimate precipitable water from surface dew point
    /// temperature, Journal of Japan Society of Hydrology and Water Resources,
    /// Vol.9, No.5, 1996.
    /// </remarks>
    public static double GetPrecipitableWater(double elevation, double dewpointTemperature)
    {
      double atm = MoistAir.GetAtmosphericPressure(elevation);
      double x0 = 1.0 - Math.Sqrt(atm / PhysicsConstants.StandardAtmosphericPressure);
      double x;
      if (dewpointTemperature < -5)
        x = 0.027 * dewpointTemperature - 0.150 - x0;
      else if (dewpointTemperature < 23)
        x = 0.031 * dewpointTemperature - 0.130 - x0;
      else
        x = 0.015 * dewpointTemperature - 0.238 - x0;
      return 10 * Math.Pow(10, x) / 0.8;
    }

    #endregion

  }
}