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

using Popolo.Core.Climate;

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
  /// Upper-layer code (<see cref="MultiRoom"/>, <see cref="Zone"/>) drives
  /// the heat balance through this base type and the surfaces it exposes,
  /// without needing to distinguish walls from windows. This base owns the
  /// truly common F/B side state (sol-air temperatures, long-wave
  /// emissivities, surfaces) and provides default no-op implementations of
  /// the dynamic-response and optical-update hooks; subclasses override only
  /// what is meaningful for them. Film-coefficient setters and storage stay
  /// in each derived class because their side effects differ
  /// (<see cref="Wall"/> flags its conduction matrix for rebuild;
  /// <see cref="Window"/> recomputes its glass / air-gap resistance lookup).
  /// </para>
  /// </remarks>
  public abstract class OpticalLayeredEnvelope
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

    #endregion

    #region 動的応答フック (default no-op)

    /// <summary>
    /// Refreshes optical properties for the current solar geometry. Opaque
    /// components are typically a no-op; translucent components (windows,
    /// future translucent walls) recompute angle-dependent transmittance,
    /// reflectance, and absorptance.
    /// </summary>
    /// <param name="sun">Current solar geometry / radiation state.</param>
    public virtual void UpdateOpticalProperties(IReadOnlySun sun) { }

    /// <summary>
    /// Refreshes the inverse step-coefficient matrix and the boundary-temperature
    /// sensitivity coefficients (FFS / BFS). Called by <see cref="MultiRoom"/>
    /// when surface heat-transfer coefficients change mid-step. No-op for
    /// components without a dynamic conduction response (current
    /// <see cref="Window"/>, until per-glass-layer heat capacity is added).
    /// </summary>
    public virtual void UpdateInverseMatrix() { }

    /// <summary>
    /// Refreshes the IF (current-state) coefficients from the component's
    /// internal state, keeping them consistent with the latest inverse matrix.
    /// Must accompany every <see cref="UpdateInverseMatrix"/> call so that
    /// <c>MakeABMatrix</c> (FFS / BFS) and <c>MakeCVector</c> (IF) stay
    /// mutually consistent. No-op for components without dynamic conduction
    /// response.
    /// </summary>
    public virtual void UpdateIFCoefficients() { }

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
