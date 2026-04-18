/* WebproJsonReader.cs
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
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

using Popolo.Webpro.Domain;
using Popolo.Webpro.Json.EnumConverters;

namespace Popolo.Webpro.Json
{
  /// <summary>
  /// Facade for reading a WEBPRO (省エネ法) input JSON file into a
  /// <see cref="WebproModel"/>.
  /// </summary>
  /// <remarks>
  /// <para>
  /// This is the main entry point for consumers who want to ingest a
  /// builelib-style JSON file without having to wire up the individual
  /// converters by hand. All necessary <see cref="JsonConverter"/>s are
  /// pre-registered in the options produced by
  /// <see cref="CreateDefaultOptions"/>, which can also be used directly by
  /// callers that need to customize the options further.
  /// </para>
  /// <para>
  /// <b>Read only.</b> The reader cannot write WEBPRO JSON; for outbound
  /// serialization use the Popolo native JSON schema via
  /// <c>Popolo.IO.Json.PopoloJsonSerializer</c>.
  /// </para>
  /// <para>
  /// All non-thermal sections of the WEBPRO JSON (HVAC, lighting, DHW,
  /// elevators, PV, etc.) are discarded during reading; see
  /// <see cref="WebproModelJsonConverter"/> for the complete list.
  /// </para>
  /// </remarks>
  public static class WebproJsonReader
  {

    #region デフォルトオプション

    /// <summary>
    /// Creates a <see cref="JsonSerializerOptions"/> pre-configured with all
    /// converters needed to read a WEBPRO input file.
    /// </summary>
    /// <returns>A fresh options instance.</returns>
    /// <remarks>
    /// A new instance is returned on every call so that callers may freely
    /// modify it (e.g. to add their own converters) without affecting other
    /// readers.
    /// </remarks>
    public static JsonSerializerOptions CreateDefaultOptions()
    {
      var options = new JsonSerializerOptions
      {
        // Japanese characters must be emitted as-is rather than escaped as
        // \uXXXX; matches Popolo v3.0 native JSON formatting. Harmless for
        // read, but required if callers later reuse these options for any
        // kind of write operation.
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
      };

      // Enum converters
      options.Converters.Add(new BuildingTypeJsonConverter());
      options.Converters.Add(new OrientationJsonConverter());
      options.Converters.Add(new WallTypeJsonConverter());
      options.Converters.Add(new WallInputMethodJsonConverter());
      options.Converters.Add(new WindowInputMethodJsonConverter());
      options.Converters.Add(new WindowFrameJsonConverter());
      options.Converters.Add(new StructureTypeJsonConverter());

      // Leaf DTO converters
      options.Converters.Add(new WebproWallLayerJsonConverter());
      options.Converters.Add(new WebproWallConfigureJsonConverter());
      options.Converters.Add(new WebproWindowJsonConverter());
      options.Converters.Add(new WebproWindowConfigureJsonConverter());

      // Mid-level DTO converters
      options.Converters.Add(new WebproBuildingJsonConverter());
      options.Converters.Add(new WebproRoomJsonConverter());
      options.Converters.Add(new WebproWallJsonConverter());
      options.Converters.Add(new WebproEnvelopeSetJsonConverter());

      // Top-level converter
      options.Converters.Add(new WebproModelJsonConverter());

      return options;
    }

    #endregion

    #region 読み取り API

    /// <summary>Reads a WEBPRO model from a JSON string.</summary>
    /// <param name="json">The WEBPRO JSON document as a string.</param>
    /// <returns>The parsed <see cref="WebproModel"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="json"/> is null.</exception>
    /// <exception cref="JsonException">The JSON is malformed or does not match the WEBPRO schema.</exception>
    public static WebproModel Read(string json)
    {
      if (json is null) throw new ArgumentNullException(nameof(json));
      return JsonSerializer.Deserialize<WebproModel>(json, CreateDefaultOptions())
        ?? throw new JsonException("Deserialization returned null.");
    }

    /// <summary>Reads a WEBPRO model from a <see cref="Stream"/>.</summary>
    /// <param name="stream">Stream containing the WEBPRO JSON document.</param>
    /// <returns>The parsed <see cref="WebproModel"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is null.</exception>
    /// <exception cref="JsonException">The JSON is malformed or does not match the WEBPRO schema.</exception>
    public static WebproModel Read(Stream stream)
    {
      if (stream is null) throw new ArgumentNullException(nameof(stream));
      return JsonSerializer.Deserialize<WebproModel>(stream, CreateDefaultOptions())
        ?? throw new JsonException("Deserialization returned null.");
    }

    /// <summary>Reads a WEBPRO model from a file path.</summary>
    /// <param name="path">Path to the WEBPRO JSON file (e.g. <c>builelib_input.json</c>).</param>
    /// <returns>The parsed <see cref="WebproModel"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is null.</exception>
    /// <exception cref="FileNotFoundException">The file does not exist.</exception>
    /// <exception cref="JsonException">The JSON is malformed or does not match the WEBPRO schema.</exception>
    public static WebproModel ReadFromFile(string path)
    {
      if (path is null) throw new ArgumentNullException(nameof(path));
      using var stream = File.OpenRead(path);
      return Read(stream);
    }

    #endregion

  }
}
