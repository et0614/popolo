/* WindowConverterTests.cs
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
using Xunit;

using Popolo.Core.Building.Envelope;
using Popolo.Core.Climate;
using Popolo.IO.Json.Climate;
using Popolo.IO.Json.Building.Envelope;

namespace Popolo.IO.Tests.Json.Building.Envelope
{
    /// <summary>Unit tests for <see cref="WindowConverter"/>.</summary>
    public class WindowConverterTests
    {
        #region ヘルパー

        /// <summary>Creates options with all converters required by WindowConverter.</summary>
        private static JsonSerializerOptions CreateOptions()
        {
            var opts = new JsonSerializerOptions();
            opts.Converters.Add(new WindowConverter());
            opts.Converters.Add(new InclineConverter());
            opts.Converters.Add(new NoShadingDeviceConverter());
            opts.Converters.Add(new SimpleShadingDeviceConverter());
            opts.Converters.Add(new VenetianBlindConverter());
            opts.Converters.Add(new SunShadeConverter());
            return opts;
        }

        private static Incline SouthVerticalIncline()
            => new Incline(horizontalAngle: 0.0, verticalAngle: Math.PI / 2);

        /// <summary>Single-pane window, south-facing.</summary>
        private static Window MakeSinglePaneWindow()
        {
            return new Window(
                area: 2.0,
                transmittance: new double[] { 0.79 },
                reflectance: new double[] { 0.07 },
                outsideIncline: SouthVerticalIncline());
        }

        /// <summary>Double-glazed window with different F/B optical properties.</summary>
        private static Window MakeDoubleGlazedWindow()
        {
            return new Window(
                area: 2.7,
                transmittanceF: new double[] { 0.79, 0.79 },
                reflectanceF:   new double[] { 0.07, 0.07 },
                transmittanceB: new double[] { 0.79, 0.79 },
                reflectanceB:   new double[] { 0.07, 0.07 },
                outsideIncline: SouthVerticalIncline());
        }

        private static Window Roundtrip(Window original)
        {
            var opts = CreateOptions();
            var json = JsonSerializer.Serialize(original, opts);
            return JsonSerializer.Deserialize<Window>(json, opts)!;
        }

        private static int CountProperties(JsonElement obj)
        {
            int count = 0;
            foreach (var _ in obj.EnumerateObject()) count++;
            return count;
        }

        #endregion

        // ================================================================
        #region シリアライズ (基本構造)

        [Fact]
        public void Write_SinglePane_ProducesExpectedStructure()
        {
            var json = JsonSerializer.Serialize(MakeSinglePaneWindow(), CreateOptions());
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.Equal("window", root.GetProperty("kind").GetString());
            Assert.Equal(2.0, root.GetProperty("area").GetDouble());
            Assert.Equal(JsonValueKind.Object, root.GetProperty("outsideIncline").ValueKind);
            Assert.Equal(1, root.GetProperty("glazings").GetArrayLength());
            Assert.Equal(0, root.GetProperty("airGapResistances").GetArrayLength());
            Assert.Equal(JsonValueKind.Object, root.GetProperty("surfaceF").ValueKind);
            Assert.Equal(JsonValueKind.Object, root.GetProperty("surfaceB").ValueKind);
        }

        [Fact]
        public void Write_DoubleGlazed_ProducesTwoGlazingsAndOneAirGap()
        {
            var json = JsonSerializer.Serialize(MakeDoubleGlazedWindow(), CreateOptions());
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.Equal(2, root.GetProperty("glazings").GetArrayLength());
            Assert.Equal(1, root.GetProperty("airGapResistances").GetArrayLength());
        }

        [Fact]
        public void Write_Glazing_IncludesAllFiveOpticalFields()
        {
            var json = JsonSerializer.Serialize(MakeSinglePaneWindow(), CreateOptions());
            using var doc = JsonDocument.Parse(json);
            var glazing = doc.RootElement.GetProperty("glazings")[0];

            Assert.Equal(5, CountProperties(glazing));
            Assert.Equal(0.79, glazing.GetProperty("transmittanceF").GetDouble());
            Assert.Equal(0.07, glazing.GetProperty("reflectanceF").GetDouble());
            Assert.Equal(0.79, glazing.GetProperty("transmittanceB").GetDouble());
            Assert.Equal(0.07, glazing.GetProperty("reflectanceB").GetDouble());
            // resistance はデフォルト 0.006
            Assert.Equal(0.006, glazing.GetProperty("resistance").GetDouble());
        }

        [Fact]
        public void Write_SurfaceFAndB_HaveTwoFieldsEach()
        {
            var json = JsonSerializer.Serialize(MakeSinglePaneWindow(), CreateOptions());
            using var doc = JsonDocument.Parse(json);
            var sf = doc.RootElement.GetProperty("surfaceF");
            var sb = doc.RootElement.GetProperty("surfaceB");

            Assert.Equal(2, CountProperties(sf));
            Assert.Equal(2, CountProperties(sb));
            Assert.Equal(18.5, sf.GetProperty("convectiveCoefficient").GetDouble());
            Assert.Equal(7.5, sb.GetProperty("convectiveCoefficient").GetDouble());
        }

        [Fact]
        public void Write_DefaultShadingDevices_OmittedWhenAllNone()
        {
            var json = JsonSerializer.Serialize(MakeSinglePaneWindow(), CreateOptions());
            using var doc = JsonDocument.Parse(json);

            Assert.False(doc.RootElement.TryGetProperty("shadingDevices", out _));
        }

        [Fact]
        public void Write_DefaultSunShade_OmittedWhenNone()
        {
            var json = JsonSerializer.Serialize(MakeSinglePaneWindow(), CreateOptions());
            using var doc = JsonDocument.Parse(json);

            Assert.False(doc.RootElement.TryGetProperty("sunShade", out _));
        }

        [Fact]
        public void Write_VenetianBlindPresent_ShadingDevicesArrayIncluded()
        {
            var window = MakeSinglePaneWindow();
            var vb = new VenetianBlind(25, 21, 0.05, 0.02, 0.6, 0.45);
            window.SetShadingDevice(1, vb); // B 側位置

            var json = JsonSerializer.Serialize(window, CreateOptions());
            using var doc = JsonDocument.Parse(json);
            var arr = doc.RootElement.GetProperty("shadingDevices");

            Assert.Equal(2, arr.GetArrayLength()); // GlazingCount + 1 = 2
            Assert.Equal("noShadingDevice", arr[0].GetProperty("kind").GetString());
            Assert.Equal("venetianBlind", arr[1].GetProperty("kind").GetString());
        }

        [Fact]
        public void Write_SunShadePresent_IncludedInOutput()
        {
            var window = MakeSinglePaneWindow();
            window.SunShade = SunShade.MakeHorizontalSunShade(
                1.5, 1.8, 0.6, 0.2, SouthVerticalIncline());

            var json = JsonSerializer.Serialize(window, CreateOptions());
            using var doc = JsonDocument.Parse(json);

            Assert.True(doc.RootElement.TryGetProperty("sunShade", out var ss));
            Assert.Equal("sunShade", ss.GetProperty("kind").GetString());
        }

        #endregion

        // ================================================================
        #region デシリアライズ

        [Fact]
        public void Read_MinimalSinglePane_Succeeds()
        {
            const string json = """
                {
                  "kind": "window",
                  "area": 2.0,
                  "outsideIncline": { "kind": "incline", "horizontalAngle": 0, "verticalAngle": 1.5708 },
                  "glazings": [
                    { "transmittanceF": 0.79, "reflectanceF": 0.07,
                      "transmittanceB": 0.79, "reflectanceB": 0.07,
                      "resistance": 0.006 }
                  ],
                  "airGapResistances": []
                }
                """;
            var window = JsonSerializer.Deserialize<Window>(json, CreateOptions())!;

            Assert.Equal(2.0, window.Area);
            Assert.Equal(1, window.GlazingCount);
            Assert.Equal(0.79, window.GetGlazingTransmittance(0, true));
        }

        [Fact]
        public void Read_MissingAirGapResistances_TreatedAsEmpty()
        {
            // single-glazed window では airGapResistances は省略可能
            const string json = """
                {
                  "kind": "window",
                  "area": 2.0,
                  "outsideIncline": { "kind": "incline", "horizontalAngle": 0, "verticalAngle": 1.5708 },
                  "glazings": [
                    { "transmittanceF": 0.79, "reflectanceF": 0.07,
                      "transmittanceB": 0.79, "reflectanceB": 0.07,
                      "resistance": 0.006 }
                  ]
                }
                """;
            var window = JsonSerializer.Deserialize<Window>(json, CreateOptions())!;
            Assert.Equal(1, window.GlazingCount);
        }

        [Fact]
        public void Read_MissingShadingDevices_UsesDefault()
        {
            var window = JsonSerializer.Deserialize<Window>("""
                {
                  "kind": "window", "area": 2.0,
                  "outsideIncline": { "kind": "incline", "horizontalAngle": 0, "verticalAngle": 1.5708 },
                  "glazings": [
                    { "transmittanceF": 0.79, "reflectanceF": 0.07,
                      "transmittanceB": 0.79, "reflectanceB": 0.07, "resistance": 0.006 }
                  ]
                }
                """, CreateOptions())!;

            // 全位置が NoShadingDevice (GlazingCount + 1 = 2 位置)
            Assert.IsType<NoShadingDevice>(window.GetShadingDevice(0));
            Assert.IsType<NoShadingDevice>(window.GetShadingDevice(1));
        }

        [Fact]
        public void Read_WithShadingDevices_DispatchesCorrectly()
        {
            const string json = """
                {
                  "kind": "window", "area": 2.0,
                  "outsideIncline": { "kind": "incline", "horizontalAngle": 0, "verticalAngle": 1.5708 },
                  "glazings": [
                    { "transmittanceF": 0.79, "reflectanceF": 0.07,
                      "transmittanceB": 0.79, "reflectanceB": 0.07, "resistance": 0.006 }
                  ],
                  "shadingDevices": [
                    { "kind": "noShadingDevice" },
                    { "kind": "simpleShadingDevice", "transmittance": 0.1, "reflectance": 0.3 }
                  ]
                }
                """;
            var window = JsonSerializer.Deserialize<Window>(json, CreateOptions())!;

            Assert.IsType<NoShadingDevice>(window.GetShadingDevice(0));
            var simple = Assert.IsType<SimpleShadingDevice>(window.GetShadingDevice(1));
            Assert.Equal(0.1, simple.Transmittance);
        }

        [Fact]
        public void Read_WithSunShade_AttachedToWindow()
        {
            const string json = """
                {
                  "kind": "window", "area": 2.0,
                  "outsideIncline": { "kind": "incline", "horizontalAngle": 0, "verticalAngle": 1.5708 },
                  "glazings": [
                    { "transmittanceF": 0.79, "reflectanceF": 0.07,
                      "transmittanceB": 0.79, "reflectanceB": 0.07, "resistance": 0.006 }
                  ],
                  "sunShade": {
                    "kind": "sunShade", "shape": "Horizontal",
                    "incline": { "kind": "incline", "horizontalAngle": 0, "verticalAngle": 1.5708 },
                    "winHeight": 1.8, "winWidth": 1.5, "overhang": 0.6,
                    "topMargin": 0.2, "bottomMargin": 0, "leftMargin": 0.1, "rightMargin": 0.1
                  }
                }
                """;
            var window = JsonSerializer.Deserialize<Window>(json, CreateOptions())!;
            Assert.Equal(SunShade.Shapes.Horizontal, window.SunShade.Shape);
        }

        #endregion

        // ================================================================
        #region ラウンドトリップ

        [Fact]
        public void RoundTrip_SinglePane_PreservesBasicFields()
        {
            var original = MakeSinglePaneWindow();
            var restored = Roundtrip(original);

            Assert.Equal(original.Area, restored.Area);
            Assert.Equal(original.GlazingCount, restored.GlazingCount);
            Assert.Equal(original.GetGlazingTransmittance(0, true), restored.GetGlazingTransmittance(0, true));
            Assert.Equal(original.GetGlazingReflectance(0, true), restored.GetGlazingReflectance(0, true));
            Assert.Equal(original.GetGlassResistance(0), restored.GetGlassResistance(0));
        }

        [Fact]
        public void RoundTrip_DoubleGlazed_PreservesAirGapResistance()
        {
            var original = MakeDoubleGlazedWindow();
            original.SetAirGapResistance(0, 0.15);
            var restored = Roundtrip(original);

            Assert.InRange(restored.GetAirGapResistance(0),
                0.15 * 0.9999, 0.15 * 1.0001);
        }

        [Fact]
        public void RoundTrip_SurfaceCoefficients_Preserved()
        {
            var original = MakeSinglePaneWindow();
            original.ConvectiveCoefficientF = 20.0;
            original.ConvectiveCoefficientB = 8.0;
            original.LongWaveEmissivityF = 0.85;
            original.LongWaveEmissivityB = 0.88;

            var restored = Roundtrip(original);

            Assert.Equal(20.0, restored.ConvectiveCoefficientF);
            Assert.Equal(8.0, restored.ConvectiveCoefficientB);
            Assert.Equal(0.85, restored.LongWaveEmissivityF);
            Assert.Equal(0.88, restored.LongWaveEmissivityB);
        }

        [Fact]
        public void RoundTrip_VenetianBlindShading_Preserved()
        {
            var original = MakeSinglePaneWindow();
            var vb = new VenetianBlind(25, 21, 0.05, 0.02, 0.6, 0.45);
            vb.SlatAngle = 0.5;
            original.SetShadingDevice(1, vb);

            var restored = Roundtrip(original);

            var restoredVb = Assert.IsType<VenetianBlind>(restored.GetShadingDevice(1));
            Assert.Equal(25.0, restoredVb.SlatWidth);
            Assert.Equal(0.5, restoredVb.SlatAngle);
        }

        [Fact]
        public void RoundTrip_SunShade_Preserved()
        {
            var original = MakeSinglePaneWindow();
            original.SunShade = SunShade.MakeHorizontalSunShade(
                wWidth: 1.5, wHeight: 1.8, depth: 0.6,
                tMargin: 0.2, SouthVerticalIncline());

            var restored = Roundtrip(original);

            Assert.Equal(SunShade.Shapes.LongHorizontal, restored.SunShade.Shape);
            Assert.Equal(0.6, restored.SunShade.Overhang);
        }

        [Fact]
        public void RoundTrip_OutsideIncline_Preserved()
        {
            var original = new Window(2.0,
                new double[] { 0.79 }, new double[] { 0.07 },
                new Incline(-Math.PI / 4, Math.PI / 2));

            var restored = Roundtrip(original);

            Assert.InRange(restored.OutsideIncline.HorizontalAngle,
                -Math.PI / 4 - 1e-12, -Math.PI / 4 + 1e-12);
        }

        #endregion

        // ================================================================
        #region エラー処理

        [Fact]
        public void Read_MissingKind_Throws()
        {
            const string json = """
                { "area": 2.0, "outsideIncline": { "kind": "incline", "horizontalAngle": 0, "verticalAngle": 1.5708 },
                  "glazings": [{ "transmittanceF": 0.79, "reflectanceF": 0.07, "transmittanceB": 0.79, "reflectanceB": 0.07, "resistance": 0.006 }] }
                """;
            Assert.Throws<JsonException>(() =>
                JsonSerializer.Deserialize<Window>(json, CreateOptions()));
        }

        [Fact]
        public void Read_WrongKind_Throws()
        {
            const string json = """
                { "kind": "wall", "area": 2.0,
                  "outsideIncline": { "kind": "incline", "horizontalAngle": 0, "verticalAngle": 1.5708 },
                  "glazings": [{ "transmittanceF": 0.79, "reflectanceF": 0.07, "transmittanceB": 0.79, "reflectanceB": 0.07, "resistance": 0.006 }] }
                """;
            Assert.Throws<JsonException>(() =>
                JsonSerializer.Deserialize<Window>(json, CreateOptions()));
        }

        [Fact]
        public void Read_MissingArea_Throws()
        {
            const string json = """
                { "kind": "window",
                  "outsideIncline": { "kind": "incline", "horizontalAngle": 0, "verticalAngle": 1.5708 },
                  "glazings": [{ "transmittanceF": 0.79, "reflectanceF": 0.07, "transmittanceB": 0.79, "reflectanceB": 0.07, "resistance": 0.006 }] }
                """;
            Assert.Throws<JsonException>(() =>
                JsonSerializer.Deserialize<Window>(json, CreateOptions()));
        }

        [Fact]
        public void Read_MissingGlazings_Throws()
        {
            const string json = """
                { "kind": "window", "area": 2.0,
                  "outsideIncline": { "kind": "incline", "horizontalAngle": 0, "verticalAngle": 1.5708 } }
                """;
            Assert.Throws<JsonException>(() =>
                JsonSerializer.Deserialize<Window>(json, CreateOptions()));
        }

        [Fact]
        public void Read_EmptyGlazings_Throws()
        {
            const string json = """
                { "kind": "window", "area": 2.0,
                  "outsideIncline": { "kind": "incline", "horizontalAngle": 0, "verticalAngle": 1.5708 },
                  "glazings": [] }
                """;
            Assert.Throws<JsonException>(() =>
                JsonSerializer.Deserialize<Window>(json, CreateOptions()));
        }

        [Fact]
        public void Read_WrongAirGapCount_Throws()
        {
            // Double-glazed (glazings=2) but airGapResistances has 2 entries (should be 1)
            const string json = """
                { "kind": "window", "area": 2.7,
                  "outsideIncline": { "kind": "incline", "horizontalAngle": 0, "verticalAngle": 1.5708 },
                  "glazings": [
                    { "transmittanceF": 0.79, "reflectanceF": 0.07, "transmittanceB": 0.79, "reflectanceB": 0.07, "resistance": 0.006 },
                    { "transmittanceF": 0.79, "reflectanceF": 0.07, "transmittanceB": 0.79, "reflectanceB": 0.07, "resistance": 0.006 }
                  ],
                  "airGapResistances": [0.1, 0.2] }
                """;
            Assert.Throws<JsonException>(() =>
                JsonSerializer.Deserialize<Window>(json, CreateOptions()));
        }

        [Fact]
        public void Read_WrongShadingDeviceCount_Throws()
        {
            // Single pane → shadingDevices must be 2 entries; this provides 1
            const string json = """
                { "kind": "window", "area": 2.0,
                  "outsideIncline": { "kind": "incline", "horizontalAngle": 0, "verticalAngle": 1.5708 },
                  "glazings": [
                    { "transmittanceF": 0.79, "reflectanceF": 0.07, "transmittanceB": 0.79, "reflectanceB": 0.07, "resistance": 0.006 }
                  ],
                  "shadingDevices": [ { "kind": "noShadingDevice" } ] }
                """;
            Assert.Throws<JsonException>(() =>
                JsonSerializer.Deserialize<Window>(json, CreateOptions()));
        }

        [Fact]
        public void Read_UnknownShadingDeviceKind_Throws()
        {
            const string json = """
                { "kind": "window", "area": 2.0,
                  "outsideIncline": { "kind": "incline", "horizontalAngle": 0, "verticalAngle": 1.5708 },
                  "glazings": [
                    { "transmittanceF": 0.79, "reflectanceF": 0.07, "transmittanceB": 0.79, "reflectanceB": 0.07, "resistance": 0.006 }
                  ],
                  "shadingDevices": [ { "kind": "quantumBlind" }, { "kind": "noShadingDevice" } ] }
                """;
            Assert.Throws<JsonException>(() =>
                JsonSerializer.Deserialize<Window>(json, CreateOptions()));
        }

        [Fact]
        public void Read_GlazingMissingRequiredField_Throws()
        {
            // transmittanceB を欠落
            const string json = """
                { "kind": "window", "area": 2.0,
                  "outsideIncline": { "kind": "incline", "horizontalAngle": 0, "verticalAngle": 1.5708 },
                  "glazings": [
                    { "transmittanceF": 0.79, "reflectanceF": 0.07, "reflectanceB": 0.07, "resistance": 0.006 }
                  ] }
                """;
            Assert.Throws<JsonException>(() =>
                JsonSerializer.Deserialize<Window>(json, CreateOptions()));
        }

        [Fact]
        public void Read_NotAnObject_Throws()
        {
            Assert.Throws<JsonException>(() =>
                JsonSerializer.Deserialize<Window>("[1,2]", CreateOptions()));
        }

        #endregion
    }
}
