/* WallAdvancedTests.cs
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
using Popolo.Core.Building.Envelope;

namespace Popolo.Core.Tests.Building.Envelope
{
    /// <summary>Wall の高度機能テスト（PCM・床冷暖房・熱水分同時移動）</summary>
    public class WallAdvancedTests
    {
        #region PCM（潜熱蓄熱材）のテスト

        /// <summary>
        /// PCM入り床構成のサンプル（wallTest2再現）。
        /// 凝固点19°C・融点23°Cのパラフィン系PCM。
        /// 初期16°C→F側30°C境界条件下で昇温させる。
        /// </summary>
        private static Wall MakePCMFloor()
        {
            var layers = new WallLayer[]
            {
                new WallLayer("Flooring",          0.120,   520.0, 0.012),
                new PCMWallLayer("PCM", 19.0, 23.0, 0.02,
                    new WallLayer("Solid",       0.190,  5000.0, 0.02),
                    new WallLayer("Equilibrium", 0.205, 21000.0, 0.02),
                    new WallLayer("Liquid",      0.220,  3000.0, 0.02)),
                new WallLayer("Polystyrene",        0.035,    80.0, 0.020),
                new WallLayer("Plywood",            0.160,   720.0, 0.009),
                new AirGapLayer("Air Gap",          false,         0.050),
                new WallLayer("Plywood",            0.160,   720.0, 0.009),
            };
            var wall = new Wall(5.0, layers);
            wall.TimeStep = 600;
            wall.Initialize(16.0);
            wall.ConvectiveCoefficientF = wall.ConvectiveCoefficientB = 4.8;
            wall.RadiativeCoefficientF  = wall.RadiativeCoefficientB  = 4.5;
            wall.SolAirTemperatureF = 30.0;
            wall.SolAirTemperatureB = 16.0;
            return wall;
        }

        /// <summary>PCMなし同等構成（比較用）</summary>
        private static Wall MakeNoPCMFloor()
        {
            // PCM層をλ=0.205, C=3000の均質層に置き換え
            var layers = new WallLayer[]
            {
                new WallLayer("Flooring",    0.120,  520.0, 0.012),
                new WallLayer("No-PCM",      0.205, 3000.0, 0.020),
                new WallLayer("Polystyrene", 0.035,   80.0, 0.020),
                new WallLayer("Plywood",     0.160,  720.0, 0.009),
                new AirGapLayer("Air Gap",   false,         0.050),
                new WallLayer("Plywood",     0.160,  720.0, 0.009),
            };
            var wall = new Wall(5.0, layers);
            wall.TimeStep = 600;
            wall.Initialize(16.0);
            wall.ConvectiveCoefficientF = wall.ConvectiveCoefficientB = 4.8;
            wall.RadiativeCoefficientF  = wall.RadiativeCoefficientB  = 4.5;
            wall.SolAirTemperatureF = 30.0;
            wall.SolAirTemperatureB = 16.0;
            return wall;
        }

        /// <summary>
        /// PCM領域（19–23°C）通過中に温度変化が緩やかになる。
        /// PCMあり壁はPCMなし壁より同ステップ後の温度上昇が遅い。
        /// </summary>
        [Fact]
        public void PCM_SlowerTemperatureRiseInPhaseChangeZone()
        {
            var pcmWall   = MakePCMFloor();
            var noPcmWall = MakeNoPCMFloor();

            // PCM領域（19-23°C）を通過する時点を確認するため
            // 十分な時間（ステップ数）加熱して中間時刻の温度を比較
            // PCMノードはindex=2（フローリング-PCMの境界）
            const int pcmNodeIndex = 2;

            // 40ステップ（約7時間）加熱
            for (int i = 0; i < 40; i++)
            {
                pcmWall.Update();
                noPcmWall.Update();
            }

            double pcmTemp   = pcmWall.Temperatures[pcmNodeIndex];
            double noPcmTemp = noPcmWall.Temperatures[pcmNodeIndex];

            // PCMあり壁の方が温度上昇が遅い（潜熱が熱を吸収するため）
            Assert.True(pcmTemp < noPcmTemp,
                $"PCM wall ({pcmTemp:F2}°C) should be cooler than no-PCM ({noPcmTemp:F2}°C) at node {pcmNodeIndex}");
        }

        /// <summary>
        /// PCMは最終的に定常状態に収束する（相変化が終われば通常の壁と同様に振る舞う）。
        /// 十分な時間後にPCMノードがF側境界温度に向かって上昇する。
        /// </summary>
        [Fact]
        public void PCM_EventuallyConvergesToSteadyState()
        {
            var wall = MakePCMFloor();
            const int pcmNodeIndex = 2;

            // 初期温度を確認
            double initialTemp = wall.Temperatures[pcmNodeIndex];

            // 144ステップ（24時間）加熱
            for (int i = 0; i < 144; i++) wall.Update();

            double finalTemp = wall.Temperatures[pcmNodeIndex];

            // 最終温度は初期温度より高い
            Assert.True(finalTemp > initialTemp,
                $"Final temp ({finalTemp:F2}°C) should be > initial ({initialTemp:F2}°C)");

            // PCM融点（23°C）を超えて上昇している（相変化完了）
            Assert.True(finalTemp > 23.0,
                $"Final temp ({finalTemp:F2}°C) should exceed melting point (23°C) after 24h");
        }

        #endregion

        #region 床冷暖房（埋設配管）のテスト

        /// <summary>床暖房用の壁（サンプルwallTest3再現）</summary>
        private static Wall MakeRadiantFloor()
        {
            var layers = new WallLayer[]
            {
                new WallLayer("Flooring",    0.120,  520.0, 0.012),
                new WallLayer("Plywood",     0.160,  720.0, 0.009),
                new WallLayer("Polystyrene", 0.035,   80.0, 0.020),
                new WallLayer("Plywood",     0.160,  720.0, 0.009),
                new AirGapLayer("Air Gap",   false,         0.050),
                new WallLayer("Plywood",     0.160,  720.0, 0.009),
            };
            var wall = new Wall(5.0, layers);
            wall.TimeStep = 600;
            wall.Initialize(16.0);
            wall.ConvectiveCoefficientF = wall.ConvectiveCoefficientB = 4.8;
            wall.RadiativeCoefficientF  = wall.RadiativeCoefficientB  = 4.5;
            wall.SolAirTemperatureF = 30.0;
            wall.SolAirTemperatureB = 16.0;
            return wall;
        }

        /// <summary>
        /// 通水後に配管ノード周辺の温度が初期温度より上昇する。
        /// 初期16°C, 水温40°C → 配管ノード1付近が温まる。
        /// </summary>
        [Fact]
        public void RadiantFloor_PipeNodeTemperatureRises()
        {
            var wall = MakeRadiantFloor();
            // サンプルと同じ配管設定（node=1, pitch=0.05m, length=200m, branch=40）
            wall.AddPipe(1, 0.05, 200, 40, 0.004, 0.0046, 0.47);
            wall.SetInletWater(1, 0.014, 40.0); // 水温40°C（暖房）

            double initialPipeTemp = wall.Temperatures[1];

            // 24ステップ（4時間）後
            for (int i = 0; i < 24; i++) wall.Update();

            double finalPipeTemp = wall.Temperatures[1];
            Assert.True(finalPipeTemp > initialPipeTemp,
                $"Pipe node temp ({finalPipeTemp:F2}°C) should rise above initial ({initialPipeTemp:F2}°C)");
        }

        /// <summary>
        /// 配管ノード付近の温度が隣接ノードより高い（温度勾配）。
        /// 床暖房では配管埋設位置（ノード1）が最も温度が高く、
        /// F側（フローリング）とB側（断熱材側）に向かって温度が下がる。
        /// </summary>
        [Fact]
        public void RadiantFloor_PipeNodeIsLocalMaximum()
        {
            var wall = MakeRadiantFloor();
            wall.AddPipe(1, 0.05, 200, 40, 0.004, 0.0046, 0.47);
            wall.SetInletWater(1, 0.014, 40.0);

            for (int i = 0; i < 24; i++) wall.Update();

            double tempNode0 = wall.Temperatures[0]; // F側
            double tempNode1 = wall.Temperatures[1]; // 配管ノード
            double tempNode2 = wall.Temperatures[2]; // B側隣接

            Assert.True(tempNode1 > tempNode0,
                $"Pipe node ({tempNode1:F2}°C) should be > F-side neighbor ({tempNode0:F2}°C)");
            Assert.True(tempNode1 > tempNode2,
                $"Pipe node ({tempNode1:F2}°C) should be > B-side neighbor ({tempNode2:F2}°C)");
        }

        /// <summary>配管からの熱流は正（床への放熱）</summary>
        [Fact]
        public void RadiantFloor_HeatTransferFromPipeIsPositive()
        {
            var wall = MakeRadiantFloor();
            wall.AddPipe(1, 0.05, 200, 40, 0.004, 0.0046, 0.47);
            wall.SetInletWater(1, 0.014, 40.0);

            for (int i = 0; i < 10; i++) wall.Update();

            double heatFromPipe = wall.GetHeatTransferFromPipe(1);
            Assert.True(heatFromPipe > 0,
                $"Heat from pipe ({heatFromPipe:F2} W) should be positive");
        }

        /// <summary>出口水温は入口水温より低い（床への放熱で冷やされる）</summary>
        [Fact]
        public void RadiantFloor_OutletWaterCoolerThanInlet()
        {
            var wall = MakeRadiantFloor();
            wall.AddPipe(1, 0.05, 200, 40, 0.004, 0.0046, 0.47);
            wall.SetInletWater(1, 0.014, 40.0);

            for (int i = 0; i < 10; i++) wall.Update();

            double outletTemp = wall.GetOutletWaterTemperature(1);
            Assert.True(outletTemp < 40.0,
                $"Outlet temp ({outletTemp:F2}°C) should be < inlet (40°C)");
        }

        /// <summary>通水なし（流量0）では配管ノード付近の昇温が小さい</summary>
        [Fact]
        public void RadiantFloor_NoPipeFlow_LessTemperatureRise()
        {
            var wallWithPipe    = MakeRadiantFloor();
            var wallWithoutPipe = MakeRadiantFloor();

            wallWithPipe.AddPipe(1, 0.05, 200, 40, 0.004, 0.0046, 0.47);
            wallWithPipe.SetInletWater(1, 0.014, 40.0);

            for (int i = 0; i < 24; i++)
            {
                wallWithPipe.Update();
                wallWithoutPipe.Update();
            }

            Assert.True(wallWithPipe.Temperatures[1] > wallWithoutPipe.Temperatures[1],
                "Wall with pipe should be warmer at pipe node than wall without pipe");
        }

        #endregion

        #region 熱水分同時移動のテスト

        /// <summary>木繊維板3層構成（wallTest4再現）</summary>
        private static (Wall withMoisture, Wall withoutMoisture) MakeMoistureWalls()
        {
            // 木繊維板: λ=0.1116, C=585, μ=4.694e-6, voidage=0.788, κ=3080, ν=1.715, d=0.006m
            WallLayer[] MakeLayers() => new WallLayer[]
            {
                new WallLayer("FiberBoard", 0.1116, 585.0, 0.000004694, 0.788, 3080.0, 1.715, 0.006),
                new WallLayer("FiberBoard", 0.1116, 585.0, 0.000004694, 0.788, 3080.0, 1.715, 0.006),
                new WallLayer("FiberBoard", 0.1116, 585.0, 0.000004694, 0.788, 3080.0, 1.715, 0.006),
            };

            var wallA = new Wall(1.0, MakeLayers(), computeMoistureTransfer: true);
            var wallB = new Wall(1.0, MakeLayers(), computeMoistureTransfer: false);

            wallA.TimeStep = wallB.TimeStep = 60;
            wallA.Initialize(20.0, 0.008);
            wallB.Initialize(20.0);

            // F側: 対流のみ（放射なし）
            wallA.ConvectiveCoefficientF = wallB.ConvectiveCoefficientF = 4.8;
            wallA.ConvectiveCoefficientB = wallB.ConvectiveCoefficientB = 0.0;
            wallA.RadiativeCoefficientF  = wallB.RadiativeCoefficientF  = 0.0;
            wallA.RadiativeCoefficientB  = wallB.RadiativeCoefficientB  = 0.0;

            // F側に1°Cの温度差を与える
            wallA.SolAirTemperatureF = wallB.SolAirTemperatureF = 21.0;
            wallA.SolAirTemperatureB = wallB.SolAirTemperatureB = 20.0;
            wallA.HumidityRatioF     = wallB.HumidityRatioF     = 0.008;
            wallA.HumidityRatioB     = wallB.HumidityRatioB     = 0.008;

            return (wallA, wallB);
        }

        /// <summary>
        /// 熱水分同時移動モデルは顕熱のみモデルより表面温度変化が遅い。
        /// 潜熱（凝縮・蒸発）が追加の熱容量として機能し、
        /// 温度変化を緩やかにする効果がある。
        /// </summary>
        [Fact]
        public void MoistureTransfer_SlowerTemperatureRise()
        {
            var (wallA, wallB) = MakeMoistureWalls();

            // 240ステップ（4時間）後の表面温度を比較
            for (int i = 0; i < 240; i++)
            {
                wallA.Update();
                wallB.Update();
            }

            double surfA = wallA.SurfaceTemperatureF; // 熱水分同時移動
            double surfB = wallB.SurfaceTemperatureF; // 顕熱のみ

            // 熱水分同時移動モデルは温度変化が遅い（表面温度が低い）
            Assert.True(surfA < surfB,
                $"Moisture model ({surfA:F4}°C) should be cooler than sensible-only ({surfB:F4}°C)");
        }

        /// <summary>
        /// 熱水分同時移動モデルでF側表面の絶対湿度が変化する。
        /// 温度境界の影響で凝縮・蒸発が生じ、湿度分布が初期値から変化する。
        /// </summary>
        [Fact]
        public void MoistureTransfer_HumidityChangesOverTime()
        {
            var (wallA, _) = MakeMoistureWalls();

            double initialHumidity = wallA.Humidities[0];

            for (int i = 0; i < 240; i++) wallA.Update();

            double finalHumidity = wallA.Humidities[0];

            // 初期と異なる湿度分布になる
            Assert.NotEqual(initialHumidity, finalHumidity);
        }

        /// <summary>
        /// 熱水分同時移動モデルの表面温度は物理的な範囲内（境界温度の間）。
        /// </summary>
        [Fact]
        public void MoistureTransfer_SurfaceTemperatureInPhysicalRange()
        {
            var (wallA, _) = MakeMoistureWalls();

            for (int i = 0; i < 240; i++) wallA.Update();

            Assert.InRange(wallA.SurfaceTemperatureF, 20.0, 21.0);
        }

        #endregion
    }
}
