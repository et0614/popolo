/* WebproWallLayerJsonConverterTests.cs
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
using Popolo.Webpro.Json;

namespace Popolo.Webpro.Tests.Json
{
    /// <summary>Unit tests for <see cref="WebproWallLayerJsonConverter"/>.</summary>
    public class WebproWallLayerJsonConverterTests
    {
        #region Helpers

        private static JsonSerializerOptions CreateOptions()
        {
            var opts = new JsonSerializerOptions();
            opts.Converters.Add(new WebproWallLayerJsonConverter());
            return opts;
        }

        #endregion

        // ================================================================
        #region Normal cases

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
        #region Error handling

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
