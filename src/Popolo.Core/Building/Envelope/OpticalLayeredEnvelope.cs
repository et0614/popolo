/* OpticalLayeredEnvelope.cs
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

using System;
using Popolo.Core.Climate;
using Popolo.Core.Numerics.LinearAlgebra;

namespace Popolo.Core.Building.Envelope
{
  /// <summary>
  /// Abstract building envelope component composed of a stack of layers, each
  /// of which can absorb, reflect, or transmit short-wave (solar) radiation.
  /// </summary>
  /// <remarks>
  /// <para>
  /// Concrete implementations include <see cref="Wall"/> (multi-layer
  /// transient conduction with optional embedded radiant pipes, PCM, and
  /// coupled moisture transport — opaque by default with transmittance 0)
  /// and <see cref="Window"/> (multi-pane glazing with internal shading
  /// devices and external sun-shading geometry — translucent with non-zero
  /// per-layer transmittance and absorptance). Both share the same notion
  /// of two opposing boundary surfaces (F and B) carrying sol-air
  /// temperatures, long-wave emissivities, convective and radiative film
  /// coefficients, and a layered short-wave optical model.
  /// </para>
  /// <para>
  /// The "Optical" qualifier signals that the layer stack is designed to
  /// handle solar transmission and absorption at every layer interface —
  /// opaque components such as a typical wall are simply the degenerate case
  /// where transmittance is zero and absorption happens at the outdoor face.
  /// Future translucent walls fit naturally as a third concrete subclass
  /// without disrupting the contract.
  /// </para>
  /// <para>
  /// F and B are positional labels only — neither is intrinsically "indoor"
  /// or "outdoor"; the user assigns orientation by registering one side via
  /// <c>MultiRoom.SetOutsideWall</c> / <c>SetGroundWall</c> and the other to
  /// a zone via <c>MultiRoom.AddWall(zoneIndex, wallIndex, isSideF)</c>.
  /// </para>
  /// <para>
  /// This base owns the implicit-Euler matrix infrastructure shared by all
  /// layered components: nodal state vector, capacity / resistance arrays,
  /// per-node solar-absorption inputs, and a sensible-only matrix solver.
  /// Subclasses implement <see cref="PopulateSensibleProperties"/> to fill
  /// <see cref="capS"/> / <see cref="resS"/> from their own layer
  /// representation, and may override <see cref="UpdateUMatrix"/>,
  /// <see cref="UpdateInverseMatrix"/>, <see cref="UpdateIFCoefficients"/>,
  /// <see cref="Update"/>, and <see cref="Initialize(double)"/> to add
  /// component-specific behavior (Wall: coupled moisture transport, embedded
  /// pipes, PCM layers; Window: glass-stack initialization).
  /// </para>
  /// </remarks>
  public abstract class OpticalLayeredEnvelope : IReadOnlyOpticalLayeredEnvelope
  {

    #region 共通 F/B 側状態

    /// <summary>Gets or sets the sol-air temperature on the F side [°C].</summary>
    public double SolAirTemperatureF { get; set; }

    /// <summary>Gets or sets the sol-air temperature on the B side [°C].</summary>
    public double SolAirTemperatureB { get; set; }

    /// <summary>Gets or sets the long-wave (thermal) emissivity on the F side [-].</summary>
    public double LongWaveEmissivityF { get; set; } = 0.9;

    /// <summary>Gets or sets the long-wave (thermal) emissivity on the B side [-].</summary>
    public double LongWaveEmissivityB { get; set; } = 0.9;

    #endregion

    #region 幾何 / 接続

    /// <summary>Gets the surface area [m²] of this envelope component.</summary>
    /// <remarks>
    /// Both get and set are abstract because <see cref="Wall"/> exposes a
    /// trivial auto-property setter while <see cref="Window"/> validates the
    /// value (rejects non-positive areas).
    /// </remarks>
    public abstract double Area { get; set; }

    /// <summary>Gets the boundary surface element on the F side.</summary>
    /// <remarks>Set by the subclass constructor when the surface objects are created.</remarks>
    public EnvelopeSurface SurfaceF { get; protected set; } = null!;

    /// <summary>Gets the boundary surface element on the B side.</summary>
    /// <remarks>Set by the subclass constructor when the surface objects are created.</remarks>
    public EnvelopeSurface SurfaceB { get; protected set; } = null!;

    #endregion

    #region F/B 側 表面熱伝達係数 (基底でユーザー値バックアップ + subclass 実装の振分)

    // ユーザー設定値の控え。setter (公開プロパティ) 経由の代入は値を _userBackup 群
    // にも書き、SetXxxInternal (内部経由、動的更新用) は _userBackup を変更しない。
    // RestoreUserCoefficients() で _userBackup の値を SetXxxCore 経由で書き戻すと、
    // ユーザーが直接設定した値がいつでも復元される。
    private double _convF_userBackup, _convB_userBackup;
    private double _radF_userBackup,  _radB_userBackup;

    /// <inheritdoc/>
    /// <remarks>
    /// 値を設定すると同時にユーザー設定値として記憶する。
    /// 後段の動的更新で内部的に上書きされた場合でも
    /// <see cref="RestoreUserCoefficients"/> でこの値に戻る。
    /// </remarks>
    public double ConvectiveCoefficientF
    {
      get => GetConvectiveCoefficientFCore();
      set { _convF_userBackup = value; SetConvectiveCoefficientFCore(value); }
    }

    /// <inheritdoc/>
    public double ConvectiveCoefficientB
    {
      get => GetConvectiveCoefficientBCore();
      set { _convB_userBackup = value; SetConvectiveCoefficientBCore(value); }
    }

    /// <inheritdoc/>
    public double RadiativeCoefficientF
    {
      get => GetRadiativeCoefficientFCore();
      set { _radF_userBackup = value; SetRadiativeCoefficientFCore(value); }
    }

    /// <inheritdoc/>
    public double RadiativeCoefficientB
    {
      get => GetRadiativeCoefficientBCore();
      set { _radB_userBackup = value; SetRadiativeCoefficientBCore(value); }
    }

    /// <summary>Subclass storage accessor for the F-side convective coefficient.</summary>
    /// <remarks>
    /// <see cref="Wall"/> / <see cref="Window"/> override these protected
    /// pairs to provide their own per-side storage and side effects
    /// (e.g., <c>needToUpdateUMatrix</c>, <c>UpdateFilmCoefficient</c>,
    /// <c>BoundaryCoefficientChanged</c>). The base class wraps them with
    /// the user-backup logic so the subclass remains free of that concern.
    /// </remarks>
    protected abstract double GetConvectiveCoefficientFCore();
    /// <inheritdoc cref="GetConvectiveCoefficientFCore"/>
    protected abstract void   SetConvectiveCoefficientFCore(double value);
    /// <inheritdoc cref="GetConvectiveCoefficientFCore"/>
    protected abstract double GetConvectiveCoefficientBCore();
    /// <inheritdoc cref="GetConvectiveCoefficientFCore"/>
    protected abstract void   SetConvectiveCoefficientBCore(double value);
    /// <inheritdoc cref="GetConvectiveCoefficientFCore"/>
    protected abstract double GetRadiativeCoefficientFCore();
    /// <inheritdoc cref="GetConvectiveCoefficientFCore"/>
    protected abstract void   SetRadiativeCoefficientFCore(double value);
    /// <inheritdoc cref="GetConvectiveCoefficientFCore"/>
    protected abstract double GetRadiativeCoefficientBCore();
    /// <inheritdoc cref="GetConvectiveCoefficientFCore"/>
    protected abstract void   SetRadiativeCoefficientBCore(double value);

    /// <summary>
    /// Internal (non-user) setter used by dynamic coefficient updates in
    /// <see cref="MultiRoom"/>. Updates the working value used by the
    /// solver but does NOT touch the user-backup. After the simulation
    /// step commits, <see cref="RestoreUserCoefficients"/> brings the
    /// working value back to whatever the user had set.
    /// </summary>
    internal void SetConvectiveCoefficientFInternal(double value) => SetConvectiveCoefficientFCore(value);
    /// <inheritdoc cref="SetConvectiveCoefficientFInternal"/>
    internal void SetConvectiveCoefficientBInternal(double value) => SetConvectiveCoefficientBCore(value);
    /// <inheritdoc cref="SetConvectiveCoefficientFInternal"/>
    internal void SetRadiativeCoefficientFInternal(double value)  => SetRadiativeCoefficientFCore(value);
    /// <inheritdoc cref="SetConvectiveCoefficientFInternal"/>
    internal void SetRadiativeCoefficientBInternal(double value)  => SetRadiativeCoefficientBCore(value);

    /// <summary>
    /// Restores the convective and radiative heat transfer coefficients
    /// (both F and B) to the values that the user most recently set via the
    /// public property setters. Called by <see cref="MultiRoom"/> at the end
    /// of <c>FixHeatTransfer</c> to undo any dynamic-update overrides
    /// performed during the solve, so user-facing reads after one
    /// forecast/fix cycle always show the user's original values.
    /// </summary>
    public void RestoreUserCoefficients()
    {
      SetConvectiveCoefficientFCore(_convF_userBackup);
      SetConvectiveCoefficientBCore(_convB_userBackup);
      SetRadiativeCoefficientFCore(_radF_userBackup);
      SetRadiativeCoefficientBCore(_radB_userBackup);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// For <see cref="Wall"/>: <c>cCoef + rCoef</c>. For <see cref="Window"/>:
    /// <c>1 / (2 × outermost air-gap resistance)</c>, encoding the film
    /// resistance as half of the outer "gap" entry.
    /// </remarks>
    public abstract double FilmCoefficientF { get; }

    /// <inheritdoc/>
    public abstract double FilmCoefficientB { get; }

    /// <inheritdoc/>
    /// <remarks>Reconstructed from <see cref="SurfaceF"/> / <see cref="SurfaceB"/>'s implicit-Euler step coefficients (FFS2/BFS2/IF2 + humidity terms when applicable).</remarks>
    public abstract double SurfaceTemperatureF { get; }

    /// <inheritdoc/>
    public abstract double SurfaceTemperatureB { get; }

    /// <summary>Gets or sets the short-wave (solar) absorptance on the F side [-].</summary>
    /// <remarks>
    /// Concretely the fraction of incident short-wave that is absorbed AT the
    /// surface. <see cref="Wall"/> overrides with a stored auto-property
    /// (default 0.7); <see cref="Window"/> overrides with computed values
    /// (F = 0; B = 1 − DiffuseSolarLostReflectance) and an ignored setter.
    /// Default base implementation returns 0 with a no-op setter.
    /// </remarks>
    public virtual double ShortWaveAbsorptanceF { get => 0.0; set { } }

    /// <summary>Gets or sets the short-wave (solar) absorptance on the B side [-].</summary>
    public virtual double ShortWaveAbsorptanceB { get => 0.0; set { } }

    /// <summary>
    /// Whether short-wave (solar) flux incident on this component's interior
    /// surface is absorbed at the surface (and therefore exposed via
    /// <see cref="EnvelopeSurface.AbsorbedSolarFlux"/>) or dissipated by some
    /// other path (e.g., per-layer absorption inside a glazing stack).
    /// </summary>
    /// <remarks>
    /// Default <c>false</c>. <see cref="Wall"/> overrides to <c>true</c>;
    /// <see cref="Window"/> keeps the default since absorbed solar is fed
    /// into glass layers via <see cref="OnIncidentSolarFlux"/>.
    /// </remarks>
    public virtual bool AbsorbsShortWaveAtSurface => false;

    /// <summary>Gets or sets the humidity ratio on the F side [kg/kg].</summary>
    /// <remarks>
    /// Default 0 with no-op setter. <see cref="Wall"/> overrides to provide
    /// real storage when coupled moisture transport is active.
    /// </remarks>
    public virtual double HumidityRatioF { get => 0.0; set { } }

    /// <summary>Gets or sets the humidity ratio on the B side [kg/kg].</summary>
    public virtual double HumidityRatioB { get => 0.0; set { } }

    /// <summary>Gets the moisture transfer coefficient on the F side [(kg/s)/((kg/kg)·m²)].</summary>
    /// <remarks>Default 0; <see cref="Wall"/> overrides for moisture-active configurations.</remarks>
    public virtual double MoistureCoefficientF => 0.0;

    /// <summary>Gets the moisture transfer coefficient on the B side [(kg/s)/((kg/kg)·m²)].</summary>
    public virtual double MoistureCoefficientB => 0.0;

    /// <summary>Sensitivity of F-side surface sensible heat to F-side sol-air temperature (humidity row).</summary>
    /// <remarks>Default 0. Only <see cref="Wall"/> in moisture-active configuration provides a non-zero value.</remarks>
    internal virtual double FFS3_F => 0.0;

    /// <summary>Sensitivity of B-side surface sensible heat to F-side sol-air temperature (humidity row).</summary>
    internal virtual double FFS3_B => 0.0;

    /// <summary>Sensitivity of F-side surface sensible heat to B-side sol-air temperature (humidity row).</summary>
    internal virtual double BFS3_F => 0.0;

    /// <summary>Sensitivity of B-side surface sensible heat to B-side sol-air temperature (humidity row).</summary>
    internal virtual double BFS3_B => 0.0;

    /// <summary>Sensitivity of F-side surface sensible heat to F-side humidity ratio (temperature row).</summary>
    internal virtual double FFL2_F => 0.0;

    /// <summary>Sensitivity of B-side surface sensible heat to F-side humidity ratio (temperature row).</summary>
    internal virtual double FFL2_B => 0.0;

    /// <summary>Sensitivity of F-side surface sensible heat to F-side humidity ratio (humidity row).</summary>
    internal virtual double FFL3_F => 0.0;

    /// <summary>Sensitivity of B-side surface sensible heat to F-side humidity ratio (humidity row).</summary>
    internal virtual double FFL3_B => 0.0;

    /// <summary>Sensitivity of F-side surface sensible heat to B-side humidity ratio (temperature row).</summary>
    internal virtual double BFL2_F => 0.0;

    /// <summary>Sensitivity of B-side surface sensible heat to B-side humidity ratio (temperature row).</summary>
    internal virtual double BFL2_B => 0.0;

    /// <summary>Sensitivity of F-side surface sensible heat to B-side humidity ratio (humidity row).</summary>
    internal virtual double BFL3_F => 0.0;

    /// <summary>Sensitivity of B-side surface sensible heat to B-side humidity ratio (humidity row).</summary>
    internal virtual double BFL3_B => 0.0;

    /// <summary>Inverse-matrix–derived F-side humidity contribution to the next-step nodal solution.</summary>
    internal virtual double IF3_F => 0.0;

    /// <summary>Inverse-matrix–derived B-side humidity contribution to the next-step nodal solution.</summary>
    internal virtual double IF3_B => 0.0;

    /// <summary>
    /// Whether the F side is exposed to outdoor wind. Default <c>false</c>.
    /// When <c>true</c>, the F-side convective coefficient participates in the
    /// wind-speed-driven dynamic update (<see cref="MultiRoom.DynamicOutdoorConvectiveCoefficient"/>).
    /// Subclasses may override the default; for example <see cref="Window"/> defaults to <c>true</c>
    /// since its F side is by construction the outdoor face.
    /// </summary>
    public virtual bool IsWindExposedF { get; set; } = false;

    /// <summary>
    /// Whether the B side is exposed to outdoor wind. Default <c>false</c>.
    /// When <c>true</c>, the B-side convective coefficient participates in the
    /// wind-speed-driven dynamic update.
    /// </summary>
    public virtual bool IsWindExposedB { get; set; } = false;

    /// <summary>
    /// Forced-convection roughness multiplier R_f [-] applied to the windward
    /// MoWiTT correlation on the F side. Default 1.0 (smooth glass — base
    /// MoWiTT). Subclasses may seed this in their constructor; <see cref="Wall"/>
    /// initializes it to <see cref="SurfaceRoughness.Rough"/>'s multiplier (1.67).
    /// </summary>
    /// <remarks>
    /// Set numerically with the property setter or categorically via
    /// <see cref="SetSurfaceRoughnessF"/>. Only consumed when
    /// <see cref="MultiRoom.DynamicOutdoorConvectiveCoefficient"/> is enabled
    /// for the side that is wind-exposed.
    /// </remarks>
    public double SurfaceRoughnessMultiplierF { get; set; } = 1.0;

    /// <summary>
    /// Forced-convection roughness multiplier R_f [-] applied to the windward
    /// MoWiTT correlation on the B side. Default 1.0 (smooth glass).
    /// </summary>
    public double SurfaceRoughnessMultiplierB { get; set; } = 1.0;

    /// <summary>Sets <see cref="SurfaceRoughnessMultiplierF"/> from a categorical roughness.</summary>
    public void SetSurfaceRoughnessF(SurfaceRoughness roughness)
        => SurfaceRoughnessMultiplierF = roughness.GetMultiplier();

    /// <summary>Sets <see cref="SurfaceRoughnessMultiplierB"/> from a categorical roughness.</summary>
    public void SetSurfaceRoughnessB(SurfaceRoughness roughness)
        => SurfaceRoughnessMultiplierB = roughness.GetMultiplier();

    /// <summary>
    /// Mid-height above ground [m] of this component's exterior face. Used to
    /// translate the recorded wind speed at the meteorological station to the
    /// wind speed at this surface via the ASHRAE atmospheric boundary-layer
    /// power law. <c>null</c> (the default) means the height is unknown — the
    /// raw recorded wind speed is then used without correction.
    /// </summary>
    /// <remarks>
    /// Set with <see cref="SetMidHeightAboveGround"/> /
    /// <see cref="ClearMidHeightAboveGround"/>. Only consumed when
    /// <see cref="MultiRoom.DynamicOutdoorConvectiveCoefficient"/> is enabled
    /// AND the building model has been given valid weather-station metadata
    /// (<see cref="MultiRoom.WeatherStation"/>) and a site terrain
    /// (<see cref="MultiRoom.SiteTerrainCategory"/>).
    /// </remarks>
    public double? MidHeightAboveGround { get; private set; }

    /// <summary>Sets <see cref="MidHeightAboveGround"/> to the given value [m].</summary>
    public void SetMidHeightAboveGround(double height) => MidHeightAboveGround = height;

    /// <summary>Clears <see cref="MidHeightAboveGround"/> back to <c>null</c> (unknown).</summary>
    public void ClearMidHeightAboveGround() => MidHeightAboveGround = null;

    /// <summary>
    /// Computes the outdoor-side convective heat transfer coefficient
    /// [W/(m²·K)] on the requested side from the local wind speed and the
    /// outdoor air temperature. The base implementation uses the Walton TARP
    /// correlation (<see cref="ExteriorConvection.GetWaltonTarp"/>) — the
    /// engineering norm for opaque rough surfaces such as a typical wall or
    /// roof. <see cref="Window"/> overrides this to use the smooth-glass
    /// MoWiTT correlation instead.
    /// </summary>
    /// <param name="isSideF">True for the F side; false for the B side.</param>
    /// <param name="windSpeed">Local wind speed at the surface [m/s].</param>
    /// <param name="airTemperature">Outdoor air temperature [°C].</param>
    /// <param name="orientation">Windward / leeward, selecting the fitted
    /// forced-convection constants.</param>
    /// <remarks>
    /// Pulls every other input — surface temperature, surface roughness
    /// multiplier and surface incline — from the component's own state, so
    /// subclasses can fully customize the correlation without expanding the
    /// parameter list. Used by
    /// <see cref="MultiRoom.UpdateOutdoorConvectiveCoefficient"/>.
    /// </remarks>
    internal virtual double ComputeExteriorConvectiveCoefficient(
        bool isSideF, double windSpeed, double airTemperature, WindOrientation orientation)
    {
      double tSurf = isSideF ? SurfaceTemperatureF : SurfaceTemperatureB;
      double rf = isSideF ? SurfaceRoughnessMultiplierF : SurfaceRoughnessMultiplierB;
      IReadOnlyIncline? incline = isSideF ? SurfaceF.Incline : SurfaceB.Incline;
      double dT = tSurf - airTemperature;
      // Incline 未指定時は垂直面相当として扱う (TARP 自然対流 = 1.31|ΔT|^(1/3))。
      // Walton 元式は傾斜→垂直の連続線形補間も提案するが、未指定 = "幾何不明"
      // を意味するため、もっとも一般的な垂直壁にフォールバックする。
      if (incline == null)
        return ExteriorConvection.GetWaltonTarp(_verticalIncline, windSpeed, dT, rf, orientation);
      return ExteriorConvection.GetWaltonTarp(incline, windSpeed, dT, rf, orientation);
    }

    /// <summary>幾何未指定時のフォールバック垂直面 (Walton TARP 用)。</summary>
    private static readonly Incline _verticalIncline =
        new Incline(Incline.Orientation.S, 0.5 * Math.PI);

    #endregion

    #region 短波長放出 (層別光学モデル)

    /// <summary>
    /// Computes this component's short-wave (solar) radiation contribution to
    /// the indoor space at one time step, using its own optical model and the
    /// solar state at its outdoor-facing surface.
    /// </summary>
    /// <param name="indoorSurface">
    /// The indoor-facing <see cref="EnvelopeSurface"/> (one of <see cref="SurfaceF"/> /
    /// <see cref="SurfaceB"/>) from which the room is observing this component.
    /// </param>
    /// <param name="sun">The current solar geometry / radiation state.</param>
    /// <param name="albedo">Ground albedo [-].</param>
    /// <returns>
    /// A <see cref="ShortWaveEmission"/> describing the flux absorbed at the
    /// indoor surface and the power transmitted into the room. Opaque
    /// components return <see cref="ShortWaveEmission.Zero"/>; their outdoor
    /// short-wave absorption is folded into the sol-air temperature on the
    /// outdoor face elsewhere.
    /// </returns>
    public abstract ShortWaveEmission EmitShortWaveToIndoor(
      EnvelopeSurface indoorSurface,
      IReadOnlySun sun,
      double albedo);

    /// <summary>
    /// Gets the effective absorptance [-] for indoor diffuse short-wave
    /// arriving on this component's indoor-facing surface from interior
    /// inter-reflection (the Gebhart-distributed remainder).
    /// </summary>
    /// <remarks>
    /// Opaque components return 1.0 — all incident diffuse short-wave is
    /// absorbed at first hit, and room-level multi-reflection is already
    /// captured by the Gebhart matrix. Translucent components (windows)
    /// return a factor that accounts for inter-layer back-and-forth
    /// (typically <c>DiffuseAbsorptance / (1 − DiffuseReflectance)</c>).
    /// </remarks>
    public abstract double IndoorDiffuseAbsorptanceFactor { get; }

    /// <summary>
    /// Refreshes optical properties for the current solar geometry. Opaque
    /// components are typically a no-op; translucent components (windows,
    /// future translucent walls) recompute angle-dependent transmittance,
    /// reflectance, and absorptance.
    /// </summary>
    /// <param name="sun">Current solar geometry / radiation state.</param>
    public virtual void UpdateOpticalProperties(IReadOnlySun sun) { }

    #endregion

    #region 行列ソルバ — 状態フィールド

    /// <summary>True when the inverse step-coefficient matrix needs rebuilding.</summary>
    protected bool needToUpdateUINVMatrix = true;

    /// <summary>True when the implicit-Euler coefficient matrix needs rebuilding.</summary>
    protected bool needToUpdateUMatrix = false;

    /// <summary>Vector holding the nodal state (temperature, plus humidity in moisture-mode subclasses).</summary>
    protected IVector tempAndHumid = null!;

    /// <summary>Per-node sensible heat capacity [J/(m²·K)]. Index 0 = F-face surface node.</summary>
    protected double[] capS = null!;

    /// <summary>Inter-node sensible thermal resistance [m²·K/W]. <c>resS[0]</c> and <c>resS[NodeCount]</c> are the F/B film resistances.</summary>
    protected double[] resS = null!;

    /// <summary>Per-node short-wave (solar) absorption [W/m²] supplied externally as a body source. Default zero.</summary>
    protected double[] solarAbsorption = null!;

    /// <summary>Per-node coefficient mapping <see cref="solarAbsorption"/> to its RHS contribution.</summary>
    /// <remarks>
    /// For nodes with non-zero capacity: <c>Δt / capS[i]</c> (sensible-only)
    /// or the equivalent moisture-mode factor; for zero-capacity steady-state
    /// nodes: <c>1.0</c> in sensible-only mode. Computed in <see cref="UpdateUMatrix"/>.
    /// </remarks>
    protected double[] qCoefS = null!;

    /// <summary>Implicit-Euler coefficient matrix <c>(I − Δt·A)</c>.</summary>
    protected IMatrix uMatrix = null!;

    /// <summary>Inverse of the matrix actually used for the step solve (= <see cref="uMatrix"/> for opaque resistive cases without pipes).</summary>
    protected IMatrix uxMatrix = null!;

    /// <summary>Per-node coefficient for the F-side boundary input contribution to the RHS.</summary>
    protected double[] uSF = null!;

    /// <summary>Per-node coefficient for the B-side boundary input contribution to the RHS.</summary>
    protected double[] uSB = null!;

    /// <summary>Calculation time step [s].</summary>
    protected double timeStep = 3600;

    #endregion

    #region 行列ソルバ — public プロパティ

    /// <summary>Gets or sets the calculation time step [s]. Setting a new value flags the matrix for rebuild.</summary>
    public virtual double TimeStep
    {
      get { return timeStep; }
      set
      {
        if (value <= 0 || timeStep == value) return;
        timeStep = value;
        needToUpdateUMatrix = true;
      }
    }

    /// <summary>Gets the number of nodes in the finite-difference network.</summary>
    public abstract int NodeCount { get; }

    /// <summary>Gets the node temperature distribution [°C].</summary>
    public IVector Temperatures
    { get { return new VectorView(tempAndHumid, 0, NodeCount); } }

    /// <summary>Sensitivity of the F-side surface temperature to the F-side sol-air temperature (sensible).</summary>
    public double FFS2_F { get; protected set; }

    /// <summary>Sensitivity of the B-side surface temperature to the F-side sol-air temperature (sensible).</summary>
    public double FFS2_B { get; protected set; }

    /// <summary>Sensitivity of the F-side surface temperature to the B-side sol-air temperature (sensible).</summary>
    public double BFS2_F { get; protected set; }

    /// <summary>Sensitivity of the B-side surface temperature to the B-side sol-air temperature (sensible).</summary>
    public double BFS2_B { get; protected set; }

    /// <summary>Initial-state contribution to the F-side surface temperature (sensible).</summary>
    public double IF2_F { get; protected set; }

    /// <summary>Initial-state contribution to the B-side surface temperature (sensible).</summary>
    public double IF2_B { get; protected set; }

    #endregion

    #region 層別吸収日射 API

    /// <summary>
    /// Supplies a per-node short-wave absorption [W/m²] as a body source in
    /// the next <see cref="Update"/>. Intended for translucent constructions
    /// (e.g., a window assembly modeled as a glass / air-gap stack) whose
    /// individual layers absorb a fraction of the incident solar.
    /// </summary>
    /// <param name="nodeIndex">Node index in <c>[0, NodeCount)</c>. Node 0 is the F-face surface; node <see cref="NodeCount"/>−1 is the B-face surface.</param>
    /// <param name="qPerArea">Absorbed short-wave heat flux at the node [W/m²]. Treated as constant over the time step.</param>
    public void SetLayerSolarAbsorption(int nodeIndex, double qPerArea)
    {
      solarAbsorption[nodeIndex] = qPerArea;
    }

    /// <summary>Resets all per-node absorbed solar inputs to zero.</summary>
    public void ClearLayerSolarAbsorption()
    {
      Array.Clear(solarAbsorption, 0, solarAbsorption.Length);
    }

    /// <summary>
    /// Hook called from <see cref="EnvelopeSurface.SetIncidentSolarFlux(double)"/>
    /// after the surface-level absorbed flux has been recorded. Allows
    /// translucent components to redistribute the indoor-side incident flux
    /// onto their per-layer solar-absorption inputs (e.g., a window
    /// re-absorbing inter-reflected diffuse from the room interior into its
    /// glass layers).
    /// </summary>
    /// <param name="surface">The surface that received the flux.</param>
    /// <param name="incidentShortWaveFlux">Indoor-incident short-wave flux at the surface [W/m²].</param>
    /// <remarks>
    /// Default no-op — opaque components rely on the surface-level
    /// <see cref="EnvelopeSurface.AbsorbedSolarFlux"/> mechanism instead.
    /// </remarks>
    public virtual void OnIncidentSolarFlux(EnvelopeSurface surface, double incidentShortWaveFlux) { }

    #endregion

    #region 行列ソルバ — sensible-only 経路 (subclass がフルバージョンを override 可能)

    /// <summary>Subclass hook: populates <see cref="capS"/> and <see cref="resS"/> from the subclass's own layer representation.</summary>
    /// <remarks>
    /// Called by the default <see cref="UpdateUMatrix"/> before constructing
    /// the matrix. For Wall, the implementation reads layer heat capacity
    /// and conductance; for Window (Phase C-2), it reads glass and air-gap
    /// resistances. Subclasses that fully override <see cref="UpdateUMatrix"/>
    /// (e.g., Wall in moisture mode) may not invoke this hook.
    /// </remarks>
    protected abstract void PopulateSensibleProperties();

    /// <summary>Rebuilds the implicit-Euler coefficient matrix when flagged.</summary>
    /// <remarks>
    /// The base implementation handles the sensible-only case with no embedded
    /// pipes. Subclasses with moisture or pipe coupling override this to add
    /// their extensions (and may bypass the base when the structure differs).
    /// </remarks>
    protected virtual void UpdateUMatrix()
    {
      if (!needToUpdateUMatrix) return;
      needToUpdateUINVMatrix = true;

      int mNum = NodeCount;
      PopulateSensibleProperties();

      uMatrix.Initialize(0);
      for (int i = 0; i < mNum; i++)
      {
        if (capS[i] == 0)
        {
          uSF[i] = 1 / resS[i];
          uSB[i] = 1 / resS[i + 1];
          qCoefS[i] = 1.0;
        }
        else
        {
          uSF[i] = timeStep / (capS[i] * resS[i]);
          uSB[i] = timeStep / (capS[i] * resS[i + 1]);
          qCoefS[i] = timeStep / capS[i];
        }
        if (i != 0) uMatrix[i, i - 1] = -uSF[i];
        if (i != mNum - 1) uMatrix[i, i + 1] = -uSB[i];
        if (capS[i] == 0) uMatrix[i, i] = uSF[i] + uSB[i];
        else uMatrix[i, i] = 1d + uSF[i] + uSB[i];
      }
      needToUpdateUMatrix = false;
    }

    /// <summary>Rebuilds <see cref="uxMatrix"/> from the (possibly extension-augmented) coefficient matrix.</summary>
    /// <remarks>
    /// The base implementation is the sensible-only no-pipe path:
    /// <c>uxMatrix = inv(uMatrix)</c>. Subclasses with pipe coupling override
    /// to invert a pipe-augmented matrix instead.
    /// </remarks>
    public virtual void UpdateInverseMatrix()
    {
      UpdateUMatrix();
      if (needToUpdateUINVMatrix)
      {
        needToUpdateUINVMatrix = false;
        LinearAlgebraOperations.GetInverse(uMatrix, uxMatrix);
        int num = uxMatrix.Rows - 1;
        FFS2_F = uxMatrix[0, 0] * uSF[0];
        FFS2_B = uxMatrix[num, 0] * uSF[0];
        BFS2_F = uxMatrix[0, num] * uSB[num];
        BFS2_B = uxMatrix[num, num] * uSB[num];
        InverseMatrixUpdated = true;
      }
    }

    /// <summary>Refreshes the IF (current-state) coefficients from the latest nodal state and inverse matrix.</summary>
    /// <remarks>
    /// The base implementation is sensible-only. Subclasses with moisture or
    /// pipe coupling override to add the corresponding contributions to the
    /// per-node body-source term.
    /// </remarks>
    public virtual void UpdateIFCoefficients()
    {
      int num = uxMatrix.Rows - 1;
      IF2_F = IF2_B = 0;
      for (int i = 0; i <= num; i++)
      {
        double bf = tempAndHumid[i];
        if (solarAbsorption[i] != 0) bf += qCoefS[i] * solarAbsorption[i];
        IF2_F += uxMatrix[0, i] * bf;
        IF2_B += uxMatrix[num, i] * bf;
      }
    }

    /// <summary>Advances the nodal state by one time step using the current sol-air temperatures.</summary>
    /// <remarks>
    /// The base implementation is sensible-only with no embedded pipes.
    /// Subclasses with moisture transport, pipes, or PCM layers override to
    /// add their RHS contributions and any post-solve state adjustments.
    /// </remarks>
    public virtual void Update()
    {
      UpdateInverseMatrix();

      int mNum = NodeCount;
      int last = mNum - 1;
      Vector tempAndHumid2 = new Vector(tempAndHumid.Length);
      tempAndHumid2.Initialize(0);

      tempAndHumid2[0] = uSF[0] * SolAirTemperatureF;
      tempAndHumid2[last] = uSB[last] * SolAirTemperatureB;
      for (int i = 0; i < tempAndHumid2.Length; i++)
        if (capS[i] != 0) tempAndHumid2[i] += tempAndHumid[i];
      for (int i = 0; i < mNum; i++)
        if (solarAbsorption[i] != 0) tempAndHumid2[i] += qCoefS[i] * solarAbsorption[i];

      LinearAlgebraOperations.Multiply(uxMatrix, tempAndHumid2, tempAndHumid, 1, 0);

      UpdateIFCoefficients();
    }

    /// <summary>Resets the nodal temperature distribution to a uniform value and rebuilds.</summary>
    /// <param name="temperature">Initial temperature [°C].</param>
    public virtual void Initialize(double temperature)
    {
      VectorView temp = new VectorView(tempAndHumid, 0, NodeCount);
      temp.Initialize(temperature);
      SolAirTemperatureF = SolAirTemperatureB = temperature;
      needToUpdateUMatrix = true;
      Update();
    }

    /// <summary>
    /// Solver-managed flag: <c>true</c> when this component's inverse matrix
    /// has been recomputed since the last AB-matrix rebuild. Set by the
    /// component's per-step Update path; the solver consumes the flag in
    /// <c>MakeABMatrix</c> and clears it at the start of the next time step.
    /// </summary>
    /// <remarks>
    /// Virtual so that <see cref="Wall"/> can override the initial value to
    /// <c>true</c> (forcing the first AB-matrix build) without changing the
    /// safer default of <c>false</c> for components whose inverse matrix is
    /// not in active use.
    /// </remarks>
    public virtual bool InverseMatrixUpdated { get; set; }

    #endregion

  }
}
