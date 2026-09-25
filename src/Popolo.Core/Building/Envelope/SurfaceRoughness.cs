/* SurfaceRoughness.cs
 *
 * Copyright (C) 2026 E.Togashi
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;

namespace Popolo.Core.Building.Envelope
{
  /// <summary>
  /// Categorical surface roughness used to scale the forced-convection term of
  /// the exterior film coefficient correlations (MoWiTT for glass, Walton TARP
  /// for opaque surfaces).
  /// </summary>
  /// <remarks>
  /// <para>
  /// The numeric multiplier <c>R_f</c> is applied to the forced-convection
  /// component of the exterior convective coefficient
  /// (see <see cref="ExteriorConvection"/>): surfaces rougher than smooth glass
  /// enhance the wind-driven heat transfer.
  /// </para>
  /// <para>
  /// Values follow ASHRAE Handbook — Fundamentals (2009), Ch. 26 Table 4 and
  /// the EnergyPlus engineering reference for "Material:Roughness" used by the
  /// DOE-2 / MoWiTT model.
  /// </para>
  /// <para>
  /// Internally <see cref="OpticalLayeredEnvelope"/> stores only the numeric
  /// multiplier (<c>SurfaceRoughnessMultiplierF/B</c>); this enum is purely a
  /// convenience for setting that multiplier from a named category. Users may
  /// also assign any positive double directly without going through the enum.
  /// </para>
  /// </remarks>
  public enum SurfaceRoughness
  {
    /// <summary>R_f = 2.17 — e.g. rough stucco, very rough concrete.</summary>
    VeryRough,

    /// <summary>R_f = 1.67 — e.g. brick, rough plaster, typical light-weight cladding.</summary>
    Rough,

    /// <summary>R_f = 1.52 — e.g. concrete (smooth), shingles.</summary>
    MediumRough,

    /// <summary>R_f = 1.13 — e.g. clear pine, painted wood.</summary>
    MediumSmooth,

    /// <summary>R_f = 1.11 — e.g. plaster (smooth).</summary>
    Smooth,

    /// <summary>R_f = 1.00 — e.g. window glass, polished metal.</summary>
    VerySmooth,
  }

  /// <summary>Extension methods for <see cref="SurfaceRoughness"/>.</summary>
  public static class SurfaceRoughnessExtensions
  {
    /// <summary>Returns the MoWiTT forced-convection roughness multiplier R_f [-] for the category.</summary>
    /// <param name="roughness">Roughness category.</param>
    /// <returns>R_f multiplier in <c>[1.0, 2.17]</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is not a defined enum member.</exception>
    public static double GetMultiplier(this SurfaceRoughness roughness)
        => roughness switch
        {
          SurfaceRoughness.VeryRough    => 2.17,
          SurfaceRoughness.Rough        => 1.67,
          SurfaceRoughness.MediumRough  => 1.52,
          SurfaceRoughness.MediumSmooth => 1.13,
          SurfaceRoughness.Smooth       => 1.11,
          SurfaceRoughness.VerySmooth   => 1.00,
          _ => throw new ArgumentOutOfRangeException(nameof(roughness)),
        };
  }
}
