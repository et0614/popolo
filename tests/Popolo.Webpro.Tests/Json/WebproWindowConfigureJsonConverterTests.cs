/* WebproWindowConfigureJsonConverterTests.cs
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

using Popolo.Webpro.Domain;
using Popolo.Webpro.Domain.Enums;
using Popolo.Webpro.Json;
using Popolo.Webpro.Json.EnumConverters;

namespace Popolo.Webpro.Tests.Json
{
    /// <summary>Unit tests for <see cref="WebproWindowConfigureJsonConverter"/>.</summary>
    public class WebproWindowConfigureJsonConverterTests
    {
        #region ヘルパー

        private static JsonSerializerOptions CreateOptions()
        {
            var opts = new JsonSerializerOptions();
            opts.Converters.Add(new WebproWindowConfigureJsonConverter());
            opts.Converters.Add(new WindowInputMethodJsonConverter());
            opts.Converters.Add(new WindowFrameJsonConverter());
            return opts;
        }

        #endregion

        // ================================================================
        #region 正常ケース - 実サンプル類似

        [Fact]
        public void Read_RealSample_G1()
        {
            // builelib_input.json の "G1" エントリそのもの
            const string json = """
                {
                  "windowArea":   1,
                  "windowWidth":  null,
                  "windowHeight": null,
                  "inputMethod":  "ガラスの種類を入力",
                  "frameType":    "金属木複合製",
                  "layerType":    "単層",
                  "glassID":      "T",
                  "Info":         null
                }
                """;
            var wnc = JsonSerializer.Deserialize<WebproWindowConfigure>(json, CreateOptions())!;

            Assert.Equal(1, wnc.Area);
            Assert.True(double.IsNaN(wnc.Width));
            Assert.True(double.IsNaN(wnc.Height));
            Assert.Equal(WindowInputMethod.FrameAndGlazingType, wnc.Method);
            Assert.Equal(WindowFrame.MetalAndWood, wnc.Frame);
            Assert.True(wnc.IsSingleGlazing);
            Assert.Equal("T", wnc.GlazingID);
            Assert.Null(wnc.Information);
        }

        [Fact]
        public void Read_WindowSpecMethod_SetsWindowUvalueAndIvalue()
        {
            const string json = """
                {
                  "windowArea":    2,
                  "windowWidth":   1,
                  "windowHeight":  2,
                  "inputMethod":   "性能値を入力",
                  "frameType":     null,
                  "layerType":     "複層",
                  "glassID":       "",
                  "glassUvalue":   null,
                  "glassIvalue":   null,
                  "windowUvalue":  2.5,
                  "windowIvalue":  0.6,
                  "Info":          null
                }
                """;
            var wnc = JsonSerializer.Deserialize<WebproWindowConfigure>(json, CreateOptions())!;

            Assert.Equal(WindowInputMethod.WindowSpec, wnc.Method);
            Assert.Equal(WindowFrame.None, wnc.Frame);
            Assert.False(wnc.IsSingleGlazing); // "複層"
            Assert.True(double.IsNaN(wnc.GlazingHeatTransferCoefficient));
            Assert.True(double.IsNaN(wnc.GlazingSolarHeatGainRate));
            Assert.Equal(2.5, wnc.WindowHeatTransferCoefficient);
            Assert.Equal(0.6, wnc.WindowSolarHeatGainRate);
        }

        [Fact]
        public void Read_FrameTypeAndGlazingSpec()
        {
            const string json = """
                {
                  "windowArea":    1,
                  "inputMethod":   "ガラスの性能を入力",
                  "frameType":     "樹脂製",
                  "layerType":     "複層",
                  "glassID":       "",
                  "glassUvalue":   1.3,
                  "glassIvalue":   0.4,
                  "windowUvalue":  null,
                  "windowIvalue":  null
                }
                """;
            var wnc = JsonSerializer.Deserialize<WebproWindowConfigure>(json, CreateOptions())!;

            Assert.Equal(WindowInputMethod.FrameTypeAndGlazingSpec, wnc.Method);
            Assert.Equal(WindowFrame.Resin, wnc.Frame);
            Assert.Equal(1.3, wnc.GlazingHeatTransferCoefficient);
            Assert.Equal(0.4, wnc.GlazingSolarHeatGainRate);
        }

        [Fact]
        public void Read_LayerTypeSingle_SetsIsSingleGlazingTrue()
        {
            const string json = """{ "layerType": "単層" }""";
            var wnc = JsonSerializer.Deserialize<WebproWindowConfigure>(json, CreateOptions())!;
            Assert.True(wnc.IsSingleGlazing);
        }

        [Fact]
        public void Read_LayerTypeMulti_SetsIsSingleGlazingFalse()
        {
            const string json = """{ "layerType": "複層" }""";
            var wnc = JsonSerializer.Deserialize<WebproWindowConfigure>(json, CreateOptions())!;
            Assert.False(wnc.IsSingleGlazing);
        }

        [Fact]
        public void Read_LayerTypeNull_SetsIsSingleGlazingFalse()
        {
            const string json = """{ "layerType": null }""";
            var wnc = JsonSerializer.Deserialize<WebproWindowConfigure>(json, CreateOptions())!;
            Assert.False(wnc.IsSingleGlazing);
        }

        [Fact]
        public void Read_MinimalJson_DefaultsApplied()
        {
            const string json = "{ }";
            var wnc = JsonSerializer.Deserialize<WebproWindowConfigure>(json, CreateOptions())!;

            Assert.Equal(0, wnc.Area);
            Assert.Equal(0, wnc.Width);
            Assert.Equal(0, wnc.Height);
            Assert.Equal(WindowInputMethod.None, wnc.Method);
            Assert.Equal(WindowFrame.None, wnc.Frame);
            Assert.False(wnc.IsSingleGlazing);
            Assert.Equal("", wnc.GlazingID);
            Assert.True(double.IsNaN(wnc.GlazingHeatTransferCoefficient));
            Assert.True(double.IsNaN(wnc.GlazingSolarHeatGainRate));
            Assert.True(double.IsNaN(wnc.WindowHeatTransferCoefficient));
            Assert.True(double.IsNaN(wnc.WindowSolarHeatGainRate));
        }

        [Fact]
        public void Read_UnknownPropertyIgnored()
        {
            const string json = """{ "windowArea": 1, "futureField": "x" }""";
            var wnc = JsonSerializer.Deserialize<WebproWindowConfigure>(json, CreateOptions())!;
            Assert.Equal(1, wnc.Area);
        }

        #endregion

        // ================================================================
        #region エラー処理

        [Fact]
        public void Read_NonObjectRoot_Throws()
        {
            Assert.Throws<JsonException>(() =>
                JsonSerializer.Deserialize<WebproWindowConfigure>("[]", CreateOptions()));
        }

        [Fact]
        public void Write_Throws()
        {
            var wnc = new WebproWindowConfigure();
            Assert.Throws<NotSupportedException>(() =>
                JsonSerializer.Serialize(wnc, CreateOptions()));
        }

        #endregion
    }
}
