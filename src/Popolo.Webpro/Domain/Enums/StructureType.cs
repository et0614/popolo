/* StructureType.cs
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
  /// Structural classification of a building or wall configuration used by WEBPRO.
  /// </summary>
  public enum StructureType
  {
    /// <summary>Unspecified; used when the enclosing JSON lacks a <c>structureType</c> property.</summary>
    None,
    /// <summary>Wood construction (木造).</summary>
    Wood,
    /// <summary>Reinforced concrete construction and similar (鉄筋コンクリート造等).</summary>
    ReinforcedConcrete,
    /// <summary>Steel frame construction (鉄骨造).</summary>
    Steel,
    /// <summary>Other construction methods (その他).</summary>
    Others,
  }
}
