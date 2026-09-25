/* ISolarShading.cs
 *
 * Copyright (C) 2025 E.Togashi
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

using Popolo.Core.Climate;

namespace Popolo.Core.Building.Envelope
{
  /// <summary>
  /// Represents an exterior solar shading object — overhang, fin, louver, tree,
  /// neighboring building, etc. — that casts a shadow onto the surface to which
  /// it is attached and reduces the solar radiation reaching that surface.
  /// </summary>
  /// <remarks>
  /// <para>
  /// Unlike <see cref="IShadingDevice"/>, which modifies the optical properties
  /// <i>within</i> a window assembly, <see cref="ISolarShading"/> sits
  /// <b>outside</b> the envelope and is applicable to both walls and windows.
  /// Implementations report the fraction of incoming solar radiation blocked,
  /// separately for direct, sky-diffuse, and ground-reflected components.
  /// </para>
  /// <para>
  /// All methods follow the <b>shading-rate</b> convention (0 = no shading,
  /// 1 = fully blocked). The engine multiplies the unobstructed irradiance by
  /// <c>(1 - shadingRate)</c> to obtain the effective irradiance reaching the
  /// surface.
  /// </para>
  /// <para>
  /// Available implementations:
  /// <list type="bullet">
  ///   <item><description><see cref="SunShade"/> — overhang, fin, or grid louver with analytical geometry.</description></item>
  /// </list>
  /// More implementations (polygonal obstructions, Monte-Carlo sampling for complex
  /// shapes, deciduous trees) can be added by implementing this interface.
  /// </para>
  /// </remarks>
  public interface ISolarShading
  {
    /// <summary>
    /// Gets the discriminator identifying the concrete shading type.
    /// Used by serializers to distinguish implementations without reflection.
    /// </summary>
    string Kind { get; }

    /// <summary>
    /// Returns the fraction of direct solar radiation blocked by this shading
    /// object, for the given solar position and the surface to which the
    /// shading is attached.
    /// </summary>
    /// <param name="sun">Current solar state.</param>
    /// <param name="surfaceIncline">
    /// The tilted surface to which this shading object is attached.
    /// Implementations may use the surface orientation to compute the apparent
    /// solar profile angle relative to the surface plane.
    /// </param>
    /// <returns>
    /// Direct-solar shading rate [-]. <c>0</c> means no shading (full direct
    /// solar reaches the surface); <c>1</c> means the surface is fully shaded.
    /// </returns>
    double GetDirectShadingRate(IReadOnlySun sun, IReadOnlyIncline surfaceIncline);

    /// <summary>
    /// Returns the fraction of sky-diffuse solar radiation blocked by this
    /// shading object. Independent of solar position; depends only on geometry.
    /// </summary>
    /// <param name="surfaceIncline">
    /// The tilted surface to which this shading object is attached.
    /// </param>
    /// <returns>
    /// Sky-diffuse shading rate [-]. <c>0</c> means no obstruction of the
    /// surface's view of the sky; <c>1</c> means the sky is fully obstructed.
    /// </returns>
    double GetSkyDiffuseShadingRate(IReadOnlyIncline surfaceIncline);

    /// <summary>
    /// Returns the fraction of ground-reflected diffuse solar radiation blocked
    /// by this shading object.
    /// </summary>
    /// <param name="surfaceIncline">
    /// The tilted surface to which this shading object is attached.
    /// </param>
    /// <returns>
    /// Ground-diffuse shading rate [-]. <c>0</c> means no obstruction of the
    /// surface's view of the ground; <c>1</c> means the ground is fully obstructed.
    /// </returns>
    /// <remarks>
    /// The default implementation returns <c>0</c> because the majority of
    /// shading objects (overhangs, fins, louvers above the window) do not
    /// obstruct the ground view from the surface. Implementations representing
    /// ground-blocking obstructions (e.g., a wall in front of the surface)
    /// should override this method.
    /// </remarks>
    double GetGroundDiffuseShadingRate(IReadOnlyIncline surfaceIncline) => 0.0;
  }
}
