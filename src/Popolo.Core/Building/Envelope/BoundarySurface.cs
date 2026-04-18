/* wallWindowSurface.cs
 * 
 * Copyright (C) 2016 E.Togashi
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
using Popolo.Core.Climate;
using Popolo.Core.Exceptions;

namespace Popolo.Core.Building.Envelope
{
  /// <summary>Represents a single boundary surface element (wall or window side).</summary>
  /// <remarks>
  /// F and B denote the two opposing sides of the element without implying indoor/outdoor direction.
  /// For external walls, F is conventionally the outdoor-facing side.
  /// </remarks>
  internal class BoundarySurface
  {

    #region インスタンス変数・プロパティ

    /// <summary>True if this surface is the F side of the element.</summary>
    internal bool isSideF { get; private set; }

    /// <summary>Gets a value indicating whether this surface belongs to a wall (true) or a window (false).</summary>
    public bool IsWall { get; private set; }

    /// <summary>Gets the wall this surface belongs to.</summary>
    public Wall Wall { get; private set; } = null!;

    /// <summary>Gets the window this surface belongs to.</summary>
    public Window Window { get; private set; } = null!;

    /// <summary>Gets or sets the surface index within the zone surface list.</summary>
    public int Index { get; set; }

    /// <summary>Gets or sets a value indicating whether this is a ground-contact wall surface.</summary>
    public bool IsGroundWall { get; set; } = false;

    /// <summary>Gets or sets the adjacent space temperature difference factor [-].</summary>
    public double AdjacentSpaceFactor { get; set; } = -1.0;

    /// <summary>Gets or sets the tilted surface orientation.</summary>
    public IReadOnlyIncline? Incline { get; set; }

    /// <summary>Gets or sets the zone index to which this surface belongs.</summary>
    public int ZoneIndex { get; set; }

    /// <summary>Gets the surface element on the opposite side of this wall or window.</summary>
    public BoundarySurface ReverseSideSurface
    {
      get
      {
        if (IsWall && isSideF) return Wall.SurfaceB;
        else if (IsWall && !isSideF) return Wall.SurfaceF;
        else if (!IsWall && isSideF) return Window.InsideSurface;
        else return Window.OutsideSurface;
      }
    }

    /// <summary>Gets the surface area [m²].</summary>
    public double Area
    {
      get
      {
        if (IsWall) return Wall.Area;
        else return Window.Area;
      }
    }

    /// <summary>Gets the combined heat transfer coefficient [W/(m²·K)].</summary>
    public double FilmCoefficient
    {
      get
      {
        if (IsWall && isSideF) return Wall.FilmCoefficientF;
        else if (IsWall && !isSideF) return Wall.FilmCoefficientB;
        else if (!IsWall && isSideF) return Window.FilmCoefficientF;
        else return Window.FilmCoefficientB;
      }
    }

    /// <summary>Gets or sets the radiative heat transfer coefficient [W/(m²·K)].</summary>
    public double RadiativeCoefficient
    {
      get
      {
        if (IsWall && isSideF) return Wall.RadiativeCoefficientF;
        else if (IsWall && !isSideF) return Wall.RadiativeCoefficientB;
        else if (!IsWall && isSideF) return Window.RadiativeCoefficientF;
        else return Window.RadiativeCoefficientB;
      }
      set
      {
        if (IsWall && isSideF) Wall.RadiativeCoefficientF = value;
        else if (IsWall && !isSideF) Wall.RadiativeCoefficientB = value;
        else if (!IsWall && isSideF) Window.RadiativeCoefficientF = value;
        else Window.RadiativeCoefficientB = value;
      }
    }

    /// <summary>Gets or sets the convective heat transfer coefficient [W/(m²·K)].</summary>
    public double ConvectiveCoefficient
    {
      get
      {
        if (IsWall && isSideF) return Wall.ConvectiveCoefficientF;
        else if (IsWall && !isSideF) return Wall.ConvectiveCoefficientB;
        else if (!IsWall && isSideF) return Window.ConvectiveCoefficientF;
        else return Window.ConvectiveCoefficientB;
      }
      set
      {
        if (IsWall && isSideF) Wall.ConvectiveCoefficientF = value;
        else if (IsWall && !isSideF) Wall.ConvectiveCoefficientB = value;
        else if (!IsWall && isSideF) Window.ConvectiveCoefficientF = value;
        else Window.ConvectiveCoefficientB = value;
      }
    }

    /// <summary>Gets the moisture transfer coefficient [(kg/s)/((kg/kg)·m²)].</summary>
    public double MoistureCoefficient
    {
      get
      {
        if (IsWall && isSideF) return Wall.MoistureCoefficientF;
        else if (IsWall && !isSideF) return Wall.MoistureCoefficientB;
        else return 0;
      }
    }

    /// <summary>Gets the short-wave (solar) absorptance [-].</summary>
    public double ShortWaveEmissivity
    {
      get
      {
        if (IsWall && isSideF) return Wall.ShortWaveAbsorptanceF;
        else if (IsWall && !isSideF) return Wall.ShortWaveAbsorptanceB;
        else if (!IsWall && isSideF) return Window.ShortWaveEmissivityF;
        else return Window.ShortWaveEmissivityB;
      }
    }

    /// <summary>Gets the long-wave (thermal) emissivity [-].</summary>
    public double LongWaveEmissivity {
      get
      {
        if (IsWall && isSideF) return Wall.LongWaveEmissivityF;
        else if (IsWall && !isSideF) return Wall.LongWaveEmissivityB;
        else if (!IsWall && isSideF) return Window.LongWaveEmissivityF;
        else return Window.LongWaveEmissivityB;
      }
    }

    /// <summary>Gets or sets the sol-air temperature [°C].</summary>
    public double SolAirTemperature
    {
      set
      {
        if (IsWall && isSideF) Wall.SolAirTemperatureF = value;
        else if (IsWall && !isSideF) Wall.SolAirTemperatureB = value;
        else if (!IsWall && isSideF) Window.SolAirTemperatureF = value;
        else Window.SolAirTemperatureB = value;
      }
      get
      {
        if (IsWall && isSideF) return Wall.SolAirTemperatureF;
        else if (IsWall && !isSideF) return Wall.SolAirTemperatureB;
        else if (!IsWall && isSideF) return Window.SolAirTemperatureF;
        else return Window.SolAirTemperatureB;
      }
    }

    /// <summary>Gets the surface temperature [°C] from the response factor model.</summary>
    public double SurfaceTemperature
    {
      get
      {
        if (IsWall) return IF2 + FFS2 * SolAirTemperature + BFS2 * ReverseSideSurface.SolAirTemperature
            + FFL2 * HumidityRatio + BFL2 * ReverseSideSurface.HumidityRatio;
        else return FFS2 * SolAirTemperature + BFS2 * ReverseSideSurface.SolAirTemperature;
      }
    }

    /// <summary>Gets or sets the humidity ratio [kg/kg] at this surface.</summary>
    public double HumidityRatio
    {
      set
      {
        if (IsWall && isSideF) Wall.HumidityRatioF = value;
        else if (IsWall && !isSideF) Wall.HumidityRatioB = value;
        else return;
      }
      get
      {
        if (IsWall && isSideF) return Wall.HumidityRatioF;
        else if (IsWall && !isSideF) return Wall.HumidityRatioB;
        else return 0;
      }
    }

    /// <summary>Gets the response factor coefficient for the F-side sol-air temperature (temperature term).</summary>
    public double FFS2
    {
      get
      {
        if (IsWall && isSideF) return Wall.FFS2_F;
        else if (IsWall && !isSideF) return Wall.BFS2_B;
        else if (!IsWall && isSideF) throw new PopoloNotImplementedException(
          "Response factor coefficients for the front side of a window surface are not supported.");
        else return 1 - 1d / (Window.GetResistance() * Window.FilmCoefficientB);
      }
    }

    /// <summary>Gets the response factor coefficient for the F-side sol-air temperature (humidity term).</summary>
    public double FFS3
    {
      get
      {
        if (IsWall && isSideF) return Wall.FFS3_F;
        else if (IsWall && !isSideF) return Wall.BFS3_B;
        else if (!IsWall && isSideF) throw new PopoloNotImplementedException(
          "Response factor coefficients for the front side of a window surface are not supported.");
        else return 0;
      }
    }

    /// <summary>Gets the response factor coefficient for the F-side humidity ratio (temperature term).</summary>
    public double FFL2
    {
      get
      {
        if (IsWall && isSideF) return Wall.FFL2_F;
        else if (IsWall && !isSideF) return Wall.BFL2_B;
        else if (!IsWall && isSideF) throw new PopoloNotImplementedException(
          "Response factor coefficients for the front side of a window surface are not supported.");
        else return 0;
      }
    }

    /// <summary>Gets the response factor coefficient for the F-side humidity ratio (humidity term).</summary>
    public double FFL3
    {
      get
      {
        if (IsWall && isSideF) return Wall.FFL3_F;
        else if (IsWall && !isSideF) return Wall.BFL3_B;
        else if (!IsWall && isSideF) throw new PopoloNotImplementedException(
          "Response factor coefficients for the front side of a window surface are not supported.");
        else return 0;
      }
    }

    /// <summary>Gets the response factor coefficient for the B-side sol-air temperature (temperature term).</summary>
    public double BFS2
    {
      get
      {
        if (IsWall && isSideF) return Wall.BFS2_F;
        else if (IsWall && !isSideF) return Wall.FFS2_B;
        else if (!IsWall && isSideF) throw new PopoloNotImplementedException(
          "Response factor coefficients for the front side of a window surface are not supported.");
        else return 1d / (Window.GetResistance() * Window.FilmCoefficientB);
      }
    }

    /// <summary>Gets the response factor coefficient for the B-side sol-air temperature (humidity term).</summary>
    public double BFS3
    {
      get
      {
        if (IsWall && isSideF) return Wall.BFS3_F;
        else if (IsWall && !isSideF) return Wall.FFS3_B;
        else if (!IsWall && isSideF) throw new PopoloNotImplementedException(
          "Response factor coefficients for the front side of a window surface are not supported.");
        else return 0;
      }
    }

    /// <summary>Gets the response factor coefficient for the B-side humidity ratio (temperature term).</summary>
    public double BFL2
    {
      get
      {
        if (IsWall && isSideF) return Wall.BFL2_F;
        else if (IsWall && !isSideF) return Wall.FFL2_B;
        else if (!IsWall && isSideF) throw new PopoloNotImplementedException(
          "Response factor coefficients for the front side of a window surface are not supported.");
        else return 0;
      }
    }

    /// <summary>Gets the response factor coefficient for the B-side humidity ratio (humidity term).</summary>
    public double BFL3
    {
      get
      {
        if (IsWall && isSideF) return Wall.BFL3_F;
        else if (IsWall && !isSideF) return Wall.FFL3_B;
        else if (!IsWall && isSideF) throw new PopoloNotImplementedException(
          "Response factor coefficients for the front side of a window surface are not supported.");
        else return 0;
      }
    }

    /// <summary>Gets the response factor coefficient for the time-delay term (temperature).</summary>
    public double IF2
    {
      get
      {
        if (IsWall && isSideF) return Wall.IF2_F;
        else if (IsWall && !isSideF) return Wall.IF2_B;
        else if (!IsWall && isSideF) return 0;
        else return 0;
      }
    }

    /// <summary>Gets the response factor coefficient for the time-delay term (temperature).</summary>
    public double IF3
    {
      get
      {
        if (IsWall && isSideF) return Wall.IF3_F;
        else if (IsWall && !isSideF) return Wall.IF3_B;
        else if (!IsWall && isSideF) return 0;
        else return 0;
      }
    }

    /// <summary>Gets the convective fraction of the combined heat transfer coefficient [-].</summary>
    public double ConvectiveFraction
    { get { return ConvectiveCoefficient / FilmCoefficient; } }

    /// <summary>Gets the radiative fraction of the combined heat transfer coefficient [-].</summary>
    public double RadiativeFraction { get { return 1 - ConvectiveFraction; } }

    /// <summary>Gets or sets the direct solar irradiance on this surface [W/m²].</summary>
    public double DirectSolarIrradiance { get; set; }

    /// <summary>Gets or sets the diffuse solar irradiance on this surface [W/m²].</summary>
    public double DiffuseSolarIrradiance { get; set; }

    #endregion

    #region コンストラクタ

    /// <summary>Initializes a new wall surface element.</summary>
    /// <param name="wall">The wall this surface belongs to.</param>
    /// <param name="isSideF">True if this is the F side; false for the B side.</param>
    public BoundarySurface(Wall wall, bool isSideF)
    {
      IsWall = true;
      Wall = wall;
      this.isSideF = isSideF;
      Index = -1;
    }

    /// <summary>Initializes a new window surface element.</summary>
    /// <param name="window">The window this surface belongs to.</param>
    /// <param name="isSideF">True if this is the F (outdoor) side; false for the B (indoor) side.</param>
    public BoundarySurface(Window window, bool isSideF)
    {
      IsWall = false;
      Window = window;
      this.isSideF = isSideF;
      Index = -1;
    }

    #endregion

  }
}
