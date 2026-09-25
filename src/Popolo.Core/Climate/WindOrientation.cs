/* WindOrientation.cs
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
  /// Orientation of an exterior surface relative to the local wind vector,
  /// used to select the appropriate forced-convection correlation in the
  /// outdoor convective heat transfer coefficient calculations
  /// (<c>Popolo.Core.Building.Envelope.ExteriorConvection</c>).
  /// </summary>
  /// <remarks>
  /// The two regimes correspond to fundamentally different boundary-layer
  /// flow patterns (impingement vs. wake/recirculation) and use distinct
  /// fitted constants in the MoWiTT correlation. The discrete switch
  /// reflects that the underlying separation-driven physics is not well
  /// represented by continuous interpolation across the surface-normal /
  /// wind angle.
  /// </remarks>
  public enum WindOrientation
  {
    /// <summary>
    /// Wind blows toward the surface (component of wind velocity along the
    /// outward surface normal is positive). Higher convective coefficient.
    /// </summary>
    Windward,

    /// <summary>
    /// Wind blows away from the surface (surface lies in the wake / leeward
    /// recirculation zone). Lower convective coefficient.
    /// </summary>
    Leeward,
  }
}
