/* OrientationJsonConverterTests.cs
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
    /// <summary>Unit tests for <see cref="OrientationJsonConverter"/>.</summary>
    public class OrientationJsonConverterTests
    {
        private static JsonSerializerOptions CreateOptions()
        {
            var opts = new JsonSerializerOptions
            {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.Create(
                    System.Text.Unicode.UnicodeRanges.All),
            };
            opts.Converters.Add(new OrientationJsonConverter());
            return opts;
        }

        // ================================================================
        #region Read - all values

        [Theory]
        [InlineData("\"北\"", Orientation.N)]
        [InlineData("\"北西\"", Orientation.NW)]
        [InlineData("\"西\"", Orientation.W)]
        [InlineData("\"南西\"", Orientation.SW)]
        [InlineData("\"南\"", Orientation.S)]
        [InlineData("\"南東\"", Orientation.SE)]
        [InlineData("\"東\"", Orientation.E)]
        [InlineData("\"北東\"", Orientation.NE)]
        [InlineData("\"日陰\"", Orientation.Shade)]
        [InlineData("\"水平\"", Orientation.Horizontal)]
        public void Read_CompassAndLegacyValues(string json, Orientation expected)
        {
            var result = JsonSerializer.Deserialize<Orientation>(json, CreateOptions());
            Assert.Equal(expected, result);
        }

        [Fact]
        public void Read_UpperHorizontalWithFullWidthParens()
        {
            // Full-width parens U+FF08, U+FF09
            string json = "\"水平\uFF08上\uFF09\"";
            var result = JsonSerializer.Deserialize<Orientation>(json, CreateOptions());
            Assert.Equal(Orientation.UpperHorizontal, result);
        }

        [Fact]
        public void Read_LowerHorizontalWithFullWidthParens()
        {
            string json = "\"水平\uFF08下\uFF09\"";
            var result = JsonSerializer.Deserialize<Orientation>(json, CreateOptions());
            Assert.Equal(Orientation.LowerHorizontal, result);
        }

        [Fact]
        public void Read_HalfWidthParens_NotAccepted()
        {
            // ASCII parens should NOT be accepted — real WEBPRO output uses
            // full-width parens.
            string json = "\"水平(上)\"";
            Assert.Throws<JsonException>(() =>
                JsonSerializer.Deserialize<Orientation>(json, CreateOptions()));
        }

        #endregion

        // ================================================================
        #region Write - all values

        [Theory]
        [InlineData(Orientation.N, "\"北\"")]
        [InlineData(Orientation.NW, "\"北西\"")]
        [InlineData(Orientation.W, "\"西\"")]
        [InlineData(Orientation.SW, "\"南西\"")]
        [InlineData(Orientation.S, "\"南\"")]
        [InlineData(Orientation.SE, "\"南東\"")]
        [InlineData(Orientation.E, "\"東\"")]
        [InlineData(Orientation.NE, "\"北東\"")]
        [InlineData(Orientation.Shade, "\"日陰\"")]
        [InlineData(Orientation.Horizontal, "\"水平\"")]
        public void Write_Values(Orientation value, string expected)
        {
            var json = JsonSerializer.Serialize(value, CreateOptions());
            Assert.Equal(expected, json);
        }

        [Fact]
        public void Write_UpperHorizontalUsesFullWidthParens()
        {
            var json = JsonSerializer.Serialize(Orientation.UpperHorizontal, CreateOptions());
            Assert.Equal("\"水平\uFF08上\uFF09\"", json);
        }

        [Fact]
        public void Write_LowerHorizontalUsesFullWidthParens()
        {
            var json = JsonSerializer.Serialize(Orientation.LowerHorizontal, CreateOptions());
            Assert.Equal("\"水平\uFF08下\uFF09\"", json);
        }

        #endregion

        // ================================================================
        #region Round-trip

        [Theory]
        [InlineData(Orientation.N)]
        [InlineData(Orientation.SE)]
        [InlineData(Orientation.UpperHorizontal)]
        [InlineData(Orientation.LowerHorizontal)]
        [InlineData(Orientation.Shade)]
        [InlineData(Orientation.Horizontal)]
        public void RoundTrip(Orientation value)
        {
            var opts = CreateOptions();
            var json = JsonSerializer.Serialize(value, opts);
            var restored = JsonSerializer.Deserialize<Orientation>(json, opts);
            Assert.Equal(value, restored);
        }

        #endregion

        // ================================================================
        #region エラー処理

        [Fact]
        public void Read_UnknownString_Throws()
        {
            Assert.Throws<JsonException>(() =>
                JsonSerializer.Deserialize<Orientation>("\"北北西\"", CreateOptions()));
        }

        [Fact]
        public void Read_NonStringToken_Throws()
        {
            Assert.Throws<JsonException>(() =>
                JsonSerializer.Deserialize<Orientation>("0", CreateOptions()));
        }

        #endregion
    }
}
