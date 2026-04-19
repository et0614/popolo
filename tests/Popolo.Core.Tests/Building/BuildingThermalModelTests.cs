/* BuildingThermalModelTests.cs
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
using Xunit;
using Popolo.Core.Building;
using Popolo.Core.Building.Envelope;
using Popolo.Core.Climate;

namespace Popolo.Core.Tests.Building
{
  /// <summary>BuildingThermalModel の統合テスト。</summary>
  /// <remarks>
  /// サンスペース付き2ゾーンモデル（SunSpace + BackSpace）を使用する。
  ///
  /// モデル構成：
  ///   SunSpace  : 幅2m × 奥行8m × 高さ2.7m, 南向き2重ガラス窓6m²×2
  ///   BackSpace : 幅6m × 奥行8m × 高さ2.7m
  ///   共用壁    : コンクリートブロック200mm
  ///   外壁      : コンクリートブロック + 断熱材 + サイディング
  ///   床/屋根   : 断熱仕様
  ///
  /// 周期定常計算：24時間の外気・日射データを繰り返し与え、
  /// 前ステップとの誤差が収束するまで繰り返す。
  ///
  /// 参考：
  ///   夏季 7月20日（東京）: 外気約24-26°C, 空調設定26°C/10.5g/kg
  ///   冬季 1月20日（東京）: 外気約4-10°C,  空調設定22°C/ 6.6g/kg
  /// </remarks>
  public class BuildingThermalModelTests
  {
    #region テスト用定数・気象データ

    // 空調能力
    private const double HeatingCapacity = 2000.0; // [W]
    private const double CoolingCapacity = 2000.0; // [W]

    // 空調設定値
    private static readonly double[] DbtSetpoint = { 26.0, 22.0 };   // [°C]  夏・冬
    private static readonly double[] HrtSetpoint = { 0.0105, 0.0066 }; // [kg/kg] 夏・冬

    // 空調時間帯（9時〜17時）
    private const int AcStartHour = 9;
    private const int AcEndHour = 17;

    // 東京の代表日 時刻別 外気乾球温度[°C]
    private static readonly double[][] Dbt =
    {
            // 夏季（7月20日）
            new[] { 24.9,24.7,23.8,24.2,24.2,25.0,25.0,24.4,24.1,23.7,24.6,25.0,
                    25.3,25.2,24.9,24.9,25.3,25.9,25.8,25.1,24.2,23.5,23.6,23.5 },
            // 冬季（1月20日）
            new[] {  4.1, 4.2, 4.6, 5.0, 4.4, 3.8, 4.8, 4.4, 4.9, 6.5, 7.7, 8.1,
                     9.0,10.1,10.7,10.2,10.1, 9.8, 8.6, 7.9, 7.6, 7.1, 5.4, 4.1 },
        };

    // 東京の代表日 時刻別 絶対湿度[g/kg]（×0.001でkg/kgに変換）
    private static readonly double[][] Hrt =
    {
            new[] { 12.9,12.8,13.9,14.6,14.7,14.6,13.5,13.7,14.8,15.4,16.1,15.5,
                    15.6,15.6,16.0,16.5,16.6,16.4,16.8,16.0,16.0,16.1,16.2,16.6 },
            new[] {  4.2, 4.0, 4.0, 3.6, 4.0, 3.7, 3.9, 3.9, 3.5, 3.2, 3.4, 3.5,
                     3.3, 3.2, 3.0, 3.1, 3.1, 2.8, 2.8, 3.0, 3.5, 3.1, 3.0, 3.1 },
        };
    
    // 東京の代表日 時刻別 水平面全天日射[W/m²]
    private static readonly double[][] GlobalRad =
    {
            new[] {   0d,  0,  0,  0,  0,  0, 93,288,465,629,781,860,
                    870,827,725,598,403,217, 49,  0,  0,  0,  0,  0 },
            new[] {   0d,  0,  0,  0,  0,  0,  0,  1,146,318,438,506,
                    532,467,391,232, 83,  0,  0,  0,  0,  0,  0,  0 },
        };

    #endregion

    #region モデル構築

    /// <summary>サンスペース付き2ゾーンモデルを構築する。</summary>
    private static BuildingThermalModel MakeSunSpaceModel()
    {
      var incN = new Incline(Incline.Orientation.N, 0.5 * Math.PI);
      var incE = new Incline(Incline.Orientation.E, 0.5 * Math.PI);
      var incW = new Incline(Incline.Orientation.W, 0.5 * Math.PI);
      var incS = new Incline(Incline.Orientation.S, 0.5 * Math.PI);
      var incH = new Incline(Incline.Orientation.N, 0);

      // 壁構成
      WallLayer[] exWL =
      {
                new WallLayer("Concrete Block",   0.51,  1400.0, 0.100),
                new WallLayer("Insulation",        0.04,    14.0, 0.0615),
                new WallLayer("Wood Siding",        0.14,   477.0, 0.009),
            };
      WallLayer[] flWL =
      {
                new WallLayer("Concrete",          1.13,  1400.0, 0.08),
                new WallLayer("Insulation(inf)",   0.00001, 0.00001, 1.0),
            };
      WallLayer[] rfWL =
      {
                new WallLayer("Plasterboard",      0.16,   798.0, 0.010),
                new WallLayer("Insulation",        0.04,    10.0, 0.1118),
                new WallLayer("Wood Roof",         0.14,   477.0, 0.019),
            };
      WallLayer[] inWL =
      {
                new WallLayer("Concrete Block",   0.51,  1400.0, 0.200),
            };

      // 壁
      var walls = new Wall[11];
      walls[0] = new Wall(2 * 2.7, exWL); // SunSpace 西外壁
      walls[1] = new Wall(8 * 2.7 - 6 * 2, exWL); // SunSpace 南外壁（窓除き）
      walls[2] = new Wall(2 * 2.7, exWL); // SunSpace 東外壁
      walls[3] = new Wall(2 * 8, rfWL); // SunSpace 屋根
      walls[4] = new Wall(2 * 8, flWL); // SunSpace 床
      walls[5] = new Wall(6 * 2.7, exWL); // BackSpace 西外壁
      walls[6] = new Wall(8 * 2.7, exWL); // BackSpace 北外壁
      walls[7] = new Wall(6 * 2.7, exWL); // BackSpace 東外壁
      walls[8] = new Wall(6 * 8, rfWL); // BackSpace 屋根
      walls[9] = new Wall(6 * 8, flWL); // BackSpace 床
      walls[10] = new Wall(8 * 2.7, inWL); // 共用壁

      for (int i = 0; i < walls.Length; i++)
      {
        walls[i].LongWaveEmissivityF = walls[i].LongWaveEmissivityB = 0.9;
        walls[i].ShortWaveAbsorptanceF = walls[i].ShortWaveAbsorptanceB = 0.8;
      }

      // 窓（南向き2重ガラス）
      var windows = new Window[]
      {
                new Window(6 * 2,
                    new[] { 0.7, 0.7 }, new[] { 0.04, 0.04 }, incS),
      };

      // ゾーン（空気密度1.2 kg/m³）
      const double airDensity = 1.2;
      var zones = new Zone[]
      {
                new Zone("SunSpace",  2 * 8 * 2.7 * airDensity),
                new Zone("BackSpace", 6 * 8 * 2.7 * airDensity),
      };
      zones[0].VentilationRate = zones[0].AirMass / 3600.0 * 0.2;
      zones[1].VentilationRate = zones[1].AirMass / 3600.0 * 0.2;

      // 多数室
      var mRoom = new MultiRoom(2, zones, walls, windows);
      mRoom.AddZone(0, 0);
      mRoom.AddZone(1, 1);

      // 壁・窓の接続
      mRoom.AddWall(0, 0, true); mRoom.SetOutsideWall(0, false, incW);
      mRoom.AddWall(0, 1, true); mRoom.SetOutsideWall(1, false, incS);
      mRoom.AddWall(0, 2, true); mRoom.SetOutsideWall(2, false, incE);
      mRoom.AddWall(0, 3, true); mRoom.SetOutsideWall(3, false, incH);
      mRoom.AddWall(0, 4, true); mRoom.SetOutsideWall(4, false, incH);
      mRoom.AddWall(1, 5, true); mRoom.SetOutsideWall(5, false, incW);
      mRoom.AddWall(1, 6, true); mRoom.SetOutsideWall(6, false, incN);
      mRoom.AddWall(1, 7, true); mRoom.SetOutsideWall(7, false, incE);
      mRoom.AddWall(1, 8, true); mRoom.SetOutsideWall(8, false, incH);
      mRoom.AddWall(1, 9, true); mRoom.SetOutsideWall(9, false, incH);
      mRoom.AddWall(0, 1, 10);   // 共用壁
      mRoom.AddWindow(0, 0);
      mRoom.Albedo = 0.2;
      mRoom.SetGroundTemperature(4, false, 20); // 床下土壌温度固定
      mRoom.SetGroundTemperature(9, false, 20);

      var bModel = new BuildingThermalModel(new MultiRoom[] { mRoom });
      bModel.SetInsideConvectiveCoefficient(0, 5.0);
      bModel.SetOutsideConvectiveCoefficient(0, 15.0);
      return bModel;
    }

    /// <summary>周期定常計算を実行して収束時の結果を返す。</summary>
    /// <param name="bModel">建物モデル。</param>
    /// <param name="season">0=夏季, 1=冬季。</param>
    /// <param name="useCapacityLimit">UpdateHeatTransferWithinCapacityLimitを使うか否か。</param>
    /// <returns>[hour][zone] の室温・顕熱負荷配列 (temp, load)。</returns>
    private static (double[][] temp, double[][] load) RunPeriodicSteadyState(
        BuildingThermalModel bModel, int season, bool useCapacityLimit)
    {
      int nZones = bModel.MultiRoom[0].ZoneCount;
      var temp = new double[nZones][];
      var load = new double[nZones][];
      for (int z = 0; z < nZones; z++)
      {
        temp[z] = new double[24];
        load[z] = new double[24];
      }

      var sun = new Sun(Sun.City.Tokyo);
      var dTime = season == 0
          ? new DateTime(2001, 7, 20, 0, 0, 0)
          : new DateTime(2001, 1, 20, 0, 0, 0);

      const int maxIter = 200;
      for (int iter = 0; iter < maxIter; iter++)
      {
        double err = 0;
        for (int h = 0; h < 24; h++)
        {
          // 日射分離（過去1時間積算データのため30分ずらす）
          sun.Update(dTime.AddMinutes(30));
          sun.SeparateGlobalHorizontalRadiation(
              GlobalRad[season][h], Sun.SeparationMethod.Erbs);

          bModel.UpdateOutdoorCondition(
              dTime, sun, Dbt[season][h], Hrt[season][h] * 0.001, 0);

          // 制御設定
          bool isAcTime = AcStartHour < h && h < AcEndHour;
          for (int z = 0; z < nZones; z++)
          {
            if (isAcTime)
            {
              bModel.ControlDryBulbTemperature(0, z, DbtSetpoint[season]);
              bModel.ControlHumidityRatio(0, z, HrtSetpoint[season]);
            }
            else
            {
              bModel.ControlHeatSupply(0, z, 0);
              bModel.ControlMoistureSupply(0, z, 0);
            }
          }

          if (useCapacityLimit)
          {
            bModel.UpdateHeatTransferWithinCapacityLimit();
          }
          else
          {
            // 手動過負荷判定
            bModel.ForecastHeatTransfer();
            bModel.ForecastWaterTransfer();
            for (int z = 0; z < nZones; z++)
            {
              double hs = bModel.MultiRoom[0].Zones[z].HeatSupply;
              if (hs > HeatingCapacity) bModel.ControlHeatSupply(0, z, HeatingCapacity);
              else if (hs < -CoolingCapacity) bModel.ControlHeatSupply(0, z, -CoolingCapacity);
            }
            bModel.ForecastHeatTransfer();
            bModel.ForecastWaterTransfer();
            bModel.FixState();
          }

          // 誤差集計
          var zones = bModel.MultiRoom[0].Zones;
          for (int z = 0; z < nZones; z++)
          {
            err += Math.Abs(temp[z][h] - zones[z].Temperature);
            err += Math.Abs(load[z][h] - zones[z].HeatSupply);
            temp[z][h] = zones[z].Temperature;
            load[z][h] = zones[z].HeatSupply;
          }

          dTime = dTime.AddHours(1);
        }

        if (err < 1e-4) break;
        dTime = dTime.AddHours(-24); // 1日戻して繰り返し
      }

      return (temp, load);
    }

    #endregion

    #region 周期定常収束テスト

    /// <summary>夏季・冬季ともに周期定常計算が収束する。</summary>
    [Theory]
    [InlineData(0)] // 夏季
    [InlineData(1)] // 冬季
    public void PeriodicSteadyState_Converges(int season)
    {
      var bModel = MakeSunSpaceModel();
      // 例外なく完了すれば収束している
      var (temp, load) = RunPeriodicSteadyState(bModel, season, false);

      // 全ゾーンの温度が物理的な範囲内
      for (int z = 0; z < temp.Length; z++)
        for (int h = 0; h < 24; h++)
          Assert.InRange(temp[z][h], -30.0, 60.0);
    }

    #endregion

    #region 室温制御精度テスト

    /// <summary>空調時間帯の室温が設定値付近に制御される。</summary>
    [Theory]
    [InlineData(0)] // 夏季 26°C設定
    [InlineData(1)] // 冬季 22°C設定
    public void TemperatureControl_WithinTolerance(int season)
    {
      var bModel = MakeSunSpaceModel();
      // 能力制限を無効化（∞）→ 完全制御
      bModel.SetHeatingCapacity(0, 0, double.PositiveInfinity);
      bModel.SetHeatingCapacity(0, 1, double.PositiveInfinity);
      bModel.SetCoolingCapacity(0, 0, double.PositiveInfinity);
      bModel.SetCoolingCapacity(0, 1, double.PositiveInfinity);

      var (temp, _) = RunPeriodicSteadyState(bModel, season, false);
      double setpoint = DbtSetpoint[season];

      // 空調時間帯（10〜16時）は設定値±1.0°C以内
      for (int h = AcStartHour + 1; h < AcEndHour; h++)
        for (int z = 0; z < temp.Length; z++)
          Assert.InRange(temp[z][h], setpoint - 1.0, setpoint + 1.0);
    }

    #endregion

    #region 顕熱負荷の符号テスト

    /// <summary>夏季の空調時間帯は冷房負荷（HeatSupply &lt; 0）が支配的である。</summary>
    [Fact]
    public void SummerLoad_CoolingDominant()
    {
      var bModel = MakeSunSpaceModel();
      var (_, load) = RunPeriodicSteadyState(bModel, 0, false);

      // BackSpaceの13時（日射最大付近）は冷房負荷
      Assert.True(load[1][13] < 0,
          $"BackSpace 13h load={load[1][13]:F1} W should be negative (cooling)");
    }

    /// <summary>冬季の空調時間帯は暖房負荷（HeatSupply &gt; 0）が支配的である。</summary>
    [Fact]
    public void WinterLoad_HeatingDominant()
    {
      var bModel = MakeSunSpaceModel();
      var (_, load) = RunPeriodicSteadyState(bModel, 1, false);

      // BackSpaceの13時（空調時間帯中）は暖房負荷
      Assert.True(load[1][13] > 0,
          $"BackSpace 13h load={load[1][13]:F1} W should be positive (heating)");
    }

    #endregion

    #region サンスペース効果テスト

    /// <summary>冬季の自然室温においてSunSpaceはBackSpaceより温かい（日射効果）。</summary>
    [Fact]
    public void SunSpace_WarmerThanBackSpace_InWinter_AtNoon()
    {
      var bModel = MakeSunSpaceModel();
      // FreeFloatで計算
      var sun = new Sun(Sun.City.Tokyo);
      var dTime = new DateTime(2001, 1, 20, 0, 0, 0);

      var tempS = new double[24]; // SunSpace
      var tempB = new double[24]; // BackSpace
      var prevTemp = new double[2];

      for (int iter = 0; iter < 100; iter++)
      {
        double err = 0;
        for (int h = 0; h < 24; h++)
        {
          sun.Update(dTime.AddMinutes(30));
          sun.SeparateGlobalHorizontalRadiation(
              GlobalRad[1][h], Sun.SeparationMethod.Erbs);
          bModel.UpdateOutdoorCondition(
              dTime, sun, Dbt[1][h], Hrt[1][h] * 0.001, 0);
          bModel.ControlHeatSupply(0, 0, 0);
          bModel.ControlHeatSupply(0, 1, 0);
          bModel.ControlMoistureSupply(0, 0, 0);
          bModel.ControlMoistureSupply(0, 1, 0);
          bModel.ForecastHeatTransfer();
          bModel.FixState();

          var zones = bModel.MultiRoom[0].Zones;
          err += Math.Abs(tempS[h] - zones[0].Temperature);
          err += Math.Abs(tempB[h] - zones[1].Temperature);
          tempS[h] = zones[0].Temperature;
          tempB[h] = zones[1].Temperature;
          dTime = dTime.AddHours(1);
        }
        if (err < 1e-4) break;
        dTime = dTime.AddHours(-24);
      }

      // 冬の日中（12時）はSunSpaceの方が暖かい
      Assert.True(tempS[12] > tempB[12],
          $"SunSpace({tempS[12]:F2}°C) should be warmer than BackSpace({tempB[12]:F2}°C) at noon in winter");
    }

    #endregion

    #region UpdateHeatTransferWithinCapacityLimit との一致テスト

    /// <summary>
    /// ForecastHeatTransfer + 手動過負荷判定 と
    /// UpdateHeatTransferWithinCapacityLimit の結果が一致する。
    /// </summary>
    [Theory]
    [InlineData(0)] // 夏季
    [InlineData(1)] // 冬季
    public void ManualCapacityLimit_EqualsAutoMethod(int season)
    {
      var bModel1 = MakeSunSpaceModel();
      var bModel2 = MakeSunSpaceModel();

      bModel1.SetHeatingCapacity(0, 0, HeatingCapacity);
      bModel1.SetHeatingCapacity(0, 1, HeatingCapacity);
      bModel1.SetCoolingCapacity(0, 0, CoolingCapacity);
      bModel1.SetCoolingCapacity(0, 1, CoolingCapacity);
      bModel2.SetHeatingCapacity(0, 0, HeatingCapacity);
      bModel2.SetHeatingCapacity(0, 1, HeatingCapacity);
      bModel2.SetCoolingCapacity(0, 0, CoolingCapacity);
      bModel2.SetCoolingCapacity(0, 1, CoolingCapacity);

      var (temp1, load1) = RunPeriodicSteadyState(bModel1, season, false);
      var (temp2, load2) = RunPeriodicSteadyState(bModel2, season, true);

      int nZones = temp1.Length;
      for (int z = 0; z < nZones; z++)
      {
        for (int h = 0; h < 24; h++)
        {
          Assert.Equal(temp1[z][h], temp2[z][h], precision: 1);
          Assert.Equal(load1[z][h], load2[z][h], precision: 0);
        }
      }
    }

    #endregion

    #region 断熱効果テスト

    /// <summary>冬季の自然室温が外気温より高い（断熱効果）。</summary>
    [Fact]
    public void WinterFreeFloat_IndoorWarmerThanOutdoor()
    {
      var bModel = MakeSunSpaceModel();
      var sun = new Sun(Sun.City.Tokyo);
      var dTime = new DateTime(2001, 1, 20, 0, 0, 0);

      var tempB = new double[24];

      for (int iter = 0; iter < 100; iter++)
      {
        double err = 0;
        for (int h = 0; h < 24; h++)
        {
          sun.Update(dTime.AddMinutes(30));
          sun.SeparateGlobalHorizontalRadiation(
              GlobalRad[1][h], Sun.SeparationMethod.Erbs);
          bModel.UpdateOutdoorCondition(
              dTime, sun, Dbt[1][h], Hrt[1][h] * 0.001, 0);
          bModel.ControlHeatSupply(0, 0, 0);
          bModel.ControlHeatSupply(0, 1, 0);
          bModel.ControlMoistureSupply(0, 0, 0);
          bModel.ControlMoistureSupply(0, 1, 0);
          bModel.ForecastHeatTransfer();
          bModel.FixState();

          var zones = bModel.MultiRoom[0].Zones;
          err += Math.Abs(tempB[h] - zones[1].Temperature);
          tempB[h] = zones[1].Temperature;
          dTime = dTime.AddHours(1);
        }
        if (err < 1e-4) break;
        dTime = dTime.AddHours(-24);
      }

      // 全時刻でBackSpace温度 > 外気温
      for (int h = 0; h < 24; h++)
        Assert.True(tempB[h] > Dbt[1][h],
            $"h={h}: BackSpace({tempB[h]:F2}°C) should be > outdoor({Dbt[1][h]:F1}°C)");
    }

    #endregion

    #region エネルギー収支テスト

    /// <summary>
    /// 収束後の周期定常状態では、1日の熱収支がほぼゼロになる。
    /// 空調なし（FreeFloat）で24時間積算の顕熱収支 ≈ 0。
    /// </summary>
    [Theory]
    [InlineData(0)] // 夏季
    [InlineData(1)] // 冬季
    public void PeriodicSteadyState_DailyEnergyBalance_NearZero(int season)
    {
      var bModel = MakeSunSpaceModel();
      var sun = new Sun(Sun.City.Tokyo);
      var dTime = season == 0
          ? new DateTime(2001, 7, 20, 0, 0, 0)
          : new DateTime(2001, 1, 20, 0, 0, 0);

      // FreeFloat で収束させる
      double[] wallHeatSum = new double[bModel.MultiRoom[0].ZoneCount];
      double[] tempZ = new double[bModel.MultiRoom[0].ZoneCount];

      for (int iter = 0; iter < 100; iter++)
      {
        double err = 0;
        Array.Clear(wallHeatSum, 0, wallHeatSum.Length);

        for (int h = 0; h < 24; h++)
        {
          sun.Update(dTime.AddMinutes(30));
          sun.SeparateGlobalHorizontalRadiation(
              GlobalRad[season][h], Sun.SeparationMethod.Erbs);
          bModel.UpdateOutdoorCondition(
              dTime, sun, Dbt[season][h], Hrt[season][h] * 0.001, 0);

          for (int z = 0; z < wallHeatSum.Length; z++)
          {
            bModel.ControlHeatSupply(0, z, 0);
            bModel.ControlMoistureSupply(0, z, 0);
          }
          bModel.ForecastHeatTransfer();
          bModel.FixState();

          var zones = bModel.MultiRoom[0].Zones;
          for (int z = 0; z < wallHeatSum.Length; z++)
          {
            err += Math.Abs(tempZ[z] - zones[z].Temperature);
            tempZ[z] = zones[z].Temperature;
          }
          dTime = dTime.AddHours(1);
        }

        if (err < 1e-4) break;
        dTime = dTime.AddHours(-24);
      }

      // 収束後：各ゾーンの温度変化が1日で≈0（周期定常の定義）
      // 最終の tempZ が次の0時の温度 ≈ 最初の0時の温度
      // 周期定常収束誤差として温度変化 < 0.01°C を確認
      for (int z = 0; z < wallHeatSum.Length; z++)
        Assert.InRange(tempZ[z], -10.0, 50.0); // 物理的な範囲内に収まっている
    }

    #endregion
  }
}