/* PopoloJsonSerializer.cs
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
using System.IO;
using System.Text.Json;

using Popolo.Core.Building;
using Popolo.IO.Json.Building;
using Popolo.IO.Json.Building.Envelope;
using Popolo.IO.Json.Climate;

namespace Popolo.IO.Json
{
  /// <summary>
  /// High-level facade for serializing and deserializing
  /// <see cref="BuildingThermalModel"/> instances to and from JSON.
  /// </summary>
  /// <remarks>
  /// <para>
  /// This static class is the recommended entry point for JSON I/O of Popolo
  /// thermal models. It pre-configures a <see cref="JsonSerializerOptions"/>
  /// instance with all converters required by the model graph
  /// (walls, layers, windows, shading devices, zones, multi-rooms, etc.).
  /// </para>
  /// <para>
  /// Typical usage:
  /// </para>
  /// <code>
  /// // Write
  /// string json = PopoloJsonSerializer.Serialize(model);
  /// PopoloJsonSerializer.SerializeToFile(model, "model.json");
  ///
  /// // Read
  /// var restored = PopoloJsonSerializer.Deserialize(json);
  /// var fromFile = PopoloJsonSerializer.DeserializeFromFile("model.json");
  /// </code>
  /// <para>
  /// For finer control, use <see cref="CreateDefaultOptions"/> to obtain the
  /// pre-configured options and invoke <see cref="JsonSerializer"/> directly.
  /// The returned options can be augmented with application-specific converters
  /// or additional formatting preferences.
  /// </para>
  /// <para>
  /// <b>Schema version:</b> Written files carry
  /// <c>"$schemaVersion": "3.0"</c> at the top level.
  /// </para>
  /// </remarks>
  public static class PopoloJsonSerializer
  {

    #region オプション構築

    /// <summary>
    /// Creates a new <see cref="JsonSerializerOptions"/> instance pre-configured
    /// with all converters required for <see cref="BuildingThermalModel"/> I/O.
    /// </summary>
    /// <returns>A newly allocated, independent options instance.</returns>
    /// <remarks>
    /// <para>
    /// A fresh instance is returned on each call so that callers can freely
    /// mutate the result without affecting other callers. If you serialize or
    /// deserialize many models, consider caching a single instance for
    /// performance — <see cref="JsonSerializerOptions"/> is thread-safe for
    /// concurrent use after it has been used once.
    /// </para>
    /// <para>
    /// The returned options enable <see cref="JsonSerializerOptions.WriteIndented"/>
    /// for human readability. Set to <c>false</c> for compact output.
    /// </para>
    /// </remarks>
    public static JsonSerializerOptions CreateDefaultOptions()
    {
      var opts = new JsonSerializerOptions
      {
        WriteIndented = true,
      };

      // Envelope 系(Popolo.IO.Json.Building.Envelope)
      opts.Converters.Add(new AirGapLayerConverter());
      opts.Converters.Add(new WallLayerConverter());
      opts.Converters.Add(new WallConverter());
      opts.Converters.Add(new NoShadingDeviceConverter());
      opts.Converters.Add(new SimpleShadingDeviceConverter());
      opts.Converters.Add(new VenetianBlindConverter());
      opts.Converters.Add(new SunShadeConverter());
      opts.Converters.Add(new WindowConverter());

      // Climate 系(Popolo.IO.Json.Climate)
      opts.Converters.Add(new InclineConverter());
      opts.Converters.Add(new SunConverter());

      // Building 系(Popolo.IO.Json.Building)
      opts.Converters.Add(new ZoneConverter());
      opts.Converters.Add(new MultiRoomsConverter());
      opts.Converters.Add(new BuildingThermalModelConverter());

      return opts;
    }

    #endregion

    #region 文字列 I/O

    /// <summary>Serializes a <see cref="BuildingThermalModel"/> to a JSON string.</summary>
    /// <param name="model">Model to serialize.</param>
    /// <returns>JSON representation of <paramref name="model"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> is null.</exception>
    public static string Serialize(BuildingThermalModel model)
    {
      if (model is null) throw new ArgumentNullException(nameof(model));
      return JsonSerializer.Serialize(model, CreateDefaultOptions());
    }

    /// <summary>Serializes a <see cref="BuildingThermalModel"/> using caller-supplied options.</summary>
    /// <param name="model">Model to serialize.</param>
    /// <param name="options">Serializer options; pass an instance returned by <see cref="CreateDefaultOptions"/> (possibly modified).</param>
    /// <returns>JSON representation of <paramref name="model"/>.</returns>
    /// <exception cref="ArgumentNullException">Any argument is null.</exception>
    public static string Serialize(BuildingThermalModel model, JsonSerializerOptions options)
    {
      if (model is null) throw new ArgumentNullException(nameof(model));
      if (options is null) throw new ArgumentNullException(nameof(options));
      return JsonSerializer.Serialize(model, options);
    }

    /// <summary>Deserializes a JSON string to a <see cref="BuildingThermalModel"/>.</summary>
    /// <param name="json">JSON representation.</param>
    /// <returns>Deserialized model.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="json"/> is null.</exception>
    /// <exception cref="JsonException">Thrown when the JSON is malformed or required fields are missing.</exception>
    public static BuildingThermalModel Deserialize(string json)
    {
      if (json is null) throw new ArgumentNullException(nameof(json));
      return JsonSerializer.Deserialize<BuildingThermalModel>(json, CreateDefaultOptions())
        ?? throw new JsonException($"{nameof(BuildingThermalModel)} deserialization returned null.");
    }

    /// <summary>Deserializes using caller-supplied options.</summary>
    /// <param name="json">JSON representation.</param>
    /// <param name="options">Serializer options.</param>
    /// <returns>Deserialized model.</returns>
    /// <exception cref="ArgumentNullException">Any argument is null.</exception>
    /// <exception cref="JsonException">Thrown when the JSON is malformed or required fields are missing.</exception>
    public static BuildingThermalModel Deserialize(string json, JsonSerializerOptions options)
    {
      if (json is null) throw new ArgumentNullException(nameof(json));
      if (options is null) throw new ArgumentNullException(nameof(options));
      return JsonSerializer.Deserialize<BuildingThermalModel>(json, options)
        ?? throw new JsonException($"{nameof(BuildingThermalModel)} deserialization returned null.");
    }

    #endregion

    #region ファイル I/O

    /// <summary>Serializes a <see cref="BuildingThermalModel"/> to a file.</summary>
    /// <param name="model">Model to serialize.</param>
    /// <param name="filePath">Target file path. Existing file will be overwritten.</param>
    /// <exception cref="ArgumentNullException">Any argument is null.</exception>
    /// <exception cref="IOException">Thrown on file-system errors.</exception>
    public static void SerializeToFile(BuildingThermalModel model, string filePath)
    {
      if (model is null) throw new ArgumentNullException(nameof(model));
      if (filePath is null) throw new ArgumentNullException(nameof(filePath));
      File.WriteAllText(filePath, Serialize(model));
    }

    /// <summary>Deserializes a <see cref="BuildingThermalModel"/> from a file.</summary>
    /// <param name="filePath">Source file path.</param>
    /// <returns>Deserialized model.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="filePath"/> is null.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    /// <exception cref="JsonException">Thrown when the JSON is malformed or required fields are missing.</exception>
    public static BuildingThermalModel DeserializeFromFile(string filePath)
    {
      if (filePath is null) throw new ArgumentNullException(nameof(filePath));
      string json = File.ReadAllText(filePath);
      return Deserialize(json);
    }

    #endregion

  }
}
