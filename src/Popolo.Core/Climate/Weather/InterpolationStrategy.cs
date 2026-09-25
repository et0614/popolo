/* InterpolationStrategy.cs
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

namespace Popolo.Core.Climate.Weather
{
  /// <summary>
  /// Interpolation scheme used by <see cref="WeatherInterpolator"/>
  /// between recorded observation points.
  /// </summary>
  public enum InterpolationStrategy
  {
    /// <summary>
    /// Piecewise linear interpolation. Suitable for smoothly varying
    /// quantities such as dry-bulb temperature and humidity ratio.
    /// </summary>
    Linear = 0,

    /// <summary>
    /// Piecewise Cubic Hermite Interpolating Polynomial (PCHIP). Monotonicity
    /// preserving; suitable for non-negative quantities with abrupt changes
    /// such as solar radiation, to avoid negative overshoot.
    /// </summary>
    Pchip = 1,

    /// <summary>
    /// Step-hold: the value at time <c>t</c> is the value of the most recent
    /// observation at or before <c>t</c>. Suitable for quantities that are
    /// accumulated or reported over intervals (e.g. precipitation).
    /// </summary>
    StepHold = 2,
  }
}
