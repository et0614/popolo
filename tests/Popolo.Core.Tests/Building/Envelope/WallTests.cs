/* WallTests.cs
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
    /// <summary>Wall のテスト</summary>
    /// <remarks>
    /// 応答係数法による非定常熱計算のテスト。
    ///
    /// テストに使用する壁構成（RC単層壁）：
    ///   RC 150mm: λ=1.6 W/(m·K), C=1896 kJ/(m³·K)
    ///   総合熱伝達率: h=9.8 W/(m²·K)（対流5.3 + 放射4.5）
    ///
    /// 定常状態での理論値：
    ///   R_wall = 0.15/1.6 ≈ 0.094 m²·K/W
    ///   R_total = 1/9.8 + 0.094 + 1/9.8 ≈ 0.298 m²·K/W
    ///   ΔT=10K → q ≈ 33.6 W/m²
    ///   T_surfF ≈ 26.6°C, T_surfB ≈ 23.4°C
    /// </remarks>
    public class WallTests
    {
        #region テスト用ヘルパー

        /// <summary>RC単層壁を生成する（150mm, λ=1.6, C=1896）</summary>
        private static Wall MakeRCWall(double area = 1.0)
        {
            var layer = new WallLayer("RC", 1.6, 1896.0, 0.15);
            return new Wall(area, new[] { layer });
        }

        /// <summary>多層壁を生成する（RC100mm + 断熱材50mm）</summary>
        private static Wall MakeInsulatedWall()
        {
            var rc   = new WallLayer("RC",          1.600, 1896.0, 0.10);
            var ins  = new WallLayer("Insulation",  0.036,   25.1, 0.05);
            return new Wall(1.0, new[] { rc, ins });
        }

        /// <summary>定常状態に収束するまでUpdateを繰り返す</summary>
        private static void RunToSteadyState(Wall wall, int steps = 200)
        {
            for (int i = 0; i < steps; i++) wall.Update();
        }

        #endregion

        #region コンストラクタのテスト

        /// <summary>コンストラクタでプロパティが正しく設定される</summary>
        [Fact]
        public void Constructor_SetsProperties()
        {
            var wall = MakeRCWall(2.5);

            Assert.Equal(2.5, wall.Area,       precision: 6);
            Assert.Equal(2,   wall.NodeCount); // 1層→質点数=2
            Assert.False(wall.ComputeMoistureTransfer);
        }

        /// <summary>デフォルトの熱伝達率が設定されている</summary>
        [Fact]
        public void Constructor_DefaultFilmCoefficients()
        {
            var wall = MakeRCWall();

            Assert.Equal(9.8, wall.FilmCoefficientF, precision: 6);
            Assert.Equal(9.8, wall.FilmCoefficientB, precision: 6);
        }

        /// <summary>多層壁のノード数が正しい（n層→n+1質点）</summary>
        [Fact]
        public void Constructor_MultiLayer_CorrectNodeNumber()
        {
            var wall = MakeInsulatedWall();
            Assert.Equal(3, wall.NodeCount); // 2層→質点数=3
        }

        #endregion

        #region 初期化のテスト

        /// <summary>Initialize後に全質点の温度が指定値になる</summary>
        [Fact]
        public void Initialize_SetsUniformTemperature()
        {
            var wall = MakeRCWall();
            wall.Initialize(20.0);

            var temps = wall.Temperatures;
            for (int i = 0; i < temps.Length; i++)
                Assert.Equal(20.0, temps[i], precision: 3);
        }

        /// <summary>両側等温で初期化後、表面温度が初期温度に等しい</summary>
        [Fact]
        public void Initialize_BothSidesEqual_SurfaceEqualsInitialTemp()
        {
            var wall = MakeRCWall();
            wall.Initialize(25.0);
            wall.SolAirTemperatureF = 25.0;
            wall.SolAirTemperatureB = 25.0;
            wall.Update();

            Assert.Equal(25.0, wall.SurfaceTemperatureF, precision: 2);
            Assert.Equal(25.0, wall.SurfaceTemperatureB, precision: 2);
        }

        #endregion

        #region 定常状態収束のテスト

        /// <summary>
        /// 両側に温度差を与えて多数ステップ後に定常に収束する。
        /// F側30°C, B側20°C → 定常熱流≒33.6 W/m², 表面温度はその間。
        /// </summary>
        [Fact]
        public void Update_SteadyState_CorrectSurfaceTemperatures()
        {
            var wall = MakeRCWall();
            wall.Initialize(25.0);
            wall.SolAirTemperatureF = 30.0;
            wall.SolAirTemperatureB = 20.0;

            RunToSteadyState(wall);

            // 定常: T_surfF ≈ 26.6°C, T_surfB ≈ 23.4°C（1°C精度で確認）
            Assert.InRange(wall.SurfaceTemperatureF, 25.5, 27.5);
            Assert.InRange(wall.SurfaceTemperatureB, 22.5, 24.5);
        }

        /// <summary>定常状態ではF側表面温度 > B側表面温度（F側が高温境界）</summary>
        [Fact]
        public void Update_SteadyState_FSurfaceWarmerThanBSurface()
        {
            var wall = MakeRCWall();
            wall.Initialize(20.0);
            wall.SolAirTemperatureF = 30.0;
            wall.SolAirTemperatureB = 20.0;

            RunToSteadyState(wall);

            Assert.True(wall.SurfaceTemperatureF > wall.SurfaceTemperatureB,
                $"SurfF={wall.SurfaceTemperatureF:F2} should be > SurfB={wall.SurfaceTemperatureB:F2}");
        }

        /// <summary>定常状態でのF側熱流が正（吸熱）</summary>
        [Fact]
        public void Update_SteadyState_FSurfaceHeatTransferPositive()
        {
            var wall = MakeRCWall();
            wall.Initialize(20.0);
            wall.SolAirTemperatureF = 30.0;
            wall.SolAirTemperatureB = 20.0;

            RunToSteadyState(wall);

            Assert.True(wall.GetSurfaceHeatTransfer(true) > 0,
                "F-side heat flux should be positive (heat absorption)");
        }

        /// <summary>定常状態でのB側熱流が負（放熱）</summary>
        [Fact]
        public void Update_SteadyState_BSurfaceHeatTransferNegative()
        {
            var wall = MakeRCWall();
            wall.Initialize(20.0);
            wall.SolAirTemperatureF = 30.0;
            wall.SolAirTemperatureB = 20.0;

            RunToSteadyState(wall);

            Assert.True(wall.GetSurfaceHeatTransfer(false) < 0,
                "B-side heat flux should be negative (heat dissipation)");
        }

        /// <summary>両側等温では熱流がほぼ0</summary>
        [Fact]
        public void Update_SteadyState_EqualTemperatures_NearZeroHeatFlux()
        {
            var wall = MakeRCWall();
            wall.Initialize(20.0);
            wall.SolAirTemperatureF = 20.0;
            wall.SolAirTemperatureB = 20.0;

            RunToSteadyState(wall);

            Assert.Equal(0.0, wall.GetSurfaceHeatTransfer(true),  precision: 3);
            Assert.Equal(0.0, wall.GetSurfaceHeatTransfer(false), precision: 3);
        }

        #endregion

        #region 断熱性能のテスト

        /// <summary>断熱材入りの壁はRC単層壁より熱流が小さい（同じ境界条件で）</summary>
        [Fact]
        public void Update_InsulatedWall_LowerHeatFlux()
        {
            var rc        = MakeRCWall();
            var insulated = MakeInsulatedWall();

            rc.Initialize(20.0);
            insulated.Initialize(20.0);
            rc.SolAirTemperatureF        = insulated.SolAirTemperatureF = 30.0;
            rc.SolAirTemperatureB        = insulated.SolAirTemperatureB = 20.0;

            RunToSteadyState(rc);
            RunToSteadyState(insulated);

            double qRC        = Math.Abs(rc.GetSurfaceHeatTransfer(true));
            double qInsulated = Math.Abs(insulated.GetSurfaceHeatTransfer(true));

            Assert.True(qInsulated < qRC,
                $"Insulated ({qInsulated:F2} W/m²) should be < RC ({qRC:F2} W/m²)");
        }

        #endregion

        #region 熱伝達率のテスト

        /// <summary>熱伝達率が高いほど表面温度が境界温度に近づく</summary>
        [Fact]
        public void Update_HigherFilmCoefficient_SurfaceCloserToBoundary()
        {
            var wallLow  = MakeRCWall();
            var wallHigh = MakeRCWall();

            wallLow.ConvectiveCoefficientF  = 1.0;
            wallLow.RadiativeCoefficientF   = 0.0;
            wallHigh.ConvectiveCoefficientF = 20.0;
            wallHigh.RadiativeCoefficientF  = 0.0;

            wallLow.Initialize(20.0);
            wallHigh.Initialize(20.0);
            wallLow.SolAirTemperatureF  = wallHigh.SolAirTemperatureF  = 30.0;
            wallLow.SolAirTemperatureB  = wallHigh.SolAirTemperatureB  = 20.0;

            RunToSteadyState(wallLow);
            RunToSteadyState(wallHigh);

            // 高い熱伝達率→表面温度が境界温度（30°C）に近い
            Assert.True(wallHigh.SurfaceTemperatureF > wallLow.SurfaceTemperatureF,
                $"High h ({wallHigh.SurfaceTemperatureF:F2}°C) should be > Low h ({wallLow.SurfaceTemperatureF:F2}°C)");
        }

        #endregion

        #region タイムステップのテスト

        /// <summary>タイムステップを変更してもUpdate後に有効な温度が得られる</summary>
        [Theory]
        [InlineData(900)]    // 15分
        [InlineData(3600)]   // 1時間
        [InlineData(7200)]   // 2時間
        public void Update_DifferentTimeSteps_ValidTemperatures(double timeStep)
        {
            var wall = MakeRCWall();
            wall.TimeStep = timeStep;
            wall.Initialize(20.0);
            wall.SolAirTemperatureF = 30.0;
            wall.SolAirTemperatureB = 20.0;

            // 例外なくUpdateが実行でき、表面温度が物理的範囲内
            for (int i = 0; i < 10; i++) wall.Update();

            Assert.InRange(wall.SurfaceTemperatureF, 20.0, 30.0);
            Assert.InRange(wall.SurfaceTemperatureB, 20.0, 30.0);
        }

        #endregion
    }
}
