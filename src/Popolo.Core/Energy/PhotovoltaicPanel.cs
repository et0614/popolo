/* PhotovoltaicPanel.cs
 *
 * Copyright (C) 2016 E.Togashi
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
using Popolo.Core.Climate;

namespace Popolo.Core.Energy
{
  /// <summary>
  /// Represents a photovoltaic (PV) panel and provides power output calculations.
  /// </summary>
  /// <remarks>
  /// References:
  /// - JIS C 8960: Vocabulary of photovoltaic solar energy systems.
  /// - JIS C 8907: Estimation method for power generation of photovoltaic systems.
  /// - Yukawa, M. et al.: "Estimation of temperature rise in photovoltaic modules,"
  ///   IEEJ Transactions on Power and Energy, Vol.116, No.9, 1996.
  /// </remarks>
  public class PhotovoltaicPanel : IReadOnlyPhotovoltaicPanel
  {

    #region 列挙型

    /// <summary>
    /// Specifies the cell material type.
    /// </summary>
    public enum MaterialType
    {
      /// <summary>Amorphous silicon.</summary>
      Amorphous,
      /// <summary>Crystalline silicon (mono- or poly-crystalline).</summary>
      Crystal
    }

    /// <summary>
    /// Specifies the panel mounting type.
    /// </summary>
    public enum MountType
    {
      /// <summary>Roof-mount (stand-off) type.</summary>
      RoofMount,
      /// <summary>Roof-integrated (building-integrated) type.</summary>
      RoofIntegrated,
      /// <summary>Ground-mount (rack) type.</summary>
      GroundMount
    }

    #endregion

    #region プロパティ

    /// <summary>Backing field for the inverter efficiency.</summary>
    private double _inverterEfficiency = 0.9;

    /// <summary>Gets or sets the inverter efficiency [-] (clamped to [0, 1]).</summary>
    public double InverterEfficiency
    {
      get => _inverterEfficiency;
      set => _inverterEfficiency = Math.Max(0, Math.Min(1, value));
    }

    /// <summary>Gets the peak power output [W] under STC (1000 W/m², 25 °C).</summary>
    public double PeakPower { get; private set; }

    /// <summary>Gets the mounting type.</summary>
    public MountType Mount { get; private set; }

    /// <summary>Gets the cell material type.</summary>
    public MaterialType Material { get; private set; }

    /// <summary>Gets the tilted surface on which the panel is installed.</summary>
    public IReadOnlyIncline Incline { get; private set; }

    #endregion

    #region コンストラクタ

    /// <summary>
    /// Initializes a new instance with the specified orientation and tilt angle.
    /// </summary>
    /// <param name="peakPower">Peak power output [W] under STC.</param>
    /// <param name="mount">Mounting type.</param>
    /// <param name="material">Cell material type.</param>
    /// <param name="orientation">Surface orientation (16-point compass direction).</param>
    /// <param name="tiltAngle">
    /// Tilt angle [degree] from horizontal (0° = horizontal, 90° = vertical).
    /// </param>
    public PhotovoltaicPanel(
        double peakPower, MountType mount, MaterialType material,
        Incline.Orientation orientation, double tiltAngle)
        : this(peakPower, mount, material,
            new Incline(orientation, tiltAngle * Math.PI / 180.0))
    { }

    /// <summary>
    /// Initializes a new instance with the specified tilted surface.
    /// </summary>
    /// <param name="peakPower">Peak power output [W] under STC (1000 W/m², 25 °C).</param>
    /// <param name="mount">Mounting type.</param>
    /// <param name="material">Cell material type.</param>
    /// <param name="incline">Tilted surface on which the panel is installed.</param>
    public PhotovoltaicPanel(
        double peakPower, MountType mount, MaterialType material, Incline incline)
    {
      PeakPower = peakPower;
      Mount = mount;
      Material = material;
      Incline = incline;
    }

    #endregion

    #region インスタンスメソッド

    /// <summary>
    /// Gets the power output [W] from the ambient conditions and tilted surface irradiance.
    /// </summary>
    /// <param name="dryBulbTemperature">Outdoor dry-bulb temperature [°C]</param>
    /// <param name="velocity">Wind speed [m/s]</param>
    /// <param name="totalIrradiance">
    /// Total irradiance on the tilted surface [W/m²] (direct + diffuse).
    /// </param>
    /// <returns>Power output [W]</returns>
    public double GetPower(
        double dryBulbTemperature, double velocity, double totalIrradiance)
    {
      return GetPower(dryBulbTemperature, velocity, totalIrradiance,
          PeakPower, InverterEfficiency, Mount, Material);
    }

    /// <summary>
    /// Gets the power output [W] from the ambient conditions and solar state.
    /// </summary>
    /// <param name="dryBulbTemperature">Outdoor dry-bulb temperature [°C]</param>
    /// <param name="velocity">Wind speed [m/s]</param>
    /// <param name="sun">Solar state</param>
    /// <returns>Power output [W]</returns>
    public double GetPower(
        double dryBulbTemperature, double velocity, IReadOnlySun sun)
    {
      double totalIrradiance =
          Incline.GetDirectSolarRadiationRatio(sun) * sun.DirectNormalRadiation
          + Incline.ConfigurationFactorToSky * sun.DiffuseHorizontalRadiation;
      return GetPower(dryBulbTemperature, velocity, totalIrradiance);
    }

    #endregion

    #region 静的メソッド

    /// <summary>
    /// Gets the power output [W] from the specified conditions and panel parameters.
    /// </summary>
    /// <param name="dryBulbTemperature">Outdoor dry-bulb temperature [°C]</param>
    /// <param name="velocity">Wind speed [m/s]</param>
    /// <param name="totalIrradiance">Total irradiance on the tilted surface [W/m²]</param>
    /// <param name="peakPower">Peak power output [W] under STC</param>
    /// <param name="inverterEfficiency">Inverter efficiency [-]</param>
    /// <param name="mount">Mounting type</param>
    /// <param name="material">Cell material type</param>
    /// <returns>Power output [W]</returns>
    public static double GetPower(
        double dryBulbTemperature, double velocity, double totalIrradiance,
        double peakPower, double inverterEfficiency,
        MountType mount, MaterialType material)
    {
      double kpt = GetTemperatureRiseCorrectionFactor(
          dryBulbTemperature, velocity, totalIrradiance, mount, material);
      return totalIrradiance / 1000.0 * kpt * peakPower * inverterEfficiency;
    }

    /// <summary>Computes the output correction factor due to the rise in panel temperature (Yukawa et al., 1996).</summary>
    private static double GetTemperatureRiseCorrectionFactor(
        double dryBulbTemperature, double velocity, double totalIrradiance,
        MountType mount, MaterialType material)
    {
      double a, b;
      switch (mount)
      {
        case MountType.GroundMount:
          a = 46; b = 0.41;
          break;
        case MountType.RoofIntegrated:
          a = 50; b = 0.33;
          break;
        default: //RoofMount
          a = 50; b = 0.38;
          break;
      }
      //パネル温度を計算する（湯川ら 式1）
      double tp = dryBulbTemperature
          + (a / (b * Math.Pow(velocity, 0.8) + 1) + 2) * totalIrradiance / 1000.0
          - 2.0;

      //温度係数: アモルファス -0.2%/°C、結晶 -0.4%/°C（基準温度25°C）
      return material == MaterialType.Amorphous
          ? 1 - 0.002 * (tp - 25.0)
          : 1 - 0.004 * (tp - 25.0);
    }

    #endregion

  }

  /// <summary>
  /// Represents a read-only view of a photovoltaic panel.
  /// </summary>
  public interface IReadOnlyPhotovoltaicPanel
  {
    /// <summary>Gets the inverter efficiency [-].</summary>
    double InverterEfficiency { get; }

    /// <summary>Gets the peak power output [W] under STC (1000 W/m², 25 °C).</summary>
    double PeakPower { get; }

    /// <summary>Gets the mounting type.</summary>
    PhotovoltaicPanel.MountType Mount { get; }

    /// <summary>Gets the cell material type.</summary>
    PhotovoltaicPanel.MaterialType Material { get; }

    /// <summary>Gets the tilted surface on which the panel is installed.</summary>
    IReadOnlyIncline Incline { get; }
  }

}
