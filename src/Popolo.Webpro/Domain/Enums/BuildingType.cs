/* BuildingType.cs
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
