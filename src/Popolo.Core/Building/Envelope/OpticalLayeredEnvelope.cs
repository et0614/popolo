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
