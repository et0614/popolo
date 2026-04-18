/* RoomTypeMapper.cs
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

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

using Popolo.Webpro.Domain;
using Popolo.Webpro.Domain.Enums;

namespace Popolo.Webpro.Conversion
{
  /// <summary>
  /// Maps a WEBPRO <c>(BuildingType, roomType)</c> pair to a
  /// <see cref="WebproHeatGainScheduler.RoomType"/> enum value.
  /// </summary>
  /// <remarks>
  /// <para>
  /// The mapping is loaded from an embedded JSON resource
  /// (<c>Resources/RoomTypeMapping.json</c>) and contains 265 entries covering
  /// all 147 scheduler room types used by Popolo v2.3. The data replaces
  /// the ~450-line nested <c>switch</c> statement in the legacy
  /// <c>WebproRoomsJson.MakeWebproHeatGain</c>.
  /// </para>
  /// <para>
  /// Use <see cref="Default"/> for the production catalog, or
  /// <see cref="LoadFromString"/> / <see cref="LoadFrom"/> to supply
  /// custom mappings for testing.
  /// </para>
  /// </remarks>
  public sealed class RoomTypeMapper
  {

    #region 定数

    private const string DefaultResourceName = "Popolo.Webpro.Resources.RoomTypeMapping.json";

    #endregion

    #region 内部データ

    /// <summary>Composite key used by the mapping dictionary.</summary>
    private readonly struct Key : IEquatable<Key>
    {
      public BuildingType BuildingType { get; }
      public string RoomType { get; }

      public Key(BuildingType bt, string rt)
      {
        BuildingType = bt;
        RoomType = rt;
      }

      public bool Equals(Key other)
        => BuildingType == other.BuildingType
           && string.Equals(RoomType, other.RoomType, StringComparison.Ordinal);

      public override bool Equals(object? obj) => obj is Key k && Equals(k);
      public override int GetHashCode() => HashCode.Combine((int)BuildingType, RoomType);
    }

    private readonly Dictionary<Key, WebproHeatGainScheduler.RoomType> entries;

    #endregion

    #region インスタンス構築

    private RoomTypeMapper(Dictionary<Key, WebproHeatGainScheduler.RoomType> entries)
    {
      this.entries = entries;
    }

    /// <summary>Gets the default mapper backed by the embedded RoomTypeMapping.json.</summary>
    public static RoomTypeMapper Default => DefaultLazy.Value;

    private static readonly Lazy<RoomTypeMapper> DefaultLazy =
      new Lazy<RoomTypeMapper>(LoadFromEmbeddedResource);

    /// <summary>Loads a mapper from a JSON stream (primarily for testing).</summary>
    public static RoomTypeMapper LoadFrom(Stream jsonStream)
    {
      if (jsonStream is null) throw new ArgumentNullException(nameof(jsonStream));
      return new RoomTypeMapper(ParseEntries(jsonStream));
    }

    /// <summary>Loads a mapper from a JSON string (primarily for testing).</summary>
    public static RoomTypeMapper LoadFromString(string json)
    {
      if (json is null) throw new ArgumentNullException(nameof(json));
      using var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
      return LoadFrom(ms);
    }

    private static RoomTypeMapper LoadFromEmbeddedResource()
    {
      var asm = typeof(RoomTypeMapper).Assembly;
      using var stream = asm.GetManifestResourceStream(DefaultResourceName)
        ?? throw new InvalidOperationException(
          $"Embedded resource '{DefaultResourceName}' not found. " +
          $"Ensure RoomTypeMapping.json is included in the project as EmbeddedResource.");
      return new RoomTypeMapper(ParseEntries(stream));
    }

    private static Dictionary<Key, WebproHeatGainScheduler.RoomType> ParseEntries(Stream stream)
    {
      using var doc = JsonDocument.Parse(stream);
      var root = doc.RootElement;
      if (!root.TryGetProperty("mappings", out var mappings) || mappings.ValueKind != JsonValueKind.Array)
        throw new InvalidOperationException(
          "Room-type mapping JSON must have a top-level 'mappings' array.");

      var dict = new Dictionary<Key, WebproHeatGainScheduler.RoomType>();
      foreach (var elem in mappings.EnumerateArray())
      {
        string btStr = elem.GetProperty("buildingType").GetString()
          ?? throw new InvalidOperationException("Mapping entry is missing 'buildingType'.");
        string rtStr = elem.GetProperty("roomType").GetString()
          ?? throw new InvalidOperationException("Mapping entry is missing 'roomType'.");
        string schStr = elem.GetProperty("schedulerRoomType").GetString()
          ?? throw new InvalidOperationException("Mapping entry is missing 'schedulerRoomType'.");

        if (!Enum.TryParse<BuildingType>(btStr, out var bt))
          throw new InvalidOperationException(
            $"'{btStr}' is not a valid BuildingType.");
        if (!Enum.TryParse<WebproHeatGainScheduler.RoomType>(schStr, out var sch))
          throw new InvalidOperationException(
            $"'{schStr}' is not a valid WebproHeatGainScheduler.RoomType.");

        var key = new Key(bt, rtStr);
        if (dict.ContainsKey(key))
          throw new InvalidOperationException(
            $"Duplicate mapping key (BuildingType={btStr}, roomType='{rtStr}').");
        dict[key] = sch;
      }

      return dict;
    }

    #endregion

    #region 公開 API

    /// <summary>Gets the number of mapping entries.</summary>
    public int Count => entries.Count;

    /// <summary>
    /// Attempts to resolve a <c>(BuildingType, roomType)</c> pair to its
    /// <see cref="WebproHeatGainScheduler.RoomType"/>.
    /// </summary>
    /// <param name="buildingType">Building-use category.</param>
    /// <param name="roomType">Room sub-category (Japanese string as supplied by WEBPRO).</param>
    /// <param name="schedulerRoomType">When the method returns true, contains the resolved enum value.</param>
    /// <returns><c>true</c> if a mapping exists; otherwise <c>false</c>.</returns>
    public bool TryGet(
      BuildingType buildingType, string roomType,
      out WebproHeatGainScheduler.RoomType schedulerRoomType)
    {
      if (roomType is null) throw new ArgumentNullException(nameof(roomType));
      return entries.TryGetValue(new Key(buildingType, roomType), out schedulerRoomType);
    }

    /// <summary>
    /// Resolves a <c>(BuildingType, roomType)</c> pair to its
    /// <see cref="WebproHeatGainScheduler.RoomType"/>, throwing if no
    /// mapping exists.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="roomType"/> is null.</exception>
    /// <exception cref="KeyNotFoundException">No mapping exists for the pair.</exception>
    public WebproHeatGainScheduler.RoomType Get(
      BuildingType buildingType, string roomType)
    {
      if (TryGet(buildingType, roomType, out var sch)) return sch;
      throw new KeyNotFoundException(
        $"No room-type mapping for (BuildingType={buildingType}, roomType='{roomType}').");
    }

    #endregion

  }
}
