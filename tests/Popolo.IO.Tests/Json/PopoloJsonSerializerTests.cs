/* PopoloJsonSerializerTests.cs
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
using System.IO;
using System.Text.Json;
using Xunit;

using Popolo.Core.Building;
using Popolo.Core.Building.Envelope;
using Popolo.Core.Climate;
using Popolo.IO.Json;

namespace Popolo.IO.Tests.Json
{
    /// <summary>Unit tests for <see cref="PopoloJsonSerializer"/>.</summary>
    public class PopoloJsonSerializerTests
    {
        #region ヘルパー

        private static BuildingThermalModel MakeModel()
        {
            var zone = new Zone("Test Zone", 120, 10);
            var layers = new WallLayer[] { new WallLayer("Concrete", 1.4, 1934, 0.15) };
            var wall = new Wall(12.0, layers) { ID = 0 };
            var mRooms = new MultiRooms(1, new[] { zone }, new[] { wall }, Array.Empty<Window>());
            mRooms.AddZone(0, 0);
            mRooms.AddWall(0, 0, true);
            mRooms.SetOutsideWall(0, true, new Incline(0d, Math.PI / 2));

            var model = new BuildingThermalModel(new[] { mRooms });
            model.TimeStep = 3600;
            model.UpdateOutdoorCondition(
                new DateTime(2026, 4, 18, 12, 0, 0),
                new Sun(35.68, 139.77, 135.0),
                20.0, 0.01, 0.0);
            return model;
        }

        #endregion

        // ================================================================
        #region CreateDefaultOptions

        [Fact]
        public void CreateDefaultOptions_ReturnsNonNullOptions()
        {
            var opts = PopoloJsonSerializer.CreateDefaultOptions();
            Assert.NotNull(opts);
            Assert.True(opts.Converters.Count > 0);
        }

        [Fact]
        public void CreateDefaultOptions_ReturnsNewInstanceEachCall()
        {
            var a = PopoloJsonSerializer.CreateDefaultOptions();
            var b = PopoloJsonSerializer.CreateDefaultOptions();
            Assert.NotSame(a, b);
        }

        [Fact]
        public void CreateDefaultOptions_EnablesWriteIndented()
        {
            var opts = PopoloJsonSerializer.CreateDefaultOptions();
            Assert.True(opts.WriteIndented);
        }

        #endregion

        // ================================================================
        #region Serialize/Deserialize 文字列 API

        [Fact]
        public void Serialize_ProducesJsonWithTopLevelSchemaVersion()
        {
            var model = MakeModel();
            var json = PopoloJsonSerializer.Serialize(model);
            using var doc = JsonDocument.Parse(json);

            Assert.Equal("3.0", doc.RootElement.GetProperty("$schemaVersion").GetString());
        }

        [Fact]
        public void Deserialize_RoundTripPreservesModelStructure()
        {
            var original = MakeModel();
            var json = PopoloJsonSerializer.Serialize(original);
            var restored = PopoloJsonSerializer.Deserialize(json);

            Assert.Equal(original.TimeStep, restored.TimeStep);
            Assert.Equal(original.CurrentDateTime, restored.CurrentDateTime);
            Assert.Equal(original.MultiRoom.Length, restored.MultiRoom.Length);
        }

        [Fact]
        public void Serialize_NullModel_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                PopoloJsonSerializer.Serialize(null!));
        }

        [Fact]
        public void Deserialize_NullJson_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                PopoloJsonSerializer.Deserialize(null!));
        }

        [Fact]
        public void Deserialize_MalformedJson_Throws()
        {
            Assert.Throws<JsonException>(() =>
                PopoloJsonSerializer.Deserialize("not valid json"));
        }

        #endregion

        // ================================================================
        #region Serialize/Deserialize with options

        [Fact]
        public void Serialize_WithOptions_UsesProvidedOptions()
        {
            var model = MakeModel();
            var opts = PopoloJsonSerializer.CreateDefaultOptions();
            opts.WriteIndented = false; // compact

            var json = PopoloJsonSerializer.Serialize(model, opts);
            Assert.DoesNotContain("\n", json); // インデント無効で改行がないはず
        }

        [Fact]
        public void Deserialize_WithOptions_WorksWithCustomOptions()
        {
            var original = MakeModel();
            var opts = PopoloJsonSerializer.CreateDefaultOptions();
            var json = PopoloJsonSerializer.Serialize(original, opts);
            var restored = PopoloJsonSerializer.Deserialize(json, opts);

            Assert.Equal(original.TimeStep, restored.TimeStep);
        }

        #endregion

        // ================================================================
        #region ファイル I/O

        [Fact]
        public void SerializeToFile_AndDeserializeFromFile_RoundTrip()
        {
            var model = MakeModel();
            var tempPath = Path.Combine(Path.GetTempPath(),
                $"popolo_test_{Guid.NewGuid()}.json");

            try
            {
                PopoloJsonSerializer.SerializeToFile(model, tempPath);
                Assert.True(File.Exists(tempPath));

                var restored = PopoloJsonSerializer.DeserializeFromFile(tempPath);
                Assert.Equal(model.TimeStep, restored.TimeStep);
                Assert.Equal(model.CurrentDateTime, restored.CurrentDateTime);
            }
            finally
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }
        }

        [Fact]
        public void DeserializeFromFile_NonexistentFile_Throws()
        {
            Assert.Throws<FileNotFoundException>(() =>
                PopoloJsonSerializer.DeserializeFromFile(
                    Path.Combine(Path.GetTempPath(), "does_not_exist_12345.json")));
        }

        [Fact]
        public void SerializeToFile_OverwritesExistingFile()
        {
            var model = MakeModel();
            var tempPath = Path.Combine(Path.GetTempPath(),
                $"popolo_test_{Guid.NewGuid()}.json");

            try
            {
                File.WriteAllText(tempPath, "pre-existing content");
                PopoloJsonSerializer.SerializeToFile(model, tempPath);

                var content = File.ReadAllText(tempPath);
                Assert.Contains("buildingThermalModel", content);
                Assert.DoesNotContain("pre-existing", content);
            }
            finally
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }
        }

        [Fact]
        public void SerializeToFile_NullArguments_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                PopoloJsonSerializer.SerializeToFile(null!, "x.json"));

            var model = MakeModel();
            Assert.Throws<ArgumentNullException>(() =>
                PopoloJsonSerializer.SerializeToFile(model, null!));
        }

        #endregion

        // ================================================================
        #region 完全ラウンドトリップ(ファサード経由)

        [Fact]
        public void FullRoundTrip_PreservesWallConfiguration()
        {
            var original = MakeModel();
            var json = PopoloJsonSerializer.Serialize(original);
            var restored = PopoloJsonSerializer.Deserialize(json);

            var origRefs = ((MultiRooms)original.MultiRoom[0]).GetOutsideWallReferences();
            var restRefs = ((MultiRooms)restored.MultiRoom[0]).GetOutsideWallReferences();

            Assert.Equal(origRefs.Length, restRefs.Length);
            Assert.Equal(origRefs[0].IsSideF, restRefs[0].IsSideF);
        }

        [Fact]
        public void FullRoundTrip_PreservesSunLocation()
        {
            var original = MakeModel();
            var json = PopoloJsonSerializer.Serialize(original);
            var restored = PopoloJsonSerializer.Deserialize(json);

            Assert.InRange(restored.Sun.Latitude,
                original.Sun.Latitude - 1e-9, original.Sun.Latitude + 1e-9);
        }

        #endregion
    }
}
