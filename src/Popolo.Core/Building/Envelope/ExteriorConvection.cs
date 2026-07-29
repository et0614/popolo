/* ExteriorConvection.cs
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
 *
 * References:
 *   Yazdanian, M., Klems, J. H., 1994. Measurement of the exterior
 *     convective film coefficient for windows in low-rise buildings.
 *     ASHRAE Transactions 100 (1).
 *     — MoWiTT correlation: smooth-glass natural + forced fits used by
 *     GetMoWiTT (and OpticalLayeredEnvelope's Window override).
 *
 *   Walton, G. N., 1981. Passive solar extension of the building loads
 *     analysis and system thermodynamics (BLAST) program. USACERL Tech.
 *     Manual.
 *   Walton, G. N., 1983. Thermal Analysis Research Program reference
 *     manual. NBSIR 83-2655, U.S. Dept. of Commerce.
 *     — TARP natural-convection correlations (vertical / horizontal
 *     stable / horizontal unstable) used by GetWaltonTarpNatural and
 *     therefore by GetWaltonTarp (combined exterior coefficient for
 *     opaque rough surfaces). The same Walton coefficients are reused
 *     for the indoor side via MultiRoom.UpdateIndoorConvectiveCoefficient
 *     for consistency between the two natural-convection environments.
 */

using System;
using Popolo.Core.Climate;

namespace Popolo.Core.Building.Envelope
{
  /// <summary>
  /// Static helpers for the outdoor (exterior) convective heat transfer
  /// coefficient on building envelope surfaces. Provides the MoWiTT
  /// (Yazdanian–Klems 1994; smooth-glass) and Walton TARP (1981/1983;
  /// general / opaque rough surface) correlations as well as the natural
  /// component shared with the indoor TARP update.
  /// </summary>
  /// <remarks>
  /// <para>
  /// MoWiTT was fitted to outdoor measurements on smooth window glass at
  /// the Mobile Window Thermal Test facility; its natural-convection
  /// constant <c>Ct = 0.84</c> is consistent with that surface texture and
  /// vertical orientation only. Walton TARP uses the McAdams family of
  /// natural-convection coefficients (<c>1.31</c> vertical, <c>1.52</c>
  /// horizontal unstable, <c>0.76</c> horizontal stable) that are the
  /// engineering norm for general — and especially rough opaque — surfaces
  /// at building scale.
  /// </para>
  /// <para>
  /// Selection between MoWiTT and TARP at the call site is made by the
  /// envelope component itself: <see cref="OpticalLayeredEnvelope"/> defaults
  /// to TARP (Wall-style opaque rough surface), while <see cref="Window"/>
  /// overrides to MoWiTT (smooth glass).
  /// </para>
  /// </remarks>
  public static class ExteriorConvection
  {
    /// <summary>
    /// Minimum effective wind speed [m/s] applied to the forced-convection
    /// term of the outdoor correlations (<see cref="GetMoWiTT"/>,
    /// <see cref="GetWaltonTarp"/>). Real outdoor air at a "calm" weather
    /// record is never truly stagnant; recorded V = 0 typically reflects
    /// the anemometer cut-in threshold (≈ 0.5 m/s) rather than physical
    /// quiescence. Without this floor the forced term collapses to zero at
    /// V = 0 and the combined coefficient becomes a poor underestimate
    /// (BESTEST C195/C200 PH systematically -5% under reference 6-tool
    /// envelope at the V = 0 peak hour). Indoor air typically stays well
    /// below 0.2 m/s so this floor is intentionally not applied to the
    /// natural-only indoor path (<see cref="GetWaltonTarpNatural"/>).
    /// 0.5 m/s follows the ASHRAE 1101-RP recommendation also adopted by
    /// EnergyPlus's TARP / DOE-2 algorithms.
    /// </summary>
    public const double MinForcedWindSpeed = 0.5;

    #region MoWiTT (for smooth glass windows)

    /// <summary>
    /// MoWiTT exterior convective coefficient [W/(m²·K)] for a smooth glass
    /// surface, combining a glass-fitted natural term with the
    /// Yazdanian–Klems forced term:
    /// <c>h_c = sqrt( (0.84·|ΔT|^(1/3))² + (R_f·a·v^b)² )</c>.
    /// </summary>
    /// <param name="windSpeed">Local wind speed at the surface [m/s].
    /// Negative values are clamped to 0.</param>
    /// <param name="surfaceAirDeltaT">Surface − outdoor air temperature
    /// difference [K] (sign ignored; the natural term uses |ΔT|).</param>
    /// <param name="roughnessMultiplier">Forced-convection roughness
    /// multiplier R_f [-]. 1.0 (smooth glass — base MoWiTT) up to ~2.17
    /// (very rough). See <see cref="SurfaceRoughness"/>.</param>
    /// <param name="orientation">Windward (impingement) or leeward
    /// (recirculation) selects the fitted constants
    /// (<c>a = 3.26, b = 0.89</c> windward; <c>a = 3.55, b = 0.617</c>
    /// leeward).</param>
    public static double GetMoWiTT(
        double windSpeed, double surfaceAirDeltaT,
        double roughnessMultiplier, WindOrientation orientation)
    {
      const double Ct = 0.84;
      var (a, b) = orientation == WindOrientation.Windward ? (3.26, 0.89) : (3.55, 0.617);
      // Apply the wind speed lower limit V_min to the forced term only (the natural term is unaffected).
      double v = windSpeed > MinForcedWindSpeed ? windSpeed : MinForcedWindSpeed;
      double dT = Math.Abs(surfaceAirDeltaT);
      double natural = Ct * Math.Pow(dT, 1.0 / 3.0);
      double forced = roughnessMultiplier * a * Math.Pow(v, b);
      return Math.Sqrt(natural * natural + forced * forced);
    }

