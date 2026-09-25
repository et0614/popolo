/* MultiRoomBoundaryTests.cs
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
using System.Linq;
using Xunit;
using Popolo.Core.Building;
using Popolo.Core.Building.Envelope;
using Popolo.Core.Climate;
using Popolo.Core.Exceptions;

namespace Popolo.Core.Tests.Building
{
  /// <summary>MultiRoom の境界条件（隣室温度差係数）と予測・確定サイクル（容量制限時の再予測）のテスト。</summary>
  public class MultiRoomBoundaryTests
  {
    #region Helpers

    private static readonly Incline IncS = new Incline(Incline.Orientation.S, 0.5 * Math.PI);

    /// <summary>木質繊維板（透湿・吸放湿あり）の層構成。</summary>
    private static WallLayer[] MakeMoistLayers()
    {
      var ls = new WallLayer[2];
      for (int i = 0; i < ls.Length; i++)
        ls[i] = new WallLayer("WoodFiberBoard", 0.1116, 585, 0.000004694, 0.788, 3080, 1.715, 0.006);
      return ls;
    }

    /// <summary>顕熱のみの層構成。</summary>
    private static WallLayer[] MakeDryLayers() => new[]
    {
      new WallLayer("Concrete", 1.6, 1896.0, 0.10),
      new WallLayer("Insulation", 0.04, 20.0, 0.05),
    };

    /// <summary>
    /// 単室モデル：壁0 は外壁（B 側屋外）、壁1 は B 側が隣室温度差係数 <paramref name="ftd"/> の隣室壁。
    /// </summary>
    private static BuildingThermalModel MakeAdjacentSpaceModel(bool moistureAware, double ftd)
    {
      var walls = new[]
      {
        new Wall(10.0, moistureAware ? MakeMoistLayers() : MakeDryLayers(), moistureAware),
        new Wall(10.0, moistureAware ? MakeMoistLayers() : MakeDryLayers(), moistureAware),
      };
      var zones = new[] { new Zone("Room", 3 * 3 * 3 * 1.2) };
      zones[0].VentilationRate = zones[0].AirMass / 3600.0 * 0.5;

      var mr = new MultiRoom(1, zones, walls, new Window[0]);
      mr.AddZone(0, 0);
      mr.AddWall(0, 0, true); mr.SetOutsideWall(0, false, IncS);
      mr.AddWall(0, 1, true); mr.UseAdjacentSpaceFactor(1, false, ftd);

      var bModel = new BuildingThermalModel(new[] { mr });
      bModel.TimeStep = 600;
      return bModel;
    }

    #endregion

    #region Adjacent-space factor

    /// <summary>
    /// 隣室温度差係数を用いた境界の湿度は、室内（裏面ゾーン）絶対湿度と外気絶対湿度の内挿になる。
    /// </summary>
    /// <remarks>
    /// かつて境界湿度を (1−f)·T_zone + f·w_out と、ゾーン「温度」から計算していた（回帰テスト）。
    /// 透湿壁では境界湿度が 10 kg/kg 程度となり、室内湿度が発散していた。
    /// </remarks>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AdjacentSpaceFactor_BoundaryHumidity_InterpolatesZoneAndOutdoorHumidity(bool moistureAware)
    {
      const double ftd = 0.3;
      const double wZone = 0.010;
      const double wOut = 0.004;
      var bModel = MakeAdjacentSpaceModel(moistureAware, ftd);
      bModel.InitializeAirState(22.0, wZone);
      // InitializeAirState は壁の含湿状態を初期化しないため、透湿壁は明示的に初期化する
      if (moistureAware)
        foreach (var wl in bModel.MultiRoom[0].Walls) ((Wall)wl).Initialize(22.0, wZone);

      var sun = new Sun(Sun.City.Tokyo);
      var dTime = new DateTime(2001, 1, 20, 0, 0, 0);
      sun.Update(dTime);
      bModel.UpdateOutdoorCondition(dTime, sun, 5.0, wOut, 0);
      bModel.ControlHeatSupply(0, 0, 0);
      bModel.ControlMoistureSupply(0, 0, 0);
      bModel.ForecastHeatTransfer();

      var wall = bModel.MultiRoom[0].Walls[1];
      Assert.Equal((1 - ftd) * wZone + ftd * wOut, wall.HumidityRatioB, 12);

      // 数ステップ進めても室内湿度は常識的範囲に収まる（旧実装では境界湿度が十数 kg/kg となり発散）。
      // 吸放湿壁の冷却に伴う吸湿で外気より低くなることはあり得るため、範囲は緩めに取る。
      bModel.ForecastWaterTransfer();
      bModel.FixState();
      for (int step = 0; step < 36; step++)
      {
        dTime = dTime.AddSeconds(600);
        sun.Update(dTime);
        bModel.UpdateOutdoorCondition(dTime, sun, 5.0, wOut, 0);
        bModel.ForecastHeatTransfer();
        bModel.ForecastWaterTransfer();
        bModel.FixState();
      }
      double w = bModel.MultiRoom[0].Zones[0].HumidityRatio;
      Assert.InRange(w, 0.0, wZone + 1e-6);
    }

    /// <summary>
    /// 隣室温度差係数を設定した面の裏面がゾーンに属していない場合、
    /// Validate がエラーを報告し、計算時は IndexOutOfRange ではなく明示的な例外になる。
    /// </summary>
    [Fact]
    public void AdjacentSpaceFactor_ReverseSideNotInZone_ValidateErrorAndClearException()
    {
      var walls = new[] { new Wall(10.0, MakeDryLayers()), new Wall(5.0, MakeDryLayers()) };
      var zones = new[] { new Zone("Room", 3 * 3 * 3 * 1.2) };
      var mr = new MultiRoom(1, zones, walls, new Window[0]);
      mr.AddZone(0, 0);
      mr.AddWall(0, 0, true); mr.SetOutsideWall(0, false, IncS);
      // 壁1 は両面とも隣室温度差係数（どちらの裏面もゾーンに属さない）
      mr.UseAdjacentSpaceFactor(1, true, 0.5);
      mr.UseAdjacentSpaceFactor(1, false, 0.5);

      var msgs = mr.Validate();
      Assert.Contains(msgs, m => m.Severity == ValidationSeverity.Error
          && m.Message.Contains("adjacent-space", StringComparison.OrdinalIgnoreCase)
          && m.Message.Contains("not attached to any zone"));

      var bModel = new BuildingThermalModel(new[] { mr });
      bModel.InitializeAirState(22.0, 0.008);
      var sun = new Sun(Sun.City.Tokyo);
      var dTime = new DateTime(2001, 1, 20, 0, 0, 0);
      sun.Update(dTime);
      bModel.UpdateOutdoorCondition(dTime, sun, 5.0, 0.004, 0);
      var ex = Record.Exception(() => bModel.ForecastHeatTransfer());
      // 並列計算時（既定）は Parallel.ForEach により AggregateException に包まれる
      if (ex is AggregateException ae) ex = ae.Flatten().InnerExceptions.Single();
      Assert.IsType<PopoloInvalidOperationException>(ex);
    }

    #endregion

    #region Re-forecast within a step (capacity limit)

    /// <summary>
    /// 水分を顕熱と別に解く MultiRoom で、顕熱の再予測（容量制限による負荷の頭打ち）が
    /// 起きても水分予測が失われない。
    /// </summary>
    /// <remarks>
    /// かつて 2 回目の ForecastHeatTransfer が室温と同時に絶対湿度もステップ開始時の値に戻し、
    /// 一方で ControlHeatSupply は水分の再予測フラグを立てないため ForecastWaterTransfer が
    /// スキップされ、FixState がステップ開始時の湿度を確定していた（回帰テスト）。
    /// 容量制限で暖房が頭打ちになる場合の結果が、最初から頭打ち値で固定負荷運転した場合と
    /// （温度・湿度とも）一致することを確認する。
    /// </remarks>
    [Fact]
    public void CapacityLimitedReforecast_SeparateMoisture_KeepsMoistureForecast()
    {
      const double capacity = 300.0;      // 暖房能力[W]
      const double moistureGain = 5e-6;   // 水分発生[kg/s]

      var bCap = MakeAdjacentSpaceModel(false, 0.5);
      var bRef = MakeAdjacentSpaceModel(false, 0.5);
      Assert.False(bCap.MultiRoom[0].SolveMoistureTransferSimultaneously);
      foreach (var b in new[] { bCap, bRef })
      {
        b.InitializeAirState(15.0, 0.005);
        b.SetBaseHeatGain(0, 0, 0.0, 0.0, moistureGain);
        b.SetHeatingCapacity(0, 0, capacity);
      }

      var sun = new Sun(Sun.City.Tokyo);
      var dTime = new DateTime(2001, 1, 20, 0, 0, 0);
      double w0 = bCap.MultiRoom[0].Zones[0].HumidityRatio;
      for (int step = 0; step < 24; step++)
      {
        sun.Update(dTime);
        foreach (var b in new[] { bCap, bRef })
          b.UpdateOutdoorCondition(dTime, sun, 0.0, 0.002, 0);

        // 容量制限付き：設定温度 22°C では能力不足 → 能力値で頭打ちに切り替えて再予測
        bCap.ControlDryBulbTemperature(0, 0, 22.0);
        bCap.ControlMoistureSupply(0, 0, 0);
        bCap.UpdateHeatTransferWithinCapacityLimit();

        // 参照：最初から能力値の固定負荷
        bRef.ControlHeatSupply(0, 0, capacity);
        bRef.ControlMoistureSupply(0, 0, 0);
        bRef.ForecastHeatTransfer();
        bRef.ForecastWaterTransfer();
        bRef.FixState();

        var zc = bCap.MultiRoom[0].Zones[0];
        var zr = bRef.MultiRoom[0].Zones[0];
        Assert.Equal(capacity, zc.HeatSupply, 9);
        Assert.Equal(zr.Temperature, zc.Temperature, 9);
        Assert.Equal(zr.HumidityRatio, zc.HumidityRatio, 12);

        dTime = dTime.AddSeconds(600);
      }
      // 湿度は水分発生により実際に変化している
      Assert.True(Math.Abs(bCap.MultiRoom[0].Zones[0].HumidityRatio - w0) > 1e-4,
          $"humidity did not evolve: w0={w0}, w={bCap.MultiRoom[0].Zones[0].HumidityRatio}");
    }

    /// <summary>
    /// 水分を先に予測してから顕熱を予測する順序でも、再予測で水分予測が二重に進まない。
    /// </summary>
    /// <remarks>
    /// 水分予測済みの状態で顕熱予測を開始すると、ステップ開始時湿度の退避値が水分予測値で
    /// 上書きされ、水分の再予測がその値を起点に 2 ステップ分進んでいた（回帰テスト）。
    /// </remarks>
    [Fact]
    public void Reforecast_WaterBeforeHeat_SameAsHeatBeforeWater()
    {
      const double moistureGain = 5e-6;
      var bA = MakeAdjacentSpaceModel(false, 0.5);
      var bB = MakeAdjacentSpaceModel(false, 0.5);
      foreach (var b in new[] { bA, bB })
      {
        b.InitializeAirState(15.0, 0.005);
        b.SetBaseHeatGain(0, 0, 0.0, 0.0, moistureGain);
      }

      var sun = new Sun(Sun.City.Tokyo);
      var dTime = new DateTime(2001, 1, 20, 0, 0, 0);
      for (int step = 0; step < 12; step++)
      {
        sun.Update(dTime);
        foreach (var b in new[] { bA, bB })
        {
          b.UpdateOutdoorCondition(dTime, sun, 0.0, 0.002, 0);
          b.ControlHeatSupply(0, 0, 100.0);
          b.ControlMoistureSupply(0, 0, 0);
        }
        // A: 顕熱 → 水分 → (条件変更) → 顕熱 → 水分
        bA.ForecastHeatTransfer();
        bA.ForecastWaterTransfer();
        bA.ControlHeatSupply(0, 0, 200.0);
        bA.ControlMoistureSupply(0, 0, 1e-6);
        bA.ForecastHeatTransfer();
        bA.ForecastWaterTransfer();
        bA.FixState();
        // B: 水分 → 顕熱 → (条件変更) → 顕熱 → 水分
        bB.ForecastWaterTransfer();
        bB.ForecastHeatTransfer();
        bB.ControlHeatSupply(0, 0, 200.0);
        bB.ControlMoistureSupply(0, 0, 1e-6);
        bB.ForecastHeatTransfer();
        bB.ForecastWaterTransfer();
        bB.FixState();

        Assert.Equal(bA.MultiRoom[0].Zones[0].Temperature, bB.MultiRoom[0].Zones[0].Temperature, 9);
        Assert.Equal(bA.MultiRoom[0].Zones[0].HumidityRatio, bB.MultiRoom[0].Zones[0].HumidityRatio, 12);
        dTime = dTime.AddSeconds(600);
      }
    }

    #endregion
  }
}
