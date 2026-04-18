/* WebproWallJsonConverterTests.cs
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
    /// <summary>Unit tests for <see cref="WebproWallJsonConverter"/>.</summary>
    public class WebproWallJsonConverterTests
    {
        private static JsonSerializerOptions CreateOptions()
        {
            var opts = new JsonSerializerOptions();
            opts.Converters.Add(new WebproWallJsonConverter());
            opts.Converters.Add(new WebproWindowJsonConverter());
            opts.Converters.Add(new OrientationJsonConverter());
            opts.Converters.Add(new WallTypeJsonConverter());
            return opts;
        }

        // ================================================================
        #region 正常ケース - 実サンプル

        [Fact]
        public void Read_RealSample_SouthWall_WithWindow()
        {
            // builelib_input.json の "1F_ロビー" の WallList[0] そのもの
            const string json = """
                {
                  "Direction":      "南",
                  "EnvelopeArea":   50.0,
                  "EnvelopeWidth":  null,
                  "EnvelopeHeight": null,
                  "WallSpec":       "W1",
                  "WallType":       "日の当たる外壁",
                  "WindowList": [
                    {
                      "WindowID":     "G1",
                      "WindowNumber": 16.64,
                      "isBlind":      "無",
                      "EavesID":      "無",
                      "Info":         null
                    }
                  ]
                }
                """;
            var w = JsonSerializer.Deserialize<WebproWall>(json, CreateOptions())!;

            Assert.Equal(Orientation.S, w.SurfaceOrientation);
            Assert.Equal(50.0, w.Area);
            Assert.Null(w.Width);
            Assert.Null(w.Height);
            Assert.Equal("W1", w.WallSpec);
            Assert.Equal(WallType.ExternalWall, w.Type);
            Assert.True(double.IsNaN(w.HeatTransferCoefficient));
            Assert.Single(w.Windows);
            Assert.Equal("G1", w.Windows[0].ID);
            Assert.Equal(16.64, w.Windows[0].Number);
        }

        [Fact]
        public void Read_RealSample_NorthGroundWall_NoWindow()
        {
            const string json = """
                {
                  "Direction":      "北",
                  "EnvelopeArea":   114.12,
                  "EnvelopeWidth":  null,
                  "EnvelopeHeight": null,
                  "WallSpec":       "FG1",
                  "WallType":       "地盤に接する外壁",
                  "WindowList": [
                    {
                      "WindowID":     "無",
                      "WindowNumber": null,
                      "isBlind":      "無",
                      "EavesID":      "無",
                      "Info":         null
                    }
                  ]
                }
                """;
            var w = JsonSerializer.Deserialize<WebproWall>(json, CreateOptions())!;

            Assert.Equal(Orientation.N, w.SurfaceOrientation);
            Assert.Equal(114.12, w.Area);
            Assert.Equal("FG1", w.WallSpec);
            Assert.Equal(WallType.GroundWall, w.Type);
            Assert.Single(w.Windows);
            Assert.Equal("無", w.Windows[0].ID); // sentinel
        }

        [Fact]
        public void Read_WithUvalue()
        {
            // Uvalue は実サンプルに無いが、将来互換性のため読める
            const string json = """
                {
                  "Direction":    "東",
                  "EnvelopeArea": 50,
                  "WallSpec":     "W1",
                  "WallType":     "日の当たる外壁",
                  "Uvalue":       2.5,
                  "WindowList":   []
                }
                """;
            var w = JsonSerializer.Deserialize<WebproWall>(json, CreateOptions())!;
            Assert.Equal(2.5, w.HeatTransferCoefficient);
        }

        [Fact]
        public void Read_UvalueNull_RemainsNaN()
        {
            const string json = """
                {
                  "Direction":    "東",
                  "EnvelopeArea": 50,
                  "WallSpec":     "W1",
                  "WallType":     "日の当たる外壁",
                  "Uvalue":       null,
                  "WindowList":   []
                }
                """;
            var w = JsonSerializer.Deserialize<WebproWall>(json, CreateOptions())!;
            Assert.True(double.IsNaN(w.HeatTransferCoefficient));
        }

        [Fact]
        public void Read_EmptyWindowList()
        {
            const string json = """
                {
                  "Direction":    "南",
                  "EnvelopeArea": 30,
                  "WallSpec":     "W1",
                  "WallType":     "内壁",
                  "WindowList":   []
                }
                """;
            var w = JsonSerializer.Deserialize<WebproWall>(json, CreateOptions())!;
            Assert.Empty(w.Windows);
        }

        [Fact]
        public void Read_MultipleWindows()
        {
            const string json = """
                {
                  "Direction":    "南",
                  "EnvelopeArea": 100,
                  "WallSpec":     "W1",
                  "WallType":     "日の当たる外壁",
                  "WindowList": [
                    { "WindowID": "G1", "WindowNumber": 10, "isBlind": "無", "EavesID": "無" },
                    { "WindowID": "G2", "WindowNumber": 15, "isBlind": "有", "EavesID": "無" }
                  ]
                }
                """;
            var w = JsonSerializer.Deserialize<WebproWall>(json, CreateOptions())!;

            Assert.Equal(2, w.Windows.Count);
            Assert.Equal("G1", w.Windows[0].ID);
            Assert.False(w.Windows[0].HasBlind);
            Assert.Equal("G2", w.Windows[1].ID);
            Assert.True(w.Windows[1].HasBlind);
        }

        [Fact]
        public void Read_AreaNull()
        {
            const string json = """
                {
                  "Direction":      "南",
                  "EnvelopeArea":   null,
                  "EnvelopeWidth":  10,
                  "EnvelopeHeight": 3,
                  "WallSpec":       "W1",
                  "WallType":       "日の当たる外壁",
                  "WindowList":     []
                }
                """;
            var w = JsonSerializer.Deserialize<WebproWall>(json, CreateOptions())!;

            Assert.Null(w.Area);
            Assert.Equal(10, w.Width);
            Assert.Equal(3, w.Height);
        }

        [Fact]
        public void Read_UnknownPropertyIgnored()
        {
            const string json = """
                {
                  "Direction":    "南",
                  "EnvelopeArea": 30,
                  "WallSpec":     "W1",
                  "WallType":     "内壁",
                  "WindowList":   [],
                  "futureField":  123
                }
                """;
            var w = JsonSerializer.Deserialize<WebproWall>(json, CreateOptions())!;
            Assert.Equal(WallType.InnerWall, w.Type);
        }

        #endregion

        // ================================================================
        #region エラー処理

        [Fact]
        public void Read_MissingDirection_Throws()
        {
            const string json = """
                {
                  "EnvelopeArea": 30,
                  "WallSpec":     "W1",
                  "WallType":     "日の当たる外壁",
                  "WindowList":   []
                }
                """;
            Assert.Throws<JsonException>(() =>
                JsonSerializer.Deserialize<WebproWall>(json, CreateOptions()));
        }

        [Fact]
        public void Read_MissingWindowList_Throws()
        {
            const string json = """
                {
                  "Direction":    "南",
                  "EnvelopeArea": 30,
                  "WallSpec":     "W1",
                  "WallType":     "内壁"
                }
                """;
            Assert.Throws<JsonException>(() =>
                JsonSerializer.Deserialize<WebproWall>(json, CreateOptions()));
        }

        [Fact]
        public void Read_MissingWallSpec_Throws()
        {
            const string json = """
                {
                  "Direction":    "南",
                  "EnvelopeArea": 30,
                  "WallType":     "内壁",
                  "WindowList":   []
                }
                """;
            Assert.Throws<JsonException>(() =>
                JsonSerializer.Deserialize<WebproWall>(json, CreateOptions()));
        }

        [Fact]
        public void Write_Throws()
        {
            var w = new WebproWall();
            Assert.Throws<NotSupportedException>(() =>
                JsonSerializer.Serialize(w, CreateOptions()));
        }

        #endregion
    }
}
