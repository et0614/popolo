/* WallType.cs
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
  /// Wall type classification used by WEBPRO to determine the boundary-condition
  /// treatment of a wall surface.
  /// </summary>
  public enum WallType
  {
    /// <summary>Sun-exposed external wall (日の当たる外壁).</summary>
    ExternalWall,
    /// <summary>Shaded external wall (日の当たらない外壁).</summary>
    ShadingExternalWall,
    /// <summary>Ground-contact external wall (地盤に接する外壁).</summary>
    GroundWall,
    /// <summary>Internal wall between zones (内壁).</summary>
    InnerWall,
  }
}
