/* Orientation.cs
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

namespace Popolo.Webpro.Domain.Enums
{
  /// <summary>
  /// Surface orientation used by WEBPRO for wall and window directions.
  /// </summary>
  /// <remarks>
  /// The eight cardinal / intercardinal directions cover vertical surfaces,
  /// <see cref="UpperHorizontal"/> and <see cref="LowerHorizontal"/> represent
  /// roofs and floors respectively, and the remaining two values
  /// (<see cref="Shade"/>, <see cref="Horizontal"/>) appear in older
  /// WEBPRO versions.
  /// </remarks>
  public enum Orientation
  {
    /// <summary>North (北).</summary>
    N,
    /// <summary>Northwest (北西).</summary>
    NW,
    /// <summary>West (西).</summary>
    W,
    /// <summary>Southwest (南西).</summary>
    SW,
    /// <summary>South (南).</summary>
    S,
    /// <summary>Southeast (南東).</summary>
    SE,
    /// <summary>East (東).</summary>
    E,
    /// <summary>Northeast (北東).</summary>
    NE,
    /// <summary>Upper horizontal surface such as a roof (水平（上）).</summary>
    UpperHorizontal,
    /// <summary>Lower horizontal surface such as a floor (水平（下）).</summary>
    LowerHorizontal,
    /// <summary>Shaded surface (日陰); legacy value from older WEBPRO versions.</summary>
    Shade,
    /// <summary>Horizontal surface (水平); legacy value from older WEBPRO versions.</summary>
    Horizontal,
  }
}
