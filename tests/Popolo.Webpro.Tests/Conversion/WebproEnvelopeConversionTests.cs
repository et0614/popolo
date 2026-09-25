/* WebproEnvelopeConversionTests.cs
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
using System.Linq;
using Xunit;

using Popolo.Core.Building.Envelope;
using Popolo.Core.Exceptions;
using Popolo.Webpro.Conversion;
using Popolo.Webpro.Json;

namespace Popolo.Webpro.Tests.Conversion
{
  /// <summary>
  /// 外皮 (壁・窓) 変換の定量的な回帰テスト。
  /// </summary>
  /// <remarks>
  /// 1 室 1 外壁の最小モデルを JSON から組み立てて変換し、
  /// 窓の定常熱貫流率、壁の層順序、窓面積 (面積×枚数)、
  /// 熱貫流率入力壁の等価層構成、日の当たらない外壁の日射吸収率を検証する。
  /// </remarks>
  public class WebproEnvelopeConversionTests
  {
    /// <summary>WEBPRO の室内外表面熱抵抗の和 [m²K/W] (1/10 + 1/20)。</summary>
    private const double R_IO = 0.15;

    // ================================================================
    #region Helpers

    /// <summary>既定の窓仕様 G1 (ガラスの種類を入力, 単板 T, 1 m²)。</summary>
    private const string DefaultWindowConfigure = """
      "G1": {
        "windowArea": 1,
        "inputMethod": "ガラスの種類を入力",
        "frameType":   "金属製",
        "layerType":   "単層",
        "glassID":     "T"
      }
      """;

    /// <summary>既定の壁仕様 W1 (builelib_input.json の W1 と同じ構成)。</summary>
    private const string DefaultWallConfigure = """
      "W1": {
        "structureType": "その他",
        "solarAbsorptionRatio": null,
        "inputMethod": "建材構成を入力",
        "layers": [
          { "materialID": "せっこうボード", "conductivity": null, "thickness": 8.0 },
          { "materialID": "非密閉中空層", "conductivity": null, "thickness": null },
          { "materialID": "押出法ポリスチレンフォーム　保温板　1種", "conductivity": null, "thickness": 25.0 },
          { "materialID": "コンクリート", "conductivity": null, "thickness": 150.0 },
          { "materialID": "セメント・モルタル", "conductivity": null, "thickness": 25.0 },
          { "materialID": "タイル", "conductivity": null, "thickness": 10.0 }
        ]
      }
      """;

    /// <summary>1 室 1 壁の最小 WEBPRO JSON を組み立てる。</summary>
    private static string BuildJson(
      string windowList = "[]",
      string wallSpec = "W1",
      string wallType = "日の当たる外壁",
      double envelopeArea = 50.0,
      string wallConfigure = DefaultWallConfigure,
      string windowConfigure = DefaultWindowConfigure)
    {
      string area = envelopeArea.ToString(System.Globalization.CultureInfo.InvariantCulture);
      return $$"""
        {
          "Building": { "Region": "6" },
          "Rooms": {
            "R1": {
              "buildingType": "事務所等",
              "roomType": "事務室",
              "floorHeight": 3.8,
              "ceilingHeight": 2.7,
              "roomArea": 100
            }
          },
          "EnvelopeSet": {
            "R1": {
              "isAirconditioned": "有",
              "WallList": [
                {
                  "Direction": "南",
                  "EnvelopeArea": {{area}},
                  "WallSpec": "{{wallSpec}}",
                  "WallType": "{{wallType}}",
                  "WindowList": {{windowList}}
                }
              ]
            }
          },
          "WallConfigure": { {{wallConfigure}} },
          "WindowConfigure": { {{windowConfigure}} },
          "AirConditioningZone": { "R1": {} }
        }
        """;
    }

    private static WebproToBuildingThermalModel.ConversionResult Convert(string json)
    {
      var model = WebproJsonReader.Read(json);
      return WebproToBuildingThermalModel.Convert(model, installHeatGainSchedulers: false);
    }

    /// <summary>変換後の外皮壁 (Walls[0] は床・天井の loop wall、Walls[1] が外皮壁)。</summary>
    private static IReadOnlyWall EnvelopeWall(WebproToBuildingThermalModel.ConversionResult r)
    {
      Assert.Equal(2, r.MultiRooms.Walls.Length);
      return r.MultiRooms.Walls[1];
    }

    /// <summary>窓の内部熱抵抗 (ガラス + 中空層、表面熱抵抗を除く) [m²K/W]。</summary>
    private static double InternalResistance(IReadOnlyWindow w)
    {
      double r = 0;
      for (int i = 0; i < w.GlazingCount; i++) r += w.GetGlassResistance(i);
      for (int i = 0; i < w.GlazingCount - 1; i++) r += w.GetAirGapResistance(i);
      return r;
    }

    /// <summary>壁の層熱抵抗の和 [m²K/W] (表面熱抵抗を除く)。</summary>
    private static double LayerResistance(IReadOnlyWall wall)
      => wall.Layers.Sum(l => 1.0 / l.HeatConductance);

    #endregion

    // ================================================================
    #region 1. 窓の熱貫流率 (中空層には熱抵抗を渡す)

    /// <summary>
    /// 性能値入力 (U=2.5) の窓で、内部熱抵抗 + R_IO が 1/U に一致すること。
    /// </summary>
    [Fact]
    public void Window_WindowSpecU_ReproducesInputU()
    {
      const string winConf = """
        "G1": {
          "windowArea": 1,
          "inputMethod": "性能値を入力",
          "frameType": "金属製",
          "layerType": "複層",
          "windowUvalue": 2.5,
          "windowIvalue": 0.5
        }
        """;
      var r = Convert(BuildJson(
        windowList: """[ { "WindowID": "G1", "WindowNumber": 4, "isBlind": "無", "EavesID": "無" } ]""",
        windowConfigure: winConf));

      var win = Assert.Single(r.MultiRooms.Windows);
      double rInt = InternalResistance(win);
      Assert.Equal(1.0 / 2.5 - R_IO, rInt, 9);
      Assert.Equal(2.5, 1.0 / (rInt + R_IO), 9);
    }

    /// <summary>
    /// カタログの単板ガラス T (U=5.95) で、内部熱抵抗 + R_IO が 1/U に一致すること。
    /// </summary>
    [Fact]
    public void Window_CatalogGlazingT_ReproducesCatalogU()
    {
      var r = Convert(BuildJson(
        windowList: """[ { "WindowID": "G1", "WindowNumber": 1, "isBlind": "無", "EavesID": "無" } ]"""));

      var win = Assert.Single(r.MultiRooms.Windows);
      double u = GlazingCatalog.Default.Get("T").HeatTransferCoefficient;
      double rInt = InternalResistance(win);
      Assert.Equal(1.0 / u - R_IO, rInt, 9);
      Assert.True(win.GetAirGapResistance(0) > 0);
    }

    /// <summary>
    /// 金属製単板窓相当の U=6.51 (内部熱抵抗 < ガラス既定値 2×0.006) でも
    /// 全抵抗が正のまま U を再現すること。
    /// </summary>
    [Fact]
    public void Window_HighUBelowLimit_ReproducesInputUWithPositiveResistances()
    {
      const string winConf = """
        "G1": {
          "windowArea": 1,
          "inputMethod": "性能値を入力",
          "frameType": "金属製",
          "layerType": "単層",
          "windowUvalue": 6.51,
          "windowIvalue": 0.8
        }
        """;
      var r = Convert(BuildJson(
        windowList: """[ { "WindowID": "G1", "WindowNumber": 1, "isBlind": "無", "EavesID": "無" } ]""",
        windowConfigure: winConf));

      var win = Assert.Single(r.MultiRooms.Windows);
      Assert.Equal(1.0 / 6.51 - R_IO, InternalResistance(win), 9);
      for (int i = 0; i < win.GlazingCount; i++) Assert.True(win.GetGlassResistance(i) > 0);
      Assert.True(win.GetAirGapResistance(0) > 0);
    }

    /// <summary>U ≥ 1/R_IO の窓は明確な例外になること。</summary>
    [Fact]
    public void Window_UAboveSurfaceResistanceLimit_Throws()
    {
      const string winConf = """
        "G1": {
          "windowArea": 1,
          "inputMethod": "性能値を入力",
          "frameType": "金属製",
          "layerType": "単層",
          "windowUvalue": 7.0,
          "windowIvalue": 0.8
        }
        """;
      string json = BuildJson(
        windowList: """[ { "WindowID": "G1", "WindowNumber": 1, "isBlind": "無", "EavesID": "無" } ]""",
        windowConfigure: winConf);
      var ex = Assert.Throws<PopoloArgumentException>(() => Convert(json));
      Assert.Contains("G1", ex.Message);
    }

    #endregion

    // ================================================================
    #region 2. 壁の層順序 (WEBPRO は室内側→屋外側、Popolo は F=屋外側)

    /// <summary>外壁 W1: 屋外側 (layers[0]) がタイル、室内側 (最終層) がせっこうボード。</summary>
    [Fact]
    public void Wall_LayersAreReversed_OutdoorFirst()
    {
      var wall = EnvelopeWall(Convert(BuildJson()));
      Assert.Equal(6, wall.Layers.Length);
      Assert.Equal("タイル", wall.Layers[0].Name);
      Assert.Equal("セメント・モルタル", wall.Layers[1].Name);
      Assert.Equal("せっこうボード", wall.Layers[^1].Name);
    }

    /// <summary>接地壁 FG1: 地盤側 (layers[0]) がコンクリート、室内側がビニル系床材。</summary>
    [Fact]
    public void GroundWall_LayersAreReversed_SoilSideFirst()
    {
      const string fg1 = """
        "FG1": {
          "structureType": "その他",
          "inputMethod": "建材構成を入力",
          "layers": [
            { "materialID": "ビニル系床材", "conductivity": null, "thickness": 3.0 },
            { "materialID": "セメント・モルタル", "conductivity": null, "thickness": 27.0 },
            { "materialID": "コンクリート", "conductivity": null, "thickness": 150.0 }
          ]
        }
        """;
      var wall = EnvelopeWall(Convert(BuildJson(
        wallSpec: "FG1", wallType: "地盤に接する外壁", wallConfigure: fg1)));
      Assert.Equal("コンクリート", wall.Layers[0].Name);
      Assert.Equal("ビニル系床材", wall.Layers[^1].Name);
    }

    #endregion

    // ================================================================
    #region 3. 窓面積 = 窓 1 枚の面積 × 枚数 (WindowNumber)

    /// <summary>windowArea=2 m² × 3 枚 → 窓 6 m²、正味壁面積 50-6 = 44 m²。</summary>
    [Fact]
    public void WindowArea_IsPerWindowAreaTimesCount()
    {
      const string winConf = """
        "G2": {
          "windowArea": 2.0,
          "inputMethod": "ガラスの種類を入力",
          "frameType": "金属製",
          "layerType": "単層",
          "glassID": "T"
        }
        """;
      var r = Convert(BuildJson(
        windowList: """[ { "WindowID": "G2", "WindowNumber": 3, "isBlind": "無", "EavesID": "無" } ]""",
        windowConfigure: winConf));

      var win = Assert.Single(r.MultiRooms.Windows);
      Assert.Equal(6.0, win.Area, 9);
      Assert.Equal(44.0, EnvelopeWall(r).Area, 9);
    }

    /// <summary>windowArea が null の場合は幅×高さ (1.5×2.0) × 枚数 2 = 6 m²。</summary>
    [Fact]
    public void WindowArea_FallsBackToWidthTimesHeight()
    {
      const string winConf = """
        "G2": {
          "windowArea": null,
          "windowWidth": 1.5,
          "windowHeight": 2.0,
          "inputMethod": "ガラスの種類を入力",
          "frameType": "金属製",
          "layerType": "単層",
          "glassID": "T"
        }
        """;
      var r = Convert(BuildJson(
        windowList: """[ { "WindowID": "G2", "WindowNumber": 2, "isBlind": "無", "EavesID": "無" } ]""",
        windowConfigure: winConf));

      Assert.Equal(6.0, Assert.Single(r.MultiRooms.Windows).Area, 9);
      Assert.Equal(44.0, EnvelopeWall(r).Area, 9);
    }

    /// <summary>WindowNumber が null の場合は 1 枚として扱う。</summary>
    [Fact]
    public void WindowArea_NullNumber_TreatedAsOne()
    {
      const string winConf = """
        "G2": {
          "windowArea": 2.5,
          "inputMethod": "ガラスの種類を入力",
          "frameType": "金属製",
          "layerType": "単層",
          "glassID": "T"
        }
        """;
      var r = Convert(BuildJson(
        windowList: """[ { "WindowID": "G2", "WindowNumber": null, "isBlind": "無", "EavesID": "無" } ]""",
        windowConfigure: winConf));

      Assert.Equal(2.5, Assert.Single(r.MultiRooms.Windows).Area, 9);
      Assert.Equal(47.5, EnvelopeWall(r).Area, 9);
    }

    /// <summary>窓 1 枚の面積が決まらない (面積・幅・高さすべて null) 場合は例外。</summary>
    [Fact]
    public void WindowArea_Undeterminable_Throws()
    {
      const string winConf = """
        "G2": {
          "windowArea": null,
          "windowWidth": null,
          "windowHeight": null,
          "inputMethod": "ガラスの種類を入力",
          "frameType": "金属製",
          "layerType": "単層",
          "glassID": "T"
        }
        """;
      string json = BuildJson(
        windowList: """[ { "WindowID": "G2", "WindowNumber": 1, "isBlind": "無", "EavesID": "無" } ]""",
        windowConfigure: winConf);
      var ex = Assert.Throws<PopoloArgumentException>(() => Convert(json));
      Assert.Contains("G2", ex.Message);
    }

    /// <summary>実サンプルのロビー南面: G1 (1 m²) × 16.64 → 窓 16.64 m²、壁 50-16.64 m²。</summary>
    [Fact]
    public void RealSample_LobbyWindowAndNetWallArea()
    {
      var path = Path.Combine(AppContext.BaseDirectory, "TestData", "builelib_input.json");
      var model = WebproJsonReader.ReadFromFile(path);
      var result = WebproToBuildingThermalModel.Convert(model, installHeatGainSchedulers: false);

      // ロビー南面の窓 (G1, windowArea=1 m² × WindowNumber=16.64)
      Assert.Contains(result.MultiRooms.Windows, w => Math.Abs(w.Area - 16.64) < 1e-9);
      Assert.Contains(result.MultiRooms.Walls, w => Math.Abs(w.Area - (50.0 - 16.64)) < 1e-9);
    }

    #endregion

    // ================================================================
    #region 4. 熱貫流率入力の壁、および層構成の妥当性

    /// <summary>熱貫流率入力 (U=0.5) の壁は、層熱抵抗 + R_IO が 1/U に一致する等価層を持つ。</summary>
    [Fact]
    public void UValueWall_SynthesizedLayersReproduceU()
    {
      const string wc = """
        "W1": {
          "structureType": "その他",
          "inputMethod": "熱貫流率を入力",
          "Uvalue": 0.5,
          "layers": []
        }
        """;
      var wall = EnvelopeWall(Convert(BuildJson(wallConfigure: wc)));
      Assert.NotEmpty(wall.Layers);
      Assert.Equal(1.0 / 0.5, LayerResistance(wall) + R_IO, 9);
    }

    /// <summary>断熱層が不要なほど U が大きい (U=5) 場合もコンクリート単層で U を再現する。</summary>
    [Fact]
    public void UValueWall_HighU_ReproducesU()
    {
      const string wc = """
        "W1": {
          "structureType": "その他",
          "inputMethod": "熱貫流率を入力",
          "Uvalue": 5.0,
          "layers": []
        }
        """;
      var wall = EnvelopeWall(Convert(BuildJson(wallConfigure: wc)));
      Assert.Equal(1.0 / 5.0, LayerResistance(wall) + R_IO, 9);
    }

    /// <summary>熱貫流率が未指定、または 1/R_IO 以上の場合は例外。</summary>
    [Theory]
    [InlineData("null")]
    [InlineData("7.0")]
    [InlineData("0")]
    public void UValueWall_InvalidU_Throws(string uValue)
    {
      string wc = $$"""
        "W1": {
          "structureType": "その他",
          "inputMethod": "熱貫流率を入力",
          "Uvalue": {{uValue}},
          "layers": []
        }
        """;
      string json = BuildJson(wallConfigure: wc);
      var ex = Assert.Throws<PopoloArgumentException>(() => Convert(json));
      Assert.Contains("W1", ex.Message);
    }

    /// <summary>建材構成入力で層が空の壁は、空の壁を作らず例外にする。</summary>
    [Fact]
    public void MaterialWall_EmptyLayers_Throws()
    {
      const string wc = """
        "W1": {
          "structureType": "その他",
          "inputMethod": "建材構成を入力",
          "layers": []
        }
        """;
      string json = BuildJson(wallConfigure: wc);
      var ex = Assert.Throws<PopoloArgumentException>(() => Convert(json));
      Assert.Contains("W1", ex.Message);
    }

    /// <summary>未対応の入力方法 (断熱材種類を入力) は例外にする。</summary>
    [Fact]
    public void InsulationTypeWall_NotSupported_Throws()
    {
      const string wc = """
        "W1": {
          "structureType": "鉄筋コンクリート造等",
          "inputMethod": "断熱材種類を入力",
          "layers": [ { "materialID": "押出法ポリスチレンフォーム　保温板　1種", "conductivity": null, "thickness": 25.0 } ]
        }
        """;
      string json = BuildJson(wallConfigure: wc);
      var ex = Assert.ThrowsAny<Exception>(() => Convert(json));
      Assert.Contains("W1", ex.Message);
    }

    /// <summary>層に熱伝導率 (conductivity) が明示されていればカタログ値より優先する。</summary>
    [Fact]
    public void MaterialWall_ConductivityOverride_IsHonored()
    {
      const string wc = """
        "W1": {
          "structureType": "その他",
          "inputMethod": "建材構成を入力",
          "layers": [
            { "materialID": "コンクリート", "conductivity": 0.5, "thickness": 100.0 },
            { "materialID": "コンクリート", "conductivity": null, "thickness": 100.0 }
          ]
        }
        """;
      var wall = EnvelopeWall(Convert(BuildJson(wallConfigure: wc)));
      // 逆順になるので layers[1] が conductivity 指定の層
      Assert.Equal(1.6 / 0.1, wall.Layers[0].HeatConductance, 9);
      Assert.Equal(0.5 / 0.1, wall.Layers[1].HeatConductance, 9);
      // 熱容量はカタログ値 (2000 kJ/(m³K)) のまま
      Assert.Equal(wall.Layers[0].HeatCapacity_F, wall.Layers[1].HeatCapacity_F, 9);
    }

    #endregion

    // ================================================================
    #region 5. 日の当たらない外壁

    /// <summary>日の当たらない外壁は日射吸収率 0 の外気境界として扱う。</summary>
    [Fact]
    public void ShadingExternalWall_ReceivesNoSolar()
    {
      var r = Convert(BuildJson(wallType: "日の当たらない外壁"));
      var wall = EnvelopeWall(r);
      Assert.Equal(0.0, wall.ShortWaveAbsorptanceF);
      // 外気温・長波放射の境界 (外気に面する壁) であることは維持
      Assert.Single(r.MultiRooms.GetOutsideWallReferences());
    }

    /// <summary>日の当たる外壁は従来どおり日射吸収率 (既定 0.7) を持つ。</summary>
    [Fact]
    public void ExternalWall_KeepsSolarAbsorptance()
    {
      var wall = EnvelopeWall(Convert(BuildJson()));
      Assert.Equal(WebproConversionConstants.DefaultSolarAbsorptionRatio, wall.ShortWaveAbsorptanceF);
    }

    #endregion
  }
}
