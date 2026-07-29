/* CentrifugalHeatPumpTests.cs
 *
 * Copyright (C) 2026 E.Togashi
 * GNU General Public License v3 – see accompanying LICENSE file.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Popolo.Core.Physics;
using HP = Popolo.Core.HVAC.HeatSource.CentrifugalHeatPump;

namespace Popolo.Core.Tests.HVAC.HeatSource
{
    /// <summary>Unit tests for <see cref="Popolo.Core.HVAC.HeatSource.CentrifugalHeatPump"/>.</summary>
    /// <remarks>
    /// 実在機（R1234yf、定格冷却能力3341 kW、冷水12→7 °C、冷却水671.5 m3/h）のカタログ
    /// 特性を検証データとする。特性表は負荷率10～100%（10%刻み）×冷却水入口温度12～32 °C
    /// の60点で、10%行は容量制御範囲外（発停/ホットガスバイパス領域）のためθの同定に使う。
    ///
    /// 校正フェーズ（static メソッド）:
    ///   EstimateMode                  → 6係数・KA・正規化基準（不等式制約付き最小二乗）
    ///   EstimateHeatingMode           → 冷却特性の援用（η_ht = α・η_ch）
    ///   EstimateMinimumFlowCoefficient→ 容量制御下限1点から ϕ_min
    ///   EstimateCyclingPowerWeight    → 範囲外データから θ
    /// 運転フェーズ（インスタンス）:
    ///   new CentrifugalHeatPump(fluid, E_max, coolingCal, heatingCal, stages)
    ///   Solve(mode, 蒸発器側入口温度/流量, 凝縮器側入口温度/流量, 出口設定値, 回収需要)
    /// </remarks>
    public class CentrifugalHeatPumpTests
    {
        #region Verification data (catalog) and shared fixture

        private const double CapacityN = 3341.0;                  // 定格冷却能力 [kW]
        private const double CopN = 6.2;                          // 定格COP
        private const double PowerN = CapacityN / CopN;           // 定格消費電力 [kW]
        private const int Stages = 2;
        private const Refrigerant.Fluid Fluid = Refrigerant.Fluid.R1234yf;

        private static double Cpw => 0.001 * PhysicsConstants.NominalWaterIsobaricSpecificHeat;
        private static double Mch => CapacityN / (Cpw * 5.0);     // 冷水流量（Δt=5K）[kg/s]
        private static double Mcd => 671.5 * 1000.0 / 3600.0;     // 冷却水流量 [kg/s]

        private static readonly int[] Pls = [10, 20, 30, 40, 50, 60, 70, 80, 90, 100];
        private static readonly double[] Tcds = [32, 28, 24, 20, 16, 12];
        private static readonly double[][] Cop =                  // COP[tcd][pl]
        [
            [ 2.00,  3.70,  5.20,  5.90,  6.30,  6.40,  6.40,  6.40,  6.30,  6.20],
            [ 2.50,  4.80,  6.30,  7.10,  7.40,  7.50,  7.40,  7.30,  7.20,  7.00],
            [ 3.40,  6.20,  8.00,  8.70,  8.90,  8.90,  8.80,  8.60,  8.40,  8.10],
            [ 5.00,  8.60, 10.50, 11.10, 11.20, 11.00, 10.80, 10.40, 10.00,  9.40],
            [ 8.00, 13.30, 14.90, 15.30, 15.00, 14.50, 13.80, 13.00, 12.10, 11.20],
            [16.50, 23.50, 24.00, 23.20, 21.90, 20.30, 18.50, 16.70, 14.90, 13.00],
        ];

        // 熱回収定格点（全量回収：冷水12→7 °C、温水40→45 °C、E=703 kW）
        private static double MchRcv => 573.1 * 1000.0 / 3600.0;
        private static double MhtRcv => 703.1 * 1000.0 / 3600.0;
        private const double ERcv = 703.0;

        private static HP.CatalogPoint Rated => new(12.0, Mch, 32.0, Mcd, CapacityN, PowerN);
        private static HP.CatalogPoint RecoveryRated => new(12.0, MchRcv, 40.0, MhtRcv, MchRcv * Cpw * 5.0, ERcv);
        private static HP.CatalogPoint HeatingRated => new(12.0, MchRcv, 40.0, MhtRcv, MhtRcv * Cpw * 5.0, ERcv);

        private static HP.CatalogPoint Point(int pl, double tcd)
        {
            int i = Array.IndexOf(Tcds, tcd);
            int j = Array.IndexOf(Pls, pl);
            double q = CapacityN * pl / 100.0;
            return new HP.CatalogPoint(7.0 + 5.0 * pl / 100.0, Mch, tcd, Mcd, q, q / Cop[i][j]);
        }

        /// <summary>連続容量制御範囲（負荷率20%以上）の54点。</summary>
        private static List<HP.CatalogPoint> ContinuousPoints()
        {
            var pts = new List<HP.CatalogPoint>();
            foreach (double t in Tcds)
                foreach (int pl in Pls)
                    if (20 <= pl) pts.Add(Point(pl, t));
            return pts;
        }

        /// <summary>範囲外（負荷率10%）の6点。</summary>
        private static List<HP.CatalogPoint> SubRangePoints()
            => Tcds.Select(t => Point(10, t)).ToList();

        private sealed class Rig
        {
            public required HP.ModeCalibration Cooling { get; init; }
            public required HP.ModeCalibration Heating { get; init; }
            public required double EMax { get; init; }
            public required double PhiMin { get; init; }
            public required double Theta { get; init; }
            public required HP Hp { get; init; }
        }

        /// <summary>校正一式（全テストで共有。1回だけ実行される）。</summary>
        private static readonly Lazy<Rig> Shared = new(() =>
        {
            HP.ModeCalibration cal = HP.EstimateMode(
                HP.OperationMode.Cooling, Fluid, Rated, ContinuousPoints(),
                stageCount: Stages, recoveryRated: RecoveryRated);
            double eMax = HP.ResolveMaximumPower(null, PowerN, ERcv);
            double phiMin = HP.EstimateMinimumFlowCoefficient(
                HP.OperationMode.Cooling, Fluid, cal, Point(20, 32.0), Stages);
            double theta = HP.EstimateCyclingPowerWeight(
                HP.OperationMode.Cooling, Fluid, cal, eMax, SubRangePoints(), Stages);

            HP.ModeCalibration calHt = HP.EstimateHeatingMode(
                cal, Fluid, HeatingRated, stageCount: Stages, recoveryRated: HeatingRated);
            calHt.MinimumFlowCoefficient = cal.MinimumFlowCoefficient;
            calHt.CyclingPowerWeight = cal.CyclingPowerWeight;

            return new Rig
            {
                Cooling = cal,
                Heating = calHt,
                EMax = eMax,
                PhiMin = phiMin,
                Theta = theta,
                Hp = new HP(Fluid, eMax, cal, calHt, Stages),
            };
        });

        #endregion

        // ================================================================
        #region Parameter estimation (cooling mode)

        /// <summary>6係数が論文の検証例と一致し、制約（0 ≤ a, 0 ≤ d）が非活性で満たされる。</summary>
        [Fact]
        public void EstimateMode_ReproducesReferenceCoefficients()
        {
            HP.Parameters p = Shared.Value.Cooling.Parameters;
            Assert.InRange(p.a_cmp, 0.1965 - 0.005, 0.1965 + 0.005);
            Assert.InRange(p.b_cmp, 0.1043 - 0.010, 0.1043 + 0.010);
            Assert.InRange(p.c_cmp, 0.3576 - 0.010, 0.3576 + 0.010);
            Assert.InRange(p.d_cmp, 0.2000 - 0.010, 0.2000 + 0.010);
            Assert.InRange(p.e_cmp, -0.6038 - 0.020, -0.6038 + 0.020);
            Assert.InRange(p.f_cmp, 1.2690 - 0.020, 1.2690 + 0.020);
            Assert.False(Shared.Value.Cooling.ConstraintActivated);
            Assert.True(0.998 < Shared.Value.Cooling.RSquared);
        }

        /// <summary>KAと正規化基準が定格点から妥当に推定される。</summary>
        [Fact]
        public void EstimateMode_HeatTransferAndNominalReferences()
        {
            HP.ModeCalibration c = Shared.Value.Cooling;
            Assert.InRange(c.EvaporatorHeatTransferCoefficient, 830.0, 845.0);
            Assert.InRange(c.CondenserHeatTransferCoefficient, 1388.0, 1402.0);
            Assert.InRange(c.RecoveryHeatTransferCoefficient, 1448.0, 1464.0);
            Assert.InRange(c.NominalHead, 8.80, 8.91);
            Assert.InRange(c.NominalFlowVolume, 1.145, 1.170);
        }

        /// <summary>定格点がpointsに含まれない場合には内部で自動追加される（結果は同一）。</summary>
        [Fact]
        public void EstimateMode_AddsRatedPointAutomatically()
        {
            var withoutRated = ContinuousPoints().Where(p => !p.Equals(Rated)).ToList();
            Assert.Equal(53, withoutRated.Count);
            HP.ModeCalibration c = HP.EstimateMode(
                HP.OperationMode.Cooling, Fluid, Rated, withoutRated, stageCount: Stages);
            Assert.Equal(Shared.Value.Cooling.Parameters.a_cmp, c.Parameters.a_cmp, 10);
            Assert.Equal(Shared.Value.Cooling.Parameters.f_cmp, c.Parameters.f_cmp, 10);
        }

        /// <summary>回帰点が7点（定格点含む）未満なら例外。</summary>
        [Fact]
        public void EstimateMode_RequiresSevenPoints()
        {
            var five = ContinuousPoints().Where(p => !p.Equals(Rated)).Take(5).ToList();
            Assert.Throws<ArgumentException>(() =>
                HP.EstimateMode(HP.OperationMode.Cooling, Fluid, Rated, five, stageCount: Stages));
        }

        /// <summary>54点に対する近似性能：MAPE 2%未満、最大誤差率 7%未満。</summary>
        [Fact]
        public void Solve_ApproximatesCatalog_MapeBelow2Percent()
        {
            HP hp = Shared.Value.Hp;
            double sum = 0, max = 0;
            int n = 0;
            foreach (double t in Tcds)
                foreach (int pl in Pls)
                {
                    if (pl < 20) continue;
                    double q = CapacityN * pl / 100.0;
                    double eCat = q / Cop[Array.IndexOf(Tcds, t)][Array.IndexOf(Pls, pl)];
                    HP.Operation op = hp.Solve(HP.OperationMode.Cooling,
                        7.0 + 5.0 * pl / 100.0, Mch, t, Mcd, 7.0);
                    double err = Math.Abs(op.PowerConsumption - eCat) / eCat;
                    sum += err;
                    max = Math.Max(max, err);
                    n++;
                }
            Assert.InRange(sum / n, 0.0, 0.02);
            Assert.InRange(max, 0.0, 0.07);
        }

        #endregion

        // ================================================================
        #region Forward model (cooling mode)

        /// <summary>需要が無い（入口温度=設定値）ときは停止し、消費電力0を返す。</summary>
        [Fact]
        public void Solve_NoDemand_ReturnsZeroPower()
        {
            HP.Operation op = Shared.Value.Hp.Solve(HP.OperationMode.Cooling, 7.0, Mch, 24.0, Mcd, 7.0);
            Assert.Equal(0.0, op.PowerConsumption);
            Assert.Equal(0.0, op.EvaporatorHeat);
        }

        /// <summary>過負荷では消費電力がE_maxに固定され、能力が絞られ、出口温度が設定値を上回る。</summary>
        [Fact]
        public void Solve_Overload_CapsPowerAtMaximum()
        {
            // 冷水入口14 °C＝需要約140%
            HP.Operation op = Shared.Value.Hp.Solve(HP.OperationMode.Cooling, 14.0, Mch, 32.0, Mcd, 7.0);
            Assert.True(op.IsOverloaded);
            Assert.InRange(op.PowerConsumption, Shared.Value.EMax - 1e-3, Shared.Value.EMax + 1e-3);
            Assert.True(op.EvaporatorHeat < Mch * Cpw * 7.0);
            Assert.True(7.0 < op.EvaporatorOutletTemperature);
        }

        /// <summary>小さな熱回収需要は全量回収され、凝縮温度上昇により消費電力が増える。</summary>
        [Fact]
        public void Solve_SmallRecoveryDemand_FullyRecovered()
        {
            double mDmd = 200.0 * 1000.0 / 3600.0;   // 200 m3/h、温水40→45 °C（需要 約1163 kW）
            var dmd = new HP.HeatRecoveryDemand(40.0, mDmd, 45.0);
            HP.Operation op0 = Shared.Value.Hp.Solve(HP.OperationMode.Cooling, 10.0, Mch, 24.0, Mcd, 7.0);
            HP.Operation op = Shared.Value.Hp.Solve(HP.OperationMode.Cooling, 10.0, Mch, 24.0, Mcd, 7.0, dmd);
            Assert.Equal(HP.HeatRecoveryLevel.Full, op.RecoveryLevel);
            Assert.InRange(op.HeatRecovery, mDmd * Cpw * 5.0 - 1.0, mDmd * Cpw * 5.0 + 1.0);
            Assert.True(op0.PowerConsumption < op.PowerConsumption);
            Assert.InRange(op.CondensingTemperature, 45.0, 45.5);
        }

        /// <summary>需要が総排熱を超えると部分回収となり（Eq.42）、冷却水出口温度は入口温度に一致する。</summary>
        [Fact]
        public void Solve_ExcessiveRecoveryDemand_SaturatesAtTotalHeatRejection()
        {
            double mDmd = 500.0 * 1000.0 / 3600.0;   // 500 m3/h（需要 約2907 kW > 総排熱）
            var dmd = new HP.HeatRecoveryDemand(40.0, mDmd, 45.0);
            HP.Operation op = Shared.Value.Hp.Solve(HP.OperationMode.Cooling, 10.0, Mch, 24.0, Mcd, 7.0, dmd);
            Assert.Equal(HP.HeatRecoveryLevel.Partial, op.RecoveryLevel);
            Assert.True(op.HeatRecovery < mDmd * Cpw * 5.0);
            Assert.InRange(op.HeatRecovery, op.CondenserHeat - 1.0, op.CondenserHeat + 1.0);
            Assert.InRange(op.CondenserOutletTemperature, 24.0 - 0.05, 24.0 + 0.05);
        }

        #endregion

        // ================================================================
        #region Out of capacity control range (ϕ_min + Eq.13)

        /// <summary>ϕ_minは容量制御下限の1点から、θは範囲外6点から、参照値どおり同定される。</summary>
        [Fact]
        public void SubRangeParameters_MatchReferenceValues()
        {
            Assert.InRange(Shared.Value.PhiMin, 0.2067 - 0.003, 0.2067 + 0.003);
            Assert.InRange(Shared.Value.Theta, 0.084 - 0.015, 0.084 + 0.015);
            Assert.Equal(Shared.Value.Theta, Shared.Value.Cooling.CyclingPowerWeight, 10);
        }

        /// <summary>範囲外（負荷率10%）のカタログ点をEq.13で±6%以内に再現する。</summary>
        [Fact]
        public void Solve_SubRangeRow_ReproducedWithinSixPercent()
        {
            foreach (double t in Tcds)
            {
                double eCat = CapacityN * 0.10 / Cop[Array.IndexOf(Tcds, t)][0];
                HP.Operation op = Shared.Value.Hp.Solve(HP.OperationMode.Cooling, 7.5, Mch, t, Mcd, 7.0);
                Assert.True(op.IsBelowControlRange);
                Assert.InRange(Math.Abs(op.PowerConsumption - eCat) / eCat, 0.0, 0.06);
            }
        }

        /// <summary>境界をまたいでも消費電力は負荷率に対して単調非減少（Eq.13の単調性保証）。</summary>
        [Fact]
        public void Solve_PowerDecreasesMonotonicallyAcrossControlBoundary()
        {
            double prev = double.MaxValue;
            foreach (int pl in new[] { 25, 20, 15, 10, 5 })
            {
                HP.Operation op = Shared.Value.Hp.Solve(HP.OperationMode.Cooling,
                    7.0 + 5.0 * pl / 100.0, Mch, 32.0, Mcd, 7.0);
                Assert.True(op.PowerConsumption < prev);
                Assert.Equal(pl < 20, op.IsBelowControlRange);
                prev = op.PowerConsumption;
            }
        }

        /// <summary>減載可能下限Q_minは冷却水温度が低いほど小さい（サージ線＝ϕ_min一定の帰結）。</summary>
        [Fact]
        public void Solve_TurndownLimitDeepensAtLowCondenserTemperature()
        {
            double qMin32 = Shared.Value.Hp.Solve(HP.OperationMode.Cooling, 7.4, Mch, 32.0, Mcd, 7.0).MinimumContinuousLoad;
            double qMin24 = Shared.Value.Hp.Solve(HP.OperationMode.Cooling, 7.4, Mch, 24.0, Mcd, 7.0).MinimumContinuousLoad;
            double qMin12 = Shared.Value.Hp.Solve(HP.OperationMode.Cooling, 7.4, Mch, 12.0, Mcd, 7.0).MinimumContinuousLoad;
            Assert.True(qMin24 < qMin32);
            Assert.True(qMin12 < qMin24);
            Assert.InRange(qMin32 / CapacityN, 0.17, 0.23);   // 定格温度条件では約20%（カタログ下限）
            Assert.InRange(qMin12 / CapacityN, 0.08, 0.14);
        }

        /// <summary>ϕ_min未同定でθを推定しようとすると例外。θの設定は[0,1]に制限される。</summary>
        [Fact]
        public void CyclingWeight_Validation()
        {
            HP.ModeCalibration raw = HP.EstimateMode(
                HP.OperationMode.Cooling, Fluid, Rated, ContinuousPoints(), stageCount: Stages);
            Assert.Throws<InvalidOperationException>(() =>
                HP.EstimateCyclingPowerWeight(HP.OperationMode.Cooling, Fluid, raw,
                    Shared.Value.EMax, SubRangePoints(), Stages));
            Assert.Throws<ArgumentOutOfRangeException>(() => raw.CyclingPowerWeight = 1.5);
        }

        #endregion

        // ================================================================
        #region Heating mode (reusing the cooling characteristics)

        /// <summary>αが1.0に近い値で同定され、加熱定格点の消費電力を再現する。</summary>
        [Fact]
        public void EstimateHeatingMode_ReproducesRatedPoint()
        {
            Assert.InRange(Shared.Value.Heating.Alpha, 0.99, 1.005);
            HP.Operation op = Shared.Value.Hp.Solve(HP.OperationMode.Heating,
                12.0, MchRcv, 40.0, MhtRcv, 45.0);
            Assert.InRange(Math.Abs(op.PowerConsumption - ERcv) / ERcv, 0.0, 0.005);
            Assert.InRange(op.CondenserHeat, MhtRcv * Cpw * 5.0 - 1.0, MhtRcv * Cpw * 5.0 + 1.0);
        }

        /// <summary>熱源水温度が低いほど蒸発温度が下がり、消費電力が増える。</summary>
        [Fact]
        public void Solve_HeatingPowerRisesAsSourceTemperatureFalls()
        {
            double prev = 0.0;
            foreach (double ths in new[] { 20.0, 15.0, 10.0, 5.0 })
            {
                HP.Operation op = Shared.Value.Hp.Solve(HP.OperationMode.Heating,
                    ths, MchRcv, 40.0, MhtRcv * 0.6, 45.0);
                Assert.True(prev < op.PowerConsumption);
                prev = op.PowerConsumption;
            }
        }

        #endregion

        // ================================================================
        #region Construction and validation (E_max, constructor, mode selection)

        /// <summary>E_maxの既定値は各モード定格消費電力の最大値。明示値は定格以上1.5倍未満に制限。</summary>
        [Fact]
        public void ResolveMaximumPower_DefaultsAndLimits()
        {
            Assert.Equal(ERcv, HP.ResolveMaximumPower(null, PowerN, ERcv));
            Assert.Equal(800.0, HP.ResolveMaximumPower(800.0, PowerN, ERcv));
            Assert.Throws<ArgumentOutOfRangeException>(() => HP.ResolveMaximumPower(500.0, PowerN, ERcv));
            Assert.Throws<ArgumentOutOfRangeException>(() => HP.ResolveMaximumPower(1100.0, PowerN, ERcv));
        }

        /// <summary>コンストラクタの引数検証。</summary>
        [Fact]
        public void Constructor_Validation()
        {
            HP.ModeCalibration cal = Shared.Value.Cooling;
            Assert.Throws<ArgumentOutOfRangeException>(() => new HP(Fluid, 0.0, cal));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HP(Fluid, 703.0, cal, null, 0));
            Assert.Throws<ArgumentException>(() => new HP(Fluid, 703.0, null, null));
        }

        /// <summary>校正が与えられていないモードのSolveは例外。</summary>
        [Fact]
        public void Solve_ModeWithoutCalibration_Throws()
        {
            var coolingOnly = new HP(Fluid, Shared.Value.EMax, Shared.Value.Cooling);
            Assert.Throws<InvalidOperationException>(() =>
                coolingOnly.Solve(HP.OperationMode.Heating, 12.0, MchRcv, 40.0, MhtRcv, 45.0));
        }

        #endregion
    }
}
