/* NoShadingDeviceConverterTests.cs
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

using Popolo.Core.Building.Envelope;
using Popolo.IO.Json.Building.Envelope;

namespace Popolo.IO.Tests.Json.Building.Envelope
{
    /// <summary>Unit tests for <see cref="NoShadingDeviceConverter"/>.</summary>
    public class NoShadingDeviceConverterTests
    {
        private static JsonSerializerOptions CreateOptions()
        {
            var opts = new JsonSerializerOptions();
            opts.Converters.Add(new NoShadingDeviceConverter());
            return opts;
        }

        private static int CountProperties(JsonElement obj)
        {
            int count = 0;
            foreach (var _ in obj.EnumerateObject()) count++;
            return count;
        }

        // ================================================================
        #region シリアライズ

        [Fact]
        public void Write_ProducesSinglePropertyObject()
        {
            var json = JsonSerializer.Serialize(new NoShadingDevice(), CreateOptions());
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.Equal(1, CountProperties(root));
            Assert.Equal("noShadingDevice", root.GetProperty("kind").GetString());
        }

        #endregion

        // ================================================================
        #region デシリアライズ

        [Fact]
        public void Read_WellFormedJson_ProducesInstance()
        {
            const string json = """{ "kind": "noShadingDevice" }""";
            var device = JsonSerializer.Deserialize<NoShadingDevice>(json, CreateOptions())!;
            Assert.NotNull(device);
        }

        [Fact]
        public void Read_UnknownProperties_Ignored()
        {
            const string json = """{ "kind": "noShadingDevice", "futureField": 42 }""";
            var device = JsonSerializer.Deserialize<NoShadingDevice>(json, CreateOptions())!;
            Assert.NotNull(device);
        }

        #endregion

        // ================================================================
        #region ラウンドトリップ

        [Fact]
        public void RoundTrip_ProducesValidInstance()
        {
            var original = new NoShadingDevice();
            var json = JsonSerializer.Serialize(original, CreateOptions());
            var restored = JsonSerializer.Deserialize<NoShadingDevice>(json, CreateOptions())!;

            Assert.Equal(original.Kind, restored.Kind);
            // 光学特性の再計算で同じ結果になるか確認
            original.ComputeOpticalProperties(
                isDiffuseIrradianceProperties: false, irradianceFromSideF: true,
                out double origT, out double origR);
            restored.ComputeOpticalProperties(
                isDiffuseIrradianceProperties: false, irradianceFromSideF: true,
                out double resT, out double resR);
            Assert.Equal(origT, resT);
            Assert.Equal(origR, resR);
        }

        #endregion

        // ================================================================
        #region エラー処理

        [Fact]
        public void Read_MissingKind_Throws()
        {
            Assert.Throws<JsonException>(() =>
                JsonSerializer.Deserialize<NoShadingDevice>("{}", CreateOptions()));
        }

        [Fact]
        public void Read_WrongKind_Throws()
        {
            const string json = """{ "kind": "venetianBlind" }""";
            Assert.Throws<JsonException>(() =>
                JsonSerializer.Deserialize<NoShadingDevice>(json, CreateOptions()));
        }

        [Fact]
        public void Read_NotAnObject_Throws()
        {
            Assert.Throws<JsonException>(() =>
                JsonSerializer.Deserialize<NoShadingDevice>("[]", CreateOptions()));
        }

        #endregion
    }
}
