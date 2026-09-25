/* WebproJsonReaderTests.cs
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
using System.IO;
using System.Text;
using System.Text.Json;
using Xunit;

using Popolo.Webpro.Json;

namespace Popolo.Webpro.Tests.Json
{
    /// <summary>Unit tests for <see cref="WebproJsonReader"/>.</summary>
    public class WebproJsonReaderTests
    {
        private const string MinimalValidJson = """
            { "Building": { "Region": "6" } }
            """;

        // ================================================================
        #region Read(string)

        [Fact]
        public void Read_String_Works()
        {
            var m = WebproJsonReader.Read(MinimalValidJson);
            Assert.Equal("6", m.Building.Region);
        }

        [Fact]
        public void Read_NullString_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                WebproJsonReader.Read((string)null!));
        }

        [Fact]
        public void Read_MalformedJson_Throws()
        {
            Assert.Throws<JsonException>(() => WebproJsonReader.Read("{ not json }"));
        }

        #endregion

        // ================================================================
        #region Read(Stream)

        [Fact]
        public void Read_Stream_Works()
        {
            using var ms = new MemoryStream(Encoding.UTF8.GetBytes(MinimalValidJson));
            var m = WebproJsonReader.Read(ms);
            Assert.Equal("6", m.Building.Region);
        }

        [Fact]
        public void Read_NullStream_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                WebproJsonReader.Read((Stream)null!));
        }

        #endregion

        // ================================================================
        #region ReadFromFile

        [Fact]
        public void ReadFromFile_NullPath_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                WebproJsonReader.ReadFromFile(null!));
        }

        [Fact]
        public void ReadFromFile_MissingFile_Throws()
        {
            Assert.Throws<FileNotFoundException>(() =>
                WebproJsonReader.ReadFromFile("definitely_not_a_real_path_xyz.json"));
        }

        [Fact]
        public void ReadFromFile_ValidFile_Works()
        {
            var tmp = Path.Combine(Path.GetTempPath(), $"popolo_webpro_test_{Guid.NewGuid():N}.json");
            try
            {
                File.WriteAllText(tmp, MinimalValidJson);
                var m = WebproJsonReader.ReadFromFile(tmp);
                Assert.Equal("6", m.Building.Region);
            }
            finally
            {
                if (File.Exists(tmp)) File.Delete(tmp);
            }
        }

        #endregion

        // ================================================================
        #region CreateDefaultOptions

        [Fact]
        public void CreateDefaultOptions_ReturnsFreshInstance()
        {
            var a = WebproJsonReader.CreateDefaultOptions();
            var b = WebproJsonReader.CreateDefaultOptions();
            Assert.NotSame(a, b);
        }

        [Fact]
        public void CreateDefaultOptions_HasConverters()
        {
            var opts = WebproJsonReader.CreateDefaultOptions();
            // At minimum, should contain enough converters to parse the minimal valid JSON.
            Assert.True(opts.Converters.Count > 0);

            // Sanity: deserialize using these options directly
            var m = JsonSerializer.Deserialize<Popolo.Webpro.Domain.WebproModel>(
                MinimalValidJson, opts);
            Assert.NotNull(m);
            Assert.Equal("6", m!.Building.Region);
        }

        #endregion
    }
}