    #endregion

    #region Walton TARP natural convection (indoor and outdoor)

    /// <summary>
    /// Walton TARP natural-convection coefficient [W/(m²·K)] for an
    /// arbitrarily inclined surface, selecting between vertical, horizontal
    /// stable and horizontal unstable regimes from the surface tilt and the
    /// signed surface-air temperature difference.
    /// </summary>
    /// <param name="incline">Surface incline (the <see cref="IReadOnlyIncline.VerticalAngle"/>
    /// is interpreted as 0 for upward-facing horizontal, π/2 for vertical,
    /// and π for downward-facing horizontal).</param>
    /// <param name="surfaceAirDeltaT">Signed surface − air temperature
    /// difference [K]. Sign distinguishes stable vs. unstable buoyancy on
    /// horizontal faces.</param>
    /// <returns>Natural-convection coefficient h_n [W/(m²·K)], with a small
    /// floor (≈ 0.948) applied so the formula does not collapse at ΔT → 0.</returns>
    /// <remarks>
    /// Used both for the outdoor side (combined with the MoWiTT forced term
    /// in <see cref="GetWaltonTarp"/>) and for the indoor side (no forced
    /// component) by the dynamic indoor / outdoor convective coefficient
    /// updates. The horizontal stable coefficient 0.76 (rather than 0.59)
    /// follows the Walton 1983 TARP indoor formulas as adopted by EnergyPlus
    /// DetailedNaturalConvection — keeping a single set of constants in
    /// both environments.
    /// </remarks>
    public static double GetWaltonTarpNatural(
        IReadOnlyIncline incline, double surfaceAirDeltaT)
    {
      const double horizontalTiltThreshold = 5.0 * Math.PI / 180.0;  // 5°
      // Lower limit ≒ 1.31 × 0.43^(1/3): matches the value the vertical formula gives at ΔT=0.43 K,
      // so that h does not collapse to 0 as ΔT→0 (Walton/EnergyPlus TARP practice).
      const double minH = 0.948;

      double dT = Math.Abs(surfaceAirDeltaT);
      double dT13 = Math.Pow(dT, 1.0 / 3.0);

      double beta = incline.VerticalAngle;   // 0 = up, π/2 = vertical, π = down
      bool isUpward = beta < horizontalTiltThreshold;
      bool isDownward = beta > Math.PI - horizontalTiltThreshold;

      double h;
      if (!isUpward && !isDownward)
      {
        // Vertical or tilted surface
        h = 1.31 * dT13;
      }
      else
      {
        // Horizontal surface: determine stable/unstable from the facing direction and the sign of ΔT
        //   Facing up + warm surface (ΔT>0) → rising plume → UNSTABLE
        //   Facing down + cool surface (ΔT<0) → sinking plume → UNSTABLE
        bool unstable = (isUpward && surfaceAirDeltaT > 0)
                     || (isDownward && surfaceAirDeltaT < 0);
        h = unstable ? 1.52 * dT13 : 0.76 * dT13;
      }
      return h < minH ? minH : h;
    }

    #endregion

    #region Walton TARP outdoor combination (for rough opaque walls)

    /// <summary>
    /// Walton TARP exterior convective coefficient [W/(m²·K)] for an opaque
    /// (and typically rough) building surface, combining the orientation-
    /// aware TARP natural term (<see cref="GetWaltonTarpNatural"/>) with the
    /// MoWiTT forced term scaled by the surface roughness multiplier:
    /// <c>h_c = sqrt( h_n_TARP² + (R_f·a·v^b)² )</c>.
    /// </summary>
    /// <param name="incline">Surface incline. The TARP natural term selects
    /// vertical / horizontal-stable / horizontal-unstable based on this and
    /// the signed ΔT.</param>
    /// <param name="windSpeed">Local wind speed at the surface [m/s].</param>
    /// <param name="surfaceAirDeltaT">Signed surface − outdoor air
    /// temperature difference [K]. Sign distinguishes stable vs. unstable
    /// buoyancy on horizontal faces.</param>
    /// <param name="roughnessMultiplier">Forced-convection roughness
    /// multiplier R_f [-] (1.67 for typical rough wall siding, etc.).</param>
    /// <param name="orientation">Windward or leeward — selects the fitted
    /// MoWiTT-style forced constants.</param>
    public static double GetWaltonTarp(
        IReadOnlyIncline incline,
        double windSpeed, double surfaceAirDeltaT,
        double roughnessMultiplier, WindOrientation orientation)
    {
      double hN = GetWaltonTarpNatural(incline, surfaceAirDeltaT);
      var (a, b) = orientation == WindOrientation.Windward ? (3.26, 0.89) : (3.55, 0.617);
      // Apply the wind speed lower limit V_min to the forced term only (the natural term is unaffected).
      double v = windSpeed > MinForcedWindSpeed ? windSpeed : MinForcedWindSpeed;
      double hF = roughnessMultiplier * a * Math.Pow(v, b);
      return Math.Sqrt(hN * hN + hF * hF);
    }

    #endregion
  }
}
