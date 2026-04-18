/* WebproWallConfigureJsonConverterTests.cs
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
    /// <summary>Unit tests for <see cref="WebproWallConfigureJsonConverter"/>.</summary>
    public class WebproWallConfigureJsonConverterTests
    {
        #region ヘルパー

        private static JsonSerializerOptions CreateOptions()
        {
            var opts = new JsonSerializerOptions();
            opts.Converters.Add(new WebproWallConfigureJsonConverter());
            opts.Converters.Add(new WebproWallLayerJsonConverter());
            opts.Converters.Add(new StructureTypeJsonConverter());
            opts.Converters.Add(new WallInputMethodJsonConverter());
            return opts;
        }

        #endregion

        // ================================================================
        #region 正常ケース - 実サンプル類似

        [Fact]
        public void Read_RealSample_R1()
        {
            // builelib_input.json の "R1" エントリそのもの
            const string json = """
                {
                  "wall_type_webpro": "外壁",
                  "structureType":    "その他",
                  "solarAbsorptionRatio": null,
                  "inputMethod":      "建材構成を入力",
                  "layers": [
                    { "materialID": "ロックウール化粧吸音板", "conductivity": null, "thickness": 12.0, "Info": null },
                    { "materialID": "せっこうボード",         "conductivity": null, "thickness": 10.0, "Info": null },
                    { "materialID": "非密閉中空層",           "conductivity": null, "thickness": null, "Info": null },
                    { "materialID": "コンクリート",           "conductivity": null, "thickness": 150.0, "Info": null }
                  ]
                }
                """;
            var wc = JsonSerializer.Deserialize<WebproWallConfigure>(json, CreateOptions())!;

            Assert.Equal(StructureType.Others, wc.Structure);
            Assert.Null(wc.SolarAbsorptionRatio);
            Assert.Equal(WallInputMethod.MaterialNumberAndThickness, wc.Method);
            Assert.Equal(4, wc.Layers.Count);
            Assert.Equal("ロックウール化粧吸音板", wc.Layers[0].MaterialID);
            Assert.Equal(12.0, wc.Layers[0].Thickness);
        }

        [Fact]
        public void Read_WallTypeWebpro_IsIgnored()
        {
            // wall_type_webpro キーは明示的に無視される
            const string json = """
                {
                  "wall_type_webpro": "接地壁",
                  "structureType":    "その他",
                  "solarAbsorptionRatio": null,
                  "inputMethod":      "建材構成を入力",
                  "layers": []
                }
                """;
            var wc = JsonSerializer.Deserialize<WebproWallConfigure>(json, CreateOptions())!;

            // 読み込めることのみ検証(wall_type_webpro は DTO にない)
            Assert.Equal(WallInputMethod.MaterialNumberAndThickness, wc.Method);
        }

        [Fact]
        public void Read_NullStructureType_BecomesNone()
        {
            const string json = """
                {
                  "structureType": null,
                  "inputMethod":   "熱貫流率を入力",
                  "layers":        []
                }
                """;
            var wc = JsonSerializer.Deserialize<WebproWallConfigure>(json, CreateOptions())!;
            Assert.Equal(StructureType.None, wc.Structure);
        }

        [Fact]
        public void Read_NullInputMethod_BecomesNone()
        {
            const string json = """
                {
                  "structureType": "木造",
                  "inputMethod":   null,
                  "layers":        []
                }
                """;
            var wc = JsonSerializer.Deserialize<WebproWallConfigure>(json, CreateOptions())!;
            Assert.Equal(WallInputMethod.None, wc.Method);
        }

        [Fact]
        public void Read_SolarAbsorptionRatio_Number()
        {
            const string json = """
                {
                  "structureType":        "その他",
                  "solarAbsorptionRatio": 0.7,
                  "inputMethod":          "熱貫流率を入力",
                  "layers":               []
                }
                """;
            var wc = JsonSerializer.Deserialize<WebproWallConfigure>(json, CreateOptions())!;
            Assert.Equal(0.7, wc.SolarAbsorptionRatio);
        }

        [Fact]
        public void Read_EmptyLayers()
        {
            const string json = """
                {
                  "structureType": "その他",
                  "inputMethod":   "熱貫流率を入力",
                  "layers":        []
                }
                """;
            var wc = JsonSerializer.Deserialize<WebproWallConfigure>(json, CreateOptions())!;
            Assert.Empty(wc.Layers);
        }

        [Fact]
        public void Read_InfoField()
        {
            const string json = """
                {
                  "structureType": "その他",
                  "inputMethod":   "熱貫流率を入力",
                  "layers":        [],
                  "Info":          "note"
                }
                """;
            var wc = JsonSerializer.Deserialize<WebproWallConfigure>(json, CreateOptions())!;
            Assert.Equal("note", wc.Information);
        }

        [Fact]
        public void Read_UnknownPropertyIgnored()
        {
            const string json = """
                {
                  "structureType":    "その他",
                  "inputMethod":      "熱貫流率を入力",
                  "layers":           [],
                  "mysteriousField":  42,
                  "nestedThing":      { "a": 1 }
                }
                """;
            var wc = JsonSerializer.Deserialize<WebproWallConfigure>(json, CreateOptions())!;
            Assert.Equal(StructureType.Others, wc.Structure);
        }

        #endregion

        // ================================================================
        #region エラー処理

        [Fact]
        public void Read_NonObjectRoot_Throws()
        {
            Assert.Throws<JsonException>(() =>
                JsonSerializer.Deserialize<WebproWallConfigure>("[]", CreateOptions()));
        }

        [Fact]
        public void Write_Throws()
        {
            var wc = new WebproWallConfigure();
            Assert.Throws<NotSupportedException>(() =>
                JsonSerializer.Serialize(wc, CreateOptions()));
        }

        #endregion
    }
}
