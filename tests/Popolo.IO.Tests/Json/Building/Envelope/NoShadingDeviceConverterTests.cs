/* NoShadingDeviceConverterTests.cs
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
        #region Serialization

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
        #region Deserialization

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
        #region Round trip

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
        #region Error handling

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
