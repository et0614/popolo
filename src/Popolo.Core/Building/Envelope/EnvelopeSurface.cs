/* EnvelopeSurface.cs
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

namespace Popolo.Core.Building.Envelope
{
  /// <summary>
  /// Represents a single side of an envelope component (wall or window) — its
  /// boundary surface as seen from one zone.
  /// </summary>
  /// <remarks>
  /// <para>
  /// Each <see cref="Wall"/> and each <see cref="Window"/> exposes two
  /// <see cref="EnvelopeSurface"/> instances, one per side. F and B denote
  /// the two opposing sides without implying indoor / outdoor direction;
  /// the user picks which side faces outdoors via
  /// <c>MultiRoom.SetOutsideWall</c> etc.
  /// </para>
  /// <para>
  /// This surface object is the single point of contact for upper-layer code
  /// (<see cref="MultiRoom"/>, <see cref="Zone"/>) that needs to query or
  /// update boundary state without caring whether the underlying envelope
  /// component is a wall or a window. Heat-balance coefficients
  /// (<c>FFS2</c>, <c>BFS2</c>, <c>FFL2</c>, <c>BFL2</c>, <c>IF2</c>, etc.)
  /// are exposed uniformly here.
  /// </para>
  /// <para>
  /// External shading objects (overhangs, fins, neighboring obstructions)
  /// can be attached via <see cref="Shading"/>; F and B sides are
  /// independent so either side can carry an exterior shading.
  /// </para>
  /// </remarks>
  public class EnvelopeSurface
  {

    #region インスタンス変数・プロパティ

    /// <summary>True if this surface is the F side of the element.</summary>
    internal bool isSideF { get; private set; }

    /// <summary>Gets the envelope component (wall or window) this surface belongs to.</summary>
    public OpticalLayeredEnvelope Component { get; private set; } = null!;

    /// <summary>
    /// Gets a value indicating whether this surface belongs to a wall (true)
    /// or a window (false).
    /// </summary>
    /// <remarks>
    /// Convenience predicate; equivalent to <c>Component is Wall</c>. New
    /// code should prefer pattern matching on <see cref="Component"/> for
    /// type-specific access.
    /// </remarks>
    public bool IsWall => Component is Wall;

    /// <summary>
    /// Gets the wall this surface belongs to, or <c>null</c> if the
    /// underlying component is not a wall.
    /// </summary>
    /// <remarks>
    /// Returned as a non-null reference for backward compatibility when
    /// <see cref="IsWall"/> is true; otherwise the value is undefined.
    /// New code should use <c>Component is Wall w</c> pattern matching.
    /// </remarks>
    public Wall Wall => (Component as Wall)!;

    /// <summary>
    /// Gets the window this surface belongs to, or <c>null</c> if the
    /// underlying component is not a window.
    /// </summary>
    /// <remarks>
    /// Returned as a non-null reference for backward compatibility when
    /// <see cref="IsWall"/> is false; otherwise the value is undefined.
    /// New code should use <c>Component is Window w</c> pattern matching.
    /// </remarks>
    public Window Window => (Component as Window)!;

    /// <summary>Gets or sets the surface index within the zone surface list.</summary>
    public int Index { get; set; }

    /// <summary>Gets or sets a value indicating whether this is a ground-contact wall surface.</summary>
    public bool IsGroundWall { get; set; } = false;

    /// <summary>Gets or sets the adjacent space temperature difference factor [-].</summary>
    public double AdjacentSpaceFactor { get; set; } = -1.0;

    /// <summary>
    /// Indicates whether the convective or radiative heat transfer coefficient on this side
    /// has changed since the last <see cref="MultiRoom.MakeABMatrix"/> rebuild. Set by
    /// <see cref="Wall"/> / <see cref="Window"/> coefficient setters; cleared by
    /// <see cref="MultiRoom"/> after consuming the change. Initial value <c>true</c> guarantees
    /// the AB matrix is built on the first call.
    /// </summary>
    internal bool BoundaryCoefficientChanged { get; set; } = true;

    /// <summary>Gets or sets the tilted surface orientation.</summary>
    public IReadOnlyIncline? Incline { get; set; }

    /// <summary>Gets or sets the zone index to which this surface belongs.</summary>
    public int ZoneIndex { get; set; }

    /// <summary>Gets the surface element on the opposite side of this wall or window.</summary>
    public EnvelopeSurface ReverseSideSurface
        => isSideF ? Component.SurfaceB : Component.SurfaceF;

    /// <summary>Gets the surface area [m²].</summary>
    public double Area => Component.Area;

    /// <summary>Gets the combined heat transfer coefficient [W/(m²·K)].</summary>
    public double FilmCoefficient
        => isSideF ? Component.FilmCoefficientF : Component.FilmCoefficientB;

    /// <summary>Gets or sets the radiative heat transfer coefficient [W/(m²·K)].</summary>
    public double RadiativeCoefficient
    {
      get => isSideF ? Component.RadiativeCoefficientF : Component.RadiativeCoefficientB;
      set { if (isSideF) Component.RadiativeCoefficientF = value;
            else         Component.RadiativeCoefficientB = value; }
    }

    /// <summary>Gets or sets the convective heat transfer coefficient [W/(m²·K)].</summary>
    public double ConvectiveCoefficient
    {
      get => isSideF ? Component.ConvectiveCoefficientF : Component.ConvectiveCoefficientB;
      set { if (isSideF) Component.ConvectiveCoefficientF = value;
            else         Component.ConvectiveCoefficientB = value; }
    }

    /// <summary>Gets the moisture transfer coefficient [(kg/s)/((kg/kg)·m²)].</summary>
    public double MoistureCoefficient
        => isSideF ? Component.MoistureCoefficientF : Component.MoistureCoefficientB;

    /// <summary>Gets the short-wave (solar) absorptance [-].</summary>
    public double ShortWaveAbsorptance
        => isSideF ? Component.ShortWaveAbsorptanceF : Component.ShortWaveAbsorptanceB;

    /// <summary>Gets the long-wave (thermal) emissivity [-].</summary>
    public double LongWaveEmissivity
        => isSideF ? Component.LongWaveEmissivityF : Component.LongWaveEmissivityB;

    /// <summary>Gets or sets the sol-air temperature [°C].</summary>
    public double SolAirTemperature
    {
      get => isSideF ? Component.SolAirTemperatureF : Component.SolAirTemperatureB;
      set { if (isSideF) Component.SolAirTemperatureF = value;
            else         Component.SolAirTemperatureB = value; }
    }

    /// <summary>Gets the surface temperature [°C] from the response factor model.</summary>
    /// <remarks>
    /// Computed uniformly for any <see cref="OpticalLayeredEnvelope"/> as
    /// <c>T = IF2 + FFS2·SolAir(this) + BFS2·SolAir(reverse) + FFL2·H(this) + BFL2·H(reverse)</c>.
    /// The <see cref="IF2"/> term carries the layer-internal contribution
    /// (zero for resistive-only configurations such as the default window
    /// model with no thermal mass). Humidity terms (FFL2/BFL2/HumidityRatio)
    /// are 0 for components without coupled moisture transport so the same
    /// formula collapses to the sensible-only form automatically.
    /// </remarks>
    public double SurfaceTemperature
        => IF2 + FFS2 * SolAirTemperature + BFS2 * ReverseSideSurface.SolAirTemperature
              + FFL2 * HumidityRatio + BFL2 * ReverseSideSurface.HumidityRatio;

    /// <summary>Gets or sets the humidity ratio [kg/kg] at this surface.</summary>
    /// <remarks>0 with no-op setter for components without coupled moisture transport.</remarks>
    public double HumidityRatio
    {
      get => isSideF ? Component.HumidityRatioF : Component.HumidityRatioB;
      set { if (isSideF) Component.HumidityRatioF = value;
            else         Component.HumidityRatioB = value; }
    }

    /// <summary>Gets the response factor coefficient for this side's sol-air temperature (sensible).</summary>
    /// <remarks>
    /// Dispatches uniformly through <see cref="OpticalLayeredEnvelope.FFS2_F"/> /
    /// <see cref="OpticalLayeredEnvelope.BFS2_B"/> regardless of component type
    /// — both walls and windows now share the matrix-derived response coefficients.
    /// </remarks>
    public double FFS2
    {
      get
      {
        return isSideF ? Component.FFS2_F : Component.BFS2_B;
      }
    }

    /// <summary>Gets the response factor coefficient for the F-side sol-air temperature (humidity term).</summary>
    /// <remarks>0 for components without coupled moisture transport (Window etc.).</remarks>
    public double FFS3
        => isSideF ? Component.FFS3_F : Component.BFS3_B;

    /// <summary>Gets the response factor coefficient for the F-side humidity ratio (temperature term).</summary>
    public double FFL2
        => isSideF ? Component.FFL2_F : Component.BFL2_B;

    /// <summary>Gets the response factor coefficient for the F-side humidity ratio (humidity term).</summary>
    public double FFL3
        => isSideF ? Component.FFL3_F : Component.BFL3_B;

    /// <summary>Gets the response factor coefficient for the opposite side's sol-air temperature (sensible).</summary>
    /// <remarks>
    /// Dispatches uniformly through <see cref="OpticalLayeredEnvelope.BFS2_F"/> /
    /// <see cref="OpticalLayeredEnvelope.FFS2_B"/> regardless of component type.
    /// </remarks>
    public double BFS2
    {
      get
      {
        return isSideF ? Component.BFS2_F : Component.FFS2_B;
      }
    }

    /// <summary>Gets the response factor coefficient for the B-side sol-air temperature (humidity term).</summary>
    public double BFS3
        => isSideF ? Component.BFS3_F : Component.FFS3_B;

    /// <summary>Gets the response factor coefficient for the B-side humidity ratio (temperature term).</summary>
    public double BFL2
        => isSideF ? Component.BFL2_F : Component.FFL2_B;

    /// <summary>Gets the response factor coefficient for the B-side humidity ratio (humidity term).</summary>
    public double BFL3
        => isSideF ? Component.BFL3_F : Component.FFL3_B;

    /// <summary>Gets the response factor coefficient for the time-delay term (sensible).</summary>
    /// <remarks>
    /// Dispatches uniformly through <see cref="OpticalLayeredEnvelope.IF2_F"/> /
    /// <see cref="OpticalLayeredEnvelope.IF2_B"/> regardless of component type.
    /// </remarks>
    public double IF2
    {
      get
      {
        return isSideF ? Component.IF2_F : Component.IF2_B;
      }
    }

    /// <summary>Gets the response factor coefficient for the time-delay term (humidity row).</summary>
    /// <remarks>0 for components without coupled moisture transport.</remarks>
    public double IF3
        => isSideF ? Component.IF3_F : Component.IF3_B;

    /// <summary>Gets the convective fraction of the combined heat transfer coefficient [-].</summary>
    public double ConvectiveFraction
    { get { return ConvectiveCoefficient / FilmCoefficient; } }

    /// <summary>Gets the radiative fraction of the combined heat transfer coefficient [-].</summary>
    public double RadiativeFraction { get { return 1 - ConvectiveFraction; } }

    /// <summary>
    /// Gets the short-wave (solar) heat flux [W/m²] absorbed at this surface
    /// and treated as a boundary heat input by the upper-layer solver.
    /// </summary>
    /// <remarks>
    /// Set by <see cref="SetIncidentSolarFlux(double)"/> from the solver each
    /// time the per-surface short-wave distribution (<c>radToSurf_S</c>) is
    /// rebuilt. The value depends on how the underlying component handles
    /// short-wave: a wall absorbs the incident flux at its surface, while a
    /// window dissipates it through per-layer absorption inside the assembly
    /// and so contributes 0 here (its incident energy is consumed via
    /// <see cref="Window.DirectSolarIncidentAbsorptance"/> /
    /// <see cref="Window.DiffuseSolarIncidentAbsorptance"/> elsewhere).
    /// </remarks>
    public double AbsorbedSolarFlux { get; private set; }

    /// <summary>
    /// Gets or sets the exterior solar shading object attached to this side
    /// of the envelope component.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the side-level placement of an external obstruction
    /// (overhang, fin, louver, neighboring building, tree, ...) that casts
    /// a shadow onto the surface and reduces incident direct, sky-diffuse,
    /// and ground-diffuse solar radiation.
    /// </para>
    /// <para>
    /// F and B sides hold independent shading; either side may be the
    /// outdoor-facing one depending on the user's wall / window orientation
    /// declaration. <c>null</c> means no exterior shading on this side.
    /// </para>
    /// <para>
    /// For shading <i>inside</i> a window assembly (e.g., Venetian blinds
    /// between glazing layers), use <see cref="IShadingDevice"/> on the
    /// <see cref="Window"/> itself instead.
    /// </para>
    /// </remarks>
    public ISolarShading? Shading { get; set; }

    #endregion

    #region メソッド

    /// <summary>
    /// Notifies this surface of the short-wave radiation [W/m²] arriving from
    /// the solver's per-surface distribution and updates
    /// <see cref="AbsorbedSolarFlux"/> according to the component's behavior.
    /// </summary>
    /// <param name="incidentShortWaveFlux">
    /// The short-wave heat flux [W/m²] reaching this surface (i.e. the
    /// <c>radToSurf_S</c> entry for this surface).
    /// </param>
    /// <remarks>
    /// Walls store the input as-is. Windows ignore it (store 0) because their
    /// incident solar energy is handled per glass layer; the raw distribution
    /// array remains in use only for the window's internal heat-resistance
    /// path, not for the surface-level boundary heat balance.
    /// </remarks>
    public void SetIncidentSolarFlux(double incidentShortWaveFlux)
    {
      AbsorbedSolarFlux = Component.AbsorbsShortWaveAtSurface ? incidentShortWaveFlux : 0.0;
      Component.OnIncidentSolarFlux(this, incidentShortWaveFlux);
    }

    #endregion

    #region コンストラクタ

    /// <summary>Initializes a new envelope surface element.</summary>
    /// <param name="component">The envelope component this surface belongs to.</param>
    /// <param name="isSideF">True if this is the F side; false for the B side.</param>
    internal EnvelopeSurface(OpticalLayeredEnvelope component, bool isSideF)
    {
      Component = component;
      this.isSideF = isSideF;
      Index = -1;
    }

    #endregion

  }
}
