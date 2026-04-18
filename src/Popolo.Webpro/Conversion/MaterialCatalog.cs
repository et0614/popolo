/* MaterialCatalog.cs
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
using System.Reflection;
using System.Text.Json;

using Popolo.Core.Building.Envelope;

namespace Popolo.Webpro.Conversion
{
  /// <summary>
  /// Catalog of WEBPRO-defined wall materials, loaded from an embedded JSON
  /// resource.
  /// </summary>
  /// <remarks>
  /// <para>
  /// Supplies <see cref="WallLayer"/> and <see cref="AirGapLayer"/> instances
  /// for each material ID used in WEBPRO input files (e.g. 「コンクリート」,
  /// 「非密閉中空層」, 「土壌」). Replaces the ~80-case <c>switch</c> statement
  /// found in the legacy Popolo v2.3 <c>WebproSingleWallConfigureJson.MakeWallLayer</c>
  /// with a data-driven approach backed by <c>Resources/Materials.json</c>.
  /// </para>
  /// <para>
  /// The catalog distinguishes three material types:
  /// </para>
  /// <list type="bullet">
  ///   <item><description>
  ///     <c>solid</c> — Ordinary building material (e.g. concrete, gypsum board).
  ///     Uses the thickness supplied by the caller (converted from mm to m).
  ///   </description></item>
  ///   <item><description>
  ///     <c>airGap</c> — Air gap layer (sealed or unsealed). Uses a fixed 20 mm
  ///     thickness; the caller-supplied thickness is ignored.
  ///   </description></item>
  ///   <item><description>
  ///     <c>soil</c> — Ground soil layer, modeled as a negligibly thin layer
  ///     (0.1 mm) because the soil thermal resistance is accounted for separately
  ///     via <c>SetGroundWall</c>.
  ///   </description></item>
  /// </list>
  /// <para>
  /// The catalog is loaded once and cached via a private <see cref="Lazy{T}"/>,
  /// so <see cref="Default"/> has constant-time access after initialization.
  /// Loading from a custom JSON file is available via <see cref="LoadFrom"/>
  /// for testing purposes.
  /// </para>
  /// </remarks>
  public sealed class MaterialCatalog
  {

    #region 定数

    /// <summary>Embedded resource name for the default Materials.json.</summary>
    private const string DefaultResourceName = "Popolo.Webpro.Resources.Materials.json";

    #endregion

    #region 内部データ

    /// <summary>Discriminator for material entry types.</summary>
    private enum MaterialType
    {
      Solid,
      AirGap,
      Soil,
    }

    /// <summary>Internal catalog entry holding all fields for every material type.</summary>
    /// <remarks>
    /// Only fields relevant to the entry's <see cref="Type"/> are meaningful;
    /// unused fields are zero / default.
    /// </remarks>
    private readonly struct Entry
    {
      public MaterialType Type { get; }
      public string Id { get; }
      public double ThermalConductivity { get; }
      public double VolumetricSpecificHeat { get; }
      public bool IsSealed { get; }
      public double FixedThickness { get; }

      public Entry(
        MaterialType type, string id,
        double thermalConductivity, double volumetricSpecificHeat,
        bool isSealed, double fixedThickness)
      {
        Type = type;
        Id = id;
        ThermalConductivity = thermalConductivity;
        VolumetricSpecificHeat = volumetricSpecificHeat;
        IsSealed = isSealed;
        FixedThickness = fixedThickness;
      }
    }

    private readonly Dictionary<string, Entry> entries;

    #endregion

    #region インスタンス構築

    /// <summary>Initializes a catalog with the provided entries.</summary>
    private MaterialCatalog(Dictionary<string, Entry> entries)
    {
      this.entries = entries;
    }

    /// <summary>Gets the default catalog backed by the embedded Materials.json.</summary>
    /// <remarks>The catalog is loaded lazily on first access and cached for the process lifetime.</remarks>
    public static MaterialCatalog Default => DefaultLazy.Value;

    private static readonly Lazy<MaterialCatalog> DefaultLazy =
      new Lazy<MaterialCatalog>(LoadFromEmbeddedResource);

    /// <summary>Loads a catalog from a JSON stream (primarily for testing).</summary>
    /// <param name="jsonStream">Stream containing the Materials.json content.</param>
    /// <returns>A new catalog instance.</returns>
    public static MaterialCatalog LoadFrom(Stream jsonStream)
    {
      if (jsonStream is null) throw new ArgumentNullException(nameof(jsonStream));
      return new MaterialCatalog(ParseEntries(jsonStream));
    }

    /// <summary>Loads a catalog from a JSON string (primarily for testing).</summary>
    /// <param name="json">String containing the Materials.json content.</param>
    public static MaterialCatalog LoadFromString(string json)
    {
      if (json is null) throw new ArgumentNullException(nameof(json));
      using var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
      return LoadFrom(ms);
    }

    private static MaterialCatalog LoadFromEmbeddedResource()
    {
      var asm = typeof(MaterialCatalog).Assembly;
      using var stream = asm.GetManifestResourceStream(DefaultResourceName)
        ?? throw new InvalidOperationException(
          $"Embedded resource '{DefaultResourceName}' not found. " +
          $"Ensure Materials.json is included in the project as EmbeddedResource.");
      return new MaterialCatalog(ParseEntries(stream));
    }

    private static Dictionary<string, Entry> ParseEntries(Stream stream)
    {
      using var doc = JsonDocument.Parse(stream);
      var root = doc.RootElement;
      if (!root.TryGetProperty("materials", out var mats) || mats.ValueKind != JsonValueKind.Array)
        throw new InvalidOperationException(
          "Materials JSON must have a top-level 'materials' array.");

      var dict = new Dictionary<string, Entry>(StringComparer.Ordinal);
      foreach (var elem in mats.EnumerateArray())
      {
        string id = elem.GetProperty("id").GetString()
          ?? throw new InvalidOperationException("Material entry is missing 'id'.");
        string typeStr = elem.GetProperty("type").GetString()
          ?? throw new InvalidOperationException($"Material '{id}' is missing 'type'.");

        Entry entry = typeStr switch
        {
          "solid" => new Entry(
            MaterialType.Solid, id,
            elem.GetProperty("thermalConductivity").GetDouble(),
            elem.GetProperty("volumetricSpecificHeat").GetDouble(),
            false, 0),
          "airGap" => new Entry(
            MaterialType.AirGap, id,
            0, 0,
            elem.GetProperty("isSealed").GetBoolean(),
            elem.GetProperty("fixedThickness").GetDouble()),
          "soil" => new Entry(
            MaterialType.Soil, id,
            elem.GetProperty("thermalConductivity").GetDouble(),
            elem.GetProperty("volumetricSpecificHeat").GetDouble(),
            false,
            elem.GetProperty("fixedThickness").GetDouble()),
          _ => throw new InvalidOperationException(
            $"Unknown material type '{typeStr}' for material '{id}'."),
        };

        if (dict.ContainsKey(id))
          throw new InvalidOperationException($"Duplicate material ID '{id}'.");
        dict[id] = entry;
      }

      return dict;
    }

    #endregion

    #region 公開 API

    /// <summary>Gets the number of materials in the catalog.</summary>
    public int Count => entries.Count;

    /// <summary>Returns whether the catalog contains a material with the given ID.</summary>
    public bool Contains(string materialId) => entries.ContainsKey(materialId);

    /// <summary>
    /// Creates a <see cref="WallLayer"/> or <see cref="AirGapLayer"/> for the
    /// given material ID and thickness.
    /// </summary>
    /// <param name="materialId">
    /// Material identifier as used in WEBPRO input (e.g. 「コンクリート」,
    /// 「非密閉中空層」, 「土壌」).
    /// </param>
    /// <param name="thicknessMm">
    /// Layer thickness in millimetres, matching the unit used in WEBPRO JSON.
    /// May be <c>null</c>: for air-gap and soil materials the value is ignored
    /// (a catalog-defined fixed thickness is used); for solid materials, a null
    /// thickness is treated as 0 mm which produces a layer with effectively zero
    /// thermal resistance. Thickness is internally converted to metres.
    /// </param>
    /// <returns>A new wall layer instance. Caller takes ownership.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="materialId"/> is null.</exception>
    /// <exception cref="KeyNotFoundException">The material ID is not in the catalog.</exception>
    public WallLayer MakeWallLayer(string materialId, double? thicknessMm)
    {
      if (materialId is null) throw new ArgumentNullException(nameof(materialId));
      if (!entries.TryGetValue(materialId, out var entry))
        throw new KeyNotFoundException(
          $"Material ID '{materialId}' is not in the catalog. " +
          $"Check Resources/Materials.json.");

      return entry.Type switch
      {
        MaterialType.Solid => new WallLayer(
          entry.Id,
          entry.ThermalConductivity,
          entry.VolumetricSpecificHeat,
          MmToMeters(thicknessMm ?? 0)),
        MaterialType.AirGap => new AirGapLayer(
          entry.Id,
          entry.IsSealed,
          entry.FixedThickness),
        MaterialType.Soil => new WallLayer(
          entry.Id,
          entry.ThermalConductivity,
          entry.VolumetricSpecificHeat,
          entry.FixedThickness),
        _ => throw new InvalidOperationException(
          $"Internal error: unhandled material type {entry.Type}."),
      };
    }

    /// <summary>Returns an enumerable of all material IDs in the catalog.</summary>
    public IEnumerable<string> Ids => entries.Keys;

    #endregion

    #region ヘルパー

    private static double MmToMeters(double mm) => mm * 0.001;

    #endregion
  }
}
