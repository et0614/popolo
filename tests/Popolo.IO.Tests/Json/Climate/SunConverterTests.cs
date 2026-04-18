/* SunConverterTests.cs
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

using Popolo.Core.Climate;
using Popolo.IO.Json.Climate;

namespace Popolo.IO.Tests.Json.Climate
{
    /// <summary>Unit tests for <see cref="SunConverter"/>.</summary>
    public class SunConverterTests
    {
        #region ヘルパー

        private static JsonSerializerOptions CreateOptions()
        {
            var opts = new JsonSerializerOptions();
            opts.Converters.Add(new SunConverter());
            return opts;
        }

        private static Sun MakeTokyo()
            => new Sun(latitude: 35.6812, longitude: 139.7671, standardLongitude: 135.0);

        private static int CountProperties(JsonElement obj)
        {
            int count = 0;
            foreach (var _ in obj.EnumerateObject()) count++;
            return count;
        }

        #endregion

        // ================================================================
        #region シリアライズ

        [Fact]
        public void Write_ProducesFourProperties()
        {
            var json = JsonSerializer.Serialize(MakeTokyo(), CreateOptions());
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.Equal(4, CountProperties(root));
            Assert.Equal("sun", root.GetProperty("kind").GetString());
            Assert.Equal(35.6812, root.GetProperty("latitude").GetDouble());
            Assert.Equal(139.7671, root.GetProperty("longitude").GetDouble());
            Assert.Equal(135.0, root.GetProperty("standardLongitude").GetDouble());
        }

        [Fact]
        public void Write_NegativeLatitude_Preserved()
        {
            // Sydney: 33.87°S
            var sun = new Sun(-33.87, 151.21, 150.0);
            var json = JsonSerializer.Serialize(sun, CreateOptions());
            using var doc = JsonDocument.Parse(json);
            Assert.Equal(-33.87, doc.RootElement.GetProperty("latitude").GetDouble());
        }

        #endregion

        // ================================================================
        #region デシリアライズ

        [Fact]
        public void Read_WellFormedJson_ProducesExpectedValues()
        {
            const string json = """
                { "kind": "sun", "latitude": 35.6812, "longitude": 139.7671, "standardLongitude": 135.0 }
                """;
            var sun = JsonSerializer.Deserialize<Sun>(json, CreateOptions())!;
            Assert.Equal(35.6812, sun.Latitude);
            Assert.Equal(139.7671, sun.Longitude);
            Assert.Equal(135.0, sun.StandardLongitude);
        }

        [Fact]
        public void Read_PropertyOrderIndependent()
        {
            const string json = """
                { "standardLongitude": 135.0, "kind": "sun", "latitude": 35.68, "longitude": 139.77 }
                """;
            var sun = JsonSerializer.Deserialize<Sun>(json, CreateOptions())!;
            Assert.Equal(35.68, sun.Latitude);
        }

        [Fact]
        public void Read_UnknownProperties_Ignored()
        {
            const string json = """
                {
                  "kind": "sun", "latitude": 35.68, "longitude": 139.77, "standardLongitude": 135,
                  "cityName": "Tokyo", "futureField": 42
                }
                """;
            var sun = JsonSerializer.Deserialize<Sun>(json, CreateOptions())!;
            Assert.Equal(35.68, sun.Latitude);
        }

        #endregion

        // ================================================================
        #region ラウンドトリップ

        [Fact]
        public void RoundTrip_PreservesLocation()
        {
            var original = MakeTokyo();
            var json = JsonSerializer.Serialize(original, CreateOptions());
            var restored = JsonSerializer.Deserialize<Sun>(json, CreateOptions())!;

            Assert.Equal(original.Latitude, restored.Latitude);
            Assert.Equal(original.Longitude, restored.Longitude);
            Assert.Equal(original.StandardLongitude, restored.StandardLongitude);
        }

        #endregion

        // ================================================================
        #region エラー処理

        [Fact]
        public void Read_MissingKind_Throws()
        {
            Assert.Throws<JsonException>(() =>
                JsonSerializer.Deserialize<Sun>(
                    """{"latitude":35.68,"longitude":139.77,"standardLongitude":135}""", CreateOptions()));
        }

        [Fact]
        public void Read_WrongKind_Throws()
        {
            Assert.Throws<JsonException>(() =>
                JsonSerializer.Deserialize<Sun>(
                    """{"kind":"incline","latitude":35.68,"longitude":139.77,"standardLongitude":135}""", CreateOptions()));
        }

        [Fact]
        public void Read_MissingLatitude_Throws()
        {
            Assert.Throws<JsonException>(() =>
                JsonSerializer.Deserialize<Sun>(
                    """{"kind":"sun","longitude":139.77,"standardLongitude":135}""", CreateOptions()));
        }

        [Fact]
        public void Read_MissingStandardLongitude_Throws()
        {
            Assert.Throws<JsonException>(() =>
                JsonSerializer.Deserialize<Sun>(
                    """{"kind":"sun","latitude":35.68,"longitude":139.77}""", CreateOptions()));
        }

        [Fact]
        public void Read_NotAnObject_Throws()
        {
            Assert.Throws<JsonException>(() =>
                JsonSerializer.Deserialize<Sun>("[35.68, 139.77, 135]", CreateOptions()));
        }

        #endregion
    }
}
