/* WebproModel.cs
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

using System.Collections.Generic;

namespace Popolo.Webpro.Domain
{
  /// <summary>
  /// Top-level data transfer object representing the thermal-calculation
  /// content of a WEBPRO input JSON file.
  /// </summary>
  /// <remarks>
  /// <para>
  /// This DTO captures only the sections of a WEBPRO file that are used for
  /// building thermal load calculation. WEBPRO files contain many additional
  /// sections for HVAC equipment, lighting, domestic hot water, elevators,
  /// photovoltaics, cogeneration, and similar subsystems; all of those are
  /// intentionally ignored by the corresponding converter.
  /// </para>
  /// <para>
  /// Sections carried on the model:
  /// </para>
  /// <list type="table">
  ///   <listheader><term>Property</term><description>WEBPRO JSON key</description></listheader>
  ///   <item>
  ///     <term><see cref="Building"/></term>
  ///     <description><c>Building</c> (single object; required)</description>
  ///   </item>
  ///   <item>
  ///     <term><see cref="Rooms"/></term>
  ///     <description><c>Rooms</c> (keyed by room name)</description>
  ///   </item>
  ///   <item>
  ///     <term><see cref="Envelopes"/></term>
  ///     <description><c>EnvelopeSet</c> (keyed by room name; sparse)</description>
  ///   </item>
  ///   <item>
  ///     <term><see cref="WallConfigurations"/></term>
  ///     <description><c>WallConfigure</c> (keyed by wall spec ID)</description>
  ///   </item>
  ///   <item>
  ///     <term><see cref="WindowConfigurations"/></term>
  ///     <description><c>WindowConfigure</c> (keyed by window spec ID)</description>
  ///   </item>
  ///   <item>
  ///     <term><see cref="AirConditionedRoomNames"/></term>
  ///     <description><c>AirConditioningZone</c> (keys only; values ignored)</description>
  ///   </item>
  /// </list>
  /// </remarks>
  public sealed class WebproModel
  {
    /// <summary>Gets or sets the building-wide information (region, name, etc.).</summary>
    /// <remarks>Required. The corresponding JSON section is <c>Building</c>.</remarks>
    public WebproBuilding Building { get; set; } = new WebproBuilding();

    /// <summary>Gets the dictionary of rooms keyed by room name.</summary>
    /// <remarks>
    /// Keys are the free-form Japanese names used in WEBPRO (e.g.
    /// <c>"1F_ロビー"</c>). Values are the room metadata from the <c>Rooms</c>
    /// section.
    /// </remarks>
    public Dictionary<string, WebproRoom> Rooms { get; } = new Dictionary<string, WebproRoom>();

    /// <summary>Gets the dictionary of envelope sets keyed by room name.</summary>
    /// <remarks>
    /// Only rooms with external / ground walls appear here. Keys match those of
    /// <see cref="Rooms"/>. Corresponds to the <c>EnvelopeSet</c> JSON section.
    /// </remarks>
    public Dictionary<string, WebproEnvelopeSet> Envelopes { get; }
      = new Dictionary<string, WebproEnvelopeSet>();

    /// <summary>Gets the dictionary of named wall constructions, keyed by spec ID.</summary>
    /// <remarks>
    /// Each wall specification (e.g. <c>"W1"</c>, <c>"FG1"</c>) is referenced from
    /// <see cref="WebproWall.WallSpec"/> of each <see cref="WebproWall"/>.
    /// Corresponds to the <c>WallConfigure</c> JSON section.
    /// </remarks>
    public Dictionary<string, WebproWallConfiguration> WallConfigurations { get; }
      = new Dictionary<string, WebproWallConfiguration>();

    /// <summary>Gets the dictionary of named window specifications, keyed by spec ID.</summary>
    /// <remarks>
    /// Each window specification (e.g. <c>"G1"</c>) is referenced from
    /// <see cref="WebproWindow.ID"/> of each <see cref="WebproWindow"/>.
    /// Corresponds to the <c>WindowConfigure</c> JSON section.
    /// </remarks>
    public Dictionary<string, WebproWindowConfiguration> WindowConfigurations { get; }
      = new Dictionary<string, WebproWindowConfiguration>();

    /// <summary>Gets the set of room names that are air-conditioned.</summary>
    /// <remarks>
    /// Derived from the keys of the WEBPRO <c>AirConditioningZone</c> section.
    /// The AHU/plant information carried by each value in that section (inside
    /// and outdoor load assignments, etc.) is not relevant to thermal load
    /// calculation and is discarded.
    /// </remarks>
    public HashSet<string> AirConditionedRoomNames { get; } = new HashSet<string>();
  }
}
