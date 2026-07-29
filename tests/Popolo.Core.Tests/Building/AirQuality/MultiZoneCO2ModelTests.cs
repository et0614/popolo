/* MultiZoneCO2ModelTests.cs
 *
 * Copyright (C) 2026 E.Togashi
 * GNU General Public License v3 — see accompanying LICENSE file.
 */

using System;
using Xunit;
using Popolo.Core.Building;
using Popolo.Core.Building.AirQuality;
using Popolo.Core.Building.Envelope;
using Popolo.Core.Exceptions;

namespace Popolo.Core.Tests.Building.AirQuality
{
  /// <summary>Unit tests for <see cref="MultiZoneCO2Model"/>.</summary>
  public class MultiZoneCO2ModelTests
  {
    #region ヘルパー

    /// <summary>2ゾーンの最小BuildingThermalModel（流量の設定・読み取り用）。</summary>
    private static BuildingThermalModel MakeThermalModel(out Zone znA, out Zone znB)
    {
      znA = new Zone("thermalA", 120);  //気積100m3相当
      znB = new Zone("thermalB", 120);
      var mr = new MultiRoom(1, new[] { znA, znB }, new Wall[0], new Window[0]);
      return new BuildingThermalModel(new[] { mr });
    }

    #endregion

    #region 単一ゾーン（解析解との照合）

    /// <summary>
    /// 単一ゾーン・定常入力の陰解法計算が、時間刻みを細かくすると
    /// Seidelの解析解（例題26.1条件）に収束する。
    /// </summary>
    [Fact]
    public void SingleZone_ConvergesToSeidelSolution()
    {
      var zn = new CO2ModelZone("office", 2700.0);
      zn.CO2Level_PPM = 500.0;
      zn.CO2Generation = 5.56e-4;
      zn.AuxiliaryVentilationRate = 0.301;
      zn.AuxiliaryVentilationCO2Level_PPM = 400.0;
      var model = new MultiZoneCO2Model(new[] { zn });

      //60秒刻みで1時間
      for (int i = 0; i < 60; i++) model.Update(60.0);

      double exact = CO2Balance.GetConcentration(
        500e-6, 400e-6, 5.56e-4, 0.301, 2700.0, 3600.0);
      Assert.Equal(exact, zn.CO2Level, 4e-6);
    }

    /// <summary>十分な時間の後に定常濃度（流入濃度+発生量/換気量）に収束する。</summary>
    [Fact]
    public void SingleZone_ReachesSteadyState()
    {
      var zn = new CO2ModelZone("office", 2700.0);
      zn.CO2Level_PPM = 500.0;
      zn.CO2Generation = 5.56e-4;
      zn.AuxiliaryVentilationRate = 0.301;
      zn.AuxiliaryVentilationCO2Level_PPM = 400.0;
      var model = new MultiZoneCO2Model(new[] { zn });

      for (int i = 0; i < 300; i++) model.Update(600.0);

      Assert.Equal(400e-6 + 5.56e-4 / 0.301, zn.CO2Level, 1e-6);
    }

    #endregion

    #region 熱モデル連携（A系統・ゾーン間換気）

    /// <summary>
    /// 紐づけゾーンのVentilationRateは毎ステップ読み直される：
    /// 換気0で上昇した濃度が、換気を設定した後に低下へ転じる。
    /// </summary>
    [Fact]
    public void BoundZone_FollowsVentilationRateChange()
    {
      var bModel = MakeThermalModel(out Zone znA, out _);
      znA.VentilationRate = 0.0;

      var czA = new CO2ModelZone("A", 100.0, znA);
      czA.CO2Generation = 1e-4;
      var model = new MultiZoneCO2Model(new[] { czA }, bModel);

      //換気なし: 上昇
      double c0 = czA.CO2Level;
      for (int i = 0; i < 10; i++) model.Update(600.0);
      double c1 = czA.CO2Level;
      Assert.True(c0 < c1, $"no vent: {c0:G4} -> {c1:G4} (rise)");

      //換気開始（0.3 m3/s = 0.36 kg/s）: 低下に転じる
      znA.VentilationRate = 0.36;
      for (int i = 0; i < 100; i++) model.Update(600.0);
      double c2 = czA.CO2Level;
      Assert.True(c2 < c1, $"vented: {c1:G4} -> {c2:G4} (fall)");

      //定常値 = 外気濃度 + G/Q
      Assert.Equal(model.OutdoorCO2Level + 1e-4 / 0.3, c2, 1e-6);
    }

    /// <summary>
    /// 換気ユニットが片方のゾーンにしかない場合、ゾーン間換気が
    /// あれば両ゾーンとも定常濃度に収束する（発散しない）。
    /// A: 発生のみ、B: 換気のみ、A-B間に対称ゾーン間換気。
    /// </summary>
    [Fact]
    public void CrossVentilation_PreventsDivergence()
    {
      var bModel = MakeThermalModel(out Zone znA, out Zone znB);
      znA.VentilationRate = 0.0;
      znB.VentilationRate = 0.12;              //0.1 m3/s
      bModel.SetCrossVentilation(0, 0, 1, 0.24);  //0.2 m3/s（双方向）

      var czA = new CO2ModelZone("A", 100.0, znA);
      var czB = new CO2ModelZone("B", 100.0, znB);
      czA.CO2Generation = 1e-4;
      var model = new MultiZoneCO2Model(new[] { czA, czB }, bModel);

      for (int i = 0; i < 600; i++) model.Update(60.0);

      //定常: C_B = C_OA + G/Q_B, C_A = C_B + G/q_x
      double cbExpected = model.OutdoorCO2Level + 1e-4 / 0.1;
      double caExpected = cbExpected + 1e-4 / 0.2;
      Assert.Equal(cbExpected, czB.CO2Level, 1e-5);
      Assert.Equal(caExpected, czA.CO2Level, 1e-5);
    }

