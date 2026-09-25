/* BuildingType.cs
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
  /// Building-use category used by the WEBPRO (省エネ法) energy calculation program.
  /// </summary>
  /// <remarks>
  /// In Japanese legal terminology these are 「建物用途」. The labels with 「等」suffix
  /// are the formal category names; shorter aliases (e.g. 「事務所」 without 「等」) are
  /// also accepted on read. See <c>BuildingTypeJsonConverter</c> for the string
  /// mapping.
  /// </remarks>
  public enum BuildingType
  {
    /// <summary>Office buildings (事務所等).</summary>
    Office,
    /// <summary>Hotels (ホテル等).</summary>
    Hotel,
    /// <summary>Hospitals (病院等).</summary>
    Hospital,
    /// <summary>Retail stores (物販店舗等).</summary>
    Retail,
    /// <summary>Schools (学校等).</summary>
    School,
    /// <summary>Restaurants (飲食店等).</summary>
    Restaurant,
    /// <summary>Assembly halls (集会所等).</summary>
    Hall,
    /// <summary>Factories and plants (工場等).</summary>
    Plant,
    /// <summary>Apartment houses (共同住宅).</summary>
    ApartmentHouse,
  }
}
