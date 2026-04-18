/* WebproRoomJsonConverter.cs
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
using System.Text.Json;
using System.Text.Json.Serialization;

using Popolo.Webpro.Domain;
using Popolo.Webpro.Domain.Enums;

namespace Popolo.Webpro.Json
{
  /// <summary>
  /// JSON converter for <see cref="WebproRoom"/> — a single entry within the
  /// WEBPRO <c>Rooms</c> dictionary.
  /// </summary>
  /// <remarks>
  /// <para>
  /// Expected JSON shape:
  /// </para>
  /// <code>
  /// {
  ///   "buildingType":      "事務所等",
  ///   "roomType":          "執務室",
  ///   "floorHeight":       3.8,
  ///   "ceilingHeight":     2.7,
  ///   "roomArea":          250.0,
  ///   "mainbuildingType":  "事務所等",    // skipped
  ///   "zone":              null,          // skipped
  ///   "modelBuildingType": "事務所モデル", // skipped
  ///   "buildingGroup":     null,          // skipped
  ///   "Info":              null
  /// }
  /// </code>
  /// <para>
  /// <b>Read only.</b> Writing throws <see cref="NotSupportedException"/>.
  /// </para>
  /// <para>
  /// <b>Required:</b> <c>buildingType</c>, <c>roomType</c>,
  /// <c>floorHeight</c>, <c>ceilingHeight</c>, <c>roomArea</c>. Missing any
  /// of these throws <see cref="JsonException"/>.
  /// </para>
  /// <para>
  /// <b>Intentionally skipped:</b> <c>mainbuildingType</c>, <c>zone</c>,
  /// <c>modelBuildingType</c>, <c>buildingGroup</c>. These are used by the
  /// WEBPRO energy / equipment subsystems but are not needed for thermal
  /// load calculation. Unknown properties are also skipped for forward
  /// compatibility.
  /// </para>
  /// </remarks>
  public sealed class WebproRoomJsonConverter : JsonConverter<WebproRoom>
  {

    #region 定数

    private const string PropBuildingType = "buildingType";
    private const string PropRoomType = "roomType";
    private const string PropFloorHeight = "floorHeight";
    private const string PropCeilingHeight = "ceilingHeight";
    private const string PropRoomArea = "roomArea";
    private const string PropInfo = "Info";

    #endregion

    #region JsonConverter 実装

    public override WebproRoom Read(
      ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
      if (reader.TokenType != JsonTokenType.StartObject)
        throw new JsonException(
          $"Expected StartObject for {nameof(WebproRoom)}, but got {reader.TokenType}.");

      BuildingType? buildingType = null;
      string? roomType = null;
      double? floorHeight = null;
      double? ceilingHeight = null;
      double? roomArea = null;
      string? info = null;

      while (reader.Read())
      {
        if (reader.TokenType == JsonTokenType.EndObject) break;
        if (reader.TokenType != JsonTokenType.PropertyName)
          throw new JsonException($"Expected PropertyName, but got {reader.TokenType}.");

        string? propName = reader.GetString();
        if (!reader.Read())
          throw new JsonException($"Unexpected end of JSON while reading '{propName}'.");

        switch (propName)
        {
          case PropBuildingType:
            buildingType = JsonSerializer.Deserialize<BuildingType>(ref reader, options);
            break;
          case PropRoomType:
            if (reader.TokenType != JsonTokenType.String)
              throw new JsonException(
                $"'{PropRoomType}' must be a string, but got {reader.TokenType}.");
            roomType = reader.GetString();
            break;
          case PropFloorHeight: floorHeight = reader.GetDouble(); break;
          case PropCeilingHeight: ceilingHeight = reader.GetDouble(); break;
          case PropRoomArea: roomArea = reader.GetDouble(); break;
          case PropInfo:
            info = reader.TokenType == JsonTokenType.Null ? null : reader.GetString();
            break;
          default:
            // mainbuildingType, zone, modelBuildingType, buildingGroup, and any
            // unknown properties are silently skipped.
            reader.Skip();
            break;
        }
      }

      if (buildingType is null)
        throw new JsonException($"Required property '{PropBuildingType}' is missing.");
      if (roomType is null)
        throw new JsonException($"Required property '{PropRoomType}' is missing.");
      if (floorHeight is null)
        throw new JsonException($"Required property '{PropFloorHeight}' is missing.");
      if (ceilingHeight is null)
        throw new JsonException($"Required property '{PropCeilingHeight}' is missing.");
      if (roomArea is null)
        throw new JsonException($"Required property '{PropRoomArea}' is missing.");

      return new WebproRoom
      {
        BuildingType = buildingType.Value,
        RoomType = roomType,
        FloorHeight = floorHeight.Value,
        CeilingHeight = ceilingHeight.Value,
        RoomArea = roomArea.Value,
        Information = info,
      };
    }

    /// <summary>Write is not supported — WEBPRO integration is import-only.</summary>
    public override void Write(
      Utf8JsonWriter writer, WebproRoom value, JsonSerializerOptions options)
    {
      throw new NotSupportedException(
        $"Writing {nameof(WebproRoom)} to JSON is not supported; " +
        $"use the Popolo native JSON schema for outbound serialization.");
    }

    #endregion
  }
}
