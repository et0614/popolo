/* WebproBuildingJsonConverter.cs
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

namespace Popolo.Webpro.Json
{
  /// <summary>
  /// JSON converter for <see cref="WebproBuilding"/> — the top-level
  /// <c>Building</c> block of a WEBPRO input file.
  /// </summary>
  /// <remarks>
  /// <para>
  /// Expected JSON shape:
  /// </para>
  /// <code>
  /// {
  ///   "Name":               "サンプル事務所ビル",
  ///   "Region":             "6",
  ///   "AnnualSolarRegion":  "A3",
  ///   "BuildingFloorArea":  10352.79,
  ///   "BuildingAddress":    { ... },   // skipped
  ///   "Coefficient_DHC":    { ... }    // skipped
  /// }
  /// </code>
  /// <para>
  /// <b>Read only.</b> Writing throws <see cref="NotSupportedException"/>.
  /// </para>
  /// <para>
  /// <b>Required:</b> <c>Region</c>. Missing or null throws <see cref="JsonException"/>.
  /// </para>
  /// <para>
  /// <b>Intentionally skipped:</b> <c>BuildingAddress</c> and
  /// <c>Coefficient_DHC</c> are used by the WEBPRO HVAC/DHW subsystems and are
  /// not relevant to thermal load calculation.
  /// </para>
  /// </remarks>
  public sealed class WebproBuildingJsonConverter : JsonConverter<WebproBuilding>
  {

    #region 定数

    private const string PropName = "Name";
    private const string PropRegion = "Region";
    private const string PropAnnualSolarRegion = "AnnualSolarRegion";
    private const string PropBuildingFloorArea = "BuildingFloorArea";

    #endregion

    #region JsonConverter 実装

    public override WebproBuilding Read(
      ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
      if (reader.TokenType != JsonTokenType.StartObject)
        throw new JsonException(
          $"Expected StartObject for {nameof(WebproBuilding)}, but got {reader.TokenType}.");

      string? region = null;
      var result = new WebproBuilding();

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
          case PropName:
            result.Name = reader.TokenType == JsonTokenType.Null ? null : reader.GetString();
            break;
          case PropRegion:
            if (reader.TokenType == JsonTokenType.Null)
              throw new JsonException(
                $"'{PropRegion}' must not be null on {nameof(WebproBuilding)}.");
            if (reader.TokenType != JsonTokenType.String)
              throw new JsonException(
                $"'{PropRegion}' must be a string, but got {reader.TokenType}.");
            region = reader.GetString();
            break;
          case PropAnnualSolarRegion:
            result.AnnualSolarRegion = reader.TokenType == JsonTokenType.Null ? null : reader.GetString();
            break;
          case PropBuildingFloorArea:
            result.FloorArea = reader.TokenType == JsonTokenType.Null
              ? (double?)null
              : reader.GetDouble();
            break;
          default:
            // BuildingAddress, Coefficient_DHC, and any unknown properties.
            reader.Skip();
            break;
        }
      }

      if (region is null)
        throw new JsonException(
          $"Required property '{PropRegion}' is missing from {nameof(WebproBuilding)} JSON.");

      result.Region = region;
      return result;
    }

    /// <summary>Write is not supported — WEBPRO integration is import-only.</summary>
    public override void Write(
      Utf8JsonWriter writer, WebproBuilding value, JsonSerializerOptions options)
    {
      throw new NotSupportedException(
        $"Writing {nameof(WebproBuilding)} to JSON is not supported; " +
        $"use the Popolo native JSON schema for outbound serialization.");
    }

    #endregion
  }
}
