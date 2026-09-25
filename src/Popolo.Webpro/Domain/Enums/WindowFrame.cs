/* WindowFrame.cs
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
  /// Window frame material classification used by WEBPRO.
  /// </summary>
  public enum WindowFrame
  {
    /// <summary>Unspecified; used when the enclosing JSON lacks a <c>frameType</c> property.</summary>
    None,
    /// <summary>Resin frame (樹脂製).</summary>
    Resin,
    /// <summary>Wood frame (木製).</summary>
    Wood,
    /// <summary>Metal frame (金属製).</summary>
    Metal,
    /// <summary>Composite metal-resin frame (金属樹脂複合製).</summary>
    MetalAndResin,
    /// <summary>Composite metal-wood frame (金属木複合製).</summary>
    MetalAndWood,
  }
}
