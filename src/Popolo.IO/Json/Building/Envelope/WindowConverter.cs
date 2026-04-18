/* WindowConverter.cs
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
using System.Text.Json;
using System.Text.Json.Serialization;
using Popolo.Core.Building.Envelope;
using Popolo.Core.Climate;

namespace Popolo.IO.Json.Building.Envelope
{
  /// <summary>
  /// JSON converter for <see cref="Window"/>.
  /// </summary>
  /// <remarks>
  /// <para>
  /// Serialized JSON schema:
  /// </para>
  /// <code>
  /// {
  ///   "kind":           "window",
  ///   "area":           2.7,
  ///   "outsideIncline": { "kind": "incline", ... },
  ///   "glazings": [
  ///     {
  ///       "transmittanceF": 0.79,
  ///       "reflectanceF":   0.07,
  ///       "transmittanceB": 0.79,
  ///       "reflectanceB":   0.07,
  ///       "resistance":     0.006
  ///     }
  ///   ],
  ///   "airGapResistances": [0.074],
  ///   "surfaceF": {
  ///     "convectiveCoefficient": 18.5,
  ///     "longWaveEmissivity":    0.9
  ///   },
  ///   "surfaceB": {
  ///     "convectiveCoefficient": 7.5,
  ///     "longWaveEmissivity":    0.9
  ///   },
  ///   "shadingDevices": [
  ///     { "kind": "noShadingDevice" },
  ///     { "kind": "venetianBlind",   ... }
  ///   ],
  ///   "sunShade": { "kind": "sunShade", ... }
  /// }
  /// </code>
  /// <para>
  /// <b>Glazing array structure:</b> <c>glazings</c> has <c>GlazingCount</c> entries.
  /// <c>airGapResistances</c> has <c>GlazingCount - 1</c> entries
  /// (zero-length for single-glazed windows).
  /// </para>
  /// <para>
  /// <b>Shading devices:</b> <c>shadingDevices</c> has
  /// <c>GlazingCount + 1</c> entries when present. Each entry is dispatched by its
  /// <c>kind</c> to <see cref="NoShadingDeviceConverter"/>,
  /// <see cref="SimpleShadingDeviceConverter"/>, or <see cref="VenetianBlindConverter"/>.
  /// If all positions are <see cref="NoShadingDevice"/>, the property is omitted on
  /// write. On read, a missing <c>shadingDevices</c> property leaves the Window's
  /// default <see cref="NoShadingDevice"/> assignments in place.
  /// </para>
  /// <para>
  /// <b>SunShade:</b> <c>sunShade</c> is omitted on write when the shade's shape is
  /// <see cref="SunShade.Shapes.None"/>. On read, a missing <c>sunShade</c> leaves
  /// the Window's default empty SunShade in place.
  /// </para>
  /// <para>
  /// <b>Angle-of-incidence dependence</b> is not serialized. Deserialized windows
  /// use the default <see cref="Window.GlassTypes.Transparent"/> angle
  /// dependence that the Window constructor installs.
  /// </para>
  /// <para>
  /// <b>Required sibling converters:</b> <see cref="InclineConverter"/>,
  /// <see cref="NoShadingDeviceConverter"/>, <see cref="SimpleShadingDeviceConverter"/>,
  /// <see cref="VenetianBlindConverter"/>, and <see cref="SunShadeConverter"/> must be
  /// registered in the same <see cref="JsonSerializerOptions"/>.
  /// </para>
  /// </remarks>
  public sealed class WindowConverter : JsonConverter<Window>
  {

    #region 定数

    private const string PropKind = "kind";
    private const string PropArea = "area";
    private const string PropOutsideIncline = "outsideIncline";
    private const string PropGlazings = "glazings";
    private const string PropAirGapResistances = "airGapResistances";
    private const string PropSurfaceF = "surfaceF";
    private const string PropSurfaceB = "surfaceB";
    private const string PropShadingDevices = "shadingDevices";
    private const string PropSunShade = "sunShade";

    // glazings 内のキー
    private const string PropTransmittanceF = "transmittanceF";
    private const string PropReflectanceF = "reflectanceF";
    private const string PropTransmittanceB = "transmittanceB";
    private const string PropReflectanceB = "reflectanceB";
    private const string PropResistance = "resistance";

    // surfaceF/surfaceB 内のキー
    private const string PropConvectiveCoefficient = "convectiveCoefficient";
    private const string PropLongWaveEmissivity = "longWaveEmissivity";

    // shadingDevices の kind 値
    private const string KindNoShadingDevice = "noShadingDevice";
    private const string KindSimpleShadingDevice = "simpleShadingDevice";
    private const string KindVenetianBlind = "venetianBlind";

    private const string ExpectedKind = "window";

    #endregion

    #region 内部型

    /// <summary>One glazing layer's optical and thermal properties.</summary>
    private readonly struct Glazing
    {
      public double TransmittanceF { get; }
      public double ReflectanceF { get; }
      public double TransmittanceB { get; }
      public double ReflectanceB { get; }
      public double Resistance { get; }

      public Glazing(double tf, double rf, double tb, double rb, double res)
      {
        TransmittanceF = tf;
        ReflectanceF = rf;
        TransmittanceB = tb;
        ReflectanceB = rb;
        Resistance = res;
      }
    }

    /// <summary>Bundle of surface coefficients for one side of a window.</summary>
    private readonly struct SurfaceCoefficients
    {
      public double ConvectiveCoefficient { get; }
      public double LongWaveEmissivity { get; }

      public SurfaceCoefficients(double conv, double emi)
      {
        ConvectiveCoefficient = conv;
        LongWaveEmissivity = emi;
      }
    }

    #endregion

    #region JsonConverter 実装

    /// <summary>Reads a <see cref="Window"/> from JSON.</summary>
    public override Window Read(
      ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
      if (reader.TokenType != JsonTokenType.StartObject)
        throw new JsonException($"Expected StartObject at the beginning of a {nameof(Window)}, but got {reader.TokenType}.");

      string? kind = null;
      double? area = null;
      Incline? outsideIncline = null;
      List<Glazing>? glazings = null;
      List<double>? airGapResistances = null;
      SurfaceCoefficients? surfaceF = null;
      SurfaceCoefficients? surfaceB = null;
      List<IShadingDevice>? shadingDevices = null;
      SunShade? sunShade = null;

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
          case PropKind: kind = reader.GetString(); break;
          case PropArea: area = reader.GetDouble(); break;
          case PropOutsideIncline:
            outsideIncline = JsonSerializer.Deserialize<Incline>(ref reader, options);
            break;
          case PropGlazings:
            glazings = ReadGlazingArray(ref reader);
            break;
          case PropAirGapResistances:
            airGapResistances = ReadDoubleArray(ref reader, PropAirGapResistances);
            break;
          case PropSurfaceF:
            surfaceF = ReadSurfaceCoefficients(ref reader, PropSurfaceF);
            break;
          case PropSurfaceB:
            surfaceB = ReadSurfaceCoefficients(ref reader, PropSurfaceB);
            break;
          case PropShadingDevices:
            shadingDevices = ReadShadingDeviceArray(ref reader, options);
            break;
          case PropSunShade:
            sunShade = JsonSerializer.Deserialize<SunShade>(ref reader, options);
            break;
          default: reader.Skip(); break;
        }
      }

      // kind 検証
      if (kind != ExpectedKind)
        throw new JsonException(
          $"Expected '{PropKind}' = '{ExpectedKind}' for {nameof(Window)}, but got '{kind ?? "(missing)"}'.");

      // 必須項目検証
      if (area is null)
        throw new JsonException($"Required property '{PropArea}' is missing from {nameof(Window)} JSON.");
      if (outsideIncline is null)
        throw new JsonException($"Required property '{PropOutsideIncline}' is missing from {nameof(Window)} JSON.");
      if (glazings is null)
        throw new JsonException($"Required property '{PropGlazings}' is missing from {nameof(Window)} JSON.");
      if (glazings.Count == 0)
        throw new JsonException($"Property '{PropGlazings}' must contain at least one glazing layer.");

      // airGapResistances のデフォルトは空リスト(single-glazed windows は airGap 不要)
      airGapResistances ??= new List<double>();
      if (airGapResistances.Count != glazings.Count - 1)
        throw new JsonException(
          $"'{PropAirGapResistances}' must have {glazings.Count - 1} entries " +
          $"(one less than '{PropGlazings}'), but got {airGapResistances.Count}.");

      // 光学特性配列を構築
      int n = glazings.Count;
      var tauF = new double[n];
      var rhoF = new double[n];
      var tauB = new double[n];
      var rhoB = new double[n];
      for (int i = 0; i < n; i++)
      {
        tauF[i] = glazings[i].TransmittanceF;
        rhoF[i] = glazings[i].ReflectanceF;
        tauB[i] = glazings[i].TransmittanceB;
        rhoB[i] = glazings[i].ReflectanceB;
      }

      // Window 生成(本コンストラクタが角度依存特性を Transparent で初期化)
      var window = new Window(area.Value, tauF, rhoF, tauB, rhoB, outsideIncline);

      // ガラス抵抗を設定
      for (int i = 0; i < n; i++)
        window.SetGlassResistance(i, glazings[i].Resistance);

      // 空気層抵抗を設定
      for (int i = 0; i < airGapResistances.Count; i++)
        window.SetAirGapResistance(i, airGapResistances[i]);

      // 表面係数(オプション)
      if (surfaceF is not null)
      {
        window.ConvectiveCoefficientF = surfaceF.Value.ConvectiveCoefficient;
        window.LongWaveEmissivityF = surfaceF.Value.LongWaveEmissivity;
      }
      if (surfaceB is not null)
      {
        window.ConvectiveCoefficientB = surfaceB.Value.ConvectiveCoefficient;
        window.LongWaveEmissivityB = surfaceB.Value.LongWaveEmissivity;
      }

      // 日射遮蔽(オプション)。省略時は Window コンストラクタが既に
      // NoShadingDevice で全位置を初期化している。
      if (shadingDevices is not null)
      {
        int expectedSdCount = n + 1;
        if (shadingDevices.Count != expectedSdCount)
          throw new JsonException(
            $"'{PropShadingDevices}' must have {expectedSdCount} entries " +
            $"(one more than '{PropGlazings}'), but got {shadingDevices.Count}.");
        for (int i = 0; i < shadingDevices.Count; i++)
          window.SetShadingDevice(i, shadingDevices[i]);
      }

      // 日除け(オプション)。省略時は Window コンストラクタが既に
      // MakeEmptySunShade() で初期化している。
      if (sunShade is not null)
        window.SunShade = sunShade;

      return window;
    }

    /// <summary>Writes a <see cref="Window"/> to JSON.</summary>
    public override void Write(
      Utf8JsonWriter writer, Window value, JsonSerializerOptions options)
    {
      if (value is null)
        throw new ArgumentNullException(nameof(value));

      writer.WriteStartObject();

      writer.WriteString(PropKind, ExpectedKind);
      writer.WriteNumber(PropArea, value.Area);

      // outsideIncline(IReadOnlyIncline を Incline にコピーしてシリアライズ)
      writer.WritePropertyName(PropOutsideIncline);
      JsonSerializer.Serialize(writer, new Incline(value.OutsideIncline), options);

      // glazings
      writer.WritePropertyName(PropGlazings);
      writer.WriteStartArray();
      for (int i = 0; i < value.GlazingCount; i++)
      {
        writer.WriteStartObject();
        writer.WriteNumber(PropTransmittanceF, value.GetGlazingTransmittance(i, true));
        writer.WriteNumber(PropReflectanceF, value.GetGlazingReflectance(i, true));
        writer.WriteNumber(PropTransmittanceB, value.GetGlazingTransmittance(i, false));
        writer.WriteNumber(PropReflectanceB, value.GetGlazingReflectance(i, false));
        writer.WriteNumber(PropResistance, value.GetGlassResistance(i));
        writer.WriteEndObject();
      }
      writer.WriteEndArray();

      // airGapResistances(GlazingCount - 1 個)
      writer.WritePropertyName(PropAirGapResistances);
      writer.WriteStartArray();
      for (int i = 0; i < value.GlazingCount - 1; i++)
        writer.WriteNumberValue(value.GetAirGapResistance(i));
      writer.WriteEndArray();

      // surfaceF
      writer.WritePropertyName(PropSurfaceF);
      writer.WriteStartObject();
      writer.WriteNumber(PropConvectiveCoefficient, value.ConvectiveCoefficientF);
      writer.WriteNumber(PropLongWaveEmissivity, value.LongWaveEmissivityF);
      writer.WriteEndObject();

      // surfaceB
      writer.WritePropertyName(PropSurfaceB);
      writer.WriteStartObject();
      writer.WriteNumber(PropConvectiveCoefficient, value.ConvectiveCoefficientB);
      writer.WriteNumber(PropLongWaveEmissivity, value.LongWaveEmissivityB);
      writer.WriteEndObject();

      // shadingDevices(全部 NoShadingDevice なら省略)
      int sdCount = value.GlazingCount + 1;
      bool anyNonDefault = false;
      for (int i = 0; i < sdCount; i++)
      {
        if (value.GetShadingDevice(i) is not NoShadingDevice)
        {
          anyNonDefault = true;
          break;
        }
      }
      if (anyNonDefault)
      {
        writer.WritePropertyName(PropShadingDevices);
        writer.WriteStartArray();
        for (int i = 0; i < sdCount; i++)
          WriteShadingDevice(writer, value.GetShadingDevice(i), options);
        writer.WriteEndArray();
      }

      // sunShade(None なら省略)
      if (value.SunShade.Shape != SunShade.Shapes.None)
      {
        writer.WritePropertyName(PropSunShade);
        JsonSerializer.Serialize(writer, value.SunShade, options);
      }

      writer.WriteEndObject();
    }

    #endregion

    #region glazing 配列の読み取り

    /// <summary>Reads the <c>glazings</c> array.</summary>
    private static List<Glazing> ReadGlazingArray(ref Utf8JsonReader reader)
    {
      if (reader.TokenType != JsonTokenType.StartArray)
        throw new JsonException($"Expected StartArray for '{PropGlazings}', but got {reader.TokenType}.");

      var result = new List<Glazing>();
      while (reader.Read())
      {
        if (reader.TokenType == JsonTokenType.EndArray)
          break;
        if (reader.TokenType != JsonTokenType.StartObject)
          throw new JsonException($"Each '{PropGlazings}' entry must be an object, but got {reader.TokenType}.");

        double? tauF = null, rhoF = null, tauB = null, rhoB = null, res = null;

        while (reader.Read())
        {
          if (reader.TokenType == JsonTokenType.EndObject)
            break;
          if (reader.TokenType != JsonTokenType.PropertyName)
            throw new JsonException($"Expected PropertyName in glazing entry, but got {reader.TokenType}.");

          string? propName = reader.GetString();
          if (!reader.Read())
            throw new JsonException($"Unexpected end of JSON while reading glazing entry property '{propName}'.");

          switch (propName)
          {
            case PropTransmittanceF: tauF = reader.GetDouble(); break;
            case PropReflectanceF:   rhoF = reader.GetDouble(); break;
            case PropTransmittanceB: tauB = reader.GetDouble(); break;
            case PropReflectanceB:   rhoB = reader.GetDouble(); break;
            case PropResistance:     res  = reader.GetDouble(); break;
            default: reader.Skip(); break;
          }
        }

        if (tauF is null) throw new JsonException($"Required property 'glazings[].{PropTransmittanceF}' is missing.");
        if (rhoF is null) throw new JsonException($"Required property 'glazings[].{PropReflectanceF}' is missing.");
        if (tauB is null) throw new JsonException($"Required property 'glazings[].{PropTransmittanceB}' is missing.");
        if (rhoB is null) throw new JsonException($"Required property 'glazings[].{PropReflectanceB}' is missing.");
        if (res is null)  throw new JsonException($"Required property 'glazings[].{PropResistance}' is missing.");

        result.Add(new Glazing(tauF.Value, rhoF.Value, tauB.Value, rhoB.Value, res.Value));
      }
      return result;
    }

    /// <summary>Reads a flat array of <see cref="double"/>.</summary>
    private static List<double> ReadDoubleArray(ref Utf8JsonReader reader, string propertyName)
    {
      if (reader.TokenType != JsonTokenType.StartArray)
        throw new JsonException($"Expected StartArray for '{propertyName}', but got {reader.TokenType}.");

      var result = new List<double>();
      while (reader.Read())
      {
        if (reader.TokenType == JsonTokenType.EndArray) break;
        result.Add(reader.GetDouble());
      }
      return result;
    }

    #endregion

    #region 表面係数オブジェクトの読み取り

    /// <summary>Reads a nested <c>surfaceF</c> or <c>surfaceB</c> object.</summary>
    private static SurfaceCoefficients ReadSurfaceCoefficients(
      ref Utf8JsonReader reader, string surfaceKey)
    {
      if (reader.TokenType != JsonTokenType.StartObject)
        throw new JsonException($"Expected StartObject for '{surfaceKey}', but got {reader.TokenType}.");

      double? conv = null;
      double? emi = null;

      while (reader.Read())
      {
        if (reader.TokenType == JsonTokenType.EndObject) break;
        if (reader.TokenType != JsonTokenType.PropertyName)
          throw new JsonException($"Expected PropertyName in '{surfaceKey}', but got {reader.TokenType}.");

        string? propName = reader.GetString();
        if (!reader.Read())
          throw new JsonException($"Unexpected end of JSON while reading '{surfaceKey}.{propName}'.");

        switch (propName)
        {
          case PropConvectiveCoefficient: conv = reader.GetDouble(); break;
          case PropLongWaveEmissivity: emi = reader.GetDouble(); break;
          default: reader.Skip(); break;
        }
      }

      if (conv is null)
        throw new JsonException($"Required property '{surfaceKey}.{PropConvectiveCoefficient}' is missing.");
      if (emi is null)
        throw new JsonException($"Required property '{surfaceKey}.{PropLongWaveEmissivity}' is missing.");

      return new SurfaceCoefficients(conv.Value, emi.Value);
    }

    #endregion

    #region 日射遮蔽配列の読み書き

    /// <summary>Reads the <c>shadingDevices</c> array with kind-based dispatch.</summary>
    private static List<IShadingDevice> ReadShadingDeviceArray(
      ref Utf8JsonReader reader, JsonSerializerOptions options)
    {
      if (reader.TokenType != JsonTokenType.StartArray)
        throw new JsonException($"Expected StartArray for '{PropShadingDevices}', but got {reader.TokenType}.");

      var result = new List<IShadingDevice>();
      while (reader.Read())
      {
        if (reader.TokenType == JsonTokenType.EndArray) break;
        if (reader.TokenType != JsonTokenType.StartObject)
          throw new JsonException($"Each '{PropShadingDevices}' entry must be an object, but got {reader.TokenType}.");

        // プリスキャンで kind を取得
        Utf8JsonReader peek = reader;
        string? deviceKind = PeekKind(ref peek);

        IShadingDevice device = deviceKind switch
        {
          KindNoShadingDevice =>
            JsonSerializer.Deserialize<NoShadingDevice>(ref reader, options)
              ?? throw new JsonException($"{nameof(NoShadingDevice)} deserialization returned null."),
          KindSimpleShadingDevice =>
            JsonSerializer.Deserialize<SimpleShadingDevice>(ref reader, options)
              ?? throw new JsonException($"{nameof(SimpleShadingDevice)} deserialization returned null."),
          KindVenetianBlind =>
            JsonSerializer.Deserialize<VenetianBlind>(ref reader, options)
              ?? throw new JsonException($"{nameof(VenetianBlind)} deserialization returned null."),
          _ => throw new JsonException(
            $"Unknown shading device kind: '{deviceKind ?? "(missing)"}'."),
        };
        result.Add(device);
      }
      return result;
    }

    /// <summary>Writes a single <see cref="IShadingDevice"/> dispatching on runtime type.</summary>
    private static void WriteShadingDevice(
      Utf8JsonWriter writer, IShadingDevice device, JsonSerializerOptions options)
    {
      // ランタイム型で分岐して、具象型のコンバータを使う。
      switch (device)
      {
        case NoShadingDevice nd:
          JsonSerializer.Serialize(writer, nd, options);
          break;
        case SimpleShadingDevice sd:
          JsonSerializer.Serialize(writer, sd, options);
          break;
        case VenetianBlind vb:
          JsonSerializer.Serialize(writer, vb, options);
          break;
        default:
          throw new JsonException(
            $"Unsupported {nameof(IShadingDevice)} implementation: {device?.GetType().FullName ?? "null"}.");
      }
    }

    /// <summary>
    /// Peeks the <c>kind</c> property of the object that <paramref name="reader"/>
    /// is currently positioned at (StartObject), using a cheap reader-copy.
    /// </summary>
    private static string? PeekKind(ref Utf8JsonReader reader)
    {
      if (reader.TokenType != JsonTokenType.StartObject)
        return null;

      int depth = 0;
      while (reader.Read())
      {
        if (reader.TokenType == JsonTokenType.StartObject || reader.TokenType == JsonTokenType.StartArray)
          depth++;
        else if (reader.TokenType == JsonTokenType.EndObject || reader.TokenType == JsonTokenType.EndArray)
        {
          if (depth == 0) return null;
          depth--;
        }
        else if (depth == 0 && reader.TokenType == JsonTokenType.PropertyName)
        {
          if (reader.GetString() == PropKind)
          {
            if (!reader.Read()) return null;
            if (reader.TokenType == JsonTokenType.String) return reader.GetString();
            return null;
          }
        }
      }
      return null;
    }

    #endregion

  }
}
