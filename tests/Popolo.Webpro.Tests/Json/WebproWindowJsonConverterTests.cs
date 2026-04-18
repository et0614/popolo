/* WebproWindowJsonConverterTests.cs
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
    /// <summary>Unit tests for <see cref="WebproWindowJsonConverter"/>.</summary>
    public class WebproWindowJsonConverterTests
    {
        #region ヘルパー

        private static JsonSerializerOptions CreateOptions()
        {
            var opts = new JsonSerializerOptions();
            opts.Converters.Add(new WebproWindowJsonConverter());
            return opts;
        }

        #endregion

        // ================================================================
        #region 正常ケース - 実サンプル類似

        [Fact]
        public void Read_RealWindowWithId()
        {
            // 実 JSON: {"WindowID": "G1", "WindowNumber": 16.64, "isBlind": "無", "EavesID": "無", "Info": null}
            const string json = """
                {
                  "WindowID":     "G1",
                  "WindowNumber": 16.64,
                  "isBlind":      "無",
                  "EavesID":      "無",
                  "Info":         null
                }
                """;
            var win = JsonSerializer.Deserialize<WebproWindow>(json, CreateOptions())!;

            Assert.Equal("G1", win.ID);
            Assert.Equal(16.64, win.Number);
            Assert.False(win.HasBlind);
            Assert.Equal("無", win.EavesID);
            Assert.Null(win.Information);
        }

        [Fact]
        public void Read_NoWindowSentinel()
        {
            // 実 JSON: WindowID="無" は「窓なし」の意
            const string json = """
                {
                  "WindowID":     "無",
                  "WindowNumber": null,
                  "isBlind":      "無",
                  "EavesID":      "無",
                  "Info":         null
                }
                """;
            var win = JsonSerializer.Deserialize<WebproWindow>(json, CreateOptions())!;

            // DTO レベルでは "無" をそのまま保持(上位で判断)
            Assert.Equal("無", win.ID);
            Assert.Null(win.Number);
        }

        [Fact]
        public void Read_IsBlindYes_SetsHasBlindTrue()
        {
            const string json = """
                {
                  "WindowID":     "G1",
                  "WindowNumber": 10,
                  "isBlind":      "有",
                  "EavesID":      "無"
                }
                """;
            var win = JsonSerializer.Deserialize<WebproWindow>(json, CreateOptions())!;
            Assert.True(win.HasBlind);
        }

        [Fact]
        public void Read_IsBlindNo_SetsHasBlindFalse()
        {
            const string json = """
                {
                  "WindowID":     "G1",
                  "WindowNumber": 10,
                  "isBlind":      "無",
                  "EavesID":      "無"
                }
                """;
            var win = JsonSerializer.Deserialize<WebproWindow>(json, CreateOptions())!;
            Assert.False(win.HasBlind);
        }

        [Fact]
        public void Read_IsBlindNull_SetsHasBlindFalse()
        {
            const string json = """
                {
                  "WindowID":     "G1",
                  "WindowNumber": 10,
                  "isBlind":      null,
                  "EavesID":      "無"
                }
                """;
            var win = JsonSerializer.Deserialize<WebproWindow>(json, CreateOptions())!;
            Assert.False(win.HasBlind);
        }

        [Fact]
        public void Read_EavesIdNull_BecomesEmptyString()
        {
            const string json = """
                {
                  "WindowID": "G1",
                  "EavesID":  null
                }
                """;
            var win = JsonSerializer.Deserialize<WebproWindow>(json, CreateOptions())!;
            Assert.Equal("", win.EavesID);
        }

        [Fact]
        public void Read_WindowNumberZero_IsZero()
        {
            const string json = """
                {
                  "WindowID":     "G1",
                  "WindowNumber": 0
                }
                """;
            var win = JsonSerializer.Deserialize<WebproWindow>(json, CreateOptions())!;
            Assert.Equal(0.0, win.Number);
        }

        [Fact]
        public void Read_UnknownPropertyIgnored()
        {
            const string json = """
                {
                  "WindowID":    "G1",
                  "futureField": 123,
                  "extraObj":    { "a": 1, "b": 2 }
                }
                """;
            var win = JsonSerializer.Deserialize<WebproWindow>(json, CreateOptions())!;
            Assert.Equal("G1", win.ID);
        }

        [Fact]
        public void Read_EmptyObject_AllDefaults()
        {
            var win = JsonSerializer.Deserialize<WebproWindow>("{}", CreateOptions())!;

            Assert.Equal("", win.ID);
            Assert.Null(win.Number);
            Assert.False(win.HasBlind);
            Assert.Equal("", win.EavesID);
            Assert.Null(win.Information);
        }

        #endregion

        // ================================================================
        #region エラー処理

        [Fact]
        public void Read_NonObjectRoot_Throws()
        {
            Assert.Throws<JsonException>(() =>
                JsonSerializer.Deserialize<WebproWindow>("42", CreateOptions()));
        }

        [Fact]
        public void Read_WindowIdNotString_Throws()
        {
            const string json = """{ "WindowID": 42 }""";
            Assert.Throws<JsonException>(() =>
                JsonSerializer.Deserialize<WebproWindow>(json, CreateOptions()));
        }

        [Fact]
        public void Write_Throws()
        {
            var win = new WebproWindow { ID = "G1" };
            Assert.Throws<NotSupportedException>(() =>
                JsonSerializer.Serialize(win, CreateOptions()));
        }

        #endregion
    }
}
