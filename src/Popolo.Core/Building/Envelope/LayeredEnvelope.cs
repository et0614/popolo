/* LayeredEnvelope.cs
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
  /// Abstract base for envelope components composed of layers (opaque walls,
  /// glazing assemblies, future translucent walls), centralizing the F/B side
  /// state shared across all such components.
  /// </summary>
  /// <remarks>
  /// <para>
  /// Derived types include <see cref="Wall"/> (multi-layer transient
  /// conduction with optional embedded radiant pipes, PCM, and coupled
  /// moisture transport) and <see cref="Window"/> (multi-pane glazing with
  /// internal shading devices and external sun-shading geometry). Both share
  /// the same notion of two opposing boundary surfaces (F and B) carrying
  /// sol-air temperatures, long-wave emissivities, convective and radiative
  /// film coefficients, and short-wave behavior — but they differ in how the
  /// internal layer stack converts those boundary inputs into surface
  /// temperatures (Wall: implicit-Euler matrix solve; Window: pure resistive
  /// network for now, eventually a layer matrix once per-glass-layer heat
  /// capacity is introduced).
  /// </para>
  /// <para>
  /// This base owns <b>only the truly common state</b>: sol-air temperatures
  /// and long-wave emissivities. Film-coefficient setters and storage stay in
  /// each derived class because their side effects differ
  /// (<see cref="Wall"/> flags its conduction matrix for rebuild;
  /// <see cref="Window"/> recomputes its glass / air-gap resistance lookup).
  /// The matrix-solver hooks (<see cref="UpdateInverseMatrix"/>,
  /// <see cref="UpdateIFCoefficients"/>) and the optical-property hook
  /// (<see cref="UpdateOpticalProperties"/>) are virtual no-ops here;
  /// subclasses override only what is meaningful for them.
  /// </para>
  /// </remarks>
  public abstract class LayeredEnvelope : IEnvelopeComponent
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

    #region IEnvelopeComponent — subclass concrete contract

    /// <inheritdoc/>
    /// <remarks>
    /// Both get and set are abstract because <see cref="Wall"/> exposes a
    /// trivial auto-property setter while <see cref="Window"/> validates the
    /// value (rejects non-positive areas).
    /// </remarks>
    public abstract double Area { get; set; }

    /// <inheritdoc/>
    /// <remarks>Set by the subclass constructor when the surface objects are created.</remarks>
    public EnvelopeSurface SurfaceF { get; protected set; } = null!;

    /// <inheritdoc/>
    /// <remarks>Set by the subclass constructor when the surface objects are created.</remarks>
    public EnvelopeSurface SurfaceB { get; protected set; } = null!;

    /// <inheritdoc/>
    public abstract ShortWaveEmission EmitShortWaveToIndoor(
      EnvelopeSurface indoorSurface,
      IReadOnlySun sun,
      double albedo);

    /// <inheritdoc/>
    public abstract double IndoorDiffuseAbsorptanceFactor { get; }

    #endregion

    #region IEnvelopeComponent — virtual hooks (default no-op)

    /// <inheritdoc/>
    /// <remarks>
    /// Default no-op for opaque components with static optical properties.
    /// <see cref="Window"/> and any future translucent <see cref="Wall"/>
    /// override this to recompute angle-dependent transmittance / reflectance
    /// / absorptance for the current solar position.
    /// </remarks>
    public virtual void UpdateOpticalProperties(IReadOnlySun sun) { }

    /// <inheritdoc/>
    /// <remarks>
    /// Default no-op for components without a dynamic conduction model
    /// (current resistive-only <see cref="Window"/>). <see cref="Wall"/>
    /// overrides this to rebuild its inverse step-coefficient matrix.
    /// </remarks>
    public virtual void UpdateInverseMatrix() { }

    /// <inheritdoc/>
    /// <remarks>
    /// Default no-op companion to <see cref="UpdateInverseMatrix"/>.
    /// </remarks>
    public virtual void UpdateIFCoefficients() { }

    /// <inheritdoc/>
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
