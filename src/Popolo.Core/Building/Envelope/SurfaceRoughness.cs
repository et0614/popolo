/* SurfaceRoughness.cs
 *
 * Copyright (C) 2026 E.Togashi
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

namespace Popolo.Core.Building.Envelope
{
  /// <summary>
  /// Categorical surface roughness used to scale the forced-convection term of
  /// the windward MoWiTT exterior film coefficient correlation.
  /// </summary>
  /// <remarks>
  /// <para>
  /// The numeric multiplier <c>R_f</c> is applied to the forced-convection
  /// component of the MoWiTT correlation
  /// (<see cref="Popolo.Core.Climate.Sky.GetExteriorConvectiveCoefficient(double, double, double)"/>):
  /// surfaces rougher than smooth glass enhance the wind-driven heat transfer.
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
