/* WallInputMethodJsonConverterTests.cs
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

using System.Text.Json;
using Xunit;

using Popolo.Webpro.Domain.Enums;
using Popolo.Webpro.Json.EnumConverters;

namespace Popolo.Webpro.Tests.Json.EnumConverters
{
    /// <summary>Unit tests for <see cref="WallInputMethodJsonConverter"/>.</summary>
    public class WallInputMethodJsonConverterTests
    {
        private static JsonSerializerOptions CreateOptions()
        {
            var opts = new JsonSerializerOptions
            {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.Create(
                    System.Text.Unicode.UnicodeRanges.All),
            };
            opts.Converters.Add(new WallInputMethodJsonConverter());
            return opts;
        }

        // ================================================================
        #region Read

        [Theory]
        [InlineData("\"熱貫流率を入力\"", WallInputMethod.HeatTransferCoefficient)]
        [InlineData("\"建材構成を入力\"", WallInputMethod.MaterialNumberAndThickness)]
        [InlineData("\"断熱材種類を入力\"", WallInputMethod.InsulationType)]
        public void Read_NonNoneValues(string json, WallInputMethod expected)
        {
            var result = JsonSerializer.Deserialize<WallInputMethod>(json, CreateOptions());
            Assert.Equal(expected, result);
        }

        [Fact]
        public void Read_NullToken_MapsToNone()
        {
            var result = JsonSerializer.Deserialize<WallInputMethod>("null", CreateOptions());
            Assert.Equal(WallInputMethod.None, result);
        }

        #endregion

        // ================================================================
        #region Write

        [Theory]
        [InlineData(WallInputMethod.HeatTransferCoefficient, "\"熱貫流率を入力\"")]
        [InlineData(WallInputMethod.MaterialNumberAndThickness, "\"建材構成を入力\"")]
        [InlineData(WallInputMethod.InsulationType, "\"断熱材種類を入力\"")]
        public void Write_NonNoneValues(WallInputMethod value, string expected)
        {
            var json = JsonSerializer.Serialize(value, CreateOptions());
            Assert.Equal(expected, json);
        }

        [Fact]
        public void Write_None_Throws()
        {
            Assert.Throws<JsonException>(() =>
                JsonSerializer.Serialize(WallInputMethod.None, CreateOptions()));
        }

        #endregion

        // ================================================================
        #region Round-trip

        [Theory]
        [InlineData(WallInputMethod.HeatTransferCoefficient)]
        [InlineData(WallInputMethod.MaterialNumberAndThickness)]
        [InlineData(WallInputMethod.InsulationType)]
        public void RoundTrip_NonNone(WallInputMethod value)
        {
            var opts = CreateOptions();
            var json = JsonSerializer.Serialize(value, opts);
            var restored = JsonSerializer.Deserialize<WallInputMethod>(json, opts);
            Assert.Equal(value, restored);
        }

        #endregion

        // ================================================================
        #region エラー処理

        [Fact]
        public void Read_UnknownString_Throws()
        {
            Assert.Throws<JsonException>(() =>
                JsonSerializer.Deserialize<WallInputMethod>("\"不明\"", CreateOptions()));
        }

        [Fact]
        public void Read_NonStringNonNullToken_Throws()
        {
            Assert.Throws<JsonException>(() =>
                JsonSerializer.Deserialize<WallInputMethod>("42", CreateOptions()));
        }

        #endregion
    }
}
