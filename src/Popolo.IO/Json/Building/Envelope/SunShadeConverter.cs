/* SunShadeConverter.cs
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
using Popolo.Core.Building.Envelope;
using Popolo.Core.Climate;

namespace Popolo.IO.Json.Building.Envelope
{
  /// <summary>
  /// JSON converter for <see cref="SunShade"/>.
  /// </summary>
  /// <remarks>
  /// <para>
  /// Serialized JSON schema when <c>shape == None</c>:
  /// </para>
  /// <code>
  /// {
  ///   "kind":  "sunShade",
  ///   "shape": "None"
  /// }
  /// </code>
  /// <para>
  /// Serialized JSON schema when <c>shape != None</c>:
  /// </para>
  /// <code>
  /// {
  ///   "kind":         "sunShade",
  ///   "shape":        "Horizontal",
  ///   "incline":      { "kind": "incline", "horizontalAngle": 0.0, "verticalAngle": 1.5708 },
  ///   "winHeight":    1.8,
  ///   "winWidth":     1.5,
  ///   "overhang":     0.6,
  ///   "topMargin":    0.2,
  ///   "bottomMargin": 0.0,
  ///   "leftMargin":   0.1,
  ///   "rightMargin":  0.1
  /// }
  /// </code>
  /// <para>
  /// <b>Incline converter requirement:</b> The nested <c>incline</c> object requires
  /// an <see cref="InclineConverter"/> to be registered in the same
  /// <see cref="JsonSerializerOptions"/>.
  /// </para>
  /// </remarks>
  public sealed class SunShadeConverter : JsonConverter<SunShade>
  {

    #region 定数

    private const string PropKind = "kind";
    private const string PropShape = "shape";
    private const string PropIncline = "incline";
    private const string PropWinHeight = "winHeight";
    private const string PropWinWidth = "winWidth";
    private const string PropOverhang = "overhang";
    private const string PropTopMargin = "topMargin";
    private const string PropBottomMargin = "bottomMargin";
    private const string PropLeftMargin = "leftMargin";
    private const string PropRightMargin = "rightMargin";

    private const string ExpectedKind = "sunShade";

    #endregion

    #region JsonConverter 実装

    /// <summary>Reads a <see cref="SunShade"/> from JSON.</summary>
    /// <param name="reader">UTF-8 JSON reader positioned at the start of the object.</param>
    /// <param name="typeToConvert">Target type.</param>
    /// <param name="options">Serializer options; must include an <see cref="InclineConverter"/>.</param>
    /// <returns>Deserialized <see cref="SunShade"/>.</returns>
    /// <exception cref="JsonException">Thrown when the JSON is malformed or a required property is missing.</exception>
    public override SunShade Read(
      ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
      if (reader.TokenType != JsonTokenType.StartObject)
        throw new JsonException($"Expected StartObject at the beginning of a {nameof(SunShade)}, but got {reader.TokenType}.");

      string? kind = null;
      SunShade.Shapes? shape = null;
      Incline? incline = null;
      double winHeight = 0, winWidth = 0, overhang = 0;
      double topMargin = 0, bottomMargin = 0, leftMargin = 0, rightMargin = 0;

      while (reader.Read())
      {
        if (reader.TokenType == JsonTokenType.EndObject)
          break;

        if (reader.TokenType != JsonTokenType.PropertyName)
          throw new JsonException($"Expected PropertyName, but got {reader.TokenType}.");

        string? propName = reader.GetString();
        if (!reader.Read())
          throw new JsonException($"Unexpected end of JSON while reading property '{propName}'.");

        switch (propName)
        {
          case PropKind:
            kind = reader.GetString();
            break;
          case PropShape:
            string? shapeStr = reader.GetString();
            if (shapeStr is null)
              throw new JsonException($"'{PropShape}' must be a string.");
            if (!Enum.TryParse<SunShade.Shapes>(shapeStr, out var parsed))
              throw new JsonException($"Unknown {nameof(SunShade.Shapes)} value: '{shapeStr}'.");
            shape = parsed;
            break;
          case PropIncline:
            incline = JsonSerializer.Deserialize<Incline>(ref reader, options);
            break;
          case PropWinHeight: winHeight = reader.GetDouble(); break;
          case PropWinWidth: winWidth = reader.GetDouble(); break;
          case PropOverhang: overhang = reader.GetDouble(); break;
          case PropTopMargin: topMargin = reader.GetDouble(); break;
          case PropBottomMargin: bottomMargin = reader.GetDouble(); break;
          case PropLeftMargin: leftMargin = reader.GetDouble(); break;
          case PropRightMargin: rightMargin = reader.GetDouble(); break;
          default: reader.Skip(); break;
        }
      }

      if (kind != ExpectedKind)
        throw new JsonException(
          $"Expected '{PropKind}' = '{ExpectedKind}' for {nameof(SunShade)}, but got '{kind ?? "(missing)"}'.");
      if (shape is null)
        throw new JsonException($"Required property '{PropShape}' is missing from {nameof(SunShade)} JSON.");

      // Shape == None は incline 不要。
      if (shape == SunShade.Shapes.None)
        return SunShade.MakeEmptySunShade();

      // Shape != None は incline 必須。
      if (incline is null)
        throw new JsonException($"Property '{PropIncline}' is required when {PropShape} != None.");

      // 9 引数コンストラクタ一発。SunShade クラスの remarks 記載のとおり、
      // 本コンストラクタはシリアライズ用途に公開されている。
      return new SunShade(
        shape.Value, incline,
        winHeight, winWidth, overhang,
        topMargin, bottomMargin, leftMargin, rightMargin);
    }

    /// <summary>Writes a <see cref="SunShade"/> to JSON.</summary>
    /// <param name="writer">UTF-8 JSON writer.</param>
    /// <param name="value">Shading device to serialize.</param>
    /// <param name="options">Serializer options; must include an <see cref="InclineConverter"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public override void Write(
      Utf8JsonWriter writer, SunShade value, JsonSerializerOptions options)
    {
      if (value is null)
        throw new ArgumentNullException(nameof(value));

      writer.WriteStartObject();
      writer.WriteString(PropKind, ExpectedKind);
      writer.WriteString(PropShape, value.Shape.ToString());

      if (value.Shape != SunShade.Shapes.None)
      {
        writer.WritePropertyName(PropIncline);
        // Incline は IReadOnlyIncline として保持されているが、IReadOnlyIncline の別実装に備えて
        // コピーコンストラクタで具象化する。InclineConverter が options に登録されている前提。
        JsonSerializer.Serialize(writer, new Incline(value.Incline), options);

        writer.WriteNumber(PropWinHeight, value.WinHeight);
        writer.WriteNumber(PropWinWidth, value.WinWidth);
        writer.WriteNumber(PropOverhang, value.Overhang);
        writer.WriteNumber(PropTopMargin, value.TopMargin);
        writer.WriteNumber(PropBottomMargin, value.BottomMargin);
        writer.WriteNumber(PropLeftMargin, value.LeftMargin);
        writer.WriteNumber(PropRightMargin, value.RightMargin);
      }

      writer.WriteEndObject();
    }

    #endregion

  }
}