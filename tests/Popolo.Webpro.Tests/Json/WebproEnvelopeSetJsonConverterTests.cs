/* WebproEnvelopeSetJsonConverterTests.cs
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
using Popolo.Webpro.Json.EnumConverters;

namespace Popolo.Webpro.Tests.Json
{
    /// <summary>Unit tests for <see cref="WebproEnvelopeSetJsonConverter"/>.</summary>
    public class WebproEnvelopeSetJsonConverterTests
    {
        private static JsonSerializerOptions CreateOptions()
        {
            var opts = new JsonSerializerOptions();
            opts.Converters.Add(new WebproEnvelopeSetJsonConverter());
            opts.Converters.Add(new WebproWallJsonConverter());
            opts.Converters.Add(new WebproWindowJsonConverter());
            opts.Converters.Add(new OrientationJsonConverter());
            opts.Converters.Add(new WallTypeJsonConverter());
            return opts;
        }

        // ================================================================
        #region 正常ケース

        [Fact]
        public void Read_RealSample_1F_Lobby()
        {
            // builelib_input.json の "1F_ロビー" そのもの
            const string json = """
                {
                  "isAirconditioned": "有",
                  "WallList": [
                    {
                      "Direction":      "南",
                      "EnvelopeArea":   50.0,
                      "EnvelopeWidth":  null,
                      "EnvelopeHeight": null,
                      "WallSpec":       "W1",
                      "WallType":       "日の当たる外壁",
                      "WindowList": [
                        { "WindowID": "G1", "WindowNumber": 16.64, "isBlind": "無", "EavesID": "無", "Info": null }
                      ]
                    },
                    {
                      "Direction":      "北",
                      "EnvelopeArea":   114.12,
                      "EnvelopeWidth":  null,
                      "EnvelopeHeight": null,
                      "WallSpec":       "FG1",
                      "WallType":       "地盤に接する外壁",
                      "WindowList": [
                        { "WindowID": "無", "WindowNumber": null, "isBlind": "無", "EavesID": "無", "Info": null }
                      ]
                    }
                  ]
                }
                """;
            var e = JsonSerializer.Deserialize<WebproEnvelopeSet>(json, CreateOptions())!;

            Assert.True(e.IsAirconditioned);
            Assert.Equal(2, e.Walls.Count);
            Assert.Equal("W1", e.Walls[0].WallSpec);
            Assert.Equal("FG1", e.Walls[1].WallSpec);
        }

        [Fact]
        public void Read_IsAirconditionedYes_True()
        {
            const string json = """{ "isAirconditioned": "有", "WallList": [] }""";
            var e = JsonSerializer.Deserialize<WebproEnvelopeSet>(json, CreateOptions())!;
            Assert.True(e.IsAirconditioned);
        }

        [Fact]
        public void Read_IsAirconditionedNo_False()
        {
            const string json = """{ "isAirconditioned": "無", "WallList": [] }""";
            var e = JsonSerializer.Deserialize<WebproEnvelopeSet>(json, CreateOptions())!;
            Assert.False(e.IsAirconditioned);
        }

        [Fact]
        public void Read_IsAirconditionedNull_False()
        {
            const string json = """{ "isAirconditioned": null, "WallList": [] }""";
            var e = JsonSerializer.Deserialize<WebproEnvelopeSet>(json, CreateOptions())!;
            Assert.False(e.IsAirconditioned);
        }

        [Fact]
        public void Read_EmptyWallList()
        {
            const string json = """{ "isAirconditioned": "有", "WallList": [] }""";
            var e = JsonSerializer.Deserialize<WebproEnvelopeSet>(json, CreateOptions())!;
            Assert.Empty(e.Walls);
        }

        [Fact]
        public void Read_UnknownPropertyIgnored()
        {
            const string json = """
                {
                  "isAirconditioned": "有",
                  "WallList":         [],
                  "someExtra":        "x"
                }
                """;
            var e = JsonSerializer.Deserialize<WebproEnvelopeSet>(json, CreateOptions())!;
            Assert.True(e.IsAirconditioned);
        }

        #endregion

        // ================================================================
        #region エラー処理

        [Fact]
        public void Read_MissingIsAirconditioned_Throws()
        {
            const string json = """{ "WallList": [] }""";
            Assert.Throws<JsonException>(() =>
                JsonSerializer.Deserialize<WebproEnvelopeSet>(json, CreateOptions()));
        }

        [Fact]
        public void Read_MissingWallList_Throws()
        {
            const string json = """{ "isAirconditioned": "有" }""";
            Assert.Throws<JsonException>(() =>
                JsonSerializer.Deserialize<WebproEnvelopeSet>(json, CreateOptions()));
        }

        [Fact]
        public void Read_NonObjectRoot_Throws()
        {
            Assert.Throws<JsonException>(() =>
                JsonSerializer.Deserialize<WebproEnvelopeSet>("42", CreateOptions()));
        }

        [Fact]
        public void Write_Throws()
        {
            var e = new WebproEnvelopeSet();
            Assert.Throws<NotSupportedException>(() =>
                JsonSerializer.Serialize(e, CreateOptions()));
        }

        #endregion
    }
}
