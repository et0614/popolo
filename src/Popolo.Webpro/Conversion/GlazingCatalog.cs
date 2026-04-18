/* GlazingCatalog.cs
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

namespace Popolo.Webpro.Conversion
{
  /// <summary>
  /// Catalog of WEBPRO-defined glazing types, loaded from an embedded JSON
  /// resource.
  /// </summary>
  /// <remarks>
  /// <para>
  /// Each glazing entry provides the overall solar heat gain rate (η-value, tau)
  /// and the center-of-glazing heat transfer coefficient (U-value, htCoef) for
  /// a predefined glazing ID such as <c>"3WgG06"</c>, <c>"2FA10"</c>, <c>"T"</c>
  /// or <c>"S"</c>.
  /// </para>
  /// <para>
  /// The ID encodes the glazing construction in a compact WEBPRO-internal code
  /// (pane count, coating type, gas fill, etc.) that is opaque to the catalog;
  /// the catalog simply resolves the ID to its two performance numbers and
  /// defers interpretation to the caller.
  /// </para>
  /// <para>
  /// Replaces the ~156-case <c>switch</c> statement found in the legacy
  /// Popolo v2.3 <c>WebproWindowJson.MakeWindow</c> with a data-driven
  /// approach backed by <c>Resources/Glazings.json</c>.
  /// </para>
  /// </remarks>
  public sealed class GlazingCatalog
  {

    #region 定数

    /// <summary>Embedded resource name for the default Glazings.json.</summary>
    private const string DefaultResourceName = "Popolo.Webpro.Resources.Glazings.json";

    #endregion

    #region 内部データ

    /// <summary>Glazing performance pair (tau, htCoef).</summary>
    public readonly struct GlazingPerformance
    {
      /// <summary>Gets the overall solar heat gain rate (η-value) of the glazing [-].</summary>
      public double SolarHeatGain { get; }

      /// <summary>Gets the center-of-glazing heat transfer coefficient (U-value) [W/(m²·K)].</summary>
      public double HeatTransferCoefficient { get; }

      /// <summary>Initializes a new glazing performance entry.</summary>
      public GlazingPerformance(double solarHeatGain, double heatTransferCoefficient)
      {
        SolarHeatGain = solarHeatGain;
        HeatTransferCoefficient = heatTransferCoefficient;
      }
    }

    private readonly Dictionary<string, GlazingPerformance> entries;

    #endregion

    #region インスタンス構築

    private GlazingCatalog(Dictionary<string, GlazingPerformance> entries)
    {
      this.entries = entries;
    }

    /// <summary>Gets the default catalog backed by the embedded Glazings.json.</summary>
    public static GlazingCatalog Default => DefaultLazy.Value;

    private static readonly Lazy<GlazingCatalog> DefaultLazy =
      new Lazy<GlazingCatalog>(LoadFromEmbeddedResource);

    /// <summary>Loads a catalog from a JSON stream (primarily for testing).</summary>
    public static GlazingCatalog LoadFrom(Stream jsonStream)
    {
      if (jsonStream is null) throw new ArgumentNullException(nameof(jsonStream));
      return new GlazingCatalog(ParseEntries(jsonStream));
    }

    /// <summary>Loads a catalog from a JSON string (primarily for testing).</summary>
    public static GlazingCatalog LoadFromString(string json)
    {
      if (json is null) throw new ArgumentNullException(nameof(json));
      using var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
      return LoadFrom(ms);
    }

    private static GlazingCatalog LoadFromEmbeddedResource()
    {
      var asm = typeof(GlazingCatalog).Assembly;
      using var stream = asm.GetManifestResourceStream(DefaultResourceName)
        ?? throw new InvalidOperationException(
          $"Embedded resource '{DefaultResourceName}' not found. " +
          $"Ensure Glazings.json is included in the project as EmbeddedResource.");
      return new GlazingCatalog(ParseEntries(stream));
    }

    private static Dictionary<string, GlazingPerformance> ParseEntries(Stream stream)
    {
      using var doc = JsonDocument.Parse(stream);
      var root = doc.RootElement;
      if (!root.TryGetProperty("glazings", out var glz) || glz.ValueKind != JsonValueKind.Array)
        throw new InvalidOperationException(
          "Glazings JSON must have a top-level 'glazings' array.");

      var dict = new Dictionary<string, GlazingPerformance>(StringComparer.Ordinal);
      foreach (var elem in glz.EnumerateArray())
      {
        string id = elem.GetProperty("id").GetString()
          ?? throw new InvalidOperationException("Glazing entry is missing 'id'.");
        double tau = elem.GetProperty("solarHeatGain").GetDouble();
        double htCoef = elem.GetProperty("heatTransferCoefficient").GetDouble();

        if (dict.ContainsKey(id))
          throw new InvalidOperationException($"Duplicate glazing ID '{id}'.");
        dict[id] = new GlazingPerformance(tau, htCoef);
      }

      return dict;
    }

    #endregion

    #region 公開 API

    /// <summary>Gets the number of glazings in the catalog.</summary>
    public int Count => entries.Count;

    /// <summary>Returns whether the catalog contains a glazing with the given ID.</summary>
    public bool Contains(string glazingId) => entries.ContainsKey(glazingId);

    /// <summary>
    /// Retrieves the performance pair (solar heat gain rate, heat transfer
    /// coefficient) for the specified glazing ID.
    /// </summary>
    /// <param name="glazingId">Glazing identifier as used in WEBPRO input (e.g. <c>"3WgG06"</c>).</param>
    /// <returns>The glazing performance values.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="glazingId"/> is null.</exception>
    /// <exception cref="KeyNotFoundException">The glazing ID is not in the catalog.</exception>
    public GlazingPerformance Get(string glazingId)
    {
      if (glazingId is null) throw new ArgumentNullException(nameof(glazingId));
      if (!entries.TryGetValue(glazingId, out var perf))
        throw new KeyNotFoundException(
          $"Glazing ID '{glazingId}' is not in the catalog. " +
          $"Check Resources/Glazings.json.");
      return perf;
    }

    /// <summary>Returns an enumerable of all glazing IDs in the catalog.</summary>
    public IEnumerable<string> Ids => entries.Keys;

    #endregion

  }
}
