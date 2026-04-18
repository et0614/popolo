/* InterpolationStrategy.cs
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
