/* WindowInputMethod.cs
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
  /// Method by which the thermal performance of a window is specified in WEBPRO
  /// input data.
  /// </summary>
  public enum WindowInputMethod
  {
    /// <summary>Unspecified; used when the enclosing JSON lacks an <c>inputMethod</c> property.</summary>
    None,
    /// <summary>The window's overall U-value and solar heat gain rate are given (性能値を入力).</summary>
    WindowSpec,
    /// <summary>The frame type is chosen and the glazing's U-value and solar heat gain rate are given (ガラスの性能を入力).</summary>
    FrameTypeAndGlazingSpec,
    /// <summary>Both the frame type and the glazing type are chosen from a predefined catalog (ガラスの種類を入力).</summary>
    FrameAndGlazingType,
  }
}
