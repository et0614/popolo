/* WebproWallLayerJsonConverterTests.cs
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
using Popolo.Webpro.Json;

namespace Popolo.Webpro.Tests.Json
{
    /// <summary>Unit tests for <see cref="WebproWallLayerJsonConverter"/>.</summary>
    public class WebproWallLayerJsonConverterTests
    {
        #region ヘルパー

        private static JsonSerializerOptions CreateOptions()
        {
            var opts = new JsonSerializerOptions();
            opts.Converters.Add(new WebproWallLayerJsonConverter());
            return opts;
        }

        #endregion

        // ================================================================
        #region 正常ケース

        [Fact]
        public void Read_FullyPopulated()
        {
            const string json = """
                {
                  "materialID":   "コンクリート",
                  "conductivity": 1.6,
                  "thickness":    150.0,
                  "Info":         "some note"
                }
                """;
            var layer = JsonSerializer.Deserialize<WebproWallLayer>(json, CreateOptions())!;

            Assert.Equal("コンクリート", layer.MaterialID);
            Assert.Equal(1.6, layer.Conductivity);
            Assert.Equal(150.0, layer.Thickness);
            Assert.Equal("some note", layer.Information);
        }

        [Fact]
        public void Read_AllOptionalFieldsNull()
        {
            const string json = """
                {
                  "materialID":   "せっこうボード",
                  "conductivity": null,
                  "thickness":    null,
                  "Info":         null
                }
                """;
            var layer = JsonSerializer.Deserialize<WebproWallLayer>(json, CreateOptions())!;

            Assert.Equal("せっこうボード", layer.MaterialID);
            Assert.Null(layer.Conductivity);
            Assert.Null(layer.Thickness);
            Assert.Null(layer.Information);
        }

        [Fact]
        public void Read_OnlyMaterialId()
        {
            // 省略されたキーは無視されて default(null) のまま
            const string json = """{ "materialID": "非密閉中空層" }""";
            var layer = JsonSerializer.Deserialize<WebproWallLayer>(json, CreateOptions())!;

            Assert.Equal("非密閉中空層", layer.MaterialID);
            Assert.Null(layer.Conductivity);
            Assert.Null(layer.Thickness);
            Assert.Null(layer.Information);
        }

        [Fact]
        public void Read_AirGapStyle_ThicknessNullOk()
        {
            // WallConfigure で空気層が出てくるときに thickness=null となる実パターン
            const string json = """
                {
                  "materialID":   "非密閉中空層",
                  "conductivity": null,
                  "thickness":    null,
                  "Info":         null
                }
                """;
            var layer = JsonSerializer.Deserialize<WebproWallLayer>(json, CreateOptions())!;

            Assert.Equal("非密閉中空層", layer.MaterialID);
            Assert.Null(layer.Thickness);
        }

        [Fact]
        public void Read_UnknownPropertyIgnored()
        {
            const string json = """
                {
                  "materialID":   "タイル",
                  "thickness":    10,
                  "futureField":  "x",
                  "nested":       { "a": 1, "b": 2 }
                }
                """;
            var layer = JsonSerializer.Deserialize<WebproWallLayer>(json, CreateOptions())!;

            Assert.Equal("タイル", layer.MaterialID);
            Assert.Equal(10, layer.Thickness);
        }

        #endregion

        // ================================================================
        #region エラー処理

        [Fact]
        public void Read_MissingMaterialId_Throws()
        {
            const string json = """
                {
                  "conductivity": 1,
                  "thickness":    150
                }
                """;
            Assert.Throws<JsonException>(() =>
                JsonSerializer.Deserialize<WebproWallLayer>(json, CreateOptions()));
        }

        [Fact]
        public void Read_NonObjectRoot_Throws()
        {
            Assert.Throws<JsonException>(() =>
                JsonSerializer.Deserialize<WebproWallLayer>("[]", CreateOptions()));
        }

        [Fact]
        public void Read_MaterialIdNotString_Throws()
        {
            const string json = """{ "materialID": 42 }""";
            Assert.Throws<JsonException>(() =>
                JsonSerializer.Deserialize<WebproWallLayer>(json, CreateOptions()));
        }

        [Fact]
        public void Write_Throws()
        {
            var layer = new WebproWallLayer { MaterialID = "X" };
            Assert.Throws<NotSupportedException>(() =>
                JsonSerializer.Serialize(layer, CreateOptions()));
        }

        #endregion
    }
}