    /// <summary>ゾーン間換気なしでは無換気ゾーンの濃度が際限なく上昇する。</summary>
    [Fact]
    public void NoCrossVentilation_UnventilatedZoneDiverges()
    {
      var bModel = MakeThermalModel(out Zone znA, out Zone znB);
      znA.VentilationRate = 0.0;
      znB.VentilationRate = 0.12;

      var czA = new CO2ModelZone("A", 100.0, znA);
      var czB = new CO2ModelZone("B", 100.0, znB);
      czA.CO2Generation = 1e-4;
      var model = new MultiZoneCO2Model(new[] { czA, czB }, bModel);

      for (int i = 0; i < 600; i++) model.Update(60.0);

      //G*t/V = 1e-4 * 36000 / 100 = 0.036 m3/m3 (36,000ppm) まで線形上昇
      Assert.Equal(400e-6 + 0.036, czA.CO2Level, 1e-6);
      //換気のあるBは外気濃度のまま
      Assert.Equal(400e-6, czB.CO2Level, 1e-8);
    }

    /// <summary>一方向流（A→B）ではBのみが汚染され、Aは影響を受けない。</summary>
    [Fact]
    public void OneWayFlow_ContaminatesDownstreamOnly()
    {
      var bModel = MakeThermalModel(out Zone znA, out Zone znB);
      bModel.SetAirFlow(0, 0, 1, 0.12);  //A→B 0.1 m3/s

      var czA = new CO2ModelZone("A", 100.0, znA);
      var czB = new CO2ModelZone("B", 100.0, znB);
      czA.CO2Level_PPM = 1000.0;
      czB.CO2Level_PPM = 400.0;
      var model = new MultiZoneCO2Model(new[] { czA, czB }, bModel);

      for (int i = 0; i < 100; i++) model.Update(60.0);

      //上流Aは不変、下流BはAの濃度に漸近
      Assert.Equal(1000.0, czA.CO2Level_PPM, 1e-6);
      Assert.True(400.0 < czB.CO2Level_PPM && czB.CO2Level_PPM <= 1000.0,
        $"B = {czB.CO2Level_PPM:F1} ppm");
    }

    /// <summary>換気・発生のない対称ゾーン間換気ではCO2総量が保存され、加重平均濃度に収束する。</summary>
    [Fact]
    public void SymmetricExchange_ConservesTotalCO2()
    {
      var bModel = MakeThermalModel(out Zone znA, out Zone znB);
      bModel.SetCrossVentilation(0, 0, 1, 0.24);

      var czA = new CO2ModelZone("A", 100.0, znA);
      var czB = new CO2ModelZone("B", 200.0, znB);
      czA.CO2Level_PPM = 1000.0;
      czB.CO2Level_PPM = 400.0;
      var model = new MultiZoneCO2Model(new[] { czA, czB }, bModel);

      double total0 = czA.Volume * czA.CO2Level + czB.Volume * czB.CO2Level;
      for (int i = 0; i < 200; i++)
      {
        model.Update(60.0);
        double total = czA.Volume * czA.CO2Level + czB.Volume * czB.CO2Level;
        Assert.Equal(total0, total, total0 * 1e-10);
      }

      //加重平均 (1000*100 + 400*200)/300 = 600 ppm に収束
      Assert.Equal(600.0, czA.CO2Level_PPM, 0.1);
      Assert.Equal(600.0, czB.CO2Level_PPM, 0.1);
    }

    #endregion

    #region プロパティ・例外

    /// <summary>ppmプロパティはm3/m3の主プロパティの10^6倍のビューである。</summary>
    [Fact]
    public void PPMProperties_AreScaledViews()
    {
      var zn = new CO2ModelZone("z", 100.0);
      zn.CO2Level_PPM = 1000.0;
      Assert.Equal(0.001, zn.CO2Level, precision: 12);
      zn.CO2Level = 450e-6;
      Assert.Equal(450.0, zn.CO2Level_PPM, precision: 9);

      var model = new MultiZoneCO2Model(new[] { zn });
      Assert.Equal(400.0, model.OutdoorCO2Level_PPM, precision: 9);
      model.OutdoorCO2Level_PPM = 420.0;
      Assert.Equal(420e-6, model.OutdoorCO2Level, precision: 12);
    }

    /// <summary>ゾーンを紐づけたのに熱モデルを渡さない場合には例外を投げる。</summary>
    [Fact]
    public void BoundZoneWithoutBuildingModel_Throws()
    {
      MakeThermalModel(out Zone znA, out _);
      var cz = new CO2ModelZone("A", 100.0, znA);

      Assert.Throws<PopoloArgumentException>(
        () => new MultiZoneCO2Model(new[] { cz }));
    }

    /// <summary>紐づけたゾーンが熱モデルに存在しない場合には例外を投げる。</summary>
    [Fact]
    public void BoundZoneNotInBuildingModel_Throws()
    {
      var bModel = MakeThermalModel(out _, out _);
      var orphan = new Zone("orphan", 120);
      var cz = new CO2ModelZone("A", 100.0, orphan);

      Assert.Throws<PopoloArgumentException>(
        () => new MultiZoneCO2Model(new[] { cz }, bModel));
    }

    /// <summary>ゾーン気積0以下・時間刻み0以下は例外を投げる。</summary>
    [Fact]
    public void InvalidArguments_Throw()
    {
      Assert.Throws<PopoloArgumentException>(() => new CO2ModelZone("z", 0.0));
      var model = new MultiZoneCO2Model(new[] { new CO2ModelZone("z", 100.0) });
      Assert.Throws<PopoloArgumentException>(() => model.Update(0.0));
    }

    #endregion
  }
}
