/* WebproWindowConfigureJsonConverterTests.cs
 *
 * Copyright (C) 2026 E.Togashi
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
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
    /// <summary>Unit tests for <see cref="WebproWindowConfigurationJsonConverter"/>.</summary>
    public class WebproWindowConfigureJsonConverterTests
    {
        #region Helpers

        private static JsonSerializerOptions CreateOptions()
        {
            var opts = new JsonSerializerOptions();
            opts.Converters.Add(new WebproWindowConfigurationJsonConverter());
            opts.Converters.Add(new WindowInputMethodJsonConverter());
            opts.Converters.Add(new WindowFrameJsonConverter());
            return opts;
        }

        #endregion

        // ================================================================
        #region Normal cases - similar to real samples

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
            var wnc = JsonSerializer.Deserialize<WebproWindowConfiguration>(json, CreateOptions())!;

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
            var wnc = JsonSerializer.Deserialize<WebproWindowConfiguration>(json, CreateOptions())!;

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
            var wnc = JsonSerializer.Deserialize<WebproWindowConfiguration>(json, CreateOptions())!;

            Assert.Equal(WindowInputMethod.FrameTypeAndGlazingSpec, wnc.Method);
            Assert.Equal(WindowFrame.Resin, wnc.Frame);
            Assert.Equal(1.3, wnc.GlazingHeatTransferCoefficient);
            Assert.Equal(0.4, wnc.GlazingSolarHeatGainRate);
        }

        [Fact]
        public void Read_LayerTypeSingle_SetsIsSingleGlazingTrue()
        {
            const string json = """{ "layerType": "単層" }""";
            var wnc = JsonSerializer.Deserialize<WebproWindowConfiguration>(json, CreateOptions())!;
            Assert.True(wnc.IsSingleGlazing);
        }

        [Fact]
        public void Read_LayerTypeMulti_SetsIsSingleGlazingFalse()
        {
            const string json = """{ "layerType": "複層" }""";
            var wnc = JsonSerializer.Deserialize<WebproWindowConfiguration>(json, CreateOptions())!;
            Assert.False(wnc.IsSingleGlazing);
        }

        [Fact]
        public void Read_LayerTypeNull_SetsIsSingleGlazingFalse()
        {
            const string json = """{ "layerType": null }""";
            var wnc = JsonSerializer.Deserialize<WebproWindowConfiguration>(json, CreateOptions())!;
            Assert.False(wnc.IsSingleGlazing);
        }

        [Fact]
        public void Read_MinimalJson_DefaultsApplied()
        {
            const string json = "{ }";
            var wnc = JsonSerializer.Deserialize<WebproWindowConfiguration>(json, CreateOptions())!;

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
            var wnc = JsonSerializer.Deserialize<WebproWindowConfiguration>(json, CreateOptions())!;
            Assert.Equal(1, wnc.Area);
        }

        #endregion

        // ================================================================
        #region Error handling

        [Fact]
        public void Read_NonObjectRoot_Throws()
        {
            Assert.Throws<JsonException>(() =>
                JsonSerializer.Deserialize<WebproWindowConfiguration>("[]", CreateOptions()));
        }

        [Fact]
        public void Write_Throws()
        {
            var wnc = new WebproWindowConfiguration();
            Assert.Throws<NotSupportedException>(() =>
                JsonSerializer.Serialize(wnc, CreateOptions()));
        }

        #endregion
    }
}
