/* WallLayerConverter.cs
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
using Popolo.Core.Physics;

namespace Popolo.IO.Json.Building.Envelope
{
  /// <summary>
  /// JSON converter for <see cref="WallLayer"/>.
  /// </summary>
  /// <remarks>
  /// <para>
  /// Serialized JSON schema (dry layer, no moisture transfer):
  /// </para>
  /// <code>
  /// {
  ///   "kind":                "wallLayer",
  ///   "name":                "Concrete",
  ///   "thermalConductivity": 1.4,
  ///   "volSpecificHeat":     1934.0,
  ///   "thickness":           0.15
  /// }
  /// </code>
  /// <para>
  /// Serialized JSON schema (moist layer, with moisture transfer enabled):
  /// </para>
  /// <code>
  /// {
  ///   "kind":                "wallLayer",
  ///   "name":                "Plywood",
  ///   "thermalConductivity": 0.15,
  ///   "volSpecificHeat":     720.0,
  ///   "thickness":           0.012,
  ///   "moistureProperties":  {
  ///     "conductivity": 2.0e-10,
  ///     "voidage":      0.15,
  ///     "kappa":        0.1,
  ///     "nu":           0.01
  ///   }
  /// }
  /// </code>
  /// <para>
  /// <b>Unit convention for moisture properties:</b>
  /// <c>voidage</c>, <c>kappa</c>, and <c>nu</c> follow the constructor input
  /// convention (per-layer, not per-half-layer). Popolo.Core internally stores
  /// these as half-layer-lumped values (<see cref="WallLayer.WaterCapacity"/>,
  /// <see cref="WallLayer.KappaC"/>, <see cref="WallLayer.NuC"/>); this converter
  /// handles the conversion on both read and write.
  /// </para>
  /// </remarks>
  public sealed class WallLayerConverter : JsonConverter<WallLayer>
  {

    #region 定数

    private const string PropKind = "kind";
    private const string PropName = "name";
    private const string PropThermalConductivity = "thermalConductivity";
    private const string PropVolSpecificHeat = "volSpecificHeat";
    private const string PropThickness = "thickness";
    private const string PropMoistureProperties = "moistureProperties";

    // moistureProperties 内のキー
    private const string PropMoistureConductivity = "conductivity";
    private const string PropVoidage = "voidage";
    private const string PropKappa = "kappa";
    private const string PropNu = "nu";

    /// <summary>Expected discriminator value for this converter.</summary>
    private const string ExpectedKind = "wallLayer";

    #endregion

    #region JsonConverter 実装

    /// <summary>Reads a <see cref="WallLayer"/> from JSON.</summary>
    /// <param name="reader">UTF-8 JSON reader positioned at the start of the object.</param>
    /// <param name="typeToConvert">Target type.</param>
    /// <param name="options">Serializer options (ignored).</param>
    /// <returns>Deserialized <see cref="WallLayer"/>.</returns>
    /// <exception cref="JsonException">Thrown when the JSON is malformed or a required property is missing.</exception>
    public override WallLayer Read(
      ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
      if (reader.TokenType != JsonTokenType.StartObject)
        throw new JsonException($"Expected StartObject at the beginning of a {nameof(WallLayer)}, but got {reader.TokenType}.");

      string? kind = null;
      string? name = null;
      double? thermalConductivity = null;
      double? volSpecificHeat = null;
      double? thickness = null;

      // moisture properties (optional)
      bool hasMoistureProps = false;
      double moistureConductivity = 0, voidage = 0, kappa = 0, nu = 0;

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
          case PropName:
            name = reader.GetString();
            break;
          case PropThermalConductivity:
            thermalConductivity = reader.GetDouble();
            break;
          case PropVolSpecificHeat:
            volSpecificHeat = reader.GetDouble();
            break;
          case PropThickness:
            thickness = reader.GetDouble();
            break;
          case PropMoistureProperties:
            ReadMoistureProperties(ref reader,
              out moistureConductivity, out voidage, out kappa, out nu);
            hasMoistureProps = true;
            break;
          default:
            reader.Skip();
            break;
        }
      }

      // kind 識別子の検証
      if (kind != ExpectedKind)
        throw new JsonException(
          $"Expected '{PropKind}' = '{ExpectedKind}' for {nameof(WallLayer)}, but got '{kind ?? "(missing)"}'.");

      // 必須項目の検証
      if (name is null)
        throw new JsonException($"Required property '{PropName}' is missing from {nameof(WallLayer)} JSON.");
      if (thermalConductivity is null)
        throw new JsonException($"Required property '{PropThermalConductivity}' is missing from {nameof(WallLayer)} JSON.");
      if (volSpecificHeat is null)
        throw new JsonException($"Required property '{PropVolSpecificHeat}' is missing from {nameof(WallLayer)} JSON.");
      if (thickness is null)
        throw new JsonException($"Required property '{PropThickness}' is missing from {nameof(WallLayer)} JSON.");

      if (hasMoistureProps)
      {
        return new WallLayer(
          name,
          thermalConductivity.Value, volSpecificHeat.Value,
          moistureConductivity, voidage, kappa, nu,
          thickness.Value);
      }
      else
      {
        return new WallLayer(
          name,
          thermalConductivity.Value, volSpecificHeat.Value,
          thickness.Value);
      }
    }

    /// <summary>Writes a <see cref="WallLayer"/> to JSON.</summary>
    /// <param name="writer">UTF-8 JSON writer.</param>
    /// <param name="value">Layer to serialize.</param>
    /// <param name="options">Serializer options (ignored).</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public override void Write(
      Utf8JsonWriter writer, WallLayer value, JsonSerializerOptions options)
    {
      if (value is null)
        throw new ArgumentNullException(nameof(value));

      writer.WriteStartObject();
      writer.WriteString(PropKind, value.Kind); // virtual / override から取得
      writer.WriteString(PropName, value.Name);
      writer.WriteNumber(PropThermalConductivity, value.ThermalConductivity);
      writer.WriteNumber(PropVolSpecificHeat, value.VolSpecificHeat);
      writer.WriteNumber(PropThickness, value.Thickness);

      if (value.MoistureConductivity != 0 && value.Thickness > 0)
      {
        // Popolo.Core は WaterCapacity/KappaC/NuC を半層集約形で保持している。
        // コンストラクタ入力と同じ原単位(voidage, kappa, nu)に戻す。
        //   WaterCapacity = 0.5 * voidage * thickness * ρ   →  voidage = 2*WC / (thickness * ρ)
        //   KappaC        = 0.5 * kappa * thickness         →  kappa   = 2*KC / thickness
        //   NuC           = 0.5 * nu    * thickness         →  nu      = 2*NuC / thickness
        double voidage = 2.0 * value.WaterCapacity
          / (value.Thickness * PhysicsConstants.NominalMoistAirDensity);
        double kappa = 2.0 * value.KappaC / value.Thickness;
        double nu = 2.0 * value.NuC / value.Thickness;

        writer.WritePropertyName(PropMoistureProperties);
        writer.WriteStartObject();
        writer.WriteNumber(PropMoistureConductivity, value.MoistureConductivity);
        writer.WriteNumber(PropVoidage, voidage);
        writer.WriteNumber(PropKappa, kappa);
        writer.WriteNumber(PropNu, nu);
        writer.WriteEndObject();
      }

      writer.WriteEndObject();
    }

    #endregion

    #region private ヘルパー

    /// <summary>Reads the nested <c>moistureProperties</c> object.</summary>
    private static void ReadMoistureProperties(
      ref Utf8JsonReader reader,
      out double conductivity, out double voidage, out double kappa, out double nu)
    {
      if (reader.TokenType != JsonTokenType.StartObject)
        throw new JsonException($"Expected StartObject for '{PropMoistureProperties}', but got {reader.TokenType}.");

      double? c = null, v = null, k = null, n = null;

      while (reader.Read())
      {
        if (reader.TokenType == JsonTokenType.EndObject)
          break;
        if (reader.TokenType != JsonTokenType.PropertyName)
          throw new JsonException($"Expected PropertyName in '{PropMoistureProperties}', but got {reader.TokenType}.");

        string? propName = reader.GetString();
        if (!reader.Read())
          throw new JsonException($"Unexpected end of JSON while reading '{PropMoistureProperties}.{propName}'.");

        switch (propName)
        {
          case PropMoistureConductivity: c = reader.GetDouble(); break;
          case PropVoidage: v = reader.GetDouble(); break;
          case PropKappa: k = reader.GetDouble(); break;
          case PropNu: n = reader.GetDouble(); break;
          default: reader.Skip(); break;
        }
      }

      if (c is null) throw new JsonException($"Required property '{PropMoistureProperties}.{PropMoistureConductivity}' is missing.");
      if (v is null) throw new JsonException($"Required property '{PropMoistureProperties}.{PropVoidage}' is missing.");
      if (k is null) throw new JsonException($"Required property '{PropMoistureProperties}.{PropKappa}' is missing.");
      if (n is null) throw new JsonException($"Required property '{PropMoistureProperties}.{PropNu}' is missing.");

      conductivity = c.Value;
      voidage = v.Value;
      kappa = k.Value;
      nu = n.Value;
    }

    #endregion

  }
}