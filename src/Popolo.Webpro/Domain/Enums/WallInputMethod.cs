/* WallInputMethod.cs
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
  /// Method by which the thermal performance of a wall construction is
  /// specified in WEBPRO input data.
  /// </summary>
  public enum WallInputMethod
  {
    /// <summary>Unspecified; used when the enclosing JSON lacks an <c>inputMethod</c> property.</summary>
    None,
    /// <summary>Heat transfer coefficient (U-value) is given directly (熱貫流率を入力).</summary>
    HeatTransferCoefficient,
    /// <summary>Each layer's material ID and thickness are listed (建材構成を入力).</summary>
    MaterialNumberAndThickness,
    /// <summary>Insulation type is selected from a predefined set (断熱材種類を入力).</summary>
    InsulationType,
  }
}
