/* GlazingCatalogTests.cs
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
using System.Collections.Generic;
using Xunit;

using Popolo.Webpro.Conversion;

namespace Popolo.Webpro.Tests.Conversion
{
    /// <summary>Unit tests for <see cref="GlazingCatalog"/>.</summary>
    public class GlazingCatalogTests
    {
        #region ヘルパー

        private const string SampleCatalogJson = """
            {
              "glazings": [
                { "id": "G1", "solarHeatGain": 0.54, "heatTransferCoefficient": 1.38 },
                { "id": "G2", "solarHeatGain": 0.33, "heatTransferCoefficient": 1.15 },
                { "id": "T",  "solarHeatGain": 0.88, "heatTransferCoefficient": 5.95 },
                { "id": "S",  "solarHeatGain": 0.0842, "heatTransferCoefficient": 2.63 }
              ]
            }
            """;

        private static GlazingCatalog CreateSampleCatalog()
            => GlazingCatalog.LoadFromString(SampleCatalogJson);

        #endregion

        // ================================================================
        #region ロード

        [Fact]
        public void LoadFromString_ParsesAllEntries()
        {
            var catalog = CreateSampleCatalog();
            Assert.Equal(4, catalog.Count);
        }

        [Fact]
        public void Contains_KnownId_ReturnsTrue()
        {
            var catalog = CreateSampleCatalog();
            Assert.True(catalog.Contains("G1"));
            Assert.True(catalog.Contains("S"));
        }

        [Fact]
        public void Contains_UnknownId_ReturnsFalse()
        {
            var catalog = CreateSampleCatalog();
            Assert.False(catalog.Contains("Unknown"));
        }

        [Fact]
        public void LoadFromString_NullArg_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                GlazingCatalog.LoadFromString(null!));
        }

        [Fact]
        public void LoadFromString_MissingGlazingsArray_Throws()
        {
            Assert.Throws<InvalidOperationException>(() =>
                GlazingCatalog.LoadFromString("""{ "wrong": [] }"""));
        }

        [Fact]
        public void LoadFromString_DuplicateId_Throws()
        {
            const string dupJson = """
                {
                  "glazings": [
                    { "id": "G1", "solarHeatGain": 0.5, "heatTransferCoefficient": 1.0 },
                    { "id": "G1", "solarHeatGain": 0.6, "heatTransferCoefficient": 1.5 }
                  ]
                }
                """;
            Assert.Throws<InvalidOperationException>(() =>
                GlazingCatalog.LoadFromString(dupJson));
        }

        #endregion

        // ================================================================
        #region Get

        [Fact]
        public void Get_ReturnsCorrectPerformance()
        {
            var catalog = CreateSampleCatalog();
            var perf = catalog.Get("G1");

            Assert.Equal(0.54, perf.SolarHeatGain, 6);
            Assert.Equal(1.38, perf.HeatTransferCoefficient, 6);
        }

        [Fact]
        public void Get_SingleCharId_Works()
        {
            var catalog = CreateSampleCatalog();
            var t = catalog.Get("T");
            Assert.Equal(0.88, t.SolarHeatGain, 6);
            Assert.Equal(5.95, t.HeatTransferCoefficient, 6);

            var s = catalog.Get("S");
            Assert.Equal(0.0842, s.SolarHeatGain, 6);
            Assert.Equal(2.63, s.HeatTransferCoefficient, 6);
        }

        [Fact]
        public void Get_NullArg_Throws()
        {
            var catalog = CreateSampleCatalog();
            Assert.Throws<ArgumentNullException>(() => catalog.Get(null!));
        }

        [Fact]
        public void Get_UnknownId_Throws()
        {
            var catalog = CreateSampleCatalog();
            Assert.Throws<KeyNotFoundException>(() => catalog.Get("NotInCatalog"));
        }

        #endregion

        // ================================================================
        #region Default - 埋め込みリソース

        [Fact]
        public void Default_HasGlazingsFromEmbeddedResource()
        {
            var catalog = GlazingCatalog.Default;
            // 旧版 v2.3 のガラス数は 156
            Assert.True(catalog.Count >= 150,
                $"Expected at least 150 glazings, got {catalog.Count}.");
        }

        [Fact]
        public void Default_ContainsWellKnownGlazings()
        {
            var catalog = GlazingCatalog.Default;
            Assert.True(catalog.Contains("3WgG06"));
            Assert.True(catalog.Contains("T"));
            Assert.True(catalog.Contains("S"));
        }

        [Fact]
        public void Default_3WgG06_HasExpectedValues()
        {
            var catalog = GlazingCatalog.Default;
            var perf = catalog.Get("3WgG06");
            // Legacy Popolo v2.3 values
            Assert.Equal(0.54, perf.SolarHeatGain, 3);
            Assert.Equal(1.38, perf.HeatTransferCoefficient, 3);
        }

        [Fact]
        public void Default_T_HasExpectedValues()
        {
            var catalog = GlazingCatalog.Default;
            var perf = catalog.Get("T");
            Assert.Equal(0.88, perf.SolarHeatGain, 3);
            Assert.Equal(5.95, perf.HeatTransferCoefficient, 3);
        }

        [Fact]
        public void Default_IsSingletonAcrossCalls()
        {
            Assert.Same(GlazingCatalog.Default, GlazingCatalog.Default);
        }

        #endregion
    }
}
