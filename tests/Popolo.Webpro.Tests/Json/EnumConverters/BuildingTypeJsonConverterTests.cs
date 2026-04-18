/* BuildingTypeJsonConverterTests.cs
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
    /// <summary>Unit tests for <see cref="BuildingTypeJsonConverter"/>.</summary>
    public class BuildingTypeJsonConverterTests
    {
        #region ヘルパー

        private static JsonSerializerOptions CreateOptions()
        {
            var opts = new JsonSerializerOptions();
            opts.Converters.Add(new BuildingTypeJsonConverter());
            return opts;
        }

        #endregion

        // ================================================================
        #region Read - canonical form

        [Theory]
        [InlineData("\"事務所等\"", BuildingType.Office)]
        [InlineData("\"ホテル等\"", BuildingType.Hotel)]
        [InlineData("\"病院等\"", BuildingType.Hospital)]
        [InlineData("\"物販店舗等\"", BuildingType.Retail)]
        [InlineData("\"学校等\"", BuildingType.School)]
        [InlineData("\"飲食店等\"", BuildingType.Restaurant)]
        [InlineData("\"集会所等\"", BuildingType.Hall)]
        [InlineData("\"工場等\"", BuildingType.Plant)]
        [InlineData("\"共同住宅\"", BuildingType.ApartmentHouse)]
        public void Read_CanonicalForm_MapsCorrectly(string json, BuildingType expected)
        {
            var result = JsonSerializer.Deserialize<BuildingType>(json, CreateOptions());
            Assert.Equal(expected, result);
        }

        #endregion

        // ================================================================
        #region Read - aliases

        [Theory]
        [InlineData("\"事務所\"", BuildingType.Office)]
        [InlineData("\"ホテル\"", BuildingType.Hotel)]
        [InlineData("\"病院\"", BuildingType.Hospital)]
        [InlineData("\"学校\"", BuildingType.School)]
        [InlineData("\"飲食店\"", BuildingType.Restaurant)]
        [InlineData("\"集会所\"", BuildingType.Hall)]
        [InlineData("\"集会場\"", BuildingType.Hall)]
        [InlineData("\"工場\"", BuildingType.Plant)]
        [InlineData("\"集合住宅\"", BuildingType.ApartmentHouse)]
        public void Read_ShortAliases_MapsCorrectly(string json, BuildingType expected)
        {
            var result = JsonSerializer.Deserialize<BuildingType>(json, CreateOptions());
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("\"物品販売業を営む店舗等\"")]
        [InlineData("\"物品販売\"")]
        [InlineData("\"物販店舗\"")]
        [InlineData("\"百貨店等\"")]
        [InlineData("\"百貨店\"")]
        public void Read_RetailAliases_MapToRetail(string json)
        {
            var result = JsonSerializer.Deserialize<BuildingType>(json, CreateOptions());
            Assert.Equal(BuildingType.Retail, result);
        }

        #endregion

        // ================================================================
        #region Write - always canonical

        [Theory]
        [InlineData(BuildingType.Office, "\"事務所等\"")]
        [InlineData(BuildingType.Hotel, "\"ホテル等\"")]
        [InlineData(BuildingType.Hospital, "\"病院等\"")]
        [InlineData(BuildingType.Retail, "\"物販店舗等\"")]
        [InlineData(BuildingType.School, "\"学校等\"")]
        [InlineData(BuildingType.Restaurant, "\"飲食店等\"")]
        [InlineData(BuildingType.Hall, "\"集会所等\"")]
        [InlineData(BuildingType.Plant, "\"工場等\"")]
        [InlineData(BuildingType.ApartmentHouse, "\"共同住宅\"")]
        public void Write_AlwaysCanonical(BuildingType value, string expected)
        {
            var opts = CreateOptions();
            opts.Encoder = System.Text.Encodings.Web.JavaScriptEncoder.Create(
                System.Text.Unicode.UnicodeRanges.All);

            var json = JsonSerializer.Serialize(value, opts);
            Assert.Equal(expected, json);
        }

        #endregion

        // ================================================================
        #region Round-trip

        [Theory]
        [InlineData(BuildingType.Office)]
        [InlineData(BuildingType.Hotel)]
        [InlineData(BuildingType.Retail)]
        [InlineData(BuildingType.ApartmentHouse)]
        public void RoundTrip_Canonical(BuildingType value)
        {
            var opts = CreateOptions();
            var json = JsonSerializer.Serialize(value, opts);
            var restored = JsonSerializer.Deserialize<BuildingType>(json, opts);
            Assert.Equal(value, restored);
        }

        #endregion

        // ================================================================
        #region エラー処理

        [Fact]
        public void Read_UnknownString_Throws()
        {
            Assert.Throws<JsonException>(() =>
                JsonSerializer.Deserialize<BuildingType>("\"unknown\"", CreateOptions()));
        }

        [Fact]
        public void Read_NonStringToken_Throws()
        {
            Assert.Throws<JsonException>(() =>
                JsonSerializer.Deserialize<BuildingType>("42", CreateOptions()));
        }

        [Fact]
        public void Read_NullToken_Throws()
        {
            // BuildingType has no None value; null is rejected.
            // System.Text.Json may throw either JsonException or
            // NotSupportedException depending on runtime; either is acceptable.
            Assert.ThrowsAny<System.Exception>(() =>
                JsonSerializer.Deserialize<BuildingType>("null", CreateOptions()));
        }

        #endregion
    }
}
