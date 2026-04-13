/* SimpleShadingDevice.cs
 * 
 * Copyright (C) 2020 E.Togashi
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

using System;

namespace Popolo.Core.Building.Envelope
{
  /// <summary>
  /// A simple solar shading device whose optical properties are defined
  /// only at normal incidence.
  /// </summary>
  /// <remarks>ASHRAE Handbook of Fundamentals, 1985, Chapter 27.</remarks>
  public class SimpleShadingDevice : IShadingDevice
  {

    #region 列挙型定義

    /// <summary>Predefined shading device types with standard optical properties.</summary>
    public enum PredefinedDevices
    {
      /// <summary>Bright (light-colored) venetian blind.</summary>
      BrightVenetianBlind,
      /// <summary>Gray (medium-colored) venetian blind.</summary>
      GrayVenetianBlind,
      /// <summary>Translucent roller shade.</summary>
      TranslucentRollerShade,
      /// <summary>Opaque roller shade.</summary>
      OpaqueRollerShade,
      /// <summary>Bright thin curtain.</summary>
      BrightThinCurtain,
      /// <summary>Bright thick curtain.</summary>
      BrightThickCurtain,
      /// <summary>Dark thick curtain.</summary>
      DarkThickCurtain,
    }

    #endregion

    #region コンストラクタ

    /// <summary>Initializes a new instance with specified normal-incidence optical properties.</summary>
    /// <param name="transmittance">Normal-incidence transmittance [-].</param>
    /// <param name="reflectance">Normal-incidence reflectance [-].</param>
    public SimpleShadingDevice
      (double transmittance, double reflectance)
    {
      Transmittance = transmittance;
      Reflectance = reflectance;
    }

    /// <summary>Initializes a new instance using a predefined device type.</summary>
    /// <param name="device">Predefined shading device type.</param>
    public SimpleShadingDevice
      (PredefinedDevices device)
    {
      switch (device)
      {
        case PredefinedDevices.BrightVenetianBlind:
          Transmittance = 0.05; Reflectance = 0.55;
          break;
        case PredefinedDevices.GrayVenetianBlind:
          Transmittance = 0.05; Reflectance = 0.35;
          break;
        case PredefinedDevices.TranslucentRollerShade:
          Transmittance = 0.25; Reflectance = 0.60;
          break;
        case PredefinedDevices.OpaqueRollerShade:
          Transmittance = 0.00; Reflectance = 0.80;
          break;
        case PredefinedDevices.BrightThinCurtain:
          Transmittance = 0.60; Reflectance = 0.35;
          break;
        case PredefinedDevices.BrightThickCurtain:
          Transmittance = 0.10; Reflectance = 0.60;
          break;
        case PredefinedDevices.DarkThickCurtain:
          Transmittance = 0.00; Reflectance = 0.10;
          break;
        default:
          throw new Popolo.Core.Exceptions.PopoloArgumentException("device", "Shading device type is not defined.");
      }
    }

    #endregion

    #region プロパティ

    /// <summary>Gets the normal-incidence transmittance [-].</summary>
    public double Transmittance { get; private set; }

    /// <summary>Gets the normal-incidence reflectance [-].</summary>
    public double Reflectance { get; private set; }

    /// <summary>Gets the normal-incidence absorptance [-].</summary>
    public double Absorptance { get { return 1 - Transmittance - Reflectance; } }

    #endregion

    #region IShadingDevices実装

    /// <summary>Backing field for the deployed state.</summary>
    private bool pullDowned = true;

    /// <summary>Gets or sets a value indicating whether the shading device is deployed.</summary>
    public bool Pulldowned
    {
      get { return pullDowned; }
      set
      {
        HasPropertyChanged |= (pullDowned != value);
        pullDowned = value;
      }
    }

    /// <summary>Gets a value indicating whether optical properties have changed.</summary>
    public bool HasPropertyChanged { get; private set; } = true;

    /// <summary>Gets or sets the profile angle (apparent solar altitude) [radian].</summary>
    public double ProfileAngle { get; set; }

    /// <summary>Computes the optical properties of the shading device.</summary>
    /// <param name="isDiffuseIrradianceProperties">True for diffuse irradiance; false for direct.</param>
    /// <param name="irradianceFromSideF">True if irradiance is from the F side.</param>
    /// <param name="transmittance">Transmittance [-].</param>
    /// <param name="reflectance">Reflectance [-].</param>
    public void ComputeOpticalProperties
      (bool isDiffuseIrradianceProperties, bool irradianceFromSideF,
      out double transmittance, out double reflectance)
    {
      if (Pulldowned)
      {
        transmittance = Transmittance;
        reflectance = Reflectance;
      }
      else
      {
        transmittance = 1.0;
        reflectance = 0.0;
      }
      HasPropertyChanged = false;
    }

    #endregion

  }
}
