/* SkyDiffuseModel.cs
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

namespace Popolo.Core.Climate
{
  /// <summary>
  /// Selects how sky diffuse irradiance is projected onto a tilted surface.
  /// </summary>
  /// <remarks>
  /// Ground-reflected diffuse is always treated as isotropic regardless of
  /// the chosen sky model, following the usual convention in the Perez
  /// literature and ASHRAE practice.
  /// </remarks>
  public enum SkyDiffuseModel
  {
    /// <summary>
    /// Perez all-weather anisotropic sky model
    /// (Perez, R. et al., <em>Solar Energy</em>, Vol. 44, 1990). Accounts
    /// for circumsolar brightening and horizon brightening in addition to
    /// the isotropic dome component.
    /// </summary>
    Perez,

    /// <summary>
    /// Isotropic sky: diffuse irradiance arrives uniformly from every point
    /// on the celestial hemisphere. The tilted-surface irradiance is simply
    /// the view factor to the sky times the horizontal diffuse irradiance.
    /// </summary>
    Isotropic,
  }
}
