/* MaterialCatalogTests.cs
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

using Popolo.Core.Building.Envelope;
using Popolo.Webpro.Conversion;

namespace Popolo.Webpro.Tests.Conversion
{
    /// <summary>Unit tests for <see cref="MaterialCatalog"/>.</summary>
    public class MaterialCatalogTests
    {
        #region ヘルパー

        private const string SampleCatalogJson = """
            {
              "materials": [
                { "id": "Concrete",
                  "type": "solid",
                  "thermalConductivity": 1.6,
                  "volumetricSpecificHeat": 2000 },
                { "id": "Gypsum",
                  "type": "solid",
                  "thermalConductivity": 0.22,
                  "volumetricSpecificHeat": 830 },
                { "id": "SealedGap",
                  "type": "airGap",
                  "isSealed": true,
                  "fixedThickness": 0.02 },
                { "id": "OpenGap",
                  "type": "airGap",
                  "isSealed": false,
                  "fixedThickness": 0.02 },
                { "id": "Soil",
                  "type": "soil",
                  "thermalConductivity": 1.0,
                  "volumetricSpecificHeat": 3300,
                  "fixedThickness": 0.0001 }
              ]
            }
            """;

        private static MaterialCatalog CreateSampleCatalog()
            => MaterialCatalog.LoadFromString(SampleCatalogJson);

        #endregion

        // ================================================================
        #region ロード

        [Fact]
        public void LoadFromString_ParsesAllEntries()
        {
            var catalog = CreateSampleCatalog();
            Assert.Equal(5, catalog.Count);
        }

        [Fact]
        public void LoadFromString_EntriesAccessibleByContains()
        {
            var catalog = CreateSampleCatalog();
            Assert.True(catalog.Contains("Concrete"));
            Assert.True(catalog.Contains("SealedGap"));
            Assert.True(catalog.Contains("Soil"));
            Assert.False(catalog.Contains("NotInCatalog"));
        }

        [Fact]
        public void LoadFromString_NullArg_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                MaterialCatalog.LoadFromString(null!));
        }

        [Fact]
        public void LoadFromString_MissingMaterialsArray_Throws()
        {
            Assert.Throws<InvalidOperationException>(() =>
                MaterialCatalog.LoadFromString("""{ "other": [] }"""));
        }

        [Fact]
        public void LoadFromString_DuplicateId_Throws()
        {
            const string dupJson = """
                {
                  "materials": [
                    { "id": "A", "type": "solid", "thermalConductivity": 1, "volumetricSpecificHeat": 1000 },
                    { "id": "A", "type": "solid", "thermalConductivity": 2, "volumetricSpecificHeat": 2000 }
                  ]
                }
                """;
            Assert.Throws<InvalidOperationException>(() =>
                MaterialCatalog.LoadFromString(dupJson));
        }

        [Fact]
        public void LoadFromString_UnknownType_Throws()
        {
            const string unkJson = """
                {
                  "materials": [
                    { "id": "X", "type": "exotic", "thermalConductivity": 1, "volumetricSpecificHeat": 1 }
                  ]
                }
                """;
            Assert.Throws<InvalidOperationException>(() =>
                MaterialCatalog.LoadFromString(unkJson));
        }

        #endregion

        // ================================================================
        #region MakeWallLayer - solid

        [Fact]
        public void MakeWallLayer_Solid_UsesSuppliedThickness()
        {
            var catalog = CreateSampleCatalog();
            var layer = catalog.MakeWallLayer("Concrete", 150.0);

            Assert.NotNull(layer);
            Assert.IsNotType<AirGapLayer>(layer);
            // 150 mm → 0.15 m
            Assert.Equal(0.15, layer.Thickness, 6);
        }

        [Fact]
        public void MakeWallLayer_Solid_PreservesThermalProperties()
        {
            var catalog = CreateSampleCatalog();
            var layer = catalog.MakeWallLayer("Concrete", 150.0);

            // thermalConductivity=1.6, volSpecificHeat=2000 from sample
            Assert.Equal(1.6, layer.ThermalConductivity, 6);
            Assert.Equal(2000, layer.VolSpecificHeat, 6);
        }

        [Fact]
        public void MakeWallLayer_Solid_NullThickness_TreatedAsZero()
        {
            var catalog = CreateSampleCatalog();
            var layer = catalog.MakeWallLayer("Concrete", null);
            Assert.Equal(0.0, layer.Thickness, 6);
        }

        [Fact]
        public void MakeWallLayer_Solid_UsesIdAsName()
        {
            var catalog = CreateSampleCatalog();
            var layer = catalog.MakeWallLayer("Concrete", 100.0);
            Assert.Equal("Concrete", layer.Name);
        }

        #endregion

        // ================================================================
        #region MakeWallLayer - airGap

        [Fact]
        public void MakeWallLayer_AirGap_IgnoresSuppliedThickness()
        {
            var catalog = CreateSampleCatalog();
            // 50 mm passed but ignored; catalog fixed = 0.02 m
            var layer = catalog.MakeWallLayer("SealedGap", 50.0);
            Assert.Equal(0.02, layer.Thickness, 6);
        }

        [Fact]
        public void MakeWallLayer_AirGap_ReturnsAirGapLayer()
        {
            var catalog = CreateSampleCatalog();
            var sealedLayer = catalog.MakeWallLayer("SealedGap", null);
            var openLayer = catalog.MakeWallLayer("OpenGap", null);

            Assert.IsType<AirGapLayer>(sealedLayer);
            Assert.IsType<AirGapLayer>(openLayer);
        }

        [Fact]
        public void MakeWallLayer_AirGap_NullThicknessOk()
        {
            var catalog = CreateSampleCatalog();
            var layer = catalog.MakeWallLayer("OpenGap", null);
            Assert.Equal(0.02, layer.Thickness, 6);
        }

        #endregion

        // ================================================================
        #region MakeWallLayer - soil

        [Fact]
        public void MakeWallLayer_Soil_UsesFixedThickness()
        {
            var catalog = CreateSampleCatalog();
            // 9999 passed but ignored; catalog fixed = 0.0001 m
            var layer = catalog.MakeWallLayer("Soil", 9999.0);
            Assert.Equal(0.0001, layer.Thickness, 6);
        }

        [Fact]
        public void MakeWallLayer_Soil_PreservesThermalProperties()
        {
            var catalog = CreateSampleCatalog();
            var layer = catalog.MakeWallLayer("Soil", null);
            Assert.Equal(1.0, layer.ThermalConductivity, 6);
            Assert.Equal(3300, layer.VolSpecificHeat, 6);
        }

        [Fact]
        public void MakeWallLayer_Soil_NotAirGapLayer()
        {
            var catalog = CreateSampleCatalog();
            var layer = catalog.MakeWallLayer("Soil", null);
            Assert.IsNotType<AirGapLayer>(layer);
        }

        #endregion

        // ================================================================
        #region エラー処理

        [Fact]
        public void MakeWallLayer_NullId_Throws()
        {
            var catalog = CreateSampleCatalog();
            Assert.Throws<ArgumentNullException>(() =>
                catalog.MakeWallLayer(null!, 100.0));
        }

        [Fact]
        public void MakeWallLayer_UnknownId_Throws()
        {
            var catalog = CreateSampleCatalog();
            Assert.Throws<KeyNotFoundException>(() =>
                catalog.MakeWallLayer("Unobtanium", 100.0));
        }

        #endregion

        // ================================================================
        #region Default - 埋め込みリソース

        [Fact]
        public void Default_HasMaterialsFromEmbeddedResource()
        {
            var catalog = MaterialCatalog.Default;
            // 旧版 v2.3 の建材数は 85
            Assert.True(catalog.Count >= 80,
                $"Expected at least 80 materials, got {catalog.Count}.");
        }

        [Fact]
        public void Default_ContainsWellKnownMaterials()
        {
            var catalog = MaterialCatalog.Default;
            Assert.True(catalog.Contains("コンクリート"));
            Assert.True(catalog.Contains("せっこうボード"));
            Assert.True(catalog.Contains("非密閉中空層"));
            Assert.True(catalog.Contains("密閉中空層"));
            Assert.True(catalog.Contains("土壌"));
        }

        [Fact]
        public void Default_ConcreteHasExpectedProperties()
        {
            var catalog = MaterialCatalog.Default;
            var layer = catalog.MakeWallLayer("コンクリート", 150.0);

            // Legacy Popolo v2.3 values: λ=1.6 W/(m·K), ρc=2000 kJ/(m³·K)
            Assert.Equal(1.6, layer.ThermalConductivity, 3);
            Assert.Equal(2000, layer.VolSpecificHeat, 3);
            Assert.Equal(0.150, layer.Thickness, 6);
        }

        [Fact]
        public void Default_NonSealedAirGapIsAirGapLayer()
        {
            var catalog = MaterialCatalog.Default;
            var layer = catalog.MakeWallLayer("非密閉中空層", null);
            Assert.IsType<AirGapLayer>(layer);
            Assert.Equal(0.02, layer.Thickness, 6);
        }

        [Fact]
        public void Default_IsSingletonAcrossCalls()
        {
            // Lazy<T> caches; multiple accesses should return the same instance.
            Assert.Same(MaterialCatalog.Default, MaterialCatalog.Default);
        }

        #endregion
    }
}
